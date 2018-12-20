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
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop

type CompilerStatus =
    | Standby
    | Running
    | Failed of FSharpErrorInfo[]
    | Succeeded of string * FSharpErrorInfo[]

type Compiler =
    {
        Checker: FSharpChecker
        Options: FSharpProjectOptions
        CheckResults: FSharpCheckProjectResults
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
        // The first compilation takes longer, so we run one during load
        let! _ = checker.Compile(checkRes)
        return {
            Checker = checker
            Options = options
            CheckResults = checkRes
            Sequence = 0
            Status = Standby
        }
    }

    let IsFailure (errors: seq<FSharpErrorInfo>) =
        errors
        |> Seq.exists (fun (x: FSharpErrorInfo) -> x.Severity = FSharpErrorSeverity.Error)

    let DownloadFile (path: string) = async {
        printfn "Downloading output..."
        try do! JSRuntime.Current.InvokeAsync("WebFsc.getCompiledFile", path) |> Async.AwaitTask
        with exn -> eprintfn "%A" exn
    }

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
            if IsFailure errors || outCode <> 0 then return { comp with Status = Failed (Array.append checkRes.Errors errors) } else
            return
                { comp with
                    Sequence = comp.Sequence + 1
                    Status = Succeeded (outFile, errors) }
        }

    member comp.TriggerCheck(source: string, dispatch: FSharpErrorInfo[] -> unit) =
        checkDelay.Trigger(async {
            let! parseRes, checkRes = comp.Checker.ParseAndCheckFileInProject(inFile, 0, source, comp.Options)
            dispatch [|
                yield! parseRes.Errors
                match checkRes with
                | FSharpCheckFileAnswer.Aborted -> ()
                | FSharpCheckFileAnswer.Succeeded checkRes -> yield! checkRes.Errors
            |]
        })

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
