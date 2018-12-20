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
            // Run entry point
            asm.EntryPoint.Invoke(null, [||]) |> ignore
            // Run Main.MainAsync() if it exists
            let mainModule = asm.GetType("Main")
            if not (isNull mainModule) then
                let mainAsyncFunc = mainModule.GetMethod("MainAsync", BindingFlags.Static ||| BindingFlags.Public)
                if not (isNull mainAsyncFunc) then
                    do! mainAsyncFunc.Invoke(null, [||]) :?> Async<unit>
            // Done
            return { this with status = Finished 0 }
        }

module Executor =

    let create () =
        { status = Standby }
