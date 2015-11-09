namespace Aardvark.Base

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

[<AbstractClass; Sealed; Extension>]
type RuntimeExtensions private() =
    
    [<Extension>]
    static member CompileClear(this : IRuntime, color : IMod<C4f>, depth : IMod<float>) =
        this.CompileClear(color |> Mod.map Some, depth |> Mod.map Some)

    [<Extension>]
    static member CompileClear(this : IRuntime, color : IMod<C4f>) =
        this.CompileClear(color |> Mod.map Some, Mod.constant None)

    [<Extension>]
    static member CompileClear(this : IRuntime, depth : IMod<float>) =
        this.CompileClear(Mod.constant None, depth |> Mod.map Some)