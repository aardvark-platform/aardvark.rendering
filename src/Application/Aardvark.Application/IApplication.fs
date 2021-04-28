﻿namespace Aardvark.Application

open System
open Aardvark.Rendering
open System.Runtime.CompilerServices

type IApplication =
    inherit IDisposable
    abstract member Runtime : IRuntime
    abstract member Initialize : IRenderControl * samples : int -> unit
    
[<AbstractClass; Sealed; Extension>]
type ApplicationExtensions private() =
    
    [<Extension>]
    static member Initialize(this : IApplication, ctrl : IRenderControl) =
        this.Initialize(ctrl, 1)