open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

[<ReflectedDefinition>]
module Shaders =
    open FShade


    type Vertex =
        {
            [<Position>]
            position : V4d

            [<Semantic("ObjectSpacePos")>]
            objPos : V3d
            

            //[<TexCoord>]  
            //texCoord : V3d

            [<Semantic("Offset")>]
            offsetAndScale : V4d

            [<Semantic("BoxSize")>]
            boxSize : float

            [<Semantic("Level")>]
            level : int

            [<InstanceId;Interpolation(InterpolationMode.Flat)>]
            id : int
        }

    type UniformScope with
        member x.PaddedTextureSize : V3d = uniform?PaddedTextureSize

    let instanceTrafo (v : Vertex) =
        vertex {
            let p = (v.position.XYZ * v.offsetAndScale.W) + v.offsetAndScale.XYZ
            return 
                { v with 
                    objPos = p
                    position = V4d(p,1.0)
                    boxSize = v.offsetAndScale.W
                    level = Fun.Log2 v.offsetAndScale.W |> round |> int
                    //texCoord = p * uniform.PaddedTextureSize + V3d(0.5,0.5,0.5)
                }
        }

    type Box = { min : V3d; max : V3d }

    type Maybe<'a> =
        | Nothing
        | Just of 'a

    let eps = 0.0001

    let contains (p : V3d) (box : Box) =
        p.X >= box.min.X - eps && p.X <= box.max.X + eps && p.Y >= box.min.Y - eps && p.Y <= box.max.Y + eps && p.Z >= box.min.Z - eps && p.Z <= box.max.Z + eps

    let containsYZ (p : V3d) (box : Box) =
        p.Y >= box.min.Y - eps && p.Y <= box.max.Y + eps && p.Z >= box.min.Z - eps && p.Z <= box.max.Z + eps

    let containsXY (p : V3d) (box : Box) =
        p.X >= box.min.X - eps && p.X <= box.max.X + eps && p.Y >= box.min.Y - eps && p.Y <= box.max.Y + eps

    let containsXZ (p : V3d) (box : Box) =
        p.X >= box.min.X - eps && p.X <= box.max.X + eps && p.Z >= box.min.Z - eps && p.Z <= box.max.Z + eps

    let intersection (origin : V3d) (dir : V3d) (box : Box) =
        // tlx = (bbmin.X - o.X) / d.X

        // contained (box, o + d * t)

        let mutable cnt = 0
        let mutable tenter = 100000.0
        let mutable texit = -100000.0

        let mutable txmin = 0.0
        let mutable txmax = 0.0
        
        let mutable tymin = 0.0
        let mutable tymax = 0.0
        
        let mutable tzmin = 0.0
        let mutable tzmax = 0.0

        //let divx = 1.0 / dir.X

        let eps = 0.00000001
        if abs dir.X < eps then
            txmin <- -100000.0
            txmax <- 100000.0

        elif dir.X > 0.0 then
            txmin <- (box.min.X - origin.X) / dir.X
            txmax <- (box.max.X - origin.X) / dir.X

        else
            txmin <- (box.max.X - origin.X) / dir.X
            txmax <- (box.min.X - origin.X) / dir.X
            
        if abs dir.Y < eps then
            tymin <- -100000.0
            tymax <- 100000.0

        elif dir.Y > 0.0 then
            tymin <- (box.min.Y - origin.Y) / dir.Y
            tymax <- (box.max.Y - origin.Y) / dir.Y
        else
            tymin <- (box.max.Y - origin.Y) / dir.Y
            tymax <- (box.min.Y - origin.Y) / dir.Y
            
            
        if abs dir.Z < eps then
            tzmin <- -100000.0
            tzmax <- 100000.0

        if dir.Z > 0.0 then
            tzmin <- (box.min.Z - origin.Z) / dir.Z
            tzmax <- (box.max.Z - origin.Z) / dir.Z
        else
            tzmin <- (box.max.Z - origin.Z) / dir.Z
            tzmax <- (box.min.Z - origin.Z) / dir.Z

        //if txmin > tymax || tymin > txmax || txmin > tzmax || tzmin > txmax then
        //    Nothing
        //else
        Just (max (max txmin tymin) tzmin, min (min txmax tymax) tzmax)

        //if abs dir.X >= eps then
        //    let t = (box.min.X - origin.X) / dir.X
        //    if containsYZ (origin + t * dir) box then
        //        if t < tenter then tenter <- t
        //        if t > texit then texit <- t

        //    let t = (box.max.X - origin.X) / dir.X
        //    if containsYZ (origin + t * dir) box then
        //        if t < tenter then tenter <- t
        //        if t > texit then texit <- t

        //if abs dir.Y >= eps then
        //    let t = (box.min.Y - origin.Y) / dir.Y
        //    if containsXZ (origin + t * dir) box then
        //        if t < tenter then tenter <- t
        //        if t > texit then texit <- t

        //    let t = (box.max.Y - origin.Y) / dir.Y
        //    if containsXZ (origin + t * dir) box then
        //        if t < tenter then tenter <- t
        //        if t > texit then texit <- t

        //if abs dir.Z >= eps then
        //    let t = (box.min.Z - origin.Z) / dir.Z
        //    if containsXY (origin + t * dir) box then
        //        if t < tenter then tenter <- t
        //        if t > texit then texit <- t

        //    let t = (box.max.Z - origin.Z) / dir.Z
        //    if containsXY (origin + t * dir) box then
        //        if t < tenter then tenter <- t
        //        if t > texit then texit <- t

        //if tenter <= texit then
        //    Just (tenter, texit)
        //else
        //    Nothing


    let max1 (v : V3d) =
        if abs v.X > abs v.Y then
            if abs v.X > abs v.Z then
                v / v.X
            else
                v / v.Z
        else
            if abs v.Y > abs v.Z then
                v / v.Y
            else
                v / v.Z

    
    [<ReflectedDefinition>]
    let hsv2rgb (h : float) (s : float) (v : float) =
        let s = clamp 0.0 1.0 s
        let v = clamp 0.0 1.0 v

        let h = h % 1.0
        let h = if h < 0.0 then h + 1.0 else h
        let hi = floor ( h * 6.0 ) |> int
        let f = h * 6.0 - float hi
        let p = v * (1.0 - s)
        let q = v * (1.0 - s * f)
        let t = v * (1.0 - s * ( 1.0 - f ))
        match hi with
            | 1 -> V3d(q,v,p)
            | 2 -> V3d(p,v,t)
            | 3 -> V3d(p,q,v)
            | 4 -> V3d(t,p,v)
            | 5 -> V3d(v,p,q)
            | _ -> V3d(v,t,p)

    let intersectsSphere (center : V3d) (r : float) (p0 : V3d) (p1 : V3d) =
        let eps = 0.000001
        let h0 = Vec.length (p0 - center)
        let h1 = Vec.length (p1 - center)
        if h0 < r + eps && h1 > r - eps then -1
        elif h1 < r + eps && h0 >= r - eps then 1
        else 0

    [<ReflectedDefinition>]
    let compose (a : V4d) (b : V4d) =
        a + (1.0 - a.W) * b

    type UniformScope with
        member x.GridSize : V3i = uniform?GridSize

    let march (v : Vertex) =
        fragment {
            let origin = uniform.ModelViewProjTrafoInv * V4d(0.0, 0.0, -1000000.0, 1.0)
            let origin = origin.XYZ / origin.W
            
            let delta = v.objPos - origin
            let dir1 = Vec.normalize delta

            //let texit = Vec.length delta / Vec.length dir1
            
            
            //let a = hsv2rgb (texit / 30.0) 1.0 1.0

            //return V4d(a, 1.0) //V4d(0.1 * a.X, 0.1 * a.Y, 0.1 * a.Z, 0.1)

            
            let delta = V3d.Zero // V3d(0.05, 0.05, 0.05)

            let mutable cnt = 0
            let box = { min = v.offsetAndScale.XYZ + delta; max = v.offsetAndScale.XYZ + v.offsetAndScale.W * V3d.III - 2.0 * delta }
            match intersection origin dir1 box with
                | Just (tenter, texit) ->
                    
                    let step = dir1
                    let mutable t = tenter
                    
                    let dt =0.07//0.1

                    let len = texit - tenter

                    let eps = 0.00001

                    let m = t % dt
                    if m <= eps || m >= dt - eps then
                        t <- t + dt
                    else
                        t <- t + (dt - m)
                    

                    let mutable color = V4d.Zero
                    let mutable coord = (origin + t * dir1) / V3d uniform.GridSize
                    let step = (dir1 * dt) / V3d uniform.GridSize
                    let mutable lastCoord = coord - step


                    let mutable iter = 0
                    while t <= texit && iter < 512 do
                        
                        let v = intersectsSphere (V3d(0.5,0.5,0.5)) 0.4 lastCoord coord
                        if v > 0 then
                            cnt <- cnt + 1
                            ()
                            color <- compose color (V4d(0.0, 1.0, 0.0, 0.2))
                            //color <- color + (1.0 - color.W) * (V4d.OIOI * 0.2)
                        elif v < 0 then
                            cnt <- cnt + 1
                            color <- compose color (V4d(1.0, 0.0, 0.0, 0.2))
                            //color <- color + (1.0 - color.W) * (V4d.IOOI * 0.2)
                        //if intersectsSphere (V3d(0.5,0.5,0.5)) 0.2 lastCoord coord then
                        //    color <- color + (1.0 - color.W) * (V4d.OIOI * 0.2)

                        t <- t + dt
                        lastCoord <- coord
                        coord <- coord + step
                        iter <- iter + 1

                    if iter = 512 then
                        color <- V4d.IOOI

                    if cnt > 2 then
                        color <- V4d(0.0, 0.0, 1.0, 1.0)









                    return V4d(hsv2rgb ((texit - tenter) / 10.0) 1.0 1.0, 1.0)



                    //return color

                | Nothing ->
                    return V4d.OOII
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


    //let size = V3d(12.0,12.0,12.0)
    let cells = V3i(12,7,4)
    let worldSize = V3d(3.0, 3.0, 3.0)





    //let totalSize = V3d(12,13,11)
    //let invSize = 1.0 / totalSize
    //let tileSize = V3d(4,4,4)
    //let count = V3i(ceil (float totalSize.X / float tileSize.X), ceil (float totalSize.Y / float tileSize.Y), ceil (float totalSize.Z / float tileSize.Z))
    //let fullSize = V3d count * tileSize
    //let step = tileSize / fullSize

    let trafo = Trafo3d.Scale(worldSize / V3d cells) * Trafo3d.Translation(-worldSize / 2.0)
   
    let boxes =
        [|
            for x in 0 .. cells.X - 1 do
                for y in 0 .. cells.Y - 1 do
                    for z in 0 .. cells.Z - 1 do
                        let coord = V3d(x,y,z)
                        yield V4f(V3f coord, 1.0f) //, (shift * fullSize + V3d(0.5,0.5,0.5))
        |]

    let sorted = 
        win.View |> Mod.map (fun vs -> 
            let cam = trafo.Backward.TransformPos (vs.[0].Backward.TransformPos(V3d.Zero))
            let b = trafo.Backward.TransformPos (vs.[0].Backward.TransformPos(-V3d.OOI))
            let fw = Vec.normalize (b - cam)



            boxes |> Array.sortBy (fun min -> 
                let c = Box3d.FromMinAndSize(V3d min.XYZ, V3d.III * float min.W).Center
                
                Vec.length (c - cam)
            ) |> ArrayBuffer :> IBuffer

        )

    let fillMode = Mod.init FillMode.Fill

    win.Keyboard.KeyDown(Keys.F).Values.Subscribe(fun _ -> 
        transact (fun _ -> 
            match fillMode.Value with
                | FillMode.Fill -> fillMode.Value <- FillMode.Line
                | _ -> fillMode.Value <- FillMode.Fill
        )
    ) |> ignore

    let box = ofIndexedGeometry2 boxes.Length Primitives.unitBox

    let buffer = BufferView(sorted,typeof<V4f>)

    let mutable blendMode = BlendMode(true)
    blendMode.AlphaOperation <- BlendOperation.Add
    blendMode.Operation <- BlendOperation.Add
    blendMode.SourceFactor <- BlendFactor.InvDestinationAlpha
    blendMode.SourceAlphaFactor <- BlendFactor.InvDestinationAlpha
    blendMode.DestinationFactor <- BlendFactor.One
    blendMode.DestinationAlphaFactor <- BlendFactor.One


    let clear =
        Sg.fullScreenQuad
            |> Sg.shader {
                do! DefaultSurfaces.constantColor (C4f(0.0,0.0,0.0,0.0))
            }
            |> Sg.depthTest (Mod.constant DepthTestMode.None)
            
    let sg = 
        // create a red box with a simple shader
        box
            |> Sg.instanceBuffer (Sym.ofString "Offset") buffer
            //|> Sg.uniform "PaddedTextureSize" (Mod.constant fullSize)
            |> Sg.fillMode fillMode
            |> Sg.shader {
                do! Shaders.instanceTrafo
                do! DefaultSurfaces.trafo
                //do! DefaultSurfaces.constantColor (C4f(0.1,0.0,0.0,0.1))
                do! Shaders.march
            }
            |> Sg.blendMode (Mod.constant blendMode)
            |> Sg.cullMode (Mod.constant CullMode.CounterClockwise)
            |> Sg.depthTest (Mod.constant DepthTestMode.None)
            |> Sg.pass (RenderPass.after "a" RenderPassOrder.Arbitrary RenderPass.main)
            |> Sg.transform trafo
            |> Sg.uniform "GridSize" (Mod.constant cells)
            |> Sg.andAlso clear

    // show the window
    win.Scene <- sg
    win.Run()

    0
