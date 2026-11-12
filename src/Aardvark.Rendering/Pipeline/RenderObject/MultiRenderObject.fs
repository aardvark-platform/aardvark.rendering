namespace Aardvark.Rendering

type MultiRenderObject(children : list<IRenderObject>) =
    let first =
        lazy (
            match children with
            | [] -> failwith "[MultiRenderObject] cannot be empty"
            | h::_ -> h
        )

    member x.Children = children

    interface IRenderObject with
        member x.Id = first.Value.Id
        member x.RenderPass = first.Value.RenderPass
        member x.AttributeScope = first.Value.AttributeScope

    override x.GetHashCode() = children.GetHashCode()
    override x.Equals o =
        match o with
        | :? MultiRenderObject as o -> children.Equals(o.Children)
        | _ -> false
