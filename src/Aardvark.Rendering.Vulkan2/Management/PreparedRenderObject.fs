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
        
        pipeline                : VulkanResource<Pipeline, VkPipeline>
        indexBuffer             : Option<IResource<nativeptr<IndexBufferBinding>>>
        uniformBuffers          : list<IResource<UniformBuffer>>
        descriptorSets          : IResource<nativeptr<DescriptorSetBinding>>
        vertexBuffers           : IResource<nativeptr<VertexBufferBinding>>
        drawCalls               : IResource<nativeptr<DrawCall>>
        isActive                : IResource<nativeint>
        activation              : IDisposable
    }
    member x.DrawCallInfos = x.original.DrawCallInfos
    member x.RenderPass = x.original.RenderPass
    member x.AttributeScope = x.original.AttributeScope

    member x.Dispose() =
        x.activation.Dispose()

        x.pipeline.Dispose()
        for b in x.uniformBuffers do
            b.Dispose()
        x.descriptorSets.Dispose()
        x.vertexBuffers.Dispose()
        x.drawCalls.Dispose()

        match x.indexBuffer with
            | Some ib -> ib.Dispose()
            | None -> ()

    member x.Update(caller : IAdaptiveObject) =
        use token = x.device.Token
        let mutable stats = FrameStatistics.Zero
        
        for b in x.uniformBuffers do
            stats <- stats + b.Update(caller)
        stats <- stats + x.pipeline.Update(caller)
        stats <- stats + x.descriptorSets.Update(caller)
        stats <- stats + x.vertexBuffers.Update(caller)
        stats <- stats + x.drawCalls.Update(caller)

        match x.indexBuffer with
            | Some b -> stats <- stats + b.Update(caller)
            | None -> ()

        stats

    member x.IncrementReferenceCount() =
        x.pipeline.AddRef()
        x.descriptorSets.AddRef()
        x.vertexBuffers.AddRef()
        x.drawCalls.AddRef()
        
        for b in x.uniformBuffers do
            b.AddRef()

        match x.indexBuffer with
            | Some ib -> ib.AddRef()
            | None -> ()

    interface IPreparedRenderObject with
        member x.Id = x.original.Id
        member x.RenderPass = x.original.RenderPass
        member x.AttributeScope = x.original.AttributeScope
        member x.Update(caller) = x.Update(caller) |> ignore
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

    member x.Update(caller : IAdaptiveObject) =
        children |> List.sumBy (fun c -> c.Update(caller))
        

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
        member x.Update caller = x.Update caller |> ignore

    interface IDisposable with
        member x.Dispose() = x.Dispose()

module private Array =
    let choosei (f : int -> 'a -> Option<'b>) (a : 'a[]) =
        let res = System.Collections.Generic.List<'b>()
        for i in 0 .. a.Length - 1 do
            match f i a.[i] with
                | Some v -> res.Add v
                | None -> ()

        res.ToArray()

[<AbstractClass; Sealed; Extension>]
type DevicePreparedRenderObjectExtensions private() =

    static let prepareObject (this : ResourceManager) (renderPass : RenderPass) (ro : RenderObject) =
        let program = this.CreateShaderProgram(renderPass, ro.Surface)
        let prog = program.Handle.GetValue()

        let uniformBuffers = System.Collections.Generic.List<IResource<UniformBuffer>>()
        let descriptorSets = 
            prog.PipelineLayout.DescriptorSetLayouts |> Array.map (fun ds ->
                let descriptors = 
                    ds.Bindings |> Array.choosei (fun i b ->
                        match b.Parameter with
                            | UniformBlockParameter block ->
                                let buffer = this.CreateUniformBuffer(ro.AttributeScope, block.layout, ro.Uniforms, prog.UniformGetters)
                                uniformBuffers.Add buffer
                                AdaptiveDescriptor.AdaptiveUniformBuffer (i, buffer.Handle.GetValue()) |> Some

                            | ImageParameter img ->
                                let name = Symbol.Create img.name

                                let semantic =
                                    match prog.Surface.SemanticMap.TryGetValue name with
                                        | (true, sem) -> sem
                                        | _ -> name   

                                let samplerState = 
                                    match prog.Surface.SamplerStates.TryGetValue name with
                                        | (true, sam) -> sam
                                        | _ -> 
                                            Log.warn "could not get sampler for texture: %A" name
                                            SamplerStateDescription()     
  
                                match ro.Uniforms.TryGetUniform(Ag.emptyScope, semantic) with
                                    | Some (:? IMod<ITexture> as tex) ->

                                        let tex = this.CreateImage(tex)
                                        let view = this.CreateImageView(tex)
                                        let sam = this.CreateSampler(Mod.constant samplerState)

                                        AdaptiveDescriptor.AdaptiveCombinedImageSampler (i, view, sam) |> Some

                                    | _ ->
                                        Log.warn "could not find texture: %A" semantic
                                        None
                    )

                this.CreateDescriptorSet(ds, descriptors)
            )


        let isCompatible (shaderType : ShaderType) (dataType : Type) =
            // TODO: verify type compatibility
            true

        let bufferViews =
            prog.Inputs
                |> Seq.map (fun p ->
                    let perInstance, view =
                        match ro.VertexAttributes.TryGetAttribute p.semantic with
                            | Some att -> false, att
                            | None ->
                                match ro.InstanceAttributes.TryGetAttribute p.semantic with
                                    | Some att -> true, att
                                    | None -> failwithf "could not get vertex data for shader input: %A" p.semantic

                    (p.semantic, p.location, perInstance, view)
                   )
                |> Seq.toList

        let buffers =
            bufferViews 
                |> Seq.sortBy (fun (_,l,_,_) -> l)
                |> Seq.map (fun (name,loc, _, view) ->
                    let buffer = this.CreateBuffer(view.Buffer)
                    buffer, int64 view.Offset
                    )
                |> Seq.toList


        let bufferFormats = 
            bufferViews |> Seq.map (fun (name,location, perInstance, view) ->
                name, (perInstance, view)
            ) |> Map.ofSeq


        let pipeline =
            this.CreatePipeline(
                renderPass, program,
                bufferFormats,
                ro.Mode,
                ro.FillMode, 
                ro.CullMode,
                ro.BlendMode,
                ro.DepthTest,
                ro.StencilMode,
                ro.WriteBuffers
            )

        let indexed = Option.isSome ro.Indices
        let indexBufferBinding =
            match ro.Indices with
                | Some view -> 
                    let buffer = this.CreateIndexBuffer(view.Buffer)
                    let res = this.CreateIndexBufferBinding(buffer, VkIndexType.ofType view.ElementType)

                    Some res
                | None -> 
                    None

            

        let calls =
            match ro.IndirectBuffer with
                | null -> this.CreateDrawCall(indexed, ro.DrawCallInfos)
                | b -> 
                    let indirect = this.CreateIndirectBuffer(b)
                    this.CreateDrawCall(indexed, indirect)

        let bindings =
            this.CreateVertexBufferBinding(buffers)

        let descriptorBindings =
            this.CreateDescriptorSetBinding(prog.PipelineLayout, descriptorSets)

        let isActive =
            { new Rendering.Resource<nativeint>(ResourceKind.Unknown) with
                member x.Create (old : Option<nativeint>) =
                    let ptr =
                        match old with
                            | Some ptr -> ptr
                            | None -> Marshal.AllocHGlobal 4

                    let v = ro.IsActive.GetValue(x)
                    NativeInt.write ptr (if v then 1 else 0)
                    
                    ptr, FrameStatistics.Zero

                member x.Destroy (h : nativeint) =
                    Marshal.FreeHGlobal h

                member x.GetInfo h =
                    ResourceInfo.Zero
            }
        isActive.AddRef()


        let res = 
            {
                device                      = this.Device
                original                    = ro
                uniformBuffers              = CSharpList.toList uniformBuffers
                descriptorSets              = descriptorBindings
                pipeline                    = pipeline
                vertexBuffers               = bindings
                indexBuffer                 = indexBufferBinding
                drawCalls                   = calls
                isActive                    = isActive
                activation                  = ro.Activate()
            }
        res

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, renderPass : RenderPass, ro : RenderObject) =
        prepareObject this renderPass ro

    [<Extension>]
    static member PrepareRenderObject(this : ResourceManager, renderPass : RenderPass, ro : IRenderObject) =
        match ro with
            | :? RenderObject as ro ->
                let res = prepareObject this renderPass ro
                new PreparedMultiRenderObject([res])

            | :? MultiRenderObject as mo ->
                let all = mo.Children |> List.collect (fun c -> DevicePreparedRenderObjectExtensions.PrepareRenderObject(this, renderPass, c).Children)
                new PreparedMultiRenderObject(all)

            | :? PreparedRenderObject as o ->
                o.IncrementReferenceCount()
                new PreparedMultiRenderObject([o])

            | :? PreparedMultiRenderObject as mo ->
                mo.Children |> List.iter (fun c -> c.IncrementReferenceCount())
                mo

            | _ ->
                failf "unsupported RenderObject-type: %A" ro
