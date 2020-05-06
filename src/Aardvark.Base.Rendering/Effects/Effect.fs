namespace Aardvark.Base.Rendering

open Aardvark.Base
open FShade
open Microsoft.FSharp.Quotations

[<AutoOpen>]
module EffectAPI =
    type private Effect = list<FShadeEffect>

    type EffectBuilder() =
        member x.Bind(f : 'a -> Expr<'b>, c : unit -> Effect) : Effect =
            let effect = toEffect f
            effect :: c()

        member x.Bind(f : FShadeEffect, c : unit -> Effect) : Effect =
            f::c()


        member x.Return (u : unit) : Effect = []

        member x.Zero() : Effect = []

        member x.Combine(l : Effect, r : unit -> Effect) : Effect = 
            l @ r()

        member x.Delay(f : unit -> Effect) = f

        member x.For(seq : seq<'a>, f : 'a -> Effect) : Effect =
            seq |> Seq.toList |> List.collect f

        member x.Run(f : unit -> Effect) = f()


    let effect = EffectBuilder()