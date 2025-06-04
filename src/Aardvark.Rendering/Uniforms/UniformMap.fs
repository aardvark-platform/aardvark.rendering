namespace Aardvark.Rendering

open System
open System.Collections.Generic
open Aardvark.Base
open FSharp.Data.Adaptive

/// Type representing an IUniformProvider exposing methods
/// for adding resources in a type-safe manner, performing necessary casts and conversions.
type UniformMap =
    private { Values : Map<Symbol, IAdaptiveValue> }

    interface IUniformProvider with
        member x.Dispose() = ()
        member x.TryGetUniform(_, name) = x.Values |> Map.tryFindV name

    static member Empty = UniformMapEmpty.Empty

    static member union (l : UniformMap) (r : UniformMap) =
        UniformMap.ofMap (Map.union l.Values r.Values)

    // ================================================================================================================
    // Static creators
    // ================================================================================================================

    static member ofMap (values : Map<Symbol, IAdaptiveValue>) =
        { Values = values }

    static member inline ofSymDict (values : SymbolDict<IAdaptiveValue>) =
        UniformMap.ofMap (SymDict.toMap values)

    static member inline ofDict (values : Dict<Symbol, IAdaptiveValue>) =
        UniformMap.ofMap (Dict.toMap values)

    static member inline ofList (values : (Symbol * IAdaptiveValue) list) =
        UniformMap.ofMap (Map.ofList values)

    static member inline ofArray (values : (Symbol * IAdaptiveValue) array) =
        UniformMap.ofMap (Map.ofArray values)

    static member inline ofSeq (values : (Symbol * IAdaptiveValue) seq) =
        UniformMap.ofMap (Map.ofSeq values)

    static member inline ofDictionary (values : IDictionary<Symbol, IAdaptiveValue>) =
        UniformMap.ofSeq (values |> Seq.map (fun item -> item.Key, item.Value))

    static member inline ofMap (values : Map<string, IAdaptiveValue>) =
        UniformMap.ofSeq (values |> Seq.map (fun item -> Sym.ofString item.Key, item.Value))

    static member inline ofDict (values : Dict<string, IAdaptiveValue>) =
        UniformMap.ofSeq (values |> Seq.map (fun item -> Sym.ofString item.Key, item.Value))

    static member inline ofList (values : (string * IAdaptiveValue) list) =
        UniformMap.ofList (values |> List.map (fun (k, v) -> Sym.ofString k, v))

    static member inline ofArray (values : (string * IAdaptiveValue) array) =
        UniformMap.ofArray (values |> Array.map (fun (k, v) -> Sym.ofString k, v))

    static member inline ofSeq (values : (string * IAdaptiveValue) seq) =
        UniformMap.ofSeq (values |> Seq.map (fun (k, v) -> Sym.ofString k, v))

    static member inline ofDictionary (values : IDictionary<string, IAdaptiveValue>) =
        UniformMap.ofSeq (values |> Seq.map (fun item -> Sym.ofString item.Key, item.Value))

    // ================================================================================================================
    // Values
    // ================================================================================================================

    static member value (name : Symbol, value : IAdaptiveValue) =
        fun (u : UniformMap) -> { Values = u.Values |> Map.add name value }

    static member inline value (name : string, value : IAdaptiveValue) =
        UniformMap.value (Sym.ofString name, value)

    static member inline value<'T when 'T : unmanaged> (name : TypedSymbol<'T>, value : aval<'T>) =
        UniformMap.value (name.Symbol, value)

    static member inline value<'T when 'T : unmanaged> (name : Symbol, value : 'T) =
        UniformMap.value (name, AVal.constant value)

    static member inline value<'T when 'T : unmanaged> (name : string, value : 'T) =
        UniformMap.value (Sym.ofString name, value)

    static member inline value<'T when 'T : unmanaged>(name : TypedSymbol<'T>, value : 'T) =
        UniformMap.value (name.Symbol, value)

    // ================================================================================================================
    // Images
    // ================================================================================================================

    static member inline image (name : string, image : aval<#ITextureLevel>) =
        UniformMap.value (name, AdaptiveResource.cast<ITextureLevel> image)

    static member inline image (name : Symbol, image : aval<#ITextureLevel>) =
        UniformMap.value (name, AdaptiveResource.cast<ITextureLevel> image)

    static member inline image (name : string, image : ITextureLevel) =
        UniformMap.image (name, AVal.constant image)

    static member inline image (name : Symbol, image : ITextureLevel) =
        UniformMap.image (name, AVal.constant image)

    // ================================================================================================================
    // Textures
    // ================================================================================================================

    static member inline texture (name : string, texture : aval<#ITexture>) =
        UniformMap.value (name, AdaptiveResource.cast<ITexture> texture)

    static member inline texture (name : Symbol, texture : aval<#ITexture>) =
        UniformMap.value (name, AdaptiveResource.cast<ITexture> texture)

    static member inline texture (name : string, texture : ITexture) =
        UniformMap.texture (name, AVal.constant texture)

    static member inline texture (name : Symbol, texture : ITexture) =
        UniformMap.texture (name, AVal.constant texture)

    // ================================================================================================================
    // Buffers
    // ================================================================================================================

    static member inline buffer (name : string, buffer : aval<#IBuffer>) =
        UniformMap.value (name, AdaptiveResource.cast<IBuffer> buffer)

    static member inline buffer (name : Symbol, buffer : aval<#IBuffer>) =
        UniformMap.value (name, AdaptiveResource.cast<IBuffer> buffer)

    static member inline buffer (name : string, buffer : aval<Array>) =
        UniformMap.value (name, buffer)

    static member inline buffer (name : Symbol, buffer : aval<Array>) =
        UniformMap.value (name, buffer)

    static member inline buffer<'T when 'T : unmanaged> (name : string, buffer : aval<'T[]>) =
        UniformMap.buffer (name, AdaptiveResource.cast<Array> buffer)

    static member inline buffer<'T when 'T : unmanaged> (name : Symbol, buffer : aval<'T[]>) =
        UniformMap.buffer (name, AdaptiveResource.cast<Array> buffer)

    static member inline buffer<'T when 'T : unmanaged> (name : TypedSymbol<'T>, buffer : aval<'T[]>) =
        UniformMap.buffer (name.Symbol, buffer)

    static member inline buffer (name : string, buffer : IBuffer) =
        UniformMap.buffer (name, AVal.constant buffer)

    static member inline buffer (name : Symbol, buffer : IBuffer) =
        UniformMap.buffer (name, AVal.constant buffer)

    static member inline buffer (name : string, buffer : Array) =
        UniformMap.buffer (name, AVal.constant buffer)

    static member inline buffer (name : Symbol, buffer : Array) =
        UniformMap.buffer (name, AVal.constant buffer)

    static member inline buffer<'T when 'T : unmanaged> (name : TypedSymbol<'T>, buffer : 'T[]) =
        UniformMap.buffer (name.Symbol, buffer)


and [<Sealed; AbstractClass>] private UniformMapEmpty() =
    static let empty = UniformMap.ofMap Map.empty<Symbol, IAdaptiveValue>
    static member Empty = empty


[<AutoOpen>]
module ``UniformMap Builder`` =

    type UniformMapBuilder() =

        member x.Yield(()) =
            UniformMap.Empty

        [<CustomOperation("value")>]
        member inline x.Value(s : UniformMap, name : Symbol, value : IAdaptiveValue) = s |> UniformMap.value (name, value)

        [<CustomOperation("value")>]
        member inline x.Value(s : UniformMap, name : string, value : IAdaptiveValue) = s |> UniformMap.value (name, value)

        [<CustomOperation("value")>]
        member inline x.Value(s : UniformMap, name : TypedSymbol<'T>, value : aval<'T>) = s |> UniformMap.value (name, value)

        [<CustomOperation("value")>]
        member inline x.Value<'T when 'T : unmanaged>(s : UniformMap, name : Symbol, value : 'T) = s |> UniformMap.value (name, value)

        [<CustomOperation("value")>]
        member inline x.Value<'T when 'T : unmanaged>(s : UniformMap, name : string, value : 'T) = s |> UniformMap.value (name, value)

        [<CustomOperation("value")>]
        member inline x.Value<'T when 'T : unmanaged>(s : UniformMap, name : TypedSymbol<'T>, value : 'T) = s |> UniformMap.value (name, value)


        [<CustomOperation("image")>]
        member inline x.Image(s : UniformMap, name : string, image : aval<#ITextureLevel>) = s |> UniformMap.image (name, image)

        [<CustomOperation("image")>]
        member inline x.Image(s : UniformMap, name : Symbol, image : aval<#ITextureLevel>) = s |> UniformMap.image (name, image)

        [<CustomOperation("image")>]
        member inline x.Image(s : UniformMap, name : string, image : ITextureLevel) = s |> UniformMap.image (name, image)

        [<CustomOperation("image")>]
        member inline x.Image(s : UniformMap, name : Symbol, image : ITextureLevel) = s |> UniformMap.image (name, image)


        [<CustomOperation("texture")>]
        member inline x.Texture(s : UniformMap, name : string, texture : aval<#ITexture>) = s |> UniformMap.texture (name, texture)

        [<CustomOperation("texture")>]
        member inline x.Texture(s : UniformMap, name : Symbol, texture : aval<#ITexture>) = s |> UniformMap.texture (name, texture)

        [<CustomOperation("texture")>]
        member inline x.Texture(s : UniformMap, name : string, texture : ITexture) = s |> UniformMap.texture (name, texture)

        [<CustomOperation("texture")>]
        member inline x.Texture(s : UniformMap, name : Symbol, texture : ITexture) = s |> UniformMap.texture (name, texture)


        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : string, buffer : aval<#IBuffer>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : Symbol, buffer : aval<#IBuffer>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : string, buffer : aval<Array>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : Symbol, buffer : aval<Array>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer<'T when 'T : unmanaged>(s : UniformMap, name : string, buffer : aval<'T[]>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer<'T when 'T : unmanaged>(s : UniformMap, name : Symbol, buffer : aval<'T[]>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer<'T when 'T : unmanaged>(s : UniformMap, name : TypedSymbol<'T>, buffer : aval<'T[]>) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : string, buffer : IBuffer) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : Symbol, buffer : IBuffer) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : string, buffer : Array) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer(s : UniformMap, name : Symbol, buffer : Array) = s |> UniformMap.buffer (name, buffer)

        [<CustomOperation("buffer")>]
        member inline x.Buffer<'T when 'T : unmanaged>(s : UniformMap, name : TypedSymbol<'T>, buffer : 'T[]) = s |> UniformMap.buffer (name, buffer)

    let uniformMap = UniformMapBuilder()