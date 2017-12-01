namespace Aardvark.Base


open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic


type ExecutionEngine =
    | Interpreter = -1
    | Debug = 0
    | Managed = 1
    | Unmanaged = 2
    | Native = 3

type RedundancyRemoval =
    | None = 0
    | Runtime = 1
    | Static = 2

[<Flags>]
type ResourceSharing =
    | None      = 0x00
    | Buffers   = 0x01
    | Textures  = 0x02
    | Full      = 0x03

type RenderObjectOrder = 
    | Unordered = 0
    | FrontToBack = 1
    | BackToFront = 2

type IDynamicRenderObjectSorter =
    abstract member Add : RenderObject -> unit
    abstract member Remove : RenderObject -> unit
    abstract member SortedList : IMod<list<RenderObject>> 
    abstract member ToSortedRenderObject : RenderObject -> RenderObject

[<CustomEquality; NoComparison>]
type RenderObjectSorting =
    | Arbitrary
    | Dynamic of (Ag.Scope -> IDynamicRenderObjectSorter)
    | Static of cmp : IComparer<IRenderObject>
    | Grouping of projections : (list<RenderObject -> obj>) with

    override x.GetHashCode() =
        match x with
            | Dynamic a -> (a :> obj).GetHashCode()
            | Static a -> a.GetHashCode()
            | Grouping l -> l |> List.fold (fun hc f -> hc ^^^ (f :> obj).GetHashCode()) 0
            | Arbitrary -> 0

    override x.Equals o =
        match o with
            | :? RenderObjectSorting as o ->
                match x, o with
                    | Dynamic x, Dynamic o -> System.Object.Equals(x,o)
                    | Static x, Static o -> x.Equals o
                    | Grouping x, Grouping o -> List.forall2 (fun l r -> System.Object.Equals(l,r)) x o
                    | Arbitrary, Arbitrary -> true
                    | _ -> false 
            | _ ->
                false

type BackendConfiguration = { 
    execution : ExecutionEngine
    redundancy : RedundancyRemoval
    sharing : ResourceSharing
    sorting : RenderObjectSorting
    useDebugOutput : bool
}

module Projections =
    let private empty = obj()

    let surface (rj : RenderObject) =
        rj.Surface :> obj

    let diffuseTexture (rj : RenderObject) =
        match rj.Uniforms.TryGetUniform (rj.AttributeScope, DefaultSemantic.DiffuseColorTexture) with
            | Some t -> t :> obj
            | _ -> empty

    let indices (rj : RenderObject) =
        match rj.Indices with
            | Some i -> i.Buffer :> obj
            | None -> empty

    let standard = [ surface; diffuseTexture; indices ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BackendConfiguration =

    let NativeOptimized = 
        { 
            execution       = ExecutionEngine.Native
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
            useDebugOutput  = false
        }

    let NativeUnoptimized = 
        { 
            execution       = ExecutionEngine.Native
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
            useDebugOutput  = false
        }

    let UnmanagedOptimized = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard
            useDebugOutput  = false
        }

    let UnmanagedRuntime = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.Runtime
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard
            useDebugOutput  = false 
        }

    let UnmanagedUnoptimized = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard
            useDebugOutput  = false 
        }

    let ManagedOptimized = 
        { 
            execution       = ExecutionEngine.Managed
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard
            useDebugOutput  = false 
        }

    let ManagedUnoptimized = 
        { 
            execution       = ExecutionEngine.Managed
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard
            useDebugOutput  = false 
        }

    let Interpreted = 
        { 
            execution       = ExecutionEngine.Interpreter
            redundancy      = RedundancyRemoval.Runtime
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Arbitrary
            useDebugOutput  = false 
        }

    let Debug = 
        { 
            execution       = ExecutionEngine.Debug
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.None
            sorting         = RenderObjectSorting.Grouping []
            useDebugOutput  = true
        }

    let Default = NativeOptimized