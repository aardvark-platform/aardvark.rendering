namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

#nowarn "9"

type IndirectBuffer =
    class
        inherit BufferDecorator
        val public Count  : int
        val public Offset : uint64
        val public Stride : int

        new(parent : Buffer, count : int, offset : uint64, stride : int) =
            { inherit BufferDecorator(parent);
              Count  = count; Offset = offset; Stride = stride }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =

    let private flags = VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit

    let private copyNative (swap : bool) (count : int) (offset : uint64) (stride : int) (src : INativeBuffer) (dst : Buffer) =
        src.Use (fun pSrc ->
            Buffer.write dst (fun pDst ->
                if swap then
                    let offset = nativeint offset
                    let stride = nativeint stride
                    DrawCallInfo.ToggleIndexedCopy(pSrc + offset, pDst + offset, stride, count)
                else
                    Buffer.MemoryCopy(pSrc, pDst, src.SizeInBytes, src.SizeInBytes)
            )
        )

    let tryUpdate (indexed : bool) (data : Aardvark.Rendering.IndirectBuffer) (buffer : IndirectBuffer) =
        let swap = (indexed <> data.Indexed)

        let rec tryUpdate (dataBuffer: IBuffer) =
            match dataBuffer with
            | :? INativeBuffer as nb ->
                if buffer.Size = nb.SizeInBytes then
                    copyNative swap data.Count data.Offset data.Stride nb buffer
                    true
                else
                    false

            | :? Buffer as b ->
                buffer.Handle = b.Handle && not swap

            | :? IBufferRange as bv when bv != bv.Buffer ->
                tryUpdate bv.Buffer

            | _ ->
                false

        if buffer.Count = data.Count && buffer.Offset = data.Offset && buffer.Stride = data.Stride then
            tryUpdate data.Buffer
        else
            false

    let create (indexed : bool) (data : Aardvark.Rendering.IndirectBuffer) (device : Device) =
        // VUID-vkCmdDrawIndexedIndirect-offset-02710
        if data.Offset % 4UL <> 0UL then
            failf $"Offset of indirect buffer must be a multiple of 4 (Offset = {data.Offset})"

        // VUID-vkCmdDrawIndexedIndirect-drawCount-00528
        if data.Count > 1 && (data.Stride < sizeof<DrawCallInfo> || data.Stride % 4 <> 0) then
            failf $"Stride of indirect buffer must not be smaller than {sizeof<DrawCallInfo>} and must be a multiple of 4 (Stride = {data.Stride})"

        let swap = (indexed <> data.Indexed)

        let rec createBuffer (buffer: IBuffer) =
            match buffer with
            | :? INativeBuffer as nb ->
                if nb.SizeInBytes <> 0UL then
                    let buffer = Buffer.create flags nb.SizeInBytes device.DeviceMemory
                    copyNative swap data.Count data.Offset data.Stride nb buffer
                    buffer
                else
                    Buffer.empty false flags 0UL device.DeviceMemory

            | :? Buffer as b ->
                if swap then
                    if data.Indexed then
                        failf "Indirect buffer contains indexed data but expected non-indexed data"
                    else
                        failf "Indirect buffer contains non-indexed data but expected indexed data"

                b.AddReference()
                b

            | :? IBufferRange as bv when bv != bv.Buffer ->
                createBuffer bv.Buffer

            | _ when obj.ReferenceEquals(buffer, null) ->
                failf $"Indirect buffer data is null"

            | _ ->
                failf $"unsupported indirect buffer type: {buffer.GetType()}"

        let buffer = createBuffer data.Buffer
        new IndirectBuffer(buffer, data.Count, data.Offset, data.Stride)

[<AbstractClass; Sealed; Extension>]
type ContextIndirectBufferExtensions private() =

    [<Extension>]
    static member inline CreateIndirectBuffer(device : Device, indexed : bool, data : Aardvark.Rendering.IndirectBuffer) =
        device |> IndirectBuffer.create indexed data
