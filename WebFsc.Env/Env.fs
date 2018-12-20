namespace global

open System.IO
open System.Net.Http
open System.Runtime.CompilerServices
open System.Threading

type Env private () =

    static let mutable http = Unchecked.defaultof<HttpClient>

    static member Http = http

    static member internal SetHttp(x) = http <- x

[<Extension>]
type HttpExtensions =

    [<Extension>]
    static member AsyncGet(this: HttpClient, uri: string, ?completionOption: HttpCompletionOption, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match completionOption, cancellationToken with
        | None, None -> this.GetAsync(uri)
        | Some co, None -> this.GetAsync(uri, co)
        | None, Some ct -> this.GetAsync(uri, ct)
        | Some co, Some ct -> this.GetAsync(uri, co, ct)
        |> Async.AwaitTask

    [<Extension>]
    static member AsyncPost(this: HttpClient, uri: string, content: HttpContent, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match cancellationToken with
        | None -> this.PostAsync(uri, content)
        | Some ct -> this.PostAsync(uri, content, ct)
        |> Async.AwaitTask

    [<Extension>]
    static member AsyncPut(this: HttpClient, uri: string, content: HttpContent, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match cancellationToken with
        | None -> this.PutAsync(uri, content)
        | Some ct -> this.PutAsync(uri, content, ct)
        |> Async.AwaitTask

    [<Extension>]
    static member AsyncDelete(this: HttpClient, uri: string, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match cancellationToken with
        | None -> this.DeleteAsync(uri)
        | Some ct -> this.DeleteAsync(uri, ct)
        |> Async.AwaitTask

    [<Extension>]
    static member AsyncSend(this: HttpClient, message: HttpRequestMessage, ?completionOption: HttpCompletionOption, ?cancellationToken: CancellationToken) : Async<HttpResponseMessage> =
        match completionOption, cancellationToken with
        | None, None -> this.SendAsync(message)
        | Some co, None -> this.SendAsync(message, co)
        | None, Some ct -> this.SendAsync(message, ct)
        | Some co, Some ct -> this.SendAsync(message, co, ct)
        |> Async.AwaitTask

    [<Extension>]
    static member AsyncReadAsString(this: HttpContent) : Async<string> =
        this.ReadAsStringAsync() |> Async.AwaitTask

    [<Extension>]
    static member AsyncReadAsByteArray(this: HttpContent) : Async<byte[]> =
        this.ReadAsByteArrayAsync() |> Async.AwaitTask

    [<Extension>]
    static member AsyncReadAsStream(this: HttpContent) : Async<Stream> =
        this.ReadAsStreamAsync() |> Async.AwaitTask

    [<Extension>]
    static member AsyncCopyTo(this: HttpContent, stream: Stream) : Async<unit> =
        this.CopyToAsync(stream) |> Async.AwaitTask

[<assembly: InternalsVisibleTo("WebFsc.Client")>]
do ()
