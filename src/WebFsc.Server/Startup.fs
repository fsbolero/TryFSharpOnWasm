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

type Startup() =

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    member this.ConfigureServices(services: IServiceCollection) =
        ()

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IHostingEnvironment) =
        let provider = FileExtensionContentTypeProvider()
        provider.Mappings.[".fsx"] <- "text/x-fsharp"

        app.UseStaticFiles(
            StaticFileOptions(
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(
                        Path.GetDirectoryName(Directory.GetCurrentDirectory()),
                        "WebFsc.Client", "wwwroot")),
                ContentTypeProvider = provider))
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
