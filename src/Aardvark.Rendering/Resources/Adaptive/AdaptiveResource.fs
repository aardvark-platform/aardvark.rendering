namespace Aardvark.Rendering

open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

/// Interface for adaptive reference-counted resources.
type IAdaptiveResource =
    inherit IAdaptiveValue

    /// Increases the reference count and creates the resource if necessary.
    abstract member Acquire : unit -> unit

    /// Decreases the reference count and destroys the resource if it is no longer used.
    abstract member Release : unit -> unit

    /// Resets the reference count and destroys the resource.
    abstract member ReleaseAll : unit -> unit

    /// Gets the resource handle.
    abstract member GetValue : AdaptiveToken * RenderToken -> obj

/// Generic interface for adaptive reference-counted resources.
type IAdaptiveResource<'a> =
    inherit IAdaptiveValue<'a>
    inherit IAdaptiveResource

    /// Gets the resource handle.
    abstract member GetValue : AdaptiveToken * RenderToken -> 'a

/// Base class for adaptive reference-counted resources.
[<AbstractClass>]
type AdaptiveResource<'a>() =
    inherit AdaptiveObject()
    let mutable cache = Unchecked.defaultof<'a>
    let mutable refCount = 0

    /// Called when the resource is first acquired.
    abstract member Create : unit -> unit

    // Called when the resource is released.
    abstract member Destroy : unit -> unit

    // Computes and returns the resource handle.
    abstract member Compute : AdaptiveToken * RenderToken -> 'a

    member x.Acquire() =
        if Interlocked.Increment(&refCount) = 1 then
            x.Create()

    member x.Release() =
        if Interlocked.Decrement(&refCount) = 0 then
            x.Destroy()
            transact x.MarkOutdated

    member x.ReleaseAll() =
        if Interlocked.Exchange(&refCount, 0) > 0 then
            x.Destroy()

    member x.GetValue(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token (fun token ->
            if x.OutOfDate then
                cache <- x.Compute (token, rt)
            cache
        )

    member x.GetValue(token : AdaptiveToken) =
        x.GetValue(token, RenderToken.Empty)

    interface IAdaptiveValue with
        member x.IsConstant = false
        member x.ContentType = typeof<'a>
        member x.GetValueUntyped(c) = x.GetValue(c) :> obj
        member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x

    interface IAdaptiveValue<'a> with
        member x.GetValue(c) = x.GetValue(c)

    interface IAdaptiveResource with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.ReleaseAll() = x.ReleaseAll()
        member x.GetValue(c,t) = x.GetValue(c,t) :> obj

    interface IAdaptiveResource<'a> with
        member x.GetValue(c,t) = x.GetValue(c,t)

[<AbstractClass; Sealed; Extension>]
type IAdaptiveResourceExtensions() =

    [<Extension>]
    static member GetValue(this : aval<'a>, c : AdaptiveToken, t : RenderToken) =
        match this with
        | :? IAdaptiveResource<'a> as x -> x.GetValue(c, t)
        | _ -> this.GetValue(c)

    [<Extension>]
    static member Acquire(this : aval<'a>) =
        match this with
        | :? IAdaptiveResource as o -> o.Acquire()
        | _ -> ()

    [<Extension>]
    static member Release(this : aval<'a>) =
        match this with
        | :? IAdaptiveResource as o -> o.Release()
        | _ -> ()

    [<Extension>]
    static member ReleaseAll(this : aval<'a>) =
        match this with
        | :? IAdaptiveResource as o -> o.ReleaseAll()
        | _ -> ()