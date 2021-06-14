namespace Aardvark.Rendering.Vulkan.Raytracing

open System.Runtime.InteropServices

#nowarn "9"

open Microsoft.FSharp.NativeInterop

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Vulkan.KHRRayTracingPipeline
open Aardvark.Rendering.Vulkan.KHRBufferDeviceAddress

type HitGroupConfig = List<Symbol>

type ShaderBindingTable =
    class
        inherit Resource
        val mutable public Pipeline : RaytracingPipeline
        val mutable public RaygenTable : Buffer
        val mutable public RaygenLookup : Map<Symbol, int>
        val mutable public MissTable : Buffer
        val mutable public MissLookup : Map<Symbol, int>
        val mutable public CallableTable : Buffer
        val mutable public CallableLookup : Map<Symbol, int>
        val mutable public HitGroupTable : Buffer
        val mutable public HitGroupLookup : Map<HitGroupConfig, int>
        val mutable public Stride : uint64

        override x.Destroy() =
            x.RaygenTable.Dispose()
            x.MissTable.Dispose()
            x.CallableTable.Dispose()
            x.HitGroupTable.Dispose()

        new(device : Device, pipeline : RaytracingPipeline,
            raygenTable : Buffer, raygenLookup : Map<Symbol, int>,
            missTable : Buffer, missLookup : Map<Symbol, int>,
            callableTable : Buffer, callableLookup : Map<Symbol, int>,
            hitGroupTable : Buffer, hitGroupLookup : Map<HitGroupConfig, int>,
            stride : uint64) =
            { inherit Resource(device)
              Pipeline = pipeline
              RaygenTable = raygenTable
              RaygenLookup = raygenLookup
              MissTable = missTable
              MissLookup = missLookup
              CallableTable = callableTable
              CallableLookup = callableLookup
              HitGroupTable = hitGroupTable
              HitGroupLookup = hitGroupLookup
              Stride = stride }
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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderBindingTable =

    let private bufferUsage =
        VkBufferUsageFlags.ShaderBindingTableBitKhr ||| VkBufferUsageFlags.ShaderDeviceAddressBitKhr

    let private tryUpdateBuffer (handles : ShaderHandles) (entries : int[]) (buffer : Buffer) =
        let size = handles.SizeAligned * entries.Length

        if size > int buffer.Size then
            false

        else
            let pSrc = NativePtr.alloc<uint8> size

            try
                let src = NativePtr.toNativeInt pSrc
                let mutable offset = 0n

                for e in entries do
                    Marshal.Copy(handles.Data, e * handles.Size, src + offset, handles.Size)
                    offset <- offset + nativeint handles.SizeAligned

                let nb = NativeMemoryBuffer(src, size)
                buffer |> Buffer.tryUpdate nb

            finally
                NativePtr.free pSrc

    let private createBuffer (device : Device) (handles : ShaderHandles) (entries : int[]) =
        let buffer = device |> Buffer.alloc bufferUsage (int64 <| handles.SizeAligned * entries.Length)
        assert (buffer |> tryUpdateBuffer handles entries)
        buffer

    let private getRaygenData (shaderGroups : GroupEntry[]) =

        let groups =
            shaderGroups |> Array.filter GroupEntry.isRaygen

        let entries =
            groups |> Array.map (fun g -> g.Index)

        let lookup =
            groups
            |> Array.mapi (fun i g -> GroupEntry.name g, i)
            |> Map.ofArray

        entries, lookup

    let private getMissData (shaderGroups : GroupEntry[])  (pipeline : RaytracingPipeline) =

        let lookup =
             pipeline.Description.Program.Effect.ShaderBindingTableLayout.MissIndices

        let entries =
            shaderGroups
            |> Array.filter GroupEntry.isMiss
            |> Array.sortBy (fun e -> lookup |> Map.find (GroupEntry.name e))
            |> Array.map GroupEntry.index

        entries, lookup

    let private getCallableData (shaderGroups : GroupEntry[]) (pipeline : RaytracingPipeline) =

        let lookup =
             pipeline.Description.Program.Effect.ShaderBindingTableLayout.CallableIndices

        let entries =
            shaderGroups
            |> Array.filter GroupEntry.isCallable
            |> Array.sortBy (fun e -> lookup |> Map.find (GroupEntry.name e))
            |> Array.map GroupEntry.index

        entries, lookup

    let private getHitData (shaderGroups : GroupEntry[]) (configs : Set<HitGroupConfig>) (pipeline : RaytracingPipeline) =

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

        entries, lookup


    let private getData (shaderGroups : GroupEntry[]) (hitConfigs : Set<HitGroupConfig>) (pipeline : RaytracingPipeline) =
        let raygenEntries, raygenLookup     = getRaygenData shaderGroups
        let missEntries, missLookup         = getMissData shaderGroups pipeline
        let callableEntries, callableLookup = getCallableData shaderGroups pipeline
        let hitGroupEntries, hitGroupLookup = getHitData shaderGroups hitConfigs pipeline

        {| RaygenEntries   = raygenEntries
           RaygenLookup    = raygenLookup
           MissEntries     = missEntries
           MissLookup      = missLookup
           CallableEntries = callableEntries
           CallableLookup  = callableLookup
           HitGroupEntries = hitGroupEntries
           HitGroupLookup  = hitGroupLookup |}

    let update (hitConfigs : Set<HitGroupConfig>) (pipeline : RaytracingPipeline) (table : ShaderBindingTable) =
        let device = pipeline.Device

        let shaderGroups =
            pipeline.Description.Program.Groups |> List.mapi GroupEntry.ofShaderGroup |> Array.ofList

        let shaderGroupHandles =
            ShaderHandles.get shaderGroups.Length pipeline

        let data = getData shaderGroups hitConfigs pipeline

        if not (table.RaygenTable |> tryUpdateBuffer shaderGroupHandles data.RaygenEntries) then
            table.RaygenTable <- createBuffer device shaderGroupHandles data.RaygenEntries

        if not (table.MissTable |> tryUpdateBuffer shaderGroupHandles data.MissEntries) then
            table.MissTable <- createBuffer device shaderGroupHandles data.MissEntries

        if not (table.CallableTable |> tryUpdateBuffer shaderGroupHandles data.CallableEntries) then
            table.CallableTable <- createBuffer device shaderGroupHandles data.CallableEntries

        if not (table.HitGroupTable |> tryUpdateBuffer shaderGroupHandles data.HitGroupEntries) then
            table.HitGroupTable <- createBuffer device shaderGroupHandles data.HitGroupEntries

        table.Pipeline <- pipeline      

    let create (hitConfigs : Set<HitGroupConfig>) (pipeline : RaytracingPipeline) =
        let device = pipeline.Device

        let shaderGroups =
            pipeline.Description.Program.Groups |> List.mapi GroupEntry.ofShaderGroup |> Array.ofList

        let shaderGroupHandles =
            ShaderHandles.get shaderGroups.Length pipeline

        let data = getData shaderGroups hitConfigs pipeline
        let raygenTable   = createBuffer device shaderGroupHandles data.RaygenEntries
        let missTable     = createBuffer device shaderGroupHandles data.MissEntries
        let callableTable = createBuffer device shaderGroupHandles data.CallableEntries
        let hitGroupTable = createBuffer device shaderGroupHandles data.HitGroupEntries

        new ShaderBindingTable(
            device, pipeline,
            raygenTable, data.RaygenLookup,
            missTable, data.MissLookup,
            callableTable, data.CallableLookup,
            hitGroupTable, data.HitGroupLookup,
            uint64 shaderGroupHandles.SizeAligned
        )