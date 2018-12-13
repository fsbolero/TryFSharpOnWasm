module WebFsc.Client.Main

open System.Threading.Tasks
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop
open Elmish
open Bolero
open Bolero.Html

type Model =
    {
        text: string
        compiler: Compiler
        executor: Executor
        exn: option<exn>
    }

let initModel =
    {
        text = "printfn \"Hello, world!\""
        compiler = Compiler.create ()
        executor = Executor.create ()
        exn = None
    }

type Message =
    | SetText of string
    | Compile
    | Compiled of Compiler
    | RunFinished of Executor
    | Error of exn
    | SelectMessage of FSharpErrorInfo

/// Find the linear position corresponding to the given line and column (base 1) in the given text.
let findPosition (line: int) (col: int) (text: string) =
    let rec go pos l =
        if l = line then
            pos + col
        else
            match text.IndexOf('\n', pos) with
            | -1 -> text.Length // position is beyond the end of text
            | pos -> go (pos + 1) (l + 1)
    go 0 1

/// Select this message in the textarea.
let selectMessage (model: Model) (message: FSharpErrorInfo) =
    JSRuntime.Current.InvokeAsync("WebFsc.selectMessage",
        findPosition message.StartLineAlternate message.StartColumn model.text,
        findPosition message.EndLineAlternate message.EndColumn model.text,
        message.StartLineAlternate, message.StartColumn,
        message.EndLineAlternate, message.EndColumn
    )
    :> Task

/// Update the application model.
let update message model =
    match message with
    | SetText text ->
        { model with text = text }, []
    | Compile ->
        let compiler, run = model.compiler.Run(model.text)
        { model with compiler = compiler },
        Cmd.ofAsync (run >> Async.WithYield) () Compiled Error
    | Compiled ({ status = Succeeded (file, _) } as compiler) ->
        let executor, run = model.executor.Run(file)
        { model with exn = None; executor = executor; compiler = compiler },
        Cmd.batch [
            Cmd.attemptTask Stdout.clear () Error
            Cmd.ofAsync run () RunFinished Error
        ]
    | Compiled compiler ->
        { model with exn = None; compiler = compiler },
        Cmd.attemptTask Stdout.clear () Error
    | RunFinished executor ->
        { model with executor = executor },
        []
    | Error exn ->
        { model with exn = Some exn },
        Cmd.attemptTask Stdout.clear () Error
    | SelectMessage message ->
        model,
        Cmd.attemptTask (selectMessage model) message Error

type Main = Template<"main.html">

let compilerMessage (msg: FSharpErrorInfo) dispatch =
    Main.CompilerMessage()
        .Severity(string msg.Severity)
        .StartLine(string msg.StartLineAlternate)
        .StartColumn(string msg.StartColumn)
        .EndLine(string msg.EndLineAlternate)
        .EndColumn(string msg.EndColumn)
        .Message(msg.Message)
        .Select(fun _ -> dispatch (SelectMessage msg))
        .Elt()

let view model dispatch =
    Main()
        .Source(model.text, fun s -> dispatch (SetText s))
        .Run(fun _ -> dispatch Compile)
        .RunClass(if model.compiler.IsRunning then "is-loading" else "")
        .Messages(concat [
            text <|
                match model.compiler.status with
                | CompilerStatus.Standby -> "Ready."
                | CompilerStatus.Running -> "Compiling..."
                | CompilerStatus.Succeeded _ -> "Compilation succeeded."
                | CompilerStatus.Failed _ -> "Compilation failed."
            forEach (model.compiler.Messages
                    |> List.sortByDescending (fun msg -> msg.Severity))
                (fun msg -> compilerMessage msg dispatch)
            cond model.exn <| function
                | None -> empty
                | Some e -> Main.SimpleMessage().Severity("Error").Message(string e).Elt()
        ])
        .Elt()

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        Program.mkProgram
            (fun _ -> initModel, Cmd.attemptFunc Stdout.init () Error)
            update view
