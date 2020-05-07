namespace Aardvark.Base.Rendering

open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive

type IOutputMod =
    inherit IAdaptiveValue
    abstract member Acquire : unit -> unit
    abstract member Release : unit -> unit
    abstract member GetValue : AdaptiveToken * RenderToken -> obj

type IOutputMod<'a> =
    inherit IAdaptiveValue<'a>
    inherit IOutputMod
    abstract member GetValue : AdaptiveToken * RenderToken -> 'a

[<AbstractClass>]
type AbstractOutputMod<'a>() =
    inherit AdaptiveObject()
    let mutable cache = Unchecked.defaultof<'a>
    let mutable refCount = 0

    abstract member Create : unit -> unit
    abstract member Destroy : unit -> unit
    abstract member Compute : AdaptiveToken * RenderToken -> 'a


    member x.Acquire() =
        if Interlocked.Increment(&refCount) = 1 then
            x.Create()

    member x.Release() =
        if Interlocked.Decrement(&refCount) = 0 then
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

    interface IAdaptiveValue<'a> with
        member x.GetValue(c) = x.GetValue(c)

    interface IOutputMod with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.GetValue(c,t) = x.GetValue(c,t) :> obj

    interface IOutputMod<'a> with
        member x.GetValue(c,t) = x.GetValue(c,t)


namespace Aardvark.Base

open FSharp.Data.Adaptive
open Aardvark.Base.Rendering

[<AutoOpen>]
module IAdaptiveValueOutputExtension =

    type IAdaptiveValue<'a> with
        member x.GetValue(c : AdaptiveToken, t : RenderToken) =
            match x with
            | :? IOutputMod<'a> as x -> x.GetValue(c, t)
            | _ -> x.GetValue(c)