namespace Aardvark.Rendering

open System
open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive

[<AbstractClass>]
type AbstractRenderTask() =
    inherit AdaptiveObject()

    let id = newId()

    static let resourcesInUse = obj()

    static let runtimeUniforms =
        Map.ofList [
            "ViewportSize", fun (o : OutputDescription) -> o.viewport.Size + V2i.II
        ]

    let mutable frameId = 0UL

    let mutable disposed = 0

    let runtimeValueCache = Dict.empty
    let currentOutput = AVal.init { framebuffer = Unchecked.defaultof<_>; viewport = Box2i(V2i.OO, V2i.II) }
    let tryGetRuntimeValue (name : string) =
        runtimeValueCache.GetOrCreate(name, fun name ->
            // TODO: different runtime-types
            match Map.tryFind name runtimeUniforms with
                | Some f ->
                    currentOutput |> AVal.map f :> IAdaptiveValue |> Some
                | None ->
                    None
        )

    let transaction = new Transaction()

    let hookProvider (provider : IUniformProvider) =
        { new IUniformProvider with
            member x.TryGetUniform(scope, name) =
                match tryGetRuntimeValue (string name)  with
                    | Some v -> Some v
                    | _ ->
                        provider.TryGetUniform(scope, name)

            member x.Dispose() =
                provider.Dispose()
        }

    member private x.UseValues (token : AdaptiveToken, output : OutputDescription, f : AdaptiveToken -> 'a) =
        // simulate transact but with reusing transaction
        useTransaction transaction (fun () ->
            currentOutput.Value <- output
        )
        transaction.Commit()
        transaction.Dispose()

        f(token)

    member x.HookProvider (uniforms : IUniformProvider) =
        hookProvider uniforms

    member x.HookRenderObject (ro : RenderObject) =
        { ro with Uniforms = hookProvider ro.Uniforms }

    static member ResourcesInUse = resourcesInUse

    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member PerformUpdate : AdaptiveToken * RenderToken -> unit
    abstract member Perform : AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member Release : unit -> unit
    abstract member Use : (unit -> 'a) -> 'a

    member x.Dispose() =
        if Interlocked.Exchange(&disposed, 1) = 0 then
            x.Release()

    member x.FrameId = frameId
    member x.Run(token : AdaptiveToken, renderToken : RenderToken, out : OutputDescription) =
        lock resourcesInUse (fun _ ->
            x.EvaluateAlways token (fun token ->
                x.OutOfDate <- true
                x.UseValues(token, out, fun token ->
                    x.Perform(token, renderToken, out)
                    frameId <- frameId + 1UL
                )
            )
        )

    member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
        lock resourcesInUse (fun _ ->
            x.EvaluateAlways token (fun token ->
                if x.OutOfDate then
                    x.PerformUpdate(token, renderToken)
            )
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRenderTask with
        member x.Id = id
        member x.FramebufferSignature = x.FramebufferSignature
        member x.Runtime = x.Runtime
        member x.FrameId = frameId
        member x.Update(token, renderToken) = x.Update(token, renderToken)
        member x.Run(token, renderToken, out) = x.Run(token, renderToken, out)
        member x.Use f = x.Use f