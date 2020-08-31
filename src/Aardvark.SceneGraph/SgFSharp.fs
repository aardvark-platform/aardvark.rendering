namespace Aardvark.SceneGraph

open System
open Aardvark.Base

open Aardvark.Base.Ag
open Aardvark.Rendering
open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Data.Adaptive

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module SgFSharp =

    module Sg =

        let uniform (name : string) (value : aval<'a>) (sg : ISg) =
            Sg.UniformApplicator(name, value :> IAdaptiveValue, sg) :> ISg

        let trafo (m : aval<Trafo3d>) (sg : ISg) =
            Sg.TrafoApplicator(m, sg) :> ISg

        let viewTrafo (m : aval<Trafo3d>) (sg : ISg) =
            Sg.ViewTrafoApplicator(m, sg) :> ISg

        let projTrafo (m : aval<Trafo3d>) (sg : ISg) =
            Sg.ProjectionTrafoApplicator(m, sg) :> ISg

        let scale (s : float) (sg : ISg) =
            sg |> trafo (s |> Trafo3d.Scale |> AVal.constant)

        let translate (x : float) (y : float) (z : float) (sg : ISg) =
            sg |> trafo (Trafo3d.Translation(x,y,z) |> AVal.constant)

        let transform (t : Trafo3d) (sg : ISg) =
            sg |> trafo (t |> AVal.constant)



        let camera (cam : aval<Camera>) (sg : ISg) =
            sg |> viewTrafo (cam |> AVal.map Camera.viewTrafo) |> projTrafo (cam |> AVal.map Camera.projTrafo)

        let surface (m : ISurface) (sg : ISg) =
            Sg.SurfaceApplicator(Surface.Backend m, sg) :> ISg

        let set (set : aset<ISg>) =
            Sg.Set(set) :> ISg

        let ofSeq (s : seq<#ISg>) =
            s |> Seq.cast<ISg> |> ASet.ofSeq |> Sg.Set :> ISg

        let ofList (l : list<#ISg>) =
            l |> ofSeq

        let empty = ofSeq Seq.empty

        let ofArray (arr : array<#ISg>) =
            arr |> ofSeq



        let andAlso (sg : ISg) (andSg : ISg) =
            Sg.Set [sg; andSg] :> ISg

        let geometrySet mode attributeTypes (geometries : aset<_>) =
            Sg.GeometrySet(geometries,mode,attributeTypes) :> ISg

        let dynamic (s : aval<ISg>) =
            Sg.DynamicNode(s) :> ISg

        let onOff (active : aval<bool>) (sg : ISg) =
            Sg.OnOffNode(active, sg) :> ISg

        let texture (sem : Symbol) (tex : aval<ITexture>) (sg : ISg) =
            Sg.TextureApplicator(sem, tex, sg) :> ISg

        let diffuseTexture (tex : aval<ITexture>) (sg : ISg) =
            texture DefaultSemantic.DiffuseColorTexture tex sg

        let diffuseTexture' (tex : ITexture) (sg : ISg) =
            texture DefaultSemantic.DiffuseColorTexture (AVal.constant tex) sg

        let diffuseFileTexture' (path : string) (wantMipMaps : bool) (sg : ISg) =
            texture DefaultSemantic.DiffuseColorTexture (AVal.constant (FileTexture(path, wantMipMaps) :> ITexture)) sg

        let fileTexture (sym : Symbol) (path : string) (wantMipMaps : bool) (sg : ISg) =
            texture sym (AVal.constant (FileTexture(path, wantMipMaps) :> ITexture)) sg

        let scopeDependentTexture (sem : Symbol) (tex : Scope -> aval<ITexture>) (sg : ISg) =
            Sg.UniformApplicator(new Providers.ScopeDependentUniformHolder([sem, fun s -> tex s :> IAdaptiveValue]), sg) :> ISg

        let scopeDependentDiffuseTexture (tex : Scope -> aval<ITexture>) (sg : ISg) =
            scopeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg

        let runtimeDependentTexture (sem : Symbol) (tex : IRuntime -> aval<ITexture>) (sg : ISg) =
            let cache = Dictionary<IRuntime, aval<ITexture>>()
            let tex runtime =
                match cache.TryGetValue runtime with
                    | (true, v) -> v
                    | _ ->
                        let v = tex runtime
                        cache.[runtime] <- v
                        v

            scopeDependentTexture sem (fun s -> s?Runtime |> tex) sg

        let runtimeDependentDiffuseTexture(tex : IRuntime -> aval<ITexture>) (sg : ISg) =
            runtimeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg

        let samplerState (sem : Symbol) (state : aval<Option<SamplerState>>) (sg : ISg) =
            let modifier =
                adaptive {
                    let! user = state
                    return fun (textureSem : Symbol) (state : SamplerState) ->
                        if sem = textureSem then
                            match user with
                                | Some state -> state
                                | _ -> state
                        else
                            state
                }
            sg |> uniform (string DefaultSemantic.SamplerStateModifier) modifier

        let modifySamplerState (sem : Symbol) (modifier : aval<SamplerState -> SamplerState>) (sg : ISg) =
            let modifier =
                adaptive {
                    let! modifier = modifier
                    return fun (textureSem : Symbol) (state : SamplerState) ->
                        if sem = textureSem then
                            modifier state
                        else
                            state
                }
            sg |> uniform (string DefaultSemantic.SamplerStateModifier) modifier

        // ================================================================================================================
        // Blending
        // ================================================================================================================

        /// Sets the global blend mode for all color attachments.
        let blendMode (mode : aval<BlendMode>) (sg : ISg) =
            Sg.BlendModeApplicator(mode, sg) :> ISg

        /// Sets the global blend mode for all color attachments.
        let blendMode' mode = blendMode (AVal.init mode)


        /// Sets the blend modes for the given color attachments (overriding the global blend mode).
        let blendModes (modes : aval<Map<Symbol, BlendMode>>) (sg : ISg) =
            Sg.AttachmentBlendModeApplicator(modes, sg) :> ISg

        /// Sets the blend modes for the given color attachments (overriding the global blend mode).
        let blendModes' modes = blendModes (AVal.init modes)


        /// Sets the blend constant color.
        let inline blendConstant (color : aval<'a>) (sg : ISg) =
            if typeof<'a> = typeof<C4f> then
                Sg.BlendConstantApplicator(color :?> aval<C4f>, sg) :> ISg
            else
                Sg.BlendConstantApplicator(color |> AVal.map c4f, sg) :> ISg

        /// Sets the blend constant color.
        let inline blendConstant' color = blendConstant (AVal.init color)


        /// Sets the global color write mask for all color attachments.
        let colorMask (mask : aval<ColorMask>) (sg : ISg) =
            Sg.ColorWriteMaskApplicator(mask, sg) :> ISg

        /// Sets the global color write mask for all color attachments.
        let colorMask' mask = colorMask (AVal.init mask)


        /// Sets the color write masks for the given color attachments (overriding the global mask).
        let colorMasks (masks : aval<Map<Symbol, ColorMask>>) (sg : ISg) =
            Sg.AttachmentColorWriteMaskApplicator(masks, sg) :> ISg

        /// Sets the color write masks for the given color attachments (overriding the global mask).
        let colorMasks' masks = colorMasks (AVal.init masks)


        /// Sets the color write mask for all color attachments to either ColorMask.None or ColorMask.All.
        let colorWrite (enabled : aval<bool>) (sg : ISg) =
            Sg.ColorWriteMaskApplicator(enabled, sg) :> ISg

        /// Sets the color write mask for all color attachments to either ColorMask.None or ColorMask.All.
        let colorWrite' enabled = colorWrite (AVal.init enabled)


        /// Sets the color write masks for the given color attachments to either
        /// ColorMask.None or ColorMask.All (overriding the global mask).
        let colorWrites (enabled : aval<Map<Symbol, bool>>) (sg : ISg) =
            Sg.AttachmentColorWriteMaskApplicator(enabled, sg) :> ISg

        /// Sets the color write masks for the given color attachments to either
        /// ColorMask.None or ColorMask.All (overriding the global mask).
        let colorWrites' enabled = colorWrites (AVal.init enabled)


        /// Restricts color output to the given attachments.
        let colorOutput (enabled : aval<Set<Symbol>>) =
            colorWrite' false
            >> colorMasks (enabled |> AVal.map ColorMask.ofWriteSet)

        /// Restricts color output to the given attachments.
        let colorOutput' enabled = colorOutput (AVal.init enabled)

        // ================================================================================================================
        // Depth
        // ================================================================================================================

        /// Sets the depth test.
        let depthTest (test : aval<DepthTest>) (sg : ISg) =
            Sg.DepthTestApplicator(test, sg) :> ISg

        /// Sets the depth test.
        let depthTest' test = depthTest (AVal.init test)


        /// Enables or disables depth writing.
        let depthWrite (depthWriteEnabled : aval<bool>) (sg : ISg) =
            Sg.DepthWriteMaskApplicator(depthWriteEnabled, sg) :> ISg

        /// Enables or disables depth writing.
        let depthWrite' depthWriteEnabled = depthWrite (AVal.init depthWriteEnabled)


        /// Sets the depth bias.
        let depthBias (bias : aval<DepthBias>) (sg: ISg) =
            Sg.DepthBiasApplicator(bias, sg) :> ISg

        /// Sets the depth bias.
        let depthBias' bias = depthBias (AVal.init bias)


        /// Enables or disables depth clamping.
        let depthClamp (clamp : aval<bool>) (sg: ISg) =
            Sg.DepthClampApplicator(clamp, sg) :> ISg

        /// Enables or disables depth clamping.
        let depthClamp' clamp = depthClamp (AVal.init clamp)

        // ================================================================================================================
        // Stencil
        // ================================================================================================================

        /// Sets the stencil mode for front-facing polygons.
        let stencilModeFront (mode : aval<StencilMode>) (sg : ISg) =
            Sg.StencilModeFrontApplicator(mode, sg) :> ISg

        /// Sets the stencil mode for front-facing polygons.
        let stencilModeFront' mode = stencilModeFront (AVal.init mode)


        /// Sets the stencil write mask for front-facing polygons.
        let stencilWriteMaskFront (mask : aval<StencilMask>) (sg : ISg) =
            Sg.StencilWriteMaskFrontApplicator(mask, sg) :> ISg

        /// Sets the stencil write mask for front-facing polygons.
        let stencilWriteMaskFront' mask = stencilWriteMaskFront (AVal.init mask)


        /// Enables or disables stencil write for front-facing polygons.
        let stencilWriteFront (enabled : aval<bool>) (sg : ISg) =
            Sg.StencilWriteMaskFrontApplicator(enabled, sg) :> ISg

        /// Enables or disables stencil write for front-facing polygons.
        let stencilWriteFront' enabled = stencilWriteFront (AVal.init enabled)


        /// Sets the stencil mode for back-facing polygons.
        let stencilModeBack (mode : aval<StencilMode>) (sg : ISg) =
            Sg.StencilModeBackApplicator(mode, sg) :> ISg

        /// Sets the stencil mode for back-facing polygons.
        let stencilModeBack' mode = stencilModeBack (AVal.init mode)


        /// Sets the stencil write mask for back-facing polygons.
        let stencilWriteMaskBack (mask : aval<StencilMask>) (sg : ISg) =
            Sg.StencilWriteMaskBackApplicator(mask, sg) :> ISg

        /// Sets the stencil write mask for back-facing polygons.
        let stencilWriteMaskBack' mask = stencilWriteMaskBack (AVal.init mask)


        /// Enables or disables stencil write for back-facing polygons.
        let stencilWriteBack (enabled : aval<bool>) (sg : ISg) =
            Sg.StencilWriteMaskBackApplicator(enabled, sg) :> ISg

        /// Enables or disables stencil write for back-facing polygons.
        let stencilWriteBack' enabled = stencilWriteBack (AVal.init enabled)


        /// Sets separate stencil modes for front- and back-facing polygons.
        let stencilModes (front : aval<StencilMode>) (back : aval<StencilMode>) =
            stencilModeFront front >> stencilModeBack back

        /// Sets separate stencil modes for front- and back-facing polygons.
        let stencilModes' front back = stencilModes (AVal.init front) (AVal.init back)


        /// Sets separate stencil write masks for front- and back-facing polygons.
        let stencilWriteMasks (front : aval<StencilMask>) (back : aval<StencilMask>) =
            stencilWriteMaskFront front >> stencilWriteMaskBack back

        /// Sets separate stencil write masks for front- and back-facing polygons.
        let stencilWriteMasks' front back = stencilWriteMasks (AVal.init front) (AVal.init back)


        /// Enables or disables stencil write for front- and back-facing polygons.
        let stencilWrites (front : aval<bool>) (back : aval<bool>) =
            stencilWriteFront front >> stencilWriteBack back

        /// Enables or disables stencil write for front- and back-facing polygons.
        let stencilWrites' front back = stencilWrites (AVal.init front) (AVal.init back)


        /// Sets the stencil mode.
        let stencilMode (mode : aval<StencilMode>) =
            stencilModes mode mode

        /// Sets the stencil mode.
        let stencilMode' mode = stencilMode (AVal.init mode)


        /// Sets the stencil write mask.
        let stencilWriteMask (mask : aval<StencilMask>) =
            stencilWriteMasks mask mask

        /// Sets the stencil write mask.
        let stencilWriteMask' mask = stencilWriteMask (AVal.init mask)


        /// Enables or disables stencil write.
        let stencilWrite (enabled : aval<bool>) =
            stencilWrites enabled enabled

        /// Enables or disables stencil write.
        let stencilWrite' enabled = stencilWrite (AVal.init enabled)

        // ================================================================================================================
        // Write buffers
        // ================================================================================================================
        let writeBuffers (buffers : aval<Set<Symbol>>) =

            let depthEnable =
                buffers |> AVal.map (Set.contains DefaultSemantic.Depth)

            let stencilEnable =
                buffers |> AVal.map (Set.contains DefaultSemantic.Stencil)

            depthWrite depthEnable
            >> stencilWrite stencilEnable
            >> colorOutput buffers

        let writeBuffers' (buffers : Set<Symbol>) =
            writeBuffers (AVal.constant buffers)

        // ================================================================================================================
        // Rasterizer
        // ================================================================================================================
        let cullMode (mode : aval<CullMode>) (sg : ISg) =
            Sg.CullModeApplicator(mode, sg) :> ISg

        let frontFace (order : aval<WindingOrder>) (sg: ISg) =
            Sg.FrontFaceApplicator(order, sg) :> ISg

        let fillMode (mode : aval<FillMode>) (sg : ISg) =
            Sg.FillModeApplicator(mode, sg) :> ISg

        let multisample (mode : aval<bool>) (sg : ISg) =
            Sg.MultisampleApplicator(mode, sg) :> ISg

        let conservativeRaster (mode : aval<bool>) (sg : ISg) =
            Sg.ConservativeRasterApplicator(mode, sg) :> ISg

        let cullMode' mode                = cullMode (AVal.init mode)
        let frontFace' order              = frontFace (AVal.init order)
        let fillMode' mode                = fillMode (AVal.init mode)
        let multisample' mode             = multisample (AVal.init mode)
        let conservativeRaster' mode      = conservativeRaster (AVal.init mode)

        let private arrayModCache = ConditionalWeakTable<IAdaptiveValue, aval<Array>>()
        let private bufferModCache = ConditionalWeakTable<IAdaptiveValue, BufferView>()

        let private modOfArray (m : aval<'a[]>) =
            match arrayModCache.TryGetValue (m :> IAdaptiveValue) with
                | (true, r) -> r
                | _ ->
                    let r = m |> AVal.map (fun a -> a :> Array)
                    arrayModCache.Add(m, r)
                    r

        let private bufferOfArray (m : aval<'a[]>) =
            match bufferModCache.TryGetValue (m :> IAdaptiveValue) with
                | (true, r) -> r
                | _ ->
                    let b = m |> AVal.map (fun a -> ArrayBuffer a :> IBuffer)
                    let r = BufferView(b, typeof<'a>)
                    bufferModCache.Add(m, r)
                    r

        let vertexAttribute<'a when 'a : struct> (s : Symbol) (value : aval<'a[]>) (sg : ISg) =
            let view = BufferView(value |> AVal.map (fun data -> ArrayBuffer(data) :> IBuffer), typeof<'a>)
            Sg.VertexAttributeApplicator(Map.ofList [s, view], AVal.constant sg) :> ISg

        let index<'a when 'a : struct> (value : aval<'a[]>) (sg : ISg) =
            Sg.VertexIndexApplicator(bufferOfArray value, sg) :> ISg

        let vertexAttribute'<'a when 'a : struct> (s : Symbol) (value : 'a[]) (sg : ISg) =
            let view = BufferView(AVal.constant (ArrayBuffer(value :> Array) :> IBuffer), typeof<'a>)
            Sg.VertexAttributeApplicator(Map.ofList [s, view], AVal.constant sg) :> ISg

        let index'<'a when 'a : struct> (value : 'a[]) (sg : ISg) =
            Sg.VertexIndexApplicator(BufferView.ofArray value, sg) :> ISg

        let vertexBuffer (s : Symbol) (view : BufferView) (sg : ISg) =
            Sg.VertexAttributeApplicator(s, view, sg) :> ISg

        let vertexArray (s : Symbol) (value : System.Array) (sg : ISg) =
            let view = BufferView(AVal.constant (ArrayBuffer value :> IBuffer), value.GetType().GetElementType())
            Sg.VertexAttributeApplicator(Map.ofList [s, view], AVal.constant sg) :> ISg

        let instanceBuffer (s : Symbol) (view : BufferView) (sg : ISg) =
            Sg.InstanceAttributeApplicator(s, view, sg) :> ISg

        let instanceArray (s : Symbol) (value : System.Array) (sg : ISg) =
            let view = BufferView(AVal.constant (ArrayBuffer value :> IBuffer), value.GetType().GetElementType())
            Sg.InstanceAttributeApplicator(s, view, sg) :> ISg

        let vertexBufferValue (s : Symbol) (value : aval<V4f>) (sg : ISg) =
            let view = BufferView(SingleValueBuffer(value), typeof<V4f>)
            Sg.VertexAttributeApplicator(s, view, sg) :> ISg

        let draw (mode : IndexedGeometryMode) =
            Sg.RenderNode(
                DrawCallInfo(
                    FirstInstance = 0,
                    InstanceCount = 1,
                    FirstIndex = 0,
                    FaceVertexCount = -1,
                    BaseVertex = 0
                ),
                mode
            ) :> ISg

        let render (mode : IndexedGeometryMode) (call : DrawCallInfo) =
            Sg.RenderNode(call,mode)

        let indirectDraw (mode : IndexedGeometryMode) (buffer : aval<IndirectBuffer>) =
            Sg.IndirectRenderNode(buffer, mode) :> ISg

        let ofIndexedGeometry (g : IndexedGeometry) =
            let attributes =
                g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) ->
                    let t = v.GetType().GetElementType()
                    let view = BufferView(AVal.constant (ArrayBuffer(v) :> IBuffer), t)

                    k, view
                ) |> Map.ofSeq


            let index, faceVertexCount =
                if g.IsIndexed then
                    g.IndexArray, g.IndexArray.Length
                else
                    null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

            let call =
                DrawCallInfo(
                    FaceVertexCount = faceVertexCount,
                    FirstIndex = 0,
                    InstanceCount = 1,
                    FirstInstance = 0,
                    BaseVertex = 0
                )

            let sg = Sg.VertexAttributeApplicator(attributes, Sg.RenderNode(call,g.Mode)) :> ISg
            if not (isNull index) then
                Sg.VertexIndexApplicator(BufferView.ofArray index, sg) :> ISg
            else
                sg

        module private Interleaved =
            open System.Reflection
            open Microsoft.FSharp.NativeInterop

            type Converter() =

                static let toFloat32 (i : 'a) : float32 =
                    let mutable i = i
                    NativePtr.read (&&i |> NativePtr.toNativeInt |> NativePtr.ofNativeInt)

                static let accessors =
                    Dict.ofList [
                        typeof<int8>,    (1, (fun (v : int8)    -> [|toFloat32 (int32 v)|]) :> obj)
                        typeof<uint8>,   (1, (fun (v : uint8)   -> [|toFloat32 (uint32 v)|]) :> obj)
                        typeof<int16>,   (1, (fun (v : int16)   -> [|toFloat32 (int32 v)|]) :> obj)
                        typeof<uint16>,  (1, (fun (v : uint16)  -> [|toFloat32 (uint32 v)|]) :> obj)
                        typeof<int32>,   (1, (fun (v : int32)   -> [|toFloat32 v|]) :> obj)
                        typeof<uint32>,  (1, (fun (v : uint32)  -> [|toFloat32 v|]) :> obj)

                        typeof<float>,   (1, (fun (v : float) -> [|float32 v|]) :> obj)
                        typeof<V2d>,     (2, (fun (v : V2d) -> [|float32 v.X; float32 v.Y|]) :> obj)
                        typeof<V3d>,     (3, (fun (v : V3d) -> [|float32 v.X; float32 v.Y; float32 v.Z|]) :> obj)
                        typeof<V4d>,     (4, (fun (v : V4d) -> [|float32 v.X; float32 v.Y; float32 v.Z; float32 v.W|]) :> obj)

                        typeof<float32>, (1, (fun (v : float32) -> [|v|]) :> obj)
                        typeof<V2f>,     (2, (fun (v : V2f) -> [|v.X; v.Y|]) :> obj)
                        typeof<V3f>,     (3, (fun (v : V3f) -> [|v.X; v.Y; v.Z|]) :> obj)
                        typeof<V4f>,     (4, (fun (v : V4f) -> [|v.X; v.Y; v.Z; v.W|]) :> obj)

                        typeof<C3f>,     (3, (fun (c : C3f) -> [|c.R; c.G; c.B|]) :> obj)
                        typeof<C4f>,     (4, (fun (c : C4f) -> [|c.R; c.G; c.B; c.A|]) :> obj)
                        typeof<C3b>,     (3, (fun (v : C3b) -> let c = v.ToC3f() in [|c.R; c.G; c.B|]) :> obj)
                        typeof<C4b>,     (4, (fun (v : C4b) -> let c = v.ToC4f() in [|c.R; c.G; c.B; c.A|]) :> obj)
                    ]

                static member ToFloatArray (arr : 'a[]) : float32[] =
                    match accessors.TryGetValue typeof<'a> with
                        | (true, (_,accessor)) ->
                            arr |> Array.collect (unbox accessor)
                        | _ -> failwithf "unsupported attribute type: %A" typeof<'a>.FullName


                static member GetDimension (t : Type) =
                    match accessors.TryGetValue t with
                        | (true, (d, arr)) -> d
                        | _ -> failwithf "unsupported attribute type: %A" t.FullName

            let toFloatArrayMeth = typeof<Converter>.GetMethod("ToFloatArray", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)

            let toFloatArray (arr : Array) =
                let t = arr.GetType().GetElementType()
                let mi = toFloatArrayMeth.MakeGenericMethod [|t|]
                let arr = mi.Invoke(null, [|arr|]) |> unbox<float32[]>

                Converter.GetDimension(t), arr


        let ofIndexedGeometryInterleaved (attributes : list<Symbol>) (g : IndexedGeometry) =
            let arrays =
                attributes |> List.choose (fun att ->
                    match g.IndexedAttributes.TryGetValue att with
                        | (true, v) ->
                            let (dim, arr) = Interleaved.toFloatArray v
                            Some (att, v.GetType().GetElementType(), dim, arr)
                        | _ -> None
                )

            let count = arrays |> List.map (fun (_,_,dim,arr) -> arr.Length / dim) |> List.min
            let vertexSize = arrays |> List.map (fun (_,_,d,_) -> d) |> List.sum

            let views = SymbolDict()
            let target = Array.zeroCreate (count * vertexSize)
            let buffer = AVal.constant (ArrayBuffer target :> IBuffer)
            let mutable current = 0
            for vi in 0..count-1 do
                for (sem, t, size, a) in arrays do
                    let mutable start = size * vi
                    for c in 0..size-1 do
                        target.[current] <- a.[start]
                        current <- current + 1
                        start <- start + 1

            let mutable offset = 0
            for (sem, t, size, a) in arrays do
                let v = BufferView(buffer, t, offset * sizeof<float32>, vertexSize * sizeof<float32>)
                views.[sem] <- v
                offset <- offset + size



            let index, faceVertexCount =
                if g.IsIndexed then
                    g.IndexArray, g.IndexArray.Length
                else
                    null, count

            let call =
                DrawCallInfo(
                    FaceVertexCount = faceVertexCount,
                    FirstIndex = 0,
                    InstanceCount = 1,
                    FirstInstance = 0,
                    BaseVertex = 0
                )

            let sg = Sg.VertexAttributeApplicator(views, Sg.RenderNode(call,g.Mode)) :> ISg
            if index <> null then
                Sg.VertexIndexApplicator(BufferView.ofArray index, sg) :> ISg
            else
                sg

        let instancedGeometry (trafos : aval<Trafo3d[]>) (g : IndexedGeometry) =
            let vertexAttributes =
                g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) ->
                    let t = v.GetType().GetElementType()
                    let view = BufferView(AVal.constant (ArrayBuffer(v) :> IBuffer), t)

                    k, view
                ) |> Map.ofSeq

            let index, faceVertexCount =
                if g.IsIndexed then
                    g.IndexArray, g.IndexArray.Length
                else
                    null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

            let call = trafos |> AVal.map (fun t ->
                    DrawCallInfo(
                        FaceVertexCount = faceVertexCount,
                        FirstIndex = 0,
                        InstanceCount = t.Length,
                        FirstInstance = 0,
                        BaseVertex = 0
                    )
                )

            let sg = Sg.VertexAttributeApplicator(vertexAttributes, Sg.RenderNode(call, g.Mode)) :> ISg

            let sg =
                if index <> null then
                    Sg.VertexIndexApplicator(BufferView.ofArray  index, sg) :> ISg
                else
                    sg

            let m44Trafos = trafos |> AVal.map (fun a -> a |> Array.map (fun (t : Trafo3d) -> (M44f.op_Explicit t.Forward)) :> Array)
            let m44View = BufferView(m44Trafos |> AVal.map (fun a -> ArrayBuffer a :> IBuffer), typeof<M44f>)

            Sg.InstanceAttributeApplicator([DefaultSemantic.InstanceTrafo, m44View] |> Map.ofList, sg) :> ISg

        let pass (pass : RenderPass) (sg : ISg) = Sg.PassApplicator(pass, sg) :> ISg

        let normalizeToAdaptive (box : Box3d) (this : ISg) =

            let getBoxScale (fromBox : Box3d) (toBox : Box3d) : float =
                let fromSize = fromBox.Size
                let toSize = toBox.Size
                let factor = toSize / fromSize

                let mutable smallest = factor.X

                if factor.Y < smallest then
                    smallest <- factor.Y
                if factor.Z < smallest then
                    smallest <- factor.Z

                smallest

            let bb = this?GlobalBoundingBox(Ag.Scope.Root) : aval<Box3d>

            printfn "normalizing from: %A" ( bb.GetValue() )

            let transformBox (sbox : Box3d) = Trafo3d.Translation(-sbox.Center) * Trafo3d.Scale(getBoxScale sbox box) * Trafo3d.Translation(box.Center)

            Sg.TrafoApplicator(AVal.map transformBox bb, this) :> ISg

        let normalizeTo (box : Box3d) (this : ISg) =

            let getBoxScale (fromBox : Box3d) (toBox : Box3d) : float =
                let fromSize = fromBox.Size
                let toSize = toBox.Size
                let factor = toSize / fromSize

                let mutable smallest = factor.X

                if factor.Y < smallest then
                    smallest <- factor.Y
                if factor.Z < smallest then
                    smallest <- factor.Z

                smallest

            let bb = this?GlobalBoundingBox(Ag.Scope.Root) : aval<Box3d>

            let transformBox (sbox : Box3d) = Trafo3d.Translation(-sbox.Center) * Trafo3d.Scale(getBoxScale sbox box) * Trafo3d.Translation(box.Center)

            Sg.TrafoApplicator(bb.GetValue() |> transformBox |> AVal.constant, this) :> ISg

        let normalizeAdaptive sg = sg |> normalizeToAdaptive ( Box3d( V3d(-1,-1,-1), V3d(1,1,1) ) )

        let normalize sg = sg |> normalizeTo ( Box3d( V3d(-1,-1,-1), V3d(1,1,1) ) )

        let adapter (o : obj) = Sg.AdapterNode(o) :> ISg

        let overlay (task : IRenderTask) =
            Sg.OverlayNode(task) :> ISg


    type IndexedGeometry with
        member x.Sg =
            Sg.ofIndexedGeometry x