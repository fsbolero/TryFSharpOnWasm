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
        checker: FSharpChecker
        sequence: int
        status: CompilerStatus
    }

exception CompilationFailed of list<FSharpErrorInfo>

module Compiler =

    let create () =
        {
            checker = FSharpChecker.Create()
            sequence = 0
            status = Standby
        }

    let isFailure (errors: seq<FSharpErrorInfo>) =
        errors
        |> Seq.exists (fun (x: FSharpErrorInfo) -> x.Severity = FSharpErrorSeverity.Error)

    let failIfError errors =
        if isFailure errors then raise (CompilationFailed errors)

    let downloadFile (path: string) = async {
        printfn "Downloading output..."
        try do! JSRuntime.Current.InvokeAsync("WebFsc.getCompiledFile", path) |> Async.AwaitTask
        with exn -> eprintfn "%A" exn
    }

type Compiler with

    member comp.Run (source: string) =
        { comp with status = Running },
        fun () -> async {
            let inFile = "/tmp/file.fsx"
            let outFile = sprintf "/tmp/out%i.exe" comp.sequence
            let! options, errors = comp.checker.GetProjectOptionsFromScript(inFile, source)
            if Compiler.isFailure errors then return { comp with status = Failed errors } else
            File.WriteAllText(inFile, source)
            let! errors, res =
                comp.checker.Compile([|
                    yield @"/tmp/fsc.exe"
                    yield! options.SourceFiles
                    yield! options.OtherOptions
                    yield "-o"
                    yield outFile
                |])
            let errors = List.ofArray errors
            if Compiler.isFailure errors || res <> 0 then return { comp with status = Failed errors } else
            return
                { comp with
                    sequence = comp.sequence + 1
                    status = Succeeded (outFile, errors) }
        }

    member comp.Messages =
        match comp.status with
        | Standby | Running -> []
        | Succeeded(_, m) | Failed m -> m

    member comp.IsRunning =
        comp.status = Running