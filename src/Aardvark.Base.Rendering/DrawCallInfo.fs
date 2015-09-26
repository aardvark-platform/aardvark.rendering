namespace Aardvark.Base

open System

type DrawCallInfo =
    struct
        val mutable public FirstIndex : int
        val mutable public FaceVertexCount : int
        val mutable public FirstInstance : int
        val mutable public InstanceCount : int
        val mutable public BaseVertex : int

        [<Obsolete("will not be respected by the backend!!! Use RenderNode.Mode instead")>]
        val mutable public Mode : IndexedGeometryMode
    end

