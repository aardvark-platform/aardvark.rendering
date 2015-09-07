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
        Context : Context
        Original : RenderObject

        Program : ChangeableResource<Program>
        UniformBuffers : list<int * ChangeableResource<UniformBuffer>>
        Uniforms : list<int * ChangeableResource<UniformLocation>>
        Textures : list<int * ChangeableResource<Texture> * ChangeableResource<Sampler>>
        Buffers : list<int * ChangeableResource<Buffer> * IMod<AttributeDescription>>
        IndexBuffer : Option<ChangeableResource<Buffer>>
        VertexArray : ChangeableResource<VertexArrayObject>

    } with

    member x.Id = x.Original.Id
    member x.CreationPath = x.Original.CreationPath

    member x.AttributeScope = x.Original.AttributeScope
    member x.IsActive = x.Original.IsActive
    member x.RenderPass = x.Original.RenderPass
    member x.DrawCallInfo = x.Original.DrawCallInfo
    member x.DepthTest = x.Original.DepthTest
    member x.CullMode = x.Original.CullMode
    member x.BlendMode = x.Original.BlendMode
    member x.FillMode = x.Original.FillMode
    member x.StencilMode = x.Original.StencilMode

    member x.Update() =
        use token = x.Context.ResourceLock

        if x.Program.OutOfDate then
            x.Program.UpdateCPU()
            x.Program.UpdateGPU()

        for (_,ub) in x.UniformBuffers do
            if ub.OutOfDate then
                ub.UpdateCPU()
                ub.UpdateGPU()

        for (_,ul) in x.Uniforms do
            if ul.OutOfDate then
                ul.UpdateCPU()
                ul.UpdateGPU()

        for (_,t,s) in x.Textures do
            if t.OutOfDate then
                t.UpdateCPU()
                t.UpdateGPU()

            if s.OutOfDate then
                s.UpdateCPU()
                s.UpdateGPU()

        for (_,b,_) in x.Buffers do
            if b.OutOfDate then
                b.UpdateCPU()
                b.UpdateGPU()

        match x.IndexBuffer with
            | Some ib ->
                if ib.OutOfDate then
                    ib.UpdateCPU()
                    ib.UpdateGPU()
            | _ -> ()

        if x.VertexArray.OutOfDate then
            x.VertexArray.UpdateCPU()
            x.VertexArray.UpdateGPU()

    member x.Dispose() =
        x.VertexArray.Dispose()
        x.Buffers |> List.iter (fun (_,b,_) -> b.Dispose())
        x.IndexBuffer |> Option.iter (fun b -> b.Dispose())
        x.Textures |> List.iter (fun (_,t,s) -> t.Dispose(); s.Dispose())
        x.Uniforms |> List.iter (fun (_,ul) -> ul.Dispose())
        x.UniformBuffers |> List.iter (fun (_,ub) -> ub.Dispose())
        x.Program.Dispose()

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


[<Extension; AbstractClass; Sealed>]
type ResourceManagerExtensions private() =
  
    static let viewCache = System.Collections.Concurrent.ConcurrentDictionary<BufferView * ChangeableResource<Aardvark.Rendering.GL.Buffer>, IMod<AttributeDescription>>()

    static let createView (frequency : AttributeFrequency) (m : BufferView) (b : ChangeableResource<Aardvark.Rendering.GL.Buffer>) =
        viewCache.GetOrAdd(((m,b)), (fun (m,b) ->
            b.Resource |> Mod.map (fun b ->
                { Type = m.ElementType; Frequency = frequency; Normalized = false; Stride = m.Stride; Offset = m.Offset; Buffer = b }
            )
        ))

    [<Extension>]
    static member Prepare (x : ResourceManager, rj : RenderObject) : PreparedRenderObject =
        use token = x.Context.ResourceLock

        let program = x.CreateSurface(rj.Surface)

        let prog = program.Resource.GetValue()

        let uniformBuffers =
            prog.UniformBlocks |> List.map (fun block ->
                let mutable values = []
                // TODO: maybe don't ignore values (are buffers actually equal when using identical values)
                block.index, x.CreateUniformBuffer(rj.AttributeScope, block, prog, rj.Uniforms, &values)
            )

        let textureUniforms, otherUniforms = 
            prog.Uniforms |> List.partition (fun uniform -> match uniform.uniformType with | SamplerType -> true | _ -> false)

        let textures =
            textureUniforms
                |> List.choose (fun uniform ->
                    let tex = rj.Uniforms.TryGetUniform(rj.AttributeScope, uniform.semantic |> Symbol.Create)

                    match tex with
                        | Some value ->
                            match value with
                                | :? IMod<ITexture> as value ->
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

                                    let t = x.CreateTexture(value)
                                    let s = x.CreateSampler(Mod.constant sampler)

                                    Some (uniform.index, t, s)
                                | _ ->
                                    Log.warn "unexpected texture type %s: %A" uniform.semantic value
                                    None
                        | _ ->
                            Log.warn "texture %s not found" uniform.semantic
                            None
                    )

        let uniforms =
            otherUniforms
                |> List.map (fun uniform ->
                    let r = x.CreateUniformLocation(rj.AttributeScope, rj.Uniforms, uniform)
                    (uniform.location, r)
                )

        let buffers =
            prog.Inputs |> List.map (fun v ->
                match rj.VertexAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                    | Some value ->
                        let dep = x.CreateBuffer(value.Buffer)
                        let view = createView AttributeFrequency.PerVertex value dep
                        (v.attributeIndex, dep, view)
                    | _  -> 
                        match rj.InstanceAttributes with
                            | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" v.semantic rj
                            | _ -> 
                                printfn "looking up %s in instance attribs" v.semantic
                                match rj.InstanceAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                                    | Some value ->
                                        let dep = x.CreateBuffer(value.Buffer)
                                        let view = createView (AttributeFrequency.PerInstances 1) value dep
                                        (v.attributeIndex, dep, view)
                                    | _ -> 
                                        failwithf "could not get attribute %A" v.semantic
            )

        let index =
            if isNull rj.Indices then None
            else x.CreateBuffer rj.Indices |> Some

        let vao =
            x.CreateVertexArrayObject(buffers |> List.map (fun (i,_,a) -> (i,a)), index)

        {
            Context = x.Context
            Original = rj
            Program = program
            UniformBuffers = uniformBuffers
            Uniforms = uniforms
            Textures = textures
            Buffers = buffers
            IndexBuffer = index
            VertexArray = vao
        }
