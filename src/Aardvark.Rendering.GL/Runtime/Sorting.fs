namespace Aardvark.Rendering.GL

open Aardvark.Rendering
open Aardvark.Base
open Aardvark.Base.Incremental

type Order = 
    | Unordered = 0
    | FrontToBack = 1
    | BackToFront = 2
 
type ISorter =
    abstract member Add : RenderJob -> unit
    abstract member Remove : RenderJob -> unit
    abstract member SortedList : IMod<list<RenderJob>> 
    abstract member ToSortedRenderJob : Order -> RenderJob -> RenderJob

module Sorting =
    let mutable private create = fun (scope : Aardvark.Base.Ag.Scope) (o : Order) (u : unit) -> failwith "no sorter registered"

    let registerSorter(createSorter : Aardvark.Base.Ag.Scope -> Order -> unit -> ISorter) =
        create <- createSorter

    let createSorter scope order =
        create scope order
