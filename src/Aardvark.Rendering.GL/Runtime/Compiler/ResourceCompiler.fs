namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
//
//module Resources =
//    let private createAndAddResource (f : ResourceManager -> IResource<'a>) =
//        { runCompile = fun s ->
//            s.resourceCreateTime.Start()
//            let r = f s.manager
//            s.resourceCreateTime.Stop()
//            { s with resources = (r:> IResource)::s.resources }, r
//        }
//
//    let createProgram surface =
//        createAndAddResource (fun m -> m.CreateSurface(surface))
//
//    let createUniformBuffer scope block surface provider =
//        { runCompile = fun s ->
//            s.resourceCreateTime.Start()
//            let r = s.manager.CreateUniformBuffer(scope, block, surface, provider)
//            s.resourceCreateTime.Stop()
//            {s with resources = (r:> IResource)::s.resources}, r
//        }
//
//    let createUniformLocation scope u provider =
//        { runCompile = fun s ->
//            let mutable values = []
//            s.resourceCreateTime.Start()
//            let r = s.manager.CreateUniformLocation(scope, provider, u)
//            s.resourceCreateTime.Stop()
//            {s with resources = (r:> IResource)::s.resources}, (r,values)
//        }
//
//    let createTexture texture =
//        createAndAddResource (fun m -> m.CreateTexture texture)
//
//    let createSampler sampler =
//        createAndAddResource (fun m -> m.CreateSampler sampler)
//
//    let createVertexArrayObject (program : Program) (rj : RenderObject) =
//        { runCompile = fun s -> 
//            let manager = s.manager
//            s.resourceCreateTime.Start()
//
//            let buffers = System.Collections.Generic.List<IResource>()
//            let bindings = System.Collections.Generic.List()
//            for v in program.Inputs do
//                match rj.VertexAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
//                    | Some value ->
//                        let dep = manager.CreateBuffer(value.Buffer)
//                        buffers.Add dep
//                        bindings.Add(v.attributeIndex, value, AttributeFrequency.PerVertex, dep)
//                    | _  -> 
//                        match rj.InstanceAttributes with
//                            | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" v.semantic rj
//                            | _ -> 
//                                printfn "looking up %s in instance attribs" v.semantic
//                                match rj.InstanceAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
//                                    | Some value ->
//                                        let dep = manager.CreateBuffer(value.Buffer)
//                                        buffers.Add dep
//                                        bindings.Add(v.attributeIndex, value, (AttributeFrequency.PerInstances 1), dep)
//                                    | _ -> failwithf "could not get attribute %A" v.semantic
//
//            //create the index-buffer if desired
//            let index = ref None
//            if rj.Indices <> null then
//                let dep = manager.CreateBuffer(rj.Indices)
//                buffers.Add dep
//                index := Some dep
//
//
//            let bindings = bindings |> Seq.toList
//            //create a vertex-array-object
//            let vao = manager.CreateVertexArrayObject(bindings, !index)
//
//            let newResources = (vao :> IChangeableResource)::(buffers |> Seq.toList)
//
//            s.resourceCreateTime.Stop()
//
//            { s with resources = List.append newResources s.resources }, vao
//        }
//
//    let runLocal (c : Compiled<'a>) =
//        { runCompile = fun s ->
//            let (rs,rv) = c.runCompile s
//            (s,rv)
//        }
