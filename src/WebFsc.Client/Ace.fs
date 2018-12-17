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

module WebFsc.Client.Ace

open System.Threading.Tasks
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop

type Annotation =
    {
        row: int
        column: int
        y2: int
        x2: int
        text: string
        ``type``: string
    }

let SetAnnotations (messages: FSharpErrorInfo[]) =
    let annotations = messages |> Array.map (fun info ->
        {
            row = info.StartLineAlternate - 1
            column = info.StartColumn
            y2 = info.EndLineAlternate - 1
            x2 = info.EndColumn
            text = info.Message
            ``type`` =
                match info.Severity with
                | FSharpErrorSeverity.Warning -> "warning"
                | FSharpErrorSeverity.Error -> "error"
        }
    )
    JSRuntime.Current.InvokeAsync("WebFsc.setAnnotations", annotations)
    :> Task

let SelectMessage (info: FSharpErrorInfo) =
    JSRuntime.Current.InvokeAsync("WebFsc.selectRange",
        info.StartLineAlternate - 1, info.StartColumn,
        info.EndLineAlternate - 1, info.EndColumn
    )
    :> Task
