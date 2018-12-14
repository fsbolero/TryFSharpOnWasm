open FSharp.Data

// Passing a URI to FSharp.Data providers is currently not available
// in TryFSharpOnWasm, so we pass the (partial) schema by hand instead.
type MyJson = JsonProvider<"""[ { "name": "repo" } ]""">

let uri = "https://api.github.com/orgs/fsbolero/repos"

let AsyncMain() = async {
    let! response = Env.Http.AsyncGet(uri)
    let! json = response.Content.AsyncReadAsString()
    printfn "Here are the repositories in the fsbolero organization:"
    for repo in MyJson.Parse(json) do
        printfn "* %s" repo.Name
}
