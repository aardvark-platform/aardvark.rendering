namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

[<AbstractClass>]
type AbstractRenderTask() =
    inherit AdaptiveObject()

    let id = RenderTaskId.New()

    static let runtimeUniforms =
        Map.ofList [
            "ViewportSize", fun (o : OutputDescription) -> o.viewport.Size + V2i.II
        ]

    let mutable frameId = 0UL

    let mutable isDisposed = false

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

    member x.HookProvider (uniforms : IUniformProvider) =
        hookProvider uniforms

    member x.HookRenderObject (ro : RenderObject) =
        let copy = RenderObject ro
        copy.Uniforms <- hookProvider ro.Uniforms
        copy

    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member PerformUpdate : AdaptiveToken * RenderToken -> unit
    abstract member Perform : AdaptiveToken * RenderToken * OutputDescription -> unit
    abstract member Release : unit -> unit
    abstract member Use : (unit -> 'a) -> 'a

    member x.Dispose() =
        lock x (fun _ ->
            if not isDisposed then
                isDisposed <- true
                x.Release()
        )

    member val Name : string = null with get, set
    member x.FrameId = frameId
    member x.Run(token : AdaptiveToken, renderToken : RenderToken, out : OutputDescription) =
        x.EvaluateAlways token (fun token ->
            if isDisposed then
                raise <| ObjectDisposedException(null, "Cannot run a disposed render task.")

            use __ = renderToken.Use()

            x.OutOfDate <- true

            // perform 'transact' with reusable transaction object
            useTransaction transaction (fun () -> // fun alloc :/
                currentOutput.Value <- out
            )
            transaction.Commit()
            transaction.Dispose()

            x.Perform(token, renderToken, out)
            frameId <- frameId + 1UL
        )

    member x.Update(token : AdaptiveToken, renderToken : RenderToken) =
        x.EvaluateAlways token (fun token ->
            if isDisposed then
                raise <| ObjectDisposedException(null, "Cannot update a disposed render task.")

            use __ = renderToken.Use()

            if x.OutOfDate then
                x.PerformUpdate(token, renderToken)
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
        member x.Name with get() = x.Name and set name = x.Name <- name