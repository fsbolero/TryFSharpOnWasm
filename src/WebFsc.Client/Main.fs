module WebFsc.Client.Main

open Microsoft.FSharp.Compiler.SourceCodeServices
open Elmish
open Bolero
open Bolero.Html

type Model =
    {
        text: string
        compiler: Compiler
    }

let initModel =
    {
        text = ""
        compiler = Compiler.create ()
    }

type Message =
    | SetText of string
    | Compile
    | Compiled of string * list<FSharpErrorInfo>
    | Error of exn

let update message model =
    match message with
    | SetText text -> { model with text = text }, []
    | Compile ->
        model,
        Cmd.ofAsync (fun m -> Compiler.run m.text m.compiler) model Compiled Error
    | Compiled (file, errors) ->
        Async.Start (Compiler.loadAndRun file)
        model, []
    | Error (CompilationFailed errors) ->
        model, [] // TODO
    | Error exn ->
        model, [] // TODO

let view model dispatch =
    concat [
        textarea [
            attr.value model.text
            on.change (fun e -> dispatch (SetText (e.Value :?> string)))
        ] []
        button [on.click (fun _ -> dispatch Compile)] [text "Compile"]
    ]

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        //printfn "%s" (System.Reflection.Assembly.GetEntryAssembly().FullName)
        //System.Diagnostics.Trace.TraceWarning("!!!")
        Program.mkProgram (fun _ -> initModel, []) update view
        |> Program.withConsoleTrace
