# Try F# on WebAssembly

[![Build status](https://ci.appveyor.com/api/projects/status/mw21lo0uhu19fkfi?svg=true)](https://ci.appveyor.com/project/IntelliFactory/tryfsharponwasm)

This is the repository for the [Try F# on WebAssembly](https://tryfsharp.fsbolero.io) website.

Uses Bolero - F# Tools for Blazor, see [website](https://fsbolero.io/) and [repository](https://github.com/fsbolero/Bolero).

## Building this project

First run `install.ps1` in Powershell. Then you can open the solution in your IDE of choice.

The server project `WebFsc.Server` is just here for developer convenience (hot reloading, MIME type for *.fsx); the actual deployed project is `WebFsc.Client`.
