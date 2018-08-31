open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module Shaders =
    open FShade


    type Vertex =
        {
            [<Position>]
            position : V4d

            //[<TexCoord>]  
            //texCoord : V3d

            [<Semantic("Offset")>]
            offset : V4d
        }

    type UniformScope with
        member x.PaddedTextureSize : V3d = uniform?PaddedTextureSize

    let instanceTrafo (v : Vertex) =
        vertex {
            let p = v.position.XYZ + v.offset.XYZ
            return 
                { v with 
                    position = V4d(p,1.0)
                    //texCoord = p * uniform.PaddedTextureSize + V3d(0.5,0.5,0.5)
                }
        }

    

let ofIndexedGeometry2 (instanceCount : int) (g : IndexedGeometry) =
    let attributes = 
        g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) -> 
            let t = v.GetType().GetElementType()
            let view = BufferView(Mod.constant (ArrayBuffer(v) :> IBuffer), t)

            k, view
        ) |> Map.ofSeq
        

    let index, faceVertexCount =
        if g.IsIndexed then
            g.IndexArray, g.IndexArray.Length
        else
            null, g.IndexedAttributes.[DefaultSemantic.Positions].Length
            
    let call = 
        DrawCallInfo(
            FaceVertexCount = faceVertexCount,
            FirstIndex = 0,
            InstanceCount = instanceCount,
            FirstInstance = 0,
            BaseVertex = 0
        )
    let sg = Sg.VertexAttributeApplicator(attributes, Sg.RenderNode(call,g.Mode)) :> ISg
    if not (isNull index) then
        Sg.VertexIndexApplicator(BufferView.ofArray index, sg) :> ISg
    else
        sg



[<EntryPoint>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    // window { ... } is similar to show { ... } but instead
    // of directly showing the window we get the window-instance
    // and may show it later.
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug true
            samples 8
        }

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    let totalSize = V3d(12,13,11)
    let invSize = 1.0 / totalSize
    let tileSize = V3d(4,4,4)
    let count = V3i(ceil (float totalSize.X / float tileSize.X), ceil (float totalSize.Y / float tileSize.Y), ceil (float totalSize.Z / float tileSize.Z))
    let fullSize = V3d count * tileSize
    let step = tileSize / fullSize
    let boxes =
        [|
            for x in 0 .. count.X - 1 do
                for y in 0 .. count.Y - 1 do
                    for z in 0 .. count.Z - 1 do
                        let coord = V3d(x,y,z)
                        let shift = coord * step
                        yield V3f shift //, (shift * fullSize + V3d(0.5,0.5,0.5))
        |]

    let fillMode = Mod.init FillMode.Fill

    win.Keyboard.KeyDown(Keys.F).Values.Subscribe(fun _ -> 
        transact (fun _ -> 
            match fillMode.Value with
                | FillMode.Fill -> fillMode.Value <- FillMode.Line
                | _ -> fillMode.Value <- FillMode.Fill
        )
    ) |> ignore

    let box = ofIndexedGeometry2 boxes.Length (IndexedGeometryPrimitives.Box.solidBox Box3d.Unit C4b.White)

    let sg = 
        // create a red box with a simple shader
        box
            |> Sg.instanceArray (Sym.ofString "Offset") boxes
            |> Sg.uniform "PaddedTextureSize" (Mod.constant fullSize)
            |> Sg.fillMode fillMode
            |> Sg.shader {
                do! Shaders.instanceTrafo
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
            }
    
    // show the window
    win.Scene <- sg
    win.Run()

    0
