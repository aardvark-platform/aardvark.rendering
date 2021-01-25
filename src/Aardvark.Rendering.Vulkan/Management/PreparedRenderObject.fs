namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open FSharp.Data.Adaptive

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
        for r in resources do r.Update(caller) |> ignore

    interface IPreparedRenderObject with
        member x.Id = original.Id
        member x.RenderPass = original.RenderPass
        member x.AttributeScope = original.AttributeScope
        member x.Update(caller, token) = x.Update(caller, token) |> ignore
        member x.Original = Some original


type PreparedMultiRenderObject(children : list<PreparedRenderObject>) =
    let id = newId()

    let first =
        match children with
            | [] -> failwith "PreparedMultiRenderObject cannot be empty"
            | h::_ -> h

    let last = children |> List.last

    member x.Id = id

    member x.Children = children

    member x.Dispose() =
        lock AbstractRenderTask.ResourcesInUse (fun _ -> 
            children |> List.iter (fun c -> c.Dispose())
        )

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

    static let prepareObject (this : ResourceManager) (renderPass : RenderPass) (ro : RenderObject) =

        let resources = System.Collections.Generic.List<IResourceLocation>()

        let programLayout, program = this.CreateShaderProgram(renderPass, ro.Surface, ro.Mode)

        let descriptorSets =
            programLayout.DescriptorSetLayouts |> Array.map (fun ds ->
                let descriptors =
                    ds.Bindings |> Array.choose (fun b ->
                        match b.Parameter with
                        | UniformBlockParameter block ->
                            let buffer = this.CreateUniformBuffer(Ag.Scope.Root, block, ro.Uniforms, SymDict.empty)
                            resources.Add buffer
                            AdaptiveDescriptor.AdaptiveUniformBuffer (b.Binding, buffer) |> Some

                        | StorageBufferParameter block ->
                            let buffer = this.CreateStorageBuffer(Ag.Scope.Root, block, ro.Uniforms, SymDict.empty)
                            AdaptiveDescriptor.AdaptiveStorageBuffer (b.Binding, buffer) |> Some

                        | SamplerParameter sam ->
                            match sam.samplerTextures with
                            | [] ->
                                Log.warn "could not get sampler information for: %A" sam
                                None

                            | descriptions ->
                                let list =
                                    descriptions
                                    |> List.choosei (fun i (textureName, samplerState) ->
                                        let textureName = Symbol.Create textureName
                                        let samplerState = samplerState.SamplerState

                                        match ro.Uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                                        | Some (:? aval<ITexture> as tex) ->
                                            let vs = this.CreateImageSampler(sam.samplerType, tex, AVal.constant samplerState)
                                            Some(i, vs)
                                        | _ ->
                                            Log.warn "[Vulkan] could not find texture: %A" textureName
                                            None
                                    )

                                let viewSam = this.CreateImageSamplerArray(list)
                                AdaptiveDescriptor.AdaptiveCombinedImageSampler(b.Binding, viewSam) |> Some

                        | ImageParameter img ->
                            let viewSam =
                                let textureName = Symbol.Create img.imageName
                                match ro.Uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                                | Some (:? aval<ITexture> as tex) ->

                                    let tex = this.CreateImage(tex)
                                    let view = this.CreateImageView(img.imageType, tex)

                                    view

                                | _ ->
                                    failf "could not find texture: %A" textureName

                            AdaptiveDescriptor.AdaptiveStorageImage(b.Binding, viewSam) |> Some
                    )

                let res = this.CreateDescriptorSet(ds, descriptors)

                res
            )

        let isCompatible (shaderType : FShade.GLSL.GLSLType) (dataType : Type) =
            // TODO: verify type compatibility
            true

        let bufferViews =
            programLayout.PipelineInfo.pInputs
                |> List.sortBy (fun p -> p.paramLocation)
                |> List.map (fun p ->
                    let sem = Symbol.Create p.paramSemantic
                    let perInstance, view =
                        match ro.VertexAttributes.TryGetAttribute sem with
                            | Some att -> false, att
                            | None ->
                                match ro.InstanceAttributes.TryGetAttribute sem with
                                    | Some att -> true, att
                                    | None -> failf "could not get vertex data for shader input: %A" sem

                    (sem, p.paramLocation, perInstance, view)
                )

        let buffers =
            bufferViews 
                |> List.map (fun (name,loc, _, view) ->
                    let buffer = this.CreateBuffer(view.Buffer)
                    buffer, int64 view.Offset
                )


        let bufferFormats = 
            bufferViews |> List.map (fun (name,location, perInstance, view) -> name, (perInstance, view)) |> Map.ofSeq

        let inputAssembly = this.CreateInputAssemblyState(ro.Mode, program)
        let inputState = this.CreateVertexInputState(programLayout.PipelineInfo, AVal.constant (VertexInputState.create bufferFormats))

        let rasterizerState =
            this.CreateRasterizerState(
                ro.DepthState.Clamp, ro.DepthState.Bias,
                ro.RasterizerState.CullMode, ro.RasterizerState.FrontFace, ro.RasterizerState.FillMode,
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
            this.CreateVertexBufferBinding(buffers)
            
        resources.Add bindings

        let descriptorBindings =
            this.CreateDescriptorSetBinding(programLayout, descriptorSets)
            
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
        let resources = System.Collections.Generic.List<IResourceLocation>()
        let sets = 
            layout.DescriptorSetLayouts |> Array.map (fun ds ->
                let descriptors = 
                    ds.Bindings |> Array.choose (fun b ->
                        match b.Parameter with
                        | UniformBlockParameter block ->
                            let buffer = this.CreateUniformBuffer(Ag.Scope.Root, block, uniforms, SymDict.empty)
                            resources.Add buffer
                            AdaptiveDescriptor.AdaptiveUniformBuffer (b.Binding, buffer) |> Some

                        | StorageBufferParameter block ->
                            let buffer = this.CreateStorageBuffer(Ag.Scope.Root, block, uniforms, SymDict.empty)
                            AdaptiveDescriptor.AdaptiveStorageBuffer (b.Binding, buffer) |> Some

                        | SamplerParameter sam ->
                            match sam.samplerTextures with
                            | [] ->
                                Log.warn "could not get sampler information for: %A" sam
                                None

                            | descriptions ->
                                let list =
                                    descriptions
                                    |> List.choosei (fun i (textureName, samplerState) ->
                                        let textureName = Symbol.Create textureName
                                        let samplerState = samplerState.SamplerState

                                        match uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                                        | Some (:? aval<ITexture> as tex) ->
                                            let vs = this.CreateImageSampler(sam.samplerType, tex, AVal.constant samplerState)
                                            Some(i, vs)
                                        | _ ->
                                            Log.warn "[Vulkan] could not find texture: %A" textureName
                                            None
                                    )

                                let viewSam = this.CreateImageSamplerArray(list)
                                AdaptiveDescriptor.AdaptiveCombinedImageSampler(b.Binding, viewSam) |> Some

                        | ImageParameter img ->
                            let viewSam = 
                                let textureName = Symbol.Create img.imageName
                                match uniforms.TryGetUniform(Ag.Scope.Root, textureName) with
                                | Some (:? aval<ITexture> as tex) ->

                                    let tex = this.CreateImage(tex)
                                    let view = this.CreateImageView(img.imageType, tex)

                                    view

                                | _ ->
                                    failf "could not find texture: %A" textureName

                            AdaptiveDescriptor.AdaptiveStorageImage(b.Binding, viewSam) |> Some
                    )

                let res = this.CreateDescriptorSet(ds, descriptors)

                res
            )

        sets, CSharpList.toList resources


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
            for r in newResources do r.Update(AdaptiveToken.Top) |> ignore

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