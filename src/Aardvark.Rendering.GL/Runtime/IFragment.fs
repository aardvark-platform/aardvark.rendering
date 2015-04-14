namespace Aardvark.Rendering.GL

open System
open Aardvark.Base

[<AllowNullLiteral>]
type IDynamicFragment =
    abstract member Append : seq<Instruction> -> int
    abstract member Update : int -> seq<Instruction> -> unit
    abstract member Clear : unit -> unit

[<AllowNullLiteral>]
type IDynamicFragment<'a when 'a :> IDynamicFragment<'a>> =
    inherit IDynamicFragment
    abstract member Next : 'a with get, set
    abstract member Prev : 'a with get, set

[<AllowNullLiteral>]
type IProgram =
    inherit IDisposable
    abstract member Add : RenderJob -> unit
    abstract member Remove : RenderJob -> unit
    abstract member Update : RenderJob -> unit
    abstract member Run : Framebuffer * ContextHandle -> FrameStatistics