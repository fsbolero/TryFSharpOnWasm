namespace WebFsc.Client

module Cmd =
    open System.Threading.Tasks
    open Elmish

    let ofAsyncIgnore (task: 'a -> Async<unit>) (arg: 'a) (ofError: exn -> 'msg) : Cmd<'msg> =
        [
            fun dispatch ->
                async {
                    try return! task arg
                    with e -> dispatch (ofError e)
                }
                |> Async.Start
        ]

    let ofTaskIgnore (task: 'a -> Task) (arg: 'a) (ofError: exn -> 'msg) : Cmd<'msg> =
        [
            fun dispatch ->
                (task arg).ContinueWith(fun t ->
                    if t.IsFaulted then dispatch (ofError t.Exception)
                )
                |> ignore
        ]