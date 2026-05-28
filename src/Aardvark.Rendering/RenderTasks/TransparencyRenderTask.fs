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

    /// Transparent rendering technique. WeightedBlended is the cheap,
    /// approximate default; ABuffer is the exact (interlocked per-pixel
    /// k-buffer) alternative. Switch the whole pipeline by editing the single
    /// `technique` binding below.
    type Technique =
        | WeightedBlended
        | ABuffer

    let technique = Technique.ABuffer

    /// Effective sample count for the intermediate / OIT / composite framebuffers
    /// and storage. ABuffer forces samples=1 (the MSAA splat re-rasterizes the
    /// transparent geometry to recover edge AA); WeightedBlended keeps the
    /// user's sample count.
    let private effectiveSamples (userSamples : int) =
        match technique with
        | WeightedBlended -> userSamples
        | ABuffer         -> 1

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

    /// Per-attachment blend-mode map for the OIT pass. Only Accum and
    /// Revealage exist in the OIT framebuffer — extras are not bound to
    /// it. Accum: additive. Revealage: multiplicative transmittance.
    let private transparentAttachmentBlend =
        Map.ofList [
            WeightedBlendedOIT.Semantic.Accum,     WeightedBlendedOIT.BlendModes.accum
            WeightedBlendedOIT.Semantic.Revealage, WeightedBlendedOIT.BlendModes.revealage
        ]

    /// Clones a transparent RenderObject and rewrites its pipeline state for
    /// the OIT compositing pass: composes the weighted-blend writer onto its
    /// surface, forces depth-write off, applies the Accum/Revealage blend
    /// modes, and clears IsTransparent on the clone to avoid recursive
    /// wrapping. The OIT framebuffer only has Accum + Revealage as color
    /// attachments — any extras the user's shader writes are dropped by
    /// FShade compilation, since they're not in the OIT signature.
    let private transformTransparent (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.Surface       <- WeightedBlendedOIT.composeSurface ro.Surface
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant false }
        copy.BlendState    <- { ro.BlendState with
                                  AttachmentMode = AVal.constant transparentAttachmentBlend }
        copy :> IRenderObject

    /// Builds the per-attachment write-mask map for the transparent depth/extras
    /// pass: Colors gets ColorMask.None (writes suppressed), all other
    /// attachments keep their existing mask (default = All).
    let private transparentPickWriteMask (existing : Map<Symbol, ColorMask>) =
        existing |> Map.add DefaultSemantic.Colors ColorMask.None

    /// Clones a transparent RenderObject for the depth + extras pass. The
    /// shader is left UNMODIFIED (user's original surface) — depth-write is
    /// enabled so the depth test alone selects the closest transparent
    /// fragment per pixel. The Colors attachment is write-masked so only the
    /// extras (PickData and friends) actually land in the framebuffer.
    /// Blending is disabled — only one fragment survives per pixel through
    /// the depth test.
    let private transformTransparentPick (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant true }
        copy.BlendState    <- { ro.BlendState with
                                  Mode                = AVal.constant BlendMode.None
                                  AttachmentMode      = AVal.constant Map.empty
                                  AttachmentWriteMask = ro.BlendState.AttachmentWriteMask |> AVal.map transparentPickWriteMask }
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

    // ===== A-buffer (exact k-buffer) technique =================================

    /// Premultiplied "over" blend for compositing the resolved A-buffer color
    /// onto the opaque scene: result = src + (1 - src.a) * dst.
    let private premultipliedOver =
        { BlendMode.Blend with
            SourceColorFactor      = BlendFactor.One
            DestinationColorFactor = BlendFactor.InvSourceAlpha
            SourceAlphaFactor      = BlendFactor.One
            DestinationAlphaFactor = BlendFactor.InvSourceAlpha }

    /// Builds a uniform provider exposing the two A-buffer storage images.
    /// All per-slot data (depth, color, mask) lives in one RGBA32UI image —
    /// see ABufferOIT.fs for the binding-count rationale.
    let private aBufferImageUniforms (count : aval<ITexture>) (slot : aval<ITexture>) =
        UniformProvider.ofList [
            ABufferOIT.Semantic.ABufferCount, count :> IAdaptiveValue
            ABufferOIT.Semantic.ABufferSlot,  slot  :> IAdaptiveValue
        ]

    /// Clones a transparent RenderObject for the A-buffer build pass: composes
    /// the interlocked insert writer onto its surface, masks colour writes,
    /// disables depth-write (depth test still occludes against opaque), binds
    /// the storage images, and clears IsTransparent on the clone.
    let private transformTransparentABuffer
                    (count : aval<ITexture>) (slot : aval<ITexture>)
                    (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.Surface       <- ABufferOIT.composeSurface ro.Surface
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant false }
        copy.BlendState    <- { ro.BlendState with
                                  Mode                = AVal.constant BlendMode.None
                                  AttachmentMode      = AVal.constant Map.empty
                                  ColorWriteMask      = AVal.constant ColorMask.None
                                  AttachmentWriteMask = AVal.constant Map.empty }
        copy.Uniforms      <- UniformProvider.union (aBufferImageUniforms count slot) ro.Uniforms
        copy :> IRenderObject

    /// Uniform provider exposing the single-sample composite texture to the
    /// MSAA splat shader.
    let private compositeInputUniform (composite : aval<ITexture>) =
        UniformProvider.ofList [
            ABufferOIT.Semantic.CompositeInput, composite :> IAdaptiveValue
        ]

    /// Clones a transparent RenderObject for the MSAA splat pass: re-rasterizes
    /// the same geometry into the multisampled user framebuffer, with the splat
    /// shader composed on (samples the single-sample composite at each pixel and
    /// overwrites the colour). Depth-tests against the opaque depth (no write)
    /// so transparent geometry behind opaque doesn't clobber it; blend disabled
    /// (overwrite). Every covered MSAA sample reads the same composite colour,
    /// so the rasterizer's coverage is the only thing that varies per sample —
    /// which is exactly what produces the anti-aliased silhouette.
    let private transformTransparentSplat
                    (composite : aval<ITexture>)
                    (ro : RenderObject) : IRenderObject =
        let copy = RenderObject(ro)
        copy.IsTransparent <- false
        copy.Surface       <- ABufferOIT.composeSplatSurface ro.Surface
        copy.DepthState    <- { ro.DepthState with WriteMask = AVal.constant false }
        copy.BlendState    <- { ro.BlendState with
                                  Mode                = AVal.constant BlendMode.None
                                  AttachmentMode      = AVal.constant Map.empty
                                  ColorWriteMask      = AVal.constant ColorMask.All
                                  AttachmentWriteMask = AVal.constant Map.empty }
        copy.Uniforms      <- UniformProvider.union (compositeInputUniform composite) ro.Uniforms
        copy :> IRenderObject

    /// Fullscreen quad that resolves the A-buffer and composites it over the
    /// opaque scene with premultiplied "over".
    let private buildABufferResolveObject
                    (count : aval<ITexture>) (slot : aval<ITexture>) : RenderObject =
        let buffer = AVal.constant (ArrayBuffer fullscreenPositions :> IBuffer)
        let view   = BufferView(buffer, typeof<V3f>)
        let attrs  = AttributeProvider.ofList [ DefaultSemantic.Positions, view ]
        let drawCall = DrawCallInfo(FaceVertexCount = 4, InstanceCount = 1)

        let ro = RenderObject()
        ro.AttributeScope   <- Ag.Scope.Root
        ro.Mode             <- IndexedGeometryMode.TriangleStrip
        ro.DrawCalls        <- DrawCalls.Direct (AVal.constant [| drawCall |])
        ro.VertexAttributes <- attrs
        ro.Uniforms         <- aBufferImageUniforms count slot
        ro.Surface          <- Surface.Effect ABufferOIT.resolveEffect
        ro.DepthState       <- { DepthState.Default with
                                    Test      = AVal.constant DepthTest.None
                                    WriteMask = AVal.constant false }
        ro.BlendState       <- { BlendState.Default with
                                    Mode = AVal.constant premultipliedOver }
        ro

    /// Cached per-(size, samples) bundle of FBO resources used by the wrapped
    /// path. Built lazily on demand and kept alive across frames so size
    /// changes between known sizes don't dispose+rebuild.
    type private FboBundle = {
        IntermediateFb : IFramebuffer
        OitFb          : IFramebuffer
        CompositeFb    : IFramebuffer
        DepthTex       : IBackendTexture
        AccumT         : IBackendTexture
        RevealageT     : IBackendTexture
        ColorTex       : Map<Symbol, IBackendTexture>
        // A-buffer storage (only allocated when technique = ABuffer)
        AbCount        : IBackendTexture voption
        AbSlot         : IBackendTexture voption    // RGBA32UI per slot: X=depth, Y=color, Z=gl_SampleMaskIn
    }

    type private WrappedTask(runtime   : IRuntime,
                             userSig   : IFramebufferSignature,
                             objects   : aset<IRenderObject>,
                             compileRaw: IFramebufferSignature * aset<IRenderObject> -> IRenderTask) =
        inherit AbstractRenderTask()

        let depthFormat =
            userSig.DepthStencilAttachment
            |> Option.defaultValue TextureFormat.Depth24Stencil8

        // The intermediate framebuffer mirrors the user signature exactly
        // (all color attachments + DepthStencil). Opaque writes go there and
        // pass 3 updates extras + depth based on depth-test selection. The
        // OIT framebuffer is separate and only carries Accum + Revealage +
        // a shared DepthStencil — extras are not bound for pass 2.
        let userColorAtts =
            userSig.ColorAttachments
            |> Map.toList
            |> List.map (fun (_, att) -> att.Name, att.Format)

        let opaqueSet = objects |> ASet.filter (not << isTransparent)
        let transparentSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r -> Some (transformTransparent r)
                    | _ -> None
                else None)

        // Pass-3 set: same transparent objects, but cloned with depth-write on,
        // Colors masked out, and the user's original surface intact. Drives the
        // "as-if-depth-test" pass that updates extras + depth based purely on
        // depth ordering.
        let transparentPickSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r -> Some (transformTransparentPick r)
                    | _ -> None
                else None)

        // Snapshot of the current transparent objects — used at Run to skip the
        // OIT sub-passes whenever there are none.
        let transparentContent = transparentSet.Content

        // Adaptive holders for the OIT textures consumed by the composite quad.
        let accumTex     : cval<ITexture> = cval (NullTexture.Instance)
        let revealageTex : cval<ITexture> = cval (NullTexture.Instance)

        // Adaptive holders for the A-buffer storage images.
        let abCountTex : cval<ITexture> = cval (NullTexture.Instance)
        let abSlotTex  : cval<ITexture> = cval (NullTexture.Instance)

        // Adaptive holder for the MSAA splat pass: the single-sample composite
        // texture the splat shader reads back (only used by the ABuffer path).
        let compositeInputTex : cval<ITexture> = cval (NullTexture.Instance)

        // A-buffer build/resolve object sets (transparent objects with the
        // interlocked insert composed; a fullscreen resolve quad).
        let aBufferBuildSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r ->
                        Some (transformTransparentABuffer abCountTex abSlotTex r)
                    | _ -> None
                else None)

        // MSAA splat set: the same transparent geometry re-rasterized into the
        // multisampled user framebuffer, sampling the single-sample composite.
        let aBufferSplatSet =
            objects |> ASet.choose (fun o ->
                if isTransparent o then
                    match o with
                    | :? RenderObject as r ->
                        Some (transformTransparentSplat compositeInputTex r)
                    | _ -> None
                else None)

        // Sample-count-dependent state. The OIT composite shader bakes in the
        // sample count and the framebuffer signatures bake in their sample
        // count, so all of this is rebuilt whenever the output framebuffer's
        // sample count changes.
        // "Direct" opaque-only task compiled against the user signature.
        // Used every frame in which the transparent set is currently empty:
        // render straight to the user framebuffer, no intermediate FBO and
        // no blits. Lazily built on first frame that hits the direct branch,
        // kept alive afterwards so re-entering direct mode is free.
        let mutable directOpaqueTask : IRenderTask voption = ValueNone

        let mutable currentSamples      = 0
        let mutable intermediateSig     : IFramebufferSignature voption = ValueNone
        let mutable oitSig              : IFramebufferSignature voption = ValueNone
        // The composite shader only writes Colors. We compile it against a
        // dedicated signature with just Colors + DepthStencil so FShade
        // doesn't synthesize varying passthroughs for the user's extra
        // attachments (which would then need to come from vertex attributes).
        let mutable compositeSig        : IFramebufferSignature voption = ValueNone
        let mutable opaqueTask          : IRenderTask voption = ValueNone
        let mutable transparentTask     : IRenderTask voption = ValueNone
        let mutable compositeTask       : IRenderTask voption = ValueNone
        let mutable transparentPickTask : IRenderTask voption = ValueNone
        let mutable aBufferBuildTask    : IRenderTask voption = ValueNone
        let mutable aBufferResolveTask  : IRenderTask voption = ValueNone
        let mutable splatTask            : IRenderTask voption = ValueNone

        // Bounded LRU cache of FBO bundles, one per (size, samples) the
        // wrapper has seen. Render tasks typically see only a handful of
        // distinct sizes (main window, preview pane, …); caching avoids
        // dispose+rebuild thrashing when the task alternates between them.
        // If an app churns through more sizes than `fboCacheCapacity`, the
        // least-recently-used bundle is evicted.
        let fboCacheCapacity = 4
        let fboCache =
            System.Collections.Generic.Dictionary<struct (V2i * int), FboBundle>()
        // Most-recently-used at head.
        let mutable fboLru : list<struct (V2i * int)> = []

        let mutable currentBundle : FboBundle voption = ValueNone

        let disposeBundle (b : FboBundle) =
            b.IntermediateFb.Dispose()
            b.OitFb.Dispose()
            b.CompositeFb.Dispose()
            b.DepthTex.Dispose()
            b.AccumT.Dispose()
            b.RevealageT.Dispose()
            for KeyValue(_, t) in b.ColorTex do t.Dispose()
            b.AbCount |> ValueOption.iter (fun t -> t.Dispose())
            b.AbSlot  |> ValueOption.iter (fun t -> t.Dispose())

        let releaseFbos () =
            for kvp in fboCache do disposeBundle kvp.Value
            fboCache.Clear()
            fboLru <- []
            currentBundle <- ValueNone

        let releaseTasksAndSignatures () =
            opaqueTask          |> ValueOption.iter (fun t -> t.Dispose())
            transparentTask     |> ValueOption.iter (fun t -> t.Dispose())
            compositeTask       |> ValueOption.iter (fun t -> t.Dispose())
            transparentPickTask |> ValueOption.iter (fun t -> t.Dispose())
            aBufferBuildTask    |> ValueOption.iter (fun t -> t.Dispose())
            aBufferResolveTask  |> ValueOption.iter (fun t -> t.Dispose())
            splatTask            |> ValueOption.iter (fun t -> t.Dispose())
            intermediateSig     |> ValueOption.iter (fun s -> s.Dispose())
            oitSig              |> ValueOption.iter (fun s -> s.Dispose())
            compositeSig        |> ValueOption.iter (fun s -> s.Dispose())
            opaqueTask          <- ValueNone
            transparentTask     <- ValueNone
            compositeTask       <- ValueNone
            transparentPickTask <- ValueNone
            aBufferBuildTask    <- ValueNone
            aBufferResolveTask  <- ValueNone
            splatTask            <- ValueNone
            intermediateSig     <- ValueNone
            oitSig              <- ValueNone
            compositeSig        <- ValueNone

        let releaseDirectTask () =
            directOpaqueTask |> ValueOption.iter (fun t -> t.Dispose())
            directOpaqueTask <- ValueNone

        let ensureForSamples (samples : int) =
            if samples <> currentSamples then
                // Tasks and signatures depend on the sample count → rebuild
                // everything (also forces FBO rebuild via releaseFbos).
                releaseFbos ()
                releaseTasksAndSignatures ()

                // Inner sample count for intermediate / OIT / composite. The
                // A-buffer technique runs single-sampled (the MSAA splat recovers
                // edge AA); weighted-blended keeps the user's sample count.
                let innerSamples = effectiveSamples samples

                let interSig =
                    let entries = seq {
                        yield! userColorAtts
                        yield DefaultSemantic.DepthStencil, depthFormat
                    }
                    runtime.CreateFramebufferSignature(entries, samples = innerSamples)

                // OIT signature: Accum + Revealage + DepthStencil only. The
                // composed transparent shader's outputs are pared down by
                // FShade to exactly these — any extras the user's shader
                // produces are dropped for this pass. Pass 3 is where extras
                // get written, with proper depth-test selection.
                let oS =
                    runtime.CreateFramebufferSignature(
                        [ WeightedBlendedOIT.Semantic.Accum,     TextureFormat.Rgba16f
                          WeightedBlendedOIT.Semantic.Revealage, TextureFormat.R32f
                          DefaultSemantic.DepthStencil,          depthFormat ],
                        samples = innerSamples)

                // Composite signature: only Colors + DepthStencil. FShade's
                // effect linker compiles outputs for every color attachment in
                // the signature; if extras (e.g. PickData) were included here,
                // the composite shader (which writes only Colors) would have
                // its missing outputs synthesized as varying passthroughs,
                // requiring matching vertex attributes that the fullscreen
                // quad doesn't have.
                let cS =
                    runtime.CreateFramebufferSignature(
                        [ DefaultSemantic.Colors,       userColorAtts
                                                       |> List.tryFind (fun (n, _) -> n = DefaultSemantic.Colors)
                                                       |> Option.map snd
                                                       |> Option.defaultValue TextureFormat.Rgba8
                          DefaultSemantic.DepthStencil, depthFormat ],
                        samples = innerSamples)

                let compositeObject = buildCompositeObject innerSamples accumTex revealageTex
                let compositeSet    = ASet.single (compositeObject :> IRenderObject)

                intermediateSig     <- ValueSome interSig
                oitSig              <- ValueSome oS
                compositeSig        <- ValueSome cS
                opaqueTask          <- ValueSome (compileRaw (interSig, opaqueSet))
                transparentPickTask <- ValueSome (compileRaw (interSig, transparentPickSet))

                match technique with
                | WeightedBlended ->
                    transparentTask <- ValueSome (compileRaw (oS, transparentSet))
                    compositeTask   <- ValueSome (compileRaw (cS, compositeSet))
                | ABuffer ->
                    // build + resolve both render to the Colors+DepthStencil
                    // composite signature (the storage is bound via uniforms,
                    // not attachments).
                    let resolveObject = buildABufferResolveObject abCountTex abSlotTex
                    let resolveSet    = ASet.single (resolveObject :> IRenderObject)
                    aBufferBuildTask   <- ValueSome (compileRaw (cS, aBufferBuildSet))
                    aBufferResolveTask <- ValueSome (compileRaw (cS, resolveSet))

                    // MSAA splat pass — re-rasterizes the transparent geometry
                    // into the (multisampled) user framebuffer, compiled against
                    // the user signature so its writes go directly to outFb.
                    splatTask <- ValueSome (compileRaw (userSig, aBufferSplatSet))

                currentSamples      <- samples

        let buildBundle (size : V2i) (samples : int) =
            // Internal textures all use the effective sample count (1 for
            // ABuffer, user samples for WeightedBlended).
            let innerSamples = effectiveSamples samples
            let dt = runtime.CreateTexture2D(size, depthFormat, samples = innerSamples)

            let ct =
                userSig.ColorAttachments
                |> Map.toList
                |> List.map (fun (_, att) ->
                    att.Name, runtime.CreateTexture2D(size, att.Format, samples = innerSamples))
                |> Map.ofList

            let at = runtime.CreateTexture2D(size, TextureFormat.Rgba16f, samples = innerSamples)
            let rt = runtime.CreateTexture2D(size, TextureFormat.R32f,    samples = innerSamples)

            let interAtts =
                ct
                |> Map.map (fun _ t -> t.GetOutputView())
                |> Map.add DefaultSemantic.DepthStencil (dt.GetOutputView())

            // OIT framebuffer: only Accum + Revealage + shared DepthStencil.
            // Extras are NOT bound here — pass 3 (against the intermediate
            // framebuffer) is where the depth-test selects the closest
            // transparent fragment's extras.
            let oitAtts =
                Map.ofList [
                    WeightedBlendedOIT.Semantic.Accum,     at.GetOutputView()
                    WeightedBlendedOIT.Semantic.Revealage, rt.GetOutputView()
                    DefaultSemantic.DepthStencil,          dt.GetOutputView()
                ]

            // Composite framebuffer: only Colors + DepthStencil, but
            // referencing the same backing textures as the intermediate
            // framebuffer so the composite's writes to Colors land in the
            // shared texture.
            let compositeAtts =
                Map.ofList [
                    DefaultSemantic.Colors,
                        (ct |> Map.tryFind DefaultSemantic.Colors
                            |> Option.map (fun t -> t.GetOutputView())
                            |> Option.defaultValue (dt.GetOutputView()))
                    DefaultSemantic.DepthStencil, dt.GetOutputView()
                ]

            // A-buffer storage: count (W×H, R32UI) + slot (W*Capacity×H, RGBA32UI).
            // Slot packs depth, color, and gl_SampleMaskIn into one image
            // (X=depth, Y=color, Z=mask) — see ABufferOIT.fs for the
            // binding-count rationale. Only when technique uses it.
            let abCount, abSlot =
                match technique with
                | ABuffer ->
                    let k = ABufferOIT.Capacity
                    let cnt = runtime.CreateTexture2D(size, TextureFormat.R32ui)
                    let sl  = runtime.CreateTexture2D(V2i(size.X * k, size.Y), TextureFormat.Rgba32ui)
                    ValueSome cnt, ValueSome sl
                | WeightedBlended ->
                    ValueNone, ValueNone

            { IntermediateFb = runtime.CreateFramebuffer(intermediateSig.Value, interAtts)
              OitFb          = runtime.CreateFramebuffer(oitSig.Value, oitAtts)
              CompositeFb    = runtime.CreateFramebuffer(compositeSig.Value, compositeAtts)
              DepthTex       = dt
              AccumT         = at
              RevealageT     = rt
              ColorTex       = ct
              AbCount        = abCount
              AbSlot         = abSlot }

        let touchLru (key : struct (V2i * int)) =
            // Move `key` to the front of the LRU list. If we exceed the
            // capacity, drop the tail entry and dispose its bundle.
            fboLru <- key :: (fboLru |> List.filter (fun k -> k <> key))
            if List.length fboLru > fboCacheCapacity then
                let rec dropTail = function
                    | [] | [_] as l -> l
                    | x :: rest -> x :: dropTail rest
                let evicted = List.last fboLru
                fboLru <- dropTail fboLru
                match fboCache.TryGetValue evicted with
                | true, b ->
                    fboCache.Remove evicted |> ignore
                    disposeBundle b
                | _ -> ()

        let ensureFbos (size : V2i) (samples : int) =
            let key = struct (size, samples)
            let bundle =
                match fboCache.TryGetValue key with
                | true, b -> b
                | _ ->
                    let b = buildBundle size samples
                    fboCache.[key] <- b
                    b
            touchLru key

            // Swap the adaptive texture references the composite quad reads
            // through so it picks up the right accum/revealage for this size.
            let mustTransact =
                match currentBundle with
                | ValueSome cur ->
                    not (obj.ReferenceEquals(cur.AccumT, bundle.AccumT))
                | ValueNone -> true

            if mustTransact then
                transact (fun () ->
                    accumTex.Value     <- bundle.AccumT :> ITexture
                    revealageTex.Value <- bundle.RevealageT :> ITexture
                    bundle.AbCount |> ValueOption.iter (fun t -> abCountTex.Value <- t :> ITexture)
                    bundle.AbSlot  |> ValueOption.iter (fun t -> abSlotTex. Value <- t :> ITexture)
                    // The splat shader samples the single-sample composite —
                    // the intermediate's Colors attachment after A-buffer resolve.
                    bundle.ColorTex
                    |> Map.tryFind DefaultSemantic.Colors
                    |> Option.iter (fun t -> compositeInputTex.Value <- t :> ITexture))

            currentBundle <- ValueSome bundle

        override x.FramebufferSignature = Some userSig
        override x.Runtime = Some runtime
        override x.Use f = f ()

        override x.PerformUpdate(token, rt) =
            directOpaqueTask    |> ValueOption.iter (fun t -> t.Update(token, rt))
            opaqueTask          |> ValueOption.iter (fun t -> t.Update(token, rt))
            transparentTask     |> ValueOption.iter (fun t -> t.Update(token, rt))
            compositeTask       |> ValueOption.iter (fun t -> t.Update(token, rt))
            transparentPickTask |> ValueOption.iter (fun t -> t.Update(token, rt))
            aBufferBuildTask    |> ValueOption.iter (fun t -> t.Update(token, rt))
            aBufferResolveTask  |> ValueOption.iter (fun t -> t.Update(token, rt))
            splatTask            |> ValueOption.iter (fun t -> t.Update(token, rt))

        override x.Perform(token, rt, output) =
            let outFb = output.Framebuffer
            let outSamples = outFb.Signature.Samples

            // Read through the current AdaptiveToken so this wrapper is
            // marked dirty when transparent objects appear / disappear —
            // important for dirty-driven render loops that only re-run
            // a task when one of its dependencies changes.
            let hasTransparent =
                not (HashSet.isEmpty (transparentContent.GetValue token))

            if not hasTransparent then
                // Fast direct path: no transparent objects right now. Render
                // the opaque set straight to the user framebuffer — no
                // intermediate FBO, no blits, no OIT machinery. The wrapped
                // resources (if previously built) stay alive for the next
                // frame where transparent objects reappear.
                match directOpaqueTask with
                | ValueNone ->
                    directOpaqueTask <- ValueSome (compileRaw (userSig, opaqueSet))
                | _ -> ()
                directOpaqueTask.Value.Run(token, rt, output)
            else

            // Wrapped path: build (or reuse) the intermediate / OIT / composite
            // signatures, sub-tasks, and framebuffers and do the full OIT
            // dance. The direct task is kept alive for the next opaque-only
            // frame.
            ensureForSamples outSamples
            ensureFbos outFb.Size outSamples

            let bundle = currentBundle.Value
            let inter = bundle.IntermediateFb

            match technique with
            | WeightedBlended ->
                // Seed the (MS) intermediate from the user FB and run opaque.
                runtime.Copy(outFb, inter)
                opaqueTask.Value.Run(token, rt, { output with Framebuffer = inter })

                let oit = bundle.OitFb
                let oitClear =
                    clear {
                        colors [
                            WeightedBlendedOIT.Semantic.Accum,     C4f.Zero
                            WeightedBlendedOIT.Semantic.Revealage, C4f.White
                        ]
                    }
                runtime.Clear(oit, oitClear)
                transparentTask.Value.Run(token, rt, { output with Framebuffer = oit })
                compositeTask.Value.Run(token, rt, { output with Framebuffer = bundle.CompositeFb })
                transparentPickTask.Value.Run(token, rt, { output with Framebuffer = inter })
                runtime.Copy(inter, outFb)

            | ABuffer ->
                // Ensure the direct opaque task exists — it's used here to render
                // the opaque scene at FULL MSAA straight into outFb.
                match directOpaqueTask with
                | ValueNone -> directOpaqueTask <- ValueSome (compileRaw (userSig, opaqueSet))
                | _ -> ()

                // 1. Single-sample backdrop + depth in the intermediate. Opaque
                //    is re-rendered here (samples=1) purely as the compositing
                //    backdrop and depth source for the A-buffer build; this copy
                //    is never shown (it's covered by transparent geometry).
                runtime.Clear(inter, clear { color C4f.Black; depth 1.0 })
                opaqueTask.Value.Run(token, rt, { output with Framebuffer = inter })

                // 2. Build + resolve the single-sample A-buffer. The resolve
                //    composites the transparent stack over the backdrop into the
                //    intermediate's Colors attachment (= the composite the splat
                //    pass reads). Single-sample, so no per-primitive coverage
                //    seam and no per-sample storage.
                bundle.AbCount |> ValueOption.iter (fun c ->
                    runtime.Clear(c, clear { color C4ui.Zero }))
                aBufferBuildTask.Value.Run(token, rt, { output with Framebuffer = bundle.CompositeFb })
                aBufferResolveTask.Value.Run(token, rt, { output with Framebuffer = bundle.CompositeFb })

                // 3. Opaque at full MSAA straight into outFb (also writes the
                //    opaque depth the splat tests against). Opaque-only pixels
                //    keep their MSAA quality.
                directOpaqueTask.Value.Run(token, rt, output)

                // 4. Splat: re-rasterize the transparent geometry into outFb
                //    (MS), each fragment reading the single-sample composite at
                //    its pixel. The rasterizer's per-sample coverage gives the
                //    anti-aliased silhouette; depth-test against opaque depth
                //    drops transparent fragments behind opaque.
                splatTask.Value.Run(token, rt, output)

        override x.Release() =
            releaseDirectTask ()
            releaseTasksAndSignatures ()
            releaseFbos ()

    /// Creates a render task that automatically groups transparent + opaque
    /// objects through Weighted Blended OIT.
    let create (runtime   : IRuntime)
               (signature : IFramebufferSignature)
               (objects   : aset<IRenderObject>)
               (compileRaw: IFramebufferSignature * aset<IRenderObject> -> IRenderTask) : IRenderTask =
        new WrappedTask(runtime, signature, objects, compileRaw) :> IRenderTask
