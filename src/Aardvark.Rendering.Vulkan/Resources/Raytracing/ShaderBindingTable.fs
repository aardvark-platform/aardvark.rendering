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

        member private x.GetAddressRegion(index : int) =
            let offset = uint64 index * x.Stride

            VkStridedDeviceAddressRegionKHR(
                x.Buffer.DeviceAddress + offset, x.Stride, uint64 x.Buffer.Size
            )

        member x.GetAddressRegion(name : 'T) =
            x.GetAddressRegion(x.Indices.[name])

        member x.AddressRegion =
            x.GetAddressRegion(0)

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
            ShaderGroup.name entry.Group
            

    type ShaderHandles =
        { Data : uint8[]
          Size : int
          SizeAligned : int 
          SizeBaseAligned : int }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module ShaderHandles =

        let private roundUp alignment x =
            (x + alignment - 1u) &&& ~~~(alignment - 1u)

        let get (groupCount : int) (pipeline : RaytracingPipeline) =

            let size, sizeAligned, sizeBaseAligned =
                match pipeline.Device.PhysicalDevice.Limits.Raytracing with
                | Some limits ->
                    limits.ShaderGroupHandleSize,
                    limits.ShaderGroupHandleSize |> roundUp limits.ShaderGroupHandleAlignment,
                    limits.ShaderGroupHandleSize |> roundUp limits.ShaderGroupBaseAlignment

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
              SizeAligned = int sizeAligned 
              SizeBaseAligned = int sizeBaseAligned}

    type Alignment =
        | Base
        | Handle

    type SubtableData<'T when 'T : comparison> =
        { Entries   : int[]
          Handles   : ShaderHandles
          Alignment : Alignment
          Lookup    : Map<'T, int> }

        member x.TotalSize =
            match x.Alignment with
            | Base -> x.Handles.SizeBaseAligned * x.Entries.Length
            | Handle -> x.Handles.SizeAligned * x.Entries.Length

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SubtableData =

        let private withLookup (lookup : Map<Symbol, int>) (alignment : Alignment) (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            let entries =
                shaderGroups
                |> Array.sortBy (fun e -> lookup |> Map.find (GroupEntry.name e))
                |> Array.map GroupEntry.index

            { Entries   = entries
              Handles   = shaderHandles
              Alignment = alignment
              Lookup    = lookup }

        let private withoutLookup (alignment : Alignment) (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            let lookup =
                shaderGroups
                |> Array.mapi (fun i g -> GroupEntry.name g, i)
                |> Map.ofArray

            withLookup lookup alignment shaderHandles shaderGroups

        let raygen (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) =
            shaderGroups |> Array.filter GroupEntry.isRaygen |> withoutLookup Base shaderHandles

        let miss (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =
            let lookup = pipeline.Description.Program.Effect.ShaderBindingTableLayout.MissIndices
            shaderGroups |> Array.filter GroupEntry.isMiss |> withLookup lookup Handle shaderHandles

        let callable (shaderHandles : ShaderHandles) (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =
            let lookup = pipeline.Description.Program.Effect.ShaderBindingTableLayout.CallableIndices
            shaderGroups |> Array.filter GroupEntry.isCallable |> withLookup lookup Handle shaderHandles

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

            { Entries   = entries
              Handles   = shaderHandles
              Alignment = Handle
              Lookup    = lookup }


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
        VkBufferUsageFlags.ShaderBindingTableBitKhr ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr

    let tryUpdate (data : SubtableData<'T>) (table : ShaderBindingSubtable<'T>) =
        if data.TotalSize > int table.Buffer.Size then
            false

        else
            table.Indices <- data.Lookup

            let pSrc = NativePtr.alloc<uint8> data.TotalSize

            try
                let src = NativePtr.toNativeInt pSrc
                let mutable offset = 0n

                for e in data.Entries do
                    Marshal.Copy(data.Handles.Data, e * data.Handles.Size, src + offset, data.Handles.Size)
                    offset <- offset + nativeint data.Handles.SizeAligned

                let nb = NativeMemoryBuffer(src, data.TotalSize)
                table.Buffer |> Buffer.tryUpdate nb

            finally
                NativePtr.free pSrc

    let create (device : Device) (data : SubtableData<'T>) =
        let buffer = device |> Buffer.alloc bufferUsage (int64 data.TotalSize)
        let table = new ShaderBindingSubtable<'T>(buffer, data.Lookup, uint64 data.Handles.SizeAligned)
        assert (table |> tryUpdate data)
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