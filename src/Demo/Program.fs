// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System
open OpenTK
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering.GL
open Aardvark.SceneGraph
open Aardvark.Base.Incremental
open System.Threading.Tasks
open System.Threading


do printfn "abc"

type OpenGlApplication() =
    
    static let mutable appCreated = false

    do OpenTK.Toolkit.Init(new ToolkitOptions(Backend=OpenTK.PlatformBackend.PreferNative)) |> ignore
    let runtime = new Runtime()
    let ctx = new Context(runtime)
    do runtime.Context <- ctx

    member x.Context = ctx
    member x.Runtime = runtime

    member x.Dispose() =
        runtime.Dispose()
        ctx.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

type GameWindow(a : OpenGlApplication) =
    inherit OpenTK.GameWindow()


    let mutable task : Option<IRenderTask> = None
    let fbo = new Framebuffer(a.Context, (fun _ -> 0), ignore, [], None)

    do base.VSync <- VSyncMode.Off
       base.Context.MakeCurrent(null)
    let ctx = ContextHandle(base.Context, base.WindowInfo)

    member x.RenderTask 
        with get() = task.Value
        and set v = task <- Some v

    override x.OnRenderFrame(e) =
        using (a.Context.RenderingLock ctx) (fun _ ->
            fbo.Size <- V2i(x.ClientSize.Width, x.ClientSize.Height)

            GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
            GL.ClearColor(0.0f,1.0f,0.0f,0.0f)
            GL.ClearDepth(1.0)
            GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)
        
            match task with
                | Some t -> t.Run(fbo) |> ignore
                | _ -> ()

            x.SwapBuffers()
        )

module WinForms = 
    open System.Windows.Forms
    open System.Collections.Concurrent
    open System.Threading
    open System.Diagnostics

    do Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)

    type IControl =
        abstract member Paint : unit -> unit
        abstract member Invoke : (unit -> unit) -> unit

    type RunningMean(maxCount : int) =
        let values = Array.zeroCreate maxCount
        let mutable index = 0
        let mutable count = 0
        let mutable sum = 0.0

        member x.Add(v : float) =
            let newSum = 
                if count < maxCount then 
                    count <- count + 1
                    sum + v
                else 
                    sum + v - values.[index]

            sum <- newSum
            values.[index] <- v
            index <- (index + 1) % maxCount
              
        member x.Average =
            if count = 0 then 0.0
            else sum / float count  

    type Periodic(interval : int, f : float -> unit) =
        let times = RunningMean(100)
        let sw = Stopwatch()

        member x.RunIfNeeded() =
            if not sw.IsRunning then
                sw.Start()
            else
                let dt = sw.Elapsed.TotalMilliseconds
               
                if interval = 0 || dt >= float interval then
                    times.Add dt
                    sw.Restart()
                    f(times.Average / 1000.0)
                

    type MessageLoop() as this =

        let q = ConcurrentBag<IControl>()
        let mutable timer : Timer = null
        let periodic = ConcurrentHashSet<Periodic>()

        let rec processAll() =
            match q.TryTake() with
                | (true, ctrl) ->
                    ctrl.Invoke (fun () -> ctrl.Paint())
                    processAll()

                | _ -> ()

        member private x.Process() =
            Application.DoEvents()
            for p in periodic do p.RunIfNeeded()
            processAll()

        member x.Start() =
            if timer <> null then
                timer.Dispose()

            timer <- new Timer(TimerCallback(fun _ -> this.Process()), null, 0L, 2L)

        member x.Draw(c : IControl) =
            q.Add c 

        member x.EnqueuePeriodic (f : float -> unit, intervalInMilliseconds : int) =
            let p = Periodic(intervalInMilliseconds, f)
            periodic.Add p |> ignore

            { new IDisposable with
                member x.Dispose() =
                    periodic.Remove p |> ignore
            }
            
        member x.EnqueuePeriodic (f : float -> unit) =
            x.EnqueuePeriodic(f, 1)
                        
                

    type OpenGlControl(ctx : Context, samples : int) =

        inherit GLControl(
            Graphics.GraphicsMode(
                OpenTK.Graphics.ColorFormat(Config.BitsPerPixel), 
                Config.DepthBits, 
                Config.StencilBits, 
                samples, 
                OpenTK.Graphics.ColorFormat.Empty,
                Config.Buffers, 
                false
            ), 
            Config.MajorVersion, 
            Config.MinorVersion, 
            Config.ContextFlags, 
            VSync = false
        )

        static let messageLoop = MessageLoop()
        static do messageLoop.Start()


        let mutable loaded = false
        let sizes = EventSource<V2i>(V2i(base.ClientSize.Width, base.ClientSize.Height))
        let statistics = EventSource<FrameStatistics>(FrameStatistics.Zero)

        let mutable task : Option<IRenderTask> = None
        let mutable taskSubscription : IDisposable = null

        let mutable contextHandle : ContextHandle = null //ContextHandle(base.Context, base.WindowInfo)
        let defaultFramebuffer = new Framebuffer(ctx, (fun _ -> 0), ignore, [], None)

        let afterRender = Microsoft.FSharp.Control.Event<unit>()
        let keyDown = Microsoft.FSharp.Control.Event<KeyEventArgs>()

        

        static member MessageLoop = messageLoop

        [<CLIEvent>]
        member x.AfterRender = afterRender.Publish

        [<CLIEvent>]
        member x.KeyDown = keyDown.Publish


        override x.OnPreviewKeyDown(e) =
            if e.KeyData = (Keys.Alt ||| Keys.Menu) ||
               e.KeyData = (Keys.Alt ||| Keys.Menu ||| Keys.Control) ||
               e.KeyData = (Keys.ShiftKey ||| Keys.Shift) ||
               e.KeyData = Keys.LWin || e.KeyData = Keys.RWin
            then
                keyDown.Trigger(KeyEventArgs(e.KeyData))


        override x.OnKeyDown(e) =
            keyDown.Trigger(e)
            e.SuppressKeyPress <- true
            e.Handled <- true

        interface IControl with
            member x.Paint() =
                use g = x.CreateGraphics()
                use e = new PaintEventArgs(g, x.ClientRectangle)
                x.InvokePaint(x, e)

            member x.Invoke f =
                base.Invoke (new System.Action(f)) |> ignore

        member private x.ForceRedraw() =
            messageLoop.Draw x
        
//        override x.OnPreviewKeyDown(e) =
//            base.OnPreviewKeyDown(e)
//
//            
//            if e.KeyCode = Keys.R then
//                x.ForceRedraw()


        member x.Size =
            V2i(base.ClientSize.Width, base.ClientSize.Height)


        member x.Sizes =
            sizes :> IEvent<_>

        member x.Statistics =
            statistics :> IEvent<_>

        member x.RenderTask
            with get() = task.Value
            and set t =
                match task with
                    | Some old -> 
                        if taskSubscription <> null then taskSubscription.Dispose()
                        old.Dispose()
                    | None -> ()

                task <- Some t
                taskSubscription <- t.AddMarkingCallback x.ForceRedraw

        

        override x.OnHandleCreated(e) =
            let c = OpenTK.Graphics.GraphicsContext.CurrentContext
            if c <> null then
                c.MakeCurrent(null)

            ContextHandle.primaryContext.MakeCurrent()
            base.OnHandleCreated(e)
            loaded <- true
            base.MakeCurrent()
            
        override x.OnResize(e) =
            if loaded then
                base.OnResize(e)
                sizes.Emit(V2i(base.ClientSize.Width, base.ClientSize.Height))

        override x.OnPaint(e) =
            if loaded then
                if contextHandle = null then
                    contextHandle <- ContextHandle(base.Context, base.WindowInfo) 

                match task with
                    | Some t ->
                        using (ctx.RenderingLock contextHandle) (fun _ ->
                            //lock changePropagationLock (fun () ->
                                defaultFramebuffer.Size <- x.Size
                                GL.Viewport(0,0,x.ClientSize.Width, x.ClientSize.Height)
                                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f)
                                GL.ClearDepth(1.0)
                                GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit)

                                let res = t.Run(defaultFramebuffer)

                                statistics.Emit res.Statistics

                                x.SwapBuffers()
                                afterRender.Trigger ()
                            //)
                        )
                    | None ->
                        ()

    type Window(app : OpenGlApplication, samples : int) as this =
        inherit Form()

        let title = "WinForms Window"

        let ctrl = new OpenGlControl(app.Context, samples, Dock = DockStyle.Fill)
        do base.Controls.Add(ctrl)
           base.Width <- 1024
           base.Height <- 768
           base.Text <- title + " (0 frames rendered)"

           let frames = ref 0
           ctrl.AfterRender.Add(fun () ->
            frames := !frames + 1
            this.Text <- title + sprintf " (%d frames rendered)" !frames
            
           )


        member x.Run() =
            Application.Run x

        member x.Control = ctrl

        member x.Sizes = ctrl.Sizes
        member x.Statistics = ctrl.Statistics
        member x.RenderTask 
            with get() = ctrl.RenderTask
            and set t = ctrl.RenderTask <- t

        new(app) = new Window(app, 1)

    type CameraController(ctrl : OpenGlControl, view : ICameraView) as this =
        
        let speed = 1.5

        let mutable s : IDisposable = null //OpenGlControl.MessageLoop.EnqueuePeriodic(this.Update, 5)

        do ctrl.KeyDown.Add(fun e -> this.KeyDown e.KeyCode)
           ctrl.KeyUp.Add(fun e -> this.KeyUp e.KeyCode)
           ctrl.MouseDown.Add(fun e -> this.MouseDown(e.Button, V2i(e.X, e.Y)))
           ctrl.MouseUp.Add(fun e -> this.MouseUp(e.Button, V2i(e.X, e.Y)))
           ctrl.MouseMove.Add(fun e -> this.MouseMove(V2i(e.X, e.Y)))
           ctrl.MouseWheel.Add(fun e -> this.MouseWheel(e.Delta))

        let mutable left = false
        let mutable right = false
        let mutable forward = false
        let mutable backward = false

        let mutable leftDown = false
        let mutable rightDown = false
        let mutable middleDown = false

        let mutable lastMousePosition = V2i.Zero

        let mutable targetZoom = 0.0
        let mutable currentZoom = 0.0

        let (<+>) (a : Option<V3d>) (b : Option<V3d>) =
            match a,b with
                | Some a, Some b -> Some (a + b)
                | Some a, _ -> Some a
                | _, Some b -> Some b
                | _ -> None

        member x.MouseDown(m : MouseButtons, pos : V2i) =
            lastMousePosition <- pos
            match m with
                | MouseButtons.Left -> leftDown <- true
                | MouseButtons.Right -> rightDown <- true
                | MouseButtons.Middle -> middleDown <- true
                | _ -> ()

        member x.MouseUp(m : MouseButtons, pos : V2i) =
            lastMousePosition <- pos
            match m with
                | MouseButtons.Left -> leftDown <- false
                | MouseButtons.Right -> rightDown <- false
                | MouseButtons.Middle -> middleDown <- false
                | _ -> ()

        member x.MouseMove(pos : V2i) =
            let delta = pos - lastMousePosition 

            transact (fun () ->
                if leftDown then
                    let r = view.Right
                    let u = view.Up


                    let t = 
                        M44d.Rotation(u, -0.008 * float delta.X) *
                        M44d.Rotation(r, -0.008 * float delta.Y)

                    view.Forward <- t.TransformDir view.Forward
  
                elif middleDown then
                    let u = view.Up
                    let r = view.Right

                    view.Location <- view.Location + 
                        u * -(0.02 * speed) * float delta.Y +
                        r * 0.02 * speed * float delta.X
                elif rightDown then
                    let fw = view.Forward


                    view.Location <- view.Location + 
                        fw * -0.02 * speed * float delta.Y
            )


            lastMousePosition <- pos

        member x.MouseWheel(delta : int) =
            targetZoom <- targetZoom + float delta / 120.0

        member x.KeyUp(k : Keys) =
            match k with
                | Keys.W -> forward <- false
                | Keys.S -> backward <- false
                | Keys.A -> left <- false
                | Keys.D -> right <- false
                | _ -> ()

        member x.KeyDown(k : Keys) =
            match k with
                | Keys.W -> forward <- true
                | Keys.S -> backward <- true
                | Keys.A -> left <- true
                | Keys.D -> right <- true
                | _ -> ()

        member x.Update(elapsedSeconds : float) =
            let mutable delta = V3d.Zero

            let deltaRight =
                match left, right with
                    | (true, false) -> Some (-V3d.IOO * speed * elapsedSeconds)
                    | (false, true) -> Some (V3d.IOO * speed * elapsedSeconds)
                    | _ -> None

            let deltaForward =
                match forward, backward with
                    | (true, false) -> Some (-V3d.OOI * speed * elapsedSeconds)
                    | (false, true) -> Some (V3d.OOI * speed * elapsedSeconds)
                    | _ -> None

            let deltaZoom =
                let diff = targetZoom - currentZoom
                if abs diff > 0.05 then
                    let step = 4.0 * diff * elapsedSeconds

                    currentZoom <- currentZoom + step
                    
                    if sign (targetZoom - currentZoom) <> sign diff then
                        currentZoom <- targetZoom
                        Some (-V3d.OOI * (targetZoom - currentZoom))
                    else
                        Some (-V3d.OOI * step)

                else
                    None

            let delta = deltaRight <+> deltaForward <+> deltaZoom

            match delta with
                | Some delta ->
                    transact (fun () ->
                        let delta =  view.ViewTrafo.Backward.TransformDir delta
                        view.Location <- view.Location + delta
                    )
                | None ->
                    ()

        member x.Start() =
            if s <> null then
                s.Dispose()

            s <- OpenGlControl.MessageLoop.EnqueuePeriodic(this.Update, 1000 / 120)

        member x.Dispose() =
            if s <> null then
                s.Dispose()
                s <- null

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let addCameraController (w : Window) (view : ICameraView) =
        let cc = new CameraController(w.Control, view)
        cc.Start()
        cc :> IDisposable

    let addFillModeController (w : Window) (fillMode : ModRef<FillMode>) =
        w.Control.KeyDown.Add (fun e ->
            if e.KeyCode = Keys.F && (e.Modifiers &&& Keys.Alt) <> Keys.None then
                
                let newMode = 
                    match fillMode.Value with
                        | FillMode.Fill -> FillMode.Line
                        | FillMode.Line -> FillMode.Point
                        | _ -> FillMode.Fill

                transact (fun () -> fillMode.Value <- newMode)
                
                
            ()
        )







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


let quadSg=
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

type Trafo3d with
    static member ChangeYZ =
        let fw =
            M44d(1.0,0.0,0.0,0.0,
                 0.0,0.0,-1.0,0.0,
                 0.0,1.0,0.0,0.0,
                 0.0,0.0,0.0,1.0)

        Trafo3d(fw, fw)

[<EntryPoint>]
let main argv = 
    printfn "ABC1"
    Aardvark.Init()
    printfn "ABC"


    use app = new OpenGlApplication()
    
    use w = new WinForms.Window(app, 8)
    

    let view = CameraViewWithSky(Location = V3d(2.0,2.0,2.0), Forward = -V3d.III.Normalized)
    let proj = CameraProjectionPerspective(60.0, 0.1, 100.0, float w.ClientSize.Width / float w.ClientSize.Height)
    let mode = Mod.initMod FillMode.Fill

    use cc = WinForms.addCameraController w view
    WinForms.addFillModeController w mode

    //let sg = Assimp.load "/home/schorsch/Downloads/scifi/Scifi downtown city.obj" //
    let sg = Assimp.load @"E:\Development\WorkDirectory\scifi\Scifi downtown city.obj"

    let normalizeTo (target : Box3d) (sg : ISg) =
        let source = sg.LocalBoundingBox().GetValue()
        
        let sourceSize = source.Size
        let scale =
            if sourceSize.MajorDim = 0 then target.SizeX / sourceSize.X
            elif sourceSize.MajorDim = 1 then target.SizeY / sourceSize.Y
            else target.SizeZ / sourceSize.Z



        let trafo = Trafo3d.Translation(-source.Center) * Trafo3d.Scale(scale) * Trafo3d.Translation(target.Center)

        sg |> Sg.trafo (Mod.initConstant trafo)


    let sg =
        sg |> Sg.effect [Shader.effect]
           |> Sg.viewTrafo view.ViewTrafos.Mod
           |> Sg.projTrafo proj.ProjectionTrafos.Mod
           |> Sg.trafo (Mod.initConstant <| Trafo3d.ChangeYZ)
           |> Sg.fillMode mode
           |> normalizeTo (Box3d(-V3d.III, V3d.III))
    
    
    w.Sizes.Values.Subscribe(fun s ->
        let aspect = float s.X / float s.Y
        proj.AspectRatio <- aspect
    ) |> ignore




    let renderJobs = sg.RenderJobs()
    let task = app.Runtime.CompileRender(renderJobs)

    w.RenderTask <- task
    w.Run()


    0
