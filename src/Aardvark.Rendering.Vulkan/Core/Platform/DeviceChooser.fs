namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open Aardvark.Rendering
open System
open System.Reflection
open System.IO
open System.Security.Cryptography
open System.Text

/// Interface for device choosers
[<AllowNullLiteral>]
type IDeviceChooser =

    /// Returns a device to use from the given array of devices.
    abstract member Run : devices: PhysicalDevice[] -> PhysicalDevice


/// Base class for device choosers that support caching the selected device.
/// The environment variable AARDVARK_VULKAN_DEVICE overrides the choice: it accepts
/// "integrated", "discrete" (or "dedicated"), or a case-insensitive substring of a device name.
[<AbstractClass>]
type DeviceChooser() =

    static let configPath =
        let newHash() =
            Guid.NewGuid().ToByteArray() |> Convert.ToBase64String

        let appHash =
            try
                let asm = Assembly.GetEntryAssembly()
                let location = if isNull asm then null else asm.Location

                if String.IsNullOrWhiteSpace location then
                    newHash()
                else
                    let md5 = MD5.Create()
                    location
                    |> Encoding.Unicode.GetBytes
                    |> md5.ComputeHash
                    |> Convert.ToBase64String
            with _ ->
                newHash()

        Path.combine [
            CachingProperties.CacheDirectory
            "Config"
            $"{appHash.Replace('/', '_')}.vkconfig"
        ]

    /// Reads the config file to determine a device to use.
    static let tryReadConfig (devices: PhysicalDevice seq) =
        if File.Exists configPath then
            try
                let currentIds = devices |> Seq.map _.Id |> Set.ofSeq
                let cachedIds = File.readAllLines configPath

                // If there is a new device do not use the cached setting
                if Set.isSuperset (Set.ofSeq cachedIds) currentIds then
                    devices |> Seq.tryFind (fun d -> d.Id = cachedIds.[0])
                else
                    None

            with e ->
                Log.warn $"[Vulkan] Failed to read device config file '{configPath}': {e.Message}"
                None
        else
            None

    /// Writes the chosen device to the config file.
    static let writeConfig (chosen: PhysicalDevice) (devices: PhysicalDevice seq) =
        try
            let otherDeviceIds =
                devices
                |> Seq.map _.Id
                |> Seq.distinct
                |> Seq.filter ((<>) chosen.Id)
                |> Seq.toArray

            Array.append [| chosen.Id |] otherDeviceIds
            |> File.writeAllLinesSafe configPath
        with e ->
            Log.warn $"[Vulkan] Failed to write device config file '{configPath}': {e.Message}"

    /// Selects the device named by EnvVar.AARDVARK_VULKAN_DEVICE.
    /// Returns None if the variable is not set or if its value matches no device.
    static let tryChooseByEnvironment (devices: PhysicalDevice[]) =
        match EnvVar.AARDVARK_VULKAN_DEVICE with
        | value when EnvVar.IsSet value ->
            let chosen =
                match value.Trim().ToLowerInvariant() with
                | "integrated" -> devices |> Array.tryFind (fun d -> d.Type = VkPhysicalDeviceType.IntegratedGpu)
                | "discrete" | "dedicated" -> devices |> Array.tryFind (fun d -> d.Type = VkPhysicalDeviceType.DiscreteGpu)
                | name -> devices |> Array.tryFind (fun d -> d.Name.ToLowerInvariant().Contains name)

            match chosen with
            | Some device ->
                Log.line "[Vulkan] AARDVARK_VULKAN_DEVICE=%s -> %s" value device.Name
                Some device

            | None ->
                Log.warn "[Vulkan] AARDVARK_VULKAN_DEVICE=%s matches no device, available devices are:" value
                for d in devices do
                    Log.warn "[Vulkan]     %s (%A)" d.Name d.Type
                None

        | _ ->
            None

    /// Returns whether stored choice should be ignored even if available.
    abstract member IgnoreCache : bool
    default _.IgnoreCache = false

    /// Returns whether the AARDVARK_VULKAN_DEVICE environment variable may override the choice.
    abstract member AllowEnvironmentOverride : bool
    default _.AllowEnvironmentOverride = true

    /// Returns a device to use from the given array of devices.
    abstract member Choose : devices: PhysicalDevice[] -> PhysicalDevice

    member this.Run(devices: PhysicalDevice[]) =
        if devices.Length = 0 then
            failwithf "[Vulkan] No devices to select from"

        else
            // The environment override is not cached, so it does not change the
            // stored choice of later runs without the variable.
            let fromEnvironment =
                if this.AllowEnvironmentOverride then tryChooseByEnvironment devices
                else None

            match fromEnvironment with
            | Some device ->
                device

            | None ->
                if devices.Length = 1 then
                    devices.[0]
                else
                    let chosen =
                        if this.IgnoreCache then
                            this.Choose devices
                        else
                            tryReadConfig devices
                            |> Option.defaultWith (fun _ -> this.Choose devices)

                    writeConfig chosen devices
                    chosen

    interface IDeviceChooser with
        member this.Run(devices) = this.Run(devices)