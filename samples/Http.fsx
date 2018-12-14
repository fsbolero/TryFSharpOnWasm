let uri = "samples/Http.fsx"

// In TryFSharpOnWasm, if you define a function AsyncMain : unit -> Async<unit>
// then it will be run as your program's main function.
let AsyncMain() = async {

    // You can make HTTP requests using Env.Http : System.Net.Http.HttpClient.
    let! response = Env.Http.AsyncGet(uri)

    let! body = response.Content.AsyncReadAsString()

    printfn "This is the source of this sample:\n\n%s" body
}
