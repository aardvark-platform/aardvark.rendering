open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.Rendering.GL
open Aardvark.SceneGraph
open Aardvark.SceneGraph.CSharp
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.CSharp
open Aardvark.Base.Rendering
//open Demo
open CSharpStuff

open Aardvark.Application
open Aardvark.Application.WinForms

// this module demonstrates how to extend a given scene graph system (here the one provided by assimp) with
// semantic functions to be used in our rendering framework
module Assimp =
    open Assimp
    open System.Runtime.CompilerServices
    open System.Collections.Generic

    
    // for convenience we define some extension
    // functions accessing Attributes type-safely
    type Node with
        member x.Scene : Scene = x?AssimpScene

        member x.RenderJobs() : aset<RenderJob> = x?RenderJobs()

        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()

    type Scene with
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()
        member x.RenderJobs() : aset<RenderJob> = x?RenderJobs()


    // in order to integrate Assimo's scene structure we
    // need to define several attributes for it (namely RenderJobs, ModelTrafo, etc.)
    // which can be done using s single or multiple semantic-types. 
    // Since the implementation is relatively dense we opted for only one type here.
    [<Semantic>]
    type AssimpSemantics() =

        // semantics may define local fields/functions as needed

        // since caching is desirable for multiple occurances of the
        // identical mesh we use a ConditionalWeakTable here.
        // NOTE that the ISg representations will be kept alive as
        //      long as the input Mesh is alive.
        let cache = ConditionalWeakTable<Mesh, ISg>()


        let textureCache = Dictionary<string, IMod<ITexture>>()

        // mapAttribute takes attributes as defined by assimp and converts 
        // them using a given function. Furthermore the result is upcasted to
        // System.Array and wrapped up in a constant mod-cell.
        // Note that the conversion is applied lazily
        let mapAttribute (f : 'a -> 'b) (l : List<'a>) =
            let data = 
                Mod.delay(fun () ->
                    let arr = Array.zeroCreate l.Count
                    for i in 0..l.Count-1 do
                        arr.[i] <- f l.[i]

                    arr :> Array
                )
            BufferView(ArrayBuffer data, typeof<'b>)

        // try to find the Mesh's diffuse texture
        // and convert it into a file-texture.
        // Finally cache texture-mods per path helping the
        // backend to identify equal textures
        let tryFindDiffuseTexture (m : Mesh) =

            let scene : Scene = m?AssimpScene
            let workDirectory : string = scene?WorkDirectory

            if m.MaterialIndex >= 0 then 
                let mat = scene.Materials.[m.MaterialIndex]
                if mat.HasTextureDiffuse then
                    let file = mat.TextureDiffuse.FilePath

                    let file =
                        file.Replace('/', System.IO.Path.DirectorySeparatorChar)
                            .Replace('\\', System.IO.Path.DirectorySeparatorChar)

                    let file =
                        if file.Length > 0 && file.[0] = System.IO.Path.DirectorySeparatorChar then
                            file.Substring(1)
                        else
                            file

                    let path = System.IO.Path.Combine(workDirectory, file)
                                
                    match textureCache.TryGetValue path with
                        | (true, tex) -> 
                            Some tex
                        | _ ->
                            let tex = FileTexture(path, true)
                            let m = Mod.initConstant (tex :> ITexture)
                            textureCache.[path] <- m
                            Some m
                else
                    None
            else 
                None


        
        // toSg is used by toRenderJobs in order to simplify 
        // things here.
        // Note that it would also be possible here to create RenderJobs 
        //      directly. However this is very wordy and we therefore create
        //      a partial SceneGraph for each mesh
        let toSg (m : Mesh) =
            match cache.TryGetValue m with
                | (true, sg) -> 
                    sg
                | _ ->
                    if m.HasFaces && m.PrimitiveType = PrimitiveType.Triangle then
                        let scene : Scene  = m?AssimpScene
                        let indexArray = m.GetIndices()

                        let vertexCount = m.Vertices.Count

                        // convert all attributes present in the mesh
                        // Note that all conversions are performed lazily here
                        //      meaning that attributes which are not required for rendering
                        //      will not be converted
                        let attributes =
                            SymDict.ofList [
                        
                                if m.Vertices <> null then
                                    let bb = Box3d(m.Vertices |> Seq.map (fun v -> V3d(v.X, v.Y, v.Z)))
                                    yield DefaultSemantic.Positions, m.Vertices |> mapAttribute (fun v -> V3f(v.X, v.Y, v.Z))
                        
                                if m.Normals <> null then
                                    yield DefaultSemantic.Normals, m.Normals |> mapAttribute (fun v -> V3f(v.X, v.Y, v.Z))

                                if m.TextureCoordinateChannelCount > 0 then
                                    let tc = m.TextureCoordinateChannels.[0]
                                    yield DefaultSemantic.DiffuseColorCoordinates, tc |> mapAttribute (fun v -> V2f(v.X, 1.0f - v.Y))

                            ]

                    
                        // try to find the Mesh's diffuse texture
                        let diffuseTexture = tryFindDiffuseTexture m


                        // if the mesh is indexed use its index for determinig the
                        // total face-vertex-count. otherwise use any 
                        let faceVertexCount =
                            if indexArray <> null then
                                indexArray.Length
                            else
                                vertexCount / 3

                        // create a partial SceneGraph containing only the information
                        // provided by the Mesh itself. Note that this SceneGraph does not 
                        // include Surfaces/Textures/etc. but will automatically inherit those
                        // attributes from its containing scope.
                        let sg = 
                            Sg.VertexAttributeApplicator(attributes,
                                Sg.RenderNode(
                                    DrawCallInfo(
                                        Mode = IndexedGeometryMode.TriangleList,
                                        FaceVertexCount = faceVertexCount,
                                        InstanceCount = 1
                                    )
                                )
                            ) :> ISg

                        let sg =
                            if indexArray <> null then
                                Sg.VertexIndexApplicator(Mod.initConstant (indexArray :> Array), sg) :> ISg
                            else
                                sg

                        // if a diffuse texture was found apply it using the
                        // standard SceneGraph
                        let sg = 
                            match diffuseTexture with
                                | Some tex ->
                                    sg |> Sg.texture DefaultSemantic.DiffuseColorTexture tex
                                | None ->
                                    sg

                        cache.Add(m, sg)
                        sg
                    else
                        let sg = Sg.group' []
                        cache.Add(m, sg)
                        sg

        // since Meshes need to be converted to RenderJobs somehow we
        // define a utility-function performing this transformation.
        // Note that RenderJobs cannot be cached per Mesh since they
        //      can differ when seen in different paths
        let toRenderJobs (m : Mesh) =
            (toSg m).RenderJobs()

        // another utility function for converting
        // transformation matrices
        let toTrafo (m : Matrix4x4) =
            let m = 
                M44d(
                    float m.A1, float m.B1, float m.C1, float m.D1,
                    float m.A2, float m.B2, float m.C2, float m.D2,
                    float m.A3, float m.B3, float m.C3, float m.D3,
                    float m.A4, float m.B4, float m.C4, float m.D4
                )

            Trafo3d(m, m.Inverse)

        // when the attribute is not defined for the scene itself
        // we simply use the current directory as working directory
        member x.WorkDirectory(scene : Scene) =
            scene.AllChildren?WorkDirectory <- System.Environment.CurrentDirectory

        // since the Assimp-Nodes need to be aware of their
        // enclosing Scene (holding Meshes/Textures/etc.) we
        // simply define an inherited attribute for passing it down
        // the tree
        member x.AssimpScene(scene : Scene) =
            scene.AllChildren?AssimpScene <- scene

        // the inherited attribute ModelTrafo will be modified
        // by some of Assimp's nodes and must therefore be defined here.
        // Note that this code is quite compilcated since we want to efficiently
        //      filter out identity trafos here and also want to be aware of the system's 
        //      overall root-trafo (which is Identity too)
        member x.ModelTrafo (n : Node) =
            let p = n?ModelTrafo
            let mine = n.Transform |> toTrafo

            // in general the following code would be sufficient (but not optimal):
            // n.AllChildren?ModelTrafo <- Mod.map (fun t -> t * mine) p

            if mine.Forward.IsIdentity(Constant.PositiveTinyValue) then
                n.AllChildren?ModelTrafo <- p
            else
                if p = Aardvark.SceneGraph.Semantics.TrafoSem.RootTrafo then
                    n.AllChildren?ModelTrafo <- Mod.initConstant mine
                else
                    n.AllChildren?ModelTrafo <- Mod.map (fun t -> t * mine) p
        

        // here we define the RenderJobs semantic for the Assimp-Scene
        // which directly queries RenderJobs from its contained Scene-Root
        member x.RenderJobs(scene : Scene) : aset<RenderJob> =
            scene.RootNode?RenderJobs()

        // here we define the RenderJobs semantic for Assimp's Nodes
        // which basically enumerates all directly contained 
        // Geometries and recursively yields all child-renderjobs
        member x.RenderJobs(n : Node) : aset<RenderJob> =
            aset {
                // get the inherited Scene attribute (needed for Mesh lookups here)
                let scene = n.Scene

                // enumerate over all meshes and yield their 
                // RenderJobs (according to the current scope)
                for i in n.MeshIndices do
                    let mesh = scene.Meshes.[i]
                    
                    yield! toRenderJobs mesh

                // recursively yield all child-renderjobs
                for c in n.Children do
                    yield! c.RenderJobs()

            }

        member x.LocalBoundingBox(s : Scene) : IMod<Box3d> =
            s.RootNode.LocalBoundingBox()

        member x.LocalBoundingBox(n : Node) : IMod<Box3d> =
            adaptive {
                let scene = n.Scene
                let meshes = n.MeshIndices |> Seq.map (fun i -> scene.Meshes.[i]) |> Seq.toList
                let trafo = toTrafo n.Transform

                let box = Box3d(meshes |> Seq.collect (fun m -> m.Vertices |> Seq.map (fun v -> V3d(v.X, v.Y, v.Z))))
                let childBoxes = Box3d(n.Children |> Seq.map (fun c -> c.LocalBoundingBox().GetValue()))

                let overall = Box3d [box; childBoxes]
                return overall.Transformed(trafo)
            }


    let private ctx =  new AssimpContext()

    // define a default checkerboard texture
    let private defaultTexture =
        let image = PixImage<byte>(Col.Format.RGBA, 128, 128, 4)

        image.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 32L
            if (c.X + c.Y) % 2L = 0L then
                C4b.VRVisGreen
            else
                C4b.White
        ) |> ignore

        let tex =
            PixTexture2d(PixImageMipMap [|image :> PixImage|], true) :> ITexture

        Mod.initConstant tex


    // a scene can simply be loaded using assimp.
    // Due to our semantic-definitions above we may simply use
    // it in our SceneGraph (which allows extensibility through its AdapterNode)
    let load (file : string) =
        let scene = ctx.ImportFile(file, PostProcessSteps.Triangulate ||| PostProcessSteps.FixInFacingNormals ||| PostProcessSteps.GenerateSmoothNormals)

        // the attribute system can also be used to extend objects
        // with "properties" wich are scope independent.
        scene?WorkDirectory <- System.IO.Path.GetDirectoryName(file)

        Sg.AdapterNode(scene) |> Sg.diffuseTexture defaultTexture


let quadSg =
    let quad =
        let index = [|0;1;2; 0;2;3|]
        let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

        IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array], SymDict.empty)


    quad |> Sg.ofIndexedGeometry



module Shader =
    open FShade

    type Vertex = 
        { 
            [<Position>]        pos     : V4d 
            [<Normal>]          n       : V3d
            [<TexCoord>]        tc      : V2d
            [<WorldPosition>]   wp      : V4d
        }

    let vs (v : Vertex)  = 
        vertex {
            let wp = uniform.ModelTrafo * v.pos

            return 
                { v with 
                    pos = uniform.ViewProjTrafo * wp; 
                    n = uniform.NormalMatrix * v.n; 
                    wp = wp 
                }
        }   

    let diffuseTex = 
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }

    let ps (v : Vertex) =
        fragment {

            let c = uniform.CameraLocation - v.wp.XYZ |> Vec.normalize
            let n = v.n |> Vec.normalize

            let c = abs (Vec.dot c n) * 0.8 + 0.2

            let color = diffuseTex.Sample(v.tc)

            return V4d(c * color.XYZ, color.W)
        }


    let effect = compose [toEffect vs; toEffect ps]

[<AutoOpen>]
module Main =
    type Trafo3d with
        static member ChangeYZ =
            let fw =
                M44d(1.0,0.0,0.0,0.0,
                     0.0,0.0,-1.0,0.0,
                     0.0,1.0,0.0,0.0,
                     0.0,0.0,0.0,1.0)

            Trafo3d(fw, fw)


open System.Threading

let timePulled = new ManualResetEventSlim()
let timeLock = obj()
let time = Mod.custom(fun () -> DateTime.Now)
//
//
//let t = 
//    new Thread(ThreadStart(fun _ ->
//        while true do
//            timePulled.Wait()
//            if not time.OutOfDate then
//                lock timeLock (fun () ->
//                    transact (fun () ->
//                        time.MarkOutdated()
//                    )
//                )
//    ))
////    new Timer(TimerCallback(fun _ ->
////            if not time.OutOfDate then
////                transact (fun () ->
////                    time.MarkOutdated()
////                )
////
////    ), null, 0, 5)


let withLastValue (m : IMod<'a>) =
    let lastValue = ref None
    adaptive {
        let! newValue = m
        let t = !lastValue, newValue
        lastValue := Some newValue
        return t
    }

let inline differentiate (m : IMod< ^a >) =
    let lastValue = ref None

    adaptive {
        let! newValue = m
        match !lastValue with
            | Some last ->
                let d = newValue - last
                lastValue := Some newValue
                return last, Some d
            | None ->
                lastValue := Some newValue
                return newValue, None
    }


let timeTest() =
    
    let down = Mod.initMod false

    let pos = ref 0.0

    let dt = differentiate time

    let move =
        adaptive {
            let! d = down
            if d then
                let! (t,dt) = differentiate time
                match dt with
                    | Some dt ->
                        pos := !pos + dt.TotalSeconds
                        return !pos
                    | None ->
                        return !pos
            else
                return !pos
        }

    let renderSem = new SemaphoreSlim(1)
    let rendering =
        new Thread(ThreadStart(fun _ ->
            while true do
                renderSem.Wait()
                printfn "pull: %A" (Mod.force move)
                transact (fun () -> time.MarkOutdated())
        ), IsBackground = true)

    rendering.Start()
    
    let d = move.AddMarkingCallback(fun () -> renderSem.Release() |> ignore)
//
//    let d = move |> Mod.registerCallback (fun t ->
//        printfn "pull: %A" t
//    )
    
    while true do
        Console.ReadLine() |> ignore
        transact (fun () ->
            Mod.change down <| not down.Value
        )
    d.Dispose()



type IEvent<'a> with
    member x.BeforeEmit(f : unit -> unit) =
        x.Values.Subscribe(fun _ -> f())

let controlWSAD (view : ICameraView) (keyboard : IKeyboard) (time : IMod<DateTime>) =
    let viewTrafoChanger =
        adaptive {

            let forward = keyboard.IsDown(Keys.W).Mod
            let backward = keyboard.IsDown(Keys.S).Mod
            let right = keyboard.IsDown(Keys.D).Mod
            let left = keyboard.IsDown(Keys.A).Mod

            let moveX =
                let l = left |> Mod.map (fun a -> if a then -V2d.IO else V2d.Zero) 
                let r = right |> Mod.map (fun a -> if a then V2d.IO else V2d.Zero) 
                Mod.map2 (+) l r

            let moveView =
                let f = forward |> Mod.map (fun a -> if a then V2d.OI else V2d.Zero) 
                let b = backward |> Mod.map (fun a -> if a then -V2d.OI else V2d.Zero) 
                Mod.map2 (+) f b

            let! move = Mod.map2 (+) moveX moveView
            if move <> V2d.Zero then
                let! (t, dt) = differentiate time
                match dt with
                    | Some dt -> 
                        let moveVec = view.Right * move.X + view.Forward * move.Y
                        view.Location <- view.Location + moveVec * dt.TotalSeconds * 1.2

                    | None -> ()
        }

    viewTrafoChanger.AddOutput(view.ViewTrafos.Mod)
    let d = viewTrafoChanger |> Mod.registerCallback id

    { new IDisposable with
        member x.Dispose() = 
            viewTrafoChanger.RemoveOutput(view.ViewTrafos.Mod)
            d.Dispose()
    }

type LazyCont<'a, 'r> = { runCont : ('a -> 'r) -> 'r }

type IEvent<'a> with
    member x.SubscribeVolatile(f : 'a -> unit) =
        let self : ref<IDisposable> = ref null
        let live = ref true
        let cb = fun (v : 'a) ->
            self.Value.Dispose()
            f v

        let subscribe() =
            if !live then
                self := x.Values.Subscribe(cb)

        subscribe()

        subscribe, { new IDisposable with member x.Dispose() = live := false; self.Value.Dispose() }

module Event =
    let any (e : list<IEvent>) : IEvent<IEvent> =
        let res = EventSource<IEvent>()
        let disp : ref<list<IDisposable>> = ref []
        disp := e |> List.map (fun e -> e.Values.Subscribe (fun _ -> res.Emit(e); !disp |> List.iter (fun d -> d.Dispose())))
        res :> IEvent<_>

type AsyncBuilder() =
    member x.Bind(e : IEvent<'a>, f : 'a -> Async<'b>) : Async<'b> =
        x.Bind(e.Values, f)

    member x.Bind(t : System.Threading.Tasks.Task<'a>, f : 'a -> Async<'b>) =
        async.Bind(Async.AwaitTask t, f)

    member x.Bind(o : IObservable<'a>, f : 'a -> Async<'b>) =
        async.Bind(x.ReturnFrom o, f)

    member x.Bind(a : Async<'a>, f : 'a -> Async<'b>) =
        async.Bind(a, f)


    member x.Return(v : 'a) = 
        async.Return(v)

    member x.ReturnFrom(o : IObservable<'a>) =
        let a = Async.FromContinuations(fun (success, error, cancel) ->
            let d : ref<IDisposable> = ref null
            d := o.Subscribe(fun v -> d.Value.Dispose(); success v)
        )
        async.ReturnFrom(a)

    member x.ReturnFrom(e : IEvent<'a>) =
        x.ReturnFrom(e.Values)

    member x.ReturnFrom(e : System.Threading.Tasks.Task<'a>) =
        x.ReturnFrom(Async.AwaitTask e)

    member x.ReturnFrom(a : Async<'a>) =
        async.ReturnFrom(a)

    member x.Combine(l : Async<unit>, r : Async<'a>) =
        async.Combine(l,r)

    member x.Zero () =
        async.Zero()

    member x.Delay(f : unit -> Async<'a>) =
        async.Delay(f)

    member x.While(guard : unit -> bool, body : Async<unit>) =
        async.While(guard, body)

    member x.For(s : seq<'a>, f : 'a -> Async<unit>) =
        async.For(s, f)

    member x.TryWith(e : Async<'a>, ex : exn -> Async<'a>) =
        async.TryWith(e, ex)

    member x.TryFinally(e : Async<'a>, f : unit -> unit) =
        async.TryFinally(e, f)

let async = AsyncBuilder()

let cc (view : ICameraView) (keyboard : IKeyboard) (time : IMod<DateTime>) =
    let time = time.Event
    let l = keyboard.IsDown(Keys.A)
    let r = keyboard.IsDown(Keys.D)

    async {
        while true do
            let! _ = Event.any [l; r]
            
            if l.Latest || r.Latest then
                printfn "start"
                let start = ref DateTime.Now
                while l.Latest || r.Latest do
                    let! time = time
                    let dt = time - !start
                    printfn "%Ams" dt.TotalMilliseconds
                    start := time

                    let delta = 
                        match l.Latest, r.Latest with
                            | true, false -> -1.0
                            | false, true -> 1.0
                            | _ -> 0.0
                    view.Location <- view.Location + view.Right * delta * dt.TotalSeconds * 1.2
                printfn "stop"
    }


[<EntryPoint>]
[<STAThread>]
let main args = 
    //timeTest()

    let modelPath = match args |> Array.toList with
                      | []     -> printfn "using default eigi model."; System.IO.Path.Combine( __SOURCE_DIRECTORY__, "eigi", "eigi.dae") 
                      | [path] -> printfn "using path: %s" path; path
                      | _      -> failwith "usage: Demo.exe | Demo.exe modelPath"
    
    DynamicLinker.tryUnpackNativeLibrary "Assimp" |> ignore
    Aardvark.Init()

    use app = new OpenGlApplication()
    let f = app.CreateSimpleRenderWindow()
    let ctrl = f.Control

//    ctrl.Mouse.Events.Values.Subscribe(fun e ->
//        match e with
//            | MouseDown p -> printfn  "down: %A" p.location.Position
//            | MouseUp p -> printfn  "up: %A" p.location.Position
//            | MouseClick p -> printfn  "click: %A" p.location.Position
//            | MouseDoubleClick p -> printfn  "doubleClick: %A" p.location.Position
//            | MouseMove p -> printfn  "move: %A" p.Position
//            | MouseScroll(delta,p) -> printfn  "scroll: %A" delta
//            | MouseEnter p -> printfn  "enter: %A" p.Position
//            | MouseLeave p -> printfn  "leave: %A" p.Position
//    ) |> ignore

    let view = CameraViewWithSky(Location = V3d(2.0,2.0,2.0), Forward = -V3d.III.Normalized)
    let proj = CameraProjectionPerspective(60.0, 0.1, 100.0, float ctrl.Sizes.Latest.X / float ctrl.Sizes.Latest.Y)
    let mode = Mod.initMod FillMode.Fill



//    let moveX =
//        let l = left |> Mod.map (fun a -> if a then -1.0 else 0.0) 
//        let r = right |> Mod.map (fun a -> if a then 1.0 else 0.0) 
//        Mod.map2 (+) l r
//
//    let moveView =
//        let f = forward |> Mod.map (fun a -> if a then 1.0 else 0.0) 
//        let b = backward |> Mod.map (fun a -> if a then -1.0 else 0.0) 
//        Mod.map2 (+) f b


    //ctrl.Time |> Mod.registerCallback (fun t -> printfn "%A" t) |> ignore

//    use cc = WinForms.addCameraController w view
//    WinForms.addFillModeController w mode

    let sg = Assimp.load modelPath

    let normalizeTo (target : Box3d) (sg : ISg) =
        let source = sg.LocalBoundingBox().GetValue()
        
        let sourceSize = source.Size
        let scale =
            if sourceSize.MajorDim = 0 then target.SizeX / sourceSize.X
            elif sourceSize.MajorDim = 1 then target.SizeY / sourceSize.Y
            else target.SizeZ / sourceSize.Z



        let trafo = Trafo3d.Translation(-source.Center) * Trafo3d.Scale(scale) * Trafo3d.Translation(target.Center)

        sg |> Sg.trafo (Mod.initConstant trafo)

    //let d = controlWSAD view ctrl.Keyboard ctrl.Time

    controlWSAD view ctrl.Keyboard ctrl.Time |> ignore

    let sg =
        sg |> Sg.effect [Shader.effect]
           |> Sg.viewTrafo view.ViewTrafos.Mod
           |> Sg.projTrafo proj.ProjectionTrafos.Mod
           |> Sg.trafo (Mod.initConstant <| Trafo3d.ChangeYZ)
           |> Sg.fillMode mode
           |> Sg.blendMode (Mod.initConstant BlendMode.Blend)
           |> normalizeTo (Box3d(-V3d.III, V3d.III))
    

    Demo.AssimpExporter.save @"C:\Users\Schorsch\Desktop\quadScene\eigi.dae" sg


//    let arr = new ModRef<Array> ([|V3f.III|] :> Array)
//
//    let info = arr.Select(fun arr -> DrawCallInfo(FaceVertexCount = arr.Length))
//    let line = 
//        new Sg.VertexAttributeApplicator(
//            SymDict.ofList [
//                DefaultSemantic.Positions, BufferView(ArrayBuffer(arr), typeof<V3f>)
//            ], 
//            new Sg.RenderNode(info)
//        )
// 
//    transact <| fun () ->
//        arr.Value <- [||]
    
    ctrl.Sizes.Values.Subscribe(fun s ->
        let aspect = float s.X / float s.Y
        proj.AspectRatio <- aspect
    ) |> ignore

    let task = app.Runtime.CompileRender(sg.RenderJobs())

    ctrl.RenderTask <- task

    let controller = 
        DefaultCameraControllers(
            HciMouseWinFormsAsync (ctrl.Implementation), 
            HciKeyboardWinFormsAsync (ctrl.Implementation), 
            view,
            isEnabled = EventSource(true)    
        )
//    w.Run()

//    let app = System.Windows.Application()
//    app.Run(f) |> ignore
    System.Windows.Forms.Application.Run(f)

    0
