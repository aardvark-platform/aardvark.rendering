namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan

#nowarn "9"

type IndirectBuffer =
    class
        inherit BufferDecorator
        val public Count  : int

        new(parent : Buffer, count : int) =
            { inherit BufferDecorator(parent);
              Count  = count }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =
    open Microsoft.FSharp.NativeInterop

    let private flags = VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit

    let inline private copySwap (src : nativeptr<DrawCallInfo>) (dst : nativeptr<DrawCallInfo>) (cnt : int) =
        let mutable src = src
        let mutable dst = dst
        for i in 1 .. cnt do
            let mutable c = NativePtr.read src
            DrawCallInfo.ToggleIndexed(&c)
            NativePtr.write dst c
            src <- NativePtr.add src 1
            dst <- NativePtr.add dst 1

    let inline private copyDirect (src : nativeptr<DrawCallInfo>) (dst : nativeptr<DrawCallInfo>) (cnt : int) =
        let src = NativePtr.toNativeInt src
        let dst = NativePtr.toNativeInt dst
        Marshal.Copy(src, dst, sizeof<DrawCallInfo> * cnt)


    let private copy (swap : bool) (src : nativeint) (dst : nativeint) (cnt : int) =
        let src = NativePtr.ofNativeInt src
        let dst = NativePtr.ofNativeInt dst

        if swap then
            copySwap src dst cnt
        else
            copyDirect src dst cnt

    let create (indexed : bool) (b : Aardvark.Rendering.IndirectBuffer) (device : Device) =
        if b.Stride <> sizeof<DrawCallInfo> then
            failf "Stride of indirect buffer must be %d (is %d)" sizeof<DrawCallInfo> b.Stride

        let swap = (indexed <> b.Indexed)

        let buffer =
            match b.Buffer with
            | :? ArrayBuffer as ab ->
                if ab.Data.Length <> 0 then
                    if ab.ElementType <> typeof<DrawCallInfo> then
                        failf "Element type of array for indirect buffer must be DrawCallInfo (is %A)" ab.ElementType

                    let size = nativeint ab.Data.LongLength * nativeint sizeof<DrawCallInfo>

                    pinned ab.Data (fun src ->
                        device.DeviceMemory |> Buffer.ofWriter flags size (fun dst ->
                            copy swap src dst ab.Data.Length
                        )
                    )
                else
                    Buffer.empty flags device.DeviceMemory

            | :? INativeBuffer as nb ->
                if nb.SizeInBytes <> 0n then
                    let size = nb.SizeInBytes
                    let count = int (nb.SizeInBytes / nativeint sizeof<DrawCallInfo>)
                    nb.Use(fun src ->
                        device.DeviceMemory |> Buffer.ofWriter flags size (fun dst -> copy swap src dst count)
                    )
                else
                    Buffer.empty flags device.DeviceMemory

            | :? Buffer as bb ->
                if swap then
                    if b.Indexed then
                        failf "Indirect buffer contains indexed data but expected non-indexed data"
                    else
                        failf "Indirect buffer contains non-indexed data but expected indexed data"

                bb.AddReference()
                bb

            | _ ->
                failf "Unsupported indirect buffer type %A" b.Buffer

        new IndirectBuffer(buffer, b.Count)

[<AbstractClass; Sealed; Extension>]
type ContextIndirectBufferExtensions private() =

    [<Extension>]
    static member inline CreateIndirectBuffer(device : Device, indexed : bool, data : Aardvark.Rendering.IndirectBuffer) =
        device |> IndirectBuffer.create indexed data
