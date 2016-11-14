namespace Aardvark.Rendering.Vulkan

open System
open System.Threading
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base


[<AbstractClass>]
type Resource =
    class
        val mutable public Device : Device

        abstract member IsValid : bool
        default x.IsValid = x.Device.Handle <> 0n

        new(device : Device) = { Device = device }
    end

//module Bla = 
//    type Float32Reinterpretor() =
//        static member (+/+) (r : Float32Reinterpretor, v : int) = 0.0f
//        static member (+/+) (r : Float32Reinterpretor, v : uint32) = 0.0f
//
//    let inline reinterpretFloat32 a = Unchecked.defaultof<Float32Reinterpretor> +/+ a
//
//    let test () =
//        //let err = reinterpretFloat32 "asdas"
//        //let err = reinterpretFloat32 1L
//        let b = reinterpretFloat32 1
//        let c = reinterpretFloat32 1u
//        ()

[<AbstractClass>]
type Resource<'a when 'a : unmanaged and 'a : equality> =
    class
        inherit Resource
        val mutable public Handle : 'a

        override x.IsValid =
            not x.Device.IsDisposed && x.Handle <> Unchecked.defaultof<_>

        new(device : Device, handle : 'a) = 
            { inherit Resource(device); Handle = handle }
    end
    