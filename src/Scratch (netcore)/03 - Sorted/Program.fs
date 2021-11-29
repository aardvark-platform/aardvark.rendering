open Aardvark.Base

open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

[<ReflectedDefinition>]
module Shaders =
    open FShade


    type Vertex =
        {
            [<Position>]
            position : V4d

            [<Semantic("ObjectSpacePos"); Interpolation(InterpolationMode.Centroid)>]
            objPos : V3d
            
            //[<TexCoord>]  
            //texCoord : V3d

            [<Semantic("CameraModel"); Interpolation(InterpolationMode.Flat)>]
            cam : V3d

            [<Semantic("Offset"); Interpolation(InterpolationMode.Flat)>]
            offsetAndScale : V4d

            [<Semantic("BoxSize"); Interpolation(InterpolationMode.Flat)>]
            boxSize : float

            [<Semantic("Level"); Interpolation(InterpolationMode.Flat)>]
            level : int

            [<InstanceId;Interpolation(InterpolationMode.Flat)>]
            id : int
        }

    type UniformScope with
        member x.PaddedTextureSize : V3d = uniform?PaddedTextureSize
        member x.CellSize : int = uniform?CellSize
        member x.Magic : float = uniform?Magic

    let instanceTrafo (v : Vertex) =
        vertex {
            let p = (v.position.XYZ * v.offsetAndScale.W) + v.offsetAndScale.XYZ
            let cam = uniform.ModelTrafoInv.TransformPos(uniform.CameraLocation)
            return 
                { v with 
                    cam = cam
                    objPos = p
                    position = V4d(p,1.0)
                    boxSize = v.offsetAndScale.W
                    level = Fun.Log2 v.offsetAndScale.W |> round |> int
                    //texCoord = p * uniform.PaddedTextureSize + V3d(0.5,0.5,0.5)
                }
        }

    type Box = { min : V3d; max : V3d }

    let intersectsBox (origin : V3d) (invDir : V3d) (box : Box) (tmin : ref<float>) (tmax : ref<float>) =
        let mutable temp = Unchecked.defaultof<_>
        let mutable t0 = (box.min - origin) * invDir
        let mutable t1 = (box.max - origin) * invDir

        let eps = 0.05

        if invDir.X < 0.0 then temp <- t0.X; t0.X <- t1.X; t1.X <- temp
        if invDir.Y < 0.0 then temp <- t0.Y; t0.Y <- t1.Y; t1.Y <- temp
         
        if (t0.X > t1.Y + eps || t0.Y > t1.X + eps) then
            false
        else
            if invDir.Z < 0.0 then temp <- t0.Z; t0.Z <- t1.Z; t1.Z <- temp

            t0.X <- max t0.X t0.Y
            t1.X <- min t1.X t1.Y
            
            if (t0.X > t1.Z + eps || t0.Z > t1.X + eps) then
                false
            else
                tmin := max t0.X (max t0.Y t0.Z)
                tmax := min t1.X (min t1.Y t1.Z)

                

                true


    //bool intersectsOrContainsMinMax(float4 boxMin, float4 boxMax, Ray ray, float4 invDir, float4 invDirSign, float minT, float maxT, float2* rayRange)
    //{

      // //faster than ifs (crazy shit)
      // float4 oneMinusInvDirSign = (float4)(1,1,1,1) - invDirSign;
      // float4 tmin = (boxMin * oneMinusInvDirSign + boxMax * invDirSign - ray.Origin) * invDir;
      // float4 tmax = (boxMin * invDirSign + boxMax * oneMinusInvDirSign - ray.Origin) * invDir;
      
      // if (tmin.x > tmax.y || tmin.y > tmax.x) return false;
      
      // if (tmin.y > tmin.y) tmin.x = tmin.y;
      // if (tmax.y < tmax.x) tmax.x = tmax.y;
      
      // if (tmin.x > tmax.z || tmin.z > tmax.x) return false;
      // if (tmin.z > tmin.x) tmin.x = tmin.z;
      // if (tmax.z < tmax.x) tmax.x = tmax.z;
      // if(tmin.x > maxT || tmax.x < minT) return false;
      
      
      // tmin.x = max(tmin.x, minT);
      // tmax.x = min(tmax.x, maxT);
      
      // *rayRange = (float2)(tmin.x, tmax.x);
      
      // return true;
    //}

    [<Inline>]
    let sgn (v : float) = if v < 0.0 then 1.0 else 0.0

    let isNonZero (v : float) = abs v >= 0.01

    let intersectsBox2 (origin : V3d) (dir : V3d) (box : Box) (tmin : ref<float>) (tmax : ref<float>) =
        let len = Vec.length dir

        let mutable result = true
        let mutable l = -10000000.0
        let mutable h =  10000000.0

        let mutable t0 = (box.min - origin) / (dir / len)
        let mutable t1 = (box.max - origin) / (dir / len)
        let mutable temp = 0.0
        
        if dir.X < 0.0 then temp <- t0.X; t0.X <- t1.X; t1.X <- temp
        if dir.Y < 0.0 then temp <- t0.Y; t0.Y <- t1.Y; t1.Y <- temp
        if dir.Z < 0.0 then temp <- t0.Z; t0.Z <- t1.Z; t1.Z <- temp
        
        if isNonZero dir.X then
            //if t0.X > h || t1.X < l then result <- false
            l <- max l t0.X
            h <- min h t1.X
            
        if isNonZero dir.Y then
            //if t0.Y > h || t1.Y < l then result <- false
            l <- max l t0.Y
            h <- min h t1.Y
            
        if isNonZero dir.Z then
            //if t0.Z > h || t1.Z < l then result <- false
            l <- max l t0.Z
            h <- min h t1.Z
              
        if l <= h then
            tmin := l / len
            tmax := h / len
            true
        //elif l <= h + 0.002 then
        //    let t = (l + h) / 2.0
        //    tmin := t / len
        //    tmax := t / len
        //    true
        else
            false

    let max1 (v : V3d) =
        let a = v.Abs()
        v / (max a.X (max a.Y a.Z))
           
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
        member x.DebugPlane : V4d = uniform?DebugPlane


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
    
    let sampleVolumeOfSizeNearest (size : V3d) (pt : V3d) =
        let px = pt * size - V3d(0.5, 0.5, 0.5)

        let p = (V3d(round px.X, round px.Y, round px.Z) + V3d(0.5,0.5,0.5)) / size

        sampleVolume p
        
    [<ReflectedDefinition>]
    let compose (a : V4d) (b : V4d) =
        a + (1.0 - a.W) * b
    
    let sampleSphere (p0 : V3d) (p1 : V3d) =
        let c = V3d(0.5, 0.5, 0.5)
        let r = 0.4
        let o = p0 - c
        let d = p1 - p0

        // a = p0 - c
        // b = p1 - p0
        // length(a + t * b) = r

        // (ax + t*bx)^2 + (ay + t*by)^2 + (az + t*bz)^2 - r^2 = 0
        // ax^2 + 2*ax*t*bx + t^2*bx^2 + 
        // ay^2 + 2*ay*t*by + t^2*by^2 + 
        // az^2 + 2*az*t*by + t^2*bz^2 -
        // r^2 = 0


        // t^2  * (bx^2 + by^2 + bz^2) +
        // t    * (2*ax*bx + 2*ay*by + 2*az*by) +
        //        (ax^2 + ay^2 + az^2 - r^2)

        // t = (-b +/- sqrt(b^2 - 4*a*c)) / 2a

        let a = Vec.lengthSquared d
        let b = 2.0 * Vec.dot o d
        let c = Vec.lengthSquared o - r*r

        let v = b*b - 4.0*a*c
        if v >= 0.0 then
            let s = sqrt v
            let mutable t0 = (-b + s) / (2.0 * a)
            let mutable t1 = (-b - s) / (2.0 * a)

            let mutable color = V4d.Zero

            if t1 < t0 then
                let t = t0
                t0 <- t1
                t1 <- t
                
            let l = 0.0
            let h =  1.0
            if t0 >= l && t0 < h then
                let n = Vec.normalize (o + t0 * d)
                let d = Vec.normalize d
                let diffuse = Vec.dot -n d |> abs
                color <- compose color (V4d(V3d.IOO * diffuse, 1.0) * 0.5)

            if t1 >= l && t1 < h then
                let n = Vec.normalize (o + t0 * d)
                let d = Vec.normalize d
                let diffuse = Vec.dot n d |> abs
                color <- compose color (V4d(V3d.OIO * diffuse, 1.0) * 0.5)

            color
        else
            V4d.Zero

        //if h0 < 0.0 && h1 >= 0.0 then
        //    V4d.IOOI * 0.2
        //elif h0 >= 0.0 && h1 < 0.0 then
        //    V4d.OIOI * 0.2
        //else
        //    V4d.Zero

        
    let normalizeRelative (dir : V3d) (v : V3d) =
        //let ld = Vec.length dir
        //let lv = Vec.length v
        //let c = Vec.dot (dir / ld) (v / lv)
        //(v / lv) * c * ld
        
        let ld = dir.Length
        let lv = v.Length
        let c = Vec.Dot(dir / ld, v / lv)
        (v / lv) * (ld / c) 

    //let nextMultiple (a : float) (v : float) =
    //    let eps = 0.00001
    //    let m = v % a
    //    if m < eps || m > a - eps then
    //        v
    //    else
    //        v + (a - m)
            
    //let prevMultiple (a : float) (v : float) =
    //    let eps = 0.00001
    //    let m = v % a
    //    if m < eps || m > a - eps then
    //        v
    //    else
    //        v - m

    let next (t : float) (dt : float) (d : float)  =
        if d <= 0.0 then
            t + dt
        else
            
            t + max 1.0 (t * d) * dt
            //let x0 = 1.0 / d 

            //if t < x0 then
            //    t + dt
            //else
            //t * (1.0 + d * dt)


            //if m > 0.0 then
            //    tmin <- tmin + (dt - m)
            //if t <= tmin then
            //    t + dt
            //else
            //    t + t * d * dt

    // tb * f ** (ceil ((log t - log tb) / log f) - 1.0)

    [<GLSLIntrinsic("float({0} * pow(double({1}), double(ceil(log(({2})/({0})) / log(({1}))) - 1.0)))", "GL_ARB_gpu_shader_fp64")>]
    let blubber (tb : float) (f : float) (t : float) = onlyInShaderCode "blubber"


    let prev (t : float) (dt : float) (d : float) =
        if d <= 0.0 then
            let m = t % dt
            if m = 0.0 then t - dt
            else t - m
                
        else

            let x0 = floor (1.0 / (d * dt)) * dt + dt

            if t <= x0 then
                let m = t % dt
                if m > 0.0 then t - m
                else t - dt
            else
                let mutable f = 1.0 + d * dt

                let mutable tLast = 0.0
                let mutable ti = 0.0
                while ti <= t do
                    tLast <- ti
                    ti <- next ti dt d
                tLast

                //// t = x0 * f ^ n
                //// log t = log x0 + n * log f
                //// log (t / x0) / log f  = n
                
                //let a = Fun.Log (t / x0) / Fun.Log f

                //x0 * f ** (ceil a - 1.0)
    
    [<GLSLIntrinsic("gl_FragCoord")>]
    let frag() : V4d = onlyInShaderCode "frag"


    let nearPoint (farPoint : V3d) (dir : V3d) (box : Box) =
        let dir = Vec.normalize dir
        // nearPoint + t * dir = farPoint
        
        // nearPoint = farPoint - t * dir

        // farPoint.X - t * dir.X = max.X
        // t = (farPoint.X - max.X) / dir.X

        let mutable t = 1000000.0

        if dir.X > 0.0 then
            t <- (farPoint.X - box.min.X) / dir.X |> min t
        elif dir.X < 0.0 then
            t <- (farPoint.X - box.max.X) / dir.X |> min t
            
        if dir.Y > 0.0 then
            t <- (farPoint.Y - box.min.Y) / dir.Y |> min t
        elif dir.Y < 0.0 then
            t <- (farPoint.Y - box.max.Y) / dir.Y |> min t
            
        if dir.Z > 0.0 then
            t <- (farPoint.Z - box.min.Z) / dir.Z |> min t
        elif dir.Z < 0.0 then
            t <- (farPoint.Z - box.max.Z) / dir.Z |> min t
        
        farPoint - t * dir











    let march (v : Vertex) =
        fragment {
            let origin = v.cam

            // (1,2,3) => (1/3, 2/3, 1)
            // (2,4,6) => (2/6, 4/6, 1)

            let pp = V2d(2.0, 2.0) * (frag().XY / V2d uniform.ViewportSize) + V2d(-1.0, -1.0)
            let ppx = V2d(2.0, 2.0) * ((frag().XY + V2d.IO) / V2d uniform.ViewportSize) + V2d(-1.0, -1.0)
            let ppy = V2d(2.0, 2.0) * ((frag().XY + V2d.OI) / V2d uniform.ViewportSize) + V2d(-1.0, -1.0)

            //let pp = V3d(v.position.XY / v.position.W, -1.0)
            //let ppx = pp + V3d(1.0 / float uniform.ViewportSize.X, 0.0, 0.0)
            //let ppy = pp + V3d(0.0, 1.0 / float uniform.ViewportSize.Y, 0.0)

            let o = uniform.ModelViewProjTrafoInv * V4d(pp.X, pp.Y, -1.0, 1.0)
            let ox = uniform.ModelViewProjTrafoInv * V4d(ppx.X, ppx.Y, -1.0, 1.0)
            let oy = uniform.ModelViewProjTrafoInv * V4d(ppy.X, ppy.Y, -1.0, 1.0)
            let o = o.XYZ / o.W
            let ox = ox.XYZ / ox.W
            let oy = oy.XYZ / oy.W




            let dir = Vec.normalize (o - origin)
            let l = Vec.length dir
            let size = float uniform.CellSize
            let invSize = 1.0 / size
            
            let totalTextureSize = size * V3d uniform.GridSize

            // TODO: maybe half pixel???
            let dx = normalizeRelative dir (ox - origin)
            let dy = normalizeRelative dir (oy - origin)

            let baseDt = invSize
                       

            // transform ray to tc-space
            let o = o / V3d uniform.GridSize
            let origin = origin / V3d uniform.GridSize
            let dir = dir / V3d uniform.GridSize
            let dx = dx / V3d uniform.GridSize
            let dy = dy / V3d uniform.GridSize
            let farPoint = v.objPos / V3d uniform.GridSize

            let invDir = 1.0 / dir

            // boxes in tc-space
            let overall = 
                { min = V3d.Zero; max = V3d.III }

            let cell = 
                { 
                    min = v.offsetAndScale.XYZ / V3d uniform.GridSize
                    max = (v.offsetAndScale.XYZ + v.offsetAndScale.W * V3d.III) / V3d uniform.GridSize 
                }
                
            let cellColor = V4d(0.5 * (cell.max + V3d.III), 1.0)

            let planeColor = 
                if uniform.ShowPlanes then cellColor * 0.02
                else V4d.Zero    
                

            let maxDim = 
                if abs dir.X > abs dir.Y then   
                    if abs dir.X > abs dir.Z then 0
                    else 2
                else
                    if abs dir.Y > abs dir.Z then 1
                    else 2
        
            let near = nearPoint farPoint dir overall
            let tminAll = (near.[maxDim] - origin.[maxDim]) / dir.[maxDim]
                
            if true then //intersectsBox2 origin dir overall &&tminAll &&tmaxAll then
                
                //let origin = origin + dir * tminAll
                let textureSize = V3d uniform.GridSize * size
                let dr = textureSize * (dx - dir) |> Vec.length
                let du = textureSize * (dy - dir) |> Vec.length
                let nd = uniform.Magic * max dr du

                // HACK
                //let nd = uniform.Magic
                
                let tnear = (o.[maxDim] - origin.[maxDim]) / dir.[maxDim]

                let near = nearPoint farPoint dir cell
                let tmin = (near.[maxDim] - origin.[maxDim]) / dir.[maxDim]
                let tmax = (farPoint.[maxDim] - origin.[maxDim]) / dir.[maxDim]
                let tmin = max tnear tmin


                //return V4d(hsv2rgb (tmin / 100.0) 1.0 1.0, 1.0) * 0.02


                if true then //intersectsBox2 origin dir cell &&tmin &&tmax then
                    
                    // woodgrain avoidance
                    let tfst = next (prev tminAll baseDt nd) baseDt nd
                    let delta = tfst - tminAll
                    let origin = origin - delta * dir

                    //let tmin = max tmin tminAll
                    //let m = (tmin - tminAll) % baseDt
                    //let tmin = tmin - m

                    //let origin = origin + tminAll * dir
                    //let nd = 0.0 //tminAll * nd
                    //let tmin = tmin - tminAll
                    //let tmax = tmax - tminAll
                    //let tminAll = 0.0
                    //let mutable origin = origin
                    //let last123 = prev tmin baseDt nd
                    //if last123 < tnear then
                    //    let tfst = next (prev tnear baseDt nd) baseDt nd
                    //    let delta = tfst - tnear
                    //    origin <- origin - delta * dir


                    let mutable lastT = prev tmin baseDt nd
                    let mutable t = next lastT baseDt nd
                    let mutable color = planeColor
                    let mutable lastCoord = origin + lastT * dir
                    let mutable coord = origin + t * dir
                
                    let mutable iter = 0
                    while t < tmax && iter < 1024 do
                        // sample the data
                        let v = sampleVolumeOfSize textureSize coord //sampleVolume coord //sampleSphere lastCoord coord //sampleVolumeOfSize textureSize coord

                        let v = if uniform.ShowPlanes then cellColor * v.W else v

                        let s = max 1.0 (nd * t)
                        let level = Fun.Log2 s
                    
                        //let v = V4d(hsv2rgb (level / 3.0) 1.0 1.0, 1.0) * v.W

                        // if alpha is nonzero
                        if v.W > 0.0 then
                            let dt = t - lastT
                            // opacity correction
                            let alpha = 1.0 - (1.0 - v.W) ** (l * dt)
                            let v = V4d(v.XYZ * (alpha / v.W), alpha)
                        
                            // compose color
                            color <- compose color v
                           
                        // step
                        lastT <- t
                        t <- next t baseDt nd
                        lastCoord <- coord
                        coord <- origin + t * dir
                        iter <- iter + 1
              


                    //if t >= tmaxAll then
                    //    color <- compose color V4d.OOII

                    //if iter = 1024 && t <= tmax then
                    //    color <- compose color V4d.IOOI
                       
                    color <- compose color planeColor
                            
                    return color
                else
                    return V4d.OIOI
                   
            else
                return V4d.OIOI
               
                    
        }

    

let ofIndexedGeometry2 (instanceCount : int) (g : IndexedGeometry) =
    let attributes = 
        g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) -> 
            let t = v.GetType().GetElementType()
            let view = BufferView(AVal.constant (ArrayBuffer(v) :> IBuffer), t)

            k, view
        ) |> Map.ofSeq
        

    let index, faceVertexCount =
        if g.IsIndexed then
            g.IndexArray, g.IndexArray.Length
        else
            null, g.IndexedAttributes.[DefaultSemantic.Positions].Length
            
    let call = 
        {
            FaceVertexCount = faceVertexCount
            FirstIndex = 0
            InstanceCount = instanceCount
            FirstInstance = 0
            BaseVertex = 0
        }
    let sg = Sg.VertexAttributeApplicator(attributes, Sg.RenderNode(call,g.Mode)) :> ISg
    if not (isNull index) then
        Sg.VertexIndexApplicator(BufferView.ofArray index, sg) :> ISg
    else
        sg



[<EntryPoint>]
let main argv = 
    
    
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
        win.View |> AVal.map (fun vs -> 
            let cam = trafo.Backward.TransformPos (vs.[0].Backward.TransformPos(V3d.Zero))
            let b = trafo.Backward.TransformPos (vs.[0].Backward.TransformPos(-V3d.OOI))
            let fw = Vec.normalize (b - cam)

            boxes |> Array.sortBy (fun min -> 
                let c = Box3d.FromMinAndSize(V3d min.XYZ, V3d.III * float min.W).Center
                
                Vec.length (c - cam)
            ) |> ArrayBuffer :> IBuffer

        )

    let fillMode = AVal.init FillMode.Fill
    let cellSize = AVal.init 64
    let magic = AVal.init 1.0
    
    win.Keyboard.KeyDown(Keys.Up).Values.Add(fun _ ->

        transact (fun () -> magic.Value <- magic.Value + 0.5)
        Log.warn "magic: %A"  magic.Value
    )
    
    win.Keyboard.KeyDown(Keys.Down).Values.Add(fun _ ->
        transact (fun () -> magic.Value <- magic.Value - 0.5)
        Log.warn "magic: %A"  magic.Value
    )
    
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
    
    let planes = AVal.init false
    win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
        transact (fun () ->
            planes.Value <- not planes.Value
        )
    )

    let box = ofIndexedGeometry2 boxes.Length Primitives.unitBox

    let buffer = BufferView(sorted,typeof<V4f>)

    let blendMode =
        BlendMode.simple BlendFactor.InvDestinationAlpha BlendFactor.One

    let clear =
        Sg.fullScreenQuad
            |> Sg.shader {
                do! DefaultSurfaces.constantColor (C4f(0.0,0.0,0.0,0.0))
            }
            |> Sg.depthTest (AVal.constant DepthTest.None)
          
    let pa = RenderPass.after "a" RenderPassOrder.Arbitrary RenderPass.main
    let pb = RenderPass.after "b" RenderPassOrder.Arbitrary pa

  

    let sg = 
        // create a red box with a simple shader
        box
            |> Sg.instanceBuffer (Sym.ofString "Offset") buffer
            //|> Sg.uniform "PaddedTextureSize" (AVal.constant fullSize)
            |> Sg.fillMode fillMode
            |> Sg.shader {
                do! Shaders.instanceTrafo
                do! DefaultSurfaces.trafo
                //do! DefaultSurfaces.constantColor (C4f(0.1,0.0,0.0,0.1))
                do! Shaders.march
            }
            |> Sg.blendMode (AVal.constant blendMode)
            |> Sg.cullMode (AVal.constant CullMode.Back)
            |> Sg.depthTest (AVal.constant DepthTest.None)
            |> Sg.pass pa
            |> Sg.transform trafo
            |> Sg.uniform "GridSize" (AVal.constant cells)
            |> Sg.uniform "ShowPlanes" planes
            |> Sg.uniform "CellSize" cellSize
            |> Sg.uniform "Magic" magic
            |> Sg.andAlso clear
            
    // show the window
    win.Scene <- sg
    win.Run()

    0
