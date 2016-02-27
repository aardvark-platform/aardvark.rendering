#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Rendering.Interactive

//open Default // makes viewTrafo and other tutorial specicific default creators visible


open System.Collections.Generic
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

module LoD = 

    Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])


    module Helpers = 
        let rand = Random()
        let randomPoints (bounds : Box3d) (pointCount : int) =
            let size = bounds.Size
            let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) * size + bounds.Min |> V3f.op_Explicit
            let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

            IndexedGeometry(
                Mode = IndexedGeometryMode.PointList,
                IndexedAttributes = 
                    SymDict.ofList [
                            DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                            DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
                    ]
            )

        let randomColor() =
            C4b(128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 255uy)
        let randomColor2(alpha) =
            C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, alpha)

        let box (color : C4b) (box : Box3d) =

            let randomColor = color //C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

            let indices =
                [|
                    1;2;6; 1;6;5
                    2;3;7; 2;7;6
                    4;5;6; 4;6;7
                    3;0;4; 3;4;7
                    0;1;5; 0;5;4
                    0;3;2; 0;2;1
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> randomColor) :> Array
                    ]

            )

        let wireBox (color : C4b) (box : Box3d) =
            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.LineList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> color) :> Array
                    ]

            )

        let frustum (f : IMod<CameraView>) (proj : IMod<Frustum>) =
            let invViewProj = Mod.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) f proj

            let positions = 
                [|
                    V3f(-1.0, -1.0, -1.0)
                    V3f(1.0, -1.0, -1.0)
                    V3f(1.0, 1.0, -1.0)
                    V3f(-1.0, 1.0, -1.0)
                    V3f(-1.0, -1.0, 1.0)
                    V3f(1.0, -1.0, 1.0)
                    V3f(1.0, 1.0, 1.0)
                    V3f(-1.0, 1.0, 1.0)
                |]

            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let geometry =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.LineList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                            DefaultSemantic.Colors, Array.create indices.Length C4b.Red :> Array
                        ]
                )

            geometry
                |> Sg.ofIndexedGeometry
                |> Sg.trafo invViewProj

    [<AutoOpen>]
    module ``Move To Base`` =
        open System.Threading
        open System.Runtime.InteropServices

        [<AllowNullLiteral>]
        type private HashQueueNode<'a> =
            class
                val mutable public Value : 'a
                val mutable public Next : HashQueueNode<'a>
                val mutable public Prev : HashQueueNode<'a>

                new(v,p,n) = { Value = v; Prev = p; Next = n }
            end

        type ConcurrentHashQueue<'a when 'a : equality>() =
            let lockObj = obj()
            let nodes = Dict<'a, HashQueueNode<'a>>()
            let mutable first = null
            let mutable last = null

            let detach (node : HashQueueNode<'a>) =
                if isNull node.Prev then first <- node.Next
                else node.Prev.Next <- node.Next

                if isNull node.Next then last <- node.Prev
                else node.Next.Prev <- node.Prev
                                 

            member x.Count = lock lockObj (fun () -> nodes.Count)

            member x.Enqueue(value : 'a) =
                lock lockObj (fun () ->
                    let node = 
                        match nodes.TryGetValue value with
                            | (true, node) -> 
                                detach node
                                node.Prev <- last
                                node.Next <- null
                                node    
                            | _ ->
                                let node = HashQueueNode(value, last, null)
                                nodes.[value] <- node
                                node

                    if isNull last then first <- node
                    else last.Next <- node
                    last <- node
                )

            member x.Dequeue() =
                lock lockObj (fun () ->
                    if isNull first then 
                        failwith "HashQueue empty"
                    else
                        let value = first.Value
                        first <- first.Next
                        first.Prev <- null
                        nodes.Remove value |> ignore
                        value
                )

            member x.TryDequeue([<Out>] result : byref<'a>) =
                try
                    Monitor.Enter lockObj
                    if isNull first then 
                        false
                    else
                        let value = first.Value
                        detach first
                        nodes.Remove value |> ignore
                        result <- value
                        true
                finally
                    Monitor.Exit lockObj

            member x.Remove(value : 'a) =
                lock lockObj (fun () ->
                    match nodes.TryRemove value with
                        | (true, node) ->
                            detach node
                            node.Value <- Unchecked.defaultof<_>
                            node.Prev <- null
                            node.Next <- null
                            true
                        | _ ->
                            false
                )


    // ===================================================================================
    // LoD stuff
    // ===================================================================================
    [<CustomEquality; NoComparison>]
    type LodDataNode =
        {
            id : obj
            level : int
            bounds : Box3d
            inner : bool
            granularity : float
        }

        override x.GetHashCode() = x.id.GetHashCode()
        override x.Equals o =
            match o with
                | :? LodDataNode as o -> x.id.Equals(o.id)
                | _ -> false

    type ILodData =
        abstract member BoundingBox : Box3d
        abstract member Traverse : (LodDataNode -> bool) -> unit
        abstract member GetData : node : LodDataNode -> Async<IndexedGeometry>

    [<AutoOpen>]
    module ``Lod Data Extensions`` =
        open System.Collections.Concurrent

        let inline private maxDir (dir : V3d) (b : Box3d) =
            V4d(
                (if dir.X > 0.0 then b.Max.X else b.Min.X), 
                (if dir.Y > 0.0 then b.Max.Y else b.Min.Y), 
                (if dir.Z > 0.0 then b.Max.Z else b.Min.Z), 
                 1.0
            )

        let inline private height (plane : V4d) (b : Box3d) =
            plane.Dot(maxDir plane.XYZ b)



        type Box3d with
            member x.IntersectsFrustumGL (viewProj : M44d) =
                let r0 = viewProj.R0
                let r1 = viewProj.R1
                let r2 = viewProj.R2
                let r3 = viewProj.R3

                height (r3 + r0) x >= 0.0 &&
                height (r3 - r0) x >= 0.0 &&
                height (r3 + r1) x >= 0.0 &&
                height (r3 - r1) x >= 0.0 &&
                height (r3 + r2) x >= 0.0 &&
                height (r3 - r2) x >= 0.0 
                




        type ILodData with
            member x.Rasterize(view : Trafo3d, projTrafo : Trafo3d, wantedNearPlaneDistance : float) =
                let viewProj = view * projTrafo
                let camLocation = view.GetViewPosition()

                let result = HashSet<LodDataNode>()

                let frustum = Frustum.ofTrafo projTrafo

                let hull = ViewProj.toFastHull3d viewProj


                x.Traverse(fun node ->
                    if hull.Intersects(node.bounds) then
                        if node.inner then
                            let bounds = node.bounds



                            let depthRange =
                                bounds.ComputeCorners()
                                    |> Array.map view.Forward.TransformPos
                                    |> Array.map (fun v -> -v.Z)
                                    |> Range1d
//
//                            if depthRange.Max < frustum.near || depthRange.Min > frustum.far then
//                                false
//                            else
                            let depthRange = Range1d(clamp frustum.near frustum.far depthRange.Min, clamp frustum.near frustum.far depthRange.Max)
                            let projAvgDistance =
                                abs (node.granularity / depthRange.Min)

                            result.Add node |> ignore
                            if projAvgDistance > wantedNearPlaneDistance then
                                true
                            else
                                false
                        else
                            result.Add node |> ignore
                            false
                    else
                        false
                )

                result




    // ===================================================================================
    // SceneGraph LoD adapters
    // ===================================================================================
    type PointCloudInfo =
        {
            attributeTypes : Map<Symbol, Type>

            /// the target point distance in pixels
            targetPointDistance : IMod<float>

            /// an optional custom view trafo
            customView : Option<IMod<Trafo3d>>

            /// an optional custom view projection trafo
            customProjection : Option<IMod<Trafo3d>>

        }

    module Sg = 
        type PointCloud(data : ILodData, config : PointCloudInfo) =
            interface ISg

            member x.Data = data
            member x.Config = config

        let pointCloud (data : ILodData) (info : PointCloudInfo) =
            PointCloud(data, info) :> ISg
            

    


    module PointCloudRenderObjectSemantics = 
        open System.Threading
        open System.Threading.Tasks
        open Aardvark.Base.Ag
        open Aardvark.SceneGraph.Semantics

        [<CustomEquality; NoComparison>]
        type GeometryRef = { node : LodDataNode; geometry : IndexedGeometry; range : Range1i } with

            override x.GetHashCode() = x.node.GetHashCode()
            override x.Equals o =
                match o with
                    | :? GeometryRef as o -> x.node.Equals(o.node)
                    | _ -> false

        type PointCloudHandler(node : Sg.PointCloud, view : IMod<Trafo3d>, proj : IMod<Trafo3d>, viewportSize : IMod<V2i>) =
            let cancel = new System.Threading.CancellationTokenSource()

            let pool = GeometryPool.create()
            let calls = DrawCallSet(true)
            let inactive = ConcurrentHashQueue<GeometryRef>()
            let mutable inactiveSize = 0
            let mutable activeSize = 0

            let levelCounts = Array.zeroCreate 128


            let geometriesRW = new ReaderWriterLockSlim()
            let geometries = Dict<LodDataNode, GeometryRef>()

            let activate (n : GeometryRef) =
                let size = n.range.Size
                if inactive.Remove n then
                    Interlocked.Add(&inactiveSize, -size) |> ignore

                Interlocked.Add(&activeSize, size) |> ignore
                levelCounts.[n.node.level] <- levelCounts.[n.node.level] + 1


                calls.Add n.range |> ignore

            let deactivate (n : GeometryRef) =
                calls.Remove n.range |> ignore
                inactive.Enqueue n |> ignore
                let size = n.range.Size
                Interlocked.Add(&inactiveSize, size) |> ignore
                Interlocked.Add(&activeSize, -size) |> ignore
                levelCounts.[n.node.level] <- levelCounts.[n.node.level] - 1


            member x.Add(n : LodDataNode) =
                let result = 
                    ReaderWriterLock.write geometriesRW (fun () ->
                        geometries.GetOrCreate(n, fun n ->
                            let g = node.Data.GetData n |> Async.RunSynchronously
                            let range = pool.Add(g)
                            { node = n; geometry = g; range = range }
                        )
                    )

                activate result

                result

            member x.Remove(n : LodDataNode) =
                ReaderWriterLock.read geometriesRW (fun () ->
                    match geometries.TryGetValue n with
                        | (true, t) -> deactivate t
                        | _ -> ()
                )


            member x.Activate() =
                
                let wantedNearPlaneDistance =
                    Mod.custom (fun self ->
                        let viewportSize = viewportSize.GetValue self
                        let wantedPixelDistance = node.Config.targetPointDistance.GetValue self

                        let size = max viewportSize.X viewportSize.Y
                        2.0 * float wantedPixelDistance / float size
                    )

                let content =
                    ASet.custom (fun self ->
                        let view = view.GetValue self
                        let proj = proj.GetValue self
                        let wantedNearPlaneDistance = wantedNearPlaneDistance.GetValue self

                        let set = node.Data.Rasterize(view, proj, wantedNearPlaneDistance)

                        let add = set |> Seq.filter (self.Content.Contains >> not) |> Seq.map Add
                        let rem = self.Content |> Seq.filter (set.Contains >> not) |> Seq.map Rem

                        let res = Seq.append add rem |> Seq.toList


                        res
                    )


                let deltas = ConcurrentDeltaQueue.ofASet content

                let printer =
                    async {
                        while true do
                            do! Async.Sleep 5000
                            Log.start "LoD"
                            for i in 0..levelCounts.Length-1 do
                                let cnt = levelCounts.[i]
                                if cnt <> 0 then
                                    Log.line "level %d: %d" i cnt
                            Log.stop()
                    }

                let runner =
                    async {
                        while true do
                            let! op = deltas.DequeueAsync()

                            match op with
                                | Add n -> x.Add n |> ignore
                                | Rem n -> x.Remove n |> ignore


                    }


                let prune =
                    async {
                        while true do
                            let mutable cnt = 0
                            while inactiveSize > activeSize do
                                match inactive.TryDequeue() with
                                    | (true, v) ->
                                        ReaderWriterLock.write geometriesRW (fun () ->
                                            match geometries.TryRemove v.node with
                                                | (true, v) ->
                                                    let r = pool.Remove v.geometry
                                                    Interlocked.Add(&inactiveSize, -v.range.Size) |> ignore
                                                    cnt <- cnt + 1
                                                | _ ->
                                                    Log.warn "failed to remove node: %A" v.node.id
                                        )

                                    | _ ->
                                        ()
                            if cnt > 0 then
                                Log.line "{ active = %A; inactive = %A; removed = %A }" activeSize inactiveSize cnt
                            do! Async.Sleep 500
                    }

                Async.StartAsTask(runner, cancellationToken = cancel.Token) |> ignore
                Async.StartAsTask(printer, cancellationToken = cancel.Token) |> ignore
                Async.StartAsTask(prune, cancellationToken = cancel.Token) |> ignore

                { new IDisposable with member x.Dispose() = () }


            member x.Attributes =
                { new IAttributeProvider with
                    member x.TryGetAttribute sem =
                        match Map.tryFind sem node.Config.attributeTypes with
                            | Some t ->
                                let b = pool.GetBuffer sem
                                BufferView(b, t) |> Some
                            | None ->
                                None

                    member x.All = Seq.empty
                    member x.Dispose() = ()
                }

            member x.DrawCallInfos =
                calls |> DrawCallSet.toMod

        [<Semantic>]
        type PointCloudSemantics() =
            member x.RenderObjects(l : Sg.PointCloud) =
                let obj = RenderObject.create()

                let view = 
                    match l.Config.customView with
                        | Some v -> v
                        | None -> l.ViewTrafo

                let proj = 
                    match l.Config.customProjection with
                        | Some p -> p
                        | None -> l.ProjTrafo

                let viewportSize =
                    match obj.Uniforms.TryGetUniform(obj.AttributeScope, Symbol.Create "ViewportSize") with
                        | Some (:? IMod<V2i> as vs) -> vs
                        | _ -> failwith "[PointCloud] could not get viewport size (please apply to scenegraph)"

                let h = PointCloudHandler(l, view, proj, viewportSize)

                let calls = h.DrawCallInfos

                obj.IndirectBuffer <- calls |> Mod.map (fun a -> ArrayBuffer(a) :> IBuffer)
                obj.IndirectCount <- calls |> Mod.map (fun a -> a.Length)
                obj.Activate <- h.Activate
                obj.VertexAttributes <- h.Attributes
                obj.Mode <- Mod.constant IndexedGeometryMode.PointList

                ASet.single (obj :> IRenderObject)


    // ===================================================================================
    // example usage
    // ===================================================================================
    type DummyDataProvider(root : Box3d) =
    
        interface ILodData with
            member x.BoundingBox = root

            member x.Traverse f =
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 100.0
                    let node = { id = b; level = level; bounds = box; inner = true; granularity = Fun.Cbrt(box.Volume / n) }

                    if f node then
                        let center = b.Center

                        let children =
                            let l = b.Min
                            let u = b.Max
                            let c = center
                            [
                                Box3d(V3d(l.X, l.Y, l.Z), V3d(c.X, c.Y, c.Z))
                                Box3d(V3d(c.X, l.Y, l.Z), V3d(u.X, c.Y, c.Z))
                                Box3d(V3d(l.X, c.Y, l.Z), V3d(c.X, u.Y, c.Z))
                                Box3d(V3d(c.X, c.Y, l.Z), V3d(u.X, u.Y, c.Z))
                                Box3d(V3d(l.X, l.Y, c.Z), V3d(c.X, c.Y, u.Z))
                                Box3d(V3d(c.X, l.Y, c.Z), V3d(u.X, c.Y, u.Z))
                                Box3d(V3d(l.X, c.Y, c.Z), V3d(c.X, u.Y, u.Z))
                                Box3d(V3d(c.X, c.Y, c.Z), V3d(u.X, u.Y, u.Z))
                            ]

                        children |> List.iter (traverse (level + 1))
                    else
                        ()
                traverse 0 root

            member x.GetData (cell : LodDataNode) =
                async {
                    do! Async.SwitchToThreadPool()
                    let b = Helpers.randomPoints cell.bounds 100
                    //let b = Helpers.box (Helpers.randomColor()) cell.bounds
                    //do! Async.Sleep 400
                    return b
                }

    let data = DummyDataProvider(Box3d(V3d.OOO, 20.0 * V3d.III)) :> ILodData

    [<AutoOpen>]
    module Camera =
        type Mode =
            | Main
            | Test

        let mode = Mod.init Main

        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)

        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Main ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let gridCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Test ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }

        let view =
            adaptive {
                let! mode = mode
                match mode with
                    | Main -> return! mainCam
                    | Test -> return! gridCam
            }

        win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
            transact (fun () ->
                match mode.Value with
                    | Main -> Mod.change mode Test
                    | Test -> Mod.change mode Main

                printfn "mode: %A" mode.Value
            )
        )

        let mainProj = perspective win 
        let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> Mod.constant

        let proj =
            adaptive {
                let! mode = mode 
                match mode with
                    | Main -> return! mainProj
                    | Test -> return! gridProj
            }



    let cloud =
        Sg.pointCloud data {
            targetPointDistance     = Mod.constant 40.0
            customView              = Some (gridCam |> Mod.map CameraView.viewTrafo)
            customProjection        = Some (gridProj |> Mod.map Frustum.projTrafo)
            attributeTypes =
                Map.ofList [
                    DefaultSemantic.Positions, typeof<V3f>
                    DefaultSemantic.Colors, typeof<C4b>
                    DefaultSemantic.Normals, typeof<V3f>
                ]

        }

                                    
    let sg = 
        Sg.group' [
            cloud
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect                  
                    DefaultSurfaces.vertexColor  |> toEffect         
                    DefaultSurfaces.pointSprite  |> toEffect     
                    DefaultSurfaces.pointSpriteFragment  |> toEffect 
                ]
            Helpers.frustum gridCam gridProj

            data.BoundingBox.EnlargedByRelativeEps(0.005)
                |> Helpers.wireBox C4b.VRVisGreen
                |> Sg.ofIndexedGeometry
        ]

    let final =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                  
                DefaultSurfaces.vertexColor  |> toEffect 
                ]
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
            |> Sg.uniform "PointSize" (Mod.constant 4.0)
            |> Sg.uniform "ViewportSize" win.Sizes
    
    let run() =
        Aardvark.Rendering.Interactive.FsiSetup.init (Path.combine [__SOURCE_DIRECTORY__; ".."; ".."; ".."; "bin";"Debug"])
        setSg final
        win.Run()












open LoD

#if INTERACTIVE
setSg final
#else
#endif