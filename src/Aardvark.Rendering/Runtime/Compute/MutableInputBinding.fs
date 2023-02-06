namespace Aardvark.Rendering

open FSharp.Data.Adaptive
open Aardvark.Base

open System
open System.Runtime.CompilerServices

/// Mutable variant for compute input bindings.
/// For backwards-compatible only, new code should create input bindings from an IUniformProvider.
type MutableComputeInputBinding internal(shader : IComputeShader) =

    member x.Item
        with set (name : string) (value : obj) = ()

    member x.Flush() =
        ()

    member x.Dispose() =
        ()

    interface IComputeInputBinding with
        member x.Shader = shader

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass; Sealed; Extension>]
type IComputeRuntimeMutableInputBindingExtensions private() =

    /// Creates a mutable input binding for the given shader.
    /// For backwards-compatible only, new code should create input bindings from an IUniformProvider.
    [<Extension>]
    static member NewInputBinding(_runtime : IComputeRuntime, shader : IComputeShader) =
        new MutableComputeInputBinding(shader)