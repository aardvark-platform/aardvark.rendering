namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base

[<AllowNullLiteral>]
type IDynamicFragment =
    abstract member Statistics : FrameStatistics
    abstract member Append : seq<Instruction> -> int
    abstract member Update : int -> seq<Instruction> -> unit
    abstract member Clear : unit -> unit
    abstract member RunAll : unit -> unit

[<AllowNullLiteral>]
type IDynamicFragment<'a when 'a :> IDynamicFragment<'a>> =
    inherit IDynamicFragment
    abstract member Next : 'a with get, set
    abstract member Prev : 'a with get, set

type IRenderJobSorter =
    inherit IComparer<RenderJob>
    abstract member Add : RenderJob -> unit
    abstract member Remove : RenderJob -> unit

[<AllowNullLiteral>]
type IProgram =
    inherit IDisposable
    abstract member RenderJobs : seq<RenderJob>
    abstract member Resources : ReferenceCountingSet<IChangeableResource>
    abstract member Add : RenderJob -> unit
    abstract member Remove : RenderJob -> unit
    abstract member Run : int * ContextHandle -> FrameStatistics


