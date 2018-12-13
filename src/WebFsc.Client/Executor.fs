namespace WebFsc.Client

open System.Reflection

type ExecutorStatus =
    | Standby
    | Running
    | Finished of int

type Executor =
    {
        status: ExecutorStatus
    }

    member this.Run(path: string) =
        { this with status = Running },
        fun () -> async {
            let asm = Assembly.LoadFrom(path)
            asm.EntryPoint.Invoke(null, [||]) |> ignore
            return { this with status = Finished 0 }
        }

module Executor =

    let create () =
        { status = Standby }