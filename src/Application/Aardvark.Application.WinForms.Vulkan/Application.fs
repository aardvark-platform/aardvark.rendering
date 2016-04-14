namespace Aardvark.Application.WinForms

open System
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators

module VisualDeviceChooser =
    open System.IO
    open System.Reflection
    open System.Windows.Forms
    open Aardvark.Application

    let private md5 = System.Security.Cryptography.MD5.Create()

    let private newHash() =
        Guid.NewGuid().ToByteArray() |> Convert.ToBase64String

    let private appHash =
        try
            let ass = Assembly.GetEntryAssembly()
            if isNull ass || String.IsNullOrWhiteSpace ass.Location then newHash()
            else
                ass.Location 
                    |> System.Text.Encoding.Unicode.GetBytes
                    |> md5.ComputeHash
                    |> Convert.ToBase64String
                   
        with _ ->
            newHash()
               
    let private configFile =
        let configDir = Path.Combine(Path.GetTempPath(), "vulkan")

        if not (Directory.Exists configDir) then
            Directory.CreateDirectory configDir |> ignore


        let fileName = appHash.Replace('/', '_')
        Path.Combine(configDir, sprintf "%s.vkconfig" fileName)

    let run(devices : list<PhysicalDevice>) =
        match devices with
            | [single] -> single
            | _ -> 
                let allIds = devices |> List.map (fun d -> string d.DeviceId) |> String.concat ";"

                let choose() =
                    let chosen = 
                        devices
                            |> List.mapi (fun i d -> 
                                let name = sprintf "%d: %A %s" i d.Vendor d.Name
                                name, d
                               )
                            |> ChooseForm.run
                    match chosen with
                        | Some d -> 
                            File.WriteAllLines(configFile, [ allIds; string d.DeviceId ])
                            d
                        | None -> 
                            Log.warn "no vulkan device chosen => stopping Application"
                            Environment.Exit 0
                            failwith ""

                if File.Exists configFile && Control.ModifierKeys <> Keys.Alt then
                    let cache = File.ReadAllLines configFile
                    match cache with
                        | [| fAll; fcache |] when fAll = allIds ->
                    
                            let did = UInt32.Parse(fcache)

                            match devices |> List.tryFind (fun d -> d.DeviceId = did) with
                                | Some d -> d
                                | _ -> choose()

                        | _ ->
                            choose()
                else
                    choose()



type VulkanApplication(appName : string, debug : bool, chooseDevice : list<PhysicalDevice> -> PhysicalDevice) =
    let requestedExtensions =
        [
            yield Instance.Extensions.Surface
            yield Instance.Extensions.SwapChain
            yield Instance.Extensions.Win32Surface
            yield Instance.Extensions.XcbSurface
            yield Instance.Extensions.XlibSurface

            if debug then
                yield Instance.Extensions.DebugReport
        ]

    let requestedLayers =
        [
            if debug then
                yield Instance.Layers.SwapChain
                yield Instance.Layers.DrawState
                yield Instance.Layers.ParamChecker
                yield Instance.Layers.StandardValidation
                yield Instance.Layers.DeviceLimits
                yield Instance.Layers.CoreValidation
                yield Instance.Layers.ParameterValidation
                yield Instance.Layers.ObjectTracker
                yield Instance.Layers.Threading
                yield Instance.Layers.UniqueObjects
        ]

    let instance = 
        let availableExtensions =
            Instance.AvailableExtensions |> Seq.map (fun e -> e.extensionName.Value) |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map (fun l -> l.layerName.Value) |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers)
    
        new Instance(appName, Version(1,0,0), enabledLayers, enabledExtensions)

    // install debug output to file (and errors/warnings to console)
    do if debug then
        instance.OnDebugMessage.Add (fun msg ->
            
            let str = sprintf "[%s] %s" msg.layerPrefix msg.message

            match msg.messageFlags with
                | VkDebugReportFlagBitsEXT.VkDebugReportErrorBitExt ->
                    Log.error "%s" str

                | VkDebugReportFlagBitsEXT.VkDebugReportWarningBitExt | VkDebugReportFlagBitsEXT.VkDebugReportPerformanceWarningBitExt ->
                    Log.warn "%s" str

                | VkDebugReportFlagBitsEXT.VkDebugReportInformationBitExt ->
                    Report.Line(4, "{0}", str)

                | _ -> ()

        )


    // choose a physical device
    let physicalDevice = 
        match instance.PhysicalDevices with
            | [] -> failwithf "[Vulkan] could not get vulkan devices"
            | l -> chooseDevice l

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.Extensions |> Seq.map (fun e -> e.extensionName.Value) |> Set.ofSeq

        let availableLayers =
            physicalDevice.Layers |> Seq.map (fun l -> l.layerName.Value) |> Set.ofSeq

        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers)
        
        instance.CreateDevice(physicalDevice, enabledLayers, enabledExtensions)

    let printInfo() =
        
        Log.start "VulkanApplication"

        do  Log.start "instance"

            do  Log.start "layers"
                for l in Instance.AvailableLayers do
                    let layerName = l.layerName.Value
                    let version = l.implementationVersion |> Version.FromUInt32
                    if instance.Layers |> Array.exists (fun li -> li = layerName) then
                        Log.line "* %s (v%A)" layerName version
                    else
                        Log.line "  %s (v%A)" layerName version
                Log.stop()

            do  Log.start "extensions"
                for e in Instance.AvailableExtensions do
                    let extName = e.extensionName.Value
                    let version = e.specVersion |> Version.FromUInt32
                    if instance.Extensions |> Array.exists (fun ei -> ei = extName) then
                        Log.line "* %s (v%A)" extName version
                    else
                        Log.line "  %s (v%A)" extName version
                Log.stop()

            Log.stop()

        do  Log.start "%A %s" physicalDevice.Vendor physicalDevice.Name

            Log.line "kind:    %A" physicalDevice.DeviceType
            Log.line "API:     v%A" (Version.FromUInt32(physicalDevice.Properties.apiVersion))
            Log.line "driver:  v%A" (Version.FromUInt32(physicalDevice.Properties.driverVersion))

            do  Log.start "memory"
                for m in physicalDevice.MemoryHeaps do
                    let suffix =
                        if m.IsDeviceLocal then " (device)"
                        else ""

                    Log.line "memory %d: %A%s" m.HeapIndex m.Size suffix

                Log.stop()

            do  Log.start "layers"
                for l in physicalDevice.Layers do
                    let layerName = l.layerName.Value
                    let version = l.implementationVersion |> Version.FromUInt32
                    if device.Layers |> List.exists (fun li -> li = layerName) then
                        Log.line "* %s (v%A)" layerName version
                    else
                        Log.line "  %s (v%A)" layerName version
                Log.stop()

            do  Log.start "extensions"
                for e in physicalDevice.Extensions do
                    let extName = e.extensionName.Value
                    let version = e.specVersion |> Version.FromUInt32
                    if device.Extensions |> List.exists (fun ei -> ei = extName) then
                        Log.line "* %s (v%A)" extName version
                    else
                        Log.line "  %s (v%A)" extName version
                Log.stop()
            
            Log.stop()

        Log.stop()  


    do printInfo()


    // create a runtime
    let runtime = new Runtime(device)


    member x.Runtime = runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                let impl = new VulkanRenderControl(runtime, samples)
                ctrl.Implementation <- impl

            | _ ->
                failwith "unsupported RenderControl"

    member x.GetRenderPass(ctrl : IRenderControl) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                match ctrl.Implementation with
                    | :? VulkanRenderControl as ctrl -> ctrl.RenderPass
                    | _ -> failwith "unsupported RenderControl"

            | _ ->
                failwith "unsupported RenderControl"


    interface IApplication with
        member x.Runtime = runtime :> _ 
        member x.Initialize(ctrl : IRenderControl, samples : int) =
            x.Initialize(ctrl, samples)

        member x.Dispose() =
            device.Dispose()
            instance.Dispose()


    new(appName, debug) = new VulkanApplication(appName, debug, VisualDeviceChooser.run)
    new(appName) = new VulkanApplication(appName, false)
    new(debug) = new VulkanApplication("Aardvark", debug)
    new() = new VulkanApplication("Aardvark", false)