namespace WebFsc.Server

open System
open System.IO
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.StaticFiles
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open WebFsc
open Bolero.Templating.Server

type Startup() =

    let (</>) x y = Path.Combine(x, y)
    let contentTypeProvider = FileExtensionContentTypeProvider()
    do  contentTypeProvider.Mappings.[".fsx"] <- "text/x-fsharp"
        contentTypeProvider.Mappings.[".scss"] <- "text/x-scss"
    let clientProjPath = Path.Combine(__SOURCE_DIRECTORY__, "..", "WebFsc.Client")
    let fileProvider path = new PhysicalFileProvider(path)

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddControllers() |> ignore
        services.AddHotReload(clientProjPath) |> ignore

    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        app.UseStaticFiles(
            StaticFileOptions(
                FileProvider = fileProvider (clientProjPath </> "wwwroot"),
                ContentTypeProvider = contentTypeProvider))
            .UseRouting()
            .UseBlazorFrameworkFiles()
            .UseEndpoints(fun endpoints ->
                endpoints.UseHotReload()
                endpoints.MapControllers() |> ignore
                endpoints.MapFallbackToFile("index.html") |> ignore)
        |> ignore

module Program =

    [<EntryPoint>]
    let main args =
        WebHost
            .CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build()
            .Run()
        0
