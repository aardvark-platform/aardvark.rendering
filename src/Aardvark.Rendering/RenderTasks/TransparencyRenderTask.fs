namespace Aardvark.Rendering

open System
open Aardvark.Base
open FSharp.Data.Adaptive

/// Wraps a set of render objects into a Weighted Blended OIT pipeline:
///   1. opaque objects render to an intermediate framebuffer matching the
///      user-supplied signature (colors + shared depth);
///   2. transparent objects (RenderObject.IsTransparent = true) render to a
///      dedicated OIT framebuffer (Accum + Revealage attachments) that
///      shares the intermediate's depth attachment;
///   3. a fullscreen composite quad resolves the OIT result onto the
///      intermediate's color attachments;
///   4. the intermediate is finally blitted onto the user-supplied
///      framebuffer.
///
/// Each backend constructs this from its CompileRender entry point and passes
/// a `compileRaw` callback that creates ordinary, unwrapped sub-render-tasks.
module TransparencyRenderTask =

    /// True if the given render object should be routed through the OIT pass.
    let isTransparent (o : IRenderObject) =
        match o with
        | :? RenderObject as r -> r.IsTransparent
        | _ -> false

    /// Returns true if a render task built over the given object set could ever
    /// route objects through the OIT pipeline. Currently always true — callers
    /// should just wrap unconditionally and let the wrapper short-circuit at
    /// Run time when there are no transparent objects in the current snapshot.
    let needsOitTreatment (_objects : aset<IRenderObject>) = true

    /// Builds the per-attachment blend-mode map for the transparent pass,
    /// preserving any existing per-attachment modes the user supplied for
    /// non-OIT attachments.
    let private transparentAttachmentBlend (existing : Map<Symbol, BlendMode>) =
        existing
        |> Map.add WeightedBlendedOIT.Semantic.Accum     WeightedBlendedOIT.BlendModes.accum
        |> Map.add WeightedBlendedOIT.Semantic.Revealage WeightedBlendedOIT.BlendModes.revealage

    /// Clones a transparent RenderObject and rewrites its pipeline state for the
    /// OIT pass: composes the weighted-blend writer onto its surface, forces
    /// depth-write off, applies per-attachment blend modes for Accum + Revealage,
    /// and clears IsTransparent on the clone to avoid recursive wrapping.
    let private transformTransparent (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.Surface       <- WeightedBlendedOIT.composeSurface ro.Surface
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant false }
        copy.BlendState    <- { ro.BlendState with
                                  AttachmentMode = ro.BlendState.AttachmentMode |> AVal.map transparentAttachmentBlend }
        copy :> IRenderObject

    let private fullscreenPositions =
        [| V3f(-1.0f, -1.0f, 0.0f)
           V3f( 1.0f, -1.0f, 0.0f)
           V3f(-1.0f,  1.0f, 0.0f)
           V3f( 1.0f,  1.0f, 0.0f) |]

    /// Builds the fullscreen-quad RenderObject that runs the composite shader.
    /// The accum and revealage textures are exposed as adaptive uniforms so the
    /// task can swap them in when the OIT framebuffer is reallocated.
    let private buildCompositeObject (samples : int)
                                     (accum : aval<ITexture>) (revealage : aval<ITexture>) : RenderObject =
        let buffer = AVal.constant (ArrayBuffer fullscreenPositions :> IBuffer)
        let view   = BufferView(buffer, typeof<V3f>)

        let attrs =
            AttributeProvider.ofList [
                DefaultSemantic.Positions, view
            ]

        let uniforms =
            UniformProvider.ofList [
                WeightedBlendedOIT.Semantic.AccumBuffer,     accum :> IAdaptiveValue
                WeightedBlendedOIT.Semantic.RevealageBuffer, revealage :> IAdaptiveValue
            ]

        let drawCall = DrawCallInfo(FaceVertexCount = 4, InstanceCount = 1)

        let ro = RenderObject()
        ro.AttributeScope   <- Ag.Scope.Root
        ro.Mode             <- IndexedGeometryMode.TriangleStrip
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| drawCall |])
        ro.VertexAttributes <- attrs
        ro.Uniforms         <- uniforms
        ro.Surface          <- Surface.Effect (WeightedBlendedOIT.compositeEffect samples)
        ro.DepthState       <- { DepthState.Default with
                                    Test      = AVal.constant DepthTest.None
                                    WriteMask = AVal.constant false }
        ro.BlendState       <- { BlendState.Default with
                                    Mode = AVal.constant WeightedBlendedOIT.BlendModes.composite }
        ro

    type private WrappedTask(runtime   : IRuntime,
                             userSig   : IFramebufferSignature,
                             objects   : aset<IRenderObject>,
                             compileRaw: IFramebufferSignature * aset<IRenderObject> -> IRenderTask) =
        inherit AbstractRenderTask()

        let depthFormat =
            userSig.DepthStencilAttachment
            |> Option.defaultValue TextureFormat.Depth24Stencil8

        let opaqueSet = objects |> ASet.filter (not << isTransparent)
        let transparentSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r -> Some (transformTransparent r)
                    | _ -> None
                else None)

        // Snapshot of the current transparent objects — used at Run to skip the
        // OIT sub-passes whenever there are none.
        let transparentContent = transparentSet.Content

        // Adaptive holders for the OIT textures consumed by the composite quad.
        let accumTex     : cval<ITexture> = cval (NullTexture.Instance)
        let revealageTex : cval<ITexture> = cval (NullTexture.Instance)

        // Sample-count-dependent state. The OIT composite shader bakes in the
        // sample count and the framebuffer signatures bake in their sample
        // count, so all of this is rebuilt whenever the output framebuffer's
        // sample count changes.
        let mutable currentSamples  = 0
        let mutable intermediateSig : IFramebufferSignature voption = ValueNone
        let mutable oitSig          : IFramebufferSignature voption = ValueNone
        let mutable opaqueTask      : IRenderTask voption = ValueNone
        let mutable transparentTask : IRenderTask voption = ValueNone
        let mutable compositeTask   : IRenderTask voption = ValueNone

        // Size-dependent state. Rebuilt whenever the output framebuffer's size
        // (or sample count) changes.
        let mutable currentSize    = V2i.Zero
        let mutable depthTex       : IBackendTexture voption = ValueNone
        let mutable accumT         : IBackendTexture voption = ValueNone
        let mutable revealageT     : IBackendTexture voption = ValueNone
        let mutable colorTex       : Map<Symbol, IBackendTexture> = Map.empty
        let mutable intermediateFb : IFramebuffer voption = ValueNone
        let mutable oitFb          : IFramebuffer voption = ValueNone

        let releaseFbos () =
            intermediateFb |> ValueOption.iter (fun fb -> fb.Dispose())
            oitFb          |> ValueOption.iter (fun fb -> fb.Dispose())
            depthTex       |> ValueOption.iter (fun t -> t.Dispose())
            accumT         |> ValueOption.iter (fun t -> t.Dispose())
            revealageT     |> ValueOption.iter (fun t -> t.Dispose())
            colorTex |> Map.iter (fun _ t -> t.Dispose())
            intermediateFb <- ValueNone
            oitFb          <- ValueNone
            depthTex       <- ValueNone
            accumT         <- ValueNone
            revealageT     <- ValueNone
            colorTex       <- Map.empty
            currentSize    <- V2i.Zero

        let releaseTasksAndSignatures () =
            opaqueTask      |> ValueOption.iter (fun t -> t.Dispose())
            transparentTask |> ValueOption.iter (fun t -> t.Dispose())
            compositeTask   |> ValueOption.iter (fun t -> t.Dispose())
            intermediateSig |> ValueOption.iter (fun s -> s.Dispose())
            oitSig          |> ValueOption.iter (fun s -> s.Dispose())
            opaqueTask      <- ValueNone
            transparentTask <- ValueNone
            compositeTask   <- ValueNone
            intermediateSig <- ValueNone
            oitSig          <- ValueNone

        let ensureForSamples (samples : int) =
            if samples <> currentSamples then
                // Tasks and signatures depend on the sample count → rebuild
                // everything (also forces FBO rebuild via releaseFbos).
                releaseFbos ()
                releaseTasksAndSignatures ()

                let interSig =
                    let colorEntries =
                        userSig.ColorAttachments
                        |> Map.toSeq
                        |> Seq.map (fun (_, att) -> att.Name, att.Format)
                    let entries = seq {
                        yield! colorEntries
                        yield DefaultSemantic.DepthStencil, depthFormat
                    }
                    runtime.CreateFramebufferSignature(entries, samples = samples)

                let oS =
                    runtime.CreateFramebufferSignature(
                        [ WeightedBlendedOIT.Semantic.Accum,     TextureFormat.Rgba16f
                          WeightedBlendedOIT.Semantic.Revealage, TextureFormat.R32f
                          DefaultSemantic.DepthStencil,          depthFormat ],
                        samples = samples)

                let compositeObject = buildCompositeObject samples accumTex revealageTex
                let compositeSet    = ASet.single (compositeObject :> IRenderObject)

                intermediateSig <- ValueSome interSig
                oitSig          <- ValueSome oS
                opaqueTask      <- ValueSome (compileRaw (interSig, opaqueSet))
                transparentTask <- ValueSome (compileRaw (oS, transparentSet))
                compositeTask   <- ValueSome (compileRaw (interSig, compositeSet))
                currentSamples  <- samples

        let ensureFbos (size : V2i) (samples : int) =
            if size <> currentSize then
                releaseFbos ()

                let dt = runtime.CreateTexture2D(size, depthFormat, samples = samples)

                let ct =
                    userSig.ColorAttachments
                    |> Map.toList
                    |> List.map (fun (_, att) ->
                        att.Name, runtime.CreateTexture2D(size, att.Format, samples = samples))
                    |> Map.ofList

                let at = runtime.CreateTexture2D(size, TextureFormat.Rgba16f, samples = samples)
                let rt = runtime.CreateTexture2D(size, TextureFormat.R32f,    samples = samples)

                let interAtts =
                    ct
                    |> Map.map (fun _ t -> t.GetOutputView())
                    |> Map.add DefaultSemantic.DepthStencil (dt.GetOutputView())

                let oitAtts =
                    Map.ofList [
                        WeightedBlendedOIT.Semantic.Accum,     at.GetOutputView()
                        WeightedBlendedOIT.Semantic.Revealage, rt.GetOutputView()
                        DefaultSemantic.DepthStencil,          dt.GetOutputView()
                    ]

                intermediateFb <- ValueSome (runtime.CreateFramebuffer(intermediateSig.Value, interAtts))
                oitFb          <- ValueSome (runtime.CreateFramebuffer(oitSig.Value, oitAtts))

                depthTex   <- ValueSome dt
                colorTex   <- ct
                accumT     <- ValueSome at
                revealageT <- ValueSome rt

                currentSize <- size

                transact (fun () ->
                    accumTex.Value     <- at :> ITexture
                    revealageTex.Value <- rt :> ITexture)

        override x.FramebufferSignature = Some userSig
        override x.Runtime = Some runtime
        override x.Use f = f ()

        override x.PerformUpdate(token, rt) =
            opaqueTask      |> ValueOption.iter (fun t -> t.Update(token, rt))
            transparentTask |> ValueOption.iter (fun t -> t.Update(token, rt))
            compositeTask   |> ValueOption.iter (fun t -> t.Update(token, rt))

        override x.Perform(token, rt, output) =
            let outFb = output.Framebuffer
            let outSamples = outFb.Signature.Samples
            ensureForSamples outSamples
            ensureFbos outFb.Size outSamples

            let inter = intermediateFb.Value

            // Always: seed intermediate from user FB (preserves any pre-clear)
            // and run the opaque pass into it.
            runtime.Copy(outFb, inter)
            opaqueTask.Value.Run(token, rt, { output with Framebuffer = inter })

            // Skip the OIT passes when there are currently no transparent
            // objects in the input set.
            let hasTransparent =
                not (HashSet.isEmpty (AVal.force transparentContent))

            if hasTransparent then
                let oit = oitFb.Value

                // Clear only Accum + Revealage on the OIT framebuffer; the
                // shared depth from the opaque pass is preserved for depth
                // testing.
                let oitClear =
                    clear {
                        colors [
                            WeightedBlendedOIT.Semantic.Accum,     C4f.Zero
                            WeightedBlendedOIT.Semantic.Revealage, C4f.White
                        ]
                    }
                runtime.Clear(oit, oitClear)
                transparentTask.Value.Run(token, rt, { output with Framebuffer = oit })
                compositeTask.Value.Run(token, rt, { output with Framebuffer = inter })

            // Final blit: intermediate → user framebuffer.
            runtime.Copy(inter, outFb)

        override x.Release() =
            releaseTasksAndSignatures ()
            releaseFbos ()

    /// Creates a render task that automatically groups transparent + opaque
    /// objects through Weighted Blended OIT.
    let create (runtime   : IRuntime)
               (signature : IFramebufferSignature)
               (objects   : aset<IRenderObject>)
               (compileRaw: IFramebufferSignature * aset<IRenderObject> -> IRenderTask) : IRenderTask =
        new WrappedTask(runtime, signature, objects, compileRaw) :> IRenderTask
