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
        SelectedSample: string
    }

let defaultSource = "printfn \"Hello, world!\"\n"

let samples =
    [
        "HelloWorld", "Hello, world!"
        "Arithmetic", "Arithmetic"
    ]

let unselectedSample = "Unselected"

let initModel compiler =
    {
        Text = defaultSource
        Compiler = compiler
        Executor = Executor.create ()
        Messages = [||]
        Exception = None
        SelectedSample = unselectedSample
    }

type Message =
    | SetText of string
    | Compile
    | Compiled of Compiler
    | RunFinished of Executor
    | Checked of FSharpErrorInfo[]
    | Error of exn
    | SelectMessage of FSharpErrorInfo
    | LoadSample of string
    | SampleLoaded of string

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

/// Update the application model.
let update (http: HttpClient) message model =
    match message with
    | SetText text ->
        { model with Text = text; SelectedSample = unselectedSample },
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
        { model with Messages = errors },
        Cmd.attemptTask Ace.SetAnnotations errors Error
    | Error exn ->
        { model with
            Exception = Some exn
            Compiler = model.Compiler.MarkAsFailedIfRunning() },
        Cmd.attemptTask ScreenOut.Clear () Error
    | SelectMessage message ->
        model,
        Cmd.attemptTask Ace.SelectMessage message Error
    | LoadSample sampleId ->
        { model with SelectedSample = sampleId },
        Cmd.ofTask
            (fun (s: string) -> http.GetStringAsync(s))
            (sprintf "/samples/%s.fsx" sampleId)
            SampleLoaded Error
    | SampleLoaded text ->
        model,
        Cmd.attemptTask
            (fun () -> JSRuntime.Current.InvokeAsync("WebFsc.setText", text) :> Task) () Error

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

let sampleOption (id: string, label: string) =
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
        .LoadSample(model.SelectedSample, fun s -> dispatch (LoadSample s))
        .Samples(forEach samples sampleOption)
        .Elt()
