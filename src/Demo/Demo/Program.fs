open System
open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering.GL
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.CSharp
open Aardvark.Base.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.SceneGraph
open Aardvark.SceneGraph.CSharp
open Aardvark.SceneGraph.Semantics

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

        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()

        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()

    type Scene with
        member x.LocalBoundingBox() : IMod<Box3d> = x?LocalBoundingBox()
        member x.RenderObjects() : aset<IRenderObject> = x?RenderObjects()


    // in order to integrate Assimo's scene structure we
    // need to define several attributes for it (namely RenderObjects, ModelTrafo, etc.)
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
            BufferView(data |> Mod.map (fun data -> ArrayBuffer data :> IBuffer), typeof<'b>)

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
                            try 
                                let tex = FileTexture(path, true)
                                let m = Mod.constant (tex :> ITexture)
                                textureCache.[path] <- m
                                Some m
                            with _ ->
                                let tex = NullTexture()
                                let m = Mod.constant (tex :> ITexture)
                                textureCache.[path] <- m
                                Some m
                else
                    None
            else 
                None


        
        // toSg is used by toRenderObjects in order to simplify 
        // things here.
        // Note that it would also be possible here to create RenderObjects 
        //      directly. However this is very wordy and we therefore create
        //      a partial SceneGraph for each mesh
        let toSg (m : Mesh) =
            match cache.TryGetValue m with
                | (true, sg) -> 
                    sg
                | _ ->
                    if m.HasFaces && m.FaceCount > 0 && m.PrimitiveType = PrimitiveType.Triangle then
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
                                    if m.Normals.Count = m.Vertices.Count then
                                        yield DefaultSemantic.Normals, m.Normals |> mapAttribute (fun v -> V3f(v.X, v.Y, v.Z))
                                    else
                                        yield DefaultSemantic.Normals, BufferView(Mod.constant (NullBuffer(V4f.OOOO) :> IBuffer), typeof<V4f>)
                                
                                if m.TextureCoordinateChannelCount > 0 then
                                    let tc = m.TextureCoordinateChannels.[0]
                                    yield DefaultSemantic.DiffuseColorCoordinates, tc |> mapAttribute (fun v -> V2f(v.X, v.Y))

                            ]

//                        for (KeyValue(k,att)) in attributes do
//                            printfn "%s: %A" (k.ToString()) (Mod.force att.Buffer |> unbox<ArrayBuffer>).Data.Length
//
//                    
                        // try to find the Mesh's diffuse texture
                        let diffuseTexture = tryFindDiffuseTexture m

                        
                        // if the mesh is indexed use its index for determinig the
                        // total face-vertex-count. otherwise use any 
                        let faceVertexCount =
                            if indexArray <> null then
                                indexArray.Length
                            else
                                vertexCount

                        if faceVertexCount = 0 || faceVertexCount % 3 <> 0 then
                            let sg = Sg.group' []
                            cache.Add(m, sg)
                            sg
                        else

                            // create a partial SceneGraph containing only the information
                            // provided by the Mesh itself. Note that this SceneGraph does not 
                            // include Surfaces/Textures/etc. but will automatically inherit those
                            // attributes from its containing scope.
                            let sg = 
                                Sg.VertexAttributeApplicator(attributes,
                                    Sg.RenderNode(
                                        DrawCallInfo(
                                            FaceVertexCount = faceVertexCount,
                                            InstanceCount = 1
                                        ),
                                        IndexedGeometryMode.TriangleList
                                    )
                                ) :> ISg

                            let sg =
                                if indexArray <> null then
                                    Sg.VertexIndexApplicator(Mod.constant (indexArray :> Array), sg) :> ISg
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

        // since Meshes need to be converted to RenderObjects somehow we
        // define a utility-function performing this transformation.
        // Note that RenderObjects cannot be cached per Mesh since they
        //      can differ when seen in different paths
        let toRenderObjects (m : Mesh) =
            (toSg m).RenderObjects()

        // another utility function for converting
        // transformation matrices
        let toTrafo (m : Matrix4x4) =
            let m = 
                M44d(
                    float m.A1, float m.A2, float m.A3, float m.A4,
                    float m.B1, float m.B2, float m.B3, float m.B4,
                    float m.C1, float m.C2, float m.C3, float m.C4,
                    float m.D1, float m.D2, float m.D3, float m.D4
                )
//                M44d(
//                    float m.A1, float m.B1, float m.C1, float m.D1,
//                    float m.A2, float m.B2, float m.C2, float m.D2,
//                    float m.A3, float m.B3, float m.C3, float m.D3,
//                    float m.A4, float m.B4, float m.C4, float m.D4
//                )

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
        member x.ModelTrafoStack (n : Node) =
            let p : list<IMod<Trafo3d>> = n?ModelTrafoStack
            let mine = n.Transform |> toTrafo

            // in general the following code would be sufficient (but not optimal):
            // n.AllChildren?ModelTrafo <- Mod.map (fun t -> t * mine) p

            if mine.Forward.IsIdentity(Constant.PositiveTinyValue) then
                n.AllChildren?ModelTrafoStack <- p
            else
                n.AllChildren?ModelTrafoStack <- (Mod.constant mine)::p

        member x.ModelTrafo(e : Node) : IMod<Trafo3d> =
            let sg = Sg.set (ASet.empty)
            sg?ModelTrafo()

        // here we define the RenderObjects semantic for the Assimp-Scene
        // which directly queries RenderObjects from its contained Scene-Root
        member x.RenderObjects(scene : Scene) : aset<IRenderObject> =
            scene.RootNode?RenderObjects()

        // here we define the RenderObjects semantic for Assimp's Nodes
        // which basically enumerates all directly contained 
        // Geometries and recursively yields all child-renderjobs
        member x.RenderObjects(n : Node) : aset<IRenderObject> =
            aset {
                // get the inherited Scene attribute (needed for Mesh lookups here)
                let scene = n.Scene

                // enumerate over all meshes and yield their 
                // RenderObjects (according to the current scope)
                for i in n.MeshIndices do
                    let mesh = scene.Meshes.[i]
                    
                    yield! toRenderObjects mesh

                // recursively yield all child-renderjobs
                for c in n.Children do
                    yield! c.RenderObjects()

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
        let image = PixImage<byte>(Col.Format.RGBA, 128L, 128L, 4L)

        image.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 32L
            if (c.X + c.Y) % 2L = 0L then
                C4b.VRVisGreen
            else
                C4b.White
        ) |> ignore

        let tex =
            PixTexture2d(PixImageMipMap [|image :> PixImage|], true) :> ITexture

        Mod.constant tex


    // a scene can simply be loaded using assimp.
    // Due to our semantic-definitions above we may simply use
    // it in our SceneGraph (which allows extensibility through its AdapterNode)
    let load (file : string) =
        let scene = ctx.ImportFile(file, PostProcessSteps.Triangulate ||| PostProcessSteps.FixInFacingNormals ||| 
                                         PostProcessSteps.GenerateSmoothNormals ||| PostProcessSteps.ImproveCacheLocality ||| 
                                         PostProcessSteps.JoinIdenticalVertices ||| PostProcessSteps.SplitLargeMeshes)

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

    let diffuseTex = 
        sampler2d {
            texture uniform?DiffuseColorTexture
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }

    type Vertex = 
        { 
            [<Position>]        pos     : V4d 
            [<Normal>]          n       : V3d
            [<TexCoord>]        tc      : V2d
            [<WorldPosition>]   wp      : V4d
            [<Color>]           color   : V3d
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

    let pvLight (v : Vertex)  = 
        vertex {
            let wp = uniform.ModelTrafo * v.pos

            let id = V3d.III
            return 
                { v with 
                    pos = uniform.ViewProjTrafo * wp; 
                    n = uniform.NormalMatrix * v.n; 
                    wp = wp 
                    color = id * Vec.dot v.n.Normalized id.Normalized
                }
        }   

    let pvFrag (v : Vertex) =
        fragment {
            let color = diffuseTex.Sample(v.tc).XYZ + v.color * 0.001
            return V4d(color, 1.0)
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
let time = Mod.custom(fun _ -> DateTime.Now)
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
    
    let down = Mod.init false

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

            let forward = keyboard.IsDown(Keys.W)
            let backward = keyboard.IsDown(Keys.S)
            let right = keyboard.IsDown(Keys.D)
            let left = keyboard.IsDown(Keys.A)

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
    let d = viewTrafoChanger |> Mod.unsafeRegisterCallbackKeepDisposable id

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


open Aardvark.Base.Incremental.Operators
let testGpuThroughput () =
    use app = new OpenGlApplication()
    let runtime = app.Runtime

    let size = V2i(10000,5000)
    
    let signature = 
        runtime.CreateFramebufferSignature [
            DefaultSemantic.Colors, { format =RenderbufferFormat.Rgba8; samples = 1 }
        ]
    
    let color = runtime.CreateRenderbuffer(size,RenderbufferFormat.R8, 1)
    let outputView = color :> IFramebufferOutput
    let color = runtime.CreateTexture(size,TextureFormat.Rgba8,1,1,1)
    let outputView = { texture = color; level = 0; slice = 0 } :> IFramebufferOutput

    let fbo = runtime.CreateFramebuffer(signature, [(DefaultSemantic.Colors,outputView)] |> Map.ofList)

    let sizex,sizey = 250,200
    let geometry = 
        [| for x in 0 .. sizex - 1 do
            for y in 0 .. sizey - 1 do
                let v0 = V3f(float32 x,float32 y,0.0f) * 2.0f
                let v1 = v0 + V3f.IOO
                let v2 = v0 + V3f.IIO
                let v3 = v0 + V3f.OIO
                let vs = [| v0; v1; v2; v0; v2; v3 |] 
                            |> Array.map (fun x ->
                                (x / V3f(float32 (sizex * 2),float32 (sizey*2),1.0f)) * 2.0f - V3f.IIO
                            )
                yield! vs
        |]
    let geom = IndexedGeometry(Mode = IndexedGeometryMode.TriangleList,IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions,geometry :> System.Array ])

    let sg = Sg.ofIndexedGeometry geom

    let sg = sg |> Sg.effect [ DefaultSurfaces.constantColor C4f.Red |> toEffect ]        
                |> Sg.depthTest ~~DepthTestMode.None
                |> Sg.cullMode ~~CullMode.Clockwise
                |> Sg.blendMode ~~BlendMode.None
                    
    let task = runtime.CompileRender(signature, sg)
    let clear = runtime.CompileClear(signature, ~~C4f.Black,~~1.0)
    
    use token = runtime.Context.ResourceLock
    let sw = System.Diagnostics.Stopwatch()

    task.Run(fbo) |> ignore

    sw.Start()
    let mutable cnt = 0
    while true do
        clear.Run fbo |> ignore
        let r = task.Run(fbo)

        cnt <- cnt + 1
        if cnt = 1000
        then
            cnt <- 0
            printfn "elapsed for 1000 frames: %A [ms]" sw.Elapsed.TotalMilliseconds

            let pi = runtime.Download(color, PixFormat.ByteBGRA)
            //pi.SaveAsImage(@"C:\Aardwork\blub.jpg")
            sw.Restart()
            //System.Environment.Exit 0

    ()



open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open System.Diagnostics
#nowarn "9"

let mycopy (src : nativeint, dst : nativeint, size : int) =
    let mutable src = src
    let mutable dst = dst
    let e = src + nativeint size


    while src < e do
        NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.read (NativePtr.ofNativeInt<int64> src))
        src <- src + 8n
        dst <- dst + 8n

    let bytes = size &&& 7
    if bytes <> 0 then
        src <- src - 8n
        dst <- dst - 8n
        while src < e do
            NativePtr.write (NativePtr.ofNativeInt dst) (NativePtr.read (NativePtr.ofNativeInt<byte> src))
            src <- src + 1n
            dst <- dst + 1n

let compare(size : int) =
    let src = Marshal.AllocHGlobal size
    let dst = Marshal.AllocHGlobal size

    
    mycopy(src, dst, size)
    Marshal.Copy(src, dst, size)

    let sw = Stopwatch()
    sw.Start()
    let mutable iter = 0
    while sw.Elapsed.TotalSeconds < 1.0 do
        mycopy(src, dst, size)
        iter <- iter + 1
    sw.Stop()
    let tmine = sw.Elapsed.TotalMilliseconds / float iter
    printfn "mycopy: %.3fms" (sw.Elapsed.TotalMilliseconds / float iter)


    let sw = Stopwatch()
    sw.Start()
    let mutable iter = 0
    while sw.Elapsed.TotalSeconds < 1.0 do
        Marshal.Copy(src, dst, size)
        iter <- iter + 1
    sw.Stop()
    let tmem = sw.Elapsed.TotalMilliseconds / float iter
    printfn "memcpy: %.3fms" (sw.Elapsed.TotalMilliseconds / float iter)

    printfn "factor: %.3f" (tmine / tmem)


    Marshal.FreeHGlobal(src)
    Marshal.FreeHGlobal(dst)


module NativeTensors =

    type NativeVolume<'a when 'a : unmanaged> =
        struct
            val mutable public DX : nativeint
            val mutable public DY : nativeint
            val mutable public DZ : nativeint
            val mutable public SX : nativeint
            val mutable public SY : nativeint
            val mutable public SZ : nativeint
            val mutable public Origin : nativeptr<'a>

            member inline x.ForEachPtr(f : nativeptr<'a> -> unit) =
            
                let mutable i = NativePtr.toNativeInt x.Origin

                let zs = x.SZ * x.DZ
                let zj = x.DZ - x.SY * x.DY

                let ys = x.SY * x.DY
                let yj = x.DY - x.SX * x.DX

                let xs = x.SX * x.DX
                let xj = x.DX


                let ze = i + zs
                while i <> ze do
                    let ye = i + ys
                    while i <> ye do
                        let xe = i + xs
                        while i <> xe do
                            f (NativePtr.ofNativeInt i) 
                            i <- i + xj

                        i <- i + yj

                    i <- i + zj



                ()

            member inline x.ForEachPtr(other : NativeVolume<'b>, f : nativeptr<'a> -> nativeptr<'b> -> unit) =            
                if x.SX <> other.SX || x.SY <> other.SY || x.SZ <> other.SZ then
                    failwithf "NativeVolume sizes do not match { src = %A; dst = %A }" (x.SX, x.SY, x.SY) (other.SX, other.SY, other.SY)

                let mutable i = NativePtr.toNativeInt x.Origin
                let mutable i1 = NativePtr.toNativeInt other.Origin

                let zs = x.SZ * x.DZ
                let zj = x.DZ - x.SY * x.DY
                let zj1 = other.DZ - other.SY * other.DY

                let ys = x.SY * x.DY
                let yj = x.DY - x.SX * x.DX
                let yj1 = other.DY - other.SX * other.DX

                let xs = x.SX * x.DX
                let xj = x.DX
                let xj1 = other.DX


                let ze = i + zs
                while i <> ze do
                    let ye = i + ys
                    while i <> ye do
                        let xe = i + xs
                        while i <> xe do
                            f (NativePtr.ofNativeInt i) (NativePtr.ofNativeInt i1)
                            i <- i + xj
                            i1 <- i1 + xj1

                        i <- i + yj
                        i1 <- i1 + yj1

                    i <- i + zj
                    i1 <- i1 + zj1



                ()

            new(ptr : nativeptr<'a>, vi : VolumeInfo) =
                let sa = int64 sizeof<'a>
                {
                    DX = nativeint (sa * vi.DX); DY = nativeint (sa * vi.DY); DZ = nativeint (sa * vi.DZ)
                    SX = nativeint vi.SX; SY = nativeint vi.SY; SZ = nativeint vi.SZ
                    Origin = NativePtr.ofNativeInt (NativePtr.toNativeInt ptr + nativeint (sa * vi.Origin))
                }

        end

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module NativeVolume =
        open System.Reflection

        let inline ofNativeInt (info : VolumeInfo) (ptr : nativeint) =
            NativeVolume(NativePtr.ofNativeInt ptr, info)

        let inline iter (f : nativeptr<'a>-> unit) (l : NativeVolume<'a>) =
            l.ForEachPtr(f) 
        
        let inline iter2 (l : NativeVolume<'a>) (r : NativeVolume<'b>) (f : nativeptr<'a> -> nativeptr<'b> -> unit) =
            l.ForEachPtr(r, f) 

        let pin (f : NativeVolume<'a> -> 'b) (pi : PixImage<'a>) : 'b =
            let gc = GCHandle.Alloc(pi.Data, GCHandleType.Pinned)
            let nv = gc.AddrOfPinnedObject() |> ofNativeInt pi.VolumeInfo
            try
                f nv
            finally
                gc.Free()

        let pin2 (l : PixImage<'a>) (r : PixImage<'b>) (f : NativeVolume<'a> -> NativeVolume<'b> -> 'c)  : 'c =
            let lgc = GCHandle.Alloc(l.Data, GCHandleType.Pinned)
            let lv = lgc.AddrOfPinnedObject() |> ofNativeInt l.VolumeInfo

            let rgc = GCHandle.Alloc(r.Data, GCHandleType.Pinned)
            let rv = rgc.AddrOfPinnedObject() |> ofNativeInt r.VolumeInfo

            try
                f lv rv
            finally
                lgc.Free()
                rgc.Free()
  
        type private CopyImpl<'a when 'a : unmanaged>() =
            static member Copy (src : PixImage<'a>, dst : nativeint, dstInfo : VolumeInfo) =
                let dst = NativeVolume(NativePtr.ofNativeInt dst, dstInfo)
                src |> pin (fun src ->
                    iter2 src dst (fun s d -> NativePtr.write d (NativePtr.read s))
                )

        let copy (src : PixImage<'a>) (dst : NativeVolume<'a>) =
            src |> pin (fun src ->
                iter2 src dst (fun s d -> NativePtr.write d (NativePtr.read s))
            )

        let copyRuntime (src : PixImage) (dst : nativeint) (dstInfo : VolumeInfo) =
            let t = typedefof<CopyImpl<byte>>.MakeGenericType [| src.PixFormat.Type |]
            let mi = t.GetMethod("Copy", BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
            mi.Invoke(null, [|src; dst; dstInfo|]) |> ignore


let testImageCopy<'a> (size : V2i) =
    let image = PixImage<'a>(Col.Format.RGBA, size)

    let byteSize = size.X * size.Y * 4 * sizeof<'a>
    let dst = Marshal.AllocHGlobal(byteSize)


    let srcInfo = image.Volume.Info
    let dstInfo = VolumeInfo(srcInfo.Index(V3l(0L, srcInfo.SY-1L,0L)), srcInfo.Size, V3l(srcInfo.DX,-srcInfo.DY,srcInfo.DZ))

    let copyNative() =
        NativeTensors.NativeVolume.copyRuntime image dst dstInfo


    let copyMem() =
        let gc = GCHandle.Alloc(image.Data, GCHandleType.Pinned)
        Marshal.Copy(gc.AddrOfPinnedObject(), dst, byteSize)
        gc.Free()

    copyNative()
    copyMem()

    let sw = Stopwatch()
    sw.Start()
    let mutable iter = 0
    while sw.Elapsed.TotalSeconds < 10.0 do
        copyNative()
        iter <- iter + 1
    sw.Stop()

    let t = sw.Elapsed.TotalMilliseconds / float iter
    printfn "mine: %.3fms" t

    let sw = Stopwatch()
    sw.Start()
    let mutable iter = 0
    while sw.Elapsed.TotalSeconds < 10.0 do
        copyMem()
        iter <- iter + 1
    sw.Stop()

    let t = sw.Elapsed.TotalMilliseconds / float iter
    printfn "memcpy: %.3fms" t


module TriangleSet =
    open System.Collections.Generic
    open Aardvark.Base.Monads.Option

    let private tryGetTrianglesAsMod (ro : IRenderObject) =
        option {
            match ro with
                | :? RenderObject as ro ->

                    
                    let! modelTrafo = ro.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelTrafo")
                    let! positions = ro.VertexAttributes.TryGetAttribute(DefaultSemantic.Positions)
                    let indices = ro.Indices

                    let positionType = positions.ElementType
                    let indexType = if isNull ro.Indices then typeof<int> else indices.GetValue().GetType().GetElementType()
                    
                    let toInt : Array -> int[] = PrimitiveValueConverter.arrayConverter indexType
                    let toV3d : Array -> V3d[] = PrimitiveValueConverter.arrayConverter positionType

                    let triangles =
                        Mod.custom(fun self ->
                            let modelTrafo = modelTrafo.GetValue self |> unbox<Trafo3d>
                            let positions = positions.Buffer.GetValue self
                            let indices =
                                if isNull indices then null
                                else indices.GetValue self |> toInt

                            match positions with
                                | :? ArrayBuffer as ab ->
                                    let data = toV3d ab.Data |> Array.map modelTrafo.Forward.TransformPos
                                        
                                    if isNull indices then
                                        let triangles = Array.zeroCreate (data.Length / 3)
                                        for i in 0..triangles.Length-1 do
                                            let p0 = data.[3*i + 0]
                                            let p1 = data.[3*i + 1]
                                            let p2 = data.[3*i + 2]
                                            triangles.[i] <- Triangle3d(p0, p1, p2)

                                        triangles
                                    else
                                        let triangles = Array.zeroCreate (indices.Length / 3)
                                        for i in 0..triangles.Length-1 do
                                            let p0 = data.[indices.[3*i + 0]]
                                            let p1 = data.[indices.[3*i + 1]]
                                            let p2 = data.[indices.[3*i + 2]]
                                            triangles.[i] <- Triangle3d(p0, p1, p2)

                                        triangles

                                | _ ->
                                    failwith "not implemented"


                        )


                    return triangles



                | _ ->
                    return! None
        }

    let private tryGetTrianglesAsASet (ro : IRenderObject) =
        option {
            match ro with
                | :? RenderObject as ro ->

                    
                    let! modelTrafo = ro.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelTrafo")
                    let! positions = ro.VertexAttributes.TryGetAttribute(DefaultSemantic.Positions)
                    let indices = ro.Indices

                    let positionType = positions.ElementType
                    let indexType = if isNull ro.Indices then typeof<int> else indices.GetValue().GetType().GetElementType()
                    
                    let toInt : Array -> int[] = PrimitiveValueConverter.arrayConverter indexType
                    let toV3d : Array -> V3d[] = PrimitiveValueConverter.arrayConverter positionType

                    let triangles =
                        ASet.custom(fun self ->
                            let modelTrafo = modelTrafo.GetValue self |> unbox<Trafo3d>
                            let positions = positions.Buffer.GetValue self
                            let indices =
                                if isNull indices then null
                                else indices.GetValue self |> toInt

                            let newTriangles =
                                match positions with
                                    | :? ArrayBuffer as ab ->
                                        let data = toV3d ab.Data |> Array.map modelTrafo.Forward.TransformPos
                                        
                                        if isNull indices then
                                            let triangles = Array.zeroCreate (data.Length / 3)
                                            for i in 0..triangles.Length-1 do
                                                let p0 = data.[3*i + 0]
                                                let p1 = data.[3*i + 1]
                                                let p2 = data.[3*i + 2]
                                                triangles.[i] <- Triangle3d(p0, p1, p2)

                                            triangles
                                        else
                                            let triangles = Array.zeroCreate (indices.Length / 3)
                                            for i in 0..triangles.Length-1 do
                                                let p0 = data.[indices.[3*i + 0]]
                                                let p1 = data.[indices.[3*i + 1]]
                                                let p2 = data.[indices.[3*i + 2]]
                                                triangles.[i] <- Triangle3d(p0, p1, p2)

                                            triangles

                                    | _ ->
                                        failwith "not implemented"

                            let newTriangles = HashSet newTriangles

                            let rem = self.Content |> Seq.filter (newTriangles.Contains >> not) |> Seq.map Rem |> Seq.toList
                            let add = newTriangles |> Seq.filter (self.Content.Contains >> not) |> Seq.map Add |> Seq.toList


                            add @ rem
                        )


                    return triangles



                | _ ->
                    return! None
        }


    let ofRenderObject (o : IRenderObject) =
        o |> tryGetTrianglesAsMod |> Option.get

    let ofRenderObjects (o : aset<IRenderObject>) =
        aset {
            for ro in o do
                match tryGetTrianglesAsASet ro with
                    | Some set -> yield! set
                    | _ -> ()
        }

    let ofSg (sg : ISg) =
        aset {
            for ro in sg.RenderObjects() do
                match tryGetTrianglesAsASet ro with
                    | Some set -> yield! set
                    | _ -> ()
        }



module SpatialDict =
    open System.Collections.Generic
    
    [<AllowNullLiteral>]
    type private Node<'a> =
        class
            val mutable public Content : List<V3d * 'a>
            val mutable public Children : Node<'a>[]
        
            new(p,c) = { Content = p; Children = c }
        end

    type SpatialDict<'a>(pointsPerLeaf : int) =

        static let (|Empty|Leaf|Node|) (n : Node<'a>) =
            if isNull n then Empty
            elif isNull n.Children then Leaf n.Content
            else Node n.Children

        static let cluster (cell : GridCell) (values : array<V3d * 'a>) =
            let c = cell.Center

            let lists = Array.init 8 (fun i -> List<V3d * 'a>(values.Length))

            for (p,v) in values do
                let index =
                    (if p.X > c.X then 4 else 0) +
                    (if p.Y > c.Y then 2 else 0) +
                    (if p.Z > c.Z then 1 else 0)

                lists.[index].Add(p,v)

            lists
                |> Array.indexed
                |> Array.choose (fun (i,l) -> 
                    if l.Count > 0 then
                        Some (i, CSharpList.toArray l)
                    else
                        None
                )


        static let Empty : Node<'a> = null
        static let Leaf v = Node(v, null)
        static let Node c = Node(null, c)

        let mutable root : Node<'a> = null
        let mutable bounds = Box3d.Invalid
        let mutable cell = GridCell()
        let mutable count = 0



        member private x.build (cell : GridCell, values : array<V3d * 'a>) =
            if values.Length = 0 then
                Empty

            elif values.Length < pointsPerLeaf then
                Leaf (List values)

            else
                let children = Array.zeroCreate 8
                for (child, values) in cluster cell values do
                    children.[child] <- x.build (cell.GetChild child, values)

                Node children

        member private x.addContained (cell : GridCell, values : array<V3d * 'a>, n : byref<Node<'a>>) =
            match n with
                | Empty ->
                    n <- Leaf (List values)

                | Leaf content ->
                    if content.Count + values.Length <= pointsPerLeaf then
                        content.AddRange values
                    else
                        let children = Array.zeroCreate 8

                        let all = Array.append values (CSharpList.toArray content)

                        for (child, values) in cluster cell all do
                            children.[child] <- x.build (cell.GetChild child, values)

                        n <- Node(children)

                | Node children ->
                    for (child, values) in cluster cell values do
                        x.addContained(cell.GetChild child, values, &children.[child])

        member private x.query (point : V3d, r : float, cell : GridCell, n : Node<'a>, res : List<'a>) =
            match n with
                | Empty -> 
                    ()

                | Leaf values ->
                    for (p,v) in values do
                        if V3d.Distance(p, point) <= r then
                            res.Add v

                | Node children ->
                    let center = cell.Center

                    let index = 
                        (if point.X > center.X then 4 else 0) +
                        (if point.Y > center.Y then 2 else 0) +
                        (if point.Z > center.Z then 1 else 0)

                    x.query(point, r, cell.GetChild index, children.[index], res)


        member x.Count = count

        member x.Clear() =
            count <- 0
            root <- null
            bounds <- Box3d.Invalid
            cell <- GridCell()

        member x.AddRange (values : array<V3d * 'a>) =
            count <- count + values.Length
            let bb = values |> Seq.map fst |> Box3d
            match root with
                | Empty ->
                    let c = GridCell.ofBox bb

                    bounds <- bb
                    cell <- c
                    root <- x.build(c, values)

                | _ ->
                    bounds.ExtendBy bb

                    while not (cell.Contains bb) do
                        let pi = cell.IndexInParent
                        root <- Node (Array.init 8 (fun i -> if i = pi then root else Empty)) 
                        cell <- cell.Parent

                    x.addContained(cell, values, &root)
                    
        member x.AddRange (values : seq<V3d * 'a>) =
            values |> Seq.toArray |> x.AddRange

        member x.Add(key : V3d, value : 'a) =
            x.AddRange [|key, value|]

        member x.Query(point : V3d, maxDistance : float) =
            let res = List<'a>()
            x.query(point, maxDistance, cell, root, res) 
            res :> seq<_>


        new() = SpatialDict<'a>(64)














module Outline = 
    open System.Collections.Generic
    open Aardvark.Base.Monads.Option



    open SpatialDict


    let depthTest (runtime : IRuntime) (viewPos : IMod<V3d>) (size : V2i) (sg : ISg) =
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]


        let bounds = sg.LocalBoundingBox()

        let viewProjTup = 
            Mod.map2 ViewProjection.containing viewPos bounds

        let viewProj = 
            viewProjTup |> Mod.map (fun (v,p) ->
                (CameraView.viewTrafo v) * (Frustum.projTrafo p)
            )

        let objects = 
            sg.RenderObjects()
                |> ASet.choose (fun ro ->
                    match ro with
                        | :? RenderObject as ro ->
                            let newObj = 
                                { ro with
                                    Uniforms =
                                        { new IUniformProvider with
                                            member x.TryGetUniform(scope, sem) =
                                                match string sem with
                                                    | "ViewTrafo" | "ViewTrafoInv" -> Mod.constant Trafo3d.Identity :> IMod |> Some
                                                    | "ProjTrafo" | "ViewProjTrafo" -> viewProj :> IMod |> Some
                                                    | "ProjTrafoInv" | "ViewProjTrafoInv" -> viewProj |> TrafoSemantics.inverse :> IMod |> Some
                                                    | "CameraLocation" -> viewPos :> IMod |> Some
                                                    | _ -> ro.Uniforms.TryGetUniform(scope, sem)
                                            member x.Dispose() =
                                                ro.Uniforms.Dispose()
                                        
                                        }
                                }
                            Some (newObj :> IRenderObject)
                        | _ ->
                            None
                )

        let task = 
            RenderTask.ofList [
                runtime.CompileClear(signature, Mod.constant C4f.Black, Mod.constant 1.0)
                runtime.CompileRender(signature, objects)
            ]

        let fbo = runtime.CreateFramebuffer(signature, Mod.constant size)
        match fbo with
            | :? IOutputMod<IFramebuffer> as m -> m.Acquire()
            | _ -> ()

        let depth = Matrix<float32>(size)
        Mod.custom (fun self ->
            let fbo = fbo.GetValue self
            let viewProj = viewProj.GetValue self

            task.Run(self, OutputDescription.ofFramebuffer fbo) |> ignore

            let da = fbo.Attachments.[DefaultSemantic.Depth] |> unbox<BackendTextureOutputView>

            runtime.DownloadDepth(da.texture, 0, 0, depth)

//            let pi = PixImage<float32>(Col.Format.Gray, size)
//            pi.ChannelArray.[0].Set(depth) |> ignore
//            pi.ToPixImage<byte>(Col.Format.RGBA).SaveAsImage @"C:\Users\schorsch\Desktop\depth.jpg"

            let dsize = V2d size
            let visible (p : V3d) =
                let pp = V3d(0.5, 0.5, 0.5) + V3d(0.5, -0.5, 0.5) * viewProj.Forward.TransformPosProj(p)

                let dpixel =
                    pp.XY * dsize + V2d(0.5, 0.5)
                    
                let pixel = V2l(int64 (round dpixel.X), int64 (round dpixel.Y))
                if pixel.X >= 0L && pixel.Y >= 0L && pixel.X < depth.Size.X && pixel.Y < depth.Size.Y then
                    let minDepth = depth.[pixel] 
                    pp.Z <= float minDepth + 0.001
                else
                    false


            visible
        )

    let create (runtime : IRuntime) (viewPos : IMod<V3d>) (sg : ISg) =

//        let size = V2i(512, 512)
//
//        let surfaceCache = Dict<IMod<ISurface>, IMod<ISurface>>()
//
//        let stencilMode =
//            StencilMode(
//                StencilOperationFunction.Replace,
//                StencilOperationFunction.Zero,
//                StencilOperationFunction.Keep,
//                StencilCompareFunction.Always,
//                1,
//                0xFFFFFFFFu
//            )
//
//        let signature =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
//                //DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
//            ]
//
//        let bounds = sg.LocalBoundingBox()
//
//        let viewProjTup = 
//            Mod.map2 ViewProjection.containing viewPos bounds
//
//        let view = viewProjTup |> Mod.map (fst >> CameraView.viewTrafo)
//        let proj = viewProjTup |> Mod.map (snd >> Frustum.projTrafo)
//
//        let viewProj = 
//            viewProjTup |> Mod.map (fun (v,p) ->
//                (CameraView.viewTrafo v) * (Frustum.projTrafo p)
//            )
//
//        let writeStencil =
//            StencilMode(
//                StencilOperationFunction.Replace,
//                StencilOperationFunction.Replace,
//                StencilOperationFunction.Keep,
//                StencilCompareFunction.Always,
//                1,
//                0xFFFFFFFFu
//            ) |> Mod.constant
//
//
//        let objects = 
//            sg.RenderObjects()
//                |> ASet.choose (fun ro ->
//                    match ro with
//                        | :? RenderObject as ro ->
//                            let newObj = 
//                                { ro with
//                                    WriteBuffers = None //Some (Set.singleton DefaultSemantic.Depth)
//                                    StencilMode = writeStencil
//                                    Uniforms =
//                                        { new IUniformProvider with
//                                            member x.TryGetUniform(scope, sem) =
//                                                match string sem with
//                                                    | "ViewTrafo" | "ViewTrafoInv" -> Mod.constant Trafo3d.Identity :> IMod |> Some
//                                                    | "ProjTrafo" | "ViewProjTrafo" -> viewProj :> IMod |> Some
//                                                    | "ProjTrafoInv" | "ViewProjTrafoInv" -> viewProj |> TrafoSemantics.inverse :> IMod |> Some
//                                                    | "CameraLocation" -> viewPos :> IMod |> Some
//                                                    | _ -> ro.Uniforms.TryGetUniform(scope, sem)
//                                            member x.Dispose() =
//                                                ro.Uniforms.Dispose()
//                                        
//                                        }
//                                }
//                            Some (newObj :> IRenderObject)
//                        | _ ->
//                            None
//                )
//
//        
        let triangles = TriangleSet.ofSg sg |> ASet.toMod


//
//
//        let renderTask = 
//            RenderTask.ofList [
//                runtime.CompileClear(signature, Mod.constant C4f.Black, Mod.constant 1.0)
//                runtime.CompileRender(signature, objects)
//            ]
//
//        let fbo = runtime.CreateFramebuffer(signature, Mod.constant size)
//        match fbo with
//            | :? IOutputMod<IFramebuffer> as m -> m.Acquire()
//            | _ -> ()


        let topology =
            Mod.custom (fun self ->
                let triangles = triangles.GetValue self
                let store = SpatialDict<int>()
                let triangles = Seq.toArray triangles
                for i in 0..triangles.Length - 1 do
                    let t = triangles.[i]
                    store.AddRange [t.P0, i; t.P1, i; t.P2, i]
                store, triangles
            )

        let depthTest = sg |> depthTest runtime viewPos (V2i.II * 4096)

        Mod.custom (fun self ->
//            let fbo = fbo.GetValue(self)
//            renderTask.Run(self, OutputDescription.ofFramebuffer fbo) |> ignore
//
//            let depth = fbo.Attachments.[DefaultSemantic.Depth] |> unbox<BackendTextureOutputView>
//
//            let stencil = Matrix<int>(fbo.Size)
//            //let depthImage = PixImage<float32>(Col.Format.Gray, fbo.Size)
//            runtime.DownloadStencil(depth.texture, 0, 0, stencil)
//            
//            let stencilDiff = Matrix<int>(fbo.Size)
//            stencilDiff
//                .SubMatrix(V2i.II, fbo.Size - 2 * V2i.II)
//                .SetByIndex(stencil.SubMatrix(V2i.II, fbo.Size - 2 * V2i.II), fun index ->
//                    let v = stencil.[index]
//                    if v > 0 then
//                        let px = stencil.[index + stencil.DX]
//                        let py = stencil.[index + stencil.DY]
//                        let nx = stencil.[index - stencil.DX]
//                        let ny = stencil.[index - stencil.DY]
//
//                        if px = 0 || py = 0 || nx = 0 || ny = 0 then 1
//                        else 0
//                    else
//                        0
//
//
//                ) |> ignore            
//
//            let triangles = triangles.GetValue self
//            let viewProj = viewProj.GetValue self
//
//            let sampleLinear (img : Matrix<int>) (tc : V2d) =
//                let c = V2d img.Size * tc + V2d(0.5, 0.5)
//                
//
//                let sx = 0L
//                let sy = 0L
//
//                let mutable sum = 0
//                let p00 = img.Info.Index(V2i c)
//                for x in -sx .. sx do
//                    for y in -sy .. sy do
//                        let i = p00 + x * img.DX + y * img.DY
//                        sum <- sum + img.[i]
//
//                sum
//
//
//            let save (file : string) (mat : Matrix<int>) =
//                let img = PixImage<byte>(Col.Format.RGBA, mat.Size)
//
//
//                img.GetMatrix<C4b>().SetMap(mat, fun v ->
//                    let v = 255 * v
//                    C4b(v,v,v,255)
//                ) |> ignore
//
//                img.SaveAsImage file
//
//
//
//            stencil |> save @"C:\Users\schorsch\Desktop\stencil.jpg"
//            stencilDiff |> save @"C:\Users\schorsch\Desktop\outline.jpg"
//
//            let isBoundary (other : V3d) (line : Line3d) =
//                if line.Direction.MajorDim <> 2 then
//                    let coord (p : V3d) =
//                        V2d(0.5 + 0.5 * p.X, 0.5 - 0.5 * p.Y)
//
//                    let c0 = sampleLinear stencil (coord line.P0)
//                    let c1 = sampleLinear stencil (coord line.P1)
//                    
//                    let n = V2d(-line.Direction.Y, line.Direction.X) |> Vec.normalize
//
//                    let outPos = 
//                        let s = n.Dot(other.XY - line.P0.XY) |> sign
//                        0.5 * (line.P0.XY + line.P1.XY) - (float s) * 0.001 * n
//                            
//
//
//                    let co = sampleLinear stencil (coord (V3d(outPos, 0.0)))
//
//                    if c0 <> 0 && c1 <> 0 && co = 0 then
//                        true
//
//                    else
//                        false
//                else
//                    false


            let outline = HashSet<Line3d>()

            let viewPos = viewPos.GetValue self
            let top, triangles = topology.GetValue self


            let frontFacing = HashSet<int>()
            let backFacing = HashSet<int>()
            let tagential = HashSet<int>()
            let depthTest = depthTest.GetValue self

            for i in 0..triangles.Length-1 do
                let t = triangles.[i]
                let d = Vec.dot t.Normal (viewPos - t.P0).Normalized

                if d > Constant.PositiveTinyValue then
                    if depthTest t.P0 || depthTest t.P1 || depthTest t.P2 then
                        frontFacing.Add i |> ignore
                elif d < -Constant.PositiveTinyValue then
                    backFacing.Add i |> ignore
                else
                    tagential.Add i |> ignore


            for i in frontFacing do
                let t = triangles.[i]
                let t0 = top.Query(t.P0, 0.001) |> Set.ofSeq |> Set.remove i
                let t1 = top.Query(t.P1, 0.001) |> Set.ofSeq |> Set.remove i
                let t2 = top.Query(t.P2, 0.001) |> Set.ofSeq |> Set.remove i

                let t01 = Set.intersect t0 t1
                let t12 = Set.intersect t1 t2
                let t20 = Set.intersect t2 t0

                if t01 |> Set.forall backFacing.Contains then
                    outline.Add(t.Line01) |> ignore

                if t12 |> Set.forall backFacing.Contains then
                    outline.Add(t.Line12) |> ignore

                if t20 |> Set.forall backFacing.Contains then
                    outline.Add(t.Line20) |> ignore



                    ()


            Seq.toArray outline
        )



    let detect (viewPos : V3d) (viewDirection : V3d) (triangles : Triangle3d[]) : Line3d[] =
    
        let frontFacing = triangles
//            triangles |> Array.filter (fun t ->
//                let d = Vec.dot t.Normal viewDirection
//                d < 0.0
//            )

        let counts = Dictionary<Line3d, int>()

        let add (l : Line3d) =
            match counts.TryGetValue l with
                | (true, old) -> counts.[l] <- old + 1
                | _ -> counts.[l] <- 1

        for t in frontFacing do
            add t.Line01 
            add t.Line12 
            add t.Line20 



        let final =
            counts
                |> Dictionary.toSeq
                |> Seq.filter (fun (_,c) -> c = 1)
                |> Seq.map fst 
                |> Seq.toArray

        Log.warn "input: %d res: %d" counts.Count final.Length
        final

    let extrude (viewPos : V3d) (far : float) (outline : Line3d[]) =
        let pointCount = outline.Length * 6

        let normals = Array.zeroCreate pointCount
        let positions = Array.zeroCreate pointCount
        for i in 0..outline.Length - 1 do
            let line = outline.[i]


            let p0n = line.P0 
            let p1n = line.P1

            let p0f = 
                let r = Ray3d(viewPos, p0n - viewPos |> Vec.normalize)
                r.GetPointOnRay(far)

            let p1f = 
                let r = Ray3d(viewPos, p1n - viewPos |> Vec.normalize)
                r.GetPointOnRay(far)

            // p0n p1n p1f p0f
            //  0   1   2   3

            // 0 1 2  0 2 3

            let n = Vec.cross (p1f - p1n) (p1n - p0n) |> Vec.normalize

            let vi = 6 * i 
            positions.[vi + 0] <- V3f p0n
            positions.[vi + 1] <- V3f p1n
            positions.[vi + 2] <- V3f p1f
        
            positions.[vi + 3] <- V3f p0n
            positions.[vi + 4] <- V3f p1f
            positions.[vi + 5] <- V3f p0f

            for o in 0..5 do
                normals.[vi + o] <- V3f n

        
        let positions =
            outline |> Array.collect (fun l -> [|V3f l.P0; V3f l.P1|])

        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, positions :> Array
                    //DefaultSemantic.Normals, normals :> Array
                ]
            )





[<EntryPoint>]
[<STAThread>]
let main args = 
    DynamicLinker.tryUnpackNativeLibrary "Assimp" |> ignore
    Aardvark.Init()

    use app = new OpenGlApplication()
    let f = app.CreateGameWindow(1)
    let ctrl = f //f.Control


    let scene = Aardvark.SceneGraph.IO.Loader.Assimp.load @"C:\Users\Schorsch\Desktop\3d\witcher\Geralt.obj"
    let sg = Sg.AdapterNode(scene)

    let normalizeTo (target : Box3d) (sg : ISg) =
        let source = sg.LocalBoundingBox().GetValue()
        
        let sourceSize = source.Size
        let scale =
            if sourceSize.MajorDim = 0 then target.SizeX / sourceSize.X
            elif sourceSize.MajorDim = 1 then target.SizeY / sourceSize.Y
            else target.SizeZ / sourceSize.Z



        let trafo = Trafo3d.Translation(-source.Center) * Trafo3d.Scale(scale) * Trafo3d.Translation(target.Center)

        sg |> Sg.trafo (Mod.constant trafo)


    let view = 
        CameraView.lookAt (V3d(2.0, 2.0, 2.0)) V3d.Zero V3d.OOI
            |> DefaultCameraController.control ctrl.Mouse ctrl.Keyboard ctrl.Time

    let proj =
        ctrl.Sizes 
            |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))

    let sg =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.White |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
                DefaultSurfaces.normalMap |> toEffect
                DefaultSurfaces.lighting false |> toEffect
              ]

           |> normalizeTo (Box3d(-V3d.III, V3d.III))
           |> Sg.trafo (Mod.constant Trafo3d.ChangeYZ)


    let bounds = sg.LocalBoundingBox()


    let viewPos = view |> Mod.map CameraView.location //|> Mod.force |> Mod.constant
    let o = Outline.create app.Runtime viewPos sg


    let helpers =
        Sg.group' [ 
            yield Sg.wireBox (Mod.constant C4b.Red) bounds

            let viewProj = viewPos |> Mod.map (fun p -> ViewProjection.containing p (Mod.force bounds))
            yield Sg.frustum (Mod.constant C4b.Green) (viewProj |> Mod.map fst) (viewProj |> Mod.map snd)

            yield
                Sg.lines (Mod.constant C4b.Blue) o
                    |> Sg.depthTest (Mod.constant DepthTestMode.None)
                    |> Sg.pass (RenderPass.after "bla" RenderPassOrder.Arbitrary RenderPass.main)
        ]
        |> Sg.effect [
            DefaultSurfaces.trafo |> toEffect
            DefaultSurfaces.vertexColor |> toEffect
        ]






    let sg =
        sg  
            |> Sg.andAlso helpers
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

    let task = app.Runtime.CompileRender(ctrl.FramebufferSignature, sg)

    ctrl.RenderTask <- task |> DefaultOverlays.withStatistics

    f.Run()
    0
