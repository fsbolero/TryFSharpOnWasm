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

namespace WebFsc.Client

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Microsoft.JSInterop

/// A wrapper object to pass callback functions to JavaScript.
type Callback =

    static member Of(f) =
        DotNetObjectReference.Create(new StringCallback(f))

// Need to do separate concrete types because JSInterop doesn't support
// generic JSInvokable methods.
and StringCallback(f: string -> unit) =

    [<JSInvokable>]
    member this.Invoke(arg) =
        f arg

[<AutoOpen>]
module JSExtensions =

    type IJSInProcessRuntime with

        // IUriHelper doesn't support query params yet :(
        member this.GetQueryParam(param: string) =
            this.Invoke<string>("WebFsc.getQueryParam", param)
            |> Option.ofObj

        member this.ListenToQueryParam(param: string, callback: string -> unit) =
            this.Invoke<unit>("WebFsc.listenToQueryParam", param, Callback.Of callback)

module Cmd =
    open Elmish

    let attemptAsync (task: 'a -> Async<unit>) (arg: 'a) (ofError: exn -> 'msg) : Cmd<'msg> =
        [
            fun dispatch ->
                async {
                    try return! task arg
                    with e -> dispatch (ofError e)
                }
                |> Async.Start
        ]

    let attemptTask (task: 'a -> Task) (arg: 'a) (ofError: exn -> 'msg) : Cmd<'msg> =
        [
            fun dispatch ->
                (task arg).ContinueWith(fun t ->
                    if t.IsFaulted then dispatch (ofError t.Exception)
                )
                |> ignore
        ]

module Async =

    /// Run this async after a short delay, to let the UI update.
    let WithYield (a: Async<'T>) : Async<'T> =
        async.Bind(Async.Sleep(10), fun _ -> a)

module ScreenOut =

    /// <summary>
    /// A TextWriter that sends text to the screen via the js function WebFsc.write.
    /// If <c>isErr</c> is true, sends it as error output; otherwise, as standard output.
    /// </summary>
    type Writer(isErr: bool, js: IJSInProcessRuntime) =
        inherit TextWriter()

        override this.Encoding = Text.Encoding.UTF8

        override this.Write(c: char) =
            this.Write(string c)

        override this.Write(s: string) =
            js.Invoke<unit>("WebFsc.write", s, isErr)

    let private out js = new Writer(false, js)
    let private err js = new Writer(true, js)

    /// Run this task with its output redirected to the screen.
    let Wrap js (task: Async<_>) =
        let normalOut = stdout
        let normalErr = stderr
        async {
            Console.SetOut(out js)
            Console.SetError(err js)
            let! res = task
            Console.SetOut(normalOut)
            Console.SetError(normalErr)
            return res
        }

    /// <summary>
    /// Clear the screen output.
    /// Can be called inside or outside a <c>Wrap</c>-ped task.
    /// </summary>
    let Clear (js: IJSInProcessRuntime) =
        js.Invoke("WebFsc.clear") : unit

/// Delays computations by the given amount, cancelling previous computations
/// if triggering during their delay.
type Delayer(durationInMs: int) =

    let mutable current: option<CancellationTokenSource> = None

    member this.Trigger(task) =
        current |> Option.iter (fun tok -> tok.Cancel())
        let cts = new CancellationTokenSource()
        Async.StartImmediate(async {
            do! Async.Sleep durationInMs
            do! task
            current <- None
        }, cts.Token)
        current <- Some cts
