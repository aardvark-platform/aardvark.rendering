namespace Transparency

open System
open Aardvark.Base
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

type FramebufferInfo =
    {
        size        : aval<V2i>
        samples     : int
        signature   : IFramebufferSignature
    }

type Scene =
    {
        opaque      : ISg
        transparent : ISg
        viewTrafo   : aval<Trafo3d>
        projTrafo   : aval<Trafo3d>
    }

type ITechnique =
    inherit IDisposable

    abstract member Name : string
    abstract member Task : IRenderTask