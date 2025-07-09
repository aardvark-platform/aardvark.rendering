namespace Aardvark.Rendering.Vulkan.Raytracing

open System.Runtime.InteropServices

#nowarn "9"

open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open KHRRayTracingPipeline
open KHRBufferDeviceAddress

type ShaderBindingSubtable<'T when 'T : comparison> =
    class
        inherit BufferDecorator
        val public Stride : uint64
        val mutable public Indices : Map<'T, int>

        member x.AddressRegion =
            VkStridedDeviceAddressRegionKHR(
                x.DeviceAddress, x.Stride, x.Size
            )

        new ( buffer : Buffer, lookup : Map<'T, int>, stride : uint64) =
            { inherit BufferDecorator(buffer)
              Stride = stride
              Indices = lookup }
    end

[<StructLayout(LayoutKind.Sequential)>]
type ShaderBindingTableHandle =
    struct
        val public RaygenTable   : VkStridedDeviceAddressRegionKHR
        val public MissTable     : VkStridedDeviceAddressRegionKHR
        val public HitGroupTable : VkStridedDeviceAddressRegionKHR
        val public CallableTable : VkStridedDeviceAddressRegionKHR

        new (raygen, miss, hit, callable) =
            { RaygenTable = raygen; MissTable = miss; HitGroupTable = hit; CallableTable = callable }
    end

type ShaderBindingTable =
    class
        inherit Resource<ShaderBindingTableHandle>
        val public RaygenTable   : ShaderBindingSubtable<Symbol>
        val public MissTable     : ShaderBindingSubtable<Symbol>
        val public HitGroupTable : ShaderBindingSubtable<Symbol[]>
        val public CallableTable : ShaderBindingSubtable<Symbol>

        override x.Destroy() =
            x.RaygenTable.Dispose()
            x.MissTable.Dispose()
            x.HitGroupTable.Dispose()
            x.CallableTable.Dispose()

        new(raygenTable : ShaderBindingSubtable<Symbol>,
            missTable : ShaderBindingSubtable<Symbol>,
            hitGroupTable : ShaderBindingSubtable<Symbol[]>,
            callableTable : ShaderBindingSubtable<Symbol>) =

            let handle =
                ShaderBindingTableHandle(
                    raygenTable.AddressRegion,
                    missTable.AddressRegion,
                    hitGroupTable.AddressRegion,
                    callableTable.AddressRegion
                )

            { inherit Resource<_>(raygenTable.Device, handle)
              RaygenTable = raygenTable
              MissTable = missTable
              HitGroupTable = hitGroupTable
              CallableTable = callableTable }
    end


[<AutoOpen>]
module private ShaderBindingTableUtilities =

    type GroupEntry =
        { Index : int
          Group : ShaderGroup<unit> }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GroupEntry =

        let ofShaderGroup (index : int) (group : ShaderGroup<'T>) =
            { Index = index; Group = group |> ShaderGroup.set () }

        let private isGeneralWithStage (stage : ShaderStage) (entry : GroupEntry) =
            match entry.Group with
            | ShaderGroup.General g when g.Stage = stage -> true
            | _ -> false

        let isRaygen =
            isGeneralWithStage ShaderStage.RayGeneration

        let isMiss =
            isGeneralWithStage ShaderStage.Miss

        let isCallable =
            isGeneralWithStage ShaderStage.Callable

        let isHitGroup (name : Symbol) (rayType : Symbol) (entry : GroupEntry) =
            match entry.Group with
            | ShaderGroup.HitGroup g -> g.Name = name && g.RayType = rayType
            | _ -> false

        let index (entry : GroupEntry) =
            entry.Index

        let name (entry : GroupEntry) =
            entry.Group |> ShaderGroup.name |> Option.get


    type ShaderHandles =
        { Data : uint8[]
          Size : int
          SizeAligned : int }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ShaderHandles =

        let private roundUp alignment x =
            (x + alignment - 1u) &&& ~~~(alignment - 1u)

        let get (groupCount : int) (pipeline : RaytracingPipeline) =

            let size, sizeAligned =
                match pipeline.Device.PhysicalDevice.Limits.Raytracing with
                | Some limits ->
                    limits.ShaderGroupHandleSize,
                    limits.ShaderGroupHandleSize |> roundUp limits.ShaderGroupHandleAlignment

                | _ ->
                    failwith "Cannot determine shader binding table alignment requirements"

            let shaderHandles : uint8[] = Array.zeroCreate <| groupCount  * (int size)

            shaderHandles |> NativePtr.pinArr (fun ptr ->
                VkRaw.vkGetRayTracingShaderGroupHandlesKHR(
                    pipeline.Device.Handle, pipeline.Handle, 0u, uint32 groupCount, uint64 shaderHandles.Length, ptr.Address
                )
                |> check "Failed to get shader group handles"
            )

            { Data = shaderHandles
              Size = int size
              SizeAligned = int sizeAligned }

    type SubtableEntries<'T when 'T : comparison> =
        | SingleEntry of int
        | MultiEntry  of int[] * lookup: Map<'T, int>

        member x.Count =
            match x with
            | SingleEntry _ -> 1
            | MultiEntry (e, _) -> e.Length

        member x.Lookup =
            match x with
            | SingleEntry _ -> Map.empty
            | MultiEntry (_, l) -> l

        member x.ToArray =
            match x with
            | SingleEntry e -> [| e |]
            | MultiEntry (e, _) -> e

    type SubtableData<'T when 'T : comparison> =
        { Entries : SubtableEntries<'T>
          Handles : ShaderHandles }

        member x.Count =
            x.Entries.Count

        member x.TotalSize =
            x.Handles.SizeAligned * x.Count

        member x.Lookup =
            x.Entries.Lookup

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SubtableData =

        let private withLookup (lookup : Map<Symbol, int>) (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            let entries =
                shaderGroups
                |> Array.choose (fun e -> lookup |> Map.tryFind (GroupEntry.name e) |> Option.map (fun i -> struct (e, i)))
                |> Array.sortBy sndv
                |> Array.map (fstv >> GroupEntry.index)

            { Entries   = MultiEntry (entries, lookup)
              Handles   = shaderHandles }

        let raygen (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            let raygen = shaderGroups |> Array.find GroupEntry.isRaygen
            { Entries = SingleEntry raygen.Index; Handles = shaderHandles }

        let miss (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =
            let lookup = pipeline.Description.Program.ShaderBindingTableLayout.MissIndices
            shaderGroups |> Array.filter GroupEntry.isMiss |> withLookup lookup shaderHandles

        let callable (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =
            let lookup = pipeline.Description.Program.ShaderBindingTableLayout.CallableIndices
            shaderGroups |> Array.filter GroupEntry.isCallable |> withLookup lookup shaderHandles

        let hitGroups (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (configs : Set<Symbol[]>) (pipeline : RaytracingPipeline) =
            let rayOffsets =
                pipeline.Description.Program.ShaderBindingTableLayout.RayOffsets

            let rayTypes =
                Map.toArray rayOffsets
                |> Array.sortBy snd

            let entries = ResizeArray(configs.Count * rayTypes.Length)
            let mutable lookup = Map.empty

            for cfg in configs do
                lookup <- lookup |> Map.add cfg entries.Count

                for name in cfg do
                    for rt, _ in rayTypes do
                        match shaderGroups |> Array.tryFindV (GroupEntry.isHitGroup name rt) with
                        | ValueSome entry -> entries.Add(entry.Index)
                        | _ -> Log.warn "[Raytracing] Missing hit group %A for ray type %A" name rt

            { Entries = MultiEntry (entries.ToArray(), lookup)
              Handles = shaderHandles }


    type TableData =
        { RaygenData   : SubtableData<Symbol>
          MissData     : SubtableData<Symbol>
          CallableData : SubtableData<Symbol>
          HitGroupData : SubtableData<Symbol[]> }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TableData =

        let create (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (hitConfigs : Set<Symbol[]>) (pipeline : RaytracingPipeline) =
            { RaygenData   = SubtableData.raygen shaderHandles shaderGroups
              MissData     = SubtableData.miss shaderHandles shaderGroups pipeline
              CallableData = SubtableData.callable shaderHandles shaderGroups pipeline
              HitGroupData = SubtableData.hitGroups shaderHandles shaderGroups hitConfigs pipeline }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private ShaderBindingSubtable =

    let private bufferUsage =
        VkBufferUsageFlags.TransferDstBit |||
        VkBufferUsageFlags.ShaderBindingTableBitKhr |||
        VkBufferUsageFlags.ShaderDeviceAddressBitKhr

    let tryUpdate (data : SubtableData<'T>) (table : ShaderBindingSubtable<'T>) =
        if data.Count = 0 then
            true

        elif data.TotalSize > int table.Size then
            false

        // Shrink if too large
        elif int table.Size > 2 * data.Handles.SizeAligned * Fun.NextPowerOfTwo data.Count then
            false

        else
            table.Indices <- data.Lookup

            let pSrc = NativePtr.alloc<uint8> data.TotalSize

            try
                let src = NativePtr.toNativeInt pSrc
                let mutable offset = 0n

                for e in data.Entries.ToArray do
                    Marshal.Copy(data.Handles.Data, e * data.Handles.Size, src + offset, data.Handles.Size)
                    offset <- offset + nativeint data.Handles.SizeAligned

                Buffer.write table (fun dst -> Marshal.Copy(src, dst, nativeint data.TotalSize))
                true

            finally
                NativePtr.free pSrc

    let createWithCount (device : Device) (name : string) (count : int) (data : SubtableData<'T>) =
        let requestedSize = count * data.Handles.SizeAligned
        let size = max requestedSize data.Handles.SizeAligned
        let baseAlignment = uint64 device.PhysicalDevice.Limits.Raytracing.Value.ShaderGroupBaseAlignment

        let buffer = device.DeviceMemory.CreateBuffer(bufferUsage, uint64 size, baseAlignment)
        let table = new ShaderBindingSubtable<'T>(buffer, data.Lookup, uint64 data.Handles.SizeAligned)

        if device.DebugLabelsEnabled then
            buffer.Name <- $"{name} (Shader Binding Table)"

        if not (table |> tryUpdate data) then
            failf "Failed to update shader binding table"

        table

    let create (device : Device) (name : string) (data : SubtableData<'T>) =
        let count = int <| Fun.NextPowerOfTwo data.Count
        data |> createWithCount device name count


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderBindingTable =

    let updateOrRecreate (hitConfigs : Set<Symbol[]>) (pipeline : RaytracingPipeline) (table : ShaderBindingTable) =
        let device = pipeline.Device

        let shaderGroups =
            pipeline.Description.Program.Groups |> List.mapi GroupEntry.ofShaderGroup |> Array.ofList

        let shaderHandles =
            ShaderHandles.get shaderGroups.Length pipeline

        let tableData =
            TableData.create shaderHandles shaderGroups hitConfigs pipeline

        let mutable recreated = false

        let updateOrRecreate (name : string) (data : SubtableData<'T>) (table : ShaderBindingSubtable<'T>) =
            if ShaderBindingSubtable.tryUpdate data table then
                table.AddReference()
                table
            else
                recreated <- true
                ShaderBindingSubtable.create device name data

        let raygenTable   = table.RaygenTable   |> updateOrRecreate "Raygen" tableData.RaygenData
        let missTable     = table.MissTable     |> updateOrRecreate "Miss" tableData.MissData
        let callableTable = table.CallableTable |> updateOrRecreate "Callable" tableData.CallableData
        let hitGroupTable = table.HitGroupTable |> updateOrRecreate "Hit Group" tableData.HitGroupData

        // At least one subtable had to be recreated.
        // Build a new table with the new subtables, old subtables are re-used.
        if recreated then
            table.Dispose()
            new ShaderBindingTable(raygenTable, missTable, hitGroupTable, callableTable)

        // Every subtable could be updated in-place.
        // Remove the added references, and return the old table.
        else
            raygenTable.Dispose()
            missTable.Dispose()
            callableTable.Dispose()
            hitGroupTable.Dispose()
            table

    let create (hitConfigs : Set<Symbol[]>) (pipeline : RaytracingPipeline) =
        let device = pipeline.Device

        let shaderGroups =
            pipeline.Description.Program.Groups |> List.mapi GroupEntry.ofShaderGroup |> Array.ofList

        let shaderHandles =
            ShaderHandles.get shaderGroups.Length pipeline

        let tableData =
            TableData.create shaderHandles shaderGroups hitConfigs pipeline

        let raygenTable   = tableData.RaygenData   |> ShaderBindingSubtable.create device "Raygen"
        let missTable     = tableData.MissData     |> ShaderBindingSubtable.create device "Miss"
        let callableTable = tableData.CallableData |> ShaderBindingSubtable.create device "Callable"
        let hitGroupTable = tableData.HitGroupData |> ShaderBindingSubtable.create device "Hit Group"

        new ShaderBindingTable(raygenTable, missTable, hitGroupTable, callableTable)