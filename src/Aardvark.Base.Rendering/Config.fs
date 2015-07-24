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

type RenderJobOrder = 
    | Unordered = 0
    | FrontToBack = 1
    | BackToFront = 2

type IDynamicRenderJobSorter =
    abstract member Add : RenderJob -> unit
    abstract member Remove : RenderJob -> unit
    abstract member SortedList : IMod<list<RenderJob>> 
    abstract member ToSortedRenderJob : RenderJob -> RenderJob

[<CustomEquality; NoComparison>]
type RenderJobSorting =
    | Dynamic of (Ag.Scope -> IDynamicRenderJobSorter)
    | Static of cmp : IComparer<RenderJob>
    | Grouping of projections : (list<RenderJob -> IMod>) with

    override x.GetHashCode() =
        match x with
            | Dynamic a -> (a :> obj).GetHashCode()
            | Static a -> a.GetHashCode()
            | Grouping l -> l |> List.fold (fun hc f -> hc ^^^ (f :> obj).GetHashCode()) 0

    override x.Equals o =
        match o with
            | :? RenderJobSorting as o ->
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
    sorting : RenderJobSorting
}

module Projections =
    let private empty = Mod.init () :> IMod

    let surface (rj : RenderJob) =
        rj.Surface :> IMod

    let diffuseTexture (rj : RenderJob) =
        match rj.Uniforms.TryGetUniform (rj.AttributeScope, DefaultSemantic.DiffuseColorCoordinates) with
            | Some t -> t
            | _ -> empty

    let indices (rj : RenderJob) =
        match rj.Indices with
            | null -> empty
            | i -> i :> IMod

    let standard = [ surface; diffuseTexture; indices ]

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BackendConfiguration =

    let native = 
        { 
            execution       = ExecutionEngine.Native
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderJobSorting.Grouping Projections.standard 
        }

    let runtime = 
        { 
            execution       = ExecutionEngine.Unmanaged
            redundancy      = RedundancyRemoval.Runtime
            sharing         = ResourceSharing.Textures
            sorting         = RenderJobSorting.Grouping Projections.standard 
        }

    let managed = 
        { 
            execution       = ExecutionEngine.Managed
            redundancy      = RedundancyRemoval.Static
            sharing         = ResourceSharing.Textures
            sorting         = RenderJobSorting.Grouping Projections.standard 
        }
