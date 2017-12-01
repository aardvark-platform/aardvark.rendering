namespace Aardvark.Base

#nowarn "9"

open System
open System.Runtime.InteropServices

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

