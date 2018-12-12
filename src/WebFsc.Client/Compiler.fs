namespace WebFsc.Client

open System
open System.IO
open System.Reflection
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop

type CompilationResult =
    {
        assemblyPath: option<string>
        errors: list<FSharpErrorInfo>
        duration: TimeSpan
    }

type Compiler =
    {
        checker: FSharpChecker
    }

exception CompilationFailed of list<FSharpErrorInfo>

module Compiler =

    let create () =
        {
            checker = FSharpChecker.Create()
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

    let loadAndRun (path: string) = async {
        let asm = Assembly.LoadFrom(path)
        asm.EntryPoint.Invoke(null, [||]) |> ignore
    }

    let run (source: string) (comp: Compiler) = async {
        let inFile = "/tmp/file.fsx"
        let outFile = "/tmp/out.exe"
        let! options, errors = comp.checker.GetProjectOptionsFromScript(inFile, source)
        if isFailure errors then raise (CompilationFailed errors)
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
        if isFailure errors || res <> 0 then raise (CompilationFailed errors)
        return outFile, errors
    }