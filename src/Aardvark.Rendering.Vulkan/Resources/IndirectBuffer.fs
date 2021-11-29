namespace Aardvark.Rendering.Vulkan

open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan


#nowarn "9"
// #nowarn "51"

type VkIndirectBuffer =
    class
        inherit Buffer
        val public Count : int

        //interface IIndirectBuffer with
        //    member x.Buffer = x :> IBuffer
        //    member x.Count = x.Count

        new(device : Device, handle : VkBuffer, ptr : DevicePtr, count : int) = 
            { inherit Buffer(device, handle, ptr, int64 count * 20L, VkBufferUsageFlags.IndirectBufferBit); Count = count }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =
    open Microsoft.FSharp.NativeInterop

    let private flags = VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit

    let inline private copyIndexed (src : nativeptr<DrawCallInfo>) (dst : nativeptr<DrawCallInfo>) (cnt : int) =
        let mutable src = src
        let mutable dst = dst
        for i in 1 .. cnt do
            let c =
                let call = NativePtr.read src
                { call with BaseVertex = call.FirstInstance; FirstInstance = call.BaseVertex }
            NativePtr.write dst c
            src <- NativePtr.add src 1
            dst <- NativePtr.add dst 1

    let inline private copyNonIndexed (src : nativeptr<DrawCallInfo>) (dst : nativeptr<DrawCallInfo>) (cnt : int) =
        Marshal.Copy(NativePtr.toNativeInt src, NativePtr.toNativeInt dst, sizeof<DrawCallInfo> * cnt)


    let private copy (indexed : bool)  (src : nativeint) (dst : nativeint) (cnt : int) =
        let src = NativePtr.ofNativeInt src
        let dst = NativePtr.ofNativeInt dst
        if indexed then copyIndexed src dst cnt
        else copyNonIndexed src dst cnt

    let rec create (indexed : bool) (b : IndirectBuffer) (device : Device) =
        let buffer = 
            match b.Buffer with
            | :? ArrayBuffer as ab ->
                if ab.Data.Length <> 0 then
                    let size = nativeint ab.Data.LongLength * nativeint (Marshal.SizeOf ab.ElementType)
                    let gc = GCHandle.Alloc(ab.Data, GCHandleType.Pinned)
                    try device.DeviceMemory |> Buffer.ofWriter flags size (fun dst -> copy indexed (gc.AddrOfPinnedObject()) dst ab.Data.Length)
                    finally gc.Free()
                else
                    Buffer.empty flags device

            | :? INativeBuffer as nb ->
                if nb.SizeInBytes <> 0 then
                    let size = nativeint nb.SizeInBytes
                    let count = nb.SizeInBytes / sizeof<DrawCallInfo>
                    nb.Use(fun src ->
                        device.DeviceMemory |> Buffer.ofWriter flags size (fun dst -> copy indexed src dst count)
                    )
                else
                    Buffer.empty flags device
            | :? Buffer as bb ->
                bb
            | _ ->
                failf "unsupported indirect buffer type %A" b.Buffer

        new VkIndirectBuffer(device, buffer.Handle, buffer.Memory, b.Count)

[<AbstractClass; Sealed; Extension>]
type ContextIndirectBufferExtensions private() =

    [<Extension>]
    static member inline CreateIndirectBuffer(device : Device, indexed : bool, data : IndirectBuffer) =
        device |> IndirectBuffer.create indexed data
