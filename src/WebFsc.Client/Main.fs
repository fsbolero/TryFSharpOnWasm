module WebFsc.Client.Main

open System.Threading.Tasks
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop
open Elmish
open Bolero
open Bolero.Html

type Model =
    {
        Text: string
        Compiler: Compiler
        Executor: Executor
        Messages: list<FSharpErrorInfo>
        Exception: option<exn>
    }

let defaultSource = "printfn \"Hello, world!\""

let initModel compiler =
    {
        Text = defaultSource
        Compiler = compiler
        Executor = Executor.create ()
        Messages = []
        Exception = None
    }

type Message =
    | SetText of string
    | Compile
    | Compiled of Compiler
    | RunFinished of Executor
    | Checked of list<FSharpErrorInfo>
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
        findPosition message.StartLineAlternate message.StartColumn model.Text,
        findPosition message.EndLineAlternate message.EndColumn model.Text,
        message.StartLineAlternate, message.StartColumn,
        message.EndLineAlternate, message.EndColumn
    )
    :> Task

/// Update the application model.
let update message model =
    match message with
    | SetText text ->
        { model with Text = text },
        Cmd.ofSub <| fun dispatch ->
            model.Compiler.TriggerCheck(text, dispatch << Checked)
    | Compile ->
        let compiler, run = model.Compiler.Run(model.Text)
        { model with Compiler = compiler },
        Cmd.ofAsync (run >> Async.WithYield) () Compiled Error
    | Compiled ({ Status = Succeeded (file, _) } as compiler) ->
        let executor, run = model.Executor.Run(file)
        { model with
            Exception = None
            Executor = executor
            Compiler = compiler
            Messages = compiler.Messages
        },
        Cmd.batch [
            Cmd.attemptTask ScreenOut.Clear () Error
            Cmd.ofAsync (run >> ScreenOut.Wrap) () RunFinished Error
        ]
    | Compiled compiler ->
        { model with
            Exception = None
            Compiler = compiler
            Messages = compiler.Messages
        },
        Cmd.attemptTask ScreenOut.Clear () Error
    | RunFinished executor ->
        { model with Executor = executor },
        []
    | Checked errors ->
        { model with Messages = errors }, []
    | Error exn ->
        { model with
            Exception = Some exn
            Compiler = model.Compiler.MarkAsFailedIfRunning() },
        Cmd.attemptTask ScreenOut.Clear () Error
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
        .Run(fun _ -> dispatch Compile)
        .RunClass(if model.Compiler.IsRunning then "is-loading" else "")
        .Messages(concat [
            text <|
                match model.Compiler.Status with
                | CompilerStatus.Standby -> "Ready."
                | CompilerStatus.Running -> "Compiling..."
                | CompilerStatus.Succeeded _ -> "Compilation succeeded."
                | CompilerStatus.Failed _ -> "Compilation failed."
            forEach (model.Messages
                    |> List.sortByDescending (fun msg -> msg.Severity))
                (fun msg -> compilerMessage msg dispatch)
            cond model.Exception <| function
                | None -> empty
                | Some e -> Main.SimpleMessage().Severity("Error").Message(string e).Elt()
        ])
        .Elt()

type AppMessage =
    | InitializeCompiler
    | InitializeEditor
    | CompilerInitialized of Compiler
    | Message of Message
    | Error of exn

type EditorBinding(dispatch: AppMessage -> unit) =

    [<JSInvokable>]
    member this.SetText(text: string) =
        dispatch (Message (SetText text))

type MyApp() =
    inherit ProgramComponent<option<Model>, AppMessage>()

    let updateApp message model =
        match message with
        | InitializeCompiler ->
            model, Cmd.ofAsync (Compiler.Create >> Async.WithYield) defaultSource CompilerInitialized Error
        | CompilerInitialized compiler ->
            Some (initModel compiler), Cmd.ofMsg InitializeEditor
        | InitializeEditor ->
            model,
            Cmd.ofSub(fun dispatch ->
                let onEdit = new DotNetObjectRef(EditorBinding(dispatch))
                JSRuntime.Current.InvokeAsync("WebFsc.initAce", "editor", defaultSource, onEdit)
                |> ignore
            )
        | Message msg ->
            match model with
            | None -> model, [] // Shouldn't happen
            | Some model ->
                let model, cmd = update msg model
                Some model, Cmd.map Message cmd
        | Error exn ->
            eprintfn "%A" exn
            model, []

    let viewApp model dispatch =
        cond model <| function
            | None -> text "Initializing compiler..."
            | Some m -> view m (dispatch << Message)

    override this.Program =
        Program.mkProgram
            (fun _ -> None, Cmd.ofMsg InitializeCompiler)
            updateApp viewApp
        //|> Program.withConsoleTrace
