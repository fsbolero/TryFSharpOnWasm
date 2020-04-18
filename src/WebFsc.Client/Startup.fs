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
open System.Net.Http
open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Microsoft.Extensions.DependencyInjection

module Program =

    let baseAddress =
#if DEBUG
        "http://localhost:8080/"
#else
        "https://tryfsharp.fsbolero.io/"
#endif

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        // TODO Why doesn't AddBaseAddressHttpClient work?
        builder.Services.AddSingleton(new HttpClient(BaseAddress = Uri(baseAddress)))
        |> ignore
        // builder.Services.AddBaseAddressHttpClient() |> ignore
        builder.RootComponents.Add<App.MainApp>("#main")
        builder.Build().RunAsync() |> ignore
        0
