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


module private RenderJobIds =
    open System.Threading
    let mutable private currentId = 0
    let newId() = Interlocked.Increment &currentId

[<CustomEquality>]
[<CustomComparison>]
type RenderJob =
    {
        Id : int
        CreationPath : string
        mutable AttributeScope : Ag.Scope
                
        mutable IsActive : IMod<bool>
        mutable RenderPass : IMod<uint64>
                
        mutable DrawCallInfo : IMod<DrawCallInfo>
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

    static member Create(path : string) =
        { Id = RenderJobIds.newId()
          CreationPath = path;
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = null
          DrawCallInfo = null
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

    static member Create() = RenderJob.Create("UNKNWON")

    override x.GetHashCode() = x.Id
    override x.Equals o =
        match o with
            | :? RenderJob as o -> x.Id = o.Id
            | _ -> false

    interface IComparable with
        member x.CompareTo o =
            match o with
                | :? RenderJob as o -> compare x.Id o.Id
                | _ -> failwith "uncomparable"

[<AutoOpen>]
module RenderJobExtensions =

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
          CreationPath = "EMPTY";
          AttributeScope = Ag.emptyScope
          IsActive = null
          RenderPass = null
          DrawCallInfo = null
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


    type RenderJob with
        static member Empty =
            empty

        member x.IsValid =
            x.Id >= 0