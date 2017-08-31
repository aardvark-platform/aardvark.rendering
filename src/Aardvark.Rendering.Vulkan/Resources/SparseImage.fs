namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop


#nowarn "9"
#nowarn "51"

type SparseImageBind =
    {
        level   : int
        slice   : int
        offset  : V3i
        size    : V3i
        pointer : Option<DevicePtr>
    }

    static member Unbind(level : int, slice : int, offset : V3i, size : V3i) =
        {
            level = level
            slice = slice
            offset = offset
            size = size
            pointer = None
        }

    static member Bind(level : int, slice : int, offset : V3i, size : V3i, ptr : DevicePtr) =
        {
            level = level
            slice = slice
            offset = offset
            size = size
            pointer = Some ptr
        }


module private Align =
    let inline next (v : ^a) (a : ^b) =
        if v % a = LanguagePrimitives.GenericZero then v
        else v + (a - (v % a))

    let next3 (v : V3i) (a : V3i) =
        V3i(next v.X a.X, next v.Y a.Y, next v.Z a.Z)

[<AutoOpen>]
module private Helpers = 
    let rec mipSize (s : V3i) (pageSize : V3i) (levels : int) =
        if levels <= 0 then 
            0L
        else
            let sa = Align.next3 s pageSize
            int64 sa.X * int64 sa.Y * int64 sa.Z + mipSize (s / 2) pageSize (levels - 1)

    let div (a : int) (b : int) =
        if a % b = 0 then a / b
        else 1 + a / b

    let div3 (a : V3i) (b : V3i) =
        V3i(div a.X b.X, div a.Y b.Y, div a.Z b.Z)

    let mod3 (a : V3i) (b : V3i) =
        V3i(a.X % b.X, a.Y % b.Y, a.Z % b.Z)    
        
        
           
type SparseImage(device : Device, handle : VkImage, size : V3i, levels : int, slices : int, dim : TextureDimension, format : VkFormat, allocMipTail : DeviceHeap -> int64 -> int64 -> DevicePtr) =
    inherit Image(device, handle, size, levels, slices, 1, dim, format, DevicePtr.Null, VkImageLayout.Undefined)

    let requirements =
        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, &&reqs)
        reqs

    let sparseRequirements =
        let mutable count = 0u
        VkRaw.vkGetImageSparseMemoryRequirements(device.Handle, handle, &&count, NativePtr.zero)

        let requirements = Array.zeroCreate (int count)
        requirements |> NativePtr.withA (fun pRequirements ->
            VkRaw.vkGetImageSparseMemoryRequirements(device.Handle, handle, &&count, pRequirements)
        )

        requirements

    let pageSize = 
        let v = sparseRequirements.[0].formatProperties.imageGranularity
        V3i(int v.width, int v.height, int v.depth)

    let pageSizeInBytes = int64 requirements.alignment
    
    let pageCounts =
        Array.init levels (fun level ->
            let s = size / (1 <<< level)
            V3i(div s.X pageSize.X, div s.Y pageSize.Y, div s.Z pageSize.Z)
        )

    let sparseLevels =
        int sparseRequirements.[0].imageMipTailFirstLod

    let memory =
        let mutable reqs = VkMemoryRequirements()
        VkRaw.vkGetImageMemoryRequirements(device.Handle, handle, &&reqs)

        device.Memories |> Array.find (fun mem ->
            let mask = 1u <<< mem.Index
            mask &&& reqs.memoryTypeBits <> 0u
        )

    let tailPtr = 
        let totalSize = 
            sparseRequirements |> Array.sumBy (fun r ->
                let singleMipTail = r.formatProperties.flags.HasFlag(VkSparseImageFormatFlags.SingleMiptailBit)
                let mipTailSize = r.imageMipTailSize |> int64
                printfn "mip tail size: %A" mipTailSize
                if singleMipTail then
                    mipTailSize
                else
                    int64 slices * mipTailSize
            )

        if totalSize <= 0L then
            DevicePtr.Null
        else
            let tailPtr = allocMipTail memory (int64 requirements.alignment) totalSize // memory.Alloc(int64 requirements.alignment, totalSize)

            let mutable offset = tailPtr.Offset
            let binds = List<VkSparseMemoryBind>()
            for r in sparseRequirements do
                let size =
                    int64 r.imageMipTailSize

                if size > 0L then

                    let singleMipTail = 
                        r.formatProperties.flags.HasFlag(VkSparseImageFormatFlags.SingleMiptailBit)

                    let flags =
                        if r.formatProperties.aspectMask.HasFlag(VkImageAspectFlags.MetadataBit) then VkSparseMemoryBindFlags.MetadataBit
                        else VkSparseMemoryBindFlags.None

                    if singleMipTail then
                        binds.Add <|
                            VkSparseMemoryBind(
                                r.imageMipTailOffset,
                                r.imageMipTailSize,
                                tailPtr.Memory.Handle,
                                uint64 offset,
                                flags
                            )

                        offset <- offset + size

                    else
                        let mutable targetOffset = int64 r.imageMipTailOffset
                        for slice in 0 .. slices - 1 do
                    
                            binds.Add <|
                                VkSparseMemoryBind(
                                    uint64 targetOffset,
                                    r.imageMipTailSize,
                                    tailPtr.Memory.Handle,
                                    uint64 offset,
                                    flags
                                )
                            targetOffset <- targetOffset + int64 r.imageMipTailStride
                            offset <- offset + size
                        

            if binds.Count > 0 then
                let binds = CSharpList.toArray binds
                binds |> NativePtr.withA (fun pBinds ->
                    let mutable images =
                        VkSparseImageOpaqueMemoryBindInfo(
                            handle, uint32 binds.Length, pBinds
                        )

                    let mutable info =
                        VkBindSparseInfo(
                            VkStructureType.BindSparseInfo, 0n,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, &&images,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero
                
                        )

                    let q = device.GraphicsFamily.GetQueue()
                    let f = device.CreateFence()
                    lock q (fun () ->
                        VkRaw.vkQueueBindSparse(q.Handle, 1u, &&info, f.Handle)
                            |> check "could not bind sparse memory"
                    )
                    f.Wait()
                    f.Dispose()
                )  


            tailPtr


    let checkBind (b : SparseImageBind) =
        if b.level < 0 || b.level >= sparseLevels then
            failwith "[SparseImage] level out of bounds"

        if b.slice < 0 || b.slice >= slices then
            failwith "[SparseImage] slice out of bounds"

        if b.size.AnySmaller 0 then
            failwith "[SparseImage] size must be positive"
            
        if b.offset.AnySmaller 0 then
            failwith "[SparseImage] offset must be positive"

        let levelSize = size / (1 <<< b.level)
        let levelPageCount = div3 levelSize pageSize
        let alignedLevelSize = pageSize * levelPageCount

        let min = b.offset
        let max = b.offset + b.size - V3i.III

        if mod3 b.offset pageSize <> V3i.Zero then
            failwith "[SparseImage] non-aligned offset"

        if mod3 b.size pageSize <> V3i.Zero then
            failwith "[SparseImage] non-aligned size"
            

        if min.AnyGreaterOrEqual alignedLevelSize then
            failwith "[SparseImage] region out of bounds"

        if max.AnyGreaterOrEqual alignedLevelSize then
            failwith "[SparseImage] region out of bounds"


        match b.pointer with
            | Some ptr ->
                let pages = b.size / pageSize
                let totalSize = pageSizeInBytes * int64 pages.X * int64 pages.Y * int64 pages.Z
                if ptr.Size < totalSize then
                    failwith "[SparseImage] non-matching memory size"
                    
            | None ->
                ()


    member x.MipTailMemory = tailPtr

    member x.PageSize = pageSize
    
    member x.PageSizeInBytes = pageSizeInBytes
    member x.PageAlign = pageSizeInBytes

    member x.PageCount(level : int) =
        if level < 0 then failwith "level out of bounds"
        elif level >= levels then V3i.Zero
        else pageCounts.[level]

    member x.SparseLevels = sparseLevels

    member x.Memory = memory

    member x.Update(bindings : SparseImageBind[]) =
        if bindings.Length > 0 then
            lock x (fun () ->
                let binds =
                    bindings |> Array.map (fun b ->
                        //checkBind b

                        let o = b.offset
                        let s = b.size

                        let memory, offset =
                            match b.pointer with
                                | Some ptr -> ptr.Memory.Handle, ptr.Offset
                                | None -> VkDeviceMemory.Null, 0L

                        VkSparseImageMemoryBind(
                            VkImageSubresource(VkImageAspectFlags.ColorBit, uint32 b.level, uint32 b.slice),
                            VkOffset3D(o.X, o.Y, o.Z),
                            VkExtent3D(s.X, s.Y, s.Z),
                            memory,
                            uint64 offset,
                            VkSparseMemoryBindFlags.None
                        )

                    )

                binds |> NativePtr.withA (fun pBinds ->
                    let mutable images =
                        VkSparseImageMemoryBindInfo(
                            handle, uint32 binds.Length, pBinds
                        )

                    let mutable info =
                        VkBindSparseInfo(
                            VkStructureType.BindSparseInfo, 0n,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            0u, NativePtr.zero,
                            1u, &&images,
                            0u, NativePtr.zero
                
                        )

                    let q = device.GraphicsFamily.GetQueue()
                    let f = device.CreateFence()
                    lock q (fun () ->
                        VkRaw.vkQueueBindSparse(q.Handle, 1u, &&info, f.Handle)
                            |> check "could not bind sparse memory"
                        f.Wait()
                        f.Dispose()
                    )

                )
            )

    member x.Bind(level : int, slice : int, offset : V3i, size : V3i, ptr : DevicePtr) =
        x.Update [| { level = level; slice = slice; offset = offset; size = size; pointer = Some ptr } |]
     
    member x.Unbind(level : int, slice : int, offset : V3i, size : V3i) =
        x.Update [| { level = level; slice = slice; offset = offset; size = size; pointer = None } |]

    member x.Dispose() =
        device.Delete x
        tailPtr.Dispose()

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<AbstractClass; Sealed; Extension>]
type SparseImageDeviceExtensions private() =

    [<Extension>]
    static member CreateSparseImage(device : Device, size : V3i, mipMapLevels : int, count : int, dim : TextureDimension, format : VkFormat, usage : VkImageUsageFlags, allocMipTail : DeviceHeap -> int64 -> int64 -> DevicePtr) : SparseImage =

        let imageType = VkImageType.ofTextureDimension dim

        let mutable info =
            VkImageCreateInfo(
                VkStructureType.ImageCreateInfo, 0n,
                VkImageCreateFlags.SparseBindingBit ||| VkImageCreateFlags.SparseResidencyBit ||| VkImageCreateFlags.SparseAliasedBit,
                imageType,
                format,
                VkExtent3D(size.X, size.Y, size.Z),
                uint32 mipMapLevels,
                uint32 count,
                VkSampleCountFlags.D1Bit,
                VkImageTiling.Optimal,
                usage,
                device.AllSharingMode, device.AllQueueFamiliesCnt, device.AllQueueFamiliesPtr,
                VkImageLayout.Preinitialized
            )

        let mutable handle = VkImage.Null
        VkRaw.vkCreateImage(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "could not create sparse image"

        new SparseImage(device, handle, size, mipMapLevels, count, dim, format, allocMipTail)
        
    [<Extension>]
    static member CreateSparseImage(device : Device, size : V3i, mipMapLevels : int, count : int, dim : TextureDimension, format : VkFormat, usage : VkImageUsageFlags) =
        SparseImageDeviceExtensions.CreateSparseImage(
            device,
            size, mipMapLevels, count, dim, format, usage, (fun m a s -> m.Alloc(a,s))
        )

    [<Extension>]
    static member Delete(device : Device, image : SparseImage) =
        image.Dispose()

[<AbstractClass; Sealed; Extension>]
type BufferTensorExtensions private() =
    [<Extension>]
    static member MappedTensor4<'a when 'a : unmanaged>(x : DevicePtr, size : V4i, action : NativeTensor4<'a> -> unit) =
        let size = V4l size
        x.Mapped(fun dst ->
            let dst = 
                NativeTensor4<'a>(
                    NativePtr.ofNativeInt dst, 
                    Tensor4Info(
                        0L,
                        size,
                        V4l(size.W, size.X * size.W, size.X * size.Y * size.W, 1L)
                    )
                )
            action dst
        )
   
module SparseTextureImplemetation = 
    open Aardvark.Base.Incremental

    type DoubleBufferedSparseImage<'a when 'a : unmanaged>(device : Device, size : V3i, levels : int, slices : int, dim : TextureDimension, format : Col.Format, usage : VkImageUsageFlags, brickSize : V3i, maxMemory : int64) =
        let fmt = VkFormat.ofPixFormat (PixFormat(typeof<'a>, format))
        let usage = VkImageUsageFlags.TransferSrcBit ||| VkImageUsageFlags.TransferDstBit ||| usage

        let mutable flushAfterUpload = true


        let updateLock = new ReaderWriterLockSlim()
        let memory = device.DeviceMemory.Copy()

        let mutable isDisposed = false

        let mutable mipTailMem = None
        let allocMipTail (heap : DeviceHeap) (align : int64) (size : int64) =
            match mipTailMem with
                | Some mem -> mem
                | None ->
                    let mem = memory.Alloc(align, size)
                    mipTailMem <- Some mem
                    mem

        let mutable back = device.CreateSparseImage(size, levels, slices, dim, fmt, usage, allocMipTail)
        let mutable front = device.CreateSparseImage(size, levels, slices, dim, fmt, usage, allocMipTail)

        do  device.perform {
                do! Command.TransformLayout(back, VkImageLayout.TransferDstOptimal)
                do! Command.TransformLayout(front, VkImageLayout.ShaderReadOnlyOptimal)
            }

        let align = back.PageAlign

        let pageSize =
            back.PageSize

        let brickSizeInBytes = 
            if brickSize.X % pageSize.X <> 0 || brickSize.Y % pageSize.Y <> 0 || brickSize.Z % pageSize.Z <> 0 then
                failwithf "[SparseTexture] invalid brickSize (not a multiple of %A)" pageSize
            
            int64 sizeof<'a> * int64 brickSize.X * int64 brickSize.Y * int64 brickSize.Z

        let maxBricks =
            maxMemory / brickSizeInBytes |> int

        let brickSem = new SemaphoreSlim(maxBricks)
        let cancel = new CancellationTokenSource()

        let unusedPointers = List<DevicePtr>()
        let pendingBinds = Dictionary<(int * int * V3i), SparseImageBind>()
        
        let swapResult = MVar.empty()

        let onSwap = Event<EventHandler, EventArgs>()

        let swapBuffers() =
            System.Threading.Tasks.Task.Factory.StartNew(fun () ->
                ReaderWriterLock.write updateLock (fun () ->
                    Fun.Swap(&back, &front)

                    onSwap.Trigger(null, null)
                    MVar.put swapResult (front :> ITexture)
                    
                    let arr = Seq.toArray pendingBinds.Values


                    // apply pending bindings
                    back.Update arr
                    pendingBinds.Clear()

                    // free memory
                    for ptr in unusedPointers do ptr.Dispose()
                    unusedPointers.Clear()


                    if arr.Length > 0 then
                        brickSem.Release(arr.Length) |> ignore

                    device.perform {
                        do! Command.TransformLayout(back, VkImageLayout.TransferDstOptimal)
                    }
                )
            ) |> ignore

            MVar.take swapResult

        let texture =
            Mod.custom (fun t ->
                if isDisposed then
                    NullTexture() :> ITexture
                else
                    swapBuffers()
            )

        let invalidate =
            let evt = new AutoResetEvent(false)
            let run =
                async {
                    while true do
                        let! _ = Async.AwaitWaitHandle evt
                        do! Async.SwitchToThreadPool()
                        transact (fun () -> texture.MarkOutdated())
                }

            Async.Start(run, cancel.Token)
            fun () -> evt.Set() |> ignore

        let channels =
            VkFormat.channels back.Format

        
        [<CLIEvent>]
        member x.OnSwap = onSwap.Publish

        member x.FlushAfterUpload 
            with get () = flushAfterUpload
            and set v = flushAfterUpload <- v

        member private x.Revoke(bind : SparseImageBind) =
            ReaderWriterLock.read updateLock (fun () ->
                let pointer = bind.pointer.Value
                let unbind = { bind with pointer = None }
                back.Update [| unbind |]
                lock pendingBinds (fun () -> 
                    pendingBinds.[(unbind.level, unbind.slice, unbind.offset)] <- unbind
                    unusedPointers.Add pointer
                )
            )

        member x.GetBrickCount level =
            if level < 0 || level >= levels then failwith "[SparseTexture] miplevel out of bounds"
            let levelSize = size / (1 <<< level)
            div3 levelSize brickSize

        member x.UploadBrick(level : int, slice : int, index : V3i, data : NativeTensor4<'a>) : IDisposable =
            if isDisposed then failwith "[SparseTexture] disposed"
            if level < 0 || level >= levels then failwith "[SparseTexture] miplevel out of bounds"
            if slice < 0 || slice >= slices then failwith "[SparseTexture] slice out of bounds"
            let cnt = x.GetBrickCount level
            if index.AnySmaller 0 || index.AnyGreaterOrEqual cnt then failwith "[SparseTexture] index out of bounds"


            if level >= back.SparseLevels then
                let levelSize = size / (1 <<< level)
                let sizeInBytes = int64 levelSize.X * int64 levelSize.Y * int64 levelSize.Z * int64 sizeof<'a>
                let tempBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit sizeInBytes

                tempBuffer.Memory.MappedTensor4<'a>(
                    V4i(levelSize.X, levelSize.Y, levelSize.Z, channels),
                    fun dst -> NativeTensor4.copy data dst
                )

                ReaderWriterLock.read updateLock (fun () ->
                    device.perform {
                        do! Command.Copy(tempBuffer, 0L, V2i.OO, back.[ImageAspect.Color, level, slice], V3i.Zero, levelSize)
                    }
                )

                device.Delete tempBuffer

                { new IDisposable with
                    member __.Dispose() = ()
                }

            else
                brickSem.Wait() |> ignore

                let levelSize = size / (1 <<< level)
                let brickSize = V3i(min levelSize.X brickSize.X, min levelSize.Y brickSize.Y, min levelSize.Z brickSize.Z)

                // alloc too much for small levels (all bricks have indentical memory sizes, even if smaller brickSize)
                let mem = memory.Alloc(align, brickSizeInBytes)
            
                let sizeInBytes = int64 brickSize.X * int64 brickSize.Y * int64 brickSize.Z * int64 sizeof<'a>
                let tempBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit sizeInBytes
                
                tempBuffer.Memory.MappedTensor4<'a>(
                    V4i(brickSize.X, brickSize.Y, brickSize.Z, channels),
                    fun dst -> NativeTensor4.copy data dst
                )

                let offset = index * brickSize
                let bind = { level = level; slice = slice; offset = offset; size = brickSize; pointer = Some mem }

                ReaderWriterLock.read updateLock (fun () ->
                    lock pendingBinds (fun () -> pendingBinds.[(bind.level, bind.slice, bind.offset)] <- bind)
                    back.Update [| bind |]

                    device.perform {
                        do! Command.Copy(tempBuffer, 0L, V2i.OO, back.[ImageAspect.Color, level, slice], offset, brickSize)
                    }
                )

                if flushAfterUpload then
                    invalidate()

                device.Delete tempBuffer

                { new IDisposable with
                    member __.Dispose() = x.Revoke bind
                }

        member x.Dispose() =
            if not isDisposed then
                isDisposed <- true
                cancel.Cancel()
                transact (fun () -> texture.MarkOutdated())
                device.Delete back
                device.Delete front
                memory.Clear()

        member x.Size = size
        member x.MipMapLevels = levels
        member x.SparseLevels = back.SparseLevels
        member x.Count = slices
        member x.Format = format
        member x.Texture = texture
        member x.BrickSize = brickSize
        member x.AllocatedMemory = memory.AllocatedMemory
        member x.UsedMemory = memory.UsedMemory
        member x.Invalidate () = invalidate ()
        

        interface IDisposable with
            member x.Dispose() = x.Dispose()
            
        interface ISparseTexture<'a> with
            
            [<CLIEvent>]
            member x.OnSwap = onSwap.Publish


            member x.Size = size
            member x.MipMapLevels = levels
            member x.SparseLevels = back.SparseLevels
            member x.Count = slices
            member x.Format = format
            member x.Texture = texture
            member x.BrickSize = brickSize

            member x.GetBrickCount level = x.GetBrickCount level
            member x.UploadBrick(level, slice, index, data) = x.UploadBrick(level, slice, index, data)
            member x.AllocatedMemory = memory.AllocatedMemory
            member x.UsedMemory = memory.UsedMemory

    type Brick<'a>(level : int, index : V3i, data : Tensor4<'a>) =
        let mutable witness : Option<IDisposable> = None

        member x.Level = level
        member x.Index = index
        member x.Data = data

        member x.Witness
            with get() = witness
            and set r = witness <- r

    let runTest() =
        let instance = new Instance(Version(1,0,0), Set.empty, Set.empty)
        let device = instance.Devices.[0].CreateDevice(Set.empty, Set.empty)

        let size = V3i(1024, 512, 256)
        let brickSize = V3i(128,128,128)
        let levels = 8

        let rand = RandomSystem()
        let img = new DoubleBufferedSparseImage<uint16>(device, size, levels, 1, TextureDimension.Texture3D, Col.Format.Gray, VkImageUsageFlags.None, brickSize, 2L <<< 30)

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
                let tempBuffer = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferSrcBit sizeInBytes
                

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




        let mutable modifications = 0
        let mutable totalErros = 0L
        let uploader() =
            while true do
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



        let renderer() =
            while true do
//                Mod.force img.Texture |> ignore
//                Log.start "frame %d" count
//                Log.line "modification: %A" modifications
//                Log.line "resident: %A" resident
//                Log.stop()
//                Thread.Sleep(16)

                let errors = Mod.force renderResult

                match errors with
                    | [] -> 
                        Log.start "frame %d" count
                        Log.line "modification: %A" modifications
                        Log.line "resident: %A" resident
                        if totalErros > 0L then Log.warn "totalErros %A" totalErros
                        Log.stop()
                    | _ ->
                        let errs = List.length errors |> int64
                        totalErros <- totalErros + errs
                        Log.warn "errors: %A" errs
                        ()

                Interlocked.Increment(&count) |> ignore


        let startThread (f : unit -> unit) =
            let t = new Thread(ThreadStart(f))
            t.IsBackground <- true
            t.Start()
            t

        let uploaders = 
            Array.init 2 (fun _ -> startThread uploader)

        let renderers = 
            Array.init 1 (fun _ -> startThread renderer)
            
        Console.ReadLine() |> ignore
//        while true do
//            Thread.Sleep 100





