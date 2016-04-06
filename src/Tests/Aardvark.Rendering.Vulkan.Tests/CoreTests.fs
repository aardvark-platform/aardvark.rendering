namespace Aardvark.Rendering.Vulkan.Tests

open System
open NUnit.Framework
open FsUnit
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Incremental.Operators
open System.Diagnostics

module ``Instance Tests`` =
    let trace (name : string) (v : 'a) =
        Console.WriteLine("{0}", sprintf "%s: %A" name v)
    
    [<Test>]
    let ``[Instance] properties working``() =
        use i = new Instance()

        i.Extensions |> trace "Extensions"
        i.Handle |> trace "Handle"
        i.Layers |> trace "Layers"
        i.PhysicalDevices |> trace "PhysicalDevices"

    [<Test>]
    let ``[Instance] debug report working``() =
        let mutable msg = None
        use i = new Instance([], [Instance.Extensions.DebugReport])
        i.OnDebugMessage.Add (fun m -> msg <- Some m)
        msg |> should not' (be Null)

    [<Test>]
    let ``[Instance] device creation working``() =
        use i = new Instance()
        use d = i.CreateDevice(i.PhysicalDevices.[0])
        ()

    [<Test>]
    let ``[Instance] disposal working``() =
        let i = new Instance()
        i.PhysicalDevices |> ignore
        i.Dispose()

module ``Device Tests`` =
    let trace (name : string) (v : 'a) =
        Console.WriteLine("{0}", sprintf "%s: %A" name v)

    [<Test>]
    let ``[Device] disposal working``() =
        use i = new Instance()
        let d = i.CreateDevice(i.PhysicalDevices.[0])
        d.Dispose()

    [<Test>]
    let ``[PhysicalDevice] properties working``() =
        use i = new Instance()
        let d = i.PhysicalDevices.[0]
        
        d.Vendor |> trace "Vendor"
        d.Name |> trace "Name"
        d.DeviceId |> trace "DeviceId"
        d.DeviceType |> trace "DeviceType"
        d.Extensions |> trace "Extensions"
        d.Features |> trace "Features"
        d.Handle |> trace "Handle"
        d.Layers |> trace "Layers"
        d.MemoryHeaps |> trace "MemoryHeaps"
        d.MemoryProperties |> trace "MemoryProperties"
        d.MemoryTypes |> trace "MemoryTypes"
        d.Properties |> trace "Properties"
        d.QueueFamilies |> trace "QueueFamilies"
