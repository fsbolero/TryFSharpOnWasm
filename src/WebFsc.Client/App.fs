module WebFsc.Client.App

open System.Net.Http
open Microsoft.AspNetCore.Blazor.Components
open Microsoft.JSInterop
open Elmish
open Bolero
open Bolero.Html

type AppModel =
    | Initializing
    | Running of Main.Model

type AppMessage =
    | InitializeCompiler
    | InitializeEditor
    | CompilerInitialized of Compiler
    | Message of Main.Message
    | Error of exn

/// A wrapper object to allow Ace to dispatch messages on edit
type EditorBinding(dispatch: AppMessage -> unit) =

    [<JSInvokable>]
    member this.SetText(text: string) =
        dispatch (Message (Main.SetText text))

let update http message model =
    match message with
    | InitializeCompiler ->
        model, Cmd.ofAsync (fun src -> async {
            Compiler.SetFSharpDataHttpClient http
            return! Compiler.Create src |> Async.WithYield
        }) Main.defaultSource CompilerInitialized Error
    | CompilerInitialized compiler ->
        Running (Main.initModel compiler), Cmd.ofMsg InitializeEditor
    | InitializeEditor ->
        model,
        Cmd.ofSub(fun dispatch ->
            let onEdit = new DotNetObjectRef(EditorBinding(dispatch))
            JSRuntime.Current.InvokeAsync("WebFsc.initAce", "editor", Main.defaultSource, onEdit)
            |> ignore
        )
    | Message msg ->
        match model with
        | Initializing -> model, [] // Shouldn't happen
        | Running model ->
            let model, cmd = Main.update http msg model
            Running model, Cmd.map Message cmd
    | Error exn ->
        eprintfn "%A" exn
        model, []

let view model dispatch =
    cond model <| function
        | Initializing -> Main.Main.Loader().Text("Initializing compiler...").Elt()
        | Running m -> Main.view m (dispatch << Message)

type MainApp() =
    inherit ProgramComponent<AppModel, AppMessage>()

    [<Inject>]
    member val Http = Unchecked.defaultof<HttpClient> with get, set

    override this.Program =
        Program.mkProgram
            (fun _ -> Initializing, Cmd.ofMsg InitializeCompiler)
            (update this.Http) view
        //|> Program.withConsoleTrace

