namespace Aardvark.Base

open System.Threading
open FSharp.Data.Adaptive

type RenderFragment() =

    let mutable refCount = 0

    abstract member Start : unit -> unit
    default x.Start() = ()

    abstract member Stop : unit -> unit
    default x.Stop() = ()

    abstract member Run : AdaptiveToken * RenderToken * OutputDescription -> unit
    default x.Run(_,_,_) = ()

    member x.AddRef() =
        if Interlocked.Increment(&refCount) = 1 then
            x.Start()

    member x.RemoveRef() =
        if Interlocked.Decrement(&refCount) = 0 then
            x.Stop()