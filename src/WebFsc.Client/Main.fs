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
        compiling: bool
        messages: list<FSharpErrorInfo>
        exn: option<exn>
    }

let initModel =
    {
        text = "printfn \"Hello, world!\""
        compiler = Compiler.create ()
        compiling = false
        messages = []
        exn = None
    }

type Message =
    | SetText of string
    | Compile
    | Compiled of string * list<FSharpErrorInfo>
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
        { model with compiling = true },
        Cmd.ofAsync (fun m -> async {
            do! Async.Sleep 100 // let the UI update and show loading
            return! Compiler.run m.text m.compiler
        }) model Compiled Error
    | Compiled (file, messages) ->
        { model with
            messages = messages
            exn = None
            compiling = false },
        Cmd.ofAsyncIgnore Compiler.loadAndRun file Error
    | Error (CompilationFailed messages) ->
        { model with
            messages = messages
            exn = None
            compiling = false },
        []
    | Error exn ->
        { model with
            exn = Some exn
            compiling = false },
        []
    | SelectMessage message ->
        model,
        Cmd.ofTaskIgnore (selectMessage model) message Error

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
        .RunClass(if model.compiling then "is-loading" else "")
        .Messages(concat [
            forEach (model.messages |> List.sortByDescending (fun msg -> msg.Severity))
                (fun msg -> compilerMessage msg dispatch)
            cond model.exn <| function
                | None -> empty
                | Some e -> Main.SimpleMessage().Message(string e).Elt()
        ])
        .Elt()

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        Program.mkProgram (fun _ -> initModel, []) update view
        |> Program.withConsoleTrace
