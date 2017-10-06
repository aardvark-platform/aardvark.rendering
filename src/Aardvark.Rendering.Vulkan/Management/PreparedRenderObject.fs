namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.Incremental

#nowarn "9"
#nowarn "51"


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
    let first =
        match children with
            | [] -> failwith "PreparedMultiRenderObject cannot be empty"
            | h::_ -> h

    let last = children |> List.last

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

[<AbstractClass; Sealed; Extension>]
type DevicePreparedRenderObjectExtensions private() =

    static let prepareObject (token : AdaptiveToken) (this : ResourceManager) (renderPass : RenderPass) (ro : RenderObject) =
        
        let resources = System.Collections.Generic.List<IResourceLocation>()

        let programLayout, program = this.CreateShaderProgram(renderPass, ro.Surface)

        let descriptorSets = 
            programLayout.DescriptorSetLayouts |> Array.map (fun ds ->
                let descriptors = 
                    ds.Bindings |> Array.choosei (fun i b ->
                        match b.Parameter with
                            | UniformBlockParameter block ->
                                let buffer = this.CreateUniformBuffer(ro.AttributeScope, block.layout, ro.Uniforms, SymDict.empty)
                                resources.Add buffer
                                AdaptiveDescriptor.AdaptiveUniformBuffer (i, buffer) |> Some

                            | ImageParameter img ->
                                match img.description with
                                    | [] ->
                                        Log.warn "could not get sampler information for: %A" img
                                        None

                                    | descriptions ->
                                        let viewSam = 
                                            descriptions |> List.map (fun desc -> 
                                                let textureName = desc.textureName
                                                let samplerState = desc.samplerState
                                                match ro.Uniforms.TryGetUniform(Ag.emptyScope, textureName) with
                                                | Some (:? IMod<ITexture> as tex) ->

                                                    let tex = this.CreateImage(tex)
                                                    let view = this.CreateImageView(img.samplerType, tex)
                                                    let sam = this.CreateSampler(Mod.constant samplerState)

                                                    Some(view, sam)

                                                | _ ->
                                                    Log.warn "[Vulkan] could not find texture: %A" textureName
                                                    None
                                            )

                                        AdaptiveDescriptor.AdaptiveCombinedImageSampler(i, List.toArray viewSam) |> Some
                                
                    )

                let res = this.CreateDescriptorSet(ds, Array.toList descriptors)

                res
            )

        let isCompatible (shaderType : ShaderType) (dataType : Type) =
            // TODO: verify type compatibility
            true

        let bufferViews =
            programLayout.PipelineInfo.pInputs
                |> List.sortBy (fun p -> p.location)
                |> List.map (fun p ->
                    let perInstance, view =
                        match ro.VertexAttributes.TryGetAttribute p.semantic with
                            | Some att -> false, att
                            | None ->
                                match ro.InstanceAttributes.TryGetAttribute p.semantic with
                                    | Some att -> true, att
                                    | None -> failwithf "could not get vertex data for shader input: %A" p.semantic

                    (p.semantic, p.location, perInstance, view)
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

        let inputAssembly = this.CreateInputAssemblyState(ro.Mode)
        let inputState = this.CreateVertexInputState(programLayout.PipelineInfo, Mod.constant (VertexInputState.create bufferFormats))
        let rasterizerState = this.CreateRasterizerState(ro.DepthTest, ro.CullMode, ro.FillMode)
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
            match ro.IndirectBuffer with
                | null -> 
                    this.CreateDrawCall(indexed, ro.DrawCallInfos)
                | b -> 
                    let indirect = this.CreateIndirectBuffer(indexed, b)
                    this.CreateDrawCall(indexed, indirect)
        resources.Add calls
        let bindings =
            this.CreateVertexBufferBinding(buffers)
            
        resources.Add bindings

        let descriptorBindings =
            this.CreateDescriptorSetBinding(programLayout, Array.toList descriptorSets)
            
        resources.Add(descriptorBindings)

        let isActive = this.CreateIsActive ro.IsActive
        resources.Add isActive

        for r in resources do r.Acquire()

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
    static member PrepareRenderObject(this : ResourceManager, token : AdaptiveToken, renderPass : RenderPass, ro : RenderObject) =
        prepareObject token this renderPass ro

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, token : AdaptiveToken, renderPass : RenderPass, ro : IRenderObject, hook : RenderObject -> RenderObject) =
        match ro with
            | :? RenderObject as ro ->
                let res = prepareObject token this renderPass (hook ro)
                new PreparedMultiRenderObject([res])

            | :? MultiRenderObject as mo ->
                let all = mo.Children |> List.collect (fun c -> DevicePreparedRenderObjectExtensions.PrepareRenderObject(this, token, renderPass, c, hook).Children)
                new PreparedMultiRenderObject(all)

            | :? PreparedRenderObject as o ->
                new PreparedMultiRenderObject([o])

            | :? PreparedMultiRenderObject as mo ->
                mo

            | _ ->
                failf "unsupported RenderObject-type: %A" ro

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, token : AdaptiveToken, renderPass : RenderPass, ro : IRenderObject) =
        DevicePreparedRenderObjectExtensions.PrepareRenderObject(this, token, renderPass, ro, id)