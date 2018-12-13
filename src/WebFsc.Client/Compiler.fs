namespace WebFsc.Client

open System
open System.IO
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop

type CompilerStatus =
    | Standby
    | Running
    | Failed of list<FSharpErrorInfo>
    | Succeeded of string * list<FSharpErrorInfo>

type Compiler =
    {
        Checker: FSharpChecker
        Options: FSharpProjectOptions
        Sequence: int
        Status: CompilerStatus
    }

exception CompilationFailed of list<FSharpErrorInfo>

module Compiler =

    let inFile = "/tmp/file.fsx"

    let Create () = async {
        let checker = FSharpChecker.Create()
        let! options, _ = checker.GetProjectOptionsFromScript(inFile, "")
        // Do a dummy check to initialize the checker
        let! _ = checker.ParseAndCheckFileInProject(inFile, 0, "", options)
        return {
            Checker = checker
            Options = options
            Sequence = 0
            Status = Standby
        }
    }

    let IsFailure (errors: seq<FSharpErrorInfo>) =
        errors
        |> Seq.exists (fun (x: FSharpErrorInfo) -> x.Severity = FSharpErrorSeverity.Error)

    let FailIfError errors =
        if IsFailure errors then raise (CompilationFailed errors)

    let DownloadFile (path: string) = async {
        printfn "Downloading output..."
        try do! JSRuntime.Current.InvokeAsync("WebFsc.getCompiledFile", path) |> Async.AwaitTask
        with exn -> eprintfn "%A" exn
    }

    let checkDelay = Delayer(200)

open Compiler

type Compiler with

    member comp.Run(source: string) =
        { comp with Status = Running },
        fun () -> async {
            let outFile = sprintf "/tmp/out%i.exe" comp.Sequence
            //let! options, errors = comp.Checker.GetProjectOptionsFromScript(inFile, source)
            //if IsFailure errors then return { comp with Status = Failed errors } else
            File.WriteAllText(inFile, source)
            let! errors, res =
                comp.Checker.Compile([|
                    yield @"/tmp/fsc.exe"
                    yield! comp.Options.SourceFiles
                    yield! comp.Options.OtherOptions
                    yield "-o"
                    yield outFile
                |])
            let errors = List.ofArray errors
            if IsFailure errors || res <> 0 then return { comp with Status = Failed errors } else
            return
                { comp with
                    Sequence = comp.Sequence + 1
                    Status = Succeeded (outFile, errors) }
        }

    member comp.TriggerCheck(source: string, dispatch: list<FSharpErrorInfo> -> unit) =
        checkDelay.Trigger(async {
            //let! options, projErrors = comp.Checker.GetProjectOptionsFromScript(inFile, source)
            let! parseRes, checkRes = comp.Checker.ParseAndCheckFileInProject(inFile, 0, source, comp.Options)
            dispatch [
                //yield! projErrors
                yield! parseRes.Errors
                match checkRes with
                | FSharpCheckFileAnswer.Aborted -> ()
                | FSharpCheckFileAnswer.Succeeded checkRes -> yield! checkRes.Errors
            ]
        })

    member comp.Messages =
        match comp.Status with
        | Standby | Running -> []
        | Succeeded(_, m) | Failed m -> m

    member comp.IsRunning =
        comp.Status = Running