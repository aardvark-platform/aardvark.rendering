open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Shader =
    open FShade
    open Aardvark.Base.Rendering.Effects

    type InstanceVertex = { 
        [<Position>]            pos   : V4d 
        [<Normal>]              n     : V3d 
        [<BiNormal>]            b     : V3d 
        [<Tangent>]             t     : V3d 
        [<InstanceTrafo>]       trafo : M44d
    }

    let orthoInstanceTrafo (v : InstanceVertex) =
        vertex {
            return 
                { v with 
                    pos = v.trafo * v.pos 
                    n = v.trafo.TransformDir(v.n)
                    b = v.trafo.TransformDir(v.b)
                    t = v.trafo.TransformDir(v.t)
                }
        }

    type Vertex = {
        [<Position>]        pos : V4d
        [<Color>]           c   : V4d
        [<Normal>]          n   : V3d
    }

    [<GLSLIntrinsic("gl_DrawIDARB",requiredExtensions=[|"GL_ARB_shader_draw_parameters"|])>]
    let drawId () : int = raise <| FShade.Imperative.FShadeOnlyInShaderCodeException "drawId"

    type UniformScope with
        member x.ObjectColors : V4d[] = x?StorageBuffer?ObjectColors
        member x.MeshTrafo : M44d[] = x?StorageBuffer?MeshTrafo
    
    let objectColor (v : Vertex) =
        vertex {
            let id = drawId()
            return 
                { v with 
                    c = uniform.ObjectColors.[id]; 
                    pos = uniform.MeshTrafo.[id] * v.pos  
                    n = uniform.MeshTrafo.[id].TransformDir v.n
                }
        }

module Packing = 

    open System.Collections.Generic

    type GeometryRange = { firstIndex : int; baseVertex : int; faceVertexCount : int }
    type PackedGeometry = 
        {
            vertices : array<V3f>
            normals  : array<V3f>
            indices  : array<int>
            ranges   : array<GeometryRange>
        }

    let pack (geometries : seq<IndexedGeometry>) =
        let packedVertices = List<V3f>()
        let packedNormals = List<V3f>()
        let packedIndices = List<int>()
        let ranges = 
            [|
                for g in geometries do
                    assert (g.Mode = IndexedGeometryMode.TriangleList)
                    let firstIndex = packedIndices.Count
                    let baseVertex = packedVertices.Count
                    let indexed = g.ToIndexed()
                    let faceVertexCount = indexed.FaceVertexCount
                    packedVertices.AddRange(indexed.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>)
                    packedNormals.AddRange(indexed.IndexedAttributes.[DefaultSemantic.Normals] |> unbox<V3f[]>)
                    packedIndices.AddRange(indexed.IndexArray |> unbox<int[]>)
                    yield { firstIndex = firstIndex; baseVertex = baseVertex; faceVertexCount = faceVertexCount }
            |]
        { vertices = packedVertices.ToArray(); normals = packedNormals.ToArray(); 
          indices = packedIndices.ToArray(); ranges = ranges }
            
[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    let torus  = IndexedGeometryPrimitives.solidTorus (Torus3d(V3d.Zero,V3d.OOI,1.0,0.2)) C4b.Green 20 20
    let sphere = IndexedGeometryPrimitives.solidPhiThetaSphere (Sphere3d.FromRadius(0.1)) 20 C4b.White

    let packedGeometry = 
        [ torus; sphere ] |> Packing.pack

    
    let vertices = BufferView(ArrayBuffer(packedGeometry.vertices) :> IBuffer |> Mod.constant,typeof<V3f>)
    let normals = BufferView(ArrayBuffer(packedGeometry.normals) :> IBuffer |> Mod.constant,typeof<V3f>)

    let size = 7
    let trafos =
        [|
            for x in -size .. size do
                for y in -size .. size do
                    for z in -size .. size do
                        yield Trafo3d.Scale(0.3) * Trafo3d.Translation(float x, float y, float z)
        |]
    let drawCallInfos = 
        [| 
            for r in packedGeometry.ranges do 
                yield DrawCallInfo(FaceVertexCount = r.faceVertexCount,
                                   BaseVertex = r.baseVertex, FirstIndex = r.firstIndex,
                                   FirstInstance = 0, InstanceCount = trafos.Length)
        |]


    let indirectBuffer = IndirectBuffer(ArrayBuffer drawCallInfos, drawCallInfos.Length) :> IIndirectBuffer

    let objectColors = Mod.init [| C4f.Red; C4f.DarkGreen |]

    let perKindAnimation = 
        let sw = System.Diagnostics.Stopwatch.StartNew()
        win.Time |> Mod.map (fun t -> 
            let t = sw.Elapsed.TotalMilliseconds
            [| M44f.RotationX(float32 (t * 0.001)); M44f.Identity |]
        )

    let sg = 
        Sg.indirectDraw IndexedGeometryMode.TriangleList (Mod.constant indirectBuffer)
        |> Sg.vertexBuffer DefaultSemantic.Positions vertices
        |> Sg.vertexBuffer DefaultSemantic.Normals   normals
        |> Sg.uniform "ObjectColors" objectColors
        |> Sg.uniform "MeshTrafo"    perKindAnimation
        |> Sg.index' packedGeometry.indices
        |> Sg.instanceArray DefaultSemantic.InstanceTrafo (trafos |> Array.map (fun t -> t.Forward |> M44f.op_Explicit))
        |> Sg.shader {
            do! Shader.objectColor
            do! Shader.orthoInstanceTrafo
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.simpleLighting
        }

    win.Scene <- sg
    win.Run()

    0
