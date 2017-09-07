// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.

open System
open FShade
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.Rendering
open Aardvark.Rendering.NanoVg
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open System.Windows.Media
open System.Windows
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


type BlaNode(calls : IMod<DrawCallInfo[]>, mode : IndexedGeometryMode) =
    interface ISg

    member x.Mode = mode

    member x.Calls = calls

module Sems =
    open Aardvark.SceneGraph.Semantics
    open Aardvark.Base.Ag

    [<Aardvark.Base.Ag.Semantic>]
    type BlaSems () =
        
        member x.RenderObjects(b : BlaNode) =
            
            let o = RenderObject.create()

            o.Mode <- Mod.constant b.Mode

            o.IndirectBuffer <- 
                b.Calls 
                    |> Mod.map ( fun arr -> IndirectBuffer(ArrayBuffer(arr), arr.Length ) :> IIndirectBuffer )

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
            frontBricks <- lock residentBricks (fun () -> HashSet.toArray residentBricks)
        )



        let renderResult =
            img.Texture |> Mod.map (fun t ->
                let img = unbox<Image> t
                let size = brickSize


                let tensor = Tensor4<uint16>(V4i(brickSize, 1))

                let sizeInBytes = int64 brickSize.X * int64 brickSize.Y * int64 brickSize.Z * int64 sizeof<uint16>
                let tempBuffer = device.HostMemory |> Buffer.create (VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.TransferDstBit) sizeInBytes
                

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
                
                device.Delete tempBuffer
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
//                    Mod.force img.Texture |> ignore
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
    
    
                    let errors = Mod.force renderResult

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
    
    type VkExternalMemoryHandleTypeFlagBitsKHR =
        | OpaqueFd = 0x00000001
        | OpaqueWin32 = 0x00000002
        | OpaqueWin32KMT = 0x00000004
        | D3D11Texture = 0x00000008
        | D3D11TextureKMT = 0x00000010
        | D3D12Heap = 0x00000020
        | D3D12Resource = 0x00000040

    type VkStructureType with
        static member inline MemoryWin32Import = unbox<VkStructureType> 1000073000

    [<StructLayout(LayoutKind.Sequential)>]
    type VkImportMemoryWin32HandleInfoKHR =
        struct
            val mutable public sType : VkStructureType
            val mutable public pNext : nativeint
            val mutable public handleType : VkExternalMemoryHandleTypeFlagBitsKHR
            val mutable public handle : nativeint
            val mutable public name : cstr

            new(stype, pnext, handleType, handle, name) =
                {
                    sType = stype
                    pNext = pnext
                    handleType = handleType
                    handle = handle
                    name = name
                }

        end

    open FShade
    open Aardvark.Rendering.Vulkan
    
    let sourceSampler =
        sampler2d {
            texture uniform?SourceImage
            addressU WrapMode.Wrap
            addressV WrapMode.Wrap
            filter Filter.MinMagMipLinear
        }

    [<LocalSize(X = 8, Y = 4)>]
    let invertShader (factor : float) (dst : Image2d<Formats.rgba8>) =
        compute {
            let id = getGlobalId().XY

            let v = sourceSampler.[id,0].XYZ


            dst.[id] <- V4d(V3d.III - v, 1.0)

        }

    [<LocalSize(X = 32)>]
    let copyToBufferShader (factor : float) (dst : uint32[]) =
        compute {
            let id = getGlobalId().X
            let size = sourceSampler.Size

            let c = V2i(id % size.X, id / size.X)

            let v = sourceSampler.[c, 0]
            dst.[id] <- packUnorm4x8 v

        }

    let invertImage (shader : ComputeShader) (pool : DescriptorPool) (src : ITexture) (dst : Image) =
        let device = shader.Device

        use args = ComputeShader.newInputBinding shader pool
        //args.["SourceImage"] <- src
        args.["dst"] <- dst
        //args.["factor"] <- 0.5
        args.Flush()

        device.perform {
            do! Command.Bind shader
            do! Command.SetInputs args
            do! Command.Dispatch(dst.Size)
        }

    [<Literal>]
    let scanSize = 2048

    [<Literal>]
    let halfScanSize = 1024

    [<LocalSize(X = halfScanSize)>]
    let scan2048 (inputOffset : int) (inputDelta : int) (inputSize : int) (input : float[]) (outputOffset : int) (outputDelta : int) (output : float[]) =
        compute {
            let mem : float[] = allocateShared scanSize
            let gid = getGlobalId().X

            let gid0 = gid
            let lid0 =  getLocalId().X

            let lgid = 2 * gid0
            let rgid = lgid + 1
            
            let llid = 2 * lid0
            let rlid = llid + 1

            if lgid < inputSize then mem.[llid] <- input.[inputOffset + lgid * inputDelta]
            else mem.[llid] <- 0.0

            if rlid < inputSize then mem.[rlid] <- input.[inputOffset + rgid * inputDelta]
            else mem.[rlid] <- 0.0

            barrier()
            
            let mutable s = 1
            let mutable d = 2
            while d <= scanSize do
                if llid % d = 0 && llid >= s then
                    mem.[llid] <- mem.[llid] + mem.[llid - s]

                barrier()
                s <- s <<< 1
                d <- d <<< 1

            d <- d >>> 1
            s <- s >>> 1
            while s >= 1 do
                if llid % d = 0 && llid + s < scanSize then
                    mem.[llid + s] <- mem.[llid + s] + mem.[llid]
                    
                barrier()
                s <- s >>> 1
                d <- d >>> 1

            if lgid < inputSize then
                output.[outputOffset + lgid * outputDelta] <- mem.[llid]
            if rgid < inputSize then
                output.[outputOffset + rgid * outputDelta] <- mem.[rlid]

        }
        
    [<LocalSize(X = halfScanSize)>]
    let addParentSum (dataLength : int) (offset : int) (scan : float[]) (data : float[]) =
        compute {
            let id = offset + getGlobalId().X
            let pid = id / offset - 1
            let s = scan.[pid]


            if id < dataLength then
                data.[id] <- data.[id] + s

        }

    [<LocalSize(X = 8, Y = 8)>]
    let reduce8 (dst : Image2d<Formats.rgba8>) =
        compute {
            let mem : V3d[] = allocateShared 64

            let lid = getLocalId().XY
            let gid = getGlobalId().XY

            let li = lid.Y * 8 + lid.X
            mem.[li] <- sourceSampler.[gid, 0].XYZ
            barrier()

            let mutable off = 1
            let mutable d = 2
            while d <= 64 do
                if li % d = 0 && li + off < 64 then
                    mem.[li] <- mem.[li] + mem.[li + off]

                barrier()
                off <- off <<< 1
                d <- d <<< 1

            if li = 0 then
                let di = gid / 8
                dst.[di] <- V4d(mem.[0] / 64.0, 1.0)

        }


    let testreduce8() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Device
        let shader = device |> ComputeShader.ofFunction scan2048
        let pool = device.CreateDescriptorPool(1 <<< 20, 1 <<< 20)
       
        let cnt = 1 <<< 20
        let input = ArrayBuffer (Array.init cnt (fun _ -> 1.0f))
        let output = device.CreateBuffer(VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.TransferSrcBit, int64 cnt * 4L)
        let mappable = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit (int64 cnt * 4L)

        //(inputOffset : int) (inputDelta : int) (inputSize : int) (input : float[]) (outputOffset : int) (outputDelta : int) (res : float[])
        let args0 = ComputeShader.newInputBinding shader pool
        args0.["inputOffset"] <- 0
        args0.["inputDelta"] <- 1
        args0.["inputSize"] <- cnt
        args0.["input"] <- input
        args0.["outputOffset"] <- 0
        args0.["outputDelta"] <- 1
        args0.["output"] <- output
        args0.Flush()
        
        let temp = device.CreateBuffer(VkBufferUsageFlags.StorageBufferBit ||| VkBufferUsageFlags.TransferSrcBit, int64 (cnt / 2048) * 4L)
        let args1 = ComputeShader.newInputBinding shader pool
        args1.["inputOffset"] <- scanSize
        args1.["inputDelta"] <- scanSize
        args1.["inputSize"] <- (cnt - scanSize) / scanSize
        args1.["input"] <- output
        args1.["outputOffset"] <- 0
        args1.["outputDelta"] <- 1
        args1.["output"] <- temp
        args1.Flush()

        
        let shader2 = device |> ComputeShader.ofFunction addParentSum
        
        let argsAdd = ComputeShader.newInputBinding shader2 pool
        argsAdd.["scan"] <- temp
        argsAdd.["data"] <- output
        argsAdd.["dataLength"] <- cnt / scanSize
        argsAdd.["offset"] <- scanSize
        argsAdd.Flush()

        device.perform {
            do! Command.Bind shader

            do! Command.SetInputs args0
            do! Command.Dispatch(cnt / scanSize)

            do! Command.SetInputs args1
            do! Command.Dispatch(cnt / scanSize / scanSize)

            do! Command.Bind shader2
            do! Command.SetInputs argsAdd
            do! Command.Dispatch((cnt - scanSize) / halfScanSize)

            do! Command.Copy(output, mappable)
        }

        let result : float32[] = Array.zeroCreate cnt
        let gc = GCHandle.Alloc(result, GCHandleType.Pinned)
        mappable.Memory.Mapped (fun src -> Marshal.Copy(src, gc.AddrOfPinnedObject(), int64 cnt * 4L))
        gc.Free()

        let check = Array.init cnt (fun i -> 1 + (i % 2048) |> float32)
        if check = result then
            printfn "OK"
        else
            printfn "ERROR:"
            for i in 0 .. cnt - 1 do
                if result.[i] <> check.[i] then
                    printfn "  %d: %A vs %A" i result.[i] check.[i]

        args0.Dispose()
        args1.Dispose()
        argsAdd.Dispose()
        device.Delete output
        device.Delete mappable
        ComputeShader.delete shader
        device.Delete pool


    let shader() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Device
        let shader = device |> Aardvark.Rendering.Vulkan.ComputeShader.ofFunction invertShader
        let pool = device.CreateDescriptorPool(1 <<< 20, 1 <<< 20)

        let inputImage = PixImage.Create @"E:\Development\WorkDirectory\DataSVN\puget_color_large.png"
        let inputTexture = PixTexture2d(PixImageMipMap [| inputImage |], TextureParams.empty)
        let size = inputImage.Size

        let img = device.CreateImage(V3i(size.X, size.Y, 1), 1, 1, 1, TextureDimension.Texture2D, TextureFormat.Rgba8, VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.StorageBit)
        let result = device |> TensorImage.create<byte> img.Size Col.Format.RGBA
        let resultCPU = new PixImage<byte>(Col.Format.RGBA, img.Size.XY)

        let args = ComputeShader.newInputBinding shader pool
        args.["SourceImage"] <- inputTexture
        args.["dst"] <- img
        //args.["factor"] <- 0.2
        args.Flush()
        

       

        let family = device.GraphicsFamily
        printfn "family: %A" family.Index

        let queue = family.GetQueue()

        family.run {
            do! Command.TransformLayout(img, VkImageLayout.General)
        }

        let groups = size / shader.GroupSize.XY

        let iter = 1000
        let innerIter = 1
        let cmd =
            command {
                do! Command.Bind shader
                do! Command.SetInputs args
                for i in 1 .. innerIter do
                    do! Command.Dispatch(groups)
            }

        let cmdBuffer = family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmdBuffer.Begin(CommandBufferUsage.None)
        cmd.Enqueue(cmdBuffer) |> ignore
        cmdBuffer.End()

        // warmup
        queue.RunSynchronously(cmdBuffer)

        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            queue.RunSynchronously(cmdBuffer)
        sw.Stop()
        Log.line "took %A" (sw.MicroTime / (iter * innerIter))
            

        device.perform {
            do! Command.TransformLayout(img, VkImageLayout.General)
            do! Command.Bind shader
            do! Command.SetInputs args
            do! Command.Dispatch(size)
            do! Command.TransformLayout(img, VkImageLayout.TransferSrcOptimal)
            do! Command.Copy(img.[ImageAspect.Color, 0, 0], result)
        }

        result.Read(resultCPU)
        resultCPU.SaveAsImage @"C:\Users\Schorsch\Desktop\test.jpg"


        args.Dispose()

        let linearSize = img.Size.X * img.Size.Y
        let buffer = device.CreateBuffer(VkBufferUsageFlags.TransferSrcBit ||| VkBufferUsageFlags.StorageBufferBit, int64 linearSize * 4L)
        let shader2 = device |> ComputeShader.ofFunction copyToBufferShader
        let args = ComputeShader.newInputBinding shader2 pool
        args.["SourceImage"] <- inputTexture
        args.["dst"] <- buffer
        args.Flush()

        
        let iter = 10
        let innerIter = 10
        let cmd =
            command {
                do! Command.Bind shader2
                do! Command.SetInputs args
                for i in 1 .. innerIter do
                    do! Command.Dispatch(linearSize / shader2.GroupSize.X)
            }

        let cmdBuffer = family.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmdBuffer.Begin(CommandBufferUsage.None)
        cmd.Enqueue(cmdBuffer) |> ignore
        cmdBuffer.End()

        // warmup
        queue.RunSynchronously(cmdBuffer)

        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            queue.RunSynchronously(cmdBuffer)
        sw.Stop()
        Log.line "took %A" (sw.MicroTime / (iter * innerIter))

        
        device.Delete img
        device.Delete result
        ComputeShader.delete shader
        ComputeShader.delete shader2
        device.Delete pool
        ()


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









[<EntryPoint; STAThread>]
let main argv = 
    
    Ag.initialize()
    Aardvark.Init()

    //tensorPerformance()
    VulkanTests.testreduce8()
    Environment.Exit 0


    use app = new VulkanApplication(true)
    let win = app.CreateSimpleRenderWindow(8)
    

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

    let trafos = trafos |> Mod.constant

    let cam = CameraView.lookAt (V3d.III * 6.0) V3d.Zero V3d.OOI

    let mode = Mod.init Fly
    let controllerActive = Mod.init true

    let flyTo = Mod.init Box3d.Invalid

    let chainM (l : IMod<list<afun<'a, 'a>>>) =
        l |> Mod.map AFun.chain |> AFun.bind id

    let controller (loc : IMod<V3d>) (target : IMod<DateTime * V3d>) = 
        adaptive {
            let! active = controllerActive


            // if the controller is active determine the implementation
            // based on mode
            if active then
                
                let! mode = mode



                return [
                    

                    yield CameraControllers.fly target
                    // scroll and zoom 
                    yield CameraControllers.controlScroll win.Mouse 0.1 0.004
                    yield CameraControllers.controlZoom win.Mouse 0.05

                    
                    match mode with
                        | Fly ->
                            // fly controller special handlers
                            yield CameraControllers.controlLook win.Mouse
                            yield CameraControllers.controlWSAD win.Keyboard 5.0
                            yield CameraControllers.controlPan win.Mouse 0.05

                        | Orbit ->
                            // special orbit controller
                            yield CameraControllers.controlOrbit win.Mouse V3d.Zero

                        | Rotate ->
                            
//                            // rotate is just a regular orbit-controller
//                            // with a simple animation rotating around the Z-Axis
                            yield CameraControllers.controlOrbit win.Mouse V3d.Zero
                            yield CameraControllers.controlAnimation V3d.Zero V3d.OOI

                ]
            else
                // if the controller is inactive simply return an empty-list
                // of controller functions
                return []

        } |> chainM

    let resetPos = Mod.init (6.0 * V3d.III)
    let resetDir = Mod.init (DateTime.MaxValue, V3d.Zero)

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
        "# This is Aardvark.Rendering\r\n" +
        "I'm uploading my first screenshot to tracker ฿"
    // old school stuff here^^

    // here's an example-usage of AIR (Aardvark Imperative Renderer) 
    // showing how to integrate arbitrary logic in the SceneGraph without
    // implementing new nodes for that
    let quad = 
        Sg.air { 
            // inside an air-block we're allowed to read current values
            // which will be inherited from the SceneGraph
            let! parentFill = AirState.fillMode

            // modes can be modified by simply calling the respective setters.
            // Note that these setters are overloaded with and without IMod<Mode>
            do! Air.DepthTest    DepthTestMode.LessOrEqual
            do! Air.CullMode     CullMode.None
            do! Air.BlendMode    BlendMode.None
            do! Air.FillMode     FillMode.Fill
            do! Air.StencilMode  StencilMode.Disabled

            // we can also override the shaders in use (and with FSHade)
            // build our own dynamic shaders e.g. depending on the inherited 
            // FillMode from the SceneGraph
            do! Air.BindShader {
                    do! DefaultSurfaces.trafo               
                    
                    // if the parent fillmode is not filled make the quad red.
                    let! fill = parentFill
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


    let mode = Mod.init FillMode.Fill
    let font = Font "Comic Sans"


    let config = 
        { MarkdownConfig.light with 
            codeFont = "Kunstler Script"
            paragraphFont = "Kunstler Script" 
        }

    let label1 =
        Sg.markdown config (Mod.constant md)
            |> Sg.scale 0.1
            |> Sg.billboard


    let f = Font "Consolas"

    let message = Mod.init message
    let label2 =
        //Sg.text f C4b.Green message
        Sg.markdown MarkdownConfig.light message
            |> Sg.scale 0.1
            |> Sg.billboard
            |> Sg.translate 5.0 0.0 0.0

    let aa = Mod.init true

    
    let f = Aardvark.Rendering.Text.Font("Consolas")
    let label3 =
        Sg.text f C4b.White message
            |> Sg.scale 0.1
            |> Sg.transform (Trafo3d.FromBasis(-V3d.IOO, V3d.OOI, V3d.OIO, V3d(0.0, 0.0, 0.0)))

    let active = Mod.init true

    let sg = 
        active |> Mod.map (fun a ->
            if a then
                Sg.group [label3]
                    //|> Sg.andAlso quad
                    |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
                    |> Sg.projTrafo (win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y) |> Frustum.projTrafo))
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

    let calls = Mod.init 999
    
    let randomCalls(cnt : int) =
        [|
            for i in 0..cnt-1 do
                yield
                    DrawCallInfo( 
                        FaceVertexCount = 1,
                        InstanceCount = 1,
                        FirstIndex = i,
                        FirstInstance = 0
                    )
        |]

    let calls = Mod.init (randomCalls 990)

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

    let trafo = win.Time |> Mod.map (fun t -> Trafo3d.RotationZ (float t.Ticks / float TimeSpan.TicksPerSecond))

    let blasg = 
        BlaNode(calls, IndexedGeometryMode.PointList)
            |> Sg.vertexAttribute' DefaultSemantic.Positions pos
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.constantColor C4f.White
            }
            |> Sg.trafo trafo
            |> Sg.viewTrafo (cam |> Mod.map CameraView.viewTrafo)
            |> Sg.projTrafo (win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y) |> Frustum.projTrafo))


    let main = app.Runtime.CompileRender(win.FramebufferSignature, config, sg) //|> DefaultOverlays.withStatistics
    //let clear = app.Runtime.CompileClear(win.FramebufferSignature, Mod.constant C4f.Black)

    win.Keyboard.Press.Values.Add (fun c ->
        if c = '\b' then
            if message.Value.Length > 0 then
                transact (fun () -> message.Value <- message.Value.Substring(0, message.Value.Length - 1))
        else
            transact (fun () -> message.Value <- message.Value + string c)
    )

    win.RenderTask <- main
    win.Run()
    win.Dispose()
    0 
