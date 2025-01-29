namespace Aardvark.Application.WinForms

open System
open System.Diagnostics
open System.Collections.Concurrent
open Aardvark.Application
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System.Runtime.InteropServices

module VisualDeviceChooser =
    open System.Windows.Forms

    let run(devices : list<PhysicalDevice>) =
        match devices with
        | [single] -> single
        | _ ->
            let choose() =
                let chosen =
                    devices
                    |> List.mapi (fun i d ->
                        let prefix =
                            match d with
                            | :? PhysicalDeviceGroup as g -> sprintf "%d x "g.Devices.Length
                            | _ -> ""
                        let name = sprintf "%s%s" prefix d.FullName
                        name, d
                    )
                    |> ChooseForm.run

                match chosen with
                | Some d ->
                    ConsoleDeviceChooser.Config.write d devices
                    d
                | None ->
                    Log.warn "no vulkan device chosen => stopping Application"
                    Environment.Exit 0
                    failwith ""

            if Control.ModifierKeys <> Keys.Alt then
                match ConsoleDeviceChooser.Config.tryRead devices with
                | Some chosen -> chosen
                | _ -> choose()
            else
                choose()



type VulkanApplication(debug : IDebugConfig, chooseDevice : list<PhysicalDevice> -> PhysicalDevice) =

    let requestedExtensions =
        [
            yield Instance.Extensions.Surface
            yield Instance.Extensions.SwapChain
            yield Instance.Extensions.Win32Surface
            yield Instance.Extensions.XcbSurface
            yield Instance.Extensions.XlibSurface

            yield Instance.Extensions.ShaderSubgroupVote
            yield Instance.Extensions.ShaderSubgroupBallot
            yield Instance.Extensions.GetPhysicalDeviceProperties2
            yield Instance.Extensions.ConservativeRasterization

            yield! Instance.Extensions.MemoryBudget
            yield! Instance.Extensions.Raytracing
            yield! Instance.Extensions.Sharing
        ]

    let requestedLayers =
        [
        ]

    let instance = 
        let availableExtensions =
            Instance.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq

        let availableLayers =
            Instance.AvailableLayers |> Seq.map (fun l -> l.name) |> Set.ofSeq

        // create an instance
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)
        let enabledLayers = requestedLayers |> List.filter (fun r -> Set.contains r availableLayers)
    
        new Instance(Version(1,1,0), enabledLayers, enabledExtensions, debug)

    // choose a physical device
    let physicalDevice = 
        if instance.Devices.Length = 0 then
            failwithf "[Vulkan] could not get vulkan devices"
        else
            chooseDevice (Seq.toList (CustomDeviceChooser.Filter instance.Devices))

    do instance.PrintInfo(Logger.Get 4, physicalDevice)

    // create a device
    let device = 
        let availableExtensions =
            physicalDevice.GlobalExtensions |> Seq.map (fun e -> e.name) |> Set.ofSeq
  
        let enabledExtensions = requestedExtensions |> List.filter (fun r -> Set.contains r availableExtensions)

        physicalDevice.CreateDevice(enabledExtensions)

    // create a runtime
    let runtime = new Runtime(device)

    let canCreateRenderControl =
        List.contains Instance.Extensions.SwapChain device.EnabledExtensions

    member x.Runtime = runtime

    member x.Initialize(ctrl : IRenderControl, samples : int) =
        match ctrl with
            | :? Aardvark.Application.WinForms.RenderControl as ctrl ->
                if canCreateRenderControl then
                    let mode = GraphicsMode(Col.Format.RGBA, 8, 24, 8, 2, samples, ImageTrafo.MirrorY, true)
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


    new(debug : bool, chooseDevice : list<PhysicalDevice> -> PhysicalDevice) =
        new VulkanApplication(DebugLevel.ofBool debug, chooseDevice)

    new(debug : IDebugConfig) = new VulkanApplication(debug, VisualDeviceChooser.run)
    new(debug : bool)         = new VulkanApplication(debug, VisualDeviceChooser.run)
    new(chooser)              = new VulkanApplication(DebugLevel.None, chooser)
    new()                     = new VulkanApplication(DebugLevel.None, VisualDeviceChooser.run)