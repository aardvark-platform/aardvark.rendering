namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open FSharp.Data.Adaptive

#nowarn "9"
// #nowarn "51"


type PreparedRenderObject =
    {
        device                  : Device
        original                : RenderObject
        
        resources               : list<IResourceLocation>

        pipelineLayout          : PipelineLayout
        pipeline                : INativeResourceLocation<VkPipeline>
        indexBuffer             : Option<INativeResourceLocation<IndexBufferBinding>>
        descriptorSets          : INativeResourceLocation<DescriptorSetBinding>
        vertexBuffers           : INativeResourceLocation<VertexBufferBinding>
        drawCalls               : INativeResourceLocation<DrawCall>
        isActive                : INativeResourceLocation<int>
        activation              : IDisposable
    }
    member x.DrawCallInfos = x.original.DrawCallInfos
    member x.RenderPass = x.original.RenderPass
    member x.AttributeScope = x.original.AttributeScope

    member x.Dispose() =
        for r in x.resources do r.Release()

    member x.Update(caller : AdaptiveToken, token : RenderToken) =
        for r in x.resources do r.Update(caller) |> ignore

    member x.IncrementReferenceCount() =
        for r in x.resources do r.Acquire()


    interface IPreparedRenderObject with
        member x.Id = x.original.Id
        member x.RenderPass = x.original.RenderPass
        member x.AttributeScope = x.original.AttributeScope
        member x.Update(caller, token) = x.Update(caller, token) |> ignore
        member x.Original = Some x.original
        member x.Dispose() = x.Dispose()

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
        children |> List.iter (fun c -> c.Dispose())

    member x.Update(caller : AdaptiveToken, token : RenderToken) =
        children |> List.iter (fun c -> c.Update(caller, token))
        

    member x.RenderPass = first.RenderPass
    member x.Original = first.original

    member x.First = first
    member x.Last = last

    interface IRenderObject with
        member x.Id = first.original.Id
        member x.AttributeScope = first.AttributeScope
        member x.RenderPass = first.RenderPass

    interface IPreparedRenderObject with
        member x.Original = Some first.original
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
                    ds.Bindings |> Array.choosei (fun i b ->
                        match b.Parameter with
                            | UniformBlockParameter block ->
                                let buffer = this.CreateUniformBuffer(Ag.emptyScope, block, ro.Uniforms, SymDict.empty)
                                resources.Add buffer
                                AdaptiveDescriptor.AdaptiveUniformBuffer (i, buffer) |> Some

                            | StorageBufferParameter block ->
                                let buffer = this.CreateStorageBuffer(Ag.emptyScope, block, ro.Uniforms, SymDict.empty)
                                AdaptiveDescriptor.AdaptiveStorageBuffer (i, buffer) |> Some

                            | SamplerParameter sam ->
                                match sam.samplerTextures with
                                    | [] ->
                                        Log.warn "could not get sampler information for: %A" sam
                                        None

                                    | descriptions ->
                                        let viewSam = 
                                            descriptions |> List.map (fun (textureName, samplerState) -> 
                                                let textureName = Symbol.Create textureName
                                                let samplerState = samplerState.SamplerStateDescription

                                                match ro.Uniforms.TryGetUniform(Ag.emptyScope, textureName) with
                                                | Some (:? aval<ITexture> as tex) ->

                                                    let tex = this.CreateImage(tex)
                                                    let view = this.CreateImageView(sam.samplerType, tex)
                                                    let sam = this.CreateSampler(AVal.constant samplerState)

                                                    Some(view, sam)

                                                | _ ->
                                                    Log.warn "[Vulkan] could not find texture: %A" textureName
                                                    None
                                            )

                                        AdaptiveDescriptor.AdaptiveCombinedImageSampler(i, List.toArray viewSam) |> Some
                                

                            | ImageParameter img ->
                                let viewSam = 
                                    let textureName = Symbol.Create img.imageName
                                    match ro.Uniforms.TryGetUniform(Ag.emptyScope, textureName) with
                                    | Some (:? aval<ITexture> as tex) ->

                                        let tex = this.CreateImage(tex)
                                        let view = this.CreateImageView(img.imageType, tex)

                                        view

                                    | _ ->
                                        failf "could not find texture: %A" textureName

                                AdaptiveDescriptor.AdaptiveStorageImage(i, viewSam) |> Some
                                
                                
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

        let writeDepth =
            match ro.WriteBuffers with
                | Some set -> Set.contains DefaultSemantic.Depth set
                | None -> true



        let inputAssembly = this.CreateInputAssemblyState(ro.Mode, program)
        let inputState = this.CreateVertexInputState(programLayout.PipelineInfo, AVal.constant (VertexInputState.create bufferFormats))
        let rasterizerState = this.CreateRasterizerState(ro.DepthTest, ro.DepthBias, ro.CullMode, ro.FrontFace, ro.FillMode)
        let colorBlendState = this.CreateColorBlendState(renderPass, ro.WriteBuffers, ro.BlendMode)
        let depthStencilState = this.CreateDepthStencilState(writeDepth, ro.DepthTest, ro.StencilMode)

        let pipeline =
            this.CreatePipeline(
                program,
                renderPass,
                inputState,
                inputAssembly,
                rasterizerState,
                colorBlendState,
                depthStencilState,
                ro.WriteBuffers
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
            match ro.IndirectBuffer :> obj with
                | null -> 
                    this.CreateDrawCall(indexed, ro.DrawCallInfos)
                | _ -> 
                    let indirect = this.CreateIndirectBuffer(indexed, ro.IndirectBuffer)
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

        let res = 
            {
                device                      = this.Device
                original                    = ro
                resources                   = CSharpList.toList resources
                descriptorSets              = descriptorBindings
                pipelineLayout              = programLayout
                pipeline                    = pipeline
                vertexBuffers               = bindings
                indexBuffer                 = indexBufferBinding
                drawCalls                   = calls
                isActive                    = isActive
                activation                  = ro.Activate()
            }

        res
        
    [<Extension>]
    static member CreateDescriptorSets (this : ResourceManager, layout : PipelineLayout, uniforms : IUniformProvider) =
        let resources = System.Collections.Generic.List<IResourceLocation>()
        let sets = 
            layout.DescriptorSetLayouts |> Array.map (fun ds ->
                let descriptors = 
                    ds.Bindings |> Array.choosei (fun i b ->
                        match b.Parameter with
                            | UniformBlockParameter block ->
                                let buffer = this.CreateUniformBuffer(Ag.emptyScope, block, uniforms, SymDict.empty)
                                resources.Add buffer
                                AdaptiveDescriptor.AdaptiveUniformBuffer (i, buffer) |> Some

                            | StorageBufferParameter block ->
                                let buffer = this.CreateStorageBuffer(Ag.emptyScope, block, uniforms, SymDict.empty)
                                AdaptiveDescriptor.AdaptiveStorageBuffer (i, buffer) |> Some

                            | SamplerParameter sam ->
                                match sam.samplerTextures with
                                    | [] ->
                                        Log.warn "could not get sampler information for: %A" sam
                                        None

                                    | descriptions ->
                                        let viewSam = 
                                            descriptions |> List.map (fun (textureName, samplerState) -> 
                                                let textureName = Symbol.Create textureName
                                                let samplerState = samplerState.SamplerStateDescription

                                                match uniforms.TryGetUniform(Ag.emptyScope, textureName) with
                                                | Some (:? aval<ITexture> as tex) ->

                                                    let tex = this.CreateImage(tex)
                                                    let view = this.CreateImageView(sam.samplerType, tex)
                                                    let sam = this.CreateSampler(AVal.constant samplerState)

                                                    Some(view, sam)

                                                | _ ->
                                                    Log.warn "[Vulkan] could not find texture: %A" textureName
                                                    None
                                            )

                                        AdaptiveDescriptor.AdaptiveCombinedImageSampler(i, List.toArray viewSam) |> Some
                                

                            | ImageParameter img ->
                                let viewSam = 
                                    let textureName = Symbol.Create img.imageName
                                    match uniforms.TryGetUniform(Ag.emptyScope, textureName) with
                                    | Some (:? aval<ITexture> as tex) ->

                                        let tex = this.CreateImage(tex)
                                        let view = this.CreateImageView(img.imageType, tex)

                                        view

                                    | _ ->
                                        failf "could not find texture: %A" textureName

                                AdaptiveDescriptor.AdaptiveStorageImage(i, viewSam) |> Some
                                
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
            let newResources = result.resources |> List.filter (fun r -> r.ReferenceCount = 0)

            // acquire all resources (possibly causing a ref-count greater than 1 when used multiple times)
            for r in result.resources do r.Acquire()

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
                new PreparedMultiRenderObject([o])

            | :? PreparedMultiRenderObject as mo ->
                mo

            | _ ->
                failf "unsupported RenderObject-type: %A" ro

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, renderPass : RenderPass, ro : IRenderObject) =
        DevicePreparedRenderObjectExtensions.PrepareRenderObject(this, renderPass, ro, id)