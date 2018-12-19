open FSharp.Data

type MyDocument = XmlProvider<"""<root></root>""">

printfn "a = %A" <| MyDocument.GetSample()
