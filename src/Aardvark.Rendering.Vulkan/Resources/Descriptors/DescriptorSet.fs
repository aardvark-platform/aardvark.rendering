namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.Raytracing
open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open KHRAccelerationStructure
open Vulkan11

#nowarn "9"

type Descriptor =
    | UniformBuffer         of slot: int * buffer: UniformBuffer
    | StorageBuffer         of slot: int * element: int * buffer: Buffer * offset: uint64 * size: uint64
    | CombinedImageSampler  of slot: int * element: int * view: ImageView * sampler: Sampler * layout: VkImageLayout
    | StorageImage          of slot: int * view: ImageView
    | AccelerationStructure of slot: int * accel: AccelerationStructure

type DescriptorSet =
    class
        static let mutable liveCount = 0

        inherit Resource<VkDescriptorSet>
        val public Pool : DescriptorPool
        val public Layout : DescriptorSetLayout
        val private onDestroyed : Event<unit>

        // Instrumentation: net count of live (allocated but not yet freed) descriptor
        // sets across the whole device (see DescriptorSetInstrumentation below). Used
        // by leak-detection probes to confirm the per-frame compute/render path does
        // not accumulate descriptor sets.
        static member LiveCount = liveCount

        [<CLIEvent>]
        member x.OnDestroyed = x.onDestroyed.Publish

        override x.Destroy() =
            if x.Handle.IsValid then
                lock x.Pool (fun _ ->
                    x.Handle <- VkDescriptorSet.Null
                    x.Pool.FreeSet()
                )
                System.Threading.Interlocked.Decrement &liveCount |> ignore
                x.onDestroyed.Trigger()

        new(device : Device, pool : DescriptorPool, layout : DescriptorSetLayout, handle : VkDescriptorSet) =
            System.Threading.Interlocked.Increment &liveCount |> ignore
            { inherit Resource<_>(device, handle); Pool = pool; Layout = layout; onDestroyed = Event<unit>() }
    end


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DescriptorSet =

    let tryAlloc (layout : DescriptorSetLayout) (variableCount : int) (pool : DescriptorPool) =
        lock pool (fun () ->
            if pool.TryAllocateSet <| layout.GetDescriptorCounts variableCount then
                native {
                    let! pLayoutHandle = layout.Handle

                    // VARIABLE_DESCRIPTOR_COUNT: allocate only the per-set count for
                    // the layout's variable binding (an unbounded sampler array)
                    // instead of its full (capped) capacity.
                    let! pCounts = [| uint32 variableCount |]
                    let! pVarInfo = Vulkan12.VkDescriptorSetVariableDescriptorCountAllocateInfo(1u, pCounts)
                    let pNext = if layout.VariableCountBinding.IsSome then NativePtr.toNativeInt pVarInfo else 0n

                    let! pInfo =
                        VkDescriptorSetAllocateInfo(
                            pNext,
                            pool.Handle,
                            1u,
                            pLayoutHandle
                        )

                    let! pHandle = VkDescriptorSet.Null
                    let res = VkRaw.vkAllocateDescriptorSets(pool.Device.Handle, pInfo, pHandle)

                    if res = VkResult.ErrorFragmentedPool || res = VkResult.ErrorOutOfPoolMemory then
                        pool.FreeSet()
                        return None
                    else
                        res |> check "could not allocate DescriptorSet"
                        return Some (new DescriptorSet(pool.Device, pool, layout, !!pHandle))
                }
            else
                None
        )

    let alloc (layout : DescriptorSetLayout) (variableCount : int) (pool : DescriptorPool) =
        match tryAlloc layout variableCount pool with
            | Some d -> d
            | None -> failf "cannot allocate DescriptorSet (out of slots)"

    let update (descriptors : array<Descriptor>) (set : DescriptorSet) (pool : DescriptorPool) =
        let device = pool.Device

        let mutable imageInfos =
            let cnt = descriptors |> Array.sumBy (function CombinedImageSampler _ | StorageImage _ -> 1 | _ -> 0)
            NativePtr.stackalloc cnt

        let mutable bufferInfos =
            let cnt = descriptors |> Array.sumBy (function StorageBuffer _ | UniformBuffer _ -> 1 | _ -> 0)
            NativePtr.stackalloc cnt

        let accelCount =
            descriptors |> Array.sumBy (function AccelerationStructure _ -> 1 | _ -> 0)

        let mutable accelWrites  = NativePtr.stackalloc accelCount
        let mutable accelHandles = NativePtr.stackalloc accelCount

        let writes =
            descriptors
            |> Array.map (fun desc ->
                match desc with
                | StorageBuffer (binding, element, buffer, offset, size) ->
                    let info =
                        VkDescriptorBufferInfo(
                            buffer.Handle,
                            offset,
                            if size > 0UL then size else VkWholeSize
                        )

                    if not <| buffer.Usage.HasFlag VkBufferUsageFlags.StorageBufferBit then
                        failf $"cannot use buffer as storage buffer descriptor (usage is {buffer.Usage})"

                    NativePtr.write bufferInfos info
                    let ptr = bufferInfos
                    bufferInfos <- NativePtr.step 1 bufferInfos

                    VkWriteDescriptorSet(
                        set.Handle,
                        uint32 binding,
                        uint32 element, 1u, VkDescriptorType.StorageBuffer,
                        NativePtr.zero,
                        ptr,
                        NativePtr.zero
                    )

                | UniformBuffer (binding, buffer) ->
                    let info =
                        VkDescriptorBufferInfo(
                            buffer.Handle,
                            0UL,
                            if buffer.Storage.Size > 0 then uint64 buffer.Storage.Size else VkWholeSize
                        )

                    if not <| buffer.Usage.HasFlag VkBufferUsageFlags.UniformBufferBit then
                        failf $"cannot use buffer as uniform buffer descriptor (usage is {buffer.Usage})"

                    NativePtr.write bufferInfos info
                    let ptr = bufferInfos
                    bufferInfos <- NativePtr.step 1 bufferInfos

                    VkWriteDescriptorSet(
                        set.Handle,
                        uint32 binding,
                        0u, 1u, VkDescriptorType.UniformBuffer,
                        NativePtr.zero,
                        ptr,
                        NativePtr.zero
                    )

                | CombinedImageSampler (binding, element, view, sampler, layout) ->
                    let info =
                        VkDescriptorImageInfo(
                            sampler.Handle,
                            view.Handle,
                            layout
                        )

                    if not <| view.Image.Usage.HasFlag VkImageUsageFlags.SampledBit then
                        failf $"cannot use image as combined image sampler descriptor (usage is {view.Image.Usage})"

                    NativePtr.write imageInfos info
                    let ptr = imageInfos
                    imageInfos <- NativePtr.step 1 imageInfos

                    VkWriteDescriptorSet(
                        set.Handle,
                        uint32 binding,
                        uint32 element, 1u, VkDescriptorType.CombinedImageSampler,
                        ptr,
                        NativePtr.zero,
                        NativePtr.zero
                    )

                | StorageImage(binding, view) ->
                    let info =
                        VkDescriptorImageInfo(
                            VkSampler.Null,
                            view.Handle,
                            VkImageLayout.General
                        )

                    if not <| view.Image.Usage.HasFlag VkImageUsageFlags.StorageBit then
                        failf $"cannot use image as storage image descriptor (usage is {view.Image.Usage})"

                    NativePtr.write imageInfos info
                    let ptr = imageInfos
                    imageInfos <- NativePtr.step 1 imageInfos

                    VkWriteDescriptorSet(
                        set.Handle,
                        uint32 binding,
                        0u, 1u, VkDescriptorType.StorageImage,
                        ptr,
                        NativePtr.zero,
                        NativePtr.zero
                    )

                | AccelerationStructure(binding, accel) ->
                    NativePtr.write accelHandles accel.Handle
                    let pHandle = accelHandles
                    accelHandles <- NativePtr.step 1 accelHandles

                    let writeAccel =
                        VkWriteDescriptorSetAccelerationStructureKHR(1u, pHandle)

                    NativePtr.write accelWrites writeAccel
                    let ptr = accelWrites
                    accelWrites <- NativePtr.step 1 accelWrites

                    VkWriteDescriptorSet(
                        NativePtr.toNativeInt ptr,
                        set.Handle,
                        uint32 binding,
                        0u, 1u, VkDescriptorType.AccelerationStructureKhr,
                        NativePtr.zero,
                        NativePtr.zero,
                        NativePtr.zero
                    )
                )

        native {
            let! pWrites = writes
            VkRaw.vkUpdateDescriptorSets(device.Handle, uint32 writes.Length, pWrites, 0u, NativePtr.zero)
        }

type internal DescriptorPoolBag(device : Device) =
    inherit CachedResource(device)

    /// Maximum number of sets per pool.
    static let [<Literal>] MaxSetsPerPool = 1024

    /// By default, every pool has MinDescriptorCount descriptors of each type.
    static let [<Literal>] MinDescriptorCount = 512

    /// When allocating a set fails, we use the requested descriptor counts as base values for the new pool.
    /// The requested descriptor count is multiplied by TargetCountMultiplier to compute the minimum descriptor count for the pool.
    static let [<Literal>] TargetCountMultiplier = 4

    /// Empty pools get reset instead of destroyed as long as the number of pools in the bag does not exceed TargetPoolCount.
    static let [<Literal>] TargetPoolCount = 8

    /// Minimum descriptor counts for the initial pool.
    static let getInitialPoolDescriptorCounts (device: Device) =
        Map.ofList [
            VkDescriptorType.UniformBuffer,                1024
            VkDescriptorType.StorageBuffer,                4096
            VkDescriptorType.CombinedImageSampler,         4096
            VkDescriptorType.StorageImage,                 1024
            if device.IsExtensionEnabled KHRAccelerationStructure.Name then
                VkDescriptorType.AccelerationStructureKhr, 1024
        ]

    let pools = System.Collections.Generic.HashSet<DescriptorPool>()

    let createPool (layout: DescriptorSetLayout) (variableCount: int) =
        let counts =
            let minCounts =
                if pools.Count = 0 then
                    getInitialPoolDescriptorCounts device
                else
                    [
                        VkDescriptorType.UniformBuffer
                        VkDescriptorType.StorageBuffer
                        VkDescriptorType.CombinedImageSampler
                        VkDescriptorType.StorageImage
                        if device.IsExtensionEnabled KHRAccelerationStructure.Name then
                            VkDescriptorType.AccelerationStructureKhr
                    ]
                    |> List.map (fun t -> t, MinDescriptorCount)
                    |> Map.ofList

            let targetCounts =
                layout.GetDescriptorCounts variableCount
                |> Map.map (fun _ count ->
                    let count = if count < 0 then variableCount else count
                    count * TargetCountMultiplier
                )

            (minCounts, targetCounts) ||> Map.fold (fun result typ count ->
                let min = result |> Map.tryFindV typ |> ValueOption.defaultValue 0
                result |> Map.add typ (max min count)
            )

        let pool = device |> DescriptorPool.create MaxSetsPerPool counts
        pools.Add pool |> ignore
        Log.line "[Vulkan] using %d descriptor pools" pools.Count
        pool

    member this.CreateSet(layout : DescriptorSetLayout, variableCount: int) =
        let tryAllocSet pool =
            match pool |> DescriptorSet.tryAlloc layout variableCount with
            | Some set ->
                set.OnDestroyed.Add(fun _ -> this.RemoveSet set)
                Some set
            | _ ->
                None

        lock pools (fun () ->
            pools
            |> Seq.tryPick tryAllocSet
            |> Option.defaultWith (fun _ ->
                let pool = createPool layout variableCount
                match tryAllocSet pool with
                | Some set -> set
                | _ -> failf "Failed to allocate descriptor set"
            )
        )

    member x.RemoveSet (set : DescriptorSet) =
        lock pools (fun () ->
            let pool = set.Pool

            lock pool (fun _ ->
                if pools.Contains pool then
                    if pool.IsEmpty && pools.Count > TargetPoolCount then
                        pool.Dispose()
                        pools.Remove pool |> ignore
                        Log.line "[Vulkan] using %d descriptor pools" pools.Count
                else
                    failf "cannot free non-pooled descriptor set using pool"
            )
         )

    override x.Destroy() =
        for p in pools do p.Dispose()
        pools.Clear()

[<AbstractClass; Sealed; Extension>]
type ContextDescriptorSetExtensions private() =
    static let DescriptorPoolBag = Symbol.Create "DescriptorPoolBag"

    [<Extension>]
    static member inline Update(this : DescriptorPool, set : DescriptorSet, values : array<Descriptor>) =
        this |> DescriptorSet.update values set

    [<Extension>]
    static member CreateDescriptorSet(this : Device, layout : DescriptorSetLayout, [<Optional; DefaultParameterValue(0)>] variableCount : int) =
        use bag = this.GetCached(DescriptorPoolBag, 0, fun _ -> new DescriptorPoolBag(this))
        bag.CreateSet(layout, variableCount)

    [<Extension>]
    static member Update(set : DescriptorSet, values : array<Descriptor>) =
        set.Pool |> DescriptorSet.update values set
