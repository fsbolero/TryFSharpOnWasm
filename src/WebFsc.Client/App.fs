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

/// MVU for the full application, including loading screen.
module WebFsc.Client.App

open System.Net.Http
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop
open Elmish
open Bolero
open Bolero.Html
open Bolero.Templating.Client

type AppModel =
    /// The app is initializing (show a loading screen).
    | Initializing
    /// The app is loaded and running.
    | Running of Main.Model

type AppMessage =
    | InitializeCompiler
    | InitializeEditor of snippetId: option<string>
    | CompilerInitialized of Compiler
    | Message of Main.Message
    | Error of exn

let sourceDuringLoad snippetId =
    if Option.isSome snippetId then "" else Main.defaultSource

let update (js: IJSInProcessRuntime) http message model =
    match message with
    | InitializeCompiler ->
        model, Cmd.ofAsync (fun src -> async {
            Compiler.SetFSharpDataHttpClient http
            return! Compiler.Create src |> Async.WithYield
        }) Main.defaultSource CompilerInitialized Error
    | CompilerInitialized compiler ->
        let snippetId = js.GetQueryParam "snippet"
        let initSource = sourceDuringLoad snippetId
        let initSnippetId = defaultArg snippetId Main.defaultSnippetId
        Running (Main.initModel compiler initSource initSnippetId),
        Cmd.ofMsg (InitializeEditor snippetId)
    | InitializeEditor snippetId ->
        model,
        Cmd.ofSub(fun dispatch ->
            let onEdit = dispatch << Message << Main.SetText
            js.Invoke<unit>("WebFsc.initAce", "editor",
                sourceDuringLoad snippetId,
                Callback.Of onEdit,
                DotNetObjectReference.Create(Autocompleter(dispatch << Message << Main.Complete, js)))
            let onSetSnippet = dispatch << Message << Main.LoadSnippet << Option.defaultValue Main.defaultSnippetId << Option.ofObj
            Option.iter onSetSnippet snippetId
            js.ListenToQueryParam("snippet", onSetSnippet)
        )
    | Message msg ->
        match model with
        | Initializing -> model, [] // Shouldn't happen
        | Running model ->
            let model, cmd = Main.update js http msg model
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
            (update (this.JSRuntime :?> _) this.Http) view
#if DEBUG
        |> Program.withHotReloading
#endif

