﻿namespace Aardvark.Application.WinForms

open System
open System.Diagnostics
open System.Collections.Concurrent
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
            if isNull ass || String.IsNullOrWhiteSpace ass.Location then 
                newHash()
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
                let allIds = devices |> List.map (fun d -> d.Id) |> String.concat ";"

                let choose() =
                    let chosen = 
                        devices
                            |> List.mapi (fun i d -> 
                                let prefix =
                                    match d with
                                        | :? PhysicalDeviceGroup as g -> sprintf "%d x "g.Devices.Length
                                        | _ -> ""
                                let name = sprintf "%s%s %s" prefix d.Vendor d.Name
                                name, d
                               )
                            |> ChooseForm.run
                    match chosen with
                        | Some d -> 
                            File.WriteAllLines(configFile, [ allIds; d.Id ])
                            d
                        | None -> 
                            Log.warn "no vulkan device chosen => stopping Application"
                            Environment.Exit 0
                            failwith ""

                if File.Exists configFile && Control.ModifierKeys <> Keys.Alt then
                    let cache = File.ReadAllLines configFile
                    match cache with
                        | [| fAll; fcache |] when fAll = allIds ->
                    
                            match devices |> List.tryFind (fun d -> d.Id = fcache) with
                                | Some d -> d
                                | _ -> choose()

                        | _ ->
                            choose()
                else
                    choose()



type VulkanApplication(debug : bool, chooseDevice : list<PhysicalDevice> -> PhysicalDevice) =
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
                yield Instance.Layers.Nsight
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
                yield Instance.Layers.Image
        ]

    let instance = 
        let availableExtensions =
            Instance.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map (fun l -> l.name) |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions) |> Set.ofList
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers) |> Set.ofList
    
        new Instance(Version(1,0,0), enabledLayers, enabledExtensions)

    // choose a physical device
    let physicalDevice = 
        if instance.Devices.Length = 0 then
            failwithf "[Vulkan] could not get vulkan devices"
        else
            chooseDevice (Array.toList instance.Devices)

    do instance.PrintInfo(Logger.Get 2, physicalDevice)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let availableLayers =
            physicalDevice.AvailableLayers |> Seq.map (fun l -> l.name) |> Set.ofSeq

        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions) |> Set.ofList
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers) |> Set.ofList
        
        physicalDevice.CreateDevice(enabledLayers, enabledExtensions)

    // create a runtime
    let runtime = new Runtime(device, false, false, debug)

    let canCreateRenderControl =
        Set.contains Instance.Extensions.SwapChain device.EnabledExtensions

    member x.Runtime = runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                if canCreateRenderControl then
                    let mode = GraphicsMode(Col.Format.RGBA, 8, 24, 8, 2, samples, ImageTrafo.MirrorY)
                    let impl = new VulkanRenderControl(runtime, mode)
                    ctrl.Implementation <- impl
                else
                    failwithf "[Vulkan] cannot initialize RenderControl since device-extension (%s) is missing" Instance.Extensions.SwapChain

            | _ ->
                failwithf "[Vulkan] unsupported RenderControl-Type %A" ctrl

    member x.GetRenderPass(ctrl : IRenderControl) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                match ctrl.Implementation with
                    | :? VulkanRenderControl as ctrl -> ctrl.RenderPass
                    | _ -> failwith "unsupported RenderControl"

            | _ ->
                failwith "unsupported RenderControl"

    member x.Dispose() =
        runtime.Dispose()
        device.Dispose()
        instance.Dispose()

    interface IApplication with
        member x.Runtime = runtime :> _ 
        member x.Initialize(ctrl : IRenderControl, samples : int) =
            x.Initialize(ctrl, samples)

        member x.Dispose() = x.Dispose()


    new(debug)      = new VulkanApplication(debug, VisualDeviceChooser.run)
    new(chooser)    = new VulkanApplication(false, chooser)
    new()           = new VulkanApplication(false, VisualDeviceChooser.run)