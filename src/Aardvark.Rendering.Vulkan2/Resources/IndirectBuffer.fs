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
            { inherit Buffer(device, handle, ptr); Count = count }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IndirectBuffer =
    
    let create (b : IIndirectBuffer) (device : Device) =
        match b with
            | :? IndirectBuffer as b ->
                b

            | :? Aardvark.Base.IndirectBuffer as b ->
                let res = device.CreateBuffer(VkBufferUsageFlags.IndirectBufferBit ||| VkBufferUsageFlags.TransferDstBit, b.Buffer)
                
                IndirectBuffer(device, res.Handle, res.Memory, b.Count)
            
            | _ -> failf "bad indirect buffer: %A" b

    let delete (b : IndirectBuffer) (device : Device) =
        Buffer.delete b device

[<AbstractClass; Sealed; Extension>]
type ContextIndirectBufferExtensions private() =

    [<Extension>]
    static member inline CreateIndirectBuffer(device : Device, data : IIndirectBuffer) =
        device |> IndirectBuffer.create data

    [<Extension>]
    static member inline Delete(device : Device, buffer : IndirectBuffer) =
        device |> IndirectBuffer.delete buffer
