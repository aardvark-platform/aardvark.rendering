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

module ``Core Tests`` =
    
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


