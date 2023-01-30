open Aardvark.Base
open Aardvark.Rendering
open Aardvark.GPGPU
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

[<ReflectedDefinition>]
module SplineShader =
    open FShade

    [<GLSLIntrinsic("cbrt({0})")>]
    let cbrt (v : float) : float = onlyInShaderCode "cbrt"


    let realRootsOfNormed (c2 : float) (c1 : float) (c0 : float) =
        // ------ eliminate quadric term (x = y - c2/3): x^3 + p x + q = 0
        let d = c2 * c2
        let p3 = 1.0/3.0 * (* p *)(-1.0/3.0 * d + c1)
        let q2 = 1.0/2.0 * (* q *)((2.0/27.0 * d - 1.0/3.0 * c1) * c2 + c0)
        let p3c = p3 * p3 * p3
        let shift = 1.0/3.0 * c2
        let d = q2 * q2 + p3c

        if d < 0.0 then
            let phi = 1.0 / 3.0 * Fun.Acos(-q2 / Fun.Sqrt(-p3c))
            let t = 2.0 * Fun.Sqrt(-p3)
            let r0 = t * Fun.Cos(phi) - shift
            let r1 = -t * Fun.Cos(phi + Constant.Pi / 3.0) - shift
            let r2 = -t * Fun.Cos(phi - Constant.Pi / 3.0) - shift
            (r0, r1, r2)
        else
            let d = Fun.Sqrt(d)                 // one single and one double root
            let uav = (d - q2) ** (1.0 / 3.0) - (d + q2) ** (1.0 / 3.0)
            let s0 = uav - shift
            let s1 = -0.5 * uav - shift
            (s0, s1, s1)

    let realRootsOf (c3 : float) (c2 : float) (c1 : float) (c0 : float) =
        realRootsOfNormed (c2 / c3) (c1 / c3) (c0 / c3)

    let clipSpline (a : V2d) (b : V2d) (c : V2d) (d : V2d) (tmin : ref<float>) (tmax : ref<float>) =
        
        let (txl0, txl1, txl2) = realRootsOf (d.X + 3.0*b.X - 3.0*c.X - a.X) (3.0*(a.X - 2.0*b.X + c.X)) (3.0 * (b.X - a.X)) (a.X - 1.0)
        let (txh0, txh1, txh2) = realRootsOf (d.X + 3.0*b.X - 3.0*c.X - a.X) (3.0*(a.X - 2.0*b.X + c.X)) (3.0 * (b.X - a.X)) (a.X + 1.0)
        
        let (tyl0, tyl1, tyl2) = realRootsOf (d.Y + 3.0*b.Y - 3.0*c.Y - a.Y) (3.0*(a.Y - 2.0*b.Y + c.Y)) (3.0 * (b.Y - a.Y)) (a.Y - 1.0)
        let (tyh0, tyh1, tyh2) = realRootsOf (d.Y + 3.0*b.Y - 3.0*c.Y - a.Y) (3.0*(a.Y - 2.0*b.Y + c.Y)) (3.0 * (b.Y - a.Y)) (a.Y + 1.0)

        let mutable min = 1.0
        let mutable max = 0.0

        if txl0 >= 0.0 && txl0 <= 1.0 then
            if txl0 < min then min <- txl0
            elif txl0 > max then max <- txl0

        if txl1 >= 0.0 && txl1 <= 1.0 then
            if txl1 < min then min <- txl1
            elif txl1 > max then max <- txl1
            
        if txl2 >= 0.0 && txl2 <= 1.0 then
            if txl2 < min then min <- txl2
            elif txl2 > max then max <- txl2

        if txh0 >= 0.0 && txh0 <= 1.0 then
            if txh0 < min then min <- txh0
            elif txh0 > max then max <- txh0

        if txh1 >= 0.0 && txh1 <= 1.0 then
            if txh1 < min then min <- txh1
            elif txh1 > max then max <- txh1
            
        if txh2 >= 0.0 && txh2 <= 1.0 then
            if txh2 < min then min <- txh2
            elif txh2 > max then max <- txh2


        if tyl0 >= 0.0 && tyl0 <= 1.0 then
            if tyl0 < min then min <- tyl0
            elif tyl0 > max then max <- tyl0

        if tyl1 >= 0.0 && tyl1 <= 1.0 then
            if tyl1 < min then min <- tyl1
            elif tyl1 > max then max <- tyl1
            
        if tyl2 >= 0.0 && tyl2 <= 1.0 then
            if tyl2 < min then min <- tyl2
            elif tyl2 > max then max <- tyl2

        if tyh0 >= 0.0 && tyh0 <= 1.0 then
            if tyh0 < min then min <- tyh0
            elif tyh0 > max then max <- tyh0

        if tyh1 >= 0.0 && tyh1 <= 1.0 then
            if tyh1 < min then min <- tyh1
            elif tyh1 > max then max <- tyh1
            
        if tyh2 >= 0.0 && tyh2 <= 1.0 then
            if tyh2 < min then min <- tyh2
            elif tyh2 > max then max <- tyh2

        tmin := min
        tmax := max

    let evalSpline (a : V4d) (b : V4d) (c : V4d) (d : V4d) (t : float) =
        (1.0-t)*((1.0-t)*((1.0-t)*a + t*b) + t*((1.0-t)*b + t*c)) +
        t      *((1.0-t)*((1.0-t)*b + t*c) + t*((1.0-t)*c + t*d))

    let evalProjectiveSpline (a : V4d) (b : V4d) (c : V4d) (d : V4d) (t : float) =
        let res = evalSpline a b c d t
        0.5 * (res.XY / res.W + V2d.II)

    [<LocalSize(X = 64)>]
    let prepare (cpIn : V4f[]) (div : int[]) (ts : V2f[]) (count : int) (viewProj : M44d) (viewportSize : V2i) (threshold : float) =
        compute {
            let id = getGlobalId().X

            if id < count then
                let i0 = 4 * id
                let p0 = viewProj * V4d cpIn.[i0 + 0]
                let p1 = viewProj * V4d cpIn.[i0 + 1]
                let p2 = viewProj * V4d cpIn.[i0 + 2]
                let p3 = viewProj * V4d cpIn.[i0 + 3]

                // TODO: proper clipping
                let mutable tmin = 0.0
                let mutable tmax = 1.0
                // !!! clipSline is wrong !!!
                // clipSpline (p0.XY / p0.W) (p1.XY / p1.W) (p2.XY / p2.W) (p3.XY / p3.W) &&tmin &&tmax
                

                let steps = 8
                let mutable tc = tmin
                let mutable pc = evalProjectiveSpline p0 p1 p2 p3 tc
                let step = (tmax - tmin) / float steps
                let mutable approxLen = 0.0
                for i in 0 .. steps - 1 do
                    let tn = tc + step
                    let pn = evalProjectiveSpline p0 p1 p2 p3 tn
                    approxLen <- approxLen + Vec.length(V2d viewportSize * (pn - pc))
                    tc <- tn
                    pc <- pn

                let dd = clamp 1 8192 (int (approxLen / threshold))
                div.[id] <- dd
                ts.[id] <- V2f(tmin, tmax)
        }
        
    [<LocalSize(X = 64)>]
    let evalulate (scannedDiv : int[]) (scannedCount : int) (ts : V2f[]) (cps : V4f[]) (lines : V4f[]) =
        compute {
            let id = getGlobalId().X
            let count = scannedDiv.[scannedCount - 1]
            if id < count then
                let mutable l = 0
                let mutable r = scannedCount - 1
                let mutable m = (l + r) / 2
                while l <= r do
                    let s = scannedDiv.[m]
                    if s < id then l <- m + 1
                    elif s > id then r <- m - 1
                    else
                        l <- m + 1
                        r <- m
                    m <- (l + r) / 2
                    
                let splineId = l

                let baseIndex = 
                    if splineId > 0 then scannedDiv.[splineId - 1]
                    else 0

                let indexInSpline =
                    id - baseIndex

                let cnt = scannedDiv.[splineId] - baseIndex

                let ts = ts.[splineId]
                let tmin = float ts.X
                let tmax = float ts.Y

                let ts = (tmax - tmin) / float cnt
                let t0 = float indexInSpline * ts + tmin
                let t1 = t0 + ts

                let i0 = 4 * splineId
                let a = V4d cps.[i0 + 0]
                let b = V4d cps.[i0 + 1]
                let c = V4d cps.[i0 + 2]
                let d = V4d cps.[i0 + 3]

                let p0 = evalSpline a b c d t0
                let p1 = evalSpline a b c d t1

                lines.[2*id+0] <- V4f p0
                lines.[2*id+1] <- V4f p1
        }


module Sg =
    open Aardvark.Base.Ag
    open Aardvark.SceneGraph.Semantics

    type SplineNode(viewportSize : aval<V2i>, threshold : aval<float>, controlPoints : aval<V3d[]>) =
        interface ISg
        member x.ViewportSize = viewportSize
        member x.Threshold = threshold
        member x.ControlPoints = controlPoints

    [<Rule>]
    type SplineSemantics() =
        
        static let cache = System.Collections.Concurrent.ConcurrentDictionary<IRuntime, ParallelPrimitives * IComputeShader * IComputeShader>()

        static let get(runtime : IRuntime) =
            cache.GetOrAdd(runtime, fun runtime ->
                let prepare = runtime.CreateComputeShader SplineShader.prepare
                let evaluate = runtime.CreateComputeShader SplineShader.evalulate
                let prim = ParallelPrimitives runtime
                prim, prepare, evaluate
            )

        static let subdiv (runtime : IRuntime) (threshold : aval<float>) (size : aval<V2i>) (viewProj : aval<Trafo3d>) (cpsArray : V3d[]) =
            let prim, prepare, evaluate = get runtime
            
            let cnt = cpsArray.Length / 4
            let mutable cps = Unchecked.defaultof<IBuffer<V4f>>
            let mutable res : Option<IBuffer<V4f>> = None
        
            let compute (t : AdaptiveToken) =
                use div = runtime.CreateBuffer<int>(cnt)
                use ts = runtime.CreateBuffer<V2f>(cnt)
                let viewProj = viewProj.GetValue t
                let size = size.GetValue t
                let threshold = threshold.GetValue t

                use ip = runtime.NewInputBinding prepare
                ip.["cpIn"] <- cps
                ip.["div"] <- div
                ip.["ts"] <- ts
                ip.["count"] <- cnt
                ip.["viewProj"] <- viewProj
                ip.["viewportSize"] <- size
                ip.["threshold"] <- threshold
                ip.Flush()

                runtime.Run [
                    ComputeCommand.Bind prepare
                    ComputeCommand.SetInput ip
                    ComputeCommand.Dispatch(ceilDiv cnt 64)
                ]


                prim.Scan(<@ (+) @>, div, div)
            
                let total = div.[cnt-1 .. cnt-1].Download().[0]
                let cap = max 16 (Fun.NextPowerOfTwo(total * 2))
            
                let res =
                    match res with
                        | Some b when b.Count = cap -> 
                            b
                        | Some b ->
                            Log.warn "size: %d" cap
                            b.Dispose()
                            let b = runtime.CreateBuffer<V4f>(cap)
                            res <- Some b
                            b
                        | None ->
                            Log.warn "size: %d" cap
                            let b = runtime.CreateBuffer<V4f>(cap)
                            res <- Some b
                            b
                        

                //let evalulate (scannedDiv : int[]) (scannedCount : int) (ts : V2f[]) (cps : V4f[]) (lines : V4f[])
                use ip = runtime.NewInputBinding evaluate
                ip.["scannedDiv"] <- div
                ip.["scannedCount"] <- cnt
                ip.["ts"] <- ts
                ip.["cps"] <- cps
                ip.["lines"] <- res
                ip.Flush()

                runtime.Run [
                    ComputeCommand.Bind evaluate
                    ComputeCommand.SetInput ip
                    ComputeCommand.Dispatch(ceilDiv total 64)
                ]

                res, total

            let overall =
                { new AdaptiveResource<IBuffer<V4f> * int>() with
                    member x.Create() = 
                        Log.warn "create"
                        cps <- runtime.CreateBuffer<V4f>(cpsArray.Length)
                        cps.Upload(cpsArray |> Array.map (fun v -> V4f(v.X, v.Y, v.Z, 1.0)))
                        transact (fun () -> x.MarkOutdated())

                    member x.Destroy() = 
                        Log.warn "destroy"
                        cps.Dispose()
                        cps <- Unchecked.defaultof<IBuffer<V4f>>
                        res |> Option.iter (fun d -> d.Dispose())
                        res <- None
                        transact (fun () -> x.MarkOutdated())

                    member x.Compute(t,rt) = 
                        compute t
                }
            

            overall :> IAdaptiveResource<_>
            //let buffer =
            //    { new AdaptiveResource<IBuffer>() with
            //        member x.Create() = overall.Acquire()
            //        member x.Destroy() = overall.Release()
        
            //        member x.Compute(t,rt) = 
            //            let (b,_) = overall.GetValue(t)
            //            b.Buffer :> IBuffer
            //    }
            //let count =
            //    { new AdaptiveResource<int>() with
            //        member x.Create() = overall.Acquire()
            //        member x.Destroy() = overall.Release()
        
            //        member x.Compute(t,rt) = 
            //            let (_,c) = overall.GetValue(t)
            //            2 * c
            //    }
            //buffer :> IAdaptiveResource<_>, count :> IAdaptiveResource<_>
            
        member x.GlobalBoundingBox(s : SplineNode, scope : Ag.Scope) =
            let t = scope.ModelTrafo
            AVal.map2 (fun (trafo : Trafo3d) (cps : V3d[]) -> Box3d(cps |> Array.map trafo.Forward.TransformPos)) t s.ControlPoints

        member x.LocalBoundingBox(s : SplineNode, scope : Ag.Scope) =
            s.ControlPoints |> AVal.map Box3d

        member x.RenderObjects(s : SplineNode, scope : Ag.Scope) : aset<IRenderObject> =
            let runtime = scope.Runtime
            let viewProj = AVal.map2 (*) scope.ViewTrafo scope.ProjTrafo
            let mvp = AVal.map2 (*) scope.ModelTrafo viewProj

            //let mm = s.ControlPoints |> AVal.map (subdiv runtime s.Threshold s.ViewportSize mvp)
            let cps = s.ControlPoints

            let mutable last : Option<IAdaptiveResource<_>> = None
            let mutable cpsChanged = false

            let bind (view : IBuffer<V4f> * int -> 'a) =
                { new AdaptiveResource<'a>() with
                    override x.InputChangedObject(o,i) =
                        if i = (cps :> IAdaptiveObject) then cpsChanged <- true

                    member x.Create() =
                        let v = cps.GetValue()
                        let om = subdiv runtime s.Threshold s.ViewportSize mvp v
                        om.Acquire()
                        last <- Some om
                        transact (fun () -> x.MarkOutdated())

                    member x.Destroy() =
                        match last with
                        | Some o -> 
                            o.Outputs.Remove x |> ignore
                            o.Release()
                            last <- None
                        | _ -> 
                            ()
                        transact (fun () -> x.MarkOutdated())

                    member x.Compute(t,rt) = 
                        let v = cps.GetValue(t)
                        let om = 
                            if cpsChanged || Option.isNone last then
                                cpsChanged <- false
                                match last with
                                | Some o ->
                                    o.Outputs.Remove x |> ignore
                                    o.Release()
                                | _ -> 
                                    ()
                                let om = subdiv runtime s.Threshold s.ViewportSize mvp v
                                om.Acquire()
                                last <- Some om
                                om
                            else
                                last.Value
                                
                        let r = om.GetValue(t)
                        view r
                }
                


            let buffer = bind (fun (b,_) -> b.Buffer :> IBuffer)
            let count = bind (fun (_,c) -> 2*c)
                

            let o = RenderObject.ofScope scope
            o.DrawCalls <- Direct(count |> AVal.map (fun c -> [DrawCallInfo(FaceVertexCount = c, InstanceCount = 1)]))
            o.Mode <- IndexedGeometryMode.LineList
            o.VertexAttributes <-
                let oa = o.VertexAttributes
                let posView = BufferView(buffer, typeof<V4f>)
                { new IAttributeProvider with
                    member x.Dispose() = oa.Dispose()
                    member x.TryGetAttribute(sem : Symbol) =
                        if sem = DefaultSemantic.Positions then Some posView
                        else oa.TryGetAttribute sem
                }

            ASet.single (o :> IRenderObject)



[<EntryPoint>]
let main argv = 
    
    Aardvark.Init()
    
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }




    let cps =
        AVal.init [|
            V3d.OOO; V3d.OOI; V3d.IOI; V3d.IOO
            V3d(0,1,0); V3d(0,1,1); V3d(1,1,-1); V3d(1,1,0)
        |]

    let threshold = AVal.init 10.0
    let active = AVal.init true

    let rand = RandomSystem()
    let bounds = Box3d(-V3d.III, V3d.III)

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.OemPlus -> transact (fun () -> threshold.Value <- threshold.Value + 1.0); Log.warn "threshold: %A" threshold.Value
        | Keys.OemMinus -> transact (fun () -> threshold.Value <- max 1.0 (threshold.Value - 1.0)); Log.warn "threshold: %A" threshold.Value
        
        | Keys.Space -> transact (fun () -> active.Value <- not active.Value)
        
        | Keys.Enter -> 
            transact (fun () -> 
                cps.Value <- Array.append cps.Value (Array.init 4 (fun _ -> rand.UniformV3d(bounds)))
            )
        | Keys.Back -> 
            if cps.Value.Length > 4 then
                transact (fun () -> 
                    cps.Value <- Array.take (cps.Value.Length - 4) cps.Value
                )
        
        | _ -> ()
    )

    let sg =
        Sg.ofList [
            for x in 0 .. 0 do
                yield 
                    Sg.SplineNode(win.Sizes, threshold, cps) :> ISg
                    |> Sg.uniform "LineWidth" (AVal.constant 6.0)
                    |> Sg.translate (float x * 2.0) 0.0 0.0
                    |> Sg.shader {
                        do! DefaultSurfaces.trafo
                        do! DefaultSurfaces.thickLine
                        do! DefaultSurfaces.thickLineRoundCaps
                        do! DefaultSurfaces.constantColor C4f.White
                    }
        ]

    let sg =
        active 
        |> AVal.map (function true -> sg | false -> Sg.empty)
        |> Sg.dynamic


    win.Scene <- sg
    win.Run()


    0
