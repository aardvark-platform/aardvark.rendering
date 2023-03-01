namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System

/// Value contained in a SingleValueBuffer.
[<RequireQualifiedAccess>]
type internal SingleValue =
    | Float   of aval<V4f>
    | Integer of aval<V4i>

/// Attribute buffer containing a single value that is repeated for all vertices or instances.
type SingleValueBuffer internal (value : SingleValue) =
    inherit AVal.AbstractVal<IBuffer>()

    static let zero = SingleValueBuffer V4f.Zero

    let buffer =
        match value with
        | SingleValue.Float f -> f |> AVal.map (Array.singleton >> ArrayBuffer)
        | SingleValue.Integer i -> i |> AVal.map (Array.singleton >> ArrayBuffer)

    let value : IAdaptiveValue =
        match value with
        | SingleValue.Float f -> f
        | SingleValue.Integer i -> i

    member x.Value = value

    new(value : aval<V4f>)     = SingleValueBuffer(SingleValue.Float value)
    new(value : aval<V3f>)     = SingleValueBuffer(value |> AVal.mapNonAdaptive v4f)
    new(value : aval<V2f>)     = SingleValueBuffer(value |> AVal.mapNonAdaptive v4f)
    new(value : aval<float32>) = SingleValueBuffer(value |> AVal.mapNonAdaptive (fun v -> V4f(v)))

    new(value : aval<V4i>)   = SingleValueBuffer(SingleValue.Integer value)
    new(value : aval<V3i>)   = SingleValueBuffer(value |> AVal.mapNonAdaptive v4i)
    new(value : aval<V2i>)   = SingleValueBuffer(value |> AVal.mapNonAdaptive v4i)
    new(value : aval<int32>) = SingleValueBuffer(value |> AVal.mapNonAdaptive (fun v -> V4i(v)))

    new(value : V4f)     = SingleValueBuffer(~~value)
    new(value : V3f)     = SingleValueBuffer(~~value)
    new(value : V2f)     = SingleValueBuffer(~~value)
    new(value : float32) = SingleValueBuffer(~~value)

    new(value : V4i)   = SingleValueBuffer(~~value)
    new(value : V3i)   = SingleValueBuffer(~~value)
    new(value : V2i)   = SingleValueBuffer(~~value)
    new(value : int32) = SingleValueBuffer(~~value)

    [<Obsolete("Use SingleValueBuffer.Zero instead.")>]
    new() = SingleValueBuffer(V4f.Zero)

    static member Zero = zero

    override x.Compute(token) : IBuffer =
        buffer.GetValue token

    override x.GetHashCode() = value.GetHashCode()
    override x.Equals o =
        match o with
        | :? SingleValueBuffer as o -> value = o.Value
        | _ -> false