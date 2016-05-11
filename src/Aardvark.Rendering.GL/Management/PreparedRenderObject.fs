namespace Aardvark.Rendering.GL

open System
open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

[<CustomEquality;CustomComparison>]
type PreparedRenderObject =
    {
        mutable Activation : IDisposable
        Context : Context
        Parent : Option<PreparedRenderObject>
        Original : RenderObject
        FramebufferSignature : IFramebufferSignature
        LastTextureSlot : int
        Program : IResource<Program>
        UniformBuffers : Map<int, IResource<UniformBufferView>>
        Uniforms : Map<int, IResource<UniformLocation>>
        Textures : Map<int, IResource<Texture> * IResource<Sampler>>
        Buffers : list<int * BufferView * AttributeFrequency * IResource<Buffer>>
        IndexBuffer : Option<IResource<Buffer>>

        IndirectBuffer : Option<IResource<IndirectBuffer>>
        DrawCallInfos : IResource<list<DrawCallInfo>>

        mutable VertexArray : IResource<VertexArrayObject>
        VertexAttributeValues : Map<int, IMod<Option<V4f>>>
        
        ColorAttachmentCount : int
        ColorBufferMasks : Option<list<V4i>>
        DepthBufferMask : bool

        
        mutable IsDisposed : bool
    } 

    interface IRenderObject with
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    interface IPreparedRenderObject with
        member x.Update(caller) = x.Update(caller) |> ignore
        member x.Original = Some x.Original

    member x.Id = x.Original.Id
    member x.CreationPath = x.Original.Path
    member x.AttributeScope = x.Original.AttributeScope

    member x.IsActive = x.Original.IsActive
    member x.RenderPass = x.Original.RenderPass

    member x.Mode = x.Original.Mode

    member x.DepthTest = x.Original.DepthTest
    member x.CullMode = x.Original.CullMode
    member x.BlendMode = x.Original.BlendMode
    member x.FillMode = x.Original.FillMode
    member x.StencilMode = x.Original.StencilMode

    member x.Resources =
        seq {
            yield x.Program :> IResource
            for (_,b) in Map.toSeq x.UniformBuffers do
                yield b :> _

            for (_,u) in Map.toSeq x.Uniforms do
                yield u :> _

            for (_,(t,s)) in Map.toSeq x.Textures do
                yield t :> _
                yield s :> _

            for (_,_,_,b) in x.Buffers do
                yield b :> _

            match x.IndexBuffer with
                | Some ib -> yield ib :> _
                | _ -> ()

            match x.IndirectBuffer with
                | Some ib -> yield ib :> _
                | _ -> ()

            yield x.VertexArray :> _ 
            yield x.DrawCallInfos :> _
        }

    member x.Update(caller : IAdaptiveObject) =
        use token = x.Context.ResourceLock

        let mutable stats = FrameStatistics.Zero
        let add s = stats <- stats + s

        x.Program.Update(caller) |> add

        for (_,ub) in x.UniformBuffers |> Map.toSeq do
            ub.Update(caller) |> add

        for (_,ul) in x.Uniforms |> Map.toSeq do
            ul.Update(caller) |> add

        for (_,(t,s)) in x.Textures |> Map.toSeq do
            t.Update(caller) |> add
            s.Update(caller) |> add

        for (_,_,_,b) in x.Buffers  do
            b.Update(caller) |> add

        match x.IndexBuffer with
            | Some ib -> ib.Update(caller) |> add
            | _ -> ()

        match x.IndirectBuffer with
            | Some ib -> ib.Update(caller) |> add
            | _ -> ()

        x.VertexArray.Update(caller) |> add

        stats

    member x.Dispose() =
        if not x.IsDisposed then
            x.Activation.Dispose()
            x.IsDisposed <- true
            x.VertexArray.Dispose() 
            x.Buffers |> List.iter (fun (_,_,_,b) -> b.Dispose())
            x.IndexBuffer |> Option.iter (fun b -> b.Dispose())
            x.IndirectBuffer |> Option.iter (fun b -> b.Dispose())
            x.Textures |> Map.iter (fun _ (t,s) -> t.Dispose(); s.Dispose())
            x.Uniforms |> Map.iter (fun _ (ul) -> ul.Dispose())
            x.UniformBuffers |> Map.iter (fun _ (ub) -> ub.Dispose())
            x.Program.Dispose() 
            x.VertexArray <- Unchecked.defaultof<_>
            

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? PreparedRenderObject as o ->
                    compare x.Original o.Original
                | _ ->
                    failwith "uncomparable"

    override x.GetHashCode() = x.Original.GetHashCode()
    override x.Equals o =
        match o with
            | :? PreparedRenderObject as o -> x.Original = o.Original
            | _ -> false


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module PreparedRenderObject =
    let empty = 
        {
            Activation = { new IDisposable with member x.Dispose() = () }
            Context = Unchecked.defaultof<_>
            Original = RenderObject.Empty
            Parent = None
            FramebufferSignature = null
            LastTextureSlot = -1
            Program = Unchecked.defaultof<_>
            UniformBuffers = Map.empty
            Uniforms = Map.empty
            Textures = Map.empty
            Buffers = []
            IndexBuffer = None
            IndirectBuffer = None
            DrawCallInfos = Unchecked.defaultof<_>
            VertexArray = Unchecked.defaultof<_>
            VertexAttributeValues = Map.empty
            ColorAttachmentCount = 0
            ColorBufferMasks = None
            DepthBufferMask = true
            IsDisposed = false
        }  

    let clone (o : PreparedRenderObject) =
        let res = 
            {
                Activation = { new IDisposable with member x.Dispose() = () }
                Context = o.Context
                Original = o.Original
                Parent = Some o
                FramebufferSignature = o.FramebufferSignature
                LastTextureSlot = o.LastTextureSlot
                Program = o.Program
                UniformBuffers = o.UniformBuffers
                Uniforms = o.Uniforms
                Textures = o.Textures
                Buffers = o.Buffers
                IndexBuffer = o.IndexBuffer
                IndirectBuffer = o.IndirectBuffer
                DrawCallInfos = o.DrawCallInfos
                VertexArray = o.VertexArray
                VertexAttributeValues = o.VertexAttributeValues
                ColorAttachmentCount = o.ColorAttachmentCount
                ColorBufferMasks = o.ColorBufferMasks
                DepthBufferMask = o.DepthBufferMask
                IsDisposed = o.IsDisposed
            }  

        for r in res.Resources do
            r.AddRef()

        res


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
    member x.Original = first.Original

    member x.First = first
    member x.Last = last

    interface IRenderObject with
        member x.AttributeScope = first.AttributeScope
        member x.RenderPass = first.RenderPass

    interface IPreparedRenderObject with
        member x.Original = Some first.Original
        member x.Update caller = x.Update caller |> ignore

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<Extension; AbstractClass; Sealed>]
type ResourceManagerExtensions private() =
  

    [<Extension>]
    static member Prepare (x : ResourceManager, fboSignature : IFramebufferSignature, rj : RenderObject) : PreparedRenderObject =
        // use a context token to avoid making context current/uncurrent repeatedly
        use token = x.Context.ResourceLock

        let activation = rj.Activate()

        // create a program and get its handle (ISSUE: assumed to be constant here)
        let program = x.CreateSurface(fboSignature, rj.Surface)
        let prog = program.Handle.GetValue()


        let createdViews = System.Collections.Generic.List()

        // create all UniformBuffers requested by the program
        let uniformBuffers =
            prog.UniformBlocks 
                |> List.map (fun block ->
                    block.index, x.CreateUniformBuffer(rj.AttributeScope, block, prog, rj.Uniforms)
                   )
                |> Map.ofList


        // partition all requested (top-level) uniforms into Textures and other
        let textureUniforms, otherUniforms = 
            prog.Uniforms |> List.partition (fun uniform -> match uniform.uniformType with | SamplerType -> true | _ -> false)

        // create all requested Textures
        let lastTextureSlot = ref -1
        let textures =
            textureUniforms
                |> List.choose (fun uniform ->
                    let tex = rj.Uniforms.TryGetUniform(rj.AttributeScope, uniform.semantic |> Symbol.Create)

                    match tex with
                        | Some value ->
                            let sampler =
                                match prog.SamplerStates.TryGetValue (Symbol.Create uniform.semantic) with
                                    | (true, sampler) -> sampler
                                    | _ -> 
                                        match uniform.samplerState with
                                            | Some sam ->
                                                match prog.SamplerStates.TryGetValue (Symbol.Create sam) with
                                                    | (true, sampler) -> sampler
                                                    | _ -> SamplerStateDescription()
                                            | None ->
                                                SamplerStateDescription()
                            let s = x.CreateSampler(Mod.constant sampler)

                            match value with
                                | :? IMod<ITexture> as value ->
                                    let t = x.CreateTexture(value)
                                    lastTextureSlot := uniform.slot
                                    Some (uniform.slot, (t, s))

                                | :? IMod<ITexture[]> as values ->
                                    let t = x.CreateTexture(values |> Mod.map (fun arr -> arr.[uniform.index]))
                                    lastTextureSlot := uniform.slot
                                    Some (uniform.slot, (t, s))

                                | _ ->
                                    Log.warn "unexpected texture type %s: %A" uniform.semantic value
                                    None
                        | _ ->
                            Log.warn "texture %s not found" uniform.semantic
                            None
                    )
                |> Map.ofList

        // create all requested UniformLocations
        let uniforms =
            otherUniforms
                |> List.choose (fun uniform ->
                    try
                        let r = x.CreateUniformLocation(rj.AttributeScope, rj.Uniforms, uniform)
                        Some (uniform.location, r)
                    with _ ->
                        None
                   )
                |> Map.ofList

        // create all requested vertex-/instance-inputs
        let buffers =
            prog.Inputs 
                |> List.map (fun v ->
                    match rj.VertexAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                        | Some value ->
                            let dep = x.CreateBuffer(value.Buffer)
                            v.attributeIndex, value, AttributeFrequency.PerVertex, dep
                        | _  -> 
                            match rj.InstanceAttributes with
                                | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" v.semantic rj
                                | _ -> 
                                    printfn "looking up %s in instance attribs" v.semantic
                                    match rj.InstanceAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                                        | Some value ->
                                            let dep = x.CreateBuffer(value.Buffer)
                                            v.attributeIndex, value, (AttributeFrequency.PerInstances 1), dep
                                        | _ -> 
                                            failwithf "could not get attribute %A" v.semantic
                   )

        // create the index buffer (if present)
        let index =
            if isNull rj.Indices then None
            else 
                x.CreateBuffer rj.Indices |> Some

        let indirect =
            if isNull rj.IndirectBuffer then None
            else x.CreateIndirectBuffer(not (isNull rj.Indices), rj.IndirectBuffer) |> Some

        // create the VertexArrayObject
        let vao =
            x.CreateVertexArrayObject(buffers, index)

        let attributeValues =
            buffers 
                |> List.map (fun (i,v,_,_) ->
                    i, v.Buffer |> Mod.map (fun v ->
                        match v with
                            | :? NullBuffer as nb -> 
                                Some nb.Value
                            | _ -> 
                                None
                    )
                ) |> Map.ofList

        let attachments = fboSignature.ColorAttachments |> Map.toList
        let attachmentCount = if attachments.Length > 0 then 1 + (attachments |> List.map (fun (i,_) -> i) |> List.max) else 0

        let colorMasks =
            match rj.WriteBuffers with
                | Some b ->
                    let isAll = fboSignature.ColorAttachments |> Map.toSeq |> Seq.forall (fun (_,(sem,_)) -> Set.contains sem b)
                    if isAll then
                        None
                    else
                        let masks = Array.zeroCreate attachmentCount
                        for (index, (sem, att)) in attachments do
                            if Set.contains sem b then
                                masks.[index] <- V4i.IIII
                            else
                                masks.[index] <- V4i.OOOO

                        Some (Array.toList masks)
                | _ ->
                    None

        let depthMask =
            match rj.WriteBuffers with
                | Some b -> Set.contains DefaultSemantic.Depth b
                | None -> true

        let drawCalls =
            if isNull rj.DrawCallInfos then
                { new Resource<list<DrawCallInfo>>(ResourceKind.Unknown) with
                    member x.Create(_) = [], FrameStatistics.Zero
                    member x.Destroy(_) = ()
                }
            else
                { new Resource<list<DrawCallInfo>>(ResourceKind.Unknown) with
                    member x.Create(_) = rj.DrawCallInfos.GetValue x, FrameStatistics.Zero
                    member x.Destroy(_) = ()
                }
        drawCalls.AddRef()

        // finally return the PreparedRenderObject
        {
            Activation = activation
            Context = x.Context
            Original = rj
            Parent = None
            FramebufferSignature = fboSignature
            LastTextureSlot = !lastTextureSlot
            Program = program
            UniformBuffers = uniformBuffers
            Uniforms = uniforms
            Textures = textures
            Buffers = buffers
            IndexBuffer = index
            IndirectBuffer = indirect
            DrawCallInfos = drawCalls
            VertexArray = vao
            VertexAttributeValues = attributeValues
            ColorAttachmentCount = attachmentCount
            ColorBufferMasks = colorMasks
            DepthBufferMask = depthMask
            IsDisposed = false
        }
