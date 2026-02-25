namespace Aardvark.Rendering.Vulkan

open System
open System.IO
open Aardvark.Base
open Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

type internal PipelineCacheId =
    { VendorId : string
      DeviceId : string
      Driver   : Version
      CacheId  : string }

[<AutoOpen>]
module internal ``PipelineCacheId Extensions`` =

    type IDevice with
        member this.PipelineCacheId =
            { VendorId = this.PhysicalDevice.Vendor
              DeviceId = this.PhysicalDevice.Id
              Driver   = this.PhysicalDevice.DriverVersion
              CacheId  = this.PhysicalDevice.Properties.PipelineCacheId }

type internal PipelineCacheData =
    { Id   : PipelineCacheId
      Hash : byte[]
      Size : uint64
      Data : byte[] }

type internal PipelineCache private (device: IDevice, handle: VkPipelineCache) =
    let mutable handle = handle
    static let serializer = MBrace.FsPickler.FsPickler.CreateBinarySerializer()

    static let getFilePath (device: IDevice) =
        try
            let assemblyName =
                let assembly = IntrospectionProperties.CurrentEntryAssembly
                let name = if notNull assembly then assembly.GetName().Name else null
                name ||? "unknown"

            Path.combine [
                CachingProperties.CacheDirectory
                "Pipelines"
                $"{assemblyName}_{device.PhysicalDevice.Id}.pipeline_cache"
            ]

        with exn ->
            Log.warn $"[Vulkan] Failed to determine path of pipeline cache: {exn.Message}"
            null

    let getData (size: uint64) (pData: nativeint) =
        let mutable size = size
        let result = VkRaw.vkGetPipelineCacheData(device.Handle, handle, &&size, pData)

        if result <> VkResult.Success then
            failwith $"Failed to get pipeline cache data ({result})"

        elif size > uint64 Int32.MaxValue then
            failwith $"Invalid pipeline cache data size ({size})"

        size

    let getDataArray() =
        let size = getData 0UL 0n

        let mutable data = Array.zeroCreate<uint8> (int32 size)
        use pData = fixed data

        let size = getData size pData.Address
        size, data

    private new (device: IDevice, createInfo: byref<VkPipelineCacheCreateInfo>) =
        let mutable handle = VkPipelineCache.Null

        VkRaw.vkCreatePipelineCache(device.Handle, &&createInfo, NativePtr.zero, &&handle)
            |> check "failed to create empty pipeline cache"

        new PipelineCache(device, handle)

    new (device: IDevice) =
        let mutable createInfo = VkPipelineCacheCreateInfo(VkPipelineCacheCreateFlags.None, 0UL, 0n)
        new PipelineCache(device, &createInfo)

    member _.Handle = handle

    static member Deserialize(device: IDevice) =
        let path = getFilePath device

        if notNull path then
            Report.BeginTimed(3, $"[Vulkan] Deserializing pipeline cache '{path}'")

            try
                let data =
                    if File.Exists path then
                        use stream = File.OpenRead path
                        let data = serializer.Deserialize<PipelineCacheData>(stream)
                        let hash = serializer.ComputeHash data.Data

                        if data.Id <> device.PipelineCacheId then
                            Report.Line(3, $"Pipeline cache ID mismatch (expected: {device.PipelineCacheId}, actual: {data.Id})")
                            None

                        elif data.Hash <> hash.Hash then
                            Report.Line(3, $"Pipeline cache hash mismatch")
                            None

                        else
                            Some data
                    else
                        None

                match data with
                | Some data ->
                    use pData = fixed data.Data
                    let mutable createInfo = VkPipelineCacheCreateInfo(VkPipelineCacheCreateFlags.None, data.Size, pData.Address)
                    let result = new PipelineCache(device, &createInfo)
                    Report.EndTimed(3, " - success") |> ignore
                    result

                | None ->
                    if File.Exists path then
                        File.Delete path
                        Report.EndTimed(3, " - invalidated") |> ignore
                    else
                        Report.EndTimed(3, " - not found") |> ignore

                    new PipelineCache(device)

            with e ->
                Report.Line(3, $"{e.GetType()}: {e.Message}")
                Report.EndTimed(3, " - failed") |> ignore
                new PipelineCache(device)
        else
            new PipelineCache(device, VkPipelineCache.Null)

    member _.Serialize() =
        let path = getFilePath device

        if notNull path && handle.IsValid then
            Report.BeginTimed(3, $"[Vulkan] Serializing pipeline cache '{path}'")

            try
                let directory = Path.GetDirectoryName path
                if not <| Directory.Exists directory then
                    Directory.CreateDirectory directory |> ignore

                let size, data = getDataArray()
                let hash = serializer.ComputeHash data

                use stream = File.OpenWrite(path)

                serializer.Serialize(stream, {
                    Id   = device.PipelineCacheId
                    Hash = hash.Hash
                    Size = size
                    Data = data
                })

                Report.EndTimed(3, " - success") |> ignore
            with e ->
                Report.Line(3, $"{e.GetType()}: {e.Message}")
                Report.EndTimed(3, " - failed") |> ignore

    member _.Dispose() =
        if handle.IsValid then
            VkRaw.vkDestroyPipelineCache(device.Handle, handle, NativePtr.zero)
            handle <- VkPipelineCache.Null

    interface IDisposable with
        member this.Dispose() = this.Dispose()