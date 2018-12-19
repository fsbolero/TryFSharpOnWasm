open FSharp.Data

type MyJson = JsonProvider<"""{ "a": 42, "b": "test" }""">

printfn "a = %A" <| MyJson.GetSample().A
