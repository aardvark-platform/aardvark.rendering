namespace Aardvark.Application

open System
open Aardvark.Base

type IApplication =
    inherit IDisposable
    abstract member Runtime : IRuntime
    abstract member Initialize : IRenderControl -> int -> unit
    