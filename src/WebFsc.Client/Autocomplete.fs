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
open System.Threading.Tasks
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.JSInterop

type Completion =
    {
        caption: string
        value: string
        meta: string
        cache: DotNetObjectRef
        index: int
    }

/// A set of autocomplete items, used to cache computed tooltips.
type CompletionCache(items: FSharpDeclarationListItem[], js: IJSInProcessRuntime) =

    let tooltips = Array.create items.Length None

    // Somehow `item.DescriptionTextAsync |> Async.RunSynchronously` or `Async.StartAsTask` freezes,
    // even though `item.DescriptionTextAsync |> Async.Start` runs nicely.
    // So we work around it by saving tooltips from async runs and calling `updateTooltip()` from `Async.Start`.
    [<JSInvokable>]
    member this.GetTooltip(index: int) =
        match tooltips.[index] with
        | None ->
            async {
                let! (FSharpToolTipText ttitems) = items.[index].DescriptionTextAsync
                let tt = String.concat "\n" <| seq {
                    for ttitem in ttitems do
                        match ttitem with
                        | FSharpToolTipElement.CompositionError e -> yield e
                        | FSharpToolTipElement.None -> ()
                        | FSharpToolTipElement.Group ttelts ->
                            for ttelt in ttelts do
                                yield ttelt.MainDescription
                }
                tooltips.[index] <- Some tt
                js.Invoke("WebFsc.updateTooltip")
            }
            |> Async.Start
            "Loading..."
        | Some tt -> tt

/// A wrapper object to trigger autocompletion.
type Autocompleter(dispatch: int * int * string * (FSharpDeclarationListItem[] -> IDisposable) -> unit, js) =

    [<JSInvokable>]
    member this.Complete(line, col, lineText) =
        let tcs = TaskCompletionSource<Completion[]>()
        dispatch (line, col, lineText, fun items ->
            let cache = new DotNetObjectRef(CompletionCache(items, js))
            items
            |> Array.mapi (fun i item ->
                {
                    caption = item.Name
                    value = item.NameInCode
                    meta = string item.Glyph
                    cache = cache
                    index = i
                })
            |> tcs.SetResult
            cache :> IDisposable
        )
        tcs.Task


