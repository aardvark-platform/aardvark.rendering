#if INTERACTIVE
#else
namespace Aardvark.Rendering.GL
#endif

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.CompilerServices

[<AutoOpen>]
module Builder =

    type ValidationResult =
        | Valid
        | Invalid of list<string>

    type Validator = bool * string

    type ValidationBuilder() =
        member x.Bind(m : bool * string, _) =
            if m |> fst |> not then
                [m |> snd]
            else
                []

        member x.Return _ = []

        member x.Zero() = []

        member x.For(s : seq<'a>, f : 'a -> list<'b>) =
            s |> Seq.collect f |> Seq.toList

        member x.For(s : seq<'a>, f : 'a -> unit) =
            s |> Seq.iter f
            []

        member x.Delay(f) = f()

        member x.Combine(l, r) = List.append l r
        member x.Combine(l : unit, r) = r
        member x.Combine(l, r : unit) = l

        member x.Run(l : list<string>) =
            match l with
                | [] -> Valid
                | l -> Invalid l


    let requires (b : bool) (err : string) =
        (b, err)

    let eq (a : 'a) (b : 'a) (err : string) =
        (a=b,err)

    let dif (a : 'a) (b : 'a) (err : string) =
        (a<>b,err)

    let validate = ValidationBuilder()
