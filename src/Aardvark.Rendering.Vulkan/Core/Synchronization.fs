namespace Aardvark.Rendering.Vulkan

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type FenceStatus =
    | Ready = 1
    | NotReady = 2
    | Error = 3

type Fence(device : Device, handle : VkFence) =
    inherit Resource(device)

    member x.Device = device
    member x.Handle = handle

    override x.Release() =
        VkRaw.vkDestroyFence(device.Handle, handle, NativePtr.zero)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Fence =
    open System.Linq

    let private infinity = ~~~0UL 

    [<CompiledName("Create")>]
    let create (device : Device) =
        let mutable info =
            VkFenceCreateInfo(
                VkStructureType.FenceCreateInfo, 0n,
                VkFenceCreateFlags.None
            )

        let mutable handle = VkFence.Null

        VkRaw.vkCreateFence(device.Handle, &&info, NativePtr.zero, &&handle) 
            |> check "vkCreateFence"

        new Fence(device, handle) 

    [<CompiledName("WaitAll")>]
    let waitAll (fences : #seq<Fence>) =
        let arr = fences.ToArray()

        if arr.Length > 0 then
            let device = arr.[0].Device
            NativePtr.stackallocWith arr.Length (fun pFences ->
                for i in 0..arr.Length-1 do
                    NativePtr.set pFences i arr.[i].Handle

                VkRaw.vkWaitForFences(device.Handle, uint32 arr.Length, pFences, 1u, infinity)
                    |> check "vkWaitForFences"
            )

    [<CompiledName("WaitAny")>]
    let waitAny (fences : #seq<Fence>) =
        let arr = fences.ToArray()

        if arr.Length > 0 then
            let device = arr.[0].Device
            NativePtr.stackallocWith arr.Length (fun pFences ->
                for i in 0..arr.Length-1 do
                    NativePtr.set pFences i arr.[i].Handle

                VkRaw.vkWaitForFences(device.Handle, uint32 arr.Length, pFences, 0u, infinity)
                    |> check "vkWaitForFences"
            )

    [<CompiledName("Wait")>]
    let wait (fence : Fence) =
        let mutable handle = fence.Handle
        VkRaw.vkWaitForFences(fence.Device.Handle, 1u, &&handle, 0u, infinity)
             |> check "vkWaitForFences"

    [<CompiledName("Reset")>]
    let reset (fence : Fence) =
        let mutable handle = fence.Handle
        VkRaw.vkResetFences(fence.Device.Handle, 1u, &&handle)
            |> check "vkResetFences"

    [<CompiledName("Status")>]
    let status (fence : Fence) =
        let res = VkRaw.vkGetFenceStatus(fence.Device.Handle, fence.Handle)

        match res with
            | VkResult.VkNotReady -> FenceStatus.NotReady
            | VkResult.VkSuccess -> FenceStatus.Ready
            | _ -> FenceStatus.Error



type Event(device : Device, handle : VkEvent) =
    inherit Resource(device)

    override x.Release() =
        VkRaw.vkDestroyEvent(device.Handle, handle, NativePtr.zero)

    member x.Device = device
    member x.Handle = handle

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Event =

    [<CompiledName("Create")>]
    let create (device : Device) =
        let mutable info =
            VkEventCreateInfo(
                VkStructureType.EventCreateInfo, 0n,
                VkEventCreateFlags.MinValue
            )

        let mutable handle = VkEvent.Null

        VkRaw.vkCreateEvent(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "vkCreateEvent" 

        new Event(device, handle)

    [<CompiledName("Set")>]
    let set (e : Event) =
        VkRaw.vkSetEvent(e.Device.Handle, e.Handle)
            |> check "vkSetEvent"

    [<CompiledName("Reset")>]
    let reset (e : Event) =
        VkRaw.vkResetEvent(e.Device.Handle, e.Handle)
            |> check "vkResetEvent"

    [<CompiledName("IsSet")>]
    let isSet (e : Event) =
        let status = 
            VkRaw.vkGetEventStatus(e.Device.Handle, e.Handle)
           
        match status with
            | VkResult.VkEventSet -> true
            | _ -> false

    module Commands =
        [<CompiledName("Set")>] 
        let set (e : Event) =
            Command.custom (fun s ->
                VkRaw.vkCmdSetEvent(
                    s.buffer.Handle,
                    e.Handle,
                    VkPipelineStageFlags.TopOfPipeBit
                )

                s
            )
            
        [<CompiledName("Reset")>] 
        let reset (e : Event) =
            Command.custom (fun s ->
                VkRaw.vkCmdResetEvent(
                    s.buffer.Handle,
                    e.Handle,
                    VkPipelineStageFlags.TopOfPipeBit
                )

                s
            )
            
        [<CompiledName("WaitAll")>] 
        let waitAll (e : #seq<Event>) =
            let e = Seq.toArray e
            Command.custom (fun s ->
                if e.Length > 0 then
                    let pEvents = NativePtr.stackalloc e.Length
                    for i in 0..e.Length-1 do
                        NativePtr.set pEvents i e.[i].Handle

                    VkRaw.vkCmdWaitEvents(
                        s.buffer.Handle,
                        uint32 e.Length,
                        pEvents, 
                        VkPipelineStageFlags.TopOfPipeBit,
                        VkPipelineStageFlags.TopOfPipeBit,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero,
                        0u, NativePtr.zero
                    )

                s
            )
            
        [<CompiledName("Wait")>] 
        let wait (e : Event) =
            Command.custom (fun s ->
                let mutable handle = e.Handle 
                VkRaw.vkCmdWaitEvents(
                    s.buffer.Handle,
                    1u,
                    &&handle, 
                    VkPipelineStageFlags.TopOfPipeBit,
                    VkPipelineStageFlags.TopOfPipeBit,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero,
                    0u, NativePtr.zero
                )

                s
            )



type Semaphore(device : Device, handle : VkSemaphore) =
    inherit Resource(device)

    override x.Release() =
        VkRaw.vkDestroySemaphore(device.Handle, handle, NativePtr.zero)

    member x.Device = device
    member x.Handle = handle

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Semaphore =

    [<CompiledName("Create")>]
    let create (device : Device) =
        let mutable info = 
            VkSemaphoreCreateInfo(
                VkStructureType.SemaphoreCreateInfo, 0n,
                VkSemaphoreCreateFlags.MinValue
            )

        let mutable handle = VkSemaphore.Null 


        VkRaw.vkCreateSemaphore(device.Handle, &&info, NativePtr.zero, &&handle)
            |> check "vkCreateSemaphore"

        new Semaphore(device, handle)



[<AbstractClass; Sealed; Extension>]
type SynchronizationExtensions private() =

    [<Extension>]
    static member CreateFence(this : Device) = Fence.create this
    
    [<Extension>]
    static member Wait(this : Fence) = Fence.wait this
    
    [<Extension>]
    static member WaitAll(this : #seq<Fence>) = Fence.waitAll this

    [<Extension>]
    static member WaitAny(this : #seq<Fence>) = Fence.waitAny this

    [<Extension>]
    static member Reset(this : Fence) = Fence.reset this

    [<Extension>]
    static member GetStatus(this : Fence) = Fence.status this




    [<Extension>]
    static member CreateEvent(this : Device) = Event.create this
    
    [<Extension>]
    static member Set(this : Event) = Event.set this

    [<Extension>]
    static member Reset(this : Event) = Event.reset this

    [<Extension>]
    static member IsSet(this : Event) = Event.isSet this

    [<Extension>]
    static member WaitCmd(this : Event) = Event.Commands.wait this

    [<Extension>]
    static member WaitAllCmd(this : #seq<Event>) = Event.Commands.waitAll this

    [<Extension>]
    static member SetCmd(this : Event) = Event.Commands.set this

    [<Extension>]
    static member ResetCmd(this : Event) = Event.Commands.reset this
    

    [<Extension>]
    static member CreateSemaphore(this : Device) = Semaphore.create this
    