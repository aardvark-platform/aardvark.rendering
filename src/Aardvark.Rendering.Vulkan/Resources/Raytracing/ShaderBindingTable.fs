namespace Aardvark.Rendering.Vulkan.Raytracing

open System.Runtime.InteropServices

#nowarn "9"

open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Raytracing
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRRayTracingPipeline
open Aardvark.Rendering.Vulkan.KHRBufferDeviceAddress

type ShaderBindingSubtable<'T when 'T : comparison> =
    class
        inherit Resource
        val public Buffer : Buffer
        val public Stride : uint64
        val mutable public Indices : Map<'T, int>

        member x.AddressRegion =
            VkStridedDeviceAddressRegionKHR(
                x.Buffer.DeviceAddress, x.Stride, uint64 x.Buffer.Size
            )

        override x.Destroy() =
            x.Buffer.Dispose()

        new ( buffer : Buffer, lookup : Map<'T, int>, stride : uint64) =
            { inherit Resource(buffer.Device)
              Buffer = buffer
              Stride = stride
              Indices = lookup }
    end


type ShaderBindingTable =
    class
        inherit Resource
        val mutable public RaygenTable : ShaderBindingSubtable<Symbol>
        val mutable public MissTable : ShaderBindingSubtable<Symbol>
        val mutable public CallableTable : ShaderBindingSubtable<Symbol>
        val mutable public HitGroupTable : ShaderBindingSubtable<HitConfig>

        override x.Destroy() =
            x.RaygenTable.Dispose()
            x.MissTable.Dispose()
            x.CallableTable.Dispose()
            x.HitGroupTable.Dispose()

        new(raygenTable : ShaderBindingSubtable<Symbol>,
            missTable : ShaderBindingSubtable<Symbol>,
            callableTable : ShaderBindingSubtable<Symbol>,
            hitGroupTable : ShaderBindingSubtable<HitConfig>) =
            { inherit Resource(raygenTable.Device)
              RaygenTable = raygenTable
              MissTable = missTable
              CallableTable = callableTable
              HitGroupTable = hitGroupTable}
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
                    failwith "[Raytracing] Cannot determine alignment requirements"

            let shaderHandles : uint8[] = Array.zeroCreate <| groupCount  * (int size)

            pinned shaderHandles (fun ptr ->
                VkRaw.vkGetRayTracingShaderGroupHandlesKHR(
                    pipeline.Device.Handle, pipeline.Handle, 0u, uint32 groupCount, uint64 shaderHandles.Length, ptr
                )
                |> check "[Raytracing] Failed to get shader group handles"
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

        member x.TotalSize =
            x.Handles.SizeAligned * x.Entries.Count

        member x.Lookup =
            x.Entries.Lookup

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SubtableData =

        let private withLookup (lookup : Map<Symbol, int>) (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            let entries =
                shaderGroups
                |> Array.choose (fun e -> lookup |> Map.tryFind (GroupEntry.name e) |> Option.map (fun i -> e, i))
                |> Array.sortBy snd
                |> Array.map (fst >> GroupEntry.index)

            { Entries   = MultiEntry (entries, lookup)
              Handles   = shaderHandles }

        let raygen (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            let raygen = shaderGroups |> Array.find GroupEntry.isRaygen
            { Entries = SingleEntry raygen.Index; Handles = shaderHandles }

        let miss (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =
            let lookup = pipeline.Description.Program.Effect.ShaderBindingTableLayout.MissIndices
            shaderGroups |> Array.filter GroupEntry.isMiss |> withLookup lookup shaderHandles

        let callable (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =
            let lookup = pipeline.Description.Program.Effect.ShaderBindingTableLayout.CallableIndices
            shaderGroups |> Array.filter GroupEntry.isCallable |> withLookup lookup shaderHandles

        let hitGroups (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (configs : Set<HitConfig>) (pipeline : RaytracingPipeline) =
            let rayOffsets =
                pipeline.Description.Program.Effect.ShaderBindingTableLayout.RayOffsets

            let rayTypes =
                Map.toList rayOffsets
                |> List.sortBy snd
                |> List.map fst

            let configs =
                Set.toList configs

            let groups =
                configs
                |> List.map (fun cfg ->
                    cfg |> List.collect (fun name ->
                        rayTypes |> List.choose (fun rt ->
                            match shaderGroups |> Array.tryFind (GroupEntry.isHitGroup name rt) with
                            | None -> Log.warn "[Raytracing] Missing hit group %A for ray type %A" name rt; None
                            | x -> x
                        )
                    )
                )

            let entries =
                groups |> List.concat |> List.map GroupEntry.index |> List.toArray

            let indices =
                (0, groups)
                ||> List.scan (fun cnt entries -> cnt + List.length entries)
                |> List.take configs.Length

            let lookup =
                (configs, indices)
                ||> List.zip
                |> Map.ofList

            { Entries   = MultiEntry (entries, lookup)
              Handles   = shaderHandles }


    type TableData =
        { RaygenData   : SubtableData<Symbol>
          MissData     : SubtableData<Symbol>
          CallableData : SubtableData<Symbol>
          HitGroupData : SubtableData<HitConfig> }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module TableData =

        let create (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (hitConfigs : Set<HitConfig>) (pipeline : RaytracingPipeline) =
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
        if data.TotalSize > int table.Buffer.Size then
            false

        elif data.Entries.Count = 0 then
            true

        else
            table.Indices <- data.Lookup

            let pSrc = NativePtr.alloc<uint8> data.TotalSize

            try
                let src = NativePtr.toNativeInt pSrc
                let mutable offset = 0n

                for e in data.Entries.ToArray do
                    Marshal.Copy(data.Handles.Data, e * data.Handles.Size, src + offset, data.Handles.Size)
                    offset <- offset + nativeint data.Handles.SizeAligned

                let nb = NativeMemoryBuffer(src, data.TotalSize)
                table.Buffer |> Buffer.tryUpdate nb

            finally
                NativePtr.free pSrc

    let create (device : Device) (data : SubtableData<'T>) =
        let size = max data.TotalSize data.Handles.SizeAligned

        let buffer = device |> Buffer.alloc bufferUsage (int64 size)
        let table = new ShaderBindingSubtable<'T>(buffer, data.Lookup, uint64 data.Handles.SizeAligned)

        if not (table |> tryUpdate data) then
            failwith "[Raytracing] Failed to update shader binding table"

        table


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderBindingTable =

    let update (hitConfigs : Set<HitConfig>) (pipeline : RaytracingPipeline) (table : ShaderBindingTable) =
        let device = pipeline.Device

        let shaderGroups =
            pipeline.Description.Program.Groups |> List.mapi GroupEntry.ofShaderGroup |> Array.ofList

        let shaderHandles =
            ShaderHandles.get shaderGroups.Length pipeline

        let tableData =
            TableData.create shaderHandles shaderGroups hitConfigs pipeline

        let updateOrRecreate (data : SubtableData<'T>) (table : ShaderBindingSubtable<'T>) =
            if ShaderBindingSubtable.tryUpdate data table then
                table
            else
                table.Dispose()
                ShaderBindingSubtable.create device data

        table.RaygenTable   <- table.RaygenTable   |> updateOrRecreate tableData.RaygenData
        table.MissTable     <- table.MissTable     |> updateOrRecreate tableData.MissData
        table.CallableTable <- table.CallableTable |> updateOrRecreate tableData.CallableData
        table.HitGroupTable <- table.HitGroupTable |> updateOrRecreate tableData.HitGroupData

    let create (hitConfigs : Set<HitConfig>) (pipeline : RaytracingPipeline) =
        let device = pipeline.Device

        let shaderGroups =
            pipeline.Description.Program.Groups |> List.mapi GroupEntry.ofShaderGroup |> Array.ofList

        let shaderHandles =
            ShaderHandles.get shaderGroups.Length pipeline

        let tableData =
            TableData.create shaderHandles shaderGroups hitConfigs pipeline

        let raygenTable   = tableData.RaygenData   |> ShaderBindingSubtable.create device
        let missTable     = tableData.MissData     |> ShaderBindingSubtable.create device
        let callableTable = tableData.CallableData |> ShaderBindingSubtable.create device
        let hitGroupTable = tableData.HitGroupData |> ShaderBindingSubtable.create device

        new ShaderBindingTable(
            raygenTable, missTable,
            callableTable, hitGroupTable
        )