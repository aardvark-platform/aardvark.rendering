open System
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

// Testing grounds for adaptive resource maps and binds

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveResource =

    type private Wrapper<'T>(value : aval<'T>) =
        inherit AdaptiveResource<'T>()

        let mutable allocated = false

        override x.Create() =
            allocated <- true

        override x.Destroy() =
            allocated <- false

        override x.Compute(t, rt) =
            if not allocated then
                failwith "Not allocated!"

            value.GetValue(t, rt)

    type private AdaptiveResourceLogger<'T>(value : aval<'T>) =

        let mutable refCount = 0

        member x.Value = value

        member x.Hash = hash value

        member x.Acquire() =
            Log.warn "%d: Acquire()" x.Hash

            if Interlocked.Increment(&refCount) = 1 then
                Log.warn "%d: Created" x.Hash

            value.Acquire()

        member x.Release() =
            Log.warn "%d: Release()" x.Hash

            if Interlocked.Decrement(&refCount) = 0 then
                Log.warn "%d: Destroyed" x.Hash

            value.Release()

        member x.ReleaseAll() =
            Log.warn "%d: ReleaseAll()" x.Hash

            if Interlocked.Exchange(&refCount, 0) > 0 then
                Log.warn "%d: Destroyed" x.Hash

            value.ReleaseAll()

        member x.Compute(t : AdaptiveToken, rt : RenderToken) =
            Log.warn "%d: Compute()" x.Hash
            value.GetValue(t, rt)

        member x.Compute(t : AdaptiveToken) =
            x.Compute(t, RenderToken.Empty)

        override x.GetHashCode() = value.GetHashCode()
        override x.Equals o =
            match o with
            | :? AdaptiveResourceLogger<'T> as other -> DefaultEquality.equals value other.Value
            | _ -> false

        interface IAdaptiveObject with
            member x.AllInputsProcessed(a) = value.AllInputsProcessed(a)
            member x.InputChanged(a,b) = value.InputChanged(a,b)
            member x.Mark() = value.Mark()
            member x.IsConstant = value.IsConstant
            member x.Level
                with get() = value.Level
                and set v = value.Level <- v
            member x.OutOfDate
                with get() = value.OutOfDate
                and set v = value.OutOfDate <- v
            member x.Outputs = value.Outputs
            member x.Tag
                with get() = value.Tag
                and set t = value.Tag <- t
            member x.Weak = value.Weak

        interface IAdaptiveValue with
            member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x
            member x.ContentType = typeof<'T>
            member x.GetValueUntyped(t) = x.Compute(t) :> obj

        interface IAdaptiveValue<'T> with
            member x.GetValue(t) = x.Compute(t)

        interface IAdaptiveResource with
            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()
            member x.ReleaseAll() = x.ReleaseAll()
            member x.GetValue(c,t) = x.Compute(c,t) :> obj

        interface IAdaptiveResource<'T> with
            member x.GetValue(c,t) = x.Compute(c,t)

    let logger (value : aval<'T>) =
        AdaptiveResourceLogger(value) :> IAdaptiveResource<'T>

    let wrap (value : aval<'T>) =
        Wrapper(value) :> aval<_>

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    use win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug true
            samples 8
        }

    let runtime = win.Runtime

    let signature =
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
            DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
        ]

    use task =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.Colors, [| C4b.Red; C4b.Green; C4b.Blue; C4b.Yellow |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates, [|V2d.OO; V2d.IO; V2d.II; V2d.OI|] :> Array
                ]
        )
        |> Sg.ofIndexedGeometry
        |> Sg.viewTrafo' (
            CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
            |> CameraView.viewTrafo
        )
        |> Sg.projTrafo' (Frustum.perspective 60.0 0.01 10.0 1.0 |> Frustum.projTrafo)
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.vertexColor |> toEffect
        ]
        |> Sg.compile runtime signature

    let renderQuadToTexture (resolution : V2i) =
        task
        |> RenderTask.renderToColor (AVal.init resolution)
        |> AdaptiveResource.logger

    let lowResResult = renderQuadToTexture (V2i(128))
    let highResResult = renderQuadToTexture (V2i(512))

    let onoff = AVal.init true
    let highRes = AVal.init false

    // Cannot use AVal.bind here since we need to maintain resource semantics
    let result =
        highRes |> AdaptiveResource.wrap |> AdaptiveResource.bind (fun high ->
            if high then
                highResResult :> aval<_>
            else
                lowResResult :> aval<_>
        )
        |> AdaptiveResource.logger

    let sg =
        onoff |> AVal.map (fun o ->
            if not o then
                Sg.empty
            else
                Sg.fullScreenQuad
                |> Sg.diffuseTexture result
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
        ) |> Sg.dynamic

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.Enter -> transact(fun _ -> onoff.Value <- not onoff.Value)
        | Keys.Space -> transact(fun _ -> highRes.Value <- not highRes.Value)
        | _ -> ()
    )

    win.Scene <- sg
    win.Run(preventDisposal = true)

    runtime.DeleteFramebufferSignature(signature)

    0
