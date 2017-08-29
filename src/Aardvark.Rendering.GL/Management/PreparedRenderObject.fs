namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics.OpenGL4


[<CustomEquality;CustomComparison>]
type PreparedRenderObject =
    {
        mutable Activation : IDisposable
        Context : Context
        Parent : Option<PreparedRenderObject>
        Original : RenderObject
        FramebufferSignature : IFramebufferSignature
        LastTextureSlot : int
        Program : IResource<Program, int>
        UniformBuffers : Map<int, IResource<UniformBufferView, int>>
        Uniforms : Map<int, IResource<UniformLocation, nativeint>>
        Textures : Map<int, IResource<Texture, int> * IResource<Sampler, int>>
        Buffers : list<int * BufferView * AttributeFrequency * IResource<Buffer, int>>
        IndexBuffer : Option<OpenGl.Enums.IndexType * IResource<Buffer, int>>
        
        IsActive : IResource<bool, int>
        BeginMode : IResource<GLBeginMode, GLBeginMode>
        DrawCallInfos : IResource<DrawCallInfoList, DrawCallInfoList>
        IndirectBuffer : Option<IResource<IndirectBuffer, V2i>>
        DepthTestMode : IResource<DepthTestInfo, DepthTestInfo>
        CullMode : IResource<int, int>
        PolygonMode : IResource<int, int>
        BlendMode : IResource<GLBlendMode, GLBlendMode>
        StencilMode : IResource<GLStencilMode, GLStencilMode>
        ConservativeRaster : IResource<bool, int>
        Multisample : IResource<bool, int>

        VertexInputBinding : IResource<VertexInputBindingHandle, int>
        
        ColorAttachmentCount : int
        DrawBuffers : Option<DrawBufferConfig>
        ColorBufferMasks : Option<list<V4i>>
        DepthBufferMask : bool
        StencilBufferMask : bool

        mutable ResourceCount : int
        mutable ResourceCounts : Map<ResourceKind, int>

        mutable IsDisposed : bool
    } 

    interface IRenderObject with
        member x.Id = x.Original.Id
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    interface IPreparedRenderObject with
        member x.Update(caller, token) = x.Update(caller, token) |> ignore
        member x.Original = Some x.Original

    member x.Id = x.Original.Id
    member x.CreationPath = x.Original.Path
    member x.AttributeScope = x.Original.AttributeScope
    member x.RenderPass = x.Original.RenderPass

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
                | Some (_,ib) -> yield ib :> _
                | _ -> ()

            match x.IndirectBuffer with
                | Some ib -> yield ib :> _
                | _ -> yield x.DrawCallInfos :> _


            yield x.VertexInputBinding :> _ 
            yield x.IsActive :> _
            yield x.BeginMode :> _
            yield x.ConservativeRaster :> _
            yield x.Multisample :> _
            yield x.DepthTestMode :> _
            yield x.CullMode :> _
            yield x.PolygonMode :> _
            yield x.BlendMode :> _
            yield x.StencilMode :> _
        }

    member x.Update(caller : AdaptiveToken, token : RenderToken) =
        use ctxToken = x.Context.ResourceLock

        x.Program.Update(caller, token)

        for (_,ub) in x.UniformBuffers |> Map.toSeq do
            ub.Update(caller, token)

        for (_,ul) in x.Uniforms |> Map.toSeq do
            ul.Update(caller, token)

        for (_,(t,s)) in x.Textures |> Map.toSeq do
            t.Update(caller, token)
            s.Update(caller, token)

        for (_,_,_,b) in x.Buffers  do
            b.Update(caller, token)

        match x.IndexBuffer with
            | Some (_,ib) -> ib.Update(caller, token)
            | _ -> ()

        match x.IndirectBuffer with
            | Some ib -> ib.Update(caller, token)
            | _ -> x.DrawCallInfos.Update(caller, token)

        x.VertexInputBinding.Update(caller, token)


        x.IsActive.Update(caller, token)
        x.BeginMode.Update(caller, token)
        x.DepthTestMode.Update(caller, token)
        x.CullMode.Update(caller, token)
        x.PolygonMode.Update(caller, token)
        x.BlendMode.Update(caller, token)
        x.StencilMode.Update(caller, token)
        x.ConservativeRaster.Update(caller, token)
        x.Multisample.Update(caller, token)

    member x.Dispose() =
        lock x (fun () -> 
            if not x.IsDisposed then
                x.IsDisposed <- true

                // ObjDisposed might occur here if GL is dead already and render objects get disposed nondeterministically by finalizer thread.
                let resourceLock = try Some x.Context.ResourceLock with :? ObjectDisposedException as o -> None

                match resourceLock with
                    | None ->
                        // OpenGL already dead
                        ()
                    | Some l -> 
                        use resourceLock = l

                        OpenTK.Graphics.OpenGL4.GL.UnbindAllBuffers()
                        x.Activation.Dispose()
                        match x.DrawBuffers with
                            | Some b -> b.RemoveRef()
                            | _ -> ()
                        x.VertexInputBinding.Dispose() 
                        x.Buffers |> List.iter (fun (_,_,_,b) -> b.Dispose())
                        x.IndexBuffer |> Option.iter (fun (_,b) -> b.Dispose())
                        match x.IndirectBuffer with
                            | Some b -> b.Dispose()
                            | None -> x.DrawCallInfos.Dispose()

                        x.Textures |> Map.iter (fun _ (t,s) -> t.Dispose(); s.Dispose())
                        x.Uniforms |> Map.iter (fun _ (ul) -> ul.Dispose())
                        x.UniformBuffers |> Map.iter (fun _ (ub) -> ub.Dispose())
                        //x.Program.Dispose() // dont want anymore

                        x.IsActive.Dispose()
                        x.BeginMode.Dispose()
                        x.DepthTestMode.Dispose()
                        x.CullMode.Dispose()
                        x.PolygonMode.Dispose()
                        x.BlendMode.Dispose()
                        x.StencilMode.Dispose()
                        x.ConservativeRaster.Dispose()
                        x.Multisample.Dispose()
        )
        
             

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
            VertexInputBinding = Unchecked.defaultof<_>
            ColorAttachmentCount = 0
            DrawBuffers = None
            ColorBufferMasks = None
            DepthBufferMask = true
            StencilBufferMask = true
            IsDisposed = false
            ResourceCount = 0
            ResourceCounts = Map.empty
            IsActive = Unchecked.defaultof<_>
            BeginMode = Unchecked.defaultof<_>
            DrawCallInfos = Unchecked.defaultof<_>
            DepthTestMode = Unchecked.defaultof<_>
            CullMode = Unchecked.defaultof<_>
            PolygonMode = Unchecked.defaultof<_>
            BlendMode = Unchecked.defaultof<_>
            StencilMode = Unchecked.defaultof<_>
            ConservativeRaster = Unchecked.defaultof<_>
            Multisample = Unchecked.defaultof<_>
        }  

    let clone (o : PreparedRenderObject) =
        let drawBuffers =
            match o.DrawBuffers with
                | Some b ->
                    b.AddRef()
                    Some b
                | _ ->
                    None

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
                VertexInputBinding = o.VertexInputBinding
                ColorAttachmentCount = o.ColorAttachmentCount
                DrawBuffers = drawBuffers
                ColorBufferMasks = o.ColorBufferMasks
                DepthBufferMask = o.DepthBufferMask
                StencilBufferMask = o.StencilBufferMask
                IsDisposed = o.IsDisposed
                ResourceCount = o.ResourceCount
                ResourceCounts = o.ResourceCounts

                IsActive  = o.IsActive 
                BeginMode  = o.BeginMode 
                DrawCallInfos  = o.DrawCallInfos 
                DepthTestMode  = o.DepthTestMode 
                CullMode  = o.CullMode 
                PolygonMode  = o.PolygonMode 
                BlendMode  = o.BlendMode 
                StencilMode  = o.StencilMode 
                ConservativeRaster = o.ConservativeRaster
                Multisample = o.Multisample
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

    member x.Update(caller : AdaptiveToken, token : RenderToken) =
        children |> List.iter (fun c -> c.Update(caller, token))
        

    member x.RenderPass = first.RenderPass
    member x.Original = first.Original

    member x.First = first
    member x.Last = last

    interface IRenderObject with
        member x.Id = first.Id
        member x.AttributeScope = first.AttributeScope
        member x.RenderPass = first.RenderPass

    interface IPreparedRenderObject with
        member x.Original = Some first.Original
        member x.Update(caller, token) = x.Update(caller, token)

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<Extension; AbstractClass; Sealed>]
type ResourceManagerExtensions private() =
  

    [<Extension>]
    static member Prepare (x : ResourceManager, fboSignature : IFramebufferSignature, rj : RenderObject) : PreparedRenderObject =
        // use a context token to avoid making context current/uncurrent repeatedly
        use token = x.Context.ResourceLock

//        ContextHandle.Current.Value.AttachDebugOutputIfNeeded()
//        OpenTK.Graphics.OpenGL4.GL.Enable(OpenTK.Graphics.OpenGL4.EnableCap.DebugOutput)

        let activation = rj.Activate()

        // create a program and get its handle (ISSUE: assumed to be constant here)
//        let surface =
//            if rj.Surface.IsConstant then
//                let surface = rj.Surface.GetValue()
//                match surface with
//                    | :? IGeneratedSurface as s -> 
//                        s.Generate(x.Context.Runtime,fboSignature) :> ISurface |> Mod.constant
//                    | _ -> rj.Surface
//            else rj.Surface

        let program = x.CreateSurface(fboSignature, rj.Surface)
        let prog = program.Handle.GetValue()

        GL.Check "[Prepare] Create Surface"

        let createdViews = System.Collections.Generic.List()

        // create all UniformBuffers requested by the program
        let uniformBuffers =
            prog.Interface.UniformBlocks 
                |> List.map (fun block ->
                    block.Index, x.CreateUniformBuffer(rj.AttributeScope, block, prog, rj.Uniforms)
                   )
                |> Map.ofList

        GL.Check "[Prepare] Uniform Buffers"

        // partition all requested (top-level) uniforms into Textures and other
        let textureUniforms, otherUniforms = 
            prog.Interface.Uniforms |> List.partition (fun uniform -> 
                match uniform.Type with 
                    | Sampler _ | FixedArray(Sampler _, _, _) | DynamicArray(Sampler _, _) -> true 
                    | _ -> false
            )

        // create all requested Textures
        let lastTextureSlot = ref -1

        let samplerModifier = 
            match rj.Uniforms.TryGetUniform(rj.AttributeScope, DefaultSemantic.SamplerStateModifier) with
                | Some(:? IMod<Symbol -> SamplerStateDescription -> SamplerStateDescription> as mode) ->
                    Some mode
                | _ ->
                    None

        let textures =
            textureUniforms
                |> List.collect (fun u ->
                    match u.Type with
                        | FixedArray(t, _, cnt) ->
                            List.init cnt (fun i -> { u with Type = t; Path = ShaderPath.Item(u.Path, i); Binding = u.Binding + i })
                        | _ -> 
                            [u]
                   )
                |> List.choose (fun uniform ->
                    let name, index =
                        match uniform.Path with 
                            | ShaderPath.Value name -> name, 0
                            | ShaderPath.Item (ShaderPath.Value name, index) -> name, index
                            | p -> failwithf "[GL] unexpected texture uniform path %A" p

                    let samplerInfo =
                        match Map.tryFind (name, index) prog.TextureInfo with
                            | Some info -> info
                            | _ ->
                                Log.warn "could not get samplerstate for uniform %A" uniform 
                                { textureName = Symbol.Create name; samplerState = SamplerStateDescription() }

                    let sem = samplerInfo.textureName
                    let samplerState = samplerInfo.samplerState

                    match rj.Uniforms.TryGetUniform(rj.AttributeScope, sem) with
                        | Some tex ->
        
                            let sampler =
                                match samplerModifier with
                                    | Some modifier -> 
                                        modifier |> Mod.map (fun f -> f sem samplerState)
                                    | None -> 
                                        Mod.constant samplerState

                            let s = x.CreateSampler(sampler)

                            match tex with
                                | :? IMod<ITexture> as value ->
                                    let t = x.CreateTexture(value)
                                    lastTextureSlot := uniform.Binding
                                    Some (uniform.Binding, (t, s))

                                | :? IMod<ITexture[]> as values ->
                                    let t = x.CreateTexture(values |> Mod.map (fun arr -> arr.[index]))
                                    lastTextureSlot := uniform.Binding
                                    Some (uniform.Binding, (t, s))

                                | _ ->
                                    Log.warn "unexpected texture type %A: %A" sem tex
                                    None
                        | _ ->
                            Log.warn "texture %A not found" sem
                            None
                    )
                |> Map.ofList

        GL.Check "[Prepare] Textures"

        // create all requested UniformLocations
        let uniforms =
            otherUniforms
                |> List.choose (fun uniform ->
                    try
                        let r = x.CreateUniformLocation(rj.AttributeScope, rj.Uniforms, uniform)
                        Some (uniform.Location, r)
                    with _ ->
                        None
                   )
                |> Map.ofList

        GL.Check "[Prepare] Create Uniform Location"

        // create all requested vertex-/instance-inputs
        let buffers =
            prog.Interface.Inputs 
                |> List.choose (fun v ->
                     if v.Location >= 0 then
                        let expected = ShaderParameterType.getExpectedType v.Type
                        let sem = v.Path |> ShaderPath.name |> Symbol.Create
                        match rj.VertexAttributes.TryGetAttribute sem with
                            | Some value ->
                                let dep = x.CreateBuffer(value.Buffer)
                                Some (v.Location, value, AttributeFrequency.PerVertex, dep)
                            | _  -> 
                                match rj.InstanceAttributes with
                                    | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" sem rj
                                    | _ -> 
                                        match rj.InstanceAttributes.TryGetAttribute sem with
                                            | Some value ->
                                                let dep = x.CreateBuffer(value.Buffer)
                                                Some(v.Location, value, (AttributeFrequency.PerInstances 1), dep)
                                            | _ -> 
                                                failwithf "could not get attribute %A" sem
                        else
                            None
                   )

        GL.Check "[Prepare] Buffers"

        // create the index buffer (if present)
        let index =
            match rj.Indices with
                | Some i -> 
                    let buffer = x.CreateBuffer i.Buffer
                    let indexType =
                        let indexType = i.ElementType
                        if indexType = typeof<byte> then OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<uint16> then OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<uint32> then OpenGl.Enums.IndexType.UnsignedInt
                        elif indexType = typeof<sbyte> then OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<int16> then OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<int32> then OpenGl.Enums.IndexType.UnsignedInt
                        else failwithf "unsupported index type: %A"  indexType
                    Some(indexType, buffer)

                | None -> None


        GL.Check "[Prepare] Indices"

        let indirect =
            if isNull rj.IndirectBuffer then None
            else x.CreateIndirectBuffer(Option.isSome rj.Indices, rj.IndirectBuffer) |> Some

        GL.Check "[Prepare] Indirect Buffer"

        // create the VertexArrayObject
        let vibh =
            x.CreateVertexInputBinding(buffers, index)

        GL.Check "[Prepare] VAO"
//
//        let attributeValues =
//            buffers 
//                |> List.choose (fun (i,v,_,_) ->
//                    match v.SingleValue with
//                        | Some v -> Some (i,v)
//                        | _ -> None
//                ) |> Map.ofList

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



        let drawBuffers = 
            match rj.WriteBuffers with
                | Some set -> x.DrawBufferManager.CreateConfig(set) |> Some
                | _ -> None

        let depthMask =
            match rj.WriteBuffers with
                | Some b -> Set.contains DefaultSemantic.Depth b
                | None -> true

        let stencilMask =
            match rj.WriteBuffers with
                | Some b -> Set.contains DefaultSemantic.Stencil b
                | None -> true


        let isActive = x.CreateIsActive rj.IsActive
        let beginMode = 
            let hasTess = program.Handle.GetValue().Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl || s.Stage = ShaderStage.TessEval)
            x.CreateBeginMode(hasTess, rj.Mode)
        let drawCalls = if isNull rj.DrawCallInfos then Unchecked.defaultof<_> else x.CreateDrawCallInfoList rj.DrawCallInfos
        let depthTest = x.CreateDepthTest rj.DepthTest
        let cullMode = x.CreateCullMode rj.CullMode
        let polygonMode = x.CreatePolygonMode rj.FillMode
        let blendMode = x.CreateBlendMode rj.BlendMode
        let stencilMode = x.CreateStencilMode rj.StencilMode
        let conservativeRaster = x.CreateFlag rj.ConservativeRaster
        let multisample = x.CreateFlag rj.Multisample


        // finally return the PreparedRenderObject
        let res = 
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
                VertexInputBinding = vibh
                //VertexAttributeValues = attributeValues
                ColorAttachmentCount = attachmentCount
                DrawBuffers = drawBuffers
                ColorBufferMasks = colorMasks
                DepthBufferMask = depthMask
                StencilBufferMask = stencilMask
                IsDisposed = false
                ResourceCount = -1
                ResourceCounts = Map.empty

                IsActive = isActive
                BeginMode = beginMode
                DrawCallInfos = drawCalls
                DepthTestMode = depthTest
                CullMode = cullMode
                PolygonMode = polygonMode
                BlendMode = blendMode
                StencilMode = stencilMode
                ConservativeRaster = conservativeRaster
                Multisample = multisample
            }

        res.ResourceCount <- res.Resources |> Seq.length
        res.ResourceCounts <- res.Resources |> Seq.countBy (fun r -> r.Kind) |> Map.ofSeq

        OpenTK.Graphics.OpenGL4.GL.UnbindAllBuffers()

        res
