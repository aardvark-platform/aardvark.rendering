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

    open Microsoft.FSharp.Quotations
    open System.Runtime.CompilerServices

    type Buffer<'a when 'a : unmanaged>(device : Device, handle : VkBuffer, mem : DevicePtr, count : int64) =
        inherit Buffer(device, handle, mem, int64 sizeof<'a> * count)

        static let sl = sizeof<'a> |> int64

        member x.Count = count

        member x.Upload(src : 'a[], srcIndex: int64, dstIndex : int64, count : int64) =
            let size = count * sl
            let srcOffset = nativeint (srcIndex * sl)
            let dstOffset = dstIndex * sl

            let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit size
            let gc = GCHandle.Alloc(src, GCHandleType.Pinned)
            try 
                temp.Memory.Mapped(fun ptr ->
                    Marshal.Copy(gc.AddrOfPinnedObject() + srcOffset, ptr, nativeint size)
                )
            finally 
                gc.Free()

            device.perform {
                try do! Command.Copy(temp, 0L, x, dstOffset, size)
                finally device.Delete temp
            }

        member x.Upload(src : 'a[], count : int64) =
            x.Upload(src, 0L, 0L, count)

        member x.Upload(src : 'a[]) =
            x.Upload(src, 0L, 0L, min src.LongLength count)

        member x.Download(srcIndex : int64, dst : 'a[], dstIndex : int64, count : int64) =
            let size = count * sl
            let dstOffset = nativeint (dstIndex * sl)
            let srcOffset = srcIndex * sl

            let temp = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit size

            device.perform {
                do! Command.Copy(x, srcOffset, temp, 0L, size)
            }

            let gc = GCHandle.Alloc(dst, GCHandleType.Pinned)
            try 
                temp.Memory.Mapped(fun ptr ->
                    Marshal.Copy(ptr, gc.AddrOfPinnedObject() + dstOffset, nativeint size)
                )
            finally 
                gc.Free()
                device.Delete temp

        member x.Download(dst : 'a[], count : int64) =
            x.Download(0L, dst, 0L, count)

        member x.Download(dst : 'a[]) =
            x.Download(0L, dst, 0L, min count dst.LongLength)
        
        interface IBufferVector<'a> with
            member x.Buffer = x
            member x.Offset = 0L
            member x.Delta = 1L
            member x.Size = count

    and IBufferVector<'a when 'a : unmanaged> =
        abstract member Buffer : Buffer<'a>
        abstract member Offset : int64
        abstract member Delta : int64
        abstract member Size : int64

    and private BufferVector<'a when 'a : unmanaged>(b : Buffer<'a>, offset : int64, delta : int64, size : int64) =
        member x.Buffer = b
        member x.Offset = offset
        member x.Delta = delta
        member x.Size = size

        interface IBufferVector<'a> with
            member x.Buffer = x.Buffer
            member x.Offset = x.Offset
            member x.Delta = x.Delta
            member x.Size = x.Size

        member x.Skip(n : int64) =
            BufferVector<'a>(b, offset + n * delta, delta, size - n)

        member x.Strided(n : int64) =
            BufferVector<'a>(b, offset, n * delta, 1L + (size - 1L) / n)
            

        new(b : Buffer<'a>) = BufferVector<'a>(b, 0L, 1L, b.Count)


    [<AbstractClass; Sealed; Extension>]
    type DeviceTypedBufferExtensions private() =
        static let usage =
            VkBufferUsageFlags.TransferSrcBit ||| 
            VkBufferUsageFlags.TransferDstBit |||
            VkBufferUsageFlags.StorageBufferBit

            
        [<Extension>]
        static member GetSlice(b : IBufferVector<'a>, l : Option<int64>, h : Option<int64>) =
            let l = match l with | Some l -> max 0L l | None -> 0L
            let h = match h with | Some h -> min (b.Size - 1L) h | None -> b.Size - 1L
            BufferVector<'a>(b.Buffer, b.Offset + l * b.Delta, b.Delta, 1L + h - l) :> IBufferVector<_>

        [<Extension>]
        static member GetSlice(b : IBufferVector<'a>, l : Option<int>, h : Option<int>) =
            let l = match l with | Some l -> max 0L (int64 l) | None -> 0L
            let h = match h with | Some h -> min (b.Size - 1L) (int64 h) | None -> b.Size - 1L
            BufferVector<'a>(b.Buffer, b.Offset + l * b.Delta, b.Delta, 1L + h - l) :> IBufferVector<_>

        [<Extension>]
        static member Strided(b : IBufferVector<'a>, delta : int64) =
            BufferVector<'a>(b.Buffer, b.Offset, delta * b.Delta, 1L + (b.Size - 1L) / delta) :> IBufferVector<_>
            
        [<Extension>]
        static member Strided(b : IBufferVector<'a>, delta : int) =
            BufferVector<'a>(b.Buffer, b.Offset, int64 delta * b.Delta, 1L + (b.Size - 1L) / int64 delta) :> IBufferVector<_>


        [<Extension>]
        static member CreateBuffer<'a when 'a : unmanaged>(device : Device, count : int64) =
            let b = device.CreateBuffer(usage, int64 sizeof<'a> * count)
            Buffer<'a>(b.Device, b.Handle, b.Memory, count)
            
        [<Extension>]
        static member CreateBuffer<'a when 'a : unmanaged>(device : Device, data : 'a[]) =
            let b = DeviceTypedBufferExtensions.CreateBuffer<'a>(device, data.LongLength)
            b.Upload(data)
            b

        [<Extension>]
        static member Coerce<'a when 'a : unmanaged>(buffer : Buffer) =
            Buffer<'a>(buffer.Device, buffer.Handle, buffer.Memory, buffer.Size / int64 sizeof<'a>)

    module ScanImpl = 
    
        [<Literal>]
        let scanSize = 128

        [<Literal>]
        let halfScanSize = 64

        [<LocalSize(X = halfScanSize)>]
        let scanKernel (add : Expr<'a -> 'a -> 'a>) (inputOffset : int) (inputDelta : int) (inputSize : int) (inputData : 'a[]) (outputOffset : int) (outputDelta : int) (outputData : 'a[]) =
            compute {
                let mem : 'a[] = allocateShared scanSize
                let gid = getGlobalId().X

                let gid0 = gid
                let lid0 =  getLocalId().X

                let lgid = 2 * gid0
                let rgid = lgid + 1
            
                let llid = 2 * lid0
                let rlid = llid + 1

                if lgid < inputSize then mem.[llid] <- inputData.[inputOffset + lgid * inputDelta]
                if rlid < inputSize then mem.[rlid] <- inputData.[inputOffset + rgid * inputDelta]


                barrier()
            
                let mutable s = 1
                let mutable d = 2
                while d <= scanSize do
                    if llid % d = 0 && llid >= s then
                        mem.[llid] <- (%add) mem.[llid - s] mem.[llid]

                    barrier()
                    s <- s <<< 1
                    d <- d <<< 1

                d <- d >>> 1
                s <- s >>> 1
                while s >= 1 do
                    if llid % d = 0 && llid + s < scanSize then
                        mem.[llid + s] <- (%add) mem.[llid] mem.[llid + s]
                    
                    barrier()
                    s <- s >>> 1
                    d <- d >>> 1

                if lgid < inputSize then
                    outputData.[outputOffset + lgid * outputDelta] <- mem.[llid]
                if rgid < inputSize then
                    outputData.[outputOffset + rgid * outputDelta] <- mem.[rlid]

            }
        
        [<LocalSize(X = halfScanSize)>]
        let mapReduceKernel (map : Expr<'a -> 'b>) (reduce : Expr<'b -> 'b -> 'b>) (inputOffset : int) (inputDelta : int) (inputSize : int) (inputData : 'a[]) (outputOffset : int) (outputDelta : int) (outputData : 'b[]) =
            compute {
                let mem : 'b[] = allocateShared scanSize
                let gid = getGlobalId().X

                let gid0 = gid
                let lid0 =  getLocalId().X

                let lgid = 2 * gid0
                let rgid = lgid + 1
            
                let llid = 2 * lid0
                let rlid = llid + 1

                if lgid < inputSize then mem.[llid] <- (%map) inputData.[inputOffset + lgid * inputDelta]
                if rlid < inputSize then mem.[rlid] <- (%map) inputData.[inputOffset + rgid * inputDelta]


                barrier()
            
                let mutable s = 1
                let mutable d = 2
                while d <= scanSize do
                    if llid % d = 0 && llid >= s then
                        mem.[llid] <- (%reduce) mem.[llid - s] mem.[llid]

                    barrier()
                    s <- s <<< 1
                    d <- d <<< 1

                d <- d >>> 1
                s <- s >>> 1
                while s >= 1 do
                    if llid % d = 0 && llid + s < scanSize then
                        mem.[llid + s] <- (%reduce) mem.[llid] mem.[llid + s]
                    
                    barrier()
                    s <- s >>> 1
                    d <- d >>> 1

                if lgid < inputSize then
                    outputData.[outputOffset + lgid * outputDelta] <- mem.[llid]
                if rgid < inputSize then
                    outputData.[outputOffset + rgid * outputDelta] <- mem.[rlid]

            }

        [<LocalSize(X = halfScanSize)>]
        let fixupKernel (add : Expr<'a -> 'a -> 'a>) (inputData : 'a[]) (inputOffset : int) (inputDelta : int) (outputData : 'a[]) (outputOffset : int) (outputDelta : int) (groupSize : int) (count : int) =
            compute {
                let id = getGlobalId().X + groupSize

                if id < count then
                    let block = id / groupSize - 1
              
                    let iid = inputOffset + block * inputDelta
                    let oid = outputOffset + id * outputDelta

                    if id % groupSize <> groupSize - 1 then
                        outputData.[oid] <- (%add) inputData.[iid] outputData.[oid]

            }
      
        type Scan<'a when 'a : unmanaged>(runtime : Runtime, add : Expr<'a -> 'a -> 'a>) =
            static let ceilDiv (v : int) (d : int) =
                if v % d = 0 then v / d
                else 1 + v / d

            let device  = runtime.Device
            let pool = runtime.DescriptorPool

            let scan    = device |> ComputeShader.ofFunction (scanKernel add)
            let fixup   = device |> ComputeShader.ofFunction (fixupKernel add)

            let release() =
                ComputeShader.delete scan
                ComputeShader.delete fixup

            do device.OnDispose.Add (fun _ -> release())

            member x.Invoke(input : IBufferVector<'a>, output : IBufferVector<'a>) =
                let rec build (input : IBufferVector<'a>) (output : IBufferVector<'a>) =
                    command {
                        let cnt = int input.Size
                        let args0 = ComputeShader.newInputBinding scan pool

                        try
                            args0.["inputOffset"] <- input.Offset |> int
                            args0.["inputDelta"] <- input.Delta |> int
                            args0.["inputSize"] <- input.Size |> int
                            args0.["inputData"] <- input.Buffer
                            args0.["outputOffset"] <- output.Offset |> int
                            args0.["outputDelta"] <- output.Delta |> int
                            args0.["outputData"] <- output.Buffer
                            args0.Flush()

                            do! Command.Bind scan
                            do! Command.SetInputs args0
                            do! Command.Dispatch(ceilDiv (int input.Size) scanSize)
                            do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)

                            let oSums = output.[int64 scanSize - 1L .. ].Strided(scanSize)
                            if oSums.Size > 1L then
                            
                                do! build oSums oSums

                                let args1 = ComputeShader.newInputBinding fixup pool
                                try
                                    args1.["inputData"] <- oSums.Buffer
                                    args1.["inputOffset"] <- oSums.Offset |> int
                                    args1.["inputDelta"] <- oSums.Delta |> int
                                    args1.["outputData"] <- output.Buffer
                                    args1.["outputOffset"] <- output.Offset |> int
                                    args1.["outputDelta"] <- output.Delta |> int
                                    args1.["count"] <- output.Size |> int
                                    args1.["groupSize"] <- scanSize
                                    args1.Flush()

                                    do! Command.Bind fixup
                                    do! Command.SetInputs args1
                                    do! Command.Dispatch(ceilDiv (int output.Size - scanSize) halfScanSize)
                                    do! Command.Sync(output.Buffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.ShaderReadBit)
                                finally
                                    args1.Dispose()


                        finally
                            args0.Dispose()
                    }

                if input.Size > 1L then
                    build input output
                else
                    Command.Nop

            member x.Compile(input : Buffer<'a>, output : Buffer<'a>) =
                let cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
                cmd.Begin(CommandBufferUsage.None)
                cmd.Enqueue(x.Invoke(input, output))
                cmd.End()
                cmd

            member x.Dispose() = release()
            interface IDisposable with
                member x.Dispose() = x.Dispose()



    [<AbstractClass; Sealed; Extension>]
    type DeviceScanExtensions private() =
        [<Extension>]
        static member CompileScan<'a when 'a : unmanaged> (this : Runtime, add : Expr<'a -> 'a -> 'a>) =
            new ScanImpl.Scan<'a>(this, add)


    [<StructuredFormatDisplay("{AsString}")>]
    type Codeword =
        struct
            val mutable public Store : uint16
            val mutable public LengthStore : uint8


            member inline x.Length = int x.LengthStore


            member private x.AsString = x.ToString()
            override x.ToString() =
                if x.LengthStore = 0uy then
                    "(0)"
                else
                    let mutable res = sprintf "(%d:" x.LengthStore
                    let mutable mask = 1us <<< 15
                    for i in 1 .. int x.Length do
                        if x.Store &&& mask <> 0us then res <- res + "1"
                        else res <- res + "0"
                        mask <- mask >>> 1
                    res + ")"

            member x.FullString =
                let mutable res = sprintf "(%d:" x.LengthStore
                let mutable mask = 1us <<< 15
                for i in 1 .. 16 do
                    if x.Store &&& mask <> 0us then res <- res + "1"
                    else res <- res + "0"
                    mask <- mask >>> 1
                res + ")"
                    
            member x.ToByteArray() =
                if x.Length = 0 then [||]
                elif x.Length <= 8 then [| byte (x.Store >>> 8) |]
                else [| byte (x.Store >>> 8); byte x.Store |]

            member x.Take(count : int) =
                if count > x.Length || count < 0 then failwith "[Codeword] cannot skip"

                let mask = ((1us <<< count) - 1us) <<< (16 - count)

                Codeword(count, x.Store &&& mask)

            member x.Skip(count : int) =
                if count > x.Length || count < 0 then failwith "[Codeword] cannot take"

                let rem = x.Length - count
                //let mask = (1us <<< rem) - 1us
                Codeword(rem, x.Store <<< count)

            member x.Append(other : Codeword) =
                let len = x.Length + other.Length
                if len > 16 then failwith "[Codeword] cannot append"

                let other = other.Store >>> x.Length
                Codeword(len, x.Store ||| other)


            member x.AppendBit (b : bool) =
                if x.Length >= 16 then failwith "[Codeword] cannot append to full codeword"

                if b then 
                    let n = 1us <<< (15 - x.Length)
                    Codeword(x.Length + 1, x.Store ||| n)
                else 
                    Codeword(x.Length + 1, x.Store)

            static member Create (length : int, code : uint16) =
                // mask the higher bits
                let code1 = 
                    if length < 16 then code &&& ((1us <<< length) - 1us)
                    else code

                // shift the code to the beginning
                let code2 = code1 <<< (16 - length)

                Codeword(length, code2)
            static member Empty = Codeword(0, 0us)

            private new(length : int, code : uint16) =  
                assert(length <= 16)
                { Store = code; LengthStore = byte length}

        end

    module JpegClean =
        
        
        type HuffmanTree =
            | HuffmanNode of left : HuffmanTree * right : HuffmanTree
            | HuffmanLeaf of value : byte
            | HuffmanEmpty

        type HuffmanTable = 
            { 
                counts      : int[]
                values      : byte[]
                forward     : HuffmanTree
                backward    : Codeword[]
            }
            
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module HuffmanTable =
            [<AutoOpen>]
            module private Helpers =
                let rec zipper (l : list<HuffmanTree>) =
                    match l with
                        | [] -> []
                        | a :: b :: rest ->
                            HuffmanNode(a,b) :: zipper rest
                        | [a] ->
                            [HuffmanNode(a,HuffmanEmpty)]

                let build (counts : int[]) (values : byte[]) : HuffmanTree =
                    let mutable currentValue = 0
                    let rec build (level : int) (n : int) : list<HuffmanTree> =
                        if n <= 0 then
                            []
                        else
                            let cnt = counts.[level]

                            let leafs = 
                                List.init cnt (fun _ -> 
                                    let i = currentValue
                                    currentValue <- i + 1
                                    HuffmanLeaf values.[i]
                                )

                            let nodes =
                                if level >= counts.Length - 1 then
                                    []
                                else
                                    let nodeCount = n - cnt
                                    build (level + 1) (2 * nodeCount) |> zipper

                            let res = leafs @ nodes
            
                            res

                    match build 0 1 with
                        | [n] -> n
                        | _ -> failwith "magic"

                let inverse (tree : HuffmanTree) =
                    let max = 256

                    let arr = Array.zeroCreate max

                    let rec traverse (path : Codeword) (t : HuffmanTree) =
                        match t with
                            | HuffmanEmpty -> ()
                            | HuffmanLeaf v -> arr.[int v] <- path
                            | HuffmanNode(l,r) ->
                                traverse (path.AppendBit false) l
                                traverse (path.AppendBit true) r

                    traverse Codeword.Empty tree
                    arr  

            let create (counts : int[]) (values : byte[]) =
                let tree = build counts values
                { 
                    counts      = counts
                    values      = values
                    forward     = tree
                    backward    = inverse tree
                }

            module Photoshop =
                
                let luminanceDC =
                    create
                        [| 0; 0; 0; 7; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0; 0 |]
                        [| 0x04uy; 0x05uy; 0x03uy; 0x02uy; 0x06uy; 0x01uy; 0x00uy; 0x07uy; 0x08uy; 0x09uy; 0x0Auy; 0x0Buy |]
                        
                let chromaDC =
                    create
                        [| 0; 0; 2; 2; 3; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0 |]
                        [| 0x01uy; 0x00uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy; 0x09uy; 0x0Auy; 0x0Buy |]

                let luminanceAC =
                    create 
                        [|  0; 0; 2; 1; 3; 3; 2; 4; 2; 6; 7; 3; 4; 2; 6; 2; 115 |]
                        [|
                            0x01uy; 0x02uy; 0x03uy; 0x11uy; 0x04uy; 0x00uy; 0x05uy; 0x21uy
                            0x12uy; 0x31uy; 0x41uy; 0x51uy; 0x06uy; 0x13uy; 0x61uy; 0x22uy
                            0x71uy; 0x81uy; 0x14uy; 0x32uy; 0x91uy; 0xA1uy; 0x07uy; 0x15uy
                            0xB1uy; 0x42uy; 0x23uy; 0xC1uy; 0x52uy; 0xD1uy; 0xE1uy; 0x33uy
                            0x16uy; 0x62uy; 0xF0uy; 0x24uy; 0x72uy; 0x82uy; 0xF1uy; 0x25uy
                            0x43uy; 0x34uy; 0x53uy; 0x92uy; 0xA2uy; 0xB2uy; 0x63uy; 0x73uy
                            0xC2uy; 0x35uy; 0x44uy; 0x27uy; 0x93uy; 0xA3uy; 0xB3uy; 0x36uy
                            0x17uy; 0x54uy; 0x64uy; 0x74uy; 0xC3uy; 0xD2uy; 0xE2uy; 0x08uy
                            0x26uy; 0x83uy; 0x09uy; 0x0Auy; 0x18uy; 0x19uy; 0x84uy; 0x94uy
                            0x45uy; 0x46uy; 0xA4uy; 0xB4uy; 0x56uy; 0xD3uy; 0x55uy; 0x28uy
                            0x1Auy; 0xF2uy; 0xE3uy; 0xF3uy; 0xC4uy; 0xD4uy; 0xE4uy; 0xF4uy
                            0x65uy; 0x75uy; 0x85uy; 0x95uy; 0xA5uy; 0xB5uy; 0xC5uy; 0xD5uy
                            0xE5uy; 0xF5uy; 0x66uy; 0x76uy; 0x86uy; 0x96uy; 0xA6uy; 0xB6uy
                            0xC6uy; 0xD6uy; 0xE6uy; 0xF6uy; 0x37uy; 0x47uy; 0x57uy; 0x67uy
                            0x77uy; 0x87uy; 0x97uy; 0xA7uy; 0xB7uy; 0xC7uy; 0xD7uy; 0xE7uy
                            0xF7uy; 0x38uy; 0x48uy; 0x58uy; 0x68uy; 0x78uy; 0x88uy; 0x98uy
                            0xA8uy; 0xB8uy; 0xC8uy; 0xD8uy; 0xE8uy; 0xF8uy; 0x29uy; 0x39uy
                            0x49uy; 0x59uy; 0x69uy; 0x79uy; 0x89uy; 0x99uy; 0xA9uy; 0xB9uy
                            0xC9uy; 0xD9uy; 0xE9uy; 0xF9uy; 0x2Auy; 0x3Auy; 0x4Auy; 0x5Auy
                            0x6Auy; 0x7Auy; 0x8Auy; 0x9Auy; 0xAAuy; 0xBAuy; 0xCAuy; 0xDAuy
                            0xEAuy; 0xFAuy
                        |]

                let chromaAC =
                    create
                        [| 0; 0; 2; 2; 1; 2; 3; 5; 5; 4; 5; 6; 4; 8; 3; 3; 109 |]
                        [|
                            0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x04uy; 0x21uy; 0x12uy
                            0x31uy; 0x41uy; 0x05uy; 0x51uy; 0x13uy; 0x61uy; 0x22uy; 0x06uy
                            0x71uy; 0x81uy; 0x91uy; 0x32uy; 0xA1uy; 0xB1uy; 0xF0uy; 0x14uy
                            0xC1uy; 0xD1uy; 0xE1uy; 0x23uy; 0x42uy; 0x15uy; 0x52uy; 0x62uy
                            0x72uy; 0xF1uy; 0x33uy; 0x24uy; 0x34uy; 0x43uy; 0x82uy; 0x16uy
                            0x92uy; 0x53uy; 0x25uy; 0xA2uy; 0x63uy; 0xB2uy; 0xC2uy; 0x07uy
                            0x73uy; 0xD2uy; 0x35uy; 0xE2uy; 0x44uy; 0x83uy; 0x17uy; 0x54uy
                            0x93uy; 0x08uy; 0x09uy; 0x0Auy; 0x18uy; 0x19uy; 0x26uy; 0x36uy
                            0x45uy; 0x1Auy; 0x27uy; 0x64uy; 0x74uy; 0x55uy; 0x37uy; 0xF2uy
                            0xA3uy; 0xB3uy; 0xC3uy; 0x28uy; 0x29uy; 0xD3uy; 0xE3uy; 0xF3uy
                            0x84uy; 0x94uy; 0xA4uy; 0xB4uy; 0xC4uy; 0xD4uy; 0xE4uy; 0xF4uy
                            0x65uy; 0x75uy; 0x85uy; 0x95uy; 0xA5uy; 0xB5uy; 0xC5uy; 0xD5uy
                            0xE5uy; 0xF5uy; 0x46uy; 0x56uy; 0x66uy; 0x76uy; 0x86uy; 0x96uy
                            0xA6uy; 0xB6uy; 0xC6uy; 0xD6uy; 0xE6uy; 0xF6uy; 0x47uy; 0x57uy
                            0x67uy; 0x77uy; 0x87uy; 0x97uy; 0xA7uy; 0xB7uy; 0xC7uy; 0xD7uy
                            0xE7uy; 0xF7uy; 0x38uy; 0x48uy; 0x58uy; 0x68uy; 0x78uy; 0x88uy
                            0x98uy; 0xA8uy; 0xB8uy; 0xC8uy; 0xD8uy; 0xE8uy; 0xF8uy; 0x39uy
                            0x49uy; 0x59uy; 0x69uy; 0x79uy; 0x89uy; 0x99uy; 0xA9uy; 0xB9uy
                            0xC9uy; 0xD9uy; 0xE9uy; 0xF9uy; 0x2Auy; 0x3Auy; 0x4Auy; 0x5Auy
                            0x6Auy; 0x7Auy; 0x8Auy; 0x9Auy; 0xAAuy; 0xBAuy; 0xCAuy; 0xDAuy
                            0xEAuy; 0xFAuy
                            |]

            module TurboJpeg = 
                let luminanceDC =
                    create 
                        [| 0; 0; 1; 5; 1; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0 |]
                        [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy; 10uy; 11uy |]

                let chromaDC =
                    create
                        [| 0; 0; 3; 1; 1; 1; 1; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0 |]
                        [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy; 10uy; 11uy |]

                let luminanceAC = 
                    create
                        [|  0; 0; 2; 1; 3; 3; 2; 4; 3; 5; 5; 4; 4; 0; 0; 1; 0x7d |]
                        [|
                            0x01uy; 0x02uy; 0x03uy; 0x00uy; 0x04uy; 0x11uy; 0x05uy; 0x12uy;
                            0x21uy; 0x31uy; 0x41uy; 0x06uy; 0x13uy; 0x51uy; 0x61uy; 0x07uy;
                            0x22uy; 0x71uy; 0x14uy; 0x32uy; 0x81uy; 0x91uy; 0xa1uy; 0x08uy;
                            0x23uy; 0x42uy; 0xb1uy; 0xc1uy; 0x15uy; 0x52uy; 0xd1uy; 0xf0uy;
                            0x24uy; 0x33uy; 0x62uy; 0x72uy; 0x82uy; 0x09uy; 0x0auy; 0x16uy;
                            0x17uy; 0x18uy; 0x19uy; 0x1auy; 0x25uy; 0x26uy; 0x27uy; 0x28uy;
                            0x29uy; 0x2auy; 0x34uy; 0x35uy; 0x36uy; 0x37uy; 0x38uy; 0x39uy;
                            0x3auy; 0x43uy; 0x44uy; 0x45uy; 0x46uy; 0x47uy; 0x48uy; 0x49uy;
                            0x4auy; 0x53uy; 0x54uy; 0x55uy; 0x56uy; 0x57uy; 0x58uy; 0x59uy;
                            0x5auy; 0x63uy; 0x64uy; 0x65uy; 0x66uy; 0x67uy; 0x68uy; 0x69uy;
                            0x6auy; 0x73uy; 0x74uy; 0x75uy; 0x76uy; 0x77uy; 0x78uy; 0x79uy;
                            0x7auy; 0x83uy; 0x84uy; 0x85uy; 0x86uy; 0x87uy; 0x88uy; 0x89uy;
                            0x8auy; 0x92uy; 0x93uy; 0x94uy; 0x95uy; 0x96uy; 0x97uy; 0x98uy;
                            0x99uy; 0x9auy; 0xa2uy; 0xa3uy; 0xa4uy; 0xa5uy; 0xa6uy; 0xa7uy;
                            0xa8uy; 0xa9uy; 0xaauy; 0xb2uy; 0xb3uy; 0xb4uy; 0xb5uy; 0xb6uy;
                            0xb7uy; 0xb8uy; 0xb9uy; 0xbauy; 0xc2uy; 0xc3uy; 0xc4uy; 0xc5uy;
                            0xc6uy; 0xc7uy; 0xc8uy; 0xc9uy; 0xcauy; 0xd2uy; 0xd3uy; 0xd4uy;
                            0xd5uy; 0xd6uy; 0xd7uy; 0xd8uy; 0xd9uy; 0xdauy; 0xe1uy; 0xe2uy;
                            0xe3uy; 0xe4uy; 0xe5uy; 0xe6uy; 0xe7uy; 0xe8uy; 0xe9uy; 0xeauy;
                            0xf1uy; 0xf2uy; 0xf3uy; 0xf4uy; 0xf5uy; 0xf6uy; 0xf7uy; 0xf8uy;
                            0xf9uy; 0xfauy
                        |]

                let chromaAC =
                    create
                        [| 0; 0; 2; 1; 2; 4; 4; 3; 4; 7; 5; 4; 4; 0; 1; 2; 0x77 |]
                        [|
                            0x00uy; 0x01uy; 0x02uy; 0x03uy; 0x11uy; 0x04uy; 0x05uy; 0x21uy;
                            0x31uy; 0x06uy; 0x12uy; 0x41uy; 0x51uy; 0x07uy; 0x61uy; 0x71uy;
                            0x13uy; 0x22uy; 0x32uy; 0x81uy; 0x08uy; 0x14uy; 0x42uy; 0x91uy;
                            0xa1uy; 0xb1uy; 0xc1uy; 0x09uy; 0x23uy; 0x33uy; 0x52uy; 0xf0uy;
                            0x15uy; 0x62uy; 0x72uy; 0xd1uy; 0x0auy; 0x16uy; 0x24uy; 0x34uy;
                            0xe1uy; 0x25uy; 0xf1uy; 0x17uy; 0x18uy; 0x19uy; 0x1auy; 0x26uy;
                            0x27uy; 0x28uy; 0x29uy; 0x2auy; 0x35uy; 0x36uy; 0x37uy; 0x38uy;
                            0x39uy; 0x3auy; 0x43uy; 0x44uy; 0x45uy; 0x46uy; 0x47uy; 0x48uy;
                            0x49uy; 0x4auy; 0x53uy; 0x54uy; 0x55uy; 0x56uy; 0x57uy; 0x58uy;
                            0x59uy; 0x5auy; 0x63uy; 0x64uy; 0x65uy; 0x66uy; 0x67uy; 0x68uy;
                            0x69uy; 0x6auy; 0x73uy; 0x74uy; 0x75uy; 0x76uy; 0x77uy; 0x78uy;
                            0x79uy; 0x7auy; 0x82uy; 0x83uy; 0x84uy; 0x85uy; 0x86uy; 0x87uy;
                            0x88uy; 0x89uy; 0x8auy; 0x92uy; 0x93uy; 0x94uy; 0x95uy; 0x96uy;
                            0x97uy; 0x98uy; 0x99uy; 0x9auy; 0xa2uy; 0xa3uy; 0xa4uy; 0xa5uy;
                            0xa6uy; 0xa7uy; 0xa8uy; 0xa9uy; 0xaauy; 0xb2uy; 0xb3uy; 0xb4uy;
                            0xb5uy; 0xb6uy; 0xb7uy; 0xb8uy; 0xb9uy; 0xbauy; 0xc2uy; 0xc3uy;
                            0xc4uy; 0xc5uy; 0xc6uy; 0xc7uy; 0xc8uy; 0xc9uy; 0xcauy; 0xd2uy;
                            0xd3uy; 0xd4uy; 0xd5uy; 0xd6uy; 0xd7uy; 0xd8uy; 0xd9uy; 0xdauy;
                            0xe2uy; 0xe3uy; 0xe4uy; 0xe5uy; 0xe6uy; 0xe7uy; 0xe8uy; 0xe9uy;
                            0xeauy; 0xf2uy; 0xf3uy; 0xf4uy; 0xf5uy; 0xf6uy; 0xf7uy; 0xf8uy;
                            0xf9uy; 0xfauy
                            |]
         

        type Quality =
            {
                qLumninance : byte[]
                qChroma     : byte[]
            }

        type Coder =
            {
                luminanceDC : HuffmanTable
                luminanceAC : HuffmanTable
                chromaDC    : HuffmanTable
                chromaAC    : HuffmanTable
            }

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Coder =

            let photoshop =
                {
                    luminanceDC = HuffmanTable.Photoshop.luminanceDC 
                    luminanceAC = HuffmanTable.Photoshop.luminanceAC 
                    chromaDC    = HuffmanTable.Photoshop.chromaDC    
                    chromaAC    = HuffmanTable.Photoshop.chromaAC    
                }

            let turboJpeg =
                {
                    luminanceDC = HuffmanTable.Photoshop.luminanceDC 
                    luminanceAC = HuffmanTable.Photoshop.luminanceAC 
                    chromaDC    = HuffmanTable.Photoshop.chromaDC    
                    chromaAC    = HuffmanTable.Photoshop.chromaAC    
                }

        module Jpeg =
            let encode (coder : Coder) (quality : Quality) (data : PixImage<'a>) =
                let mat = data.GetMatrix<C4f>()
                ()





    module Jpeg =
        open Aardvark.Base

        let zigZagOrder =
            [|
                0;  1;  8;  16;  9;  2;  3; 10
                17; 24; 32; 25; 18; 11;  4;  5
                12; 19; 26; 33; 40; 48; 41; 34 
                27; 20; 13;  6;  7; 14; 21; 28
                35; 42; 49; 56; 57; 50; 43; 36
                29; 22; 15; 23; 30; 37; 44; 51
                58; 59; 52; 45; 38; 31; 39; 46
                53; 60; 61; 54; 47; 55; 62; 63
            |]

        let inverseZigZagOrder =
            [|
                0; 1; 5; 6; 14; 15; 27; 28
                2; 4; 7; 13; 16; 26; 29; 42
                3; 8; 12; 17; 25; 30; 41; 43
                9; 11; 18; 24; 31; 40; 44; 53
                10; 19; 23; 32; 39; 45; 52; 54
                20; 22; 33; 38; 46; 51; 55; 60
                21; 34; 37; 47; 50; 56; 59; 61
                35; 36; 48; 49; 57; 58; 62; 63
            |]

 
        let izigZag (block : 'a[]) =
            inverseZigZagOrder |> Array.map (fun i -> block.[i])

        let zigZag (block : 'a[]) =
            zigZagOrder |> Array.map (fun i -> 
                let x = i % 8
                let y = i / 8
                block.[x + 8* y]
            )

        let ycbcr =
            let mat = 
                M34d(
                     0.299,       0.587,      0.114,     -128.0,
                    -0.168736,   -0.331264,   0.5,        0.0,
                     0.5,        -0.418688,  -0.081312,   0.0
                )

            fun (c : V3d[]) ->
                c |> Array.map (fun c -> mat.TransformPos(c * V3d(255.0, 255.0, 255.0)))

        let dct (block : V3d[]) =
            Array.init 64 (fun i ->
                let x = i % 8
                let y = i / 8
                let cx = if x = 0 then sqrt 0.5 else 1.0
                let cy = if y = 0 then sqrt 0.5 else 1.0

                let mutable sum = V3d.Zero
                for m in 0 .. 7 do
                    for n in 0 .. 7 do
                        let a = cos ((2.0 * float m + 1.0) * float x * Constant.Pi / 16.0)
                        let b = cos ((2.0 * float n + 1.0) * float y * Constant.Pi / 16.0)
                        sum <- sum + block.[m + 8*n] * a * b
                
                0.25 * cx * cy * sum
            )
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

        let imageSize = V2i(1920,1080)
        let testBlocks =
            let blockCount = imageSize / 8
            List.init (blockCount.X * blockCount.Y) (fun bi ->
                let bx = bi % blockCount.X
                let by = bi / blockCount.X

                Array.init 64 (fun i ->
                    let x = 8*bx + i%8
                    let y = 8*by + i/8

                    let x = x / 18
                    let y = y / 18

                    let r = (x + y) % 2


                    if r > 0 then
                        let h = hsv2rgb ((float x + float y) / 27.0) 1.0 1.0
                        h
                    else
                        V3d(0.4, 0.4, 0.4)
                )

            )


        let printMat8 (arr : 'a[]) =
            arr |> Array.map (sprintf "%A") |> Array.chunkBySize 8 |> Array.map (String.concat " ") |> String.concat "\r\n"
        
        let QBase =
            [|
                16;   11;   10;   16;   24;   40;   51;   61;
                12;   12;   14;   19;   26;   58;   60;   55;
                14;   13;   16;   24;   40;   57;   69;   56;
                14;   17;   22;   29;   51;   87;   80;   62;
                18;   22;   37;   56;   68;  109;  103;   77;
                24;   35;   55;   64;   81;  104;  113;   92;
                49;   64;   78;   87;  103;  121;  120;  101;
                72;   92;   95;   98;  112;  100;  103;   99;
            |]

        let Q (q : float) =
            let q = q |> max 1.0 |> min 100.0
            let s = if q < 50.0 then 5000.0 / q else 200.0 - 2.0 * q
            QBase |> Array.map (fun v ->
                floor ((s * float v + 50.0) / 100.0) |> int  |> max 1
            )
               
        let quality = 70.0
        let QLum = //Q quality
            [|
                2;  2;  2;  2;  3;  4;  5;  6;
                2;  2;  2;  2;  3;  4;  5;  6;
                2;  2;  2;  2;  4;  5;  7;  9;
                2;  2;  2;  4;  5;  7;  9; 12;
                3;  3;  4;  5;  8; 10; 12; 12;
                4;  4;  5;  7; 10; 12; 12; 12;
                5;  5;  7;  9; 12; 12; 12; 12;
                6;  6;  9; 12; 12; 12; 12; 12;
            |]

        let QChrom = //Q quality
            [|
                 3;  3;  5;  9; 13; 15; 15; 15;
                 3;  4;  6; 11; 14; 12; 12; 12;
                 5;  6;  9; 14; 12; 12; 12; 12;
                 9; 11; 14; 12; 12; 12; 12; 12;
                13; 14; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
            |]
            

        let quantify (block : V3d[]) =
            let round (v : V3d) = V3i(int (round v.X), int (round v.Y), int (round v.Z))
            Array.map3 (fun ql qc v -> round(v / V3d(float ql,float qc,float qc))) QLum QChrom block

        type TableSpec =
            {
                counts : int[]
                values : byte[]
            }

        type HuffmanSpec =
            {
                specDCLum : TableSpec
                specACLum : TableSpec
                specDCChroma : TableSpec
                specACChroma : TableSpec
            }
            
        type HuffTree =
            | Empty
            | Leaf of byte
            | Node of HuffTree * HuffTree

        type HuffmanCoder =
            {
                spec : HuffmanSpec
                dcLum : HuffTree
                acLum : HuffTree
                dcChroma : HuffTree
                acChroma : HuffTree

                dcLumInv : Codeword[]
                acLumInv : Codeword[]
                dcChromaInv : Codeword[]
                acChromaInv : Codeword[]
            }

        module Huffman =
            [<AutoOpen>]
            module private Helpers = 
                let rec zipper (l : list<HuffTree>) =
                    match l with
                        | [] -> []
                        | a :: b :: rest ->
                            Node(a,b) :: zipper rest
                        | [a] ->
                            [Node(a,Empty)]

                let build (spec : TableSpec) : HuffTree =
                    let mutable currentValue = 0
                    let rec build (level : int) (n : int) : list<HuffTree> =
                        if n <= 0 then
                            []
                        else
                            let cnt = spec.counts.[level]

                            let leafs = 
                                List.init cnt (fun _ -> 
                                    let i = currentValue
                                    currentValue <- i + 1
                                    Leaf spec.values.[i]
                                )

                            let nodes =
                                if level >= spec.counts.Length - 1 then
                                    []
                                else
                                    let nodeCount = n - cnt
                                    build (level + 1) (2 * nodeCount) |> zipper

                            let res = leafs @ nodes
            
                            res

                    match build 0 1 with
                        | [n] -> n
                        | _ -> failwith "magic"

                let inverse (spec : TableSpec) (tree : HuffTree) =
                    let max = 1 + (spec.values |> Array.max |> int)

                    let arr = Array.zeroCreate max

                    let rec traverse (path : Codeword) (t : HuffTree) =
                        match t with
                            | Empty -> ()
                            | Leaf v -> arr.[int v] <- path
                            | Node(l,r) ->
                                traverse (path.AppendBit false) l
                                traverse (path.AppendBit true) r

                    traverse Codeword.Empty tree
                    arr  


            let build (spec : HuffmanSpec) =
                let dcLum = build spec.specDCLum
                let dcLumInv = inverse spec.specDCLum dcLum

                let dcChroma = build spec.specDCChroma
                let dcChromaInv = inverse spec.specDCChroma dcChroma

                let acLum = build spec.specACLum
                let acLumInv = inverse spec.specACLum acLum

                let acChroma = build spec.specACChroma
                let acChromaInv = inverse spec.specACChroma acChroma
                
                {
                    spec = spec
                    dcLum = dcLum
                    acLum = acLum
                    dcChroma = dcChroma
                    acChroma = acChroma

                    dcLumInv = dcLumInv
                    acLumInv = acLumInv
                    dcChromaInv = dcChromaInv
                    acChromaInv = acChromaInv
                }

            let photoshop =
                build {
                    specDCLum = 
                        {
                            counts = [| 0; 0; 0; 7; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0; 0 |]
                            values = [| 0x04uy; 0x05uy; 0x03uy; 0x02uy; 0x06uy; 0x01uy; 0x00uy; 0x07uy; 0x08uy; 0x09uy; 0x0Auy; 0x0Buy |]
                        }

                    specDCChroma = 
                        {
                            counts = [| 0; 0; 2; 2; 3; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0 |]
                            values = [| 0x01uy; 0x00uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy; 0x09uy; 0x0Auy; 0x0Buy |]
                        }

                    specACLum = 
                        { 
                            counts = [|  0; 0; 2; 1; 3; 3; 2; 4; 2; 6; 7; 3; 4; 2; 6; 2; 115 |]
                            values = 
                            [|
                                0x01uy; 0x02uy; 0x03uy; 0x11uy; 0x04uy; 0x00uy; 0x05uy; 0x21uy
                                0x12uy; 0x31uy; 0x41uy; 0x51uy; 0x06uy; 0x13uy; 0x61uy; 0x22uy
                                0x71uy; 0x81uy; 0x14uy; 0x32uy; 0x91uy; 0xA1uy; 0x07uy; 0x15uy
                                0xB1uy; 0x42uy; 0x23uy; 0xC1uy; 0x52uy; 0xD1uy; 0xE1uy; 0x33uy
                                0x16uy; 0x62uy; 0xF0uy; 0x24uy; 0x72uy; 0x82uy; 0xF1uy; 0x25uy
                                0x43uy; 0x34uy; 0x53uy; 0x92uy; 0xA2uy; 0xB2uy; 0x63uy; 0x73uy
                                0xC2uy; 0x35uy; 0x44uy; 0x27uy; 0x93uy; 0xA3uy; 0xB3uy; 0x36uy
                                0x17uy; 0x54uy; 0x64uy; 0x74uy; 0xC3uy; 0xD2uy; 0xE2uy; 0x08uy
                                0x26uy; 0x83uy; 0x09uy; 0x0Auy; 0x18uy; 0x19uy; 0x84uy; 0x94uy
                                0x45uy; 0x46uy; 0xA4uy; 0xB4uy; 0x56uy; 0xD3uy; 0x55uy; 0x28uy
                                0x1Auy; 0xF2uy; 0xE3uy; 0xF3uy; 0xC4uy; 0xD4uy; 0xE4uy; 0xF4uy
                                0x65uy; 0x75uy; 0x85uy; 0x95uy; 0xA5uy; 0xB5uy; 0xC5uy; 0xD5uy
                                0xE5uy; 0xF5uy; 0x66uy; 0x76uy; 0x86uy; 0x96uy; 0xA6uy; 0xB6uy
                                0xC6uy; 0xD6uy; 0xE6uy; 0xF6uy; 0x37uy; 0x47uy; 0x57uy; 0x67uy
                                0x77uy; 0x87uy; 0x97uy; 0xA7uy; 0xB7uy; 0xC7uy; 0xD7uy; 0xE7uy
                                0xF7uy; 0x38uy; 0x48uy; 0x58uy; 0x68uy; 0x78uy; 0x88uy; 0x98uy
                                0xA8uy; 0xB8uy; 0xC8uy; 0xD8uy; 0xE8uy; 0xF8uy; 0x29uy; 0x39uy
                                0x49uy; 0x59uy; 0x69uy; 0x79uy; 0x89uy; 0x99uy; 0xA9uy; 0xB9uy
                                0xC9uy; 0xD9uy; 0xE9uy; 0xF9uy; 0x2Auy; 0x3Auy; 0x4Auy; 0x5Auy
                                0x6Auy; 0x7Auy; 0x8Auy; 0x9Auy; 0xAAuy; 0xBAuy; 0xCAuy; 0xDAuy
                                0xEAuy; 0xFAuy
                            |]
                        }

                    specACChroma =
                        {
                             counts = [| 0; 0; 2; 2; 1; 2; 3; 5; 5; 4; 5; 6; 4; 8; 3; 3; 109 |]
                             values = 
                             [|
                                0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x04uy; 0x21uy; 0x12uy
                                0x31uy; 0x41uy; 0x05uy; 0x51uy; 0x13uy; 0x61uy; 0x22uy; 0x06uy
                                0x71uy; 0x81uy; 0x91uy; 0x32uy; 0xA1uy; 0xB1uy; 0xF0uy; 0x14uy
                                0xC1uy; 0xD1uy; 0xE1uy; 0x23uy; 0x42uy; 0x15uy; 0x52uy; 0x62uy
                                0x72uy; 0xF1uy; 0x33uy; 0x24uy; 0x34uy; 0x43uy; 0x82uy; 0x16uy
                                0x92uy; 0x53uy; 0x25uy; 0xA2uy; 0x63uy; 0xB2uy; 0xC2uy; 0x07uy
                                0x73uy; 0xD2uy; 0x35uy; 0xE2uy; 0x44uy; 0x83uy; 0x17uy; 0x54uy
                                0x93uy; 0x08uy; 0x09uy; 0x0Auy; 0x18uy; 0x19uy; 0x26uy; 0x36uy
                                0x45uy; 0x1Auy; 0x27uy; 0x64uy; 0x74uy; 0x55uy; 0x37uy; 0xF2uy
                                0xA3uy; 0xB3uy; 0xC3uy; 0x28uy; 0x29uy; 0xD3uy; 0xE3uy; 0xF3uy
                                0x84uy; 0x94uy; 0xA4uy; 0xB4uy; 0xC4uy; 0xD4uy; 0xE4uy; 0xF4uy
                                0x65uy; 0x75uy; 0x85uy; 0x95uy; 0xA5uy; 0xB5uy; 0xC5uy; 0xD5uy
                                0xE5uy; 0xF5uy; 0x46uy; 0x56uy; 0x66uy; 0x76uy; 0x86uy; 0x96uy
                                0xA6uy; 0xB6uy; 0xC6uy; 0xD6uy; 0xE6uy; 0xF6uy; 0x47uy; 0x57uy
                                0x67uy; 0x77uy; 0x87uy; 0x97uy; 0xA7uy; 0xB7uy; 0xC7uy; 0xD7uy
                                0xE7uy; 0xF7uy; 0x38uy; 0x48uy; 0x58uy; 0x68uy; 0x78uy; 0x88uy
                                0x98uy; 0xA8uy; 0xB8uy; 0xC8uy; 0xD8uy; 0xE8uy; 0xF8uy; 0x39uy
                                0x49uy; 0x59uy; 0x69uy; 0x79uy; 0x89uy; 0x99uy; 0xA9uy; 0xB9uy
                                0xC9uy; 0xD9uy; 0xE9uy; 0xF9uy; 0x2Auy; 0x3Auy; 0x4Auy; 0x5Auy
                                0x6Auy; 0x7Auy; 0x8Auy; 0x9Auy; 0xAAuy; 0xBAuy; 0xCAuy; 0xDAuy
                                0xEAuy; 0xFAuy
                             |]
                        }               
                }

            let turboJpeg = 
                build {
                    specDCLum = 
                        {
                            counts = [| 0; 0; 1; 5; 1; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0 |]
                            values = [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy; 10uy; 11uy |]
                        }

                    specDCChroma = 
                        {
                            counts = [| 0; 0; 3; 1; 1; 1; 1; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0 |]
                            values = [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy; 10uy; 11uy |]
                        }

                    specACLum = 
                        { 
                            counts = [|  0; 0; 2; 1; 3; 3; 2; 4; 3; 5; 5; 4; 4; 0; 0; 1; 0x7d |]
                            values = 
                            [|
                                0x01uy; 0x02uy; 0x03uy; 0x00uy; 0x04uy; 0x11uy; 0x05uy; 0x12uy;
                                0x21uy; 0x31uy; 0x41uy; 0x06uy; 0x13uy; 0x51uy; 0x61uy; 0x07uy;
                                0x22uy; 0x71uy; 0x14uy; 0x32uy; 0x81uy; 0x91uy; 0xa1uy; 0x08uy;
                                0x23uy; 0x42uy; 0xb1uy; 0xc1uy; 0x15uy; 0x52uy; 0xd1uy; 0xf0uy;
                                0x24uy; 0x33uy; 0x62uy; 0x72uy; 0x82uy; 0x09uy; 0x0auy; 0x16uy;
                                0x17uy; 0x18uy; 0x19uy; 0x1auy; 0x25uy; 0x26uy; 0x27uy; 0x28uy;
                                0x29uy; 0x2auy; 0x34uy; 0x35uy; 0x36uy; 0x37uy; 0x38uy; 0x39uy;
                                0x3auy; 0x43uy; 0x44uy; 0x45uy; 0x46uy; 0x47uy; 0x48uy; 0x49uy;
                                0x4auy; 0x53uy; 0x54uy; 0x55uy; 0x56uy; 0x57uy; 0x58uy; 0x59uy;
                                0x5auy; 0x63uy; 0x64uy; 0x65uy; 0x66uy; 0x67uy; 0x68uy; 0x69uy;
                                0x6auy; 0x73uy; 0x74uy; 0x75uy; 0x76uy; 0x77uy; 0x78uy; 0x79uy;
                                0x7auy; 0x83uy; 0x84uy; 0x85uy; 0x86uy; 0x87uy; 0x88uy; 0x89uy;
                                0x8auy; 0x92uy; 0x93uy; 0x94uy; 0x95uy; 0x96uy; 0x97uy; 0x98uy;
                                0x99uy; 0x9auy; 0xa2uy; 0xa3uy; 0xa4uy; 0xa5uy; 0xa6uy; 0xa7uy;
                                0xa8uy; 0xa9uy; 0xaauy; 0xb2uy; 0xb3uy; 0xb4uy; 0xb5uy; 0xb6uy;
                                0xb7uy; 0xb8uy; 0xb9uy; 0xbauy; 0xc2uy; 0xc3uy; 0xc4uy; 0xc5uy;
                                0xc6uy; 0xc7uy; 0xc8uy; 0xc9uy; 0xcauy; 0xd2uy; 0xd3uy; 0xd4uy;
                                0xd5uy; 0xd6uy; 0xd7uy; 0xd8uy; 0xd9uy; 0xdauy; 0xe1uy; 0xe2uy;
                                0xe3uy; 0xe4uy; 0xe5uy; 0xe6uy; 0xe7uy; 0xe8uy; 0xe9uy; 0xeauy;
                                0xf1uy; 0xf2uy; 0xf3uy; 0xf4uy; 0xf5uy; 0xf6uy; 0xf7uy; 0xf8uy;
                                0xf9uy; 0xfauy
                            |]
                        }

                    specACChroma = 
                        {
                             counts = [| 0; 0; 2; 1; 2; 4; 4; 3; 4; 7; 5; 4; 4; 0; 1; 2; 0x77 |]
                             values = 
                             [|
                                0x00uy; 0x01uy; 0x02uy; 0x03uy; 0x11uy; 0x04uy; 0x05uy; 0x21uy;
                                0x31uy; 0x06uy; 0x12uy; 0x41uy; 0x51uy; 0x07uy; 0x61uy; 0x71uy;
                                0x13uy; 0x22uy; 0x32uy; 0x81uy; 0x08uy; 0x14uy; 0x42uy; 0x91uy;
                                0xa1uy; 0xb1uy; 0xc1uy; 0x09uy; 0x23uy; 0x33uy; 0x52uy; 0xf0uy;
                                0x15uy; 0x62uy; 0x72uy; 0xd1uy; 0x0auy; 0x16uy; 0x24uy; 0x34uy;
                                0xe1uy; 0x25uy; 0xf1uy; 0x17uy; 0x18uy; 0x19uy; 0x1auy; 0x26uy;
                                0x27uy; 0x28uy; 0x29uy; 0x2auy; 0x35uy; 0x36uy; 0x37uy; 0x38uy;
                                0x39uy; 0x3auy; 0x43uy; 0x44uy; 0x45uy; 0x46uy; 0x47uy; 0x48uy;
                                0x49uy; 0x4auy; 0x53uy; 0x54uy; 0x55uy; 0x56uy; 0x57uy; 0x58uy;
                                0x59uy; 0x5auy; 0x63uy; 0x64uy; 0x65uy; 0x66uy; 0x67uy; 0x68uy;
                                0x69uy; 0x6auy; 0x73uy; 0x74uy; 0x75uy; 0x76uy; 0x77uy; 0x78uy;
                                0x79uy; 0x7auy; 0x82uy; 0x83uy; 0x84uy; 0x85uy; 0x86uy; 0x87uy;
                                0x88uy; 0x89uy; 0x8auy; 0x92uy; 0x93uy; 0x94uy; 0x95uy; 0x96uy;
                                0x97uy; 0x98uy; 0x99uy; 0x9auy; 0xa2uy; 0xa3uy; 0xa4uy; 0xa5uy;
                                0xa6uy; 0xa7uy; 0xa8uy; 0xa9uy; 0xaauy; 0xb2uy; 0xb3uy; 0xb4uy;
                                0xb5uy; 0xb6uy; 0xb7uy; 0xb8uy; 0xb9uy; 0xbauy; 0xc2uy; 0xc3uy;
                                0xc4uy; 0xc5uy; 0xc6uy; 0xc7uy; 0xc8uy; 0xc9uy; 0xcauy; 0xd2uy;
                                0xd3uy; 0xd4uy; 0xd5uy; 0xd6uy; 0xd7uy; 0xd8uy; 0xd9uy; 0xdauy;
                                0xe2uy; 0xe3uy; 0xe4uy; 0xe5uy; 0xe6uy; 0xe7uy; 0xe8uy; 0xe9uy;
                                0xeauy; 0xf2uy; 0xf3uy; 0xf4uy; 0xf5uy; 0xf6uy; 0xf7uy; 0xf8uy;
                                0xf9uy; 0xfauy
                             |]
                        }
                }


        type BitStream(coder : HuffmanCoder, s : System.IO.Stream) =
            let mutable current = Codeword.Empty
            let result = System.Collections.Generic.List<byte>()

            static let bla (f : byte[]) =
                f |> Array.collect (fun b ->
                    if b = 0xFFuy then [| b; 0x00uy |]
                    else [| b |]
                )

            
            let write arr =
                let arr = bla arr
                result.AddRange arr

            let s = ()

            member x.Write(value : uint32) =
                let a = Codeword.Create(16, uint16 (value >>> 16))
                x.Write a
                let b = Codeword.Create(16, uint16 value)
                x.Write b

            member x.Write(w : Codeword) =
                let appendBits = min w.Length (16 - current.Length)

                if appendBits >= w.Length then
                    current <- current.Append w
                    ()
                else
                    current <- current.Append (w.Take appendBits)
                    let h = current.Store >>> 8 |> byte
                    let l = current.Store |> byte
                    write [| h; l |]
                    current <- w.Skip appendBits

            member x.Write(b : byte) =
                x.Write(Codeword.Create(8, uint16 b))

            member x.Flush() =
                let arr = current.ToByteArray()
                write arr
                current <- Codeword.Empty

                let data = result.ToArray()
                result.Clear()
                write data
                //s.Write(data, 0, data.Length)

            member x.WriteDCLum(v : int) =
                let dc = Fun.HighestBit(abs v) + 1
                let off = if v < 0 then (1 <<< dc) - 1 else 0
                let v = uint16 (off + v)
                let huff = coder.dcLumInv.[dc]
                x.Write(huff)
                x.Write(Codeword.Create(dc, v))

            member x.WriteACLum(leadingZeros : int, v : int) =
                assert(leadingZeros < 16) 

                let dc = Fun.HighestBit(abs v) + 1
                let off = if v < 0 then (1 <<< dc) - 1 else 0
                let v = uint16 (off + v)

                let index = (byte leadingZeros <<< 4) ||| byte dc
                let huff = coder.acLumInv.[int index]
                x.Write huff
                x.Write(Codeword.Create(dc, v))

                
            member x.WriteDCChrom(v : int) =
                let dc = Fun.HighestBit(abs v) + 1
                let off = if v < 0 then (1 <<< dc) - 1 else 0
                let v = uint16 (off + v)
                let huff = coder.dcChromaInv.[dc]
                x.Write(huff)
                x.Write(Codeword.Create(dc, v))

            member x.WriteACChrom(leadingZeros : int, v : int) =
                assert(leadingZeros < 16) 

                let dc = Fun.HighestBit(abs v) + 1
                let off = if v < 0 then (1 <<< dc) - 1 else 0
                let v = uint16 (off + v)

                let index = (byte leadingZeros <<< 4) ||| byte dc
                let huff = coder.acChromaInv.[int index]
                x.Write huff
                x.Write(Codeword.Create(dc, v))

        let writeBlock (bs : BitStream) (lastDC : V3i) (block : V3i[]) =
            bs.WriteDCLum(block.[0].X - lastDC.X)
            let mutable leading = 0
            let mutable i = 1 
            while i < 64 do
                while i < 64 && block.[i].X = 0 do 
                    leading <- leading + 1
                    i <- i + 1

                if i < 64 then
                    let v = block.[i]

                    while leading >= 16 do
                        bs.Write(0xF0uy)
                        leading <- leading - 16

                    bs.WriteACLum(leading, v.X)
                    leading <- 0
                    i <- i + 1
                else
                    bs.WriteACLum(0, 0)

            for d in 1 .. 2 do
                bs.WriteDCChrom(block.[0].[d] - lastDC.[d])
                let mutable leading = 0
                let mutable i = 1 
                while i < 64 do
                    while i < 64 && block.[i].[d] = 0 do 
                        leading <- leading + 1
                        i <- i + 1

                    if i < 64 then
                        let v = block.[i]

                        while leading >= 16 do
                            bs.Write(0xF0uy)
                            leading <- leading - 16


                        bs.WriteACChrom(leading, v.[d])
                        leading <- 0
                        i <- i + 1
                    else
                        bs.WriteACChrom(0, 0)

        let writeImage (size : V2i) (blocks : list<V3i[]>) =
            let ms = new System.IO.MemoryStream()
            let coder = Huffman.photoshop

            let header = [| 0xFFuy; 0xD8uy;  |]
            ms.Write(header, 0, header.Length)

            let encode (v : uint16) =
                [| byte (v >>> 8); byte v |]


            let quant = 
                Array.concat [
                    [| 0xFFuy; 0xDBuy; 0x00uy; 0x84uy |]
                    [| 0x00uy |]
                    QLum |> zigZag |> Array.map byte
                    [| 0x01uy |]
                    QChrom |> zigZag |> Array.map byte
                ]
            ms.Write(quant, 0, quant.Length)

            let sof =
                Array.concat [
                    [| 0xFFuy; 0xC0uy; 0x00uy; 0x11uy |]
                    [| 0x08uy |]
                    encode (uint16 size.Y)
                    encode (uint16 size.X)
                    [| 0x03uy; |]
                    [| 0x01uy; 0x11uy; 0x00uy |]
                    [| 0x02uy; 0x11uy; 0x01uy |]
                    [| 0x03uy; 0x11uy; 0x01uy |]
                ]
            ms.Write(sof, 0, sof.Length)

            let huff =
                let huffSize (spec : TableSpec)  =
                     uint16 (1 + 16 + spec.values.Length)

                let encodeHuff (kind : byte) (spec : TableSpec) =
                    Array.concat [
                        [| kind |]
                        Array.skip 1 spec.counts |> Array.map byte
                        spec.values
                    ]

                Array.concat [
                    [| 0xFFuy; 0xC4uy |]
                    encode (2us + huffSize coder.spec.specDCLum + huffSize coder.spec.specDCChroma + huffSize coder.spec.specACLum + huffSize coder.spec.specACChroma)

                    encodeHuff 0x00uy coder.spec.specDCLum
                    encodeHuff 0x01uy coder.spec.specDCChroma
                    encodeHuff 0x10uy coder.spec.specACLum
                    encodeHuff 0x11uy coder.spec.specACChroma
                ]
            ms.Write(huff, 0, huff.Length)

            let sos = [| 0xFFuy; 0xDAuy; 0x00uy; 0x0Cuy; 0x03uy; 0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x11uy; 0x00uy; 0x3Fuy; 0x00uy |]
            ms.Write(sos, 0, sos.Length)

            let start = ms.Position
            let bs = new BitStream(coder, ms)
            let mutable lastDC = V3i.Zero
            for b in blocks do 
                writeBlock bs lastDC b
                lastDC <- b.[0]
            bs.Flush()
            let len = ms.Position - start
            Log.warn "total scan size: %d (%.3f bpp)" len (float (8L * len) / (float (size.X * size.Y)))
            ms.Write([| 0xFFuy; 0xD9uy |], 0, 2)

            ms.ToArray()



        let test() = 
            testBlocks
                |> List.map (ycbcr >> dct >> quantify >> zigZag)
                |> writeImage imageSize

    

    [<ReflectedDefinition>]
    module JpegGPU =

        let inputImage =
            sampler2d {
                texture uniform?InputImage
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let QLum =
            [|
                2;  2;  2;  2;  3;  4;  5;  6;
                2;  2;  2;  2;  3;  4;  5;  6;
                2;  2;  2;  2;  4;  5;  7;  9;
                2;  2;  2;  4;  5;  7;  9; 12;
                3;  3;  4;  5;  8; 10; 12; 12;
                4;  4;  5;  7; 10; 12; 12; 12;
                5;  5;  7;  9; 12; 12; 12; 12;
                6;  6;  9; 12; 12; 12; 12; 12;
            |]

        let QChrom =
            [|
                 3;  3;  5;  9; 13; 15; 15; 15;
                 3;  4;  6; 11; 14; 12; 12; 12;
                 5;  6;  9; 14; 12; 12; 12; 12;
                 9; 11; 14; 12; 12; 12; 12; 12;
                13; 14; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
            |]

        let Q = Array.map2 (fun l c -> V3d(float l, float c, float c)) QLum QChrom

        let y =  V4d(  0.299,       0.587,      0.114,     -128.0 )
        let cb = V4d( -0.168736,   -0.331264,   0.5,        0.0   )
        let cr = V4d(  0.5,        -0.418688,  -0.081312,   0.0   )


        let inverseZigZagOrder =
            [|
                0; 1; 5; 6; 14; 15; 27; 28
                2; 4; 7; 13; 16; 26; 29; 42
                3; 8; 12; 17; 25; 30; 41; 43
                9; 11; 18; 24; 31; 40; 44; 53
                10; 19; 23; 32; 39; 45; 52; 54
                20; 22; 33; 38; 46; 51; 55; 60
                21; 34; 37; 47; 50; 56; 59; 61
                35; 36; 48; 49; 57; 58; 62; 63
            |]

        let ycbcr (v : V3d) =
            let v = 255.0 * v
            V3d(
                Vec.dot y (V4d(v, 1.0)),
                Vec.dot cb (V4d(v, 1.0)),
                Vec.dot cr (V4d(v, 1.0))
            )

        let quantify (i : int) (v : V3d) =
            let t = v / Q.[i]
            V3d(round t.X, round t.Y, round t.Z)

        [<LocalSize(X = 8, Y = 8)>]
        let dct (size : V2i) (target : V4d[]) =
            compute {
                let size = size
                let ll : V3d[] = allocateShared 64

                let c = getGlobalId().XY
                let lc = getLocalId().XY
                let lid = lc.Y * 8 + lc.X

                ll.[lid] <- ycbcr inputImage.[c, 0].XYZ
                barrier()
                let x = c.X
                let y = c.Y

                let f = (if x = 0 then Constant.Sqrt2Half else 1.0) * (if y = 0 then Constant.Sqrt2Half else 1.0)

                let mutable sum = V3d.Zero
                let mutable i = 0
                for n in 0 .. 7 do
                    for m in 0 .. 7 do
                        let a = cos ( (2.0 * float m + 1.0) * Constant.Pi * float x / 16.0)
                        let b = cos ( (2.0 * float n + 1.0) * Constant.Pi * float y / 16.0)
                        sum <- sum + ll.[i] * a * b
                        i <- i + 1

                let dct = 0.25 * f * sum |> quantify lid
                barrier()

                ll.[inverseZigZagOrder.[lid]] <- dct
                barrier()

                let tid = c.X + size.X * c.Y
                target.[tid] <- V4d(ll.[lid], 1.0)
            }
        
        
        type Ballot () =
            
            [<GLSLIntrinsic("gl_SubGroupSizeARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member SubGroupSize() : uint32 =
                failwith ""

            [<GLSLIntrinsic("ballotARB({0})", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member Ballot(a : bool) : uint64 =
                failwith ""

            [<GLSLIntrinsic("bitCount({0})")>]
            static member BitCount(u : uint32) : int =
                failwith ""

            [<GLSLIntrinsic("findMSB({0})")>]
            static member MSB(u : uint32) : int =
                failwith ""

            [<GLSLIntrinsic("gl_SubGroupLtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member LessMask() : uint64 =
                failwith ""

            [<GLSLIntrinsic("gl_SubGroupLeMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member LessEqualMask() : uint64 =
                failwith ""

            [<GLSLIntrinsic("gl_SubGroupGtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member GreaterMask() : uint64 =
                failwith ""

            [<GLSLIntrinsic("gl_SubGroupGeMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member GreaterEqualMask() : uint64 =
                failwith ""
                
            [<GLSLIntrinsic("addInvocationsAMD", "GL_AMD_shader_ballot", "GL_ARB_gpu_shader_int64")>]
            static member AddInvocations(v : int) : int =
                failwith ""
        
            [<GLSLIntrinsic("atomicOr({0}, {1})")>]
            static member AtomicOr(r : 'a, v : 'a) : unit =
                failwith ""
            [<GLSLIntrinsic("atomicAdd({0}, {1})")>]
            static member AtomicAdd(r : 'a, v : 'a) : 'a =
                failwith ""


        let dcLumInv = Jpeg.Huffman.photoshop.dcLumInv |> Array.map (fun c -> (uint32 c.Length) ||| (uint32 c.Store <<< 16))
        let dcChromaInv = Jpeg.Huffman.photoshop.dcChromaInv |> Array.map (fun c -> (uint32 c.Length) ||| (uint32 c.Store <<< 16))
        let acLumInv = Jpeg.Huffman.photoshop.acLumInv |> Array.map (fun c -> (uint32 c.Length) ||| (uint32 c.Store <<< 16))
        let acChromaInv = Jpeg.Huffman.photoshop.acChromaInv |> Array.map (fun c -> (uint32 c.Length) ||| (uint32 c.Store <<< 16))

        let lenMask = 0x000000FFu
        let huffMask = 0xFFFF0000u


        [<ReflectedDefinition>]
        let inline scan1 (v : 'a) =
            let scanSize = LocalSize.X
            let lid = getLocalId().X
            let temp = allocateShared<'a> LocalSize.X
            temp.[lid] <- v
            barrier()

            let mutable s = 1
            let mutable d = 2
            while d <= scanSize do
                if lid % d = 0 && lid >= s then
                    temp.[lid] <- temp.[lid - s] + temp.[lid]

                barrier()
                s <- s <<< 1
                d <- d <<< 1

            d <- d >>> 1
            s <- s >>> 1
            while s >= 1 do
                if lid % d = 0 && lid + s < scanSize then
                    temp.[lid + s] <- temp.[lid] + temp.[lid + s]
                    
                barrier()
                s <- s >>> 1
                d <- d >>> 1
              
            let left =
                if lid > 0 then temp.[lid - 1]
                else LanguagePrimitives.GenericZero
        
            temp.[lid]

        let encode (index : int) (chroma : bool) (leading : int) (value : float) : int * uint32 =
            let value = int value
            if chroma then
                if index = 0 then
                    let dc = Ballot.MSB(uint32 (abs value)) + 1
                    let off = if value < 0 then (1 <<< dc) else 0
                    let v = uint32 (off + value)
                    let huff = dcChromaInv.[dc]
                    let len = huff &&& lenMask |> int
                    let huff = huff &&& huffMask

                    len + dc, huff ||| (v <<< (32 - len + dc))
                else
                    let dc = Ballot.MSB(uint32 (abs value)) + 1
                    let off = if value < 0 then (1 <<< dc) else 0
                    let v = uint32 (off + value)
                    let huff = acChromaInv.[dc]
                    let len = huff &&& lenMask |> int
                    let huff = huff &&& huffMask

                    len + dc, huff ||| (v <<< (32 - len + dc))

            else
                if index = 0 then
                    let dc = Ballot.MSB(uint32 (abs value)) + 1
                    let off = if value < 0 then (1 <<< dc) else 0
                    let v = uint32 (off + value)
                    let huff = dcLumInv.[dc]
                    let len = huff &&& lenMask |> int
                    let huff = huff &&& huffMask

                    len + dc, huff ||| (v <<< (32 - len + dc))
                else
                    let dc = Ballot.MSB(uint32 (abs value)) + 1
                    let off = if value < 0 then (1 <<< dc) else 0
                    let v = uint32 (off + value)
                    let huff = acLumInv.[dc]
                    let len = huff &&& lenMask |> int
                    let huff = huff &&& huffMask

                    len + dc, huff ||| (v <<< (32 - len + dc))

        let takeHigh (cnt : int) (word : uint32) =
            word >>> (32 - cnt)
            
        let takeLow (cnt : int) (word : uint32) =
            word &&& ((1u <<< cnt) - 1u)

        let skip (cnt : int) (word : uint32) =
            word <<< cnt

        [<LocalSize(X = 32)>]
        let compact (channel : int) (counter : int[]) (data : float[]) (ranges : V2i[]) (mask : uint32[]) =
            compute {
                let mem = allocateShared 64
                let temp = allocateShared 64
                let offsetStore = allocateShared 1

                let offset = getWorkGroupId().X * 64

                let lid = getLocalId().X
                let llid = 2 * lid 
                let rlid = llid + 1

                let gid = getGlobalId().X
                let li = 2 * gid
                let ri = li + 1


                let lv = data.[li * 4 + channel]
                let rv = data.[ri * 4 + channel]

                let lnz = llid = 0 || lv <> 0.0
                let rnz = rv <> 0.0


                // count leading zeros
                let lessMask = Ballot.LessMask() |> uint32
                let greaterMask = Ballot.GreaterMask() |> uint32
                let lb = Ballot.Ballot(lnz) |> uint32
                let rb = Ballot.Ballot(rnz) |> uint32


                let lpm = Ballot.MSB(lb &&& lessMask)
                let rpm = Ballot.MSB(rb &&& lessMask)
                
                let nonZeroAfterR = (lb &&& greaterMask) <> 0u || (rb &&& greaterMask) <> 0u
                let nonZeroAfterL = rnz || nonZeroAfterR

                let lp =
                    if lpm > rpm then 2 * lpm
                    else 2 * rpm + 1

                let rp =
                    if lnz then li
                    else lp

                let llz = li - lp - 1
                let rlz = ri - rp - 1


                let scanSize = 64

                // encode values and write their bit-counts to mem
                // TODO: what about >=16 zeros??? => should be done
                // TODO: what about EOB marker
                let mutable lCode = 0u
                let mutable rCode = 0u
                let mutable lLength = 0
                let mutable rLength = 0

                if llid = 0 then
                    let v = if lid >= 64 then lv - data.[lid - 4*64] else lv
                    let lSize, lc = encode 0 (channel <> 0) 0 v
                    lCode <- lc
                    lLength <- lSize

                elif lnz then
                    let lSize, lc = encode llid (channel <> 0) (llz % 16) lv
                    lCode <- lc
                    lLength <- lSize

                elif llz > 0 && llz % 16 = 0 && nonZeroAfterL then
                    lCode <- 0xFAu
                    lLength <- 8


                if rnz then
                    let rSize, rc = encode rlid (channel <> 0) (rlz % 16) rv
                    rCode <- rc
                    rLength <- rSize

                elif rlz > 0 && rlz % 16 = 0 && nonZeroAfterR then
                    rCode <- 0xFAu
                    rLength <- 8




                mem.[llid] <- lLength
                mem.[rlid] <- rLength        

                // scan mem
                barrier()

                let mutable s = 1
                let mutable d = 2
                while d <= scanSize do
                    if llid % d = 0 && llid >= s then
                        mem.[llid] <- mem.[llid - s] + mem.[llid]

                    barrier()
                    s <- s <<< 1
                    d <- d <<< 1

                d <- d >>> 1
                s <- s >>> 1
                while s >= 1 do
                    if llid % d = 0 && llid + s < scanSize then
                        mem.[llid + s] <- mem.[llid] + mem.[llid + s]
                    
                    barrier()
                    s <- s >>> 1
                    d <- d >>> 1
                    


                temp.[llid] <- 0u
                temp.[rlid] <- 0u
                barrier()

                if lLength > 0 then
                    let bitOffset = if llid > 0 then mem.[llid - 1] else 0
                    let bitLength = lLength
                    let store = lCode

                    let oi = bitOffset / 32
                    let oo = bitOffset % 32

                    let word = takeHigh bitLength store
                    let space = 32 - oo

                    if bitLength <= space then
                        // oo = 0 => word <<< (32 - bitLength)
                        // oo = 16 => word <<< (16 - bitLength)
                        // oo = 24 => word <<< (8 - bitLength)
                        let a = word <<< (space - bitLength)
                        Ballot.AtomicOr(temp.[oi], a)

                    else
                        let a = takeHigh space word
                        Ballot.AtomicOr(temp.[oi], a)

                        let cnt = bitLength - space
                        let b = takeLow cnt word <<< (32 - cnt)
                        Ballot.AtomicOr(temp.[oi+1], b)

                if rLength > 0 then
                    let bitOffset = mem.[rlid - 1]
                    let bitLength = rLength
                    let store = rCode

                    let oi = bitOffset / 32
                    let oo = bitOffset % 32

                    let word = takeHigh bitLength store
                    let space = 32 - oo

                    if bitLength <= space then
                        // oo = 0 => word <<< (32 - bitLength)
                        // oo = 16 => word <<< (16 - bitLength)
                        // oo = 24 => word <<< (8 - bitLength)
                        let a = word <<< (space - bitLength)
                        Ballot.AtomicOr(temp.[oi], a)

                    else
                        let a = takeHigh space word
                        Ballot.AtomicOr(temp.[oi], a)

                        let cnt = bitLength - space
                        let b = takeLow cnt word <<< (32 - cnt)
                        Ballot.AtomicOr(temp.[oi+1], b)



                barrier()

                let bitCnt = mem.[63]
                let intCnt = (if bitCnt % 32 = 0 then bitCnt / 32 else 1 + bitCnt / 32)
                if lid = 0 then
                    let off = Ballot.AtomicAdd(counter.[0], intCnt)
                    ranges.[getWorkGroupId().X] <- V2i(off, bitCnt)
                    offsetStore.[0] <- off

                barrier()
                let offset = offsetStore.[0]

                if llid < intCnt then
                    mask.[offset + llid] <- temp.[llid]
                    
                if rlid < intCnt then
                    mask.[offset + rlid] <- temp.[rlid]

            
            }

//
//    [<LocalSize(X = 64)>]
//    let computer (values : float[]) (res : float[]) =
//        compute {
//            let id = getGlobalId().X
//            let v = values.[id] //if values.[id] > 0.0 then 1 else 0
//
//            let lg = scan1 v
//            res.[id] <- lg
//        }

    let next (v : int) (a : int) =
        if v % a = 0 then v
        else (1 + v / a) * a

    let writeImage (size : V2i) (offsets : V3i[]) (counts : V3i[]) (data : uint32[]) =
        let ms = new System.IO.MemoryStream()
        let coder = Jpeg.Huffman.photoshop

        let header = [| 0xFFuy; 0xD8uy;  |]
        ms.Write(header, 0, header.Length)

        let encode (v : uint16) =
            [| byte (v >>> 8); byte v |]


        let quant = 
            Array.concat [
                [| 0xFFuy; 0xDBuy; 0x00uy; 0x84uy |]
                [| 0x00uy |]
                Jpeg.QLum |> Jpeg.zigZag |> Array.map byte
                [| 0x01uy |]
                Jpeg.QChrom |> Jpeg.zigZag |> Array.map byte
            ]
        ms.Write(quant, 0, quant.Length)

        let sof =
            Array.concat [
                [| 0xFFuy; 0xC0uy; 0x00uy; 0x11uy |]
                [| 0x08uy |]
                encode (uint16 size.Y)
                encode (uint16 size.X)
                [| 0x03uy; |]
                [| 0x01uy; 0x11uy; 0x00uy |]
                [| 0x02uy; 0x11uy; 0x01uy |]
                [| 0x03uy; 0x11uy; 0x01uy |]
            ]
        ms.Write(sof, 0, sof.Length)

        let huff =
            let huffSize (spec : Jpeg.TableSpec)  =
                uint16 (1 + 16 + spec.values.Length)

            let encodeHuff (kind : byte) (spec : Jpeg.TableSpec) =
                Array.concat [
                    [| kind |]
                    Array.skip 1 spec.counts |> Array.map byte
                    spec.values
                ]

            Array.concat [
                [| 0xFFuy; 0xC4uy |]
                encode (2us + huffSize coder.spec.specDCLum + huffSize coder.spec.specDCChroma + huffSize coder.spec.specACLum + huffSize coder.spec.specACChroma)

                encodeHuff 0x00uy coder.spec.specDCLum
                encodeHuff 0x01uy coder.spec.specDCChroma
                encodeHuff 0x10uy coder.spec.specACLum
                encodeHuff 0x11uy coder.spec.specACChroma
            ]
        ms.Write(huff, 0, huff.Length)

        let sos = [| 0xFFuy; 0xDAuy; 0x00uy; 0x0Cuy; 0x03uy; 0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x11uy; 0x00uy; 0x3Fuy; 0x00uy |]
        ms.Write(sos, 0, sos.Length)

        let start = ms.Position
        let bs = new Jpeg.BitStream(coder, ms)


        for bi in 0 .. offsets.Length - 1 do
            for ci in 0 .. 2 do
                let offset = offsets.[bi].[ci]
                let size = counts.[bi].[ci]

                let mutable i = 0
                while i <= size - 32 do
                    bs.Write data.[offset + i / 32]
                    i <- i + 32

                if size > i then
                    let mutable rem = size - i
                    if rem > 16 then
                        let w = Codeword.Create(16, uint16 (data.[offset + i/32] >>> 16))
                        bs.Write(w)
                        rem <- rem - 16

                    let w = Codeword.Create(rem, uint16 (data.[offset + i/32] >>> (16 - rem)))
                    bs.Write(w)

                        
                if ci = 0 then
                    bs.WriteACLum(0, 0)
                else
                    bs.WriteACChrom(0,0)

        bs.Flush()
        let len = ms.Position - start
        Log.warn "total scan size: %d (%.3f bpp)" len (float (8L * len) / (float (size.X * size.Y)))
        ms.Write([| 0xFFuy; 0xD9uy |], 0, 2)

        ms.ToArray()




    let testDCT() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Device
        
        let dct = device |> ComputeShader.ofFunction JpegGPU.dct
        let input = ComputeShader.newInputBinding dct app.Runtime.DescriptorPool
        
        let rand = RandomSystem()

        let image = PixImage<byte>(Col.Format.RGBA, V2i(8,8))
        image.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            if c.X > 4L then C4b.White
            else C4b.Black
        ) |> ignore

        let alignedSize = V2i(next image.Size.X 8, next image.Size.Y 8)

        let sourceData = device.CreateImage(PixTexture2d(PixImageMipMap [| image :> PixImage|], TextureParams.empty))
        let targetBuffer = device.CreateBuffer<V4f>(int64 alignedSize.X * int64 alignedSize.Y)

        input.["size"] <- alignedSize
        input.["target"] <- targetBuffer
        input.["InputImage"] <- sourceData
        input.Flush()

        device.perform {
            do! Command.Bind dct
            do! Command.SetInputs input
            do! Command.Dispatch (alignedSize / V2i(8,8))
            do! Command.Sync(targetBuffer, VkAccessFlags.ShaderWriteBit, VkAccessFlags.TransferReadBit)
        }


        let encode = device |> ComputeShader.ofFunction JpegGPU.compact
        let input = ComputeShader.newInputBinding encode app.Runtime.DescriptorPool


        //let data = device.CreateBuffer<V4f>(Array.init 64 (fun i -> if i % 3 = 0 then V4f(i,i,i,i) else V4f.Zero))
        let blocks = (alignedSize.X * alignedSize.Y) / 64
        let ranges = device.CreateBuffer<V2i>(int64 blocks)
        let b = device.CreateBuffer<uint32>(64L * int64 blocks)
        let counter = device.CreateBuffer<int>(Array.zeroCreate 1)

        input.["data"] <- targetBuffer
        input.["ranges"] <- ranges
        input.["counter"] <- counter
        input.["mask"] <- b
        input.Flush()
        
        let offsets : V3i[] = Array.zeroCreate blocks
        let counts : V3i[] = Array.zeroCreate blocks

        for c in 0 .. 2 do
            input.["channel"] <- c
            input.Flush()

            device.perform {
                do! Command.Bind encode
                do! Command.SetInputs input
                do! Command.Dispatch blocks
            }

            let r = Array.zeroCreate blocks
            ranges.Download(r)
            for i in 0 .. blocks - 1 do
                offsets.[i].[c] <- r.[i].X
                counts.[i].[c] <- r.[i].Y



        let cnt = Array.zeroCreate 1
        counter.Download(cnt)

        let arr = Array.zeroCreate cnt.[0]
        b.Download(0L, arr, 0L, arr.LongLength)

        let data = writeImage image.Size offsets counts arr
        File.writeAllBytes @"C:\Users\Schorsch\Desktop\hugo.jpg" data
        printfn "%A (%A) %A" arr.[0] offsets.[0] cnt.[0]
        Environment.Exit 0

        

        let arr = Array.zeroCreate (alignedSize.X * alignedSize.Y)
        targetBuffer.Download(arr)

        printfn "%A" arr

        let reference() =
            let mat = image.GetMatrix<C4b>().SubMatrix(V2l.Zero, V2l(8L,8L))

            let arr = Array.zeroCreate 64
            mat.ForeachXYIndex (fun (x : int64) (y : int64) (i : int64) ->
                let v = mat.[i]
                let ti = int x + 8 * int y
                arr.[ti] <- v.ToC3f().ToV3d()
            )

            arr
                |> Jpeg.ycbcr
                |> Jpeg.dct
                |> Jpeg.quantify
                |> Jpeg.zigZag
                |> Array.map (V3d)

        let ref = reference()

        let diff = Array.map2 (fun (gpu : V4f) (cpu : V3d) -> (V3d gpu.XYZ - cpu).LengthSquared) arr ref

        let sum = diff |> Array.fold (+) V3d.Zero
        printfn "dev: %A" sum


        ()
    let testscan() =
        testDCT()
//
//        let data = Jpeg.test()
//        //data |> Array.map (sprintf "0x%02X") |> String.concat ";" |> printfn "%s"
//        Log.warn "total size: %d (%.3f bpp)" data.Length (float (8 * data.Length) / float (Jpeg.imageSize.X * Jpeg.imageSize.Y))
//
//        File.writeAllBytes @"C:\Users\Schorsch\Desktop\wtf.jpg" data
//
//



        System.Environment.Exit 0

        use app = new HeadlessVulkanApplication(false)
        let device = app.Device

        // generate random data
        let cnt = 1 <<< 26
        let rand = RandomSystem()
        let inputData = Array.init cnt (fun _ -> V4d(rand.UniformV3d(), rand.UniformDouble()) |> V4f)

        // compile a scan using (+) as accumulation function
        let scanner = app.Runtime.CompileScan <@ (+) @>

        // create buffers holding the in-/output
        let input   = device.CreateBuffer(inputData)
        let output  = device.CreateBuffer(int64 cnt)

        //device.Scan(<@ (*) @>, input, output)

        // perform the scan once
        device.perform {
            do! scanner.Invoke(input, output)
        }

        // compile a scan-command-buffer ahead of time
        let cmd = scanner.Compile(input, output)

        // run the pre-compiled scan 100 times and measure its execution-time
        let queue = device.GraphicsFamily.GetQueue()
        let iter = 100
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            queue.RunSynchronously cmd
        sw.Stop()
        Log.warn "took %A" (sw.MicroTime / iter)
            

        // validate the scan result using a single threaded CPU implementation (and measure its time)
        let check = Array.zeroCreate cnt
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. 10 do
            check.[0] <- inputData.[0]
            for i in 1 .. check.Length - 1 do
                check.[i] <- check.[i-1] + inputData.[i]
        sw.Stop()
        Log.warn "took %A" (sw.MicroTime / 10)



        let mutable hist : MapExt<int, int * float> = MapExt.empty

        let mutable ok = true
        // download the result and check it against the CPU version
        let result = Array.zeroCreate cnt
        output.Download(result)
        for i in 0 .. cnt - 1 do
            if check.[i] <> result.[i] then
                ok <- false
//            let d = M44d.Distance2(M44d.op_Explicit result.[i], M44d.op_Explicit check.[i]) |> float
//            let e = if d <= 0.0 then -1000 else Fun.Log10 d |> int
//            hist <-
//                hist |> MapExt.alter e (fun o ->
//                    match o with
//                        | Some (cnt, v) -> Some (cnt + 1, v + d)
//                        | None -> Some (1, d)
//                )
//
//        printfn "results"
//        for (e,(cnt,v)) in MapExt.toSeq hist do
//            if e <= -1000 then
//                printfn "   0:          (%d)" cnt
//            else
//                let v = (v / float cnt)
//                let v = v / (10.0 ** float e)
//                printfn "   1E%d: %.3f (%d)" e v cnt

        if ok then printfn "OK"
        else printfn "ERROR"

        // relase all the resources
        cmd.Dispose()
        scanner.Dispose()
        device.Delete input
        device.Delete output


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

    Jpeg.Test.run()
    Environment.Exit 0

    //tensorPerformance()
    VulkanTests.testscan()


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
