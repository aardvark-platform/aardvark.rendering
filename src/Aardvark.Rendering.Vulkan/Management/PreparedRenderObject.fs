namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Collections.Concurrent
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering.Vulkan


type PreparedRenderObject =
    {
        ctx : Context
        original : RenderObject
        indirect : Option<Resource<IndirectBuffer>>
        geometryMode : IMod<IndexedGeometryMode>
        program : Resource<ShaderProgram>
        descriptorResources : list<IResource>
        descriptorSets : list<Resource<DescriptorSet>>
        pipeline : Resource<Pipeline>
        vertexBuffers : array<Resource<Buffer> * int>
        indexBuffer : Option<Resource<Buffer>>
        activation : IDisposable
    }

    member x.DrawCallInfos = x.original.DrawCallInfos
    member x.RenderPass = x.original.RenderPass
    member x.AttributeScope = x.original.AttributeScope

    member x.Dispose() =
        //x.geometryMode.Dispose()
        x.activation.Dispose()
        x.program.Dispose()
        x.pipeline.Dispose()
        for r in x.descriptorResources do r.Dispose()
        for r in x.descriptorSets do r.Dispose()

        for (b,_) in x.vertexBuffers do b.Dispose()

        match x.indirect with
            | Some i -> i.Dispose()
            | None -> ()

        match x.indexBuffer with
            | Some ib -> ib.Dispose()
            | None -> ()

    member x.Update(caller : IAdaptiveObject) =
        command {
            x.geometryMode |> Mod.force |> ignore
            do! x.program.Update(caller)
            do! x.pipeline.Update(caller)

            for r in x.descriptorResources do do! r.Update(caller)
            for r in x.descriptorSets do do! r.Update(caller)
            for (b,_) in x.vertexBuffers do do! b.Update(caller)

            match x.indirect with
                | Some b -> do! b.Update(caller)
                | None -> ()

            match x.indexBuffer with
                | Some b -> do! b.Update(caller)
                | None -> ()
        }

    member x.IncrementReferenceCount() =
        //x.geometryMode.IncrementReferenceCount()
        x.program.AddRef()
        x.pipeline.AddRef()

        for r in x.descriptorResources do r.AddRef()
        for r in x.descriptorSets do r.AddRef()
        for (b,_) in x.vertexBuffers do b.AddRef()

        match x.indirect with
            | Some ib -> ib.AddRef()
            | None -> ()

        match x.indexBuffer with
            | Some ib -> ib.AddRef()
            | None -> ()

    interface IPreparedRenderObject with
        member x.Id = x.original.Id
        member x.RenderPass = x.original.RenderPass
        member x.AttributeScope = x.original.AttributeScope
        member x.Update(caller) = x.Update(caller) |> x.ctx.DefaultQueue.RunSynchronously
        member x.Original = Some x.original
        member x.Dispose() = x.Dispose()

[<AbstractClass; Sealed; Extension>]
type ResourceMangerExtensions private() =

    [<Extension>]
    static member PrepareRenderObject(x : ResourceManager, pass : RenderPass, ro : IRenderObject) =
        match ro with
            | :? PreparedRenderObject as prep -> 
                prep.IncrementReferenceCount()
                prep
            | :? RenderObject as ro ->

                    
                let program = 
                    x.CreateShaderProgram(
                        pass, 
                        ro.Surface
                    )

                let prog = program.Handle |> Mod.force

                let descSetsAndResources = 
                    prog.DescriptorSetLayouts
                        |> Seq.map (fun dsl ->
                            let buffers =
                                dsl.Descriptors |> Seq.choose (fun desc ->
                                    match desc.Parameter.paramType with
                                        | ShaderType.Ptr(_, ShaderType.Struct(_,fields)) ->
                                            let layout = UniformBufferLayoutStd140.structLayout fields
                                            let buffer = x.CreateUniformBuffer(layout, ro.Uniforms)
                                            match Parameter.tryGetBinding desc.Parameter with
                                                | Some b ->
                                                    (b, buffer) |> Some
                                                | None ->
                                                    None

                                        | _ -> None
                                )
                                |> Map.ofSeq


                            let images =
                                dsl.Descriptors |> Seq.choose (fun desc ->
                                    match desc.Parameter.paramType with
                                        | ShaderType.Ptr(_, ShaderType.Image(sampledType, dim, isDepth, isArray, isMS, _, _)) 
                                        | ShaderType.Image(sampledType, dim, isDepth, isArray, isMS, _, _) ->
                                            let sym = Symbol.Create desc.Parameter.paramName

                                            let sym =
                                                match prog.Surface.SemanticMap.TryGetValue sym with
                                                    | (true, sem) -> sem
                                                    | _ -> sym

                                            let binding =
                                                match Parameter.tryGetBinding desc.Parameter with
                                                    | Some b -> b
                                                    | None -> failwithf "image has not been given an explicit binding: %A" desc.Parameter

                                            let samplerState = 
                                                match prog.Surface.SamplerStates.TryGetValue sym with
                                                    | (true, sam) -> sam
                                                    | _ -> 
                                                        Log.warn "could not get sampler for texture: %A" sym
                                                        SamplerStateDescription()



                                            match ro.Uniforms.TryGetUniform(Ag.emptyScope, sym) with
                                                | Some (:? IMod<ITexture> as tex) ->

                                                    let tex = x.CreateImage(tex)
                                                    let view = x.CreateImageView(tex)
                                                    let sam = x.CreateSampler(samplerState)

                                                    Some (binding, (tex, view, sam))
                                                | _ -> 
                                                    failwithf "could not find texture: %A" desc.Parameter


                                        | _ -> None
                                )
                                |> Map.ofSeq

                            // TODO: images

                            let resources = 
                                List.concat [
                                    buffers |> Map.toList |> List.map (fun (_,b) -> b :> IResource)
                                    images |> Map.toList |> List.collect (fun (_,(t,v,s)) -> [t :> IResource; v; s])
                                ]

                            let imageViews =
                                images |> Map.map (fun _ (t,v,s) -> v,s)

                            x.CreateDescriptorSet(dsl, buffers, imageViews), resources
                        )
                        |> Seq.toList

                let descriptorResources = descSetsAndResources |> List.collect snd
                let descriptorSets = descSetsAndResources |> List.map fst


                let isCompatible (shaderType : ShaderType) (dataType : Type) =
                    // TODO: verify type compatibility
                    true

                let bufferViews =
                    prog.Inputs
                        |> Seq.map (fun param ->
                            match Parameter.tryGetLocation param with
                                | Some loc -> loc, param.paramName, param.paramType
                                | None -> failwithf "no attribute location given for shader input: %A" param
                            )

                        |> Seq.map (fun (location, parameterName, parameterType) ->
                            let sym = Symbol.Create parameterName

                            let perInstance, view =
                                match ro.VertexAttributes.TryGetAttribute sym with
                                    | Some att -> false, att
                                    | None ->
                                        match ro.InstanceAttributes.TryGetAttribute sym with
                                            | Some att -> true, att
                                            | None -> failwithf "could not get vertex data for shader input: %A" parameterName

                            if isCompatible parameterType view.ElementType then
                                (parameterName, location, perInstance, view)
                            else
                                failwithf "vertex data has incompatible type for shader: %A vs %A" view.ElementType parameterType
                            )
                        |> Seq.toList

                let bufferFormats = 
                    bufferViews |> Seq.map (fun (name,location, perInstance, view) -> name, (perInstance, view.ElementType)) |> Map.ofSeq

                // TODO: changeable geometry mode
                let geometryMode =
                    ro.Mode

                let pipeline =
                    x.CreatePipeline(
                        pass,
                        program,
                        bufferFormats,
                        geometryMode,
                        ro.FillMode,
                        ro.CullMode,
                        ro.BlendMode,
                        ro.DepthTest,
                        ro.StencilMode
                    )

                let buffers =
                    bufferViews 
                        |> Seq.sortBy (fun (_,l,_,_) -> l)
                        |> Seq.map (fun (name,loc, _, view) ->
                            let buffer = x.CreateBuffer(view.Buffer)
                            buffer, view.Offset
                            )
                        |> Seq.toArray

                let indexBuffer =
                    match ro.Indices with
                        | null -> None
                        | indices -> x.CreateBuffer(ro.Indices, VkBufferUsageFlags.IndexBufferBit) |> Some


                let indirect =
                    match ro.IndirectBuffer with
                        | null -> None
                        | indirect -> 
                            let hasIndex = Option.isSome indexBuffer
                            x.CreateIndirectBuffer(ro.IndirectBuffer, hasIndex) |> Some

                let res =
                    {
                        ctx = x.Context
                        original = ro
                        geometryMode = geometryMode
                        indirect = indirect
                        program = program
                        descriptorResources = descriptorResources
                        descriptorSets = descriptorSets
                        pipeline = pipeline
                        vertexBuffers = buffers
                        indexBuffer = indexBuffer
                        activation = ro.Activate()
                    }
                
                res.Update(null) |> x.Context.DefaultQueue.RunSynchronously
                
                res

            | _ -> failwithf "unsupported RenderObject-type: %A" ro