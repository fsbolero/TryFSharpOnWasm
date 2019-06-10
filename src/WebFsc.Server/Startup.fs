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

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member this.ConfigureServices(services: IServiceCollection) =
        services.AddHotReload(clientProjPath)
        |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IHostingEnvironment) =
        app.UseStaticFiles(
            StaticFileOptions(
                FileProvider = fileProvider (clientProjPath </> "wwwroot"),
                ContentTypeProvider = contentTypeProvider))
            .UseHotReload()
            .UseBlazor<Client.Startup>()
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
