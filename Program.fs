open System
open System.IO
open System.Reactive.Subjects
open System.Threading.Tasks
open FSharp.Control
open FSharp.Control.Reactive
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Hosting

let currentMessage =
    "fist message!"
    |> Subject.behavior
    |> Subject.Synchronize

let handlePost (ctx: HttpContext) =
    task {
        use bodyStream = new StreamReader(ctx.Request.Body)
        let! body = bodyStream.ReadToEndAsync()

        currentMessage.OnNext body

        do! ctx.Response.WriteAsync $"published: {body}"
    }
    :> Task

let handleGet (ctx: HttpContext) =
    task {
        ctx.Response.Headers.Add("Content-Type", StringValues "text/event-stream")

        let requestAborted =
            Async.AwaitWaitHandle ctx.RequestAborted.WaitHandle
            |> Async.Ignore

        do!
            AsyncSeq.ofObservableBuffered currentMessage
            |> AsyncSeq.takeUntilSignal requestAborted
            |> AsyncSeq.iterAsync
                (fun next ->
                    async {
                        do!
                            $"data: {next}\n\n"
                            |> ctx.Response.WriteAsync
                            |> Async.AwaitTask

                        do! ctx.Response.Body.FlushAsync() |> Async.AwaitTask
                    })
    }
    :> Task

let endpoints (endpoints: IEndpointRouteBuilder) =
    endpoints.MapGet("/sse", RequestDelegate handleGet)
    |> ignore

    endpoints.MapPost("/sse", RequestDelegate handlePost)
    |> ignore

let configureApp (app: IApplicationBuilder) =
    app.UseRouting() |> ignore
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore
    app.UseEndpoints(Action<_> endpoints) |> ignore


[<EntryPoint>]
let main args =
    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun builder ->
            builder
                .Configure(configureApp)
                .ConfigureServices(fun services -> ())
            |> ignore)
        .Build()
        .Run()

    0
