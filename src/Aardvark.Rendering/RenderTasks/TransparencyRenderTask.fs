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

    /// True if any object currently in the set is marked transparent.
    /// Snapshot taken at compile time — the wrapping decision is static for
    /// the lifetime of the compiled render task.
    let needsOitTreatment (objects : aset<IRenderObject>) =
        objects.Content |> AVal.force |> HashSet.exists isTransparent

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

        let samples = userSig.Samples
        let depthFormat =
            userSig.DepthStencilAttachment
            |> Option.defaultValue TextureFormat.Depth24Stencil8

        // The intermediate signature mirrors the user signature: same color
        // attachments at the same sample count, plus a depth-stencil that we
        // share with the OIT pass.
        let intermediateSig =
            let colorEntries =
                userSig.ColorAttachments
                |> Map.toSeq
                |> Seq.map (fun (_, att) -> att.Name, att.Format)
            let entries = seq {
                yield! colorEntries
                yield DefaultSemantic.DepthStencil, depthFormat
            }
            runtime.CreateFramebufferSignature(entries, samples = samples)

        // OIT signature: dedicated Accum + Revealage attachments with the same
        // depth-stencil format so we can reuse the same depth attachment.
        let oitSig =
            runtime.CreateFramebufferSignature(
                [ WeightedBlendedOIT.Semantic.Accum,     TextureFormat.Rgba16f
                  WeightedBlendedOIT.Semantic.Revealage, TextureFormat.R32f
                  DefaultSemantic.DepthStencil,          depthFormat ],
                samples = samples)

        let opaqueSet = objects |> ASet.filter (not << isTransparent)
        let transparentSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r -> Some (transformTransparent r)
                    | _ -> None
                else None)

        // Adaptive holders for the OIT textures consumed by the composite quad.
        let accumTex     : cval<ITexture> = cval (NullTexture.Instance)
        let revealageTex : cval<ITexture> = cval (NullTexture.Instance)

        let compositeObject = buildCompositeObject samples accumTex revealageTex
        let compositeSet    = ASet.single (compositeObject :> IRenderObject)

        let opaqueTask      = compileRaw (intermediateSig, opaqueSet)
        let transparentTask = compileRaw (oitSig,          transparentSet)
        let compositeTask   = compileRaw (intermediateSig, compositeSet)

        // Mutable FBO state, recreated on size / sample changes.
        let mutable currentSize    = V2i.Zero
        let mutable currentSamples = 0
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

        let ensureFbos (size : V2i) (outputSamples : int) =
            if outputSamples <> samples then
                failwithf "[OIT] output framebuffer has %d samples but signature expects %d" outputSamples samples

            if size <> currentSize || outputSamples <> currentSamples then
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

                intermediateFb <- ValueSome (runtime.CreateFramebuffer(intermediateSig, interAtts))
                oitFb          <- ValueSome (runtime.CreateFramebuffer(oitSig, oitAtts))

                depthTex   <- ValueSome dt
                colorTex   <- ct
                accumT     <- ValueSome at
                revealageT <- ValueSome rt

                currentSize    <- size
                currentSamples <- outputSamples

                transact (fun () ->
                    accumTex.Value     <- at :> ITexture
                    revealageTex.Value <- rt :> ITexture)

        override x.FramebufferSignature = Some userSig
        override x.Runtime = Some runtime
        override x.Use f = f ()

        override x.PerformUpdate(token, rt) =
            opaqueTask.Update(token, rt)
            transparentTask.Update(token, rt)
            compositeTask.Update(token, rt)

        override x.Perform(token, rt, output) =
            let outFb = output.Framebuffer
            ensureFbos outFb.Size outFb.Signature.Samples

            let inter = intermediateFb.Value
            let oit   = oitFb.Value

            // 1. Seed intermediate from the user framebuffer so any prior clear
            //    or content (from a clear task / previous pass) is preserved.
            //    The final blit at the end overwrites the user framebuffer.
            runtime.Copy(outFb, inter)

            // 2. Opaque pass into the intermediate.
            opaqueTask.Run(token, rt, { output with Framebuffer = inter })

            // 3. Clear only Accum + Revealage on the OIT framebuffer; keep the
            //    shared depth produced by the opaque pass for depth testing.
            let oitClear =
                clear {
                    colors [
                        WeightedBlendedOIT.Semantic.Accum,     C4f.Zero
                        WeightedBlendedOIT.Semantic.Revealage, C4f.White
                    ]
                }
            runtime.Clear(oit, oitClear)

            // 4. Transparent pass into the OIT framebuffer.
            transparentTask.Run(token, rt, { output with Framebuffer = oit })

            // 5. Composite the OIT result back into the intermediate's colors.
            compositeTask.Run(token, rt, { output with Framebuffer = inter })

            // 6. Blit intermediate → user framebuffer (final).
            runtime.Copy(inter, outFb)

        override x.Release() =
            opaqueTask.Dispose()
            transparentTask.Dispose()
            compositeTask.Dispose()
            releaseFbos ()
            intermediateSig.Dispose()
            oitSig.Dispose()

    /// Creates a render task that automatically groups transparent + opaque
    /// objects through Weighted Blended OIT.
    let create (runtime   : IRuntime)
               (signature : IFramebufferSignature)
               (objects   : aset<IRenderObject>)
               (compileRaw: IFramebufferSignature * aset<IRenderObject> -> IRenderTask) : IRenderTask =
        new WrappedTask(runtime, signature, objects, compileRaw) :> IRenderTask
