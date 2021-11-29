// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open FShade
open Aardvark.Base

open FSharp.Data.Adaptive
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open FontRendering
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

#nowarn "9"

module Shader =
    type Vertex = { 
        [<Position>] pos : V4d 
        [<TexCoord>] tc : V2d
        [<Color>] color : V4d
        [<Semantic("InstanceTrafo")>] trafo : M44d
    }

    let trafo (v : Vertex) =
        vertex {

            let wp = uniform.ModelTrafo * (v.trafo * v.pos)
            return { 
                pos = uniform.ViewProjTrafo * wp
                tc = v.tc
                trafo = v.trafo
                color = v.color
            }
        }

    let white (v : Vertex) =
        fragment {
            return V4d.IIII
        }


type CameraMode =
    | Orbit
    | Fly
    | Rotate


type BlaNode(calls : aval<DrawCallInfo[]>, mode : IndexedGeometryMode) =
    interface ISg

    member x.Mode = mode

    member x.Calls = calls

module Sems =
    open Aardvark.SceneGraph.Semantics
    open Aardvark.Base.Ag

    [<Aardvark.Base.Rule>]
    type BlaSems () =
        
        member x.RenderObjects(b : BlaNode, scope : Ag.Scope) =
            
            let o = RenderObject.ofScope scope

            o.Mode <- b.Mode

            o.DrawCalls <- Indirect (b.Calls |> AVal.map ( fun arr -> IndirectBuffer.ofArray false arr))

            ASet.single (o :> IRenderObject)




module VulkanTests =
    open System.Threading
    open Aardvark.Rendering.Vulkan
    open Microsoft.FSharp.NativeInterop

    type Brick<'a>(level : int, index : V3i, data : Tensor4<'a>) =
        let mutable witness : Option<IDisposable> = None

        member x.Level = level
        member x.Index = index
        member x.Data = data

        member x.Witness
            with get() = witness
            and set r = witness <- r

    let run() =
        use app = new HeadlessVulkanApplication(true)
        let device = app.Device


        let size = V3i(1024, 512, 256)
        let brickSize = V3i(128,128,128)
        let levels = 8

        let rand = RandomSystem()
        let img = app.Runtime.CreateSparseTexture<uint16>(size, levels, 1, TextureDimension.Texture3D, Col.Format.Gray, brickSize, 2L <<< 30)

        let randomTensor (s : V3i) =
            let data = new Tensor4<uint16>(V4i(s.X, s.Y, s.Z, 1))
            data.SetByIndex(fun _ -> rand.UniformInt() |> uint16)

        let bricks =
            [|
                for l in 0 .. img.MipMapLevels - 1 do
                    let size = img.Size / (1 <<< l)

                    let size = V3i(min brickSize.X size.X, min brickSize.Y size.Y, min brickSize.Z size.Z)
                    let cnt = img.GetBrickCount l
                    for x in 0 .. cnt.X - 1 do
                        for y in 0 .. cnt.Y - 1 do
                            for z in 0 .. cnt.Z - 1 do
                                yield Brick(l, V3i(x,y,z), randomTensor size)

            |]

        let mutable resident = 0

        let mutable count = 0

        let residentBricks = System.Collections.Generic.HashSet<Brick<uint16>>()
        let mutable frontBricks = Array.zeroCreate 0

        img.OnSwap.Add (fun _ ->
            frontBricks <- lock residentBricks (fun () -> Aardvark.Base.HashSet.toArray residentBricks)
        )



        let renderResult =
            img.Texture |> AVal.map (fun t ->
                let img = unbox<Image> t
                let size = brickSize


                let tensor = Tensor4<uint16>(V4i(brickSize, 1))

                let sizeInBytes = int64 brickSize.X * int64 brickSize.Y * int64 brickSize.Z * int64 sizeof<uint16>
                use tempBuffer = device.HostMemory |> Buffer.create (VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit) sizeInBytes
                

                device.perform {
                    do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)
                }

                let result = 
                    [
                        for b in frontBricks do
                            let size = V3i b.Data.Size.XYZ
                            
                            device.perform {
                                do! Command.Copy(img.[ImageAspect.Color, b.Level, 0], b.Index * brickSize, tempBuffer, 0L, V2i.OO, size)
                            }
                                
                            tempBuffer.Memory.MappedTensor4<uint16>(
                                V4i(size.X, size.Y, size.Z, 1),
                                fun src ->
                                    NativeTensor4.using tensor (fun dst ->
                                        let subDst = dst.SubTensor4(V4i.Zero, V4i(size.X, size.Y, size.Z, 1))
                                        NativeTensor4.copy src subDst
                                    )
                            )




                            let should = b.Data
                            let real = tensor.SubTensor4(V4i.Zero, V4i(size, 1))
                            let equal = should.InnerProduct(real, (=), true, (&&))
                            if not equal then
                                yield b


                    ]
                
                result
            )



        let cancel = new CancellationTokenSource()

        let mutable modifications = 0
        let mutable totalErros = 0L
        let uploader() =
            try
                let ct = cancel.Token
                let mutable cnt = 0
                while true do 
                    ct.ThrowIfCancellationRequested()

                    cnt <- cnt + 1
                    let brickIndex = rand.UniformInt(bricks.Length)
                    let brick = bricks.[brickIndex]

                    lock brick (fun () ->
                        match brick.Witness with
                            | Some w ->
                                // swap() -> frontBricks.Contains brick
                                lock residentBricks (fun () -> residentBricks.Remove brick |> ignore)
                                w.Dispose()
                                Interlocked.Decrement(&resident) |> ignore
                                brick.Witness <- None
                            | None ->
                                //Log.line "commit(%d, %A)" brick.Level brick.Index
                                let witness = 
                                    NativeTensor4.using brick.Data (fun data ->
                                        img.UploadBrick(brick.Level, 0, brick.Index, data)
                                    )
                                brick.Witness <- Some witness
                                lock residentBricks (fun () -> residentBricks.Add brick |> ignore)
                                Interlocked.Increment(&resident) |> ignore
                        Interlocked.Increment(&modifications) |> ignore
                    )

            with _ -> ()

        let sw = System.Diagnostics.Stopwatch()

        let renderer() =
            try
                let ct = cancel.Token
                while true do
                    ct.ThrowIfCancellationRequested()
//                    sw.Start()
//                    AVal.force img.Texture |> ignore
//                    sw.Stop()
//                    Interlocked.Increment(&count) |> ignore
//
//                    if count % 10 = 0 then
//                        Log.start "frame %d" count
//                        Log.line "modifications: %A" modifications
//                        Log.line "resident:      %A" resident
//                        Log.line "force:         %A" (sw.MicroTime / 10.0)
//                        Log.stop()
//                        sw.Reset()
//
//                    Thread.Sleep(16)
    
    
                    let errors = AVal.force renderResult

                    match errors with
                        | [] -> 
                            Log.start "frame %d" count
                            Log.line "modifications: %A" modifications
                            Log.line "resident: %A" resident
                            if totalErros > 0L then Log.warn "totalErros %A" totalErros
                            Log.stop()
                        | _ ->
                            let errs = List.length errors |> int64
                            totalErros <- totalErros + errs
                            Log.warn "errors: %A" errs
                            ()
    
    
            with _ -> ()




        let startThread (f : unit -> unit) =
            let t = new Thread(ThreadStart(f))
            t.IsBackground <- true
            t.Start()
            t

        let uploaders = 
            Array.init 1 (fun _ -> startThread uploader)

        let renderers = 
            Array.init 1 (fun _ -> startThread renderer)
            

        Console.ReadLine() |> ignore
        cancel.Cancel()

        for t in uploaders do t.Join()
        for t in renderers do t.Join()

        img.Dispose()

let tensorPerformance() =
    
    for sizeE in 4 .. 9 do
        let size = V4i(1 <<< sizeE, 1 <<< sizeE, 1 <<< sizeE, 1)
        //let size = V4i(1024,512,512,1)
        let iter = 30

        printfn " 0: copy %A" size

        let srcManaged = new Tensor4<float32>(size)
        let dstManaged = new Tensor4<float32>(size)

        let s = V4l size
        let srcManaged = 
            srcManaged.SubTensor4(
                V4l(10L, 10L, 10L, 0L),
                srcManaged.Size - V4l(11L, 12L, 13L, 0L)
            )

        let dstManaged = 
            dstManaged.SubTensor4(
                V4l(10L, 10L, 10L, 0L),
                dstManaged.Size - V4l(11L, 12L, 13L, 0L)
            )



        let sw = System.Diagnostics.Stopwatch()
        // warmup
        for i in 1 .. 2 do
            dstManaged.Set(srcManaged) |> ignore

        printf " 0:     managed: "
        sw.Restart()
        for i in 1 .. iter do
            dstManaged.Set(srcManaged) |> ignore
        sw.Stop()

        printfn "%A" (sw.MicroTime / iter)

        let sizeInBytes = nativeint size.X * nativeint size.Y * nativeint size.Z * nativeint size.W * nativeint sizeof<float32>
        let srcPtr = Marshal.AllocHGlobal sizeInBytes |> NativePtr.ofNativeInt
        let dstPtr = Marshal.AllocHGlobal sizeInBytes |> NativePtr.ofNativeInt
        let srcNative = NativeTensor4<float32>(srcPtr, srcManaged.Info)
        let dstNative = NativeTensor4<float32>(dstPtr, dstManaged.Info)
        // warmup
        for i in 1 .. 2 do
            srcNative.CopyTo dstNative

        printf " 0:     native: "
        sw.Restart()
        for i in 1 .. iter do
            srcNative.CopyTo dstNative
        sw.Stop()

        printfn "%A" (sw.MicroTime / iter)


        let srcRaw = NativePtr.toNativeInt srcPtr
        let dstRaw = NativePtr.toNativeInt dstPtr
        
        // warmup
        for i in 1 .. 2 do
            Marshal.Copy(srcRaw, dstRaw, sizeInBytes)
            
        printf " 0:     raw: "
        sw.Restart()
        for i in 1 .. iter do
            Marshal.Copy(srcRaw, dstRaw, sizeInBytes)
        sw.Stop()

        printfn "%A" (sw.MicroTime / iter)



        NativePtr.free srcPtr
        NativePtr.free dstPtr






let ellipseTest() =
  
    
    Aardvark.Init()
    
    
    let rand = RandomSystem()
    for i in 1 .. 10000 do rand.UniformDouble() |> ignore

    
    use app = new OpenGlApplication()
    use win = app.CreateGameWindow(8)
    let proj = win.Sizes |> AVal.map (fun s -> Trafo3d.Scale(float s.Y / float s.X, 1.0, 1.0))
    
    let scale = cval 1.0
    let kind = cval 0


    win.Mouse.Scroll.Values.Add (fun d ->
        transact (fun () ->
            let d1 = d / 120.0
            scale.Value <- scale.Value * (1.05 ** d1)
        )
    )

    win.Keyboard.DownWithRepeats.Values.Add (fun k ->
        match k with
        | Keys.Space -> transact (fun () -> kind.Value <- kind.Value + 1)
        | _ -> ()
    )


    let e0s = 
        [
            //PathSegment.bezier2 (V2d(-0.5,0.0)) (V2d(0.0, 0.5)) (V2d(0.5, 0.0))
            //PathSegment.arc 0.0 -Constant.Pi (Ellipse2d(V2d(0.0, -0.2), 0.5*V2d.IO, 0.5*V2d.OI))
            
            let trafo = Trafo2d.Rotation(Constant.PiHalf)

            let p0 = V2d(-0.5,-0.5)  |> trafo.Forward.TransformPos
            let p1 = V2d(3.0, -0.5) |> trafo.Forward.TransformPos
            let p2 = V2d(-3.0, 0.5) |> trafo.Forward.TransformPos
            let p3 = V2d(0.5, 0.5) |> trafo.Forward.TransformPos

            yield PathSegment.line (V2d(-0.5,-0.7)) (V2d(0.5,-0.7))

            
            yield PathSegment.bezier2 (V2d(0.7,-0.7)) (V2d(0.95,0.0)) (V2d(0.7,0.7))
            
            yield PathSegment.bezier3 p0 p1 p2 p3


            yield PathSegment.arc 0.0 -Constant.PiTimesTwo (Ellipse2d(V2d.Zero, 0.7 * V2d.IO, 0.5 * V2d.OI))

            //PathSegment.bezier3 (V2d(-0.5,-0.2)) (V2d(-0.2, 0.7)) (V2d(0.2, 0.7)) (V2d(0.5, -0.2))
        ]

    let e1 = 
        AVal.custom (fun t ->
            let k = kind.GetValue t
            let p = win.Mouse.Position.GetValue(t).Position
            let s = win.Sizes.GetValue t
            let scale = scale.GetValue t
            let ndc = 
                V3d(
                    2.0 * (float p.X / float s.X) - 1.0, 
                    1.0 - 2.0 * (float p.Y / float s.Y),
                    -1.0
                )

            let cc = proj.GetValue().Backward.TransformPosProj ndc |> Vec.xy

            let e = Ellipse2d(cc, scale * 0.2*V2d.IO, scale * 0.3*V2d.OI)
            //PathSegment.arc 0.0 -Constant.Pi e

            let trafo = Trafo2d.Scale(scale) * Trafo2d.Translation(cc)

            let p0 = V2d(-0.5,-0.5)  |> trafo.Forward.TransformPos
            let p1 = V2d(3.0, -0.5) |> trafo.Forward.TransformPos
            let p2 = V2d(-3.0, 0.5) |> trafo.Forward.TransformPos
            let p3 = V2d(0.5, 0.5) |> trafo.Forward.TransformPos


            match k % 4 with
            | 0 -> PathSegment.line (cc - V2d.Half*scale) (cc + V2d.Half*scale)
            | 1 -> PathSegment.bezier2 (cc + V2d(-0.5*scale, 0.5*scale)) (V2d(cc.X, cc.Y-0.5*scale)) (cc + V2d(0.5*scale, 0.5*scale)) 
            | 2 -> PathSegment.arc 0.0 -(0.75 * Constant.PiTimesTwo) e
            | _ -> PathSegment.bezier3 p0 p1 p2 p3
        )

    let allIntersections =
        e1 |> AVal.map (fun e1 ->   
            let map =
                e0s |> List.choose (fun e0 ->
                    let intersections = PathSegment.intersections 1E-9 e0 e1
                    match intersections with
                    | [] -> None
                    | _ -> Some (e0, intersections)
                )
                |> HashMap.ofList
            e1, map
        )


    let intersections =
        allIntersections |> AVal.map (fun (e1, map) ->  
            map |> HashMap.toArray |> Array.collect (fun (e0, ts) ->
                ts |> List.toArray |> Array.map (fun (t0, t1) ->
                    let p0 = PathSegment.point t0 e0
                    let p1 = PathSegment.point t1 e1
                    V3f(V2f p0, -1.0f), V3f(V2f p1, -0.9f)
                )
            )
            |> Array.unzip
        )

    let toGeometry (color : C4b) (s : aval<option<PathSegment>>) =
        let positions = 
            s |> AVal.map (fun s ->
                match s with
                | Some s -> 
                    let lines = System.Collections.Generic.List<V3f>()
                    let div = 128
                    let step = 1.0 / float div

                    let mutable last = PathSegment.startPoint s
                    let mutable t = step
                    for i in 0 .. div - 1 do
                        let pt = PathSegment.point t s
                        lines.Add (V3f(V2f last, 0.0f))
                        lines.Add (V3f(V2f pt, 0.0f))
                        last <- pt
                        t <- t + step
                    lines.ToArray()
                | None ->
                    [||]
            )
            
        Sg.draw IndexedGeometryMode.LineList
        |> Sg.vertexAttribute DefaultSemantic.Positions positions
        |> Sg.vertexBufferValue DefaultSemantic.Colors (AVal.constant (color.ToC4f().ToV4f()))
        
    let splitted =
        allIntersections |> AVal.map (fun (e1,splits) ->
            let ts = splits |> HashMap.toList |> List.collect snd |> List.map snd
            PathSegment.splitMany ts e1 |> List.indexed |> HashMap.ofList
        )
        |> AMap.ofAVal


    let colors =
        Dict.ofList [
            0, C4b.Red
            1, C4b.Green
            2, C4b.Blue
            3, C4b.Yellow
            4, C4b.Cyan
            5, C4b.Magenta
            6, C4b.VRVisGreen
        ]
    let inline getColor (i : int) =
        colors.GetOrCreate(i, fun _ ->
            rand.UniformC3f().ToC4b()
        )

    let shapes =
        Sg.ofList [
            Sg.set (
                let last = AMap.count splitted |> AVal.map (fun c -> max (Fun.NextPowerOfTwo (c-1)) 32)
                ASet.range (AVal.constant 0) last |> ASet.map (fun i ->
                    let color = getColor i
                    let r = AMap.tryFind i splitted
                    toGeometry color r
                )
            )
            for e0 in e0s do
                let color = rand.UniformC3f().ToC4b()
                toGeometry color (AVal.constant (Some e0))
        ]
        |> Sg.uniform "LineWidth" (AVal.constant 3.0)
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.thickLine
            do! DefaultSurfaces.thickLineRoundCaps
        }


    let sg = 
        Sg.ofList [
            Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.map fst intersections)
            |> Sg.vertexBufferValue DefaultSemantic.Colors (AVal.constant V4f.IOOI)
            |> Sg.uniform "PointSize" (AVal.constant 5.0)
        
            Sg.draw IndexedGeometryMode.PointList
            |> Sg.vertexAttribute DefaultSemantic.Positions (AVal.map snd intersections)
            |> Sg.vertexBufferValue DefaultSemantic.Colors (AVal.constant V4f.OIOI)
            |> Sg.uniform "PointSize" (AVal.constant 7.0)
        ]
        |> Sg.andAlso shapes
        |> Sg.projTrafo proj
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.pointSprite
            do! DefaultSurfaces.pointSpriteFragment
        }

    win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, sg)
    win.Run()



open Aardvark.Rendering.Text

[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    //ellipseTest()


    use app = new OpenGlApplication(true, false)
    let win = app.CreateGameWindow(8)
    

    let cam = CameraViewWithSky(Location = V3d.III * 2.0, Forward = -V3d.III.Normalized)
    let proj = CameraProjectionPerspective(60.0, 0.1, 1000.0, float 1024 / float 768)

    let geometry = 
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = [| 0; 1; 2; 0; 2; 3 |],
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions,                  [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                ]
        )



    let trafos =
        [|
            for x in -4..4 do
                for y in -4..4 do
                    yield Trafo3d.Translation(2.0 * float x - 0.5, 2.0 * float y - 0.5, 0.0)
        |]

    let trafos = trafos |> AVal.constant

    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI

    let mode = AVal.init Fly
    let controllerActive = AVal.init true

    let flyTo = AVal.init Box3d.Invalid

    let chainM (l : aval<list<afun<'a, 'a>>>) =
        l |> AVal.map AFun.chain |> AFun.bind id

    let cam = DefaultCameraController.control win.Mouse win.Keyboard win.Time cam // |> AFun.integrate controller
    //let cam = cam |> AFun.integrate (controller resetPos resetDir)

        
//    let test = sgs |> ASet.map id
//    let r = test.GetReader()
//    r.GetDelta() |> List.length |> printfn "got %d deltas"


    let all = "abcdefghijklmnopqrstuvwxyz\r\nABCDEFGHIJKLMNOPQRSTUVWXYZ\r\n1234567890 ?ß\\\r\n^°!\"§$%&/()=?´`@+*~#'<>|,;.:-_µ"
       
    let md = 
        "# Heading1\r\n" +
        "## Heading2\r\n" + 
        "\r\n" +
        "This is ***markdown*** code being parsed by *CommonMark.Net*  \r\n" +
        "It seems to work quite **well**\r\n" +
        "*italic* **bold** ***bold/italic***\r\n" +
        "\r\n" +
        "    type A(a : int) = \r\n" + 
        "        member x.A = a\r\n" +
        "\r\n" + 
        "regular Text again\r\n"+
        "\r\n" +
        "-----------------------------\r\n" +
        "is there a ruler???\r\n" + 
        "1) First *item*\r\n" + 
        "2) second *item*\r\n" +
        "\r\n"+
        "* First *item*  \r\n" + 
        "with multiple lines\r\n" + 
        "* second *item*\r\n" 

    let message = 
        "This is Aardvark rendering weird crazy fonts. 😀😁😎"
    // old school stuff here^^

    // here's an example-usage of AIR (Aardvark Imperative Renderer) 
    // showing how to integrate arbitrary logic in the SceneGraph without
    // implementing new nodes for that
    let quad = 
        Sg.air { 
            // inside an air-block we're allowed to read current values
            // which will be inherited from the SceneGraph
            let! parentRast = AirState.rasterizerState

            // modes can be modified by simply calling the respective setters.
            // Note that these setters are overloaded with and without aval<Mode>
            do! Air.DepthTest    DepthTest.LessOrEqual
            do! Air.CullMode     CullMode.None
            do! Air.BlendMode    BlendMode.None
            do! Air.FillMode     FillMode.Fill
            do! Air.StencilMode  StencilMode.None

            // we can also override the shaders in use (and with FSHade)
            // build our own dynamic shaders e.g. depending on the inherited 
            // FillMode from the SceneGraph
            do! Air.BindShader {
                    do! DefaultSurfaces.trafo               
                    
                    // if the parent fillmode is not filled make the quad red.
                    let fill = parentRast.FillMode |> AVal.force
                    match fill with
                        | FillMode.Fill -> do! DefaultSurfaces.diffuseTexture
                        | _ -> do! DefaultSurfaces.constantColor C4f.Red 

                }

            // uniforms can be bound using lists or one-by-one
            do! Air.BindUniforms [
                    "Hugo", uniformValue 10 
                    "Sepp", uniformValue V3d.Zero
                ]

            do! Air.BindUniform(
                    Symbol.Create "BlaBla",
                    Trafo3d.Identity
                )

            // textures can also be bound (using file-texture here)
            do! Air.BindTexture(
                    DefaultSemantic.DiffuseColorTexture, 
                    @"E:\Development\WorkDirectory\DataSVN\pattern.jpg"
                )

            do! Air.BindVertexBuffers [
                    DefaultSemantic.Positions,                  attValue [|V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO|]
                    DefaultSemantic.DiffuseColorCoordinates,    attValue [|V2f.OO; V2f.IO; V2f.II; V2f.OI|]
                ]

            do! Air.BindIndexBuffer [| 
                    0;1;2
                    0;2;3 
                |]
        

            // since for some effects it is not desireable to write certain pixel-outputs
            // one can change the current WriteBuffers using a list of written semantics
            do! Air.WriteBuffers [
                    DefaultSemantic.Depth
                    DefaultSemantic.Colors
                ]

            // topology can be set separately (not by the DrawCall)
            do! Air.Toplogy IndexedGeometryMode.TriangleList


            // trafos keep their usual stack-semantics and can be pushed/poped
            // initially the trafo-stack is filled with all trafos inherited 
            // from the containing SceneGraph
            do! Air.PushTrafo (Trafo3d.Scale 5.0)

            // draw the quad 10 times and step by 1/5 in z every time
            for y in 1..10 do
                do! Air.Draw 6
                do! Air.PushTrafo (Trafo3d.Translation(0.0,0.0,1.0/5.0))



        }


    let mode = AVal.init FillMode.Fill

    let config = MarkdownConfig.light

    let label1 =
        Sg.markdown config (AVal.constant md)
            |> Sg.scale 0.1
            |> Sg.billboard

    let shape = 
        let a = V2d(-0.1,0.0)
        let b = V2d(0.1,0.0)
        let r = 0.13
        let d = a - b
        let len = Vec.length d
        let m = (a + b) / 2.0
        let n = V2d(-d.Y, d.X) |> Vec.normalize
        let d = sqrt (r*r - sqr (len / 2.0))
        let c0 = m - n * d
        let c1 = m + n * d

        let e0 = Ellipse2d(c0, V2d.IO * r, V2d.OI * r)
        let e1 = Ellipse2d(c1, V2d.IO * r, V2d.OI * r)
        let a0 = e0.GetAlpha a
        let b0 = e0.GetAlpha b
        let a1 = e1.GetAlpha a
        let b1 = e1.GetAlpha b

        let d0s = a0 - b0
        let d0l = 
            if d0s < 0.0 then Constant.PiTimesTwo + d0s
            else d0s - Constant.PiTimesTwo

        let d1s = a1 - b1
        let d1l = 
            if d1s < 0.0 then Constant.PiTimesTwo + d1s
            else d1s - Constant.PiTimesTwo

        ShapeList.ofList [
            ConcreteShape.fillRoundedRectangle C4b.Yellow 0.1 (Box2d.FromCenterAndSize(V2d.Zero, V2d.II))
            ConcreteShape.roundedRectangle C4b.Red 0.1 0.3 (Box2d.FromCenterAndSize(V2d.Zero, V2d.II))

            ConcreteShape.fillEllipse C4b.Green (Ellipse2d(V2d.Zero, V2d.IO * 0.4, V2d.OI * 0.3))
            
            ConcreteShape.ellipse C4b.Red (0.1) (Ellipse2d(V2d.Zero, V2d.IO * 0.4, V2d.OI * 0.3))

            ConcreteShape.ofList M33d.Identity C4b.White  [
                PathSegment.line (V2d(0.1, 0.1)) b
                PathSegment.arc b0 d0l e0
                PathSegment.line a (V2d(-0.1, 0.1))
                PathSegment.line (V2d(-0.1, 0.1)) (V2d(0.1, 0.1))
            ]

            ConcreteShape.ofList (M33d.Translation(0.3, 0.0)) C4b.White [
                PathSegment.line (V2d(0.1, 0.2)) b
                PathSegment.arc b0 d0s e0
                PathSegment.line a (V2d(-0.1, 0.2))
                PathSegment.line (V2d(-0.1, 0.2)) (V2d(0.1, 0.2))
            ]

            ConcreteShape.ofList (M33d.Translation(-0.3, 0.0)) C4b.White [
                PathSegment.line (V2d(0.1, 0.2)) b
                PathSegment.arc b1 d1s e1
                PathSegment.line a (V2d(-0.1, 0.2))
                PathSegment.line (V2d(-0.1, 0.2)) (V2d(0.1, 0.2))
            ]

            ConcreteShape.ofList (M33d.Translation(-0.6, 0.0)) C4b.White [
                PathSegment.line (V2d(0.1, 0.2)) b
                PathSegment.arc b1 d1l e1
                PathSegment.line a (V2d(-0.1, 0.2))
                PathSegment.line (V2d(-0.1, 0.2)) (V2d(0.1, 0.2))
            ]
            ConcreteShape.ofList (M33d.Translation(0.6, 0.0)) C4b.White [
                PathSegment.line (V2d(0.1, 0.2)) b
                PathSegment.bezier2 b (V2d(0.0, 0.3)) a
                PathSegment.line a (V2d(-0.1, 0.2))
                PathSegment.line (V2d(-0.1, 0.2)) (V2d(0.1, 0.2))
            ]

            //ConcreteShape.ofList (M33d.Translation(0.0, -0.5)) C4b.Yellow [
            //    PathSegment.arc Constant.Pi Constant.Pi (Ellipse2d(m, -V2d.IO * len/2.0, V2d.OI * len/2.0))
            //    PathSegment.line a (V2d(-0.1, 0.2))
            //    PathSegment.line (V2d(-0.1, 0.2)) (V2d(0.1, 0.2))
            //    PathSegment.line (V2d(0.1, 0.2)) b
            //]
            



        ]

    let message = AVal.constant message
    let label2 =
        //Sg.text f C4b.Green message
        Sg.markdown MarkdownConfig.light message
            |> Sg.scale 0.1
            |> Sg.billboard
            |> Sg.translate 5.0 0.0 0.0

    let aa = AVal.init true



    let f = Aardvark.Rendering.Text.FontSquirrel.Hack.Regular

    //let r = f.Layout "asdsadas"
    //let bb = r.bounds



    let label3 =
        Sg.text f C4b.White message
        |> Sg.scale 0.1
        |> Sg.transform (Trafo3d.FromBasis(-V3d.IOO, V3d.OOI, V3d.OIO, V3d(0.0, 0.0, 0.2)))
            
    let f = Aardvark.Rendering.Text.FontSquirrel.Leafy_glade.Regular
    let label4 =
        Sg.text f C4b.White message
        |> Sg.scale 0.1
        |> Sg.transform (Trafo3d.FromBasis(-V3d.IOO, V3d.OOI, V3d.OIO, V3d(0.0, 0.0, 0.0)))
           
    let f = Aardvark.Rendering.Text.FontSquirrel.Roboto.Regular
    let label5 =
        Sg.text f C4b.White (AVal.constant "or just regular ones")
        |> Sg.scale 0.1
        |> Sg.transform (Trafo3d.FromBasis(-V3d.IOO, V3d.OOI, V3d.OIO, V3d(0.0, 0.0, -0.2)))

    let active = AVal.init true

    let sg = 
        active |> AVal.map (fun a ->
            if a then
                Sg.ofList [label3; label4; label5]
                    //|> Sg.andAlso quad
                    //|> Sg.andAlso (Sg.shape (AVal.constant shape))
                    |> Sg.viewTrafo (cam |> AVal.map CameraView.viewTrafo)
                    |> Sg.projTrafo (win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y) |> Frustum.projTrafo))

                    |> Sg.projTrafo (win.Sizes |> AVal.map (fun s -> Trafo3d.Scale (1.0, float s.X / float s.Y, 1.0)))
                    |> Sg.fillMode mode
                    |> Sg.uniform "Antialias" aa
            else
                Sg.ofList []
        ) |> Sg.dynamic

    win.Keyboard.KeyDown(Keys.Enter).Values.Add (fun _ ->
        transact (fun () ->
            active.Value <- not active.Value
        )
    )

    win.Keyboard.KeyDown(Keys.F8).Values.Add (fun _ ->
        transact (fun () ->
            match mode.Value with
                | FillMode.Fill -> mode.Value <- FillMode.Line
                | _ -> mode.Value <- FillMode.Fill
        )
    )
    win.Keyboard.KeyDown(Keys.F7).Values.Add (fun _ ->
        transact (fun () ->
            aa.Value <- not aa.Value

            if aa.Value then Log.warn "AA enabled"
            else Log.warn "AA disabled"
        )
    )

    let config = { BackendConfiguration.Default with useDebugOutput = true }

    let calls = AVal.init 999
    
    let randomCalls(cnt : int) =
        [|
            for i in 0..cnt-1 do
                yield
                    { DrawCallInfo.empty with
                        FaceVertexCount = 1
                        InstanceCount = 1
                        FirstIndex = i
                    }
        |]

    let calls = AVal.init (randomCalls 990)

    win.Keyboard.DownWithRepeats.Values.Add (function
        | Keys.Add ->
            transact (fun () ->
                let cnt = (calls.Value.Length + 10) % 1000
                calls.Value <- randomCalls cnt
                printfn "count = %d" cnt
            )
        | Keys.Subtract ->
            transact (fun () ->
                let cnt = (calls.Value.Length + 990) % 1000
                calls.Value <- randomCalls cnt
                printfn "count = %d" cnt
            )
        | Keys.Enter ->
            transact (fun () ->
                let cnt = calls.Value.Length % 1000
                calls.Value <- randomCalls cnt
                printfn "count = %d" cnt
            )
        | _ -> ()
    )


    let pos =
        let rand = RandomSystem()
        Array.init 1000 (ignore >> rand.UniformV3d >> V3f)

    let trafo = win.Time |> AVal.map (fun t -> Trafo3d.RotationZ (float t.Ticks / float TimeSpan.TicksPerSecond))

    let blasg = 
        BlaNode(calls, IndexedGeometryMode.PointList)
            |> Sg.vertexAttribute' DefaultSemantic.Positions pos
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
            }
            |> Sg.trafo trafo
            |> Sg.viewTrafo (cam |> AVal.map CameraView.viewTrafo)
            |> Sg.projTrafo (win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y) |> Frustum.projTrafo))


    let main = app.Runtime.CompileRender(win.FramebufferSignature, config, sg) //|> DefaultOverlays.withStatistics
    //let clear = app.Runtime.CompileClear(win.FramebufferSignature, AVal.constant C4f.Black)

    //win.Keyboard.Press.Values.Add (fun c ->
    //    if c = '\b' then
    //        if message.Value.Length > 0 then
    //            transact (fun () -> message.Value <- message.Value.Substring(0, message.Value.Length - 1))
    //    else
    //        transact (fun () -> message.Value <- message.Value + string c)
    //)

    win.RenderTask <- main
    win.Run()
    win.Dispose()
    0 
