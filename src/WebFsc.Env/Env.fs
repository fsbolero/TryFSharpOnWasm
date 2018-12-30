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

/// Types and methods available to the user within their code.
namespace global

open System.IO
open System.Net.Http
open System.Runtime.CompilerServices
open System.Threading

/// Features available only in the TryF# environment.
type Env private () =

    static let mutable http = Unchecked.defaultof<HttpClient>

    /// An HTTP client that uses the browser's fetch API.
    static member Http = http

    static member internal SetHttp(x) = http <- x

[<Extension>]
type HttpExtensions =

    /// Send a GET request to the specified URI as an asynchronous operation.
    [<Extension>]
    static member AsyncGet(this: HttpClient, uri: string, ?completionOption: HttpCompletionOption, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match completionOption, cancellationToken with
        | None, None -> this.GetAsync(uri)
        | Some co, None -> this.GetAsync(uri, co)
        | None, Some ct -> this.GetAsync(uri, ct)
        | Some co, Some ct -> this.GetAsync(uri, co, ct)
        |> Async.AwaitTask

    /// Send a POST request to the specified URI as an asynchronous operation.
    [<Extension>]
    static member AsyncPost(this: HttpClient, uri: string, content: HttpContent, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match cancellationToken with
        | None -> this.PostAsync(uri, content)
        | Some ct -> this.PostAsync(uri, content, ct)
        |> Async.AwaitTask

    /// Send a PUT request to the specified URI as an asynchronous operation.
    [<Extension>]
    static member AsyncPut(this: HttpClient, uri: string, content: HttpContent, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match cancellationToken with
        | None -> this.PutAsync(uri, content)
        | Some ct -> this.PutAsync(uri, content, ct)
        |> Async.AwaitTask

    /// Send a DELETE request to the specified URI as an asynchronous operation.
    [<Extension>]
    static member AsyncDelete(this: HttpClient, uri: string, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match cancellationToken with
        | None -> this.DeleteAsync(uri)
        | Some ct -> this.DeleteAsync(uri, ct)
        |> Async.AwaitTask

    /// Send an HTTP request as an asynchronous operation.
    [<Extension>]
    static member AsyncSend(this: HttpClient, message: HttpRequestMessage, ?completionOption: HttpCompletionOption, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match completionOption, cancellationToken with
        | None, None -> this.SendAsync(message)
        | Some co, None -> this.SendAsync(message, co)
        | None, Some ct -> this.SendAsync(message, ct)
        | Some co, Some ct -> this.SendAsync(message, co, ct)
        |> Async.AwaitTask

    /// Serialize the HTTP content to a string as an asynchronous operation.
    [<Extension>]
    static member AsyncReadAsString(this: HttpContent) : Async<string> =
        this.ReadAsStringAsync() |> Async.AwaitTask

    /// Serialize the HTTP content to a byte array as an asynchronous operation.
    [<Extension>]
    static member AsyncReadAsByteArray(this: HttpContent) : Async<byte[]> =
        this.ReadAsByteArrayAsync() |> Async.AwaitTask

    /// Serialize the HTTP content and return a stream that represents the content as an asynchronous operation.
    [<Extension>]
    static member AsyncReadAsStream(this: HttpContent) : Async<Stream> =
        this.ReadAsStreamAsync() |> Async.AwaitTask

    /// Serialize the HTTP content into a stream of bytes and copy it to the stream object provided as the stream parameter.
    [<Extension>]
    static member AsyncCopyTo(this: HttpContent, stream: Stream) : Async<unit> =
        this.CopyToAsync(stream) |> Async.AwaitTask

// Allow access to `Env.SetHttp` to the application itself.
[<assembly: InternalsVisibleTo("WebFsc.Client")>]
do ()
