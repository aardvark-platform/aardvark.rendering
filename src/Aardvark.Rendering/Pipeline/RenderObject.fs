namespace Aardvark.Rendering

open System
open FSharp.Data.Adaptive
open Aardvark.Base

open Aardvark.Rendering

[<AutoOpen>]
module private RenderObjectHelpers =
    let nopActivate () = Disposable.empty

type IRenderObject =
    abstract member Id : int
    abstract member RenderPass : RenderPass
    abstract member AttributeScope : Ag.Scope

type IPreparedRenderObject =
    inherit IRenderObject
    inherit IDisposable

    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Original : Option<RenderObject>

and [<CustomEquality; CustomComparison>] RenderObject =
    {
        Id : int
        mutable AttributeScope : Ag.Scope

        mutable IsActive            : aval<bool>
        mutable RenderPass          : RenderPass

        mutable DrawCalls           : DrawCalls
        mutable Mode                : IndexedGeometryMode

        mutable Surface             : Surface

        mutable DepthState          : DepthState
        mutable BlendState          : BlendState
        mutable StencilState        : StencilState
        mutable RasterizerState     : RasterizerState

        mutable Indices             : Option<BufferView>
        mutable InstanceAttributes  : IAttributeProvider
        mutable VertexAttributes    : IAttributeProvider

        mutable Uniforms            : IUniformProvider

        mutable Activate            : unit -> IDisposable
    }
    interface IRenderObject with
        member x.Id = x.Id
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    member x.Path =
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.Scope.Root)
        then "EMPTY"
        else string x.AttributeScope

    static member Create() =
        { Id = newId()
          AttributeScope = Ag.Scope.Root
          IsActive = Unchecked.defaultof<_>
          RenderPass = RenderPass.main
          DrawCalls = Unchecked.defaultof<_>

          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthState = Unchecked.defaultof<_>
          BlendState = Unchecked.defaultof<_>
          StencilState = Unchecked.defaultof<_>
          RasterizerState = Unchecked.defaultof<_>
          Indices = None
          InstanceAttributes = Unchecked.defaultof<_>
          VertexAttributes = Unchecked.defaultof<_>
          Uniforms = Unchecked.defaultof<_>
          Activate = nopActivate
        }

    static member Clone(org : RenderObject) =
        { org with Id = newId() }

    override x.GetHashCode() = x.Id
    override x.Equals o =
        match o with
            | :? RenderObject as o -> x.Id = o.Id
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? RenderObject as o -> compare x.Id o.Id
                | _ -> failwith "uncomparable"

[<AutoOpen>]
module RenderObjectExtensions =

    let private empty =
        { Id = -1
          AttributeScope = Ag.Scope.Root
          IsActive = Unchecked.defaultof<_>
          RenderPass = RenderPass.main
          DrawCalls = Unchecked.defaultof<_>
          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthState = Unchecked.defaultof<_>
          BlendState = Unchecked.defaultof<_>
          StencilState = Unchecked.defaultof<_>
          RasterizerState = Unchecked.defaultof<_>
          Indices = None
          InstanceAttributes = AttributeProvider.Empty
          VertexAttributes = AttributeProvider.Empty
          Uniforms = UniformProvider.Empty
          Activate = nopActivate
        }

    type RenderObject with
        static member Empty =
            empty

        member x.IsValid =
            x.Id >= 0


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