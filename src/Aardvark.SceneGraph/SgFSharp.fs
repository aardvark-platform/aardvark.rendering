namespace Aardvark.SceneGraph

open System
open Aardvark.Base

open Aardvark.Base.Ag
open Aardvark.Rendering
open System.Runtime.CompilerServices
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators

#nowarn "9"
#nowarn "51"

[<RequireQualifiedAccess>]
type WriteBuffer =
    | Color of Symbol
    | Depth
    | Stencil

    static member Colors([<ParamArray>] names : Symbol[]) =
        names |> Array.map WriteBuffer.Color

    static member op_Explicit(name : Symbol) = WriteBuffer.Color name

[<AutoOpen>]
module SgFSharp =

    module SgFSharpHelpers =

        type SgEffectBuilder() =
            inherit EffectBuilder()

            member x.Run(f : unit -> list<FShadeEffect>) =
                let surface = f() |> FShade.Effect.compose |> Surface.Effect
                fun (sg : ISg) -> Sg.SurfaceApplicator(surface, sg) :> ISg

        // Utilities to create cached buffer views
        module Caching =

            // Note: we need these caches because of the AVal.maps below
            let private cache = ConditionalWeakTable<IAdaptiveValue, obj>()

            let private getOrCreate (create: 'T1 -> 'T2) (value: 'T1) : 'T2 =
                match cache.TryGetValue value with
                | (true, r) when r.GetType() = typeof<'T2> -> unbox<'T2> r
                | _ ->
                    let r = create value
                    cache.Add(value, r)
                    r

            let bufferOfArray (value: aval<'T[]>) : BufferView =
                value |> getOrCreate (fun value ->
                    let b = value |> AVal.map (fun a -> ArrayBuffer a :> IBuffer)
                    BufferView(b, typeof<'T>)
                )

            let buffersOfTrafos (value: aval<Trafo3d[]>) : BufferView * BufferView =
                value |> getOrCreate (fun value ->
                    let forward = value |> AVal.map (fun a ->
                        let a = a |> Array.map (Trafo.forward >> M44f)
                        ArrayBuffer a :> IBuffer
                    )

                    let backward = value |> AVal.map (fun a ->
                        let a = a |> Array.map (Trafo.backward >> M44f)
                        ArrayBuffer a :> IBuffer
                    )

                    BufferView(forward, typeof<M44f>), BufferView(backward, typeof<M44f>)
                )

    module Sg =
        open SgFSharpHelpers

        // ================================================================================================================
        // Utilities
        // ================================================================================================================

        /// Combines the scene graphs in the given adaptive set.
        let set (set : aset<ISg>) =
            Sg.Set(set) :> ISg

        /// Combines the scene graphs in the given sequence.
        let ofSeq (s : seq<#ISg>) =
            s |> Seq.cast<ISg> |> ASet.ofSeq |> Sg.Set :> ISg

        /// Combines the scene graphs in the given list.
        let ofList (l : list<#ISg>) =
            l |> ofSeq

        /// Combines the scene graphs in the given array.
        let ofArray (arr : array<#ISg>) =
            arr |> ofSeq

        /// Combines two scene graphs.
        let andAlso (sg : ISg) (andSg : ISg) =
            Sg.Set [sg; andSg] :> ISg

        /// Empty scene graph.
        let empty = ofSeq Seq.empty

        /// Unwraps an adaptive scene graph.
        let dynamic (s : aval<ISg>) =
            Sg.DynamicNode(s) :> ISg

        /// Toggles visibility of the scene.
        let onOff (active : aval<bool>) (sg : ISg) =
            Sg.OnOffNode(active, sg) :> ISg

        /// Inserts an arbitrary object as node in the scene graph.
        let adapter (o : obj) =
            Sg.AdapterNode(o) :> ISg

        /// Combines the render objects in the given adaptive set.
        let renderObjectSet (s : #aset<IRenderObject>) =
            Sg.RenderObjectNode(s) :> ISg

        /// Applies the given activation function to the given scene graph.
        /// An activation function is invoked when the render objects of the scene graph are prepared.
        /// The resulting IDisposable is disposed when the render objects are disposed.
        let onActivation (f : unit -> IDisposable) (sg : ISg) =
            Sg.ActivationApplicator(f, sg) :> ISg

        /// Generates a scene graph depending on the scope.
        let delay (generator : Ag.Scope -> ISg) =
            Sg.DelayNode(generator) :> ISg

        // ================================================================================================================
        // Uniforms & Textures
        // ================================================================================================================

        /// Sets the uniform with the given name to the given value.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline uniform (name : ^Name) (value : aval<'Value>) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'Value>
            Sg.UniformApplicator(sym, value, sg) :> ISg

        /// Sets the uniform with the given name to the given value.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline uniform' (name : ^Name) (value : 'Value) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<'Value>

            let value : IAdaptiveValue =
                if typeof<IAdaptiveValue>.IsAssignableFrom typeof<'Value> then
                    unbox value
                else
                    ~~value

            Sg.UniformApplicator(sym, value, sg) :> ISg


        let inline private textureAux< ^Conv, ^Name, 'Texture when 'Texture :> ITexture and (^Conv or ^Name) : (static member GetSymbol : ^Name -> Symbol)>
                                      (name : ^Name) =
            ((^Conv or ^Name) : (static member GetSymbol : ^Name -> Symbol) (name))

        /// Sets the given texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline texture (name : ^Name) (tex : aval< 'Texture>) (sg : ISg) =
            let sym = textureAux<Symbol.Converters.TypedConverter<ITexture>, ^Name, 'Texture> name
            let value = tex |> AdaptiveResource.cast<ITexture> // No caching required, since equality is preserved
            Sg.TextureApplicator(sym, value, sg) :> ISg

        /// Sets the given texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline texture' (name : ^Name) (tex : ITexture) (sg : ISg) =
            sg |> texture name ~~tex


        /// Sets the given diffuse texture.
        let diffuseTexture (tex : aval<#ITexture>) (sg : ISg) =
            texture DefaultSemantic.DiffuseColorTexture tex sg

        /// Sets the given diffuse texture.
        let diffuseTexture' (tex : ITexture) (sg : ISg) =
            sg |> diffuseTexture ~~tex


        /// Loads and sets the given texture file to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline fileTexture (name : ^Name) (path : string) (wantMipMaps : bool) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<ITexture>
            sg |> texture sym ~~(FileTexture(path, wantMipMaps) :> ITexture)

        /// Loads and sets the given diffuse texture file.
        let diffuseFileTexture (path : string) (wantMipMaps : bool) (sg : ISg) =
            sg |> fileTexture DefaultSemantic.DiffuseColorTexture path wantMipMaps


        /// Sets the given scope-dependent texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline scopeDependentTexture (name : ^Name) (tex : Scope -> aval<'Texture>) (sg : ISg) =
            let sym = textureAux<Symbol.Converters.TypedConverter<ITexture>, ^Name, 'Texture> name
            let value = tex >> AdaptiveResource.cast<ITexture> >> unbox<IAdaptiveValue>
            Sg.UniformApplicator(new Providers.ScopeDependentUniformHolder([sym, value]), sg) :> ISg

        /// Sets the given scope-dependent diffuse texture.
        let scopeDependentDiffuseTexture (tex : Scope -> aval<#ITexture>) (sg : ISg) =
            scopeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg


        /// Sets the given runtime-dependent texture to the slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline runtimeDependentTexture (name : ^Name) (tex : IRuntime -> aval<'Texture>) (sg : ISg) =
            let sym = textureAux<Symbol.Converters.TypedConverter<ITexture>, ^Name, 'Texture> name
            let value = tex >> AdaptiveResource.cast<ITexture> >> unbox<IAdaptiveValue>
            Sg.UniformApplicator(new Providers.RuntimeDependentUniformHolder([sym, value]), sg) :> ISg

        /// Sets the given runtime-dependent diffuse texture.
        let runtimeDependentDiffuseTexture(tex : IRuntime -> aval<#ITexture>) (sg : ISg) =
            runtimeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg


        /// Sets the given array of textures to the slots with the given name, which can be accessed by a sampler array.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline textureArray (name : ^Name) (textures : aval<'Texture[]>) (sg : ISg) =
            let sym = textureAux<Symbol.Converters.TypedConverter<ITexture>, ^Name, 'Texture> name
            let value : aval<ITexture[]> =
                if typeof<'Texture> = typeof<ITexture> then
                    textures |> unbox
                else
                    textures |> AdaptiveResource.mapNonAdaptive (Array.map unbox)

            Sg.UniformApplicator(sym, value :> IAdaptiveValue, sg) :> ISg

        /// Sets the given array of textures to the slots with the given name, which can be accessed by a sampler array.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline textureArray' (name : ^Name) (textures : 'Texture[]) (sg : ISg) =
            sg |> textureArray name ~~textures


        /// Modifies the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline modifySamplerState (name : ^Name) (modifier : aval<SamplerState -> SamplerState>) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed<ITexture>
            sg |> uniform $"{DefaultSemantic.SamplerStateModifier}_{sym}" modifier

        /// Modifies the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline modifySamplerState' (name : ^Name) (modifier : SamplerState -> SamplerState) (sg : ISg) =
            sg |> modifySamplerState name ~~modifier


        /// Sets the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline samplerState (name : ^Name) (state : aval<SamplerState>) (sg : ISg) =
            let modifier = state |> AVal.map (fun s -> (fun _ -> s))
            sg |> modifySamplerState name modifier

        /// Sets the sampler state for the texture slot with the given name.
        /// The name can be a string, Symbol, or TypedSymbol<ITexture>.
        let inline samplerState' (name : ^Name) (state : SamplerState) (sg : ISg) =
            sg |> samplerState name ~~state

        // ================================================================================================================
        // Trafos
        // ================================================================================================================

        /// Sets the model transformation.
        let trafo (m : aval<Trafo3d>) (sg : ISg) =
            Sg.TrafoApplicator(m, sg) :> ISg

        /// Sets the model transformation.
        let trafo' (m : Trafo3d) (sg : ISg) =
            sg |> trafo ~~m

        /// Sets the model transformation.
        let transform (m : Trafo3d) (sg : ISg) =
            sg |> trafo' m


        /// Sets the view transformation.
        let viewTrafo (m : aval<Trafo3d>) (sg : ISg) =
            Sg.ViewTrafoApplicator(m, sg) :> ISg

        /// Sets the view transformation.
        let viewTrafo' (m : Trafo3d) (sg : ISg) =
            sg |> viewTrafo ~~m


        /// Sets the projection transformation.
        let projTrafo (m : aval<Trafo3d>) (sg : ISg) =
            Sg.ProjectionTrafoApplicator(m, sg) :> ISg

        /// Sets the projection transformation.
        let projTrafo' (m : Trafo3d) (sg : ISg) =
            sg |> projTrafo ~~m


        /// Sets the view and projection transformations according to the given camera.
        let camera (cam : aval<Camera>) (sg : ISg) =
            sg |> viewTrafo (cam |> AVal.map Camera.viewTrafo) |> projTrafo (cam |> AVal.map Camera.projTrafo)

        /// Sets the view and projection transformations according to the given camera.
        let camera' (cam : Camera) (sg : ISg) =
            sg |> viewTrafo' (cam |> Camera.viewTrafo) |> projTrafo' (cam |> Camera.projTrafo)


        /// Scales the scene by the given scaling factors.
        let scaling (s : aval<V3d>) (sg : ISg) =
            sg |> trafo (s |> AVal.map Trafo3d.Scale)

        /// Scales the scene by the given scaling factors.
        let scaling' (s : V3d) (sg : ISg) =
            sg |> scaling ~~s

        /// Scales the scene by a uniform factor.
        let scale (s : float) (sg : ISg) =
            sg |> transform (Trafo3d.Scale s)


        /// Translates the scene by the given vector.
        let translation (v : aval<V3d>) (sg : ISg) =
            sg |> trafo (v |> AVal.map Trafo3d.Translation)

        /// Translates the scene by the given vector.
        let translation' (v : V3d) (sg : ISg) =
            sg |> translation ~~v

        /// Translates the scene by the given vector.
        let translate (x : float) (y : float) (z : float) (sg : ISg) =
            sg |> transform (Trafo3d.Translation(x, y, z))


        /// Rotates the scene by the given Euler angles.
        let rotate (rollInRadians : float) (pitchInRadians : float) (yawInRadians : float) (sg : ISg) =
            sg |> transform (Trafo3d.RotationEuler(rollInRadians, pitchInRadians, yawInRadians))

        // ================================================================================================================
        // Blending
        // ================================================================================================================

        /// Sets the global blend mode for all color attachments.
        let blendMode (mode : aval<BlendMode>) (sg : ISg) =
            Sg.BlendModeApplicator(mode, sg) :> ISg

        /// Sets the global blend mode for all color attachments.
        let blendMode' mode = blendMode (AVal.constant mode)


        /// Sets the blend modes for the given color attachments (overriding the global blend mode).
        let blendModes (modes : aval<Map<Symbol, BlendMode>>) (sg : ISg) =
            Sg.AttachmentBlendModeApplicator(modes, sg) :> ISg

        /// Sets the blend modes for the given color attachments (overriding the global blend mode).
        let blendModes' modes = blendModes (AVal.constant modes)


        /// Sets the blend constant color.
        /// The color must be compatible with C4f.
        let inline blendConstant (color : aval< ^Value>) (sg : ISg) =
            if typeof< ^Value> = typeof<C4f> then
                Sg.BlendConstantApplicator(color :?> aval<C4f>, sg) :> ISg
            else
                Sg.BlendConstantApplicator(color |> AVal.map c4f, sg) :> ISg

        /// Sets the blend constant color.
        /// The color must be compatible with C4f.
        let inline blendConstant' (color : ^Value) =
            blendConstant (AVal.constant color)


        /// Sets the global color write mask for all color attachments.
        let colorMask (mask : aval<ColorMask>) (sg : ISg) =
            Sg.ColorWriteMaskApplicator(mask, sg) :> ISg

        /// Sets the global color write mask for all color attachments.
        let colorMask' mask = colorMask (AVal.constant mask)


        /// Sets the color write masks for the given color attachments (overriding the global mask).
        let colorMasks (masks : aval<Map<Symbol, ColorMask>>) (sg : ISg) =
            Sg.AttachmentColorWriteMaskApplicator(masks, sg) :> ISg

        /// Sets the color write masks for the given color attachments (overriding the global mask).
        let colorMasks' masks = colorMasks (AVal.constant masks)


        /// Sets the color write mask for all color attachments to either ColorMask.None or ColorMask.All.
        let colorWrite (enabled : aval<bool>) (sg : ISg) =
            Sg.ColorWriteMaskApplicator(enabled, sg) :> ISg

        /// Sets the color write mask for all color attachments to either ColorMask.None or ColorMask.All.
        let colorWrite' enabled = colorWrite (AVal.constant enabled)


        /// Sets the color write masks for the given color attachments to either
        /// ColorMask.None or ColorMask.All (overriding the global mask).
        let colorWrites (enabled : aval<Map<Symbol, bool>>) (sg : ISg) =
            Sg.AttachmentColorWriteMaskApplicator(enabled, sg) :> ISg

        /// Sets the color write masks for the given color attachments to either
        /// ColorMask.None or ColorMask.All (overriding the global mask).
        let colorWrites' enabled = colorWrites (AVal.constant enabled)


        /// Restricts color output to the given attachments.
        let colorOutput (enabled : aval<Set<Symbol>>) =
            colorWrite' false
            >> colorMasks (enabled |> AVal.map ColorMask.ofWriteSet)

        /// Restricts color output to the given attachments.
        let colorOutput' enabled = colorOutput (AVal.constant enabled)

        // ================================================================================================================
        // Depth
        // ================================================================================================================

        /// Sets the depth test.
        let depthTest (test : aval<DepthTest>) (sg : ISg) =
            Sg.DepthTestApplicator(test, sg) :> ISg

        /// Sets the depth test.
        let depthTest' test = depthTest (AVal.constant test)


        /// Enables or disables depth writing.
        let depthWrite (depthWriteEnabled : aval<bool>) (sg : ISg) =
            Sg.DepthWriteMaskApplicator(depthWriteEnabled, sg) :> ISg

        /// Enables or disables depth writing.
        let depthWrite' depthWriteEnabled = depthWrite (AVal.constant depthWriteEnabled)


        /// Sets the depth bias.
        let depthBias (bias : aval<DepthBias>) (sg: ISg) =
            Sg.DepthBiasApplicator(bias, sg) :> ISg

        /// Sets the depth bias.
        let depthBias' bias = depthBias (AVal.constant bias)


        /// Enables or disables depth clamping.
        let depthClamp (clamp : aval<bool>) (sg: ISg) =
            Sg.DepthClampApplicator(clamp, sg) :> ISg

        /// Enables or disables depth clamping.
        let depthClamp' clamp = depthClamp (AVal.constant clamp)

        // ================================================================================================================
        // Stencil
        // ================================================================================================================

        /// Sets the stencil mode for front-facing polygons.
        let stencilModeFront (mode : aval<StencilMode>) (sg : ISg) =
            Sg.StencilModeFrontApplicator(mode, sg) :> ISg

        /// Sets the stencil mode for front-facing polygons.
        let stencilModeFront' mode = stencilModeFront (AVal.constant mode)


        /// Sets the stencil write mask for front-facing polygons.
        let stencilWriteMaskFront (mask : aval<StencilMask>) (sg : ISg) =
            Sg.StencilWriteMaskFrontApplicator(mask, sg) :> ISg

        /// Sets the stencil write mask for front-facing polygons.
        let stencilWriteMaskFront' mask = stencilWriteMaskFront (AVal.constant mask)


        /// Enables or disables stencil write for front-facing polygons.
        let stencilWriteFront (enabled : aval<bool>) (sg : ISg) =
            Sg.StencilWriteMaskFrontApplicator(enabled, sg) :> ISg

        /// Enables or disables stencil write for front-facing polygons.
        let stencilWriteFront' enabled = stencilWriteFront (AVal.constant enabled)


        /// Sets the stencil mode for back-facing polygons.
        let stencilModeBack (mode : aval<StencilMode>) (sg : ISg) =
            Sg.StencilModeBackApplicator(mode, sg) :> ISg

        /// Sets the stencil mode for back-facing polygons.
        let stencilModeBack' mode = stencilModeBack (AVal.constant mode)


        /// Sets the stencil write mask for back-facing polygons.
        let stencilWriteMaskBack (mask : aval<StencilMask>) (sg : ISg) =
            Sg.StencilWriteMaskBackApplicator(mask, sg) :> ISg

        /// Sets the stencil write mask for back-facing polygons.
        let stencilWriteMaskBack' mask = stencilWriteMaskBack (AVal.constant mask)


        /// Enables or disables stencil write for back-facing polygons.
        let stencilWriteBack (enabled : aval<bool>) (sg : ISg) =
            Sg.StencilWriteMaskBackApplicator(enabled, sg) :> ISg

        /// Enables or disables stencil write for back-facing polygons.
        let stencilWriteBack' enabled = stencilWriteBack (AVal.constant enabled)


        /// Sets separate stencil modes for front- and back-facing polygons.
        let stencilModes (front : aval<StencilMode>) (back : aval<StencilMode>) =
            stencilModeFront front >> stencilModeBack back

        /// Sets separate stencil modes for front- and back-facing polygons.
        let stencilModes' front back = stencilModes (AVal.constant front) (AVal.constant back)


        /// Sets separate stencil write masks for front- and back-facing polygons.
        let stencilWriteMasks (front : aval<StencilMask>) (back : aval<StencilMask>) =
            stencilWriteMaskFront front >> stencilWriteMaskBack back

        /// Sets separate stencil write masks for front- and back-facing polygons.
        let stencilWriteMasks' front back = stencilWriteMasks (AVal.constant front) (AVal.constant back)


        /// Enables or disables stencil write for front- and back-facing polygons.
        let stencilWrites (front : aval<bool>) (back : aval<bool>) =
            stencilWriteFront front >> stencilWriteBack back

        /// Enables or disables stencil write for front- and back-facing polygons.
        let stencilWrites' front back = stencilWrites (AVal.constant front) (AVal.constant back)


        /// Sets the stencil mode.
        let stencilMode (mode : aval<StencilMode>) =
            stencilModes mode mode

        /// Sets the stencil mode.
        let stencilMode' mode = stencilMode (AVal.constant mode)


        /// Sets the stencil write mask.
        let stencilWriteMask (mask : aval<StencilMask>) =
            stencilWriteMasks mask mask

        /// Sets the stencil write mask.
        let stencilWriteMask' mask = stencilWriteMask (AVal.constant mask)


        /// Enables or disables stencil write.
        let stencilWrite (enabled : aval<bool>) =
            stencilWrites enabled enabled

        /// Enables or disables stencil write.
        let stencilWrite' enabled = stencilWrite (AVal.constant enabled)

        // ================================================================================================================
        // Write buffers
        // ================================================================================================================

        /// Toggles color, depth and stencil writes according to the given set of symbols.
        let writeBuffers (buffers : aval<Set<WriteBuffer>>) =

            let depthEnable =
                buffers |> AVal.map (Set.contains WriteBuffer.Depth)

            let stencilEnable =
                buffers |> AVal.map (Set.contains WriteBuffer.Stencil)

            let colors =
                let get = function WriteBuffer.Color sem -> Some sem | _ -> None
                buffers |> AVal.map (Set.toList >> List.choose get >> Set.ofList)

            depthWrite depthEnable
            >> stencilWrite stencilEnable
            >> colorOutput colors

        /// Toggles color, depth and stencil writes according to the given set of symbols.
        let writeBuffers' (buffers : Set<WriteBuffer>) =
            writeBuffers (~~buffers)

        // ================================================================================================================
        // Rasterizer
        // ================================================================================================================

        /// Sets the cull mode.
        let cullMode (mode : aval<CullMode>) (sg : ISg) =
            Sg.CullModeApplicator(mode, sg) :> ISg

        /// Sets the cull mode.
        let cullMode' mode = cullMode (AVal.constant mode)


        /// Sets the winding order of front faces.
        let frontFacing (order : aval<WindingOrder>) (sg : ISg) =
            Sg.FrontFacingApplicator(order, sg) :> ISg

        /// Sets the winding order of front faces.
        let frontFacing' order = frontFacing (AVal.constant order)


        /// Sets the fill mode.
        let fillMode (mode : aval<FillMode>) (sg : ISg) =
            Sg.FillModeApplicator(mode, sg) :> ISg

        /// Sets the fill mode.
        let fillMode' mode = fillMode (AVal.constant mode)


        /// Toggles multisampling for the scene.
        let multisample (mode : aval<bool>) (sg : ISg) =
            Sg.MultisampleApplicator(mode, sg) :> ISg

        /// Toggles multisampling for the scene.
        let multisample' mode = multisample (AVal.constant mode)


        /// Toggles conservative rasterization for the scene.
        let conservativeRaster (mode : aval<bool>) (sg : ISg) =
            Sg.ConservativeRasterApplicator(mode, sg) :> ISg

        /// Toggles conservative rasterization for the scene.
        let conservativeRaster' mode = conservativeRaster (AVal.constant mode)

        // ================================================================================================================
        // Viewport
        // ================================================================================================================

        /// <summary>
        /// Sets a custom viewport for the scene.
        /// The viewport is the region of the framebuffer that will be rendered to.
        /// It determines the transformation from normalized device coordinates to framebuffer coordinates.
        /// </summary>
        /// <remarks>
        /// The default viewport is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The viewport to set. Min and Max are the framebuffer coordinates of the viewport's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the viewport to.</param>
        let viewport (region : aval<Box2i>) (sg : ISg) =
            Sg.ViewportApplicator(region, sg) :> ISg

        /// <summary>
        /// Sets a custom viewport for the scene.
        /// The viewport is the region of the framebuffer that will be rendered to.
        /// It determines the transformation from normalized device coordinates to framebuffer coordinates.
        /// </summary>
        /// <remarks>
        /// The default viewport is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The viewport to set. Min and Max are the framebuffer coordinates of the viewport's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the viewport to.</param>
        let viewport' (region : Box2i) (sg : ISg) =
            viewport (AVal.constant region) sg

        /// <summary>
        /// Sets a custom scissor for the scene.
        /// The scissor is the region of the framebuffer that can be modified by the render task.
        /// Fragments with coordinates outside of the scissor region will be discarded.
        /// </summary>
        /// <remarks>
        /// The default scissor is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The scissor to set. Min and Max are the framebuffer coordinates of the scissor's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the scissor to.</param>
        let scissor (region : aval<Box2i>) (sg : ISg) =
            Sg.ScissorApplicator(region, sg) :> ISg

        /// <summary>
        /// Sets a custom scissor for the scene.
        /// The scissor is the region of the framebuffer that can be modified by the render task.
        /// Fragments with coordinates outside of the scissor region will be discarded.
        /// </summary>
        /// <remarks>
        /// The default scissor is specified in <see cref="OutputDescription"/>.
        /// </remarks>
        /// <param name="region">The scissor to set. Min and Max are the framebuffer coordinates of the scissor's lower left and upper right corners (exclusive) respectively.</param>
        /// <param name="sg">The scene to apply the scissor to.</param>
        let scissor' (region : Box2i) (sg : ISg) =
            scissor (AVal.constant region) sg

        // ================================================================================================================
        // Attributes & Indices
        // ================================================================================================================

        let inline private attributeAux< ^Conv, ^Name, 'Value when 'Value : struct and (^Conv or ^Name) : (static member GetSymbol : ^Name -> Symbol)>
                                       (name : ^Name) (value : aval<'Value[]>) =
            let sym = ((^Conv or ^Name) : (static member GetSymbol : ^Name -> Symbol) (name))
            sym, Caching.bufferOfArray value

        let inline private attributeAux'< ^Conv, ^Name, 'Value when 'Value : struct and (^Conv or ^Name) : (static member GetSymbol : ^Name -> Symbol)>
                                        (name : ^Name) (value : 'Value[]) =
            let sym = ((^Conv or ^Name) : (static member GetSymbol : ^Name -> Symbol) (name))
            sym, BufferView.ofArray value

        /// Provides a vertex attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline vertexAttribute (name : ^Name) (value : aval<'Value[]>) (sg : ISg) =
            let name, view = attributeAux<Symbol.Converters.TypedConverter<'Value>, ^Name, 'Value> name value
            Sg.VertexAttributeApplicator(Map.ofList [name, view], ~~sg) :> ISg

        /// Provides a vertex attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline vertexAttribute' (name : ^Name) (value : 'Value[]) (sg : ISg) =
            let name, view = attributeAux'<Symbol.Converters.TypedConverter<'Value>, ^Name, 'Value> name value
            Sg.VertexAttributeApplicator(Map.ofList [name, view], ~~sg) :> ISg

        /// Provides a vertex attribute with the given name by supplying a BufferView.
        /// The name can be a string or Symbol.
        let inline vertexBuffer (name : ^Name) (view : BufferView) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            Sg.VertexAttributeApplicator(sym, view, sg) :> ISg

        /// Provides a vertex attribute with the given name by supplying an untyped array.
        /// The name can be a string or Symbol.
        let inline vertexArray (name : ^Name) (value : System.Array) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let view = BufferView(value)
            Sg.VertexAttributeApplicator(Map.ofList [sym, view], ~~sg) :> ISg

        /// Provides a vertex attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline vertexBufferValue (name : ^Name) (value : aval< ^Value>) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed< ^Value>
            let view = BufferView(SingleValueBuffer value, typeof< ^Value>)
            Sg.VertexAttributeApplicator(Map.ofList [sym, view], ~~sg) :> ISg

        /// Provides a vertex attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline vertexBufferValue' (name : ^Name) (value : ^Value) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed< ^Value>
            let view = BufferView(SingleValueBuffer ~~value, typeof< ^Value>)
            Sg.VertexAttributeApplicator(sym, view, sg) :> ISg


        /// Provides an instance attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline instanceAttribute (name : ^Name) (value : aval<'Value[]>) (sg : ISg) =
            let name, view = attributeAux<Symbol.Converters.TypedConverter<'Value>, ^Name, 'Value> name value
            Sg.InstanceAttributeApplicator(Map.ofList [name, view], ~~sg) :> ISg

        /// Provides an instance attribute with the given name by supplying an array of values.
        /// The name can be a string, Symbol, or TypedSymbol.
        let inline instanceAttribute' (name : ^Name) (value : 'Value[]) (sg : ISg) =
            let name, view = attributeAux'<Symbol.Converters.TypedConverter<'Value>, ^Name, 'Value> name value
            Sg.InstanceAttributeApplicator(Map.ofList [name, view], ~~sg) :> ISg

        /// Provides an index attribute with the given name by supplying a BufferView.
        /// The name can be a string or Symbol.
        let inline instanceBuffer (name : ^Name) (view : BufferView) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            Sg.InstanceAttributeApplicator(sym, view, sg) :> ISg

        /// Provides an index attribute with the given name by supplying an untyped array.
        /// The name can be a string or Symbol.
        let inline instanceArray (name : ^Name) (value : System.Array) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.untyped
            let view = BufferView(~~(ArrayBuffer value :> IBuffer), value.GetType().GetElementType())
            Sg.InstanceAttributeApplicator(sym, view, sg) :> ISg

        /// Provides an instance attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline instanceBufferValue (name : ^Name) (value : aval< ^Value>) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed< ^Value>
            let view = BufferView(SingleValueBuffer value, typeof< ^Value>)
            Sg.InstanceAttributeApplicator(Map.ofList [sym, view], ~~sg) :> ISg

        /// Provides an instance attribute with the given name by supplying a single value.
        /// The name can be a string, Symbol, or TypedSymbol.
        /// The value has to be compatible with V4f.
        let inline instanceBufferValue' (name : ^Name) (value : ^Value) (sg : ISg) =
            let sym = name |> Symbol.convert Symbol.Converters.typed< ^Value>
            let view = BufferView(SingleValueBuffer ~~value, typeof< ^Value>)
            Sg.InstanceAttributeApplicator(sym, view, sg) :> ISg


        /// Provides the given vertex indices.
        let index<'Value when 'Value : struct> (value : aval<'Value[]>) (sg : ISg) =
            Sg.VertexIndexApplicator(Caching.bufferOfArray value, sg) :> ISg

        /// Provides the given vertex indices.
        let index'<'Value when 'Value : struct> (value : 'Value[]) (sg : ISg) =
            Sg.VertexIndexApplicator(BufferView.ofArray value, sg) :> ISg

        /// Provides vertex indices by supplying a BufferView.
        let indexBuffer (view : BufferView) (sg : ISg) =
            Sg.VertexIndexApplicator(view, sg) :> ISg

         /// Provides vertex indices by supplying an untyped array.
        let indexArray (value : System.Array) (sg : ISg) =
            let view = BufferView(~~(ArrayBuffer value :> IBuffer), value.GetType().GetElementType())
            Sg.VertexIndexApplicator(view, sg) :> ISg

        // ================================================================================================================
        // Drawing
        // ================================================================================================================

        /// Applies the given effects to the scene.
        let shader = SgEffectBuilder()

        /// Applies the given effects to the scene.
        let effect (s : seq<FShadeEffect>) (sg : ISg) =
            let s = FShade.Effect.compose s |> Surface.Effect
            Sg.SurfaceApplicator(s, sg) :> ISg

        /// Applies the given surface to the scene.
        let inline surface (s : ^Surface) (sg : ISg) =
            Sg.SurfaceApplicator(Surface.create s, sg) :> ISg

        /// Applies the given pool of effects to the scene.
        /// The index active determines which effect is used at a time.
        let effectPool (effects : FShade.Effect[]) (active : aval<int>) (sg : ISg) =
            Sg.SurfaceApplicator(Surface.effectPool effects active, sg) :> ISg

        /// Applies the given render pass.
        let pass (pass : RenderPass) (sg : ISg) =
            Sg.PassApplicator(pass, sg) :> ISg

        /// Creates a single draw call for the given geometry mode.
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

        /// Supplies the given draw call with the given geometry mode.
        let render (mode : IndexedGeometryMode) (call : DrawCallInfo) =
            Sg.RenderNode(call, mode)

        /// Supplies the draw calls in the given indirect buffer with the given geometry mode.
        let indirectDraw (mode : IndexedGeometryMode) (buffer : aval<IndirectBuffer>) =
            Sg.IndirectRenderNode(buffer, mode) :> ISg

        /// Supplies the draw calls in the given indirect buffer with the given geometry mode.
        let inline indirectDraw' (mode : IndexedGeometryMode) (buffer : IndirectBuffer) =
            indirectDraw mode ~~buffer

        /// Creates a draw call from the given indexed geometry and an adpative instance count.
        let ofIndexedGeometryInstancedA (g : IndexedGeometry) (instanceCount : aval<int>) =
            let attributes =
                let indexed =
                    g.IndexedAttributes |> Seq.map (fun (KeyValue(sem, attr)) ->
                        sem, BufferView(attr)
                    ) |> Map.ofSeq

                let single =
                    if isNull g.SingleAttributes then Map.empty
                    else
                        g.SingleAttributes |> Seq.map (fun (KeyValue(sem, value)) ->
                            sem, BufferView(SingleValueBuffer.create value)
                        ) |> Map.ofSeq

                Map.union indexed single

            let index, faceVertexCount =
                if g.IsIndexed then
                    g.IndexArray, g.IndexArray.Length
                else
                    null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

            let call = instanceCount |> AVal.map (fun ic ->
                                                    DrawCallInfo(
                                                        FaceVertexCount = faceVertexCount,
                                                        FirstIndex = 0,
                                                        InstanceCount = ic,
                                                        FirstInstance = 0,
                                                        BaseVertex = 0
                                                    ))

            let sg = Sg.VertexAttributeApplicator(attributes, Sg.RenderNode(call, g.Mode)) :> ISg
            if not (isNull index) then
                Sg.VertexIndexApplicator(BufferView.ofArray index, sg) :> ISg
            else
                sg

        /// Creates a draw call from the given indexed geometry and instance count.
        let ofIndexedGeometryInstanced (g : IndexedGeometry) (instanceCount : int) =
            instanceCount |> AVal.constant |> ofIndexedGeometryInstancedA g

        /// Creates a draw call from the given indexed geometry.
        let ofIndexedGeometry (g : IndexedGeometry) =
            ofIndexedGeometryInstanced g 1

        /// Creates a draw call, supplying the given transformations as per-instance attributes
        /// DefaultSemantic.InstanceTrafo and DefaultSemantic.InstanceTrafoInv.
        let instancedGeometry (trafos : aval<Trafo3d[]>) (g : IndexedGeometry) =
            let sg =
                trafos
                |> AVal.mapNonAdaptive Array.length
                |> ofIndexedGeometryInstancedA g

            let forward, backward = Caching.buffersOfTrafos trafos

            Sg.InstanceAttributeApplicator([
                DefaultSemantic.InstanceTrafo, forward
                DefaultSemantic.InstanceTrafoInv, backward
            ] |> Map.ofList, sg) :> ISg

        // ================================================================================================================
        // Bounding boxes
        // ================================================================================================================

        let private transformBox (dst : Box3d) (src : Box3d) =

            let scale =
                let fromSize = src.Size
                let toSize = dst.Size
                let factor = toSize / fromSize

                factor.X |> min factor.Y |> min factor.Z

            Trafo3d.Translation(-src.Center) * Trafo3d.Scale(scale) * Trafo3d.Translation(dst.Center)

        /// Adaptively transforms the scene so its bounding box aligns with the given box.
        let normalizeToAdaptive (box : Box3d) (sg : ISg) =
            let bb = sg?GlobalBoundingBox(Ag.Scope.Root) : aval<Box3d>
            Sg.TrafoApplicator(bb |> AVal.map (transformBox box), sg) :> ISg

        /// Transforms the scene so its bounding box aligns with the given box.
        let normalizeTo (box : Box3d) (sg : ISg) =
            let bb = sg?GlobalBoundingBox(Ag.Scope.Root) : aval<Box3d>
            Sg.TrafoApplicator(bb.GetValue() |> transformBox box |> AVal.constant, sg) :> ISg

        /// Adaptively transforms the scene so its bounding box spans from -1 to 1 in all dimensions.
        let normalizeAdaptive sg =
            sg |> normalizeToAdaptive ( Box3d( V3d(-1,-1,-1), V3d(1,1,1) ) )

        /// Transforms the scene so its bounding box spans from -1 to 1 in all dimensions.
        let normalize sg =
            sg |> normalizeTo ( Box3d( V3d(-1,-1,-1), V3d(1,1,1) ) )


    type IndexedGeometry with
        member x.Sg =
            Sg.ofIndexedGeometry x


    module private ``F# Sg Generic Identifiers Tests`` =
        let working() =
            let texture = unbox<NullTexture> nullTexture
            let backendTex : IBackendTexture = failwith ""
            let someFloat = 1.0
            let MyTexture = Sym.ofString "MyTexture"
            let MyTextureT = TypedSymbol<ITexture>("MyTexture")

            let MyNormals = TypedSymbol<V3f>("MyNormals")
            let someNormals = ~~[| V3f.Zero |]
            let someNormals' = ~~[| C3f.Zero |]

            let backendTexArray = [| backendTex |]

            let myCoolFunc tex sg =
                sg |> Sg.texture "" tex

            let myCoolArrayFunc tex sg =
                sg |> Sg.textureArray "" tex

            let myCoolAttribFunc data sg =
                sg |> Sg.vertexAttribute "" data

            let myCoolInstAttribFunc data sg =
                sg |> Sg.instanceAttribute' "" data

            let sg : ISg =
                Sg.empty
                |> Sg.uniform "MyTexture" ~~texture
                |> Sg.uniform MyTexture ~~texture
                |> Sg.uniform' "SomeConstantTrafo" M44f.Zero
                |> Sg.uniform' "SomeConstantVec" V3f.One
                |> Sg.texture MyTextureT ~~texture
                |> Sg.texture' MyTextureT texture
                |> Sg.vertexAttribute MyNormals someNormals
                |> Sg.vertexBufferValue' MyNormals V3f.Zero
                |> myCoolFunc ~~texture
                |> myCoolFunc ~~backendTex
                |> myCoolArrayFunc ~~backendTexArray
                |> myCoolAttribFunc someNormals
                |> myCoolAttribFunc someNormals'
                |> myCoolInstAttribFunc [| V3d.Zero |]

            ()
