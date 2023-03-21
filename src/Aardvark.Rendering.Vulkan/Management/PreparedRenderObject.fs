namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive
open TypeInfo

#nowarn "9"
// #nowarn "51"

type PreparedRenderObject(device         : Device,
                          original       : RenderObject,
                          resources      : list<IResourceLocation>,
                          program        : IResourceLocation<ShaderProgram>,
                          pipelineLayout : PipelineLayout,
                          pipeline       : INativeResourceLocation<VkPipeline>,
                          indexBuffer    : Option<INativeResourceLocation<IndexBufferBinding>>,
                          descriptorSets : INativeResourceLocation<DescriptorSetBinding>,
                          vertexBuffers  : INativeResourceLocation<VertexBufferBinding>,
                          drawCalls      : INativeResourceLocation<DrawCall>,
                          isActive       : INativeResourceLocation<int>,
                          activation     : IDisposable) =

    inherit Resource(device)

    member x.Original = original
    member x.Resources = resources
    member x.PipelineLayout = pipelineLayout
    member x.Pipeline = pipeline
    member x.IndexBuffer = indexBuffer
    member x.DescriptorSets = descriptorSets
    member x.VertexBuffers = vertexBuffers
    member x.DrawCalls = drawCalls
    member x.IsActive = isActive
    member x.RenderPass = original.RenderPass
    member x.AttributeScope = original.AttributeScope

    override x.Destroy() =
        // TODO: Resources aren't actually acquired by preparing the render object (except the program) but only
        // when added to the compiler. Thus, we don't release them here. May wanna change that behavior.
        program.Release()
        activation.Dispose()

    member x.Update(caller : AdaptiveToken, token : RenderToken) =
        for r in resources do r.Update(ResourceUser.None, caller, token) |> ignore

    interface IPreparedRenderObject with
        member x.Id = original.Id
        member x.RenderPass = original.RenderPass
        member x.AttributeScope = original.AttributeScope
        member x.Update(caller, token) = x.Update(caller, token) |> ignore
        member x.Original = Some original


type PreparedMultiRenderObject(children : list<PreparedRenderObject>) =
    let id = RenderObjectId.New()

    let first =
        match children with
            | [] -> failwith "PreparedMultiRenderObject cannot be empty"
            | h::_ -> h

    let last = children |> List.last

    member x.Id = id

    member x.Children = children

    member x.Dispose() =
        children |> List.iter Disposable.dispose

    member x.Update(caller : AdaptiveToken, token : RenderToken) =
        children |> List.iter (fun c -> c.Update(caller, token))

    member x.RenderPass = first.RenderPass
    member x.Original = first.Original

    member x.First = first
    member x.Last = last

    interface IRenderObject with
        member x.Id = first.Original.Id
        member x.AttributeScope = first.AttributeScope
        member x.RenderPass = first.RenderPass

    interface IPreparedRenderObject with
        member x.Original = Some first.Original
        member x.Update(caller, token) = x.Update(caller, token) |> ignore

    interface IDisposable with
        member x.Dispose() = x.Dispose()

open Aardvark.Rendering.Vulkan.Resources
open System.Threading.Tasks

[<AbstractClass; Sealed; Extension>]
type DevicePreparedRenderObjectExtensions private() =

    static let (|NullUniform|_|) (value : IAdaptiveValue option) =
        match value with
        | Some value when Object.ReferenceEquals(value, null) ->
            Some ()
        | _ ->
            None

    static let (|CastUniformResource|_|) (value : IAdaptiveValue option) =
        match value with
        | Some value when typeof<'T>.IsAssignableFrom value.ContentType ->
            Some (AdaptiveResource.cast<'T> value)
        | _ ->
            None

    static let createSamplerState (this : ResourceManager) (name : Symbol) (uniforms : IUniformProvider) (state : FShade.SamplerState)  =
        match uniforms.TryGetUniform(Ag.Scope.Root, DefaultSemantic.SamplerStateModifier) with
        | Some (:? aval<Symbol -> SamplerState -> SamplerState> as modifier) ->
            this.CreateDynamicSamplerState(name, state.SamplerState, modifier) :> aval<_>
        | _ ->
            AVal.constant state.SamplerState

    static let createDescriptorSets (this : ResourceManager) (layout : PipelineLayout) (uniforms : IUniformProvider) =
        layout.DescriptorSetLayouts |> Array.map (fun ds ->
            let descriptors =
                ds.Bindings |> Array.map (fun b ->
                    match b.Parameter with
                    | UniformBlockParameter block ->
                        let buffer = this.CreateUniformBuffer(Ag.Scope.Root, block, uniforms)
                        AdaptiveDescriptor.UniformBuffer (b.Binding, buffer) :> IAdaptiveDescriptor

                    | StorageBufferParameter block ->
                        let bufferName = Symbol.Create block.ssbName

                        let buffer =
                            match uniforms.TryGetUniform(Ag.Scope.Root, bufferName) with
                            | NullUniform ->
                                failf "storage buffer '%A' is null" bufferName

                            | CastUniformResource (buffer : aval<IBuffer>) ->
                                this.CreateStorageBuffer(buffer)

                            | CastUniformResource (array : aval<Array>) ->
                                this.CreateStorageBuffer(array)

                            | Some value ->
                                failf "invalid type '%A' for storage buffer '%A' (expected a subtype of IBuffer or Array)" value.ContentType bufferName

                            | _ ->
                                failf "could not find storage buffer '%A'" bufferName

                        AdaptiveDescriptor.StorageBuffer (b.Binding, buffer) :> IAdaptiveDescriptor

                    | SamplerParameter sam ->
                        let arraySampler =
                            sam.Array |> Option.bind (fun (textureName, samplerState) ->
                                let textureName = Symbol.Create textureName

                                match uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                                | NullUniform ->
                                    failf "texture array '%A' is null" textureName

                                | Some (:? aval<(int * aval<ITexture>)[]> as tex) ->
                                    let s = createSamplerState this textureName uniforms samplerState
                                    let is = this.CreateImageSamplerArray(sam.samplerCount, sam.samplerType, tex, s)
                                    Some is

                                | Some (:? aval<ITexture[]> as tex) ->
                                    let s = createSamplerState this textureName uniforms samplerState
                                    let is = this.CreateImageSamplerArray(sam.samplerCount, sam.samplerType, tex, s)
                                    Some is

                                | Some t ->
                                    failf "invalid type '%A' for texture array '%A' (expected ITexture[] or (int * aval<ITexture>)[])" t.ContentType textureName

                                | _ ->
                                    None
                            )

                        let sampler =
                            arraySampler |> Option.defaultWith(fun _ ->
                                match sam.samplerTextures with
                                | [] ->
                                    failf "could not get sampler information for: %A" sam

                                | descriptions ->
                                    let list =
                                        descriptions
                                        |> List.mapi (fun i (textureName, samplerState) ->
                                            let textureName = Symbol.Create textureName

                                            match uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                                            | NullUniform ->
                                                failf "texture '%A' is null" textureName

                                            | CastUniformResource (texture : aval<ITexture>) ->
                                                let desc = createSamplerState this textureName uniforms samplerState
                                                let sampler = this.CreateImageSampler(sam.samplerType, texture, desc)
                                                i, sampler

                                            | CastUniformResource (level : aval<ITextureLevel>) ->
                                                let desc = createSamplerState this textureName uniforms samplerState
                                                let sampler = this.CreateImageSampler(sam.samplerType, level, desc)
                                                i, sampler

                                            | Some t ->
                                                failf "invalid type '%A' for texture '%A' (expected a subtype of ITexture or ITextureLevel)" t.ContentType textureName

                                            | _ ->
                                                failf "could not find texture '%A'" textureName
                                        )

                                    let empty = this.CreateImageSampler(sam.samplerType, nullTextureConst, AVal.constant SamplerState.Default)
                                    this.CreateImageSamplerArray(sam.samplerCount, empty, list)
                            )

                        AdaptiveDescriptor.CombinedImageSampler(b.Binding, b.DescriptorCount, sampler) :> IAdaptiveDescriptor

                    | ImageParameter image ->
                        let viewSam =
                            let imageName = Symbol.Create image.imageName

                            match uniforms.TryGetUniform(Ag.Scope.Root, imageName) with
                            | NullUniform ->
                                failf "storage image '%A' is null" imageName

                            | CastUniformResource (texture : aval<ITexture>) ->
                                let img = this.CreateImage(image.imageType.Properties, texture)
                                let view = this.CreateImageView(image.imageType, img)
                                view

                            | CastUniformResource (level : aval<ITextureLevel>) ->
                                let levels = level |> AVal.mapNonAdaptive (fun l -> l.Levels)
                                let slices = level |> AVal.mapNonAdaptive (fun l -> l.Slices)
                                let img = this.CreateImage(image.imageType.Properties, level)
                                let view = this.CreateImageView(image.imageType, img, levels, slices)
                                view

                            | Some i ->
                                failf "invalid type '%A' for storage image '%A' (expected a subtype of ITexture or ITextureLevel)" i.ContentType imageName

                            | None ->
                                failf "could not find storage image '%A'" imageName

                        AdaptiveDescriptor.StorageImage(b.Binding, viewSam) :> IAdaptiveDescriptor

                    | AccelerationStructureParameter a ->
                        let accel =
                            let name = Sym.ofString a.accelName

                            match uniforms.TryGetUniform(Ag.Scope.Root, name) with
                            | NullUniform ->
                                failf "acceleration structure '%A' is null" name

                            | Some (:? IResourceLocation<Raytracing.AccelerationStructure> as accel) ->
                                accel

                            | Some a ->
                                failf "invalid type '%A' for acceleration structure '%A'" a.ContentType name

                            | _ ->
                                failf "could not find acceleration structure '%A'" name

                        AdaptiveDescriptor.AccelerationStructure(a.accelBinding, accel) :> IAdaptiveDescriptor
                )

            let res = this.CreateDescriptorSet(ds, descriptors)
            res
        )

    static let getExpectedType (t : FShade.GLSL.GLSLType) =
        try
            GLSLType.toType t
        with _ ->
            failf "unexpected vertex type: %A" t

    static let prepareObject (this : ResourceManager) (renderPass : RenderPass) (ro : RenderObject) =

        let resources = System.Collections.Generic.List<IResourceLocation>()

        let programLayout, program = this.CreateShaderProgram(renderPass, ro.Surface, ro.Mode)

        let descriptorSets = createDescriptorSets this programLayout ro.Uniforms

        let attributeBindings =
            programLayout.PipelineInfo.pInputs
            |> List.sortBy (fun p -> p.paramLocation)
            |> List.map (fun p ->
                let semantic = Sym.ofString p.paramSemantic
                let expectedType = getExpectedType p.paramType

                let view, perInstance =
                    match ro.TryGetAttribute semantic with
                    | Some attr -> attr
                    | _ -> failf "could not get attribute '%A'" semantic

                let buffer = this.CreateVertexBuffer view.Buffer

                let stride =
                    if view.IsSingleValue then 0
                    else view.Stride

                let rows =
                    match view.ElementType with
                    | MatrixOf(s, _) -> s.Y
                    | _ -> 1

                let format =
                    VkFormat.tryGetAttributeFormat expectedType view.ElementType
                    |> Option.defaultWith (fun _ ->
                        failf "cannot use %A elements for attribute '%A' expecting %A elements" view.ElementType semantic expectedType
                    )

                let formatFeatures = this.Device.PhysicalDevice.GetBufferFormatFeatures format
                if not <| formatFeatures.HasFlag VkFormatFeatureFlags.VertexBufferBit then
                    failf "format %A for attribute '%A' is not supported" format semantic

                let inputDescription =
                    VertexInputDescription.create perInstance view.IsSingleValue view.Offset stride rows format

                {| Semantic    = semantic
                   Buffer      = (buffer, int64 view.Offset)
                   Description = inputDescription |}
            )

        let attributeDescriptions =
            attributeBindings
            |> List.map (fun b -> b.Semantic, b.Description)
            |> Map.ofSeq

        let inputAssembly = this.CreateInputAssemblyState(ro.Mode, program)
        let inputState = this.CreateVertexInputState(programLayout.PipelineInfo, attributeDescriptions)

        let rasterizerState =
            this.CreateRasterizerState(
                ro.DepthState.Clamp, ro.DepthState.Bias,
                ro.RasterizerState.CullMode, ro.RasterizerState.FrontFacing, ro.RasterizerState.FillMode,
                ro.RasterizerState.ConservativeRaster
            )

        let colorBlendState =
            this.CreateColorBlendState(
                renderPass, ro.BlendState.ColorWriteMask, ro.BlendState.AttachmentWriteMask,
                ro.BlendState.Mode, ro.BlendState.AttachmentMode, ro.BlendState.ConstantColor
            )

        let depthStencilState =
            this.CreateDepthStencilState(
                ro.DepthState.Test, ro.DepthState.WriteMask,
                ro.StencilState.ModeFront, ro.StencilState.WriteMaskFront,
                ro.StencilState.ModeBack, ro.StencilState.WriteMaskBack
            )

        let multisampleState =
            this.CreateMultisampleState(renderPass, ro.RasterizerState.Multisample)

        let pipeline =
            this.CreatePipeline(
                program,
                renderPass,
                inputState,
                inputAssembly,
                rasterizerState,
                colorBlendState,
                depthStencilState,
                multisampleState
            )

        resources.Add pipeline

        let indexed = Option.isSome ro.Indices
        let indexBufferBinding =
            match ro.Indices with
            | Some view ->
                let buffer = this.CreateIndexBuffer(view.Buffer)
                let res = this.CreateIndexBufferBinding(buffer, VkIndexType.ofType view.ElementType)
                resources.Add res
                Some res
            | None ->
                None

        let calls =
            match ro.DrawCalls with
            | Direct dir ->
                this.CreateDrawCall(indexed, dir)
            | Indirect indir ->
                let indirect = this.CreateIndirectBuffer(indexed, indir)
                this.CreateDrawCall(indexed, indirect)

        resources.Add calls

        let bindings =
            this.CreateVertexBufferBinding(attributeBindings |> List.map (fun b -> b.Buffer))

        resources.Add bindings

        let descriptorBindings =
            this.CreateDescriptorSetBinding(VkPipelineBindPoint.Graphics, programLayout, descriptorSets)

        resources.Add(descriptorBindings)

        let isActive = this.CreateIsActive ro.IsActive
        resources.Add isActive

        //for r in resources do r.Acquire()

        new PreparedRenderObject(
            this.Device, ro,
            CSharpList.toList resources,
            program,
            programLayout,
            pipeline,
            indexBufferBinding,
            descriptorBindings,
            bindings,
            calls,
            isActive,
            ro.Activate()
        )

    [<Extension>]
    static member CreateDescriptorSets (this : ResourceManager, layout : PipelineLayout, uniforms : IUniformProvider) =
        createDescriptorSets this layout uniforms

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, renderPass : RenderPass, ro : RenderObject) =
        prepareObject this renderPass ro

    [<Extension>]
    static member PrepareRenderObjectAsync(this : ResourceManager, renderPass : RenderPass, ro : RenderObject) =
        let device = this.Device
        let oldToken = device.UnsafeCurrentToken
        try
            let newToken = new DeviceToken(device.GraphicsFamily, ref None)
            device.UnsafeSetToken (Some newToken)
        
            let result = prepareObject this renderPass ro

            // get all "new" resources
            let newResources = result.Resources |> List.filter (fun r -> r.ReferenceCount = 0)

            // acquire all resources (possibly causing a ref-count greater than 1 when used multiple times)
            for r in result.Resources do r.Acquire()

            // update all "new" resources
            for r in newResources do r.Update(ResourceUser.None, AdaptiveToken.Top, RenderToken.Empty) |> ignore

            // enqueue a callback to the CopyEngine tiggering on completion of
            // all prior commands (possibly including more than the needed ones)
            let tcs = System.Threading.Tasks.TaskCompletionSource()
            device.CopyEngine.Enqueue [ CopyCommand.Callback (fun () -> tcs.SetResult ()) ]

            // when the copies are done continue syncing the token (using the graphics queue)
            tcs.Task |> Task.bind (fun () ->
                let task = newToken.SyncTask(4).AsTask

                // when everything is synced properly dispose the token and return the object
                task.ContinueWith(fun (t : System.Threading.Tasks.Task<_>) ->
                    newToken.Dispose()
                    result
                )
            )
        finally 
            device.UnsafeSetToken oldToken
        
    [<Extension>]
    static member PrepareRenderObjectAsync(this : ResourceManager, renderPass : RenderPass, ro : IRenderObject, hook : RenderObject -> RenderObject) : Task<PreparedMultiRenderObject> =
        match ro with
            | :? RenderObject as ro ->
                DevicePreparedRenderObjectExtensions.PrepareRenderObjectAsync(this, renderPass, ro)
                    |> Task.mapInline (fun v -> new PreparedMultiRenderObject([v]))

            | :? MultiRenderObject as mo ->
                match mo.Children with
                    | [] -> 
                        Task.FromResult(new PreparedMultiRenderObject([]))
                    | [o] ->
                        DevicePreparedRenderObjectExtensions.PrepareRenderObjectAsync(this, renderPass, o, hook)
                    | children ->
                        children 
                        |> List.collectT (fun (o : IRenderObject) ->
                            DevicePreparedRenderObjectExtensions.PrepareRenderObjectAsync(this, renderPass, o, hook) 
                                |> Task.mapInline (fun o -> o.Children)
                        )
                        |> Task.mapInline (fun l -> new PreparedMultiRenderObject(l))

            | :? PreparedRenderObject as o ->
                Task.FromResult (new PreparedMultiRenderObject([o]))

            | :? PreparedMultiRenderObject as mo ->
                Task.FromResult mo

            | _ ->
                failf "unsupported RenderObject-type: %A" ro



    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, renderPass : RenderPass, ro : IRenderObject, hook : RenderObject -> RenderObject) =
        match ro with
            | :? RenderObject as ro ->
                let res = prepareObject this renderPass (hook ro)
                new PreparedMultiRenderObject([res])

            | :? MultiRenderObject as mo ->
                let all = mo.Children |> List.collect (fun c -> DevicePreparedRenderObjectExtensions.PrepareRenderObject(this, renderPass, c, hook).Children)
                new PreparedMultiRenderObject(all)

            | :? PreparedRenderObject as o ->
                o.AddReference()
                new PreparedMultiRenderObject([o])

            | :? PreparedMultiRenderObject as mo ->
                for o in mo.Children do o.AddReference()
                mo

            | _ ->
                failf "unsupported RenderObject-type: %A" ro

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, renderPass : RenderPass, ro : IRenderObject) =
        DevicePreparedRenderObjectExtensions.PrepareRenderObject(this, renderPass, ro, id)