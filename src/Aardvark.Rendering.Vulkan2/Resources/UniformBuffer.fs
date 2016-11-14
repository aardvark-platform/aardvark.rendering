namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type UniformBufferLayout = { align : int; size : int; fieldOffsets : Map<string, int>; fieldTypes : Map<string, ShaderType> }

module UniformBufferLayoutStd140 =
    open System.Collections.Generic
    open FShade.SpirV

    let rec private sizeAndAlignOf (t : ShaderType) : int * int =
        match t with
            | Int(w,_) | Float(w) -> 
                // both the size and alignment are the size of the scalar
                // in basic machine types (e.g. sizeof<int>)
                let s = w / 8
                (s,s)

            | Bool -> 
                (4,4)

            | Vector(bt,3) -> 
                // both the size and alignment are 4 times the size
                // of the underlying scalar type.
                let s = sizeOf bt * 4
                (s,s)

            | Vector(bt,d) ->  
                // both the size and alignment are <d> times the size
                // of the underlying scalar type.
                let s = sizeOf bt * d
                (s,s)

            | Array(bt, len) -> 
                let physicalSize = sizeOf bt

                // the size of each element in the array will be the size
                // of the element type rounded up to a multiple of the size
                // of a vec4. This is also the array's alignment.
                // The array's size will be this rounded-up element's size
                // times the number of elements in the array.
                let size =
                    if physicalSize % 16 = 0 then physicalSize
                    else physicalSize + 16 - (physicalSize % 16)

                (size * len, size)

            | Matrix(colType, cols) ->
                // same layout as an array of N vectors each with 
                // R components, where N is the total number of columns
                // present.
                sizeAndAlignOf (Array(colType, cols))

            | Ptr(_,t) -> 
                sizeAndAlignOf t

            | Image(sampledType, dim,depth,arr, ms, sam, fmt) -> 
                failf "cannot determine size for image type"

            | Sampler -> 
                failf "cannot determine size for sampler type"

            | Struct(name,fields) -> 
                let layout = structLayout fields
                layout.align, layout.size

            | Void -> 
                failf "cannot determine size for void type"

            | Function _ ->
                failf "cannot use function in UniformBuffer"

            | SampledImage _ ->
                failf "cannot use SampledImage in UniformBuffer"
                

    and sizeOf (t : ShaderType) =
        t |> sizeAndAlignOf |> fst

    and alignOf (t : ShaderType) =
        t |> sizeAndAlignOf |> snd

    and structLayout (fields : list<ShaderType * string * list<SpirV.Decoration * uint32[]>>) : UniformBufferLayout =
        let mutable currentOffset = 0
        let mutable offsets : Map<string, int> = Map.empty
        let mutable types : Map<string, ShaderType> = Map.empty
        let mutable biggestFieldSize = 0
        let mutable biggestFieldAlign = 0

        for (t,n,dec) in fields do
            let (size,align) = sizeAndAlignOf t

            // align the field offset
            if currentOffset % align <> 0 then
                currentOffset <- currentOffset + align - (currentOffset % align)

            // keep track of the biggest member
            if size > biggestFieldSize then
                biggestFieldSize <- size
                biggestFieldAlign <- align

            // store the member's offset
            offsets <- Map.add n currentOffset offsets
            types <- Map.add n t types
            currentOffset <- currentOffset + size
            ()

        // structure alignment will be the alignment for
        // the biggest structure member, according to the previous
        // rules, rounded up to a multiple of the size of a vec4.
        // each structure will start on this alignment, and its size will
        // be the space needed by its members, according to the previous
        // rules, rounded up to a multiple of the structure alignment.
        let structAlign =
            if biggestFieldAlign % 16 = 0 then biggestFieldAlign
            else biggestFieldAlign + 16 - (biggestFieldAlign % 16)

        let structSize =
            if currentOffset % structAlign = 0 then currentOffset
            else currentOffset + structAlign - (currentOffset % structAlign)

        { align = structAlign; size = structSize; fieldOffsets = offsets; fieldTypes = types }

type UnmanagedStruct(ptr : nativeint, size : int) =
    let mutable ptr = ptr
    let mutable size = size

    member x.Pointer = ptr
    member x.Size = size

    member x.WriteUnsafe(offset : int, value : 'a) =
        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.write ptr value

    member x.Write(offset : int, value : 'a) =
        if ptr = 0n then
            raise <| ObjectDisposedException("UnmanagedStruct")

        if offset < 0 || offset + sizeof<'a> > size then
            raise <| IndexOutOfRangeException(sprintf "%d < 0 || %d > %d" offset (offset + sizeof<'a>) size)

        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.write ptr value

    member x.Read(offset : int) : 'a =
        if ptr = 0n then
            raise <| ObjectDisposedException("UnmanagedStruct")

        if offset < 0 || offset + sizeof<'a> > size then
            raise <| IndexOutOfRangeException()

        let ptr = (ptr + nativeint offset) |> NativePtr.ofNativeInt<'a>
        NativePtr.read ptr

    member x.Dispose() =
        if ptr <> 0n then
            Marshal.FreeHGlobal ptr
            ptr <- 0n
            size <- 0

    interface IDisposable with
        member x.Dispose() = x.Dispose()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UnmanagedStruct =
    
    let Null = new UnmanagedStruct(0n, 0)

    let alloc (size : int) =
        let ptr = Marshal.AllocHGlobal size
        new UnmanagedStruct(ptr, size)

    let inline free (s : UnmanagedStruct) =
        s.Dispose()
    
    let inline write (s : UnmanagedStruct) (offset : int) (value : 'a) =
        s.Write(offset, value)

    let inline writeUnsafe (s : UnmanagedStruct) (offset : int) (value : 'a) =
        s.WriteUnsafe(offset, value)


    let inline read (s : UnmanagedStruct) (offset : int) =
        s.Read(offset)




type UniformBuffer =
    class
        inherit Buffer
        val mutable public Storage : UnmanagedStruct
        val mutable public Layout : UniformBufferLayout

        new(device : Device, handle : VkBuffer, mem : DevicePtr, storage : UnmanagedStruct, layout : UniformBufferLayout) = 
            { inherit Buffer(device, handle, mem); Storage = storage; Layout = layout }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module UniformBuffer =
    let create (p : ShaderParameter) (device : Device) =
        let fields = 
            match p.paramType with
                | ShaderType.Ptr(_,ShaderType.Struct(name, fields)) -> fields
                | ShaderType.Struct(name, fields) -> fields
                | _ -> failf "cannot create uniform-buffer for non-buffer parameter: %A" p

        let layout = UniformBufferLayoutStd140.structLayout fields
        let storage = UnmanagedStruct.alloc layout.size

        let align = device.MinUniformBufferOffsetAlignment
        let alignedSize = Alignment.next align (int64 layout.size)

        let mem = device.DeviceMemory.Alloc(align, alignedSize)
        let buffer = device.CreateBuffer(VkBufferUsageFlags.UniformBufferBit ||| VkBufferUsageFlags.TransferDstBit, mem)

        UniformBuffer(device, buffer.Handle, buffer.Memory, storage, layout)

    let upload (b : UniformBuffer) (device : Device) =
        use t = device.ResourceToken
        let cmd =
            { new Command<unit>() with
                member x.Enqueue cmd = VkRaw.vkCmdUpdateBuffer(cmd.Handle, b.Handle, 0UL, uint64 b.Storage.Size, b.Storage.Pointer)
                member x.Dispose() = ()
            }
        t.Enqueue(cmd)

    let delete (b : UniformBuffer) (device : Device) =
        device.Delete(b :> Buffer)
        UnmanagedStruct.free b.Storage
        b.Storage <- UnmanagedStruct.Null


[<AbstractClass; Sealed; Extension>]
type ContextUniformBufferExtensions private() =
    [<Extension>]
    static member inline CreateUniformBuffer(this : Device, parameter : ShaderParameter) =
        this |> UniformBuffer.create parameter

    [<Extension>]
    static member inline Delete(this : Device, b : UniformBuffer) =
        this |> UniformBuffer.delete b       