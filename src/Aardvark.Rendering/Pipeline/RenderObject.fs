namespace Aardvark.Rendering

open System
open System.Threading
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

[<AutoOpen>]
module private RenderObjectHelpers =
    let nopActivate () = Disposable.empty

/// Unique ID for render objects.
[<Struct; StructuredFormatDisplay("{AsString}")>]
type RenderObjectId private (value : int) =
    static let mutable currentId = 0

    static member New() = RenderObjectId(Interlocked.Increment(&currentId))
    static member op_Explicit(id : RenderObjectId) = id.Value

    member private x.Value = value
    member private x.AsString = x.ToString()
    override x.ToString() = string value

type IRenderObject =
    abstract member Id : RenderObjectId
    abstract member RenderPass : RenderPass
    abstract member AttributeScope : Ag.Scope

type IPreparedRenderObject =
    inherit IRenderObject
    inherit IDisposable

    abstract member Update : AdaptiveToken * RenderToken -> unit
    abstract member Original : Option<RenderObject>

and RenderObject private (id : RenderObjectId,
                          attributeScope : Ag.Scope, isActive : aval<bool>, renderPass : RenderPass, drawCalls : DrawCalls, mode : IndexedGeometryMode,
                          surface : Surface, depthState : DepthState, blendState : BlendState, stencilState : StencilState, rasterizerState : RasterizerState,
                          indices : BufferView option, instanceAttributes : IAttributeProvider, vertexAttributes : IAttributeProvider,
                          uniforms : IUniformProvider, activate : unit -> IDisposable) =
    static let empty =
        RenderObject(
            id                 = RenderObjectId(),
            attributeScope     = Ag.Scope.Root,
            isActive           = AVal.constant true,
            renderPass         = RenderPass.main,
            drawCalls          = DrawCalls.Direct (AVal.constant Array.empty),
            mode               = IndexedGeometryMode.TriangleList,
            surface            = Surface.None,
            depthState         = DepthState.Default,
            blendState         = BlendState.Default,
            stencilState       = StencilState.Default,
            rasterizerState    = RasterizerState.Default,
            indices            = None,
            instanceAttributes = AttributeProvider.Empty,
            vertexAttributes   = AttributeProvider.Empty,
            uniforms           = UniformProvider.Empty,
            activate           = nopActivate
        )

    member val Id                 = id
    member val AttributeScope     = attributeScope     with get, set
    member val IsActive           = isActive           with get, set
    member val RenderPass         = renderPass         with get, set
    member val DrawCalls          = drawCalls          with get, set
    member val Mode               = mode               with get, set
    member val Surface            = surface            with get, set
    member val DepthState         = depthState         with get, set
    member val BlendState         = blendState         with get, set
    member val StencilState       = stencilState       with get, set
    member val RasterizerState    = rasterizerState    with get, set
    member val Indices            = indices            with get, set
    member val InstanceAttributes = instanceAttributes with get, set
    member val VertexAttributes   = vertexAttributes   with get, set
    member val Uniforms           = uniforms           with get, set
    member val Activate           = activate           with get, set

    private new(id : RenderObjectId, other : RenderObject) =
        RenderObject(
            id                 = id,
            attributeScope     = other.AttributeScope,
            isActive           = other.IsActive,
            renderPass         = other.RenderPass,
            drawCalls          = other.DrawCalls,
            mode               = other.Mode,
            surface            = other.Surface,
            depthState         = other.DepthState,
            blendState         = other.BlendState,
            stencilState       = other.StencilState,
            rasterizerState    = other.RasterizerState,
            indices            = other.Indices,
            instanceAttributes = other.InstanceAttributes,
            vertexAttributes   = other.VertexAttributes,
            uniforms           = other.Uniforms,
            activate           = other.Activate
        )

    /// Creates an empty render object with a unique id.
    new() =
        RenderObject(id = RenderObjectId.New(), other = empty)

    /// Creates a copy of the given render object.
    /// Note: The copy will have the same id as the original.
    new(other : RenderObject) =
        RenderObject(id = other.Id, other = other)

    /// Clones the given render object and assigns a newly generated id.
    static member Clone(ro : RenderObject) =
        RenderObject(id = RenderObjectId.New(), other = ro)

    member x.Path =
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.Scope.Root) then "EMPTY"
        else string x.AttributeScope

    /// Tries to retrieve the buffer view of the given attribute and returns if it is and per-instance attribute.
    member x.TryGetAttribute(name : Symbol) : struct (BufferView * bool) voption =
        match x.VertexAttributes.TryGetAttribute name with
        | ValueSome value -> ValueSome (value, false)
        | _  ->
            if x.InstanceAttributes <> null then
                match x.InstanceAttributes.TryGetAttribute name with
                | ValueSome value -> ValueSome (value, true)
                | _ -> ValueNone
            else
                ValueNone

    override x.GetHashCode() = int x.Id
    override x.Equals o =
        match o with
        | :? RenderObject as o -> x.Id = o.Id
        | _ -> false

    interface IEquatable<RenderObject> with
        member x.Equals(other) = (x.Id = other.Id)

    interface IComparable<RenderObject> with
        member x.CompareTo(other) = compare x.Id other.Id

    interface IComparable with
        member x.CompareTo o =
            match o with
            | :? RenderObject as o -> compare x.Id o.Id
            | _ -> failwith "uncomparable"

    interface IRenderObject with
        member x.Id = x.Id
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope


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