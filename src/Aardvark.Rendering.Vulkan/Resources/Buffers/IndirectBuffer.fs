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

    let create (indexed : bool) (data : Aardvark.Rendering.IndirectBuffer) (device : Device) =
        // VUID-vkCmdDrawIndexedIndirect-offset-02710
        if data.Offset % 4UL <> 0UL then
            failf $"Offset of indirect buffer must be a multiple of 4 (Offset = {data.Offset})"

        // VUID-vkCmdDrawIndexedIndirect-drawCount-00528
        if data.Count > 1 && (data.Stride < sizeof<DrawCallInfo> || data.Stride % 4 <> 0) then
            failf $"Stride of indirect buffer must not be smaller than {sizeof<DrawCallInfo>} and must be a multiple of 4 (Stride = {data.Stride})"

        let swap = (indexed <> data.Indexed)

        let buffer =
            match data.Buffer with
            | :? INativeBuffer as nb ->
                if nb.SizeInBytes <> 0UL then
                    let buffer = Buffer.create flags nb.SizeInBytes device.DeviceMemory

                    nb.Use (fun src ->
                        Buffer.write buffer (fun dst ->
                            if swap then
                                let offset = nativeint data.Offset
                                let stride = nativeint data.Stride
                                DrawCallInfo.ToggleIndexedCopy(src + offset, dst + offset, stride, data.Count)
                            else
                                Buffer.MemoryCopy(src, dst, nb.SizeInBytes, nb.SizeInBytes)
                        )
                    )

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

            | _ ->
                failf "Unsupported indirect buffer type %A" data.Buffer

        new IndirectBuffer(buffer, data.Count, data.Offset, data.Stride)

[<AbstractClass; Sealed; Extension>]
type ContextIndirectBufferExtensions private() =

    [<Extension>]
    static member inline CreateIndirectBuffer(device : Device, indexed : bool, data : Aardvark.Rendering.IndirectBuffer) =
        device |> IndirectBuffer.create indexed data
