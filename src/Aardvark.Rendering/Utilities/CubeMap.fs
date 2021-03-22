namespace Aardvark.Rendering

open Aardvark.Base
open System.Collections
open System.Collections.Generic

/// Struct to manage data of a mipmapped cube, where each
/// mip level on each face holds a data element.
[<Struct>]
type CubeMap<'a> =

    /// The array holding the elements. Each consecutive six elements
    /// represent a mip level, face indices within a level are determined by
    /// the CubeSide enumeration.
    val Data : 'a[]

    /// Creates a new cube map with the given number of levels.
    new(levels : int) =
        { Data = Array.zeroCreate (6 * levels) }

    /// Creates a new cube map from an existing one, appending and removing levels
    /// according to the new number of levels.
    new(other : CubeMap<'a>, levels : int) =
        let data = Array.zeroCreate (6 * levels)

        if not other.IsEmpty then
            let n = min data.Length other.Data.Length
            for i in 0 .. n - 1 do
                data.[i] <- other.Data.[i]

        { Data = data }

    /// Creates a new cube map from the given sequence. Each consecutive six elements
    /// represent a mip level, face indices within a level are determined by
    /// the CubeSide enumeration.
    new(data : seq<'a>) =
        let arr = Array.ofSeq data

        if arr.Length % 6 <> 0 then
            failwithf "length of data must be a multiple of 6 (is %d)" arr.Length

        { Data = arr }

    /// The number of levels.
    member x.Levels = x.Data.Length / 6

    /// Returns whether the cube map is empty.
    member x.IsEmpty = x.Data.Length = 0

    member x.Item
        with get(face : CubeSide, level : int) =
            x.Data.[level * 6 + int face]
        and set (face : CubeSide, level : int) (value : 'a) =
            x.Data.[level * 6 + int face] <- value

    member x.Item
        with get(face : CubeSide) =
            x.Data.[int face]
        and set (face : CubeSide) (value : 'a) =
            x.Data.[int face] <- value

    member x.TryItem(face : CubeSide, level : int) =
        let i = int face + level * 6
        x.Data |> Array.tryItem i

    member x.TryItem(face : CubeSide) =
        x.TryItem(face, 0)

    interface IEnumerable<'a> with
        member x.GetEnumerator() : IEnumerator<'a> =
            (x.Data :> seq<'a>).GetEnumerator()

        member x.GetEnumerator() : IEnumerator =
            x.Data.GetEnumerator()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CubeMap =

    /// Empty cube map.
    let empty<'a> = CubeMap<'a>([||])

    /// Returns whether the cube map is empty.
    let isEmpty (c : CubeMap<'a>) = c.IsEmpty

    /// Gets the array holding the data. Each consecutive six elements
    /// represent a mip level, face indices within a level are determined by
    /// the CubeSide enumeration.
    let data (c : CubeMap<'a>) = c.Data

    /// Creates a cube map given the number of levels and a generator function to compute the elements.
    let init (levels : int) (f : CubeSide -> int -> 'a) =
        let data = Array.init (6 * levels) (fun i -> f (unbox (i % 6)) (i / 6))
        CubeMap(data)

    /// Creates a cube map given the number of levels and a single value.
    let single (levels : int) (value : 'a) =
        init levels (fun _ _ -> value)

    /// Builds a new cube map whose elements are the results of applying the given
    /// function to each of the elements of the cube.
    let mapi (f : CubeSide -> int -> 'a -> 'b) (c : CubeMap<'a>) =
        CubeMap(c.Data |> Array.mapi (fun i -> f (unbox (i % 6)) (i / 6)))

    /// Builds a new cube map whose elements are the results of applying the given
    /// function to each of the elements of the cube.
    let map (f : 'a -> 'b) (c : CubeMap<'a>) =
        c |> mapi (fun _ _ x -> f x)

    /// Builds a new cube map whose elements are the results of applying the given
    /// function to pairs of elements of each cube.
    let mapi2 (f : CubeSide -> int -> 'a -> 'b -> 'c) (a : CubeMap<'a>) (b : CubeMap<'b>) =
        CubeMap(Array.mapi2 (fun i -> f (unbox (i % 6)) (i / 6)) a.Data b.Data)

    /// Builds a new cube map whose elements are the results of applying the given
    /// function to pairs of elements of each cube.
    let map2 (f : 'a -> 'b -> 'c) (a : CubeMap<'a>) (b : CubeMap<'b>) =
        mapi2 (fun _ _ x y -> f x y) a b

    /// Applies the given function to each element of the cube map.
    let iteri (f : CubeSide -> int -> 'a -> unit) (c : CubeMap<'a>) =
        c.Data |> Array.iteri (fun i -> f (unbox (i % 6)) (i / 6))

    /// Applies the given function to each element of the cube map.
    let iter (f : 'a -> unit) (c : CubeMap<'a>) =
        c |> iteri (fun _ _ x -> f x)

    /// Applies the given function to pairs of elements of each cube map.
    let iteri2 (f : CubeSide -> int -> 'a -> 'b -> unit) (a : CubeMap<'a>) (b : CubeMap<'b>) =
        Array.iteri2 (fun i -> f (unbox (i % 6)) (i / 6)) a.Data b.Data

    /// Applies the given function to pairs of elements of each cube map.
    let iter2 (f : 'a -> 'b -> unit) (a : CubeMap<'a>) (b : CubeMap<'b>) =
        iteri2 (fun _ _ x y -> f x y) a b
