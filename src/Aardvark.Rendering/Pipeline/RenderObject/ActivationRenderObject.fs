namespace Aardvark.Rendering

open Aardvark.Base

/// A render object that carries nothing to render: backends ignore it for
/// drawing and only invoke its <c>Activate</c>, disposing the returned handle when
/// the object leaves the render task. It lets a producer scope resource lifetime
/// to the union of the tasks rendering it: build on the first activation, tear
/// down when the last task drops the object.
type ActivationRenderObject(pass : RenderPass, scope : Ag.Scope, activate : unit -> System.IDisposable) =
    let id = RenderObjectId.New()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.Activate() = activate()

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
        | :? ActivationRenderObject as o -> id = o.Id
        | _ -> false
