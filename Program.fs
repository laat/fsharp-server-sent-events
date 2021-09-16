open System
open System.IO
open System.Threading.Tasks
open FSharp.Control
open FSharp.Control.Reactive
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Primitives

let current = Subject.behavior "first message!"

let handlePost (ctx: HttpContext) =
    task {
        printfn "POST %A" ctx.Request.Path

        use bodyStream = new StreamReader(ctx.Request.Body)
        let! body = bodyStream.ReadToEndAsync()

        body |> current.OnNext

        do! ctx.Response.WriteAsync body
    }
    :> Task

let handleGet (ctx: HttpContext) =
    task {
        printfn "GET %A" ctx.Request.Path
        ctx.Response.Headers.Add("Content-Type", StringValues "text/event-stream")

        let signal =
            Async.AwaitWaitHandle ctx.RequestAborted.WaitHandle
            |> Async.Ignore

        do!
            AsyncSeq.ofObservableBuffered current
            |> AsyncSeq.takeUntilSignal signal
            |> AsyncSeq.iterAsync
                (fun next ->
                    $"data: {next}\n\n"
                    |> ctx.Response.WriteAsync
                    |> Async.AwaitTask)
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
