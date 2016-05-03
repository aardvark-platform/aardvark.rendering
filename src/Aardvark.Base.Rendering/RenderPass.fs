namespace Aardvark.Base.Rendering

open System
open Aardvark.Base

type RenderPassOrder =
    | Arbitrary = 0
    | BackToFront = 1
    | FrontToBack = 2

[<CustomEquality; CustomComparison>]
type RenderPass =
    struct
        val mutable public Name : string
        val mutable public Order : RenderPassOrder
        val mutable internal SortKey : SimpleOrder.SortKey

        internal new(name : string, order : RenderPassOrder, key : SimpleOrder.SortKey) = { Name = name; Order = order; SortKey = key }

        override x.GetHashCode() = x.SortKey.GetHashCode()
        override x.Equals o =
            match o with
                | :? RenderPass as o -> x.SortKey = o.SortKey
                | _ -> false

        override x.ToString() =
            sprintf "RenderPass(%s)" x.Name

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? RenderPass as o -> compare x.SortKey o.SortKey
                    | _ -> failwithf "[RenderPass] cannot compare to %A" o

    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RenderPass =

    let private mainOrder = SimpleOrder.create()
    let private mainPass = mainOrder.After mainOrder.Root

    let main = RenderPass("main", RenderPassOrder.Arbitrary, mainPass)

    let after (name : string) (order : RenderPassOrder) (pass : RenderPass) =
        let key = mainOrder.After(pass.SortKey)
        RenderPass(name, order, key)

    let before (name : string) (order : RenderPassOrder) (pass : RenderPass) =
        let key = mainOrder.Before(pass.SortKey)
        RenderPass(name, RenderPassOrder.BackToFront, key)