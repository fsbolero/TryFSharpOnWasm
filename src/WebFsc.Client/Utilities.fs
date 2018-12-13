namespace WebFsc.Client

open System
open System.IO
open System.Threading
open System.Threading.Tasks

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

module Stdout =
    open Microsoft.JSInterop

    type Writer(isErr: bool) =
        inherit TextWriter()

        override this.Encoding = Text.Encoding.UTF8

        override this.Write(c: char) =
            this.Write(string c)

        override this.Write(s: string) =
            this.WriteAsync(s) |> ignore

        override this.WriteAsync(s: string) =
            JSRuntime.Current.InvokeAsync("WebFsc.write", s, isErr)
            :> Task

    let Init () =
        Console.SetOut(new Writer(false))
        Console.SetError(new Writer(true))

    let Clear () =
        JSRuntime.Current.InvokeAsync("WebFsc.clear")
        :> Task

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
