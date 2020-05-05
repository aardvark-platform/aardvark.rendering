namespace Aardvark.Base

open System
open Aardvark.Base
open FSharp.Data.Adaptive
open System.Collections.Generic
open Aardvark.Base.Rendering
open System.Runtime.CompilerServices

type SignaturelessBackendSurface(runtime : IRuntime, s : ISurface) =
    let cache = Dict<IFramebufferSignature, IBackendSurface>()
    
    interface IDisposableSurface with
        member x.Dispose() = x.Dispose()

    member x.Get(signature : IFramebufferSignature) =
        lock cache (fun () ->
            cache.GetOrCreate(signature, fun signature ->
                runtime.PrepareSurface(signature, s)
            )
        )

    member x.Dispose() =
        lock cache (fun () ->
            cache |> Dict.toSeq |> Seq.iter (fun (_,bs) -> runtime.DeleteSurface bs)
            cache.Clear()        
        )

[<AbstractClass; Sealed; Extension>]
type RuntimeExtensions private() =

    [<Extension>]
    static member PrepareSurface(this : IRuntime, surface : ISurface) =
        new SignaturelessBackendSurface(this, surface) :> IDisposableSurface


[<AbstractClass; Sealed; Extension>]
type IBackendTextureExtensions private() =
    
    /// <summary>
    /// Creates a FramebufferOutput of the texture with the given level and slice.
    /// </summary>
    [<Extension>]
    static member GetOutputView(this : IBackendTexture, level : int, slice : int) =
        { texture = this; level = level; slice = slice } :> IFramebufferOutput

    /// <summary>
    /// Creates a FramebufferOutput of the texture with the given level.
    /// In case the texture is an array or a cube, all items or faces are selected as texture layers.
    /// </summary>
    [<Extension>]
    static member GetOutputView(this : IBackendTexture, level : int) =
        { texture = this; level = level; slice = -1 } :> IFramebufferOutput

    /// <summary>
    /// Creates a FramebufferOutput of the level of the texture. 
    /// In case the texture is an array or a cube, all items or faces are selected as texture layers.
    /// </summary>
    [<Extension>]
    static member GetOutputView(this : IBackendTexture) =
        { texture = this; level = 0; slice = -1 } :> IFramebufferOutput