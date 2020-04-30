namespace Aardvark.Base

open System.Runtime.InteropServices
open FSharp.Data.Adaptive

[<StructLayout(LayoutKind.Sequential)>]
type DrawCallInfo =
    struct
        val mutable public FaceVertexCount : int
        val mutable public InstanceCount : int
        val mutable public FirstIndex : int
        val mutable public FirstInstance : int
        val mutable public BaseVertex : int
        new(faceVertexCount : int) = {
            FaceVertexCount = faceVertexCount;
            InstanceCount = 1;
            FirstIndex = 0;
            FirstInstance = 0;
            BaseVertex = 0;
        }
    end

type DrawCalls =
    | Direct of aval<list<DrawCallInfo>> // F# list seriously !?
    | Indirect of aval<IndirectBuffer>
