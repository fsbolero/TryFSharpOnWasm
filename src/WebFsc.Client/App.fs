module WebFsc.Client.App

open System.Net.Http
open Microsoft.AspNetCore.Blazor.Components
open Microsoft.JSInterop
open WebFsc.Client.Main
open Elmish
open Bolero
open Bolero.Html

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

type AppModel =
    | Initializing
    | Running of Model

type MainApp() =
    inherit ProgramComponent<AppModel, AppMessage>()

    [<Inject>]
    member val Http = Unchecked.defaultof<HttpClient> with get, set

    member this.UpdateApp message model =
        match message with
        | InitializeCompiler ->
            model, Cmd.ofAsync (Compiler.Create >> Async.WithYield) defaultSource CompilerInitialized Error
        | CompilerInitialized compiler ->
            Running (initModel compiler), Cmd.ofMsg InitializeEditor
        | InitializeEditor ->
            model,
            Cmd.ofSub(fun dispatch ->
                let onEdit = new DotNetObjectRef(EditorBinding(dispatch))
                JSRuntime.Current.InvokeAsync("WebFsc.initAce", "editor", defaultSource, onEdit)
                |> ignore
            )
        | Message msg ->
            match model with
            | Initializing -> model, [] // Shouldn't happen
            | Running model ->
                let model, cmd = update this.Http msg model
                Running model, Cmd.map Message cmd
        | Error exn ->
            eprintfn "%A" exn
            model, []

    member this.ViewApp model dispatch =
        cond model <| function
            | Initializing -> Main.Main.Loader().Text("Initializing compiler...").Elt()
            | Running m -> view m (dispatch << Message)

    override this.Program =
        Program.mkProgram
            (fun _ -> Initializing, Cmd.ofMsg InitializeCompiler)
            this.UpdateApp this.ViewApp
        //|> Program.withConsoleTrace

