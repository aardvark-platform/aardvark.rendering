namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Rendering.Vulkan


#nowarn "9"
#nowarn "51"

type IndirectBuffer =
    class
        inherit Buffer
        val mutable public Count : int

        interface IIndirectBuffer with
            member x.Buffer = x :> IBuffer
            member x.Count = x.Count

        new(device : Device, handle : VkBuffer, ptr : DevicePtr, count : int) = 
            { inherit Buffer(device, handle, ptr, int64 count * 20L); Count = count }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =
    open Microsoft.FSharp.NativeInterop

    let private flags = VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit

    let inline private copyIndexed (src : nativeptr<DrawCallInfo>) (dst : nativeptr<DrawCallInfo>) (cnt : int) =
        let mutable src = src
        let mutable dst = dst
        for i in 1 .. cnt do
            let mutable c = NativePtr.read src
            Fun.Swap(&c.BaseVertex, &c.FirstInstance)
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

    let create (indexed : bool) (b : IIndirectBuffer) (device : Device) =
        match b with
            | :? IndirectBuffer as b ->
                b.AddReference()
                b

            | :? Aardvark.Base.IndirectBuffer as b ->
                let buffer = 
                    match b.Buffer with
                        | :? ArrayBuffer as ab ->
                            if ab.Data.Length <> 0 then
                                let size = nativeint ab.Data.LongLength * nativeint (Marshal.SizeOf ab.ElementType)
                                let gc = GCHandle.Alloc(ab.Data, GCHandleType.Pinned)
                                try device |> Buffer.ofWriter flags size (fun dst -> copy indexed (gc.AddrOfPinnedObject()) dst ab.Data.Length)
                                finally gc.Free()
                            else
                                Buffer.empty flags device

                        | :? INativeBuffer as nb ->
                            if nb.SizeInBytes <> 0 then
                                let size = nativeint nb.SizeInBytes
                                let count = nb.SizeInBytes / sizeof<DrawCallInfo>
                                nb.Use(fun src ->
                                    device |> Buffer.ofWriter flags size (fun dst -> copy indexed src dst count)
                                )
                            else
                                Buffer.empty flags device

                        | _ ->
                            failf "unsupported indirect buffer type %A" b.Buffer

                IndirectBuffer(device, buffer.Handle, buffer.Memory, b.Count)
            
            | _ -> failf "bad indirect buffer: %A" b

    let delete (b : IndirectBuffer) (device : Device) =
        Buffer.delete b device

[<AbstractClass; Sealed; Extension>]
type ContextIndirectBufferExtensions private() =

    [<Extension>]
    static member inline CreateIndirectBuffer(device : Device, indexed : bool, data : IIndirectBuffer) =
        device |> IndirectBuffer.create indexed data

    [<Extension>]
    static member inline Delete(device : Device, buffer : IndirectBuffer) =
        device |> IndirectBuffer.delete buffer
