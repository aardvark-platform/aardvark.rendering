namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

type SingleValueBuffer(value : aval<V4f>) =
    inherit AVal.AbstractVal<IBuffer>()

    member x.Value = value

    new() = SingleValueBuffer(AVal.constant V4f.Zero)

    override x.Compute(token) =
        let v = value.GetValue token
        ArrayBuffer [|v|] :> IBuffer

    override x.GetHashCode() = value.GetHashCode()
    override x.Equals o =
        match o with
        | :? SingleValueBuffer as o -> value = o.Value
        | _ -> false