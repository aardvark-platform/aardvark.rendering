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

            [<Semantic("CameraModel")>]
            cam : V3d

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
        member x.CellSize : int = uniform?CellSize

    let instanceTrafo (v : Vertex) =
        vertex {
            let p = (v.position.XYZ * v.offsetAndScale.W) + v.offsetAndScale.XYZ
            return 
                { v with 
                    cam = uniform.ModelTrafoInv.TransformPos(uniform.CameraLocation)
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
            
        if txmin > tymax || tymin > txmax then
            Nothing
        else
            let tmin = max txmin tymin
            let tmax = min txmax tymax

            if abs dir.Z < eps then
                tzmin <- -100000.0
                tzmax <- 100000.0

            if dir.Z > 0.0 then
                tzmin <- (box.min.Z - origin.Z) / dir.Z
                tzmax <- (box.max.Z - origin.Z) / dir.Z
            else
                tzmin <- (box.max.Z - origin.Z) / dir.Z
                tzmax <- (box.min.Z - origin.Z) / dir.Z

            if tmin > tzmax || tzmin > tmax  then
                Nothing
            else
                Just (max tmin tzmin, min tmax tzmax)

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
                v / abs v.X
            elif v.Z <> 0.0 then
                v / abs v.Z
            else
                V3d.Zero
        else
            if abs v.Y > abs v.Z then
                v / abs v.Y
            elif v.Z <> 0.0 then
                v / abs v.Z
            else
                V3d.Zero

    
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

    type UniformScope with
        member x.GridSize : V3i = uniform?GridSize
        member x.ShowPlanes : bool = uniform?ShowPlanes

    let intersectsSphere (center : V3d) (r : float) (p0 : V3d) (p1 : V3d) =
        let eps = 0.000001
        let h0 = Vec.length (p0 - center)
        let h1 = Vec.length (p1 - center)
        if h0 < r + eps && h1 > r - eps then -1
        elif h1 < r + eps && h0 >= r - eps then 1
        else 0

    let sampleVolume (pt : V3d) =
        let center = V3d(0.5, 0.5, 0.5)
        let r = 0.4
        let h = Vec.length (pt - center) - r
        
        let s = 0.01
        let alpha = (0.008 / sqrt (2.0 * Constant.Pi * s * s)) * exp (-h*h / (2.0*s*s))
        let c0 = V4d(pt * alpha, alpha)

        let center = V3d(0.55, 0.5, 0.5)
        let r = 0.2
        let h = Vec.length (pt - center) - r
        
        let s = 0.01
        let alpha = (0.008 / sqrt (2.0 * Constant.Pi * s * s)) * exp (-h*h / (2.0*s*s))
        let c1 = V4d(V3d.III * alpha, alpha)

        c0 + c1

    [<ReflectedDefinition>]
    let nextMultiple (a : float) (v : float) =
        let eps = 0.00001
        let m = v % a
        if m < eps || m > a - eps then
            v
        else
            v + (a - m)

    [<ReflectedDefinition>]
    let prevMultiple (a : float) (v : float) =
        let eps = 0.00001
        let m = v % a
        if m < eps || m > a - eps then
            v
        else
            v - m

    let sampleVolumeOfSize (size : V3d) (pt : V3d) =
        let invSize = 1.0 / size
        let px = pt * size - V3d(0.5, 0.5, 0.5)
        let p000 = V3d(floor px.X, floor px.Y, floor px.Z)
        let p100 = p000 + V3d.IOO
        let p010 = p000 + V3d.OIO
        let p110 = p000 + V3d.IIO
        let p001 = p000 + V3d.OOI
        let p101 = p000 + V3d.IOI
        let p011 = p000 + V3d.OII
        let p111 = p000 + V3d.III

        let v000 = sampleVolume ((p000 + V3d(0.5,0.5,0.5)) / size)
        let v100 = sampleVolume ((p100 + V3d(0.5,0.5,0.5)) / size)
        let v010 = sampleVolume ((p010 + V3d(0.5,0.5,0.5)) / size)
        let v110 = sampleVolume ((p110 + V3d(0.5,0.5,0.5)) / size)
        let v001 = sampleVolume ((p001 + V3d(0.5,0.5,0.5)) / size)
        let v101 = sampleVolume ((p101 + V3d(0.5,0.5,0.5)) / size)
        let v011 = sampleVolume ((p011 + V3d(0.5,0.5,0.5)) / size)
        let v111 = sampleVolume ((p111 + V3d(0.5,0.5,0.5)) / size)

        let t = px - p000

        let vx00 = v000 * (1.0 - t.X) + v100 * t.X
        let vx01 = v001 * (1.0 - t.X) + v101 * t.X
        let vx10 = v010 * (1.0 - t.X) + v110 * t.X
        let vx11 = v011 * (1.0 - t.X) + v111 * t.X
        let vxx0 = vx00 * (1.0 - t.Y) + vx10 * t.Y
        let vxx1 = vx01 * (1.0 - t.Y) + vx11 * t.Y
        vxx0 * (1.0 - t.Z) + vxx1 * t.Z
        //(sampleVolume p000 + p001 + p010 + p011 + p001 + p011 + p101 + p111) / 8.0


    let sampleVolumeOfSizeNearest (size : V3d) (pt : V3d) =
        let px = pt * size - V3d(0.5, 0.5, 0.5)

        let p = (V3d(round px.X, round px.Y, round px.Z) + V3d(0.5,0.5,0.5)) / size

        
        sampleVolume p
        

    [<ReflectedDefinition>]
    let compose (a : V4d) (b : V4d) =
        a + (1.0 - a.W) * b
        
    

    let march (v : Vertex) =
        fragment {
            let origin = v.cam
            let delta = v.objPos - origin
            let dir = max1 delta
            let l = Vec.length dir
            //let texit = Vec.length delta / Vec.length dir1
            
            
            //let a = hsv2rgb (texit / 30.0) 1.0 1.0

            //return V4d(a, 1.0) //V4d(0.1 * a.X, 0.1 * a.Y, 0.1 * a.Z, 0.1)

            
            let size = float uniform.CellSize
            //let invSize = 1.0 / size
            

            let overall = { min = V3d.Zero; max = V3d uniform.GridSize }
            let box = { min = v.offsetAndScale.XYZ; max = v.offsetAndScale.XYZ + v.offsetAndScale.W * V3d.III }
            let dt = 
                if size > 256.0 then 1.0 / 256.0
                else 1.0 / size

            match intersection origin dir overall with
                | Just (tmin, tmax) ->
                    match intersection origin dir box with
                        | Just (tenter, texit) ->
                            let tenter = max tenter tmin
                            let tenter = prevMultiple dt (tenter - tmin) + tmin + dt
                   
                            let planeColor = 
                                if uniform.ShowPlanes then V4d(0.02,0.0,0.0,0.02)
                                else V4d.Zero

                            let mutable t = tenter
                            let mutable color = planeColor
                            let mutable coord = (origin + t * dir) / V3d uniform.GridSize
                            let step = (dir * dt) / V3d uniform.GridSize


                            let mutable iter = 0
                            while t <= texit && iter < 512 do
                                let v = sampleVolumeOfSize (V3d uniform.GridSize * size) coord
                                
                                if v.W > 0.0 then
                                    // opacity correction
                                    let alpha = 1.0 - (1.0 - v.W) ** (l * dt)
                                    let v = V4d(v.XYZ * (alpha / v.W), alpha)

                                    // compose color
                                    color <- compose color v
                                    
                                t <- t + dt
                                coord <- coord + step
                                iter <- iter + 1

                            if iter = 512 then
                                color <- compose color V4d.IOOI
                        
                            color <- compose color planeColor








                            return color //V4d(hsv2rgb ((texit - tenter) / 10.0) 1.0 1.0, 1.0)



                            //return color

                        | Nothing ->
                            return V4d.OOII

                | Nothing ->
                    return V4d.OOOO
                    
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
            backend Backend.Vulkan
            display Display.Mono
            debug false
            samples 1
        }

    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red


    //let size = V3d(12.0,12.0,12.0)
    let cells = V3i(4,4,4)
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
    let cellSize = Mod.init 64

    win.Keyboard.KeyDown(Keys.Add).Values.Add(fun _ ->
        transact (fun () -> cellSize.Value <- 2 * cellSize.Value)
        Log.warn "cell size: %A" cellSize.Value
    )
    
    win.Keyboard.KeyDown(Keys.Subtract).Values.Add(fun _ ->
        transact (fun () -> cellSize.Value <- max 2 (cellSize.Value / 2))
        Log.warn "cell size: %A" cellSize.Value
    )

    win.Keyboard.KeyDown(Keys.F).Values.Subscribe(fun _ -> 
        transact (fun _ -> 
            match fillMode.Value with
                | FillMode.Fill -> fillMode.Value <- FillMode.Line
                | _ -> fillMode.Value <- FillMode.Fill
        )
    ) |> ignore
    
    let planes = Mod.init false
    win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
        transact (fun () ->
            planes.Value <- not planes.Value
        )
    )

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
          
    let pa = RenderPass.after "a" RenderPassOrder.Arbitrary RenderPass.main
    let pb = RenderPass.after "b" RenderPassOrder.Arbitrary pa

  

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
            |> Sg.pass pa
            |> Sg.transform trafo
            |> Sg.uniform "GridSize" (Mod.constant cells)
            |> Sg.uniform "ShowPlanes" planes
            |> Sg.uniform "CellSize" cellSize
            |> Sg.andAlso clear
            
    // show the window
    win.Scene <- sg
    win.Run()

    0
