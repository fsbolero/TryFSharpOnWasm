// $begin{copyright}
//
// Copyright (c) 2018 IntelliFactory and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebFsc.Client

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop

type CompilerStatus =
    | Standby
    | Running
    | Failed of FSharpErrorInfo[]
    | Succeeded of string * FSharpErrorInfo[]

/// Cache the parse and check results for a given file.
type FileResults =
    {
        Parse: FSharpParseFileResults
        Check: FSharpCheckFileResults
    }

module FileResults =

    let OfRes (parseRes, checkRes) =
        {
            Parse = parseRes
            Check = checkRes
        }

/// The compiler's state.
type Compiler =
    {
        Checker: FSharpChecker
        Options: FSharpProjectOptions
        CheckResults: FSharpCheckProjectResults
        MainFile: FileResults
        Sequence: int
        Status: CompilerStatus
    }

module Compiler =

    /// Dummy project file path needed by the checker API. This file is never actually created.
    let projFile = "/tmp/out.fsproj"
    /// The input F# source file path.
    let inFile = "/tmp/Main.fs"
    /// The default output assembly file path.
    let outFile = "/tmp/out.exe"

    /// <summary>
    /// Create checker options.
    /// </summary>
    /// <param name="checker">The F# code checker.</param>
    /// <param name="outFile"></param>
    let Options (checker: FSharpChecker) (outFile: string) =
        checker.GetProjectOptionsFromCommandLineArgs(projFile, [|
            "--simpleresolution"
            "--optimize-"
            "--noframework"
            "--fullpaths"
            "--warn:3"
            "--target:exe"
            inFile
            // Necessary standard library
            "-r:/tmp/FSharp.Core.dll"
            "-r:/tmp/mscorlib.dll"
            "-r:/tmp/netstandard.dll"
            "-r:/tmp/System.dll"
            "-r:/tmp/System.Core.dll"
            "-r:/tmp/System.IO.dll"
            "-r:/tmp/System.Numerics.dll"
            "-r:/tmp/System.Runtime.dll"
            // Additional libraries we want to make available
            "-r:/tmp/System.Net.Http.dll"
            "-r:/tmp/System.Threading.dll"
            "-r:/tmp/System.Threading.Tasks.dll"
            "-r:/tmp/FSharp.Data.dll"
            "-r:/tmp/System.Xml.Linq.dll"
            "-r:/tmp/WebFsc.Env.dll"
            "-o:" + outFile
        |])

    /// <summary>
    /// Create a compiler instance.
    /// </summary>
    /// <param name="source">The initial contents of Main.fs</param>
    let Create source = async {
        let checker = FSharpChecker.Create(keepAssemblyContents = true)
        let options = Options checker outFile
        File.WriteAllText(inFile, source)
        let! checkRes = checker.ParseAndCheckProject(options)
        let! fileRes = checker.GetBackgroundCheckResultsForFileInProject(inFile, options)
        // The first compilation takes longer, so we run one during load
        let! _ = checker.Compile(checkRes)
        return {
            Checker = checker
            Options = options
            CheckResults = checkRes
            MainFile = FileResults.OfRes fileRes
            Sequence = 0
            Status = Standby
        }
    }

    /// <summary>
    /// Check whether compilation has failed.
    /// </summary>
    /// <param name="errors">The messages returned by the compiler</param>
    let IsFailure (errors: seq<FSharpErrorInfo>) =
        errors
        |> Seq.exists (fun (x: FSharpErrorInfo) -> x.Severity = FSharpErrorSeverity.Error)

    /// <summary>
    /// Turn a file in the virtual filesystem into a browser download.
    /// </summary>
    /// <param name="path">The file's location in the virtual filesystem</param>
    let DownloadFile (js: IJSInProcessRuntime) (path: string) =
        printfn "Downloading output..."
        try js.Invoke<unit>("WebFsc.getCompiledFile", path)
        with exn -> eprintfn "%A" exn

    /// <summary>
    /// Set the HttpClient used by FSharp.Data and by user code.
    /// </summary>
    /// <param name="http"></param>
    let SetFSharpDataHttpClient http =
        // Set the FSharp.Data run time HttpClient
        FSharp.Data.Http.Client <- http
        // Set the FSharp.Data design time HttpClient
        let asm = System.Reflection.Assembly.LoadFrom("/tmp/FSharp.Data.DesignTime.dll")
        let ty = asm.GetType("FSharp.Data.Http")
        let prop = ty.GetProperty("Client", BindingFlags.Static ||| BindingFlags.Public)
        prop.GetSetMethod().Invoke(null, [|http|])
        |> ignore
        // Set the user run time HttpClient
        Env.SetHttp http

    let asyncMainTypeName = "Microsoft.FSharp.Core.unit -> \
                            Microsoft.FSharp.Control.Async<Microsoft.FSharp.Core.unit>"

    /// <summary>
    /// Check whether the code contains a function <c>Main.AsyncMain : unit -> Async&lt;unit&gt;</c>.
    /// </summary>
    /// <param name="checkRes">The compiler check results</param>
    let findAsyncMain (checkRes: FSharpCheckProjectResults) =
        match checkRes.AssemblySignature.FindEntityByPath ["Main"] with
        | Some m ->
            m.MembersFunctionsAndValues
            |> Seq.exists (fun v ->
                v.IsModuleValueOrMember &&
                v.LogicalName = "AsyncMain" &&
                v.FullType.Format(FSharpDisplayContext.Empty) = asyncMainTypeName
            )
        | None -> false

    /// <summary>
    /// Filter out "Main module of program is empty: nothing will happen when it is run"
    /// when the program has a function <c>Main.AsyncMain : unit -> Async&lt;unit&gt;</c>.
    /// </summary>
    /// <param name="checkRes">The compiler check results</param>
    /// <param name="errors">The parse and check messages</param>
    let filterNoMainMessage checkRes (errors: FSharpErrorInfo[]) =
        if findAsyncMain checkRes then
            errors |> Array.filter (fun m -> m.ErrorNumber <> 988)
        else
            errors

    /// The delayer for triggering type checking on user input.
    let checkDelay = Delayer(500)

open Compiler

type Compiler with

    /// <summary>
    /// Compile an assembly.
    /// </summary>
    /// <param name="source">The source of Main.fs.</param>
    /// <returns>The compiler in "Running" mode and the callback to complete the compilation</returns>
    member comp.Run(source: string) =
        { comp with Status = Running },
        fun () -> async {
            let start = DateTime.Now
            let outFile = sprintf "/tmp/out%i.exe" comp.Sequence
            File.WriteAllText(inFile, source)
            // We need to recompute the options because we're changing the out file
            let options = Compiler.Options comp.Checker outFile
            let! checkRes = comp.Checker.ParseAndCheckProject(options)
            if IsFailure checkRes.Errors then return { comp with Status = Failed checkRes.Errors } else
            let! errors, outCode = comp.Checker.Compile(checkRes)
            let finish = DateTime.Now
            printfn "Compiled in %A" (finish - start)
            let errors =
                Array.append checkRes.Errors errors
                |> filterNoMainMessage checkRes
            if IsFailure errors || outCode <> 0 then return { comp with Status = Failed errors } else
            return
                { comp with
                    Sequence = comp.Sequence + 1
                    Status = Succeeded (outFile, errors) }
        }

    /// <summary>
    /// Trigger code checking.
    /// Includes auto-delay, so can (and should) be called on every user input.
    /// </summary>
    /// <param name="source">The source of Main.fs</param>
    /// <param name="dispatch">The callback to dispatch the results</param>
    member comp.TriggerCheck(source: string, dispatch: Compiler * FSharpErrorInfo[] -> unit) =
        checkDelay.Trigger(async {
            let! parseRes, checkRes = comp.Checker.ParseAndCheckFileInProject(inFile, 0, source, comp.Options)
            let checkRes =
                match checkRes with
                | FSharpCheckFileAnswer.Succeeded res -> res
                | FSharpCheckFileAnswer.Aborted -> comp.MainFile.Check
            dispatch
                ({ comp with MainFile = FileResults.OfRes (parseRes, checkRes) },
                Array.append parseRes.Errors checkRes.Errors)
        })

    /// <summary>
    /// Get autocompletion items.
    /// </summary>
    /// <param name="line">The line where code has been input</param>
    /// <param name="col">The column where code has been input</param>
    /// <param name="lineText">The text of the line that has changed</param>
    member comp.Autocomplete(line: int, col: int, lineText: string) = async {
        let partialName = QuickParse.GetPartialLongNameEx(lineText, col)
        let! res = comp.MainFile.Check.GetDeclarationListInfo(Some comp.MainFile.Parse, line, lineText, partialName)
        return res.Items
    }

    /// The warnings and errors from the latest check.
    member comp.Messages =
        match comp.Status with
        | Standby | Running -> [||]
        | Succeeded(_, m) | Failed m -> m

    member comp.IsRunning =
        comp.Status = Running

    member comp.MarkAsFailedIfRunning() =
        match comp.Status with
        | Running -> { comp with Status = Failed [||] }
        | _ -> comp
