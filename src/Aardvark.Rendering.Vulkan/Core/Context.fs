namespace Aardvark.Rendering.Vulkan

#nowarn "9"
#nowarn "51"

open System
open System.Threading
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base


type Context (d : Device) =
    
    let hostMem = MemoryManager.create d.HostVisibleMemory
    let deviceMem = MemoryManager.create d.DeviceLocalMemory


    member x.Device = d
    member x.HostVisibleMemory = hostMem
    member x.DeviceLocalMemory = deviceMem


