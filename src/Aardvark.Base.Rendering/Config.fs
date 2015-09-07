namespace Aardvark.Base


open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic


type ExecutionEngine =
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
    | Dynamic of (Ag.Scope -> IDynamicRenderObjectSorter)
    | Static of cmp : IComparer<IRenderObject>
    | Grouping of projections : (list<RenderObject -> IMod>) with

    override x.GetHashCode() =
        match x with
            | Dynamic a -> (a :> obj).GetHashCode()
            | Static a -> a.GetHashCode()
            | Grouping l -> l |> List.fold (fun hc f -> hc ^^^ (f :> obj).GetHashCode()) 0

    override x.Equals o =
        match o with
            | :? RenderObjectSorting as o ->
                match x, o with
                    | Dynamic x, Dynamic o -> System.Object.Equals(x,o)
                    | Static x, Static o -> x.Equals o
                    | Grouping x, Grouping o -> List.forall2 (fun l r -> System.Object.Equals(l,r)) x o
                    | _ -> false 
            | _ ->
                false

type BackendConfiguration = { 
    execution : ExecutionEngine
    redundancy : RedundancyRemoval
    sharing : ResourceSharing
    sorting : RenderObjectSorting
}

module Projections =
    let private empty = Mod.init () :> IMod

    let surface (rj : RenderObject) =
        rj.Surface :> IMod

    let diffuseTexture (rj : RenderObject) =
        match rj.Uniforms.TryGetUniform (rj.AttributeScope, DefaultSemantic.DiffuseColorCoordinates) with
            | Some t -> t
            | _ -> empty

    let indices (rj : RenderObject) =
        match rj.Indices with
            | null -> empty
            | i -> i :> IMod

    let standard = [ surface; diffuseTexture; indices ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BackendConfiguration =

    let NativeOptimized = 
        { 
            execution       = ExecutionEngine.Native
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let NativeUnoptimized = 
        { 
            execution       = ExecutionEngine.Native
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let UnmanagedOptimized = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let UnmanagedRuntime = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.Runtime
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let UnmanagedUnoptimized = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let ManagedOptimized = 
        { 
            execution       = ExecutionEngine.Managed
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let ManagedUnoptimized = 
        { 
            execution       = ExecutionEngine.Managed
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.Textures
            sorting         = RenderObjectSorting.Grouping Projections.standard 
        }

    let Debug = 
        { 
            execution       = ExecutionEngine.Debug
            redundancy      = RedundancyRemoval.None
            sharing         = ResourceSharing.None
            sorting         = RenderObjectSorting.Grouping []
        }

    let Default = NativeOptimized