open System
open System.Collections.Generic
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

                    let mode = ro.Mode
                    let! modelTrafo = ro.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelTrafo")
                    let! positions = ro.VertexAttributes.TryGetAttribute(DefaultSemantic.Positions)
                    let indices = ro.Indices

                    let positionType = positions.ElementType
                    let indexType = if isNull ro.Indices then typeof<int> else indices.GetValue().GetType().GetElementType()
                    
                    let toInt : Array -> int[] = PrimitiveValueConverter.arrayConverter indexType
                    let toV3d : Array -> V3d[] = PrimitiveValueConverter.arrayConverter positionType

                    let triangles =
                        Mod.custom(fun self ->
                            let mode = mode.GetValue self
                            let modelTrafo = modelTrafo.GetValue self |> unbox<Trafo3d>
                            let positions = positions.Buffer.GetValue self
                            let indices =
                                if isNull indices then null
                                else indices.GetValue self |> toInt

                            match positions with
                                | :? ArrayBuffer as ab ->
                                    let data = toV3d ab.Data |> Array.map modelTrafo.Forward.TransformPos
                                        
                                    match mode with
                                        | IndexedGeometryMode.TriangleList ->
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

                                        | IndexedGeometryMode.TriangleStrip ->
                                            if isNull indices then
                                                let triangles = Array.zeroCreate (data.Length - 2)

                                                let mutable p0 = data.[0]
                                                let mutable p1 = data.[1]
                         
                                                for i in 2..triangles.Length-1 do
                                                    let p2 = data.[i]
                                                    triangles.[i - 2] <- Triangle3d(p0, p1, p2)

                                                    if i % 2 = 0 then p0 <- p2
                                                    else p1 <- p2


                                                triangles
                                            else
                                                let triangles = Array.zeroCreate (indices.Length - 2)

                                                let mutable p0 = data.[indices.[0]]
                                                let mutable p1 = data.[indices.[1]]
                         
                                                for i in 2..triangles.Length-1 do
                                                    let p2 = data.[indices.[i]]
                                                    triangles.[i - 2] <- Triangle3d(p0, p1, p2)

                                                    if i % 2 = 0 then p0 <- p2
                                                    else p1 <- p2

                                                triangles
                                        | _ ->
                                            [||]

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

                    let mode = ro.Mode
                    let! modelTrafo = ro.Uniforms.TryGetUniform(Ag.emptyScope, Symbol.Create "ModelTrafo")
                    let! positions = ro.VertexAttributes.TryGetAttribute(DefaultSemantic.Positions)
                    let indices = ro.Indices

                    let positionType = positions.ElementType
                    let indexType = if isNull ro.Indices then typeof<int> else indices.GetValue().GetType().GetElementType()
                    
                    let toInt : Array -> int[] = PrimitiveValueConverter.arrayConverter indexType
                    let toV3d : Array -> V3d[] = PrimitiveValueConverter.arrayConverter positionType

                    let triangles =
                        ASet.custom(fun self ->
                            let mode = mode.GetValue self
                            let modelTrafo = modelTrafo.GetValue self |> unbox<Trafo3d>
                            let positions = positions.Buffer.GetValue self
                            let indices =
                                if isNull indices then null
                                else indices.GetValue self |> toInt

                            let newTriangles =
                                match positions with
                                    | :? ArrayBuffer as ab ->
                                        let data = toV3d ab.Data |> Array.map modelTrafo.Forward.TransformPos
                                        
                                        match mode with
                                            | IndexedGeometryMode.TriangleList ->
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

                                            | IndexedGeometryMode.TriangleStrip ->
                                                if isNull indices then
                                                    let triangles = Array.zeroCreate (data.Length - 2)

                                                    let mutable p0 = data.[0]
                                                    let mutable p1 = data.[1]
                         
                                                    for i in 0..triangles.Length-1 do
                                                        let p2 = data.[i + 2]
                                                        triangles.[i] <- Triangle3d(p0, p1, p2)

                                                        if i % 2 = 0 then p0 <- p2
                                                        else p1 <- p2


                                                    triangles
                                                else
                                                    let triangles = Array.zeroCreate (indices.Length - 2)

                                                    let mutable p0 = data.[indices.[0]]
                                                    let mutable p1 = data.[indices.[1]]
                         
                                                    for i in 0..triangles.Length-1 do
                                                        let p2 = data.[indices.[i + 2]]
                                                        triangles.[i] <- Triangle3d(p0, p1, p2)

                                                        if i % 2 = 0 then p0 <- p2
                                                        else p1 <- p2

                                                    triangles
                                            | _ ->
                                                [||]

                                    | _ ->
                                        failwith "not implemented"

                            let newTriangles = 
                                newTriangles |> Seq.filter (fun t -> not t.IsDegenerated) |> HashSet
                            


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
    
    type private OctNodeInfo =
        {
            pointsPerLeaf : int
            minLeafVolume : float
        }

    [<AllowNullLiteral>]
    type private OctNode<'a> =
        class
            val mutable public Center : V3d
            val mutable public Content : List<V3d * 'a>
            val mutable public Children : OctNode<'a>[]
        
            new(center, p,c) = { Center = center; Content = p; Children = c }
        end

    [<AutoOpen>]
    module private NodePatterns =
        let inline (|Empty|Leaf|Node|) (n : OctNode<'a>) =
            if isNull n then Empty
            elif isNull n.Children then Leaf n.Content
            else Node n.Children
        let Empty<'a> : OctNode<'a> = null
        let Leaf(center, v) = OctNode(center, v, null)
        let Node(center, c) = OctNode(center, null, c)

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private OctNode =

        let cluster (cell : GridCell) (values : array<V3d * 'a>) =
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

        let clusterPoints (cell : GridCell) (values : array<V3d>) =
            let c = cell.Center

            let lists = Array.init 8 (fun i -> List<V3d>(values.Length))

            for p in values do
                let index =
                    (if p.X > c.X then 4 else 0) +
                    (if p.Y > c.Y then 2 else 0) +
                    (if p.Z > c.Z then 1 else 0)

                lists.[index].Add p

            lists
                |> Array.indexed
                |> Array.choose (fun (i,l) -> 
                    if l.Count > 0 then
                        Some (i, CSharpList.toArray l)
                    else
                        None
                )

        let rec build (info : OctNodeInfo) (cell : GridCell) (values : array<V3d * 'a>) =
            if values.Length = 0 then
                Empty

            elif values.Length < info.pointsPerLeaf || cell.ChildVolume < info.minLeafVolume then
                Leaf (cell.Center, List values)

            else
                let children = Array.zeroCreate 8
                for (child, values) in cluster cell values do
                    children.[child] <- build info (cell.GetChild child) values

                Node(cell.Center, children)

        let rec addContained (info : OctNodeInfo) (cell : GridCell) (values : array<V3d * 'a>) (n : byref<OctNode<'a>>) =
            match n with
                | Empty ->
                    n <- build info cell values

                | Leaf content ->
                    if content.Count + values.Length <= info.pointsPerLeaf || cell.ChildVolume < info.minLeafVolume then
                        content.AddRange values
                    else
                        let children = Array.zeroCreate 8

                        let all = Array.append values (CSharpList.toArray content)

                        for (child, values) in cluster cell all do
                            children.[child] <- build info (cell.GetChild child) values

                        n <- Node(cell.Center, children)

                | Node children ->
                    for (child, values) in cluster cell values do
                        addContained info (cell.GetChild child) values &children.[child]

        let rec query (point : V3d) (r : float) (cell : GridCell) (n : OctNode<'a>) (res : List<'a>) =
            match n with
                | Empty -> 
                    ()

                | Leaf values ->
                    for (p,v) in values do

                        if (p - point).Abs.AllSmallerOrEqual r then
                            res.Add v

                | Node children ->
                    let center = n.Center

                    let indices = HashSet.ofList [0..7]

                    if point.X > center.X + r then
                        indices.IntersectWith [4; 5; 6; 7]
                    elif point.X < center.X - r then
                        indices.IntersectWith [0; 1; 2; 3]
                        
                    if point.Y > center.Y + r then
                        indices.IntersectWith [2; 3; 6; 7]
                    elif point.Y < center.Y - r then
                        indices.IntersectWith [0; 1; 4; 5]

                    if point.Z > center.Z + r then
                        indices.IntersectWith [1; 3; 5; 7]
                    elif point.Z < center.Z - r then
                        indices.IntersectWith [0; 2; 4; 6]

                    for i in indices do
                        query point r (cell.GetChild i) children.[i] res



        let rec visit (result : List<V3d * list<'a>>) (r : float) (n : OctNode<'a>) =
            match n with
                | Empty -> 
                    ()
                | Leaf(values) ->
                    let groups = List<V3d * list<'a>>()
                        
                    for (k,v) in values do
                        let mutable found = false
                        let mutable i = 0
                        while i < groups.Count && not found do
                            let (g,vs) = groups.[i]
                            if (g - k).Abs.AllSmallerOrEqual r then
                                groups.[i] <- (g, v::vs)
                                found <- true

                            i <- i + 1

                        if not found then
                            groups.Add(k, [v])
                            
                        
                    result.AddRange groups

                | Node(children) -> 
                    children |> Seq.iter (visit result r)

    type SpatialDict<'a>(pointsPerLeaf : int) =

        let info = { pointsPerLeaf = pointsPerLeaf; minLeafVolume = pown 0.00001 3 }
        let mutable root : OctNode<'a> = null
        let mutable bounds = Box3d.Invalid
        let mutable cell = GridCell()
        let mutable count = 0

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
                    root <- OctNode.build info c values

                | _ ->
                    bounds.ExtendBy bb

                    while not (cell.Contains bb) do
                        let pi = cell.IndexInParent
                        cell <- cell.Parent
                        root <- Node (cell.Center, Array.init 8 (fun i -> if i = pi then root else Empty)) 

                    OctNode.addContained info cell values &root
                    
        member x.AddRange (values : seq<V3d * 'a>) =
            values |> Seq.toArray |> x.AddRange

        member x.Add(key : V3d, value : 'a) =
            x.AddRange [|key, value|]

        member x.Query(point : V3d, maxDistance : float) =
            let res = List<'a>()
            OctNode.query point maxDistance cell root res
            res :> seq<_>


        member x.Indexed(r : float) =
            let result = List<V3d * list<'a>>()
            root |> OctNode.visit result r

            result |> CSharpList.toArray



        new() = SpatialDict<'a>(10)


module private NewSpatialDict =
    
    type private OctNodeInfo =
        {
            pointsPerLeaf : int
            minLeafVolume : float
        }

    [<AllowNullLiteral>]
    type private OctNode<'a> =
        class
            val mutable public Center : V3d
            val mutable public Content : List<V3d * 'a>
            val mutable public Children : OctNode<'a>[]
        
            new(center, p,c) = { Center = center; Content = p; Children = c }
        end

    [<AutoOpen>]
    module private NodePatterns =
        let inline (|Empty|Leaf|Node|) (n : OctNode<'a>) =
            if isNull n then Empty
            elif isNull n.Children then Leaf n.Content
            else Node n.Children
        let Empty<'a> : OctNode<'a> = null
        let Leaf(center, v) = OctNode(center, v, null)
        let Node(center, c) = OctNode(center, null, c)    

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module private OctNode =

        let cluster (cell : GridCell) (values : array<V3d * int>) =
            let c = cell.Center

            let lists = Array.init 8 (fun i -> List<V3d * int>(values.Length))

            for (p,i) in values do
                let index =
                    (if p.X > c.X then 4 else 0) +
                    (if p.Y > c.Y then 2 else 0) +
                    (if p.Z > c.Z then 1 else 0)

                lists.[index].Add(p,i)

            lists
                |> Array.indexed
                |> Array.choose (fun (i,l) -> 
                    if l.Count > 0 then
                        Some (i, CSharpList.toArray l)
                    else
                        None
                )
 
        let rec build (info : OctNodeInfo) (cell : GridCell) (r : float) (f : V3d -> 'a) (output : array<'a>) (points : array<V3d * int>) =
            if points.Length = 0 then
                Empty

            elif points.Length < info.pointsPerLeaf || cell.ChildVolume < info.minLeafVolume then
                let values = List<V3d * 'a>()
                let rec findOrAdd (offset : int) (p : V3d) =
                    if offset >= values.Count then
                        let v = f p
                        values.Add(p, v)
                        v
                    else
                        let (k, v) = values.[offset]

                        let found = (k - p).Abs.AllSmallerOrEqual r 
                        if found then
                            v
                        else
                            findOrAdd (offset + 1) p

                for (p,i) in points do
                    output.[i] <- findOrAdd 0 p


                Leaf (cell.Center, values)

            else
                let children = Array.zeroCreate 8
                for (child, points) in cluster cell points do
                    children.[child] <- build info (cell.GetChild child) r f output points

                Node(cell.Center, children)

        let rec query (point : V3d) (r : float) (cell : GridCell) (n : OctNode<'a>) (res : List<'a>) =
            match n with
                | Empty -> 
                    ()

                | Leaf values ->
                    for (p,v) in values do
                        if (p - point).Abs.AllSmallerOrEqual r then
                            res.Add v

                | Node children ->
                    let center = n.Center

                    let indices = HashSet.ofList [0..7]

                    if point.X > center.X + r then
                        indices.IntersectWith [4; 5; 6; 7]
                    elif point.X < center.X - r then
                        indices.IntersectWith [0; 1; 2; 3]
                        
                    if point.Y > center.Y + r then
                        indices.IntersectWith [2; 3; 6; 7]
                    elif point.Y < center.Y - r then
                        indices.IntersectWith [0; 1; 4; 5]

                    if point.Z > center.Z + r then
                        indices.IntersectWith [1; 3; 5; 7]
                    elif point.Z < center.Z - r then
                        indices.IntersectWith [0; 2; 4; 6]

                    for i in indices do
                        query point r (cell.GetChild i) children.[i] res

    type SpatialDict<'a>(r : float, data : V3d[], f : V3d -> 'a) =

        let info = { pointsPerLeaf = 10; minLeafVolume = pown 0.00001 3 }
        let mutable bounds = Box3d data
        let mutable cell = GridCell.Containing bounds
        let res = Array.zeroCreate data.Length
        let mutable root = OctNode.build info cell r f res (data |> Array.mapi (fun i v -> v, i))

        member x.Values = res

        member x.Query(point : V3d, maxDistance : float) =
            let res = List<'a>()
            OctNode.query point maxDistance cell root res
            res :> seq<_>

[<StructLayout(LayoutKind.Sequential)>]
type TriangleAdjacency =
    struct
        val mutable public I0 : int
        val mutable public N01 : int
        val mutable public I1 : int
        val mutable public N12 : int
        val mutable public I2 : int
        val mutable public N20 : int

        static member Size = 6 * sizeof<int>
        
        member x.CopyTo(arr : int[], start : int) =
            arr.[start + 0] <- x.I0
            arr.[start + 1] <- x.N01
            arr.[start + 2] <- x.I1
            arr.[start + 3] <- x.N12
            arr.[start + 4] <- x.I2
            arr.[start + 5] <- x.N20

        new(i0, i1, i2) = { I0 = i0; I1 = i1; I2 = i2; N01 = -1; N12 = -1; N20 = -1 }
        new(i0, i1, i2, n01, n12, n20) = { I0 = i0; I1 = i1; I2 = i2; N01 = n01; N12 = n12; N20 = n20 }

    end

type TriangleMesh(triangles : Triangle3d[]) =
    let indices, positions, pointTriangles =
        let points = triangles |> Array.collect (fun t -> [|t.P0; t.P1; t.P2|])

        let indexedPoints = List<V3d>()
        let add (p : V3d) =
            let i = indexedPoints.Count
            indexedPoints.Add p
            i

        let store = NewSpatialDict.SpatialDict<int>(Constant.PositiveTinyValue, points, add)

        let indices = store.Values
        let positions = indexedPoints |> CSharpList.toArray

        let pointTriangles = Array.create positions.Length Set.empty 

        for ti in 0 .. triangles.Length - 1 do
            let i0 = indices.[3 * ti + 0]
            let i1 = indices.[3 * ti + 1]
            let i2 = indices.[3 * ti + 2]

            pointTriangles.[i0] <- Set.add ti pointTriangles.[i0]
            pointTriangles.[i1] <- Set.add ti pointTriangles.[i1]
            pointTriangles.[i2] <- Set.add ti pointTriangles.[i2]

        indices, positions, pointTriangles

    let mutable adjacency : TriangleAdjacency[] = null

    member x.Indices = indices
    member x.Positions = positions
    member x.PointTriangles = pointTriangles

    member x.Adjacency =
        if not (isNull adjacency) then 
            adjacency
        else
            adjacency <- 
                Array.init (indices.Length / 3) (fun ti ->
                    let i0 = indices.[3 * ti + 0]
                    let i1 = indices.[3 * ti + 1]
                    let i2 = indices.[3 * ti + 2]
                    let points = Set.ofList [i0; i1; i2]

                    let n0 = pointTriangles.[i0] |> Set.remove ti
                    let n1 = pointTriangles.[i1] |> Set.remove ti
                    let n2 = pointTriangles.[i2] |> Set.remove ti

                    let n01 = Set.intersect n0 n1
                    let n12 = Set.intersect n1 n2
                    let n20 = Set.intersect n2 n0

                    let get (other : int) (s : Set<int>) =
                        match Set.count s with
                            | 1 -> 
                                let oi = Seq.head s

                                let otherPoints = 
                                    Set.ofList [
                                        indices.[3 * oi + 0]
                                        indices.[3 * oi + 1]
                                        indices.[3 * oi + 2]
                                    ]

                                let notMine = Set.difference otherPoints points
                                if Set.count notMine = 1 then Seq.head notMine
                                else other


                            | _ -> 
                                other
                            
                    TriangleAdjacency(i0, i1, i2, get i2 n01, get i0 n12, get i1 n20)
                ) 
            adjacency





open ClipperLib
open System.Collections.Generic
type Clipper private() =
    static let clipper = ClipperLib.ClipperOffset()
    static let max = V2d(4096, 4096)
    static let toClipper (offset : V2d) (size : V2d) (p : Polygon2d) =
        let res = 
            p.Points
                |> Seq.map (fun p ->
                    let pp = max * ((p - offset) / size) |> V2l
                    IntPoint(pp.X, pp.Y)
                   )
                |> List
        res

    static let toClipperTri (offset : V2d) (size : V2d) (p : Triangle2d) =
        
        let p =
            if p.WindingOrder < 0.0 then
                p.Reversed
            else
                p

        let res = 
            p.Points
                |> Seq.map (fun p ->
                    //let p = 1.0001 * (p - c) + c
                    let pp = max * ((p - offset) / size)
                    IntPoint(int64 (round pp.X), int64 (round pp.Y))
                   )
                |> List
        res

    static let ofClipper (offset : V2d) (size : V2d) (l : List<IntPoint>) =
        l 
        |> Seq.map (fun ip ->
            let p = (V2d(ip.X, ip.Y) / max) * size + offset
            p
        )
        |> Seq.toArray
        |> Polygon2d



    static member Union (bounds : Box2d, tris : seq<Triangle2d>) =
        clipper.Clear()
        let res = List()
        let off = bounds.Min
        let size = bounds.Size
        for p in tris do
            res.Add(toClipperTri off size p)

        clipper.AddPaths(res, JoinType.jtMiter, EndType.etClosedPolygon) |> ignore


        let mutable sol = PolyTree()
        clipper.Execute(&sol, 0.0001) |> ignore

        let rec all (res : List<Polygon2d>) (t : PolyNode) =
            if isNull t then ()
            else 
                if t.Contour.Count > 2 && not t.IsOpen then
                    let poly = ofClipper off size t.Contour
                    
                    if t.IsHole then res.Add poly
                    else res.Add poly.Reversed

                if t.Childs.Count > 0 then
                    t.Childs |> Seq.iter (all res)


        let res = List()
        sol.Childs |> Seq.iter (all res)
        res |> CSharpList.toArray

    static member Union (tris : seq<Triangle2d>) =
        let bounds = tris |> Seq.map (fun p -> p.BoundingBox2d) |> Box2d
        Clipper.Union(bounds, tris)


module Outline = 
    open System.Collections.Generic
    open Aardvark.Base.Monads.Option

    open SpatialDict


    let depthImage (runtime : IRuntime) (viewProj : IMod<Trafo3d>) (size : V2i) (sg : ISg) =
        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]


        let bounds = sg.LocalBoundingBox()


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
                                                    | "CameraLocation" -> viewProj |> Mod.map (fun t -> t.GetViewPosition()) :> IMod |> Some
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
                runtime.CompileClear(signature, Mod.constant Map.empty, Mod.constant(Some 1.0))
                runtime.CompileRender(signature, objects)
            ]

        let fbo = runtime.CreateFramebuffer(signature, Mod.constant size)
        match fbo with
            | :? IOutputMod<IFramebuffer> as m -> m.Acquire()
            | _ -> ()

        let depth = Matrix<float32>(size + V2i.II * 4)
        depth.Set(1.0f) |> ignore

        let depth = depth.SubMatrix(V2i(2,2),V2i(size.X, size.Y))

        Mod.custom (fun self ->
            let fbo = fbo.GetValue self
            let viewProj = viewProj.GetValue self

            task.Run(self, OutputDescription.ofFramebuffer fbo) |> ignore
            let da = fbo.Attachments.[DefaultSemantic.Depth] |> unbox<BackendTextureOutputView>

            runtime.DownloadDepth(da.texture, 0, 0, depth)

            let sampleIndex (index : int64) =
                2.0 * float depth.Data.[int index] - 1.0

            let dsize = V2d size
            let sample (pp : V2d) =
                let dpixel =
                    pp.XY * dsize + V2d(0.5, 0.5)
                    
                let pixel = V2l(int64 (floor dpixel.X), int64 (floor dpixel.Y))
                let frac = dpixel - V2d pixel

                let index = depth.Info.Index(pixel)


                let mutable res = 1.0
                let mat = depth.SubMatrix(V2i pixel - 2*V2i.II, V2i.II * 5)
                mat.ForeachIndex (fun i ->
                    res <- min res (float mat.[i])
                ) |> ignore

                2.0 * res - 1.0

//                let v00 = sampleIndex index
//                let v10 = sampleIndex (index + depth.DX)
//                let v01 = sampleIndex (index + depth.DY)
//                let v11 = sampleIndex (index + depth.DX + depth.DY)
//
//                (v00 * (1.0 - frac.X) + v10 * frac.X) * (1.0 - frac.Y) +
//                (v01 * (1.0 - frac.X) + v11 * frac.X) * frac.Y

            sample
        )

    let create (runtime : IRuntime) (viewPos : IMod<V3d>) (sg : ISg) =

        let bounds      = sg.LocalBoundingBox()
        let triangles   = TriangleSet.ofSg sg |> ASet.toMod |> Mod.map Seq.toArray
        let viewProjTup = Mod.map2 ViewProjection.containing viewPos bounds
        let viewProj    = Mod.map (fun (v, p) -> (CameraView.viewTrafo v) * (Frustum.projTrafo p)) viewProjTup

        let depth = depthImage runtime viewProj (V2i.II * 512) sg

        let outline = 
            Mod.custom (fun self ->
                let viewPos         = viewPos.GetValue self
                let viewProj        = viewProj.GetValue self
                let triangles       = triangles.GetValue self
                let depth           = depth.GetValue self

                //let mutable bounds = Box2d.Invalid
            
   
                let back = HashSet<Triangle3d>()

                let mutable bounds = Box2d.Invalid
                let projected = 
                    triangles 
                    |> Seq.choose (fun t ->
                        if Vec.dot t.Normal (viewPos - t.P0) > 0.0 then
                            let p0 = viewProj.Forward.TransformPosProj(t.P0)
                            let p1 = viewProj.Forward.TransformPosProj(t.P1)
                            let p2 = viewProj.Forward.TransformPosProj(t.P2)

                            bounds.ExtendBy(p0.XY)
                            bounds.ExtendBy(p1.XY)
                            bounds.ExtendBy(p2.XY)

                            Triangle2d(p0.XY, p1.XY, p2.XY) |> Some
                        else
                            None
                    )
                    |> Seq.toArray

                let unprojectPoint (p : V2d) =
                    viewProj.Backward.TransformPosProj(V3d(p, depth (V2d(0.5, 0.5) + V2d(0.5, -0.5) * p) ))
            
                let unproject (line : Line2d) =
                    Line3d(unprojectPoint line.P0, unprojectPoint line.P1)

                let outline = Clipper.Union(bounds, projected) |> Array.collect (fun c -> c.EdgeLines |> Seq.map unproject |> Seq.toArray)


                

                outline, [||]
            )


        outline

    [<AllowNullLiteral>]
    type UnionNode() as this =
        let mutable rank = 0
        let mutable parent = this

        member x.Parent
            with get() = parent
            and set p = parent <- p

        member x.Rank
            with get() = rank
            and set r = rank <- r

    type UnionFind<'a when 'a : equality>() =
        let nodes = Dict<'a, UnionNode>()

        let node (v : 'a) =
            nodes.GetOrCreate(v, fun v -> 
                let n = UnionNode()
                n
            )

        let rec find (n : UnionNode) =
            if n = n.Parent then n
            else 
                let parent = find n.Parent
                n.Parent <- parent
                parent

        member x.Add(l : 'a, r : 'a) =
            let l = node l
            let r = node r

            let pl = find l
            let pr = find r
            if pl <> pr then
                if pl.Rank < pr.Rank then
                    pl.Parent <- pr
                elif pl.Rank > pr.Rank then
                    pr.Parent <- pl
                else
                    pr.Parent <- pl
                    pl.Rank <- pl.Rank + 1

        member x.Groups =
            let sets = Dictionary<UnionNode, HashSet<'a>>()

            for (k,v) in Dict.toSeq nodes do
                let rep = find v
                match sets.TryGetValue rep with
                    | (true, set) -> set.Add k |> ignore
                    | _ -> sets.[rep] <- HashSet [k]
           
            sets.Values :> seq<HashSet<'a>>

    


    let partitionTriangles (top : Set<int>[]) =
        let triangleCount = top.Length / 3
        let uf = UnionFind<int>()
        let isolated = HashSet<int>()


        for i in 0..triangleCount-1 do
            let t0 = top.[3*i+0] |> Set.remove i
            let t1 = top.[3*i+1] |> Set.remove i
            let t2 = top.[3*i+2] |> Set.remove i

            let t01 = Set.intersect t0 t1
            let t12 = Set.intersect t1 t2
            let t20 = Set.intersect t2 t0
            let adjacent = Set.unionMany [t01; t12; t20]

            if Set.isEmpty adjacent then isolated.Add i |> ignore
            else for a in adjacent do uf.Add(i, a)

           
        let parts = uf.Groups |> Seq.toList
        let isolated = isolated |> Seq.map (fun v -> true, HashSet [v]) |> Seq.toList

        let parts = 
            parts |> List.map (fun p ->
                let tris = HashSet.toArray p
                let mutable hasBoundary = false
                let mutable gi = 0
                while not hasBoundary && gi < tris.Length do
                    let i = tris.[gi]
                    let t0 = top.[3*i+0] |> Set.remove i
                    let t1 = top.[3*i+1] |> Set.remove i
                    let t2 = top.[3*i+2] |> Set.remove i

                    let t01 = Set.intersect t0 t1 |> Set.isEmpty
                    let t12 = Set.intersect t1 t2 |> Set.isEmpty
                    let t20 = Set.intersect t2 t0 |> Set.isEmpty

                    if t01 || t12 || t20 then
                        hasBoundary <- true

                    gi <- gi + 1

                hasBoundary, p

            )


        isolated @ parts
                       
    let createFB (runtime : IRuntime) (viewPos : IMod<V3d>) (sg : ISg) =

        let filterRedundant (triangles : Triangle3d[]) =
            
            let res = List<Triangle3d>()

            let eps = 2.0 * Constant.PositiveTinyValue
            let rec anyContains (current : int) (t : Triangle3d) (ti : int) =
                if current = ti then 
                    anyContains (current + 1) t ti

                elif current < triangles.Length then
                    let tt = triangles.[current]
                    let p = tt.Plane
                    if Fun.IsTiny(p.Height(t.P0), eps) && Fun.IsTiny(p.Height(t.P1), eps) && Fun.IsTiny(p.Height(t.P2), eps) then
                        
                        let u = tt.P1 - tt.P0
                        let v = tt.P2 - tt.P0
                        let n = Vec.cross u v
                        let mat = M33d.FromCols(u,v,n).Inverse

                        let contains (p : V3d) =
                            let coord = mat * (p - tt.P0)
                            let u' = coord.X
                            let v' = coord.Y
                            u' > 0.0 && v' > 0.0 && u' + v' < 1.0

                        contains t.P0 && contains t.P1 && contains t.P2

                    else
                        anyContains (current + 1) t ti

                else
                    false

            for ti in 0..triangles.Length-1 do
                let t = triangles.[ti]
                if not (anyContains 0 t ti) then
                    res.Add t

            let filtered = triangles.Length - res.Count
            Log.warn "remove %d triangles" filtered
            res |> CSharpList.toArray

        let bounds      = sg.LocalBoundingBox()
        let triangles = 
            TriangleSet.ofSg sg 
                |> ASet.toMod 
                |> Mod.map Seq.toArray

        let centroids = triangles |> Mod.map (Array.map (fun t -> t.ComputeCentroid()))

        let mesh = triangles |> Mod.map TriangleMesh

        let topology =
            Mod.custom (fun self ->
                let mesh = mesh.GetValue self

                let triangles = triangles.GetValue self
                let store = SpatialDict<int>()


                let values = Array.zeroCreate (triangles.Length * 3)
                let mutable vi = 0
                for i in 0..triangles.Length - 1 do
                    let t = triangles.[i]

                    values.[vi+0] <- (t.P0, i)
                    values.[vi+1] <- (t.P1, i)
                    values.[vi+2] <- (t.P2, i)
                    vi <- vi + 3

                store.AddRange values


                let indexed = store.Indexed Constant.PositiveTinyValue

                let positions = indexed |> Array.map fst

                let indexArray = Array.zeroCreate (triangles.Length * 3)



                let pointTriangles = Array.zeroCreate (3 * triangles.Length)

                for ti in 0..triangles.Length-1 do
                    let t = triangles.[ti]
                    let mutable n0 = store.Query(t.P0, 10.0 * Constant.PositiveTinyValue) |> Set.ofSeq
                    let mutable n1 = store.Query(t.P1, 10.0 * Constant.PositiveTinyValue) |> Set.ofSeq
                    let mutable n2 = store.Query(t.P2, 10.0 * Constant.PositiveTinyValue) |> Set.ofSeq

                    pointTriangles.[3 * ti + 0] <- n0
                    pointTriangles.[3 * ti + 1] <- n1
                    pointTriangles.[3 * ti + 2] <- n2




                for ti in 0..triangles.Length-1 do
                    let t0 = pointTriangles.[3 * ti + 0]
                    let t1 = pointTriangles.[3 * ti + 1]
                    let t2 = pointTriangles.[3 * ti + 2]

                    let t01 = Set.intersect t0 t1 |> Set.remove ti
                    let t12 = Set.intersect t1 t2 |> Set.remove ti
                    let t20 = Set.intersect t2 t0 |> Set.remove ti

                    for oi in Set.unionMany [t01; t12; t20] do
                        let o0 = pointTriangles.[3 * oi + 0]
                        let o1 = pointTriangles.[3 * oi + 1]
                        let o2 = pointTriangles.[3 * oi + 2]
                        let o01 = Set.intersect o0 o1 |> Set.contains ti
                        let o12 = Set.intersect o1 o2 |> Set.contains ti
                        let o20 = Set.intersect o2 o0 |> Set.contains ti


                        if not (o01 || o12 || o20) then
                            Log.warn "broken topology"


                        ()

                let edgeTriangles =
                    Array.init (3 * triangles.Length) (fun i ->
                        let ti = i / 3
                        let ei = i % 3
                        let t = triangles.[ti]

                        let p0 = ei
                        let p1 = (ei+1) % 3
                        
                        Set.intersect pointTriangles.[3 * ti + p0] pointTriangles.[3 * ti + p1] |> Set.remove ti
                    )

                



                edgeTriangles
            )

        let eps = 50.0 * Constant.PositiveTinyValue




        Mod.custom (fun self ->

            let outline = List<Line3d>()
            let cap = List<Triangle3d>()

            let parts = List<Triangle3d[] * Line3d[]>()

            let triangles = triangles.GetValue self
            let centroids = centroids.GetValue self
            let viewPos = viewPos.GetValue self
            let edgeTriangles = topology.GetValue self
         

            let frontFacing = HashSet<int>()

            for i in 0..triangles.Length-1 do
                let t = triangles.[i]
                let d = Vec.dot t.Normal (viewPos - t.P0).Normalized
                if d >= 0.0 then frontFacing.Add i |> ignore


            let filteredEdgeTriangles =
                Array.init edgeTriangles.Length (fun ei ->
                    let neighbours = edgeTriangles.[ei]
                    
                    let ti = ei / 3
                    let ei = ei % 3
                    let t = triangles.[ti]

                    match neighbours |> Set.toList with
                        | [l] -> 
                            [l] |> List.filter (fun oi ->
                                let p0t = ei
                                let p1t = (ei + 1) % 3
                                let o = triangles.[oi]

                                let line = Vec.normalize (t.[p1t] - t.[p0t])
                                let n = Vec.cross (t.[p0t] - viewPos) line |> Vec.normalize
                                let plane = Plane3d(n, t.[p0t])

                                let ht = plane.Height centroids.[ti]
                                let ho = plane.Height centroids.[oi]

                                ht * ho <= 0.0
                            )
                        | _ -> []

                )


            let unvisited = HashSet { 0..triangles.Length-1 }
            
            while unvisited.Count > 0 do
                let start = unvisited |> Seq.head

                let lines = List()
                let part = List()
                

                let queue = Queue [start]

                let enqueue (v : int) =
                    if unvisited.Contains v then queue.Enqueue v

                let addCap (l : Triangle3d) =
                    part.Add l
                    cap.Add l

                let add (l : Line3d) =
                    lines.Add l
                    outline.Add l



                let mutable cnt = 0
                while queue.Count > 0 do
                    let i = queue.Dequeue()
                    if unvisited.Remove i then
                        cnt <- cnt + 1
                        let t = triangles.[i]

                        let t01 = filteredEdgeTriangles.[3*i+0]
                        let t12 = filteredEdgeTriangles.[3*i+1]
                        let t20 = filteredEdgeTriangles.[3*i+2]

                        let front = frontFacing.Contains i
                        if front then addCap t.Reversed |> ignore
                        else addCap t |> ignore

                        match t01  with
                            | [] | _::_::_ ->
                                if front then add t.Line10 |> ignore
                                else add t.Line01 |> ignore
                            | [l] -> enqueue l

                        match t12  with
                            | [] | _::_::_ ->
                                if front then add t.Line21 |> ignore
                                else add t.Line12 |> ignore
                            | [l] -> enqueue l
                        
                        match t20  with
                            | [] | _::_::_ ->
                                if front then add t.Line02 |> ignore
                                else add t.Line20 |> ignore
                            | [l] -> enqueue l
                                                       
    

                Log.warn "group-size: %d" cnt
                parts.Add (part |> Seq.toArray, lines |> Seq.toArray)




            Seq.toArray outline, Seq.toArray cap, parts
        )


[<ReflectedDefinition>]
module VolumeShader =
    open FShade

    type Vertex = 
        {
            [<Position>]                pos     : V4d
            [<Semantic "Inf">]          inf     : V4d
            [<WorldPosition>]           wp      : V4d
            [<Semantic "Light">]        l       : V3d
        }

    type SimpleVertex = 
        {
            [<Position>]                p     : V4d
        }

    type UniformScope with
        member x.LightPos : V3d = uniform?LightPos

    
    let vertex (v : Vertex) =
        vertex {
            let p = uniform.ViewProjTrafo * v.pos
            let l = uniform.ViewProjTrafo * V4d(uniform.LightLocation, 1.0)

            return {
                pos = p
                inf = p - l
                wp = v.pos
                l = uniform.LightLocation
            }
        }

    let isOutlineEdge (p0 : V4d) (p1 : V4d) (inside : V4d) (test : V4d) =
        let light = V3d.Zero

        let line = Vec.normalize (p1.XYZ - p0.XYZ)
        let dir = Vec.normalize (p0.XYZ - light)
        let n = Vec.cross dir line
        
        let hi = Vec.dot n inside.XYZ
        let ht = Vec.dot n test.XYZ

        hi * ht > 0.0

    let eps = 0.0

    let isOutlineEdgeWorld (light : V3d) (p0 : V3d) (p1 : V3d) (inside : V3d) (test : V3d) =
        let line = Vec.normalize (p1 - p0)
        let dir = Vec.normalize (p0 - light)
        let n = Vec.cross dir line
        let n = Vec.normalize n
        let d = Vec.dot light n
        let hi = Vec.dot n inside - d
        let ht = Vec.dot n test - d

        if (hi >= 0.0 && ht <= 0.0) || (hi <= 0.0 && ht >= 0.0) then
            false
        else
            true 


    let isDegenerated (light : V3d) (p0 : V3d) (p1 : V3d) (p2 : V3d) =
        let u = p1 - p0
        let v = p2 - p0
        let n = Vec.cross u v |> Vec.normalize
        let h = Vec.dot n (Vec.normalize (p0 - light))
        abs h <= 0.0001

    let extrude (a : TriangleAdjacency<Vertex>) =
        triangle {
            let u = a.P1.inf - a.P0.inf |> Vec.xyz
            let v = a.P2.inf - a.P0.inf |> Vec.xyz
            let n = Vec.cross u v |> Vec.normalize
            let h = Vec.dot n a.P0.inf.XYZ
            let ff = h > 0.0

            let w0 = a.P0.wp.XYZ
            let w1 = a.P1.wp.XYZ
            let w2 = a.P2.wp.XYZ
            let w01 = a.N01.wp.XYZ
            let w12 = a.N12.wp.XYZ
            let w20 = a.N20.wp.XYZ

            let p0n = a.P0.pos + V4d(0.0,0.0,0.0001,0.0)
            let p1n = a.P1.pos + V4d(0.0,0.0,0.0001,0.0)
            let p2n = a.P2.pos + V4d(0.0,0.0,0.0001,0.0)
                    
            let light = a.P0.l

            if ff then
                yield { p = p0n }
                yield { p = p2n }
                yield { p = p1n }
                restartStrip()
                yield { p = a.P0.inf }
                yield { p = a.P1.inf }
                yield { p = a.P2.inf }
                restartStrip()
            else
                yield { p = p0n }
                yield { p = p1n }
                yield { p = p2n }
                restartStrip()
                yield { p = a.P0.inf }
                yield { p = a.P2.inf }
                yield { p = a.P1.inf }
                restartStrip()


            if isDegenerated light w0 w1 w01 || isOutlineEdgeWorld light w0 w1 w2 w01 then
                // Line01 is an outline

                if ff then 
                    yield { p = p0n }
                    yield { p = p1n }
                    yield { p = a.P0.inf }
                    yield { p = a.P1.inf }
                    restartStrip()
                else
                    yield { p = p1n }
                    yield { p = p0n }
                    yield { p = a.P1.inf }
                    yield { p = a.P0.inf }
                    restartStrip()



                ()

            if isDegenerated light w1 w2 w12 || isOutlineEdgeWorld light w1 w2 w0 w12 then
                // Line12 is an outline

                if ff then 
                    yield { p = p1n }
                    yield { p = p2n }
                    yield { p = a.P1.inf }
                    yield { p = a.P2.inf }
                    restartStrip()
                else
                    yield { p = p2n }
                    yield { p = p1n }
                    yield { p = a.P2.inf }
                    yield { p = a.P1.inf }
                    restartStrip()

            if isDegenerated light w2 w0 w20 || isOutlineEdgeWorld light w2 w0 w1 w20 then
                // Line20 is an outline
                if ff then 
                    yield { p = p2n }
                    yield { p = p0n }
                    yield { p = a.P2.inf }
                    yield { p = a.P0.inf }
                    restartStrip()
                else
                    yield { p = p0n }
                    yield { p = p2n }
                    yield { p = a.P0.inf }
                    yield { p = a.P2.inf }
                    restartStrip()

        }


    [<ReflectedDefinition>]
    let closestHitPoint (vp : M44d) (o : V3d) (d : V3d) =
        let r0 = vp.R0
        let r1 = vp.R1
        let r2 = vp.R2
        let r3 = vp.R3


        let l = r3 + r0
        let r = r3 - r0
        let t = r3 + r1
        let b = r3 - r1
        let n = r3 + r2
        let f = r3 - r2

        let ts = Arr<N<6>, float>()

        ts.[0] <- (l.W - o.Dot(l.XYZ)) / d.Dot(l.XYZ)
        ts.[1] <- (r.W - o.Dot(r.XYZ)) / d.Dot(r.XYZ)
        ts.[2] <- (t.W - o.Dot(t.XYZ)) / d.Dot(t.XYZ)
        ts.[3] <- (b.W - o.Dot(b.XYZ)) / d.Dot(b.XYZ)
        ts.[4] <- (n.W - o.Dot(n.XYZ)) / d.Dot(n.XYZ)
        ts.[5] <- (f.W - o.Dot(f.XYZ)) / d.Dot(f.XYZ)

        let mutable minT = 10000000.0
        for i in 0..2 do
            if ts.[i] >= 0.0 then
                minT <- min minT ts.[i]

        o + minT * d

    let extrudeToInf (l : Line<Effects.Vertex>) =
        triangle {

            let vp      = uniform.ViewProjTrafo
            let light   = V4d(uniform.LightPos,1.0)
            let p0      = l.P1.pos
            let p1      = l.P0.pos

            
            let pl      = vp * light
            let p0n     = vp * p0
            let p1n     = vp * p1
            let p0f     = p0n - pl
            let p1f     = p1n - pl

            // p0n p1n p1f p0f
            //  0   1   2   3
            yield { l.P0 with pos = p0n }
            yield { l.P1 with pos = p1n }
            yield { l.P0 with pos = p0f }
            yield { l.P1 with pos = p1f }
        }

    let duplicateAtInf (t : Triangle<Effects.Vertex>) =
        triangle {

            let vp      = uniform.ViewProjTrafo
            let light   = V4d(uniform.LightPos,1.0)
            let p0      = t.P0.pos
            let p1      = t.P1.pos
            let p2      = t.P2.pos

            
            let pl      = vp * light
            let mutable p0n     = vp * p0
            let mutable p1n     = vp * p1
            let mutable p2n     = vp * p2
            let p0f     = (p0n - pl)
            let p1f     = (p1n - pl)
            let p2f     = (p2n - pl)


            p0n.Z <- p0n.Z + 0.0001
            p1n.Z <- p1n.Z + 0.0001
            p2n.Z <- p2n.Z + 0.0001

            // p0n p1n p1f p0f
            //  0   1   2   3
            yield { t.P0 with pos = p0n }
            yield { t.P1 with pos = p1n }
            yield { t.P2 with pos = p2n }
            restartStrip()
            yield { t.P0 with pos = p0f }
            yield { t.P2 with pos = p2f }
            yield { t.P1 with pos = p1f }
                                   
        }

    let frontBack (t : Vertex) =
        fragment {
            return V4d.OIOI
//            if t.f then return V4d(1,0,0,1)
//            else return V4d(0,1,0,1)
                                   
        }





module Camera =
    type Mode =
        | Main
        | Test

    type Camera =
        {
            mainView : IMod<CameraView>
            testView : IMod<CameraView>

            currentView : IMod<CameraView>
            currentProj : IMod<Frustum>

        }

    let viewProj (win : IRenderControl) =
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


        let proj = win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 500.0 (float s.X / float s.Y))

        {
            mainView = mainCam
            testView = gridCam

            currentView = view
            currentProj = proj

        }

[<EntryPoint>]
[<STAThread>]
let main args = 
    DynamicLinker.tryUnpackNativeLibrary "Assimp" |> ignore
    Aardvark.Init()

    use app = new OpenGlApplication()
    let f = app.CreateSimpleRenderWindow(1)
    let ctrl = f //f.Control

    let normalizeTo (target : Box3d) (sg : ISg) =
        let source = sg.LocalBoundingBox().GetValue()
        
        let sourceSize = source.Size
        let scale =
            if sourceSize.MajorDim = 0 then target.SizeX / sourceSize.X
            elif sourceSize.MajorDim = 1 then target.SizeY / sourceSize.Y
            else target.SizeZ / sourceSize.Z



        let trafo = Trafo3d.Translation(-source.Center) * Trafo3d.Scale(scale) * Trafo3d.Translation(target.Center)

        sg |> Sg.trafo (Mod.constant trafo)


        
    let scene = Aardvark.SceneGraph.IO.Loader.Assimp.load @"C:\Aardwork\sponza_cm.obj"
    let sg = 
        Sg.AdapterNode(scene) 
            |> normalizeTo (Box3d(-V3d.III, V3d.III))
            


    let cam = Camera.viewProj ctrl
    let view = cam.currentView
    let proj = cam.currentProj

    let floor =
        Sg.fullScreenQuad
            |> Sg.scale 200.0
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.White |> toEffect
                DefaultSurfaces.simpleLighting |> toEffect
            ]



    let sg =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.White |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
                DefaultSurfaces.normalMap |> toEffect
                DefaultSurfaces.lighting false |> toEffect
              ]

           |> normalizeTo (Box3d(-V3d.III, V3d.III))
           |> Sg.trafo (Mod.constant (Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero) ))
           |> Sg.translate 0.0 0.0 1.0
           |> Sg.scale 20.0
//           |> Sg.andAlso (Sg.box' C4b.Red (Box3d.FromMinAndSize(V3d(-1.6, -1.6, 0.0), V3d(0.1,0.2,0.3))))
//           |> Sg.andAlso (Sg.box' C4b.Red (Box3d.FromMinAndSize(V3d(-1.56, -1.57, 0.15), V3d(0.05,0.15,0.3))))
//           |> Sg.andAlso (Sg.fullScreenQuad |> Sg.scale 0.5 |> Sg.transform (Trafo3d.RotationX Constant.PiHalf ) |> Sg.translate -2.0 -2.0 0.5)
           |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.constantColor C4f.White |> toEffect
                DefaultSurfaces.lighting true |> toEffect
            ]






    let viewPos = cam.testView |> Mod.map CameraView.location 
    let outlineAndTriangles = Outline.createFB app.Runtime viewPos sg


    let writeStencil =
        StencilMode(
            IsEnabled = true,
            Compare = StencilFunction(StencilCompareFunction.Always, 0, 0u),
            OperationFront = 
                StencilOperation(
                    StencilOperationFunction.Keep, 
                    StencilOperationFunction.IncrementWrap, 
                    StencilOperationFunction.Keep
                ),
            OperationBack = 
                StencilOperation(
                    StencilOperationFunction.Keep,
                    StencilOperationFunction.DecrementWrap, 
                    StencilOperationFunction.Keep
                )
        )





    let readStencil =
        StencilMode(
            IsEnabled = true,
            Compare = StencilFunction(StencilCompareFunction.NotEqual, 0, 0xFFFFFFFFu),
            Operation = 
                StencilOperation(
                    StencilOperationFunction.Keep, 
                    StencilOperationFunction.Keep, 
                    StencilOperationFunction.Keep
                )
        )

    let afterMain = RenderPass.after "bla" RenderPassOrder.Arbitrary RenderPass.main
    let final = RenderPass.after "blubb" RenderPassOrder.Arbitrary afterMain
    let afterFinal = RenderPass.after "blubber" RenderPassOrder.Arbitrary final
    let afterafterFinal = RenderPass.after "blubber2" RenderPassOrder.Arbitrary afterFinal
    
    let group = Mod.init 0
    let debug = false

    let mesh =
        sg  |> TriangleSet.ofSg
            |> ASet.toMod
            |> Mod.map (Seq.filter (fun t -> not t.IsDegenerated) >> Seq.toArray >> TriangleMesh)

    
    let shadowVolumes =
        let index = mesh |> Mod.map (fun m -> m.Adjacency.UnsafeCoerce<int>())
        let positions = mesh |> Mod.map (fun m -> m.Positions |> Array.map V3f)

        let drawCall = 
            index |> Mod.map (fun i -> 
                DrawCallInfo(
                    FaceVertexCount = i.Length,
                    InstanceCount = 1
                )
            )

        
        Sg.RenderNode(drawCall, Mod.constant IndexedGeometryMode.TriangleAdjacencyList)
            |> Sg.index index
            |> Sg.vertexAttribute DefaultSemantic.Positions positions
            |> Sg.stencilMode (Mod.constant writeStencil)
            |> Sg.writeBuffers' (Set.ofList [ DefaultSemantic.Stencil ])
            |> Sg.pass afterMain
            |> Sg.effect [
                VolumeShader.vertex |> toEffect
                VolumeShader.extrude |> toEffect
                DefaultSurfaces.constantColor (C4f(1.0,0.0,0.0,0.0)) |> toEffect
            ]



//    let currentGroup = outlineAndTriangles |> Mod.map2 (fun i (o,t,g) -> if i < 0 then (t,o) else g.[i % g.Count]) group
//    let lines = if debug then currentGroup |> Mod.map snd else outlineAndTriangles |> Mod.map (fun (l,_,_) -> l)
//    let capTriangles = if debug then currentGroup |> Mod.map fst else outlineAndTriangles |> Mod.map (fun (_,t,_) -> t)
    let helpers =
        Sg.group' [ 

            let color = C4f(0.0,0.0,0.0,0.0)

//            let cap = 
//                Sg.triangles (Mod.constant C4b.White) capTriangles
//                    |> Sg.stencilMode (Mod.constant writeStencil)
//                    |> Sg.effect [
//                        VolumeShader.duplicateAtInf |> toEffect
//                        DefaultSurfaces.constantColor color |> toEffect
//                    ]
//
//            let sides = 
//                Sg.lines (Mod.constant C4b.White) lines
//                    |> Sg.stencilMode (Mod.constant writeStencil)
//                    |> Sg.effect [
//                        VolumeShader.extrudeToInf |> toEffect
//                        DefaultSurfaces.constantColor color |> toEffect
//                    ]
//            yield 
//                Sg.group' [cap; sides]
//                    |> Sg.pass afterMain
//                    |> Sg.writeBuffers (Set.ofList [ DefaultSemantic.Colors; DefaultSemantic.Stencil ])
//                    |> Sg.blendMode (Mod.constant BlendMode.Blend)
//                    |> Sg.uniform "LightPos" viewPos
//                    |> Sg.depthTest (Mod.constant DepthTestMode.Less)

            yield shadowVolumes

            yield
                Sg.fullScreenQuad
                    |> Sg.pass final
                    |> Sg.depthTest (Mod.constant DepthTestMode.None)
                    |> Sg.writeBuffers' (Set.ofList [DefaultSemantic.Colors])
                    |> Sg.stencilMode (Mod.constant readStencil)
                    |> Sg.blendMode (Mod.constant BlendMode.Blend)
                    |> Sg.effect [
                        DefaultSurfaces.constantColor (C4f(0.0,0.0,0.0,0.8)) |> toEffect
                    ]

//            if debug then
//                yield 
//                    Sg.lines (Mod.constant C4b.Blue) (Mod.map snd currentGroup)
//                        |> Sg.depthTest (Mod.constant DepthTestMode.None)
//                        |> Sg.pass afterafterFinal
//                        |> Sg.effect [
//                            DefaultSurfaces.trafo |> toEffect
//                            DefaultSurfaces.vertexColor |> toEffect
//                        ]
//
//                yield 
//                    Sg.triangles (Mod.constant C4b.Green) (currentGroup |> Mod.map fst)
//                        |> Sg.depthTest (Mod.constant DepthTestMode.None)
//                        |> Sg.cullMode (Mod.constant CullMode.None)
//                        |> Sg.pass afterFinal
//                        |> Sg.effect [
//                            DefaultSurfaces.trafo |> toEffect
//                            VolumeShader.frontBack |> toEffect
//                        ]
//                        |> Sg.fillMode (Mod.constant FillMode.Line)

        ]

    let mode = Mod.init FillMode.Fill

    let sg =
        sg  |> Sg.fillMode mode
            |> Sg.andAlso floor
            |> Sg.andAlso helpers
            |> Sg.uniform "LightLocation" viewPos
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo)

    ctrl.Keyboard.KeyDown(Keys.X).Values.Add (fun () ->
        transact (fun () ->
            match mode.Value with
                | FillMode.Fill -> mode.Value <- FillMode.Line
                | _ -> mode.Value <- FillMode.Fill
        )
    )

    let mutable oldGroup = 0
    ctrl.Keyboard.KeyDown(Keys.Y).Values.Add (fun () ->
        transact (fun () ->
            transact (fun () ->
                if group.Value >= 0 then 
                    oldGroup <- group.Value
                    group.Value <- -1
                else 
                    group.Value <- oldGroup
                Log.warn "showing group %d" group.Value
            )
        )
    )
    ctrl.Keyboard.DownWithRepeats.Values.Add (fun k ->
        if k = Keys.G then
            transact (fun () ->
                group.Value <- group.Value + 1
                Log.warn "showing group %d" group.Value
            )
    )
    use task = 
        RenderTask.ofList [
            //app.Runtime.CompileClear(ctrl.FramebufferSignature, Mod.constant C4f.Black, Mod.constant 1.0)
            app.Runtime.CompileRender(ctrl.FramebufferSignature, sg)
        ]

    ctrl.RenderTask <- task |> DefaultOverlays.withStatistics

    f.Run()
    0
