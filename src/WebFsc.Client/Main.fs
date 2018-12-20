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

module WebFsc.Client.Main

open System.Threading.Tasks
open System.Net.Http
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
        Messages: FSharpErrorInfo[]
        Exception: option<exn>
        SelectedSnippet: string
    }

let defaultSnippetId = "HelloWorld"
let defaultSource = "printfn \"Hello, world!\"\n"

let snippets =
    [
        "HelloWorld", "Hello, world!"
        "Arithmetic", "Arithmetic"
        "Http", "HTTP Requests"
        "TP_Json", "JSON Type Provider"
        "TP_Xml", "XML Type Provider"
    ]

let initModel compiler initSource initSnippetId =
    {
        Text = initSource
        Compiler = compiler
        Executor = Executor.create ()
        Messages = [||]
        Exception = None
        SelectedSnippet = initSnippetId
    }

type Message =
    | SetText of string
    | Compile
    | Compiled of Compiler
    | RunFinished of Executor
    | Checked of Compiler * FSharpErrorInfo[]
    | Complete of int * int * string * (FSharpDeclarationListItem[] -> unit)
    | Error of exn
    | SelectMessage of FSharpErrorInfo
    | LoadSnippet of string
    | SnippetLoaded of string

/// A wrapper object to trigger autocompletion
type Autocompleter(dispatch: int * int * string * (FSharpDeclarationListItem[] -> unit) -> unit) =
    [<JSInvokable>]
    member this.Complete(line, col, lineText) =
        let tcs = TaskCompletionSource<Ace.Completion[]>()
        dispatch (line, col, lineText,
            tcs.SetResult << Array.map (fun item ->
                {
                    caption = item.Name
                    value = item.NameInCode
                    meta = string item.Glyph
                }))
        tcs.Task

/// Update the application model.
let update (http: HttpClient) message model =
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
            Cmd.attemptFunc ScreenOut.Clear () Error
            Cmd.ofAsync (run >> ScreenOut.Wrap) () RunFinished Error
        ]
    | Compiled compiler ->
        { model with
            Exception = None
            Compiler = compiler
            Messages = compiler.Messages
        },
        Cmd.attemptFunc ScreenOut.Clear () Error
    | RunFinished executor ->
        { model with Executor = executor },
        []
    | Checked (compiler, errors) ->
        { model with Compiler = compiler; Messages = errors },
        Cmd.attemptFunc Ace.SetAnnotations errors Error
    | Complete (line, col, lineText, callback) ->
        model,
        Cmd.attemptAsync (fun args -> async {
            let! res = model.Compiler.Autocomplete args
            callback res
        }) (line, col, lineText) Error
    | Error exn ->
        { model with
            Exception = Some exn
            Compiler = model.Compiler.MarkAsFailedIfRunning() },
        Cmd.attemptFunc ScreenOut.Clear () Error
    | SelectMessage message ->
        model,
        Cmd.attemptFunc Ace.SelectMessage message Error
    | LoadSnippet snippetId ->
        { model with SelectedSnippet = snippetId },
        Cmd.ofTask
            (fun (s: string) -> http.GetStringAsync(s))
            (sprintf "samples/%s.fsx" snippetId)
            SnippetLoaded Error
    | SnippetLoaded text ->
        model,
        Cmd.attemptFunc
            (fun () ->
                JS.Invoke<unit>("WebFsc.setText", text)
                JS.Invoke<unit>("WebFsc.setQueryParam", "snippet", model.SelectedSnippet)
            ) () Error

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

let snippetOption (id: string, label: string) =
    option [attr.value id] [text label]

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
                    |> Array.sortByDescending (fun msg -> msg.Severity))
                (fun msg -> compilerMessage msg dispatch)
            cond model.Exception <| function
                | None -> empty
                | Some e -> Main.SimpleMessage().Severity("Error").Message(string e).Elt()
        ])
        .LoadSnippet(model.SelectedSnippet, fun s -> dispatch (LoadSnippet s))
        .Snippets(forEach snippets snippetOption)
        .Elt()
