namespace Aardvark.Rendering.GL

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering

module Resources =
    let private createAndAddResource (f : ResourceManager -> ChangeableResource<'a>) =
        { runCompile = fun s ->
            let r = f s.manager
            { s with resources = (r:> IChangeableResource)::s.resources }, r
        }

    let createProgram surface =
        createAndAddResource (fun m -> m.CreateSurface(surface))

    let createUniformBuffer scope block surface provider =
        { runCompile = fun s ->
            let mutable values = []
            let r = s.manager.CreateUniformBuffer(scope, block, surface, provider, &values)
            {s with resources = (r:> IChangeableResource)::s.resources}, (r,values)
        }

    let createUniformLocation scope u provider =
        { runCompile = fun s ->
            let mutable values = []
            let r = s.manager.CreateUniformLocation(scope, provider, u)
            {s with resources = (r:> IChangeableResource)::s.resources}, (r,values)
        }

    let createTexture texture =
        createAndAddResource (fun m -> m.CreateTexture texture)

    let createSampler sampler =
        createAndAddResource (fun m -> m.CreateSampler sampler)

    let private viewCache = System.Collections.Concurrent.ConcurrentDictionary<BufferView * ChangeableResource<Aardvark.Rendering.GL.Buffer>, IMod<AttributeDescription>>()

    let private createView (frequency : AttributeFrequency) (m : BufferView) (b : ChangeableResource<Aardvark.Rendering.GL.Buffer>) =
        viewCache.GetOrAdd(((m,b)), (fun (m,b) ->
            b.Resource |> Mod.map (fun b ->
                { Type = m.ElementType; Frequency = frequency; Normalized = false; Stride = m.Stride; Offset = m.Offset; Buffer = b }
            )
        ))

    let createVertexArrayObject (program : Program) (rj : RenderJob) =
        { runCompile = fun s -> 
            let manager = s.manager

            let buffers = System.Collections.Generic.List<IChangeableResource>()
            let bindings = System.Collections.Generic.Dictionary()
            for v in program.Inputs do
                match rj.VertexAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                    | Some value ->
                        let dep = manager.CreateBuffer(value.Buffer)
                        buffers.Add dep
                        bindings.[v.attributeIndex] <- createView AttributeFrequency.PerVertex value dep
                    | _  -> 
                        match rj.InstanceAttributes with
                            | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" v.semantic rj
                            | _ -> 
                                printfn "looking up %s in instance attribs" v.semantic
                                match rj.InstanceAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                                    | Some value ->
                                        let dep = manager.CreateBuffer(value.Buffer)
                                        buffers.Add dep
                                        bindings.[v.attributeIndex] <- createView (AttributeFrequency.PerInstances 1) value dep
                                    | _ -> failwithf "could not get attribute %A" v.semantic

            //create the index-buffer if desired
            let index = ref None
            if rj.Indices <> null then
                let dep = manager.CreateBuffer(rj.Indices)
                buffers.Add dep
                index := Some dep


            let bindings = bindings |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Seq.toList
            //create a vertex-array-object
            let vao = manager.CreateVertexArrayObject(bindings, !index)

            let newResources = (vao :> IChangeableResource)::(buffers |> Seq.toList)

            { s with resources = List.append newResources s.resources }, vao
        }

    let runLocal (c : Compiled<'a>) =
        { runCompile = fun s ->
            let (rs,rv) = c.runCompile s
            (s,rv)
        }
