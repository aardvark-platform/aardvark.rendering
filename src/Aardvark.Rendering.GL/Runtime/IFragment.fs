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

type IRenderObjectSorter =
    inherit IComparer<IRenderObject>
    abstract member Add : IRenderObject -> unit
    abstract member Remove : IRenderObject -> unit

[<AllowNullLiteral>]
type IRenderProgram =
    inherit IDisposable
    
    abstract member Disassemble : unit -> seq<Instruction>
    abstract member RenderObjects : seq<IRenderObject>

    abstract member Add : IRenderObject -> unit
    abstract member Remove : IRenderObject -> unit

    //abstract member Resources : ReferenceCountingSet<IChangeableResource>

    abstract member Update : int * ContextHandle -> FrameStatistics
    abstract member Run : int * ContextHandle -> FrameStatistics


