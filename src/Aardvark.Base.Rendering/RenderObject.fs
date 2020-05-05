namespace Aardvark.Base

open System
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering

[<AutoOpen>]
module private RenderObjectHelpers =
    let private nopDisposable = { new IDisposable with member x.Dispose() = () }
    let nopActivate () = nopDisposable

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

        mutable DepthTest           : aval<DepthTestMode>
        mutable DepthBias           : aval<DepthBiasState>
        mutable CullMode            : aval<CullMode>
        mutable FrontFace           : aval<WindingOrder>
        mutable BlendMode           : aval<BlendMode>
        mutable FillMode            : aval<FillMode>
        mutable StencilMode         : aval<StencilMode>

        mutable Indices             : Option<BufferView>
        mutable InstanceAttributes  : IAttributeProvider
        mutable VertexAttributes    : IAttributeProvider

        mutable Uniforms            : IUniformProvider

        mutable ConservativeRaster  : aval<bool>
        mutable Multisample         : aval<bool>

        mutable Activate            : unit -> IDisposable
        mutable WriteBuffers        : Option<Set<Symbol>>

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
          DepthTest = Unchecked.defaultof<_>
          DepthBias = Unchecked.defaultof<_>
          CullMode = Unchecked.defaultof<_>
          FrontFace = Unchecked.defaultof<_>
          BlendMode = Unchecked.defaultof<_>
          FillMode = Unchecked.defaultof<_>
          StencilMode = Unchecked.defaultof<_>
          Indices = None
          InstanceAttributes = Unchecked.defaultof<_>
          VertexAttributes = Unchecked.defaultof<_>
          Uniforms = Unchecked.defaultof<_>
          ConservativeRaster = Unchecked.defaultof<_>
          Multisample = Unchecked.defaultof<_>
          Activate = nopActivate
          WriteBuffers = None
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

    let private emptyUniforms =
        { new IUniformProvider with
            member x.TryGetUniform (_,_) = None
            member x.Dispose() = ()
        }

    let private emptyAttributes =
        { new IAttributeProvider with
            member x.TryGetAttribute(name : Symbol) = None
            member x.All = Seq.empty
            member x.Dispose() = ()
        }

    let private empty =
        { Id = -1
          AttributeScope = Ag.Scope.Root
          IsActive = Unchecked.defaultof<_>
          RenderPass = RenderPass.main
          DrawCalls = Unchecked.defaultof<_>
          Mode = IndexedGeometryMode.TriangleList
          Surface = Surface.None
          DepthTest = Unchecked.defaultof<_>
          DepthBias = Unchecked.defaultof<_>
          CullMode = Unchecked.defaultof<_>
          FrontFace = Unchecked.defaultof<_>
          BlendMode = Unchecked.defaultof<_>
          FillMode = Unchecked.defaultof<_>
          StencilMode = Unchecked.defaultof<_>
          Indices = None
          ConservativeRaster = Unchecked.defaultof<_>
          Multisample = Unchecked.defaultof<_>
          InstanceAttributes = emptyAttributes
          VertexAttributes = emptyAttributes
          Uniforms = emptyUniforms
          Activate = nopActivate
          WriteBuffers = None
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