namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive

[<AbstractClass>]
type AbstractRenderTask() =
    inherit AdaptiveObject()

    let id = RenderTaskId.New()

    let mutable frameId = 0UL

    let mutable isDisposed = false

    let outputDescription = AVal.init Unchecked.defaultof<OutputDescription>
    let viewportSize = outputDescription |> AVal.map _.Viewport.Size

    let tryGetRuntimeValue (name : string) : IAdaptiveValue voption =
        match name with
        | "ViewportSize" -> ValueSome viewportSize
        | _ -> ValueNone

    let transaction = new Transaction()

    let hookProvider (provider : IUniformProvider) =
        { new IUniformProvider with
            member x.TryGetUniform(scope, name) =
                match provider.TryGetUniform(scope, name) with
                | ValueSome v -> ValueSome v
                | _ -> tryGetRuntimeValue (string name)

            member x.Dispose() =
                provider.Dispose()
        }

    member x.HookProvider (uniforms : IUniformProvider) =
        hookProvider uniforms

    member x.HookRenderObject (ro : RenderObject) =
        let copy = RenderObject ro
        copy.Uniforms <- hookProvider ro.Uniforms
        copy

    /// The output description with which the task has last been run or is currently being run.
    member _.OutputDescription = outputDescription :> aval<_>

    /// The viewport size of the current output description.
    member _.ViewportSize = viewportSize

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
    member x.Run(token : AdaptiveToken, renderToken : RenderToken, output : OutputDescription) =
        x.EvaluateAlways token (fun token ->
            if isDisposed then
                raise <| ObjectDisposedException(null, "Cannot run a disposed render task.")

            use __ = renderToken.Use()

            x.OutOfDate <- true

            // perform 'transact' with reusable transaction object
            useTransaction transaction (fun () -> // fun alloc :/
                outputDescription.Value <- output
            )
            transaction.Commit()
            transaction.Dispose()

            x.Perform(token, renderToken, output)
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