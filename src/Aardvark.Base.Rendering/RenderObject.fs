namespace Aardvark.Base

open System
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Aardvark.Base.Rendering

[<AllowNullLiteral>]
type ISurface = interface end


[<AllowNullLiteral>]
type IAttributeProvider =
    inherit IDisposable
    abstract member TryGetAttribute : name : Symbol -> Option<BufferView>
    abstract member All : seq<Symbol * BufferView>

[<AllowNullLiteral>]
type IUniformProvider =
    inherit IDisposable
    abstract member TryGetUniform : scope : Ag.Scope * name : Symbol -> Option<IMod>


module private RenderObjectIds =
    open System.Threading
    let mutable private currentId = 0
    let newId() = Interlocked.Increment &currentId

type IRenderObject =
    abstract member RenderPass : IMod<uint64>
    abstract member AttributeScope : Ag.Scope

[<CustomEquality>]
[<CustomComparison>]
type RenderObject =
    {
        Id : int
        mutable AttributeScope : Ag.Scope
                
        mutable IsActive : IMod<bool>
        mutable RenderPass : IMod<uint64>
                
        mutable DrawCallInfo : IMod<DrawCallInfo>
        mutable Mode : IMod<IndexedGeometryMode>
        mutable Surface : IMod<ISurface>
                
        mutable DepthTest : IMod<DepthTestMode>
        mutable CullMode : IMod<CullMode>
        mutable BlendMode : IMod<BlendMode>
        mutable FillMode : IMod<FillMode>
        mutable StencilMode : IMod<StencilMode>
                
        mutable Indices : IMod<Array>
        mutable InstanceAttributes : IAttributeProvider
        mutable VertexAttributes : IAttributeProvider
                
        mutable Uniforms : IUniformProvider
    }  
    interface IRenderObject with
        member x.RenderPass = x.RenderPass
        member x.AttributeScope = x.AttributeScope

    member x.Path = 
        if System.Object.ReferenceEquals(x.AttributeScope,Ag.emptyScope) 
        then "EMPTY" 
        else x.AttributeScope.Path

    static member Create() =
        { Id = RenderObjectIds.newId()
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = null
          DrawCallInfo = null
          Mode = null
          Surface = null
          DepthTest = null
          CullMode = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = null
          InstanceAttributes = null
          VertexAttributes = null
          Uniforms = null
        }

    static member Clone(org : RenderObject) =
        { org with Id = RenderObjectIds.newId() }

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
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = null
          DrawCallInfo = null
          Mode = null
          Surface = null
          DepthTest = null
          CullMode = null
          BlendMode = null
          FillMode = null
          StencilMode = null
          Indices = null
          InstanceAttributes = emptyAttributes
          VertexAttributes = emptyAttributes
          Uniforms = emptyUniforms
        }


    type RenderObject with
        static member Empty =
            empty

        member x.IsValid =
            x.Id >= 0