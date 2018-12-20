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

type CompilerStatus =
    | Standby
    | Running
    | Failed of FSharpErrorInfo[]
    | Succeeded of string * FSharpErrorInfo[]

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

    let projFile = "/tmp/out.fsproj"
    let inFile = "/tmp/Main.fs"
    let outFile = "/tmp/out.exe"

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
            "-r:/tmp/mscorlib.dll"
            "-r:/tmp/netstandard.dll"
            "-r:/tmp/System.dll"
            "-r:/tmp/System.Core.dll"
            "-r:/tmp/System.IO.dll"
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

    let IsFailure (errors: seq<FSharpErrorInfo>) =
        errors
        |> Seq.exists (fun (x: FSharpErrorInfo) -> x.Severity = FSharpErrorSeverity.Error)

    let DownloadFile (path: string) =
        printfn "Downloading output..."
        try JS.Invoke<unit>("WebFsc.getCompiledFile", path)
        with exn -> eprintfn "%A" exn

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

    let findAsyncMain (checkRes: FSharpCheckProjectResults) =
        match checkRes.AssemblySignature.FindEntityByPath ["Main"] with
        | Some m ->
            m.MembersFunctionsAndValues
            |> Seq.exists (fun v ->
                v.IsModuleValueOrMember &&
                v.FullType.Format(FSharpDisplayContext.Empty) = asyncMainTypeName
            )
        | None -> false

    /// Filter out "Main module of program is empty: nothing will happen when it is run"
    /// when the program has an AsyncMain : unit -> Async<unit>.
    let filterNoMainMessage checkRes (errors: FSharpErrorInfo[]) =
        if findAsyncMain checkRes then
            errors |> Array.filter (fun m -> m.ErrorNumber <> 988)
        else
            errors

    let checkDelay = Delayer(500)

open Compiler

type Compiler with

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

    member comp.Autocomplete(line: int, col: int, lineText: string) = async {
        let partialName = QuickParse.GetPartialLongNameEx(lineText, col)
        let! res = comp.MainFile.Check.GetDeclarationListInfo(Some comp.MainFile.Parse, line, lineText, partialName)
        return res.Items
    }

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
