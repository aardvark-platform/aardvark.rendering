namespace Aardvark.Rendering.CSharp

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open System
open System.Runtime.CompilerServices

[<Extension; AbstractClass; Sealed>]
type UniformMapExtensions =

    // ================================================================================================================
    // Values
    // ================================================================================================================

    [<Extension>]
    static member inline Value(map : UniformMap, name : TypedSymbol<'T>, value : aval<'T>) =
        map |> UniformMap.value (name, value)

    [<Extension>]
    static member inline Value<'T when 'T : unmanaged>(map : UniformMap, name : Symbol, value : 'T) =
        if typeof<IAdaptiveValue>.IsAssignableFrom typeof<'T> then  // C# doesn't care about unmanaged constraint
            map |> UniformMap.value (name, unbox<IAdaptiveValue> value)
        else
            map |> UniformMap.value (name, value)

    [<Extension>]
    static member inline Value<'T when 'T : unmanaged>(map : UniformMap, name : string, value : 'T) =
        map.Value(Sym.ofString name, value)

    [<Extension>]
    static member inline Value<'T when 'T : unmanaged>(map : UniformMap, name : TypedSymbol<'T>, value : 'T) =
        map |> UniformMap.value (name, value)

    // ================================================================================================================
    // Images
    // ================================================================================================================

    [<Extension>]
    static member inline Image<'TextureLevel when 'TextureLevel :> ITextureLevel>(map : UniformMap, name : string, image : aval<'TextureLevel>) =
        map |> UniformMap.image (name, image)

    [<Extension>]
    static member inline Image<'TextureLevel when 'TextureLevel :> ITextureLevel>(map : UniformMap, name : Symbol, image : aval<'TextureLevel>) =
        map |> UniformMap.image (name, image)

    [<Extension>]
    static member inline Image(map : UniformMap, name : string, image : ITextureLevel) =
        map |> UniformMap.image (name, image)

    [<Extension>]
    static member inline Image(map : UniformMap, name : Symbol, image : ITextureLevel) =
        map |> UniformMap.image (name, image)

    // ================================================================================================================
    // Textures
    // ================================================================================================================

    [<Extension>]
    static member inline Texture<'Texture when 'Texture :> ITexture>(map : UniformMap, name : string, texture : aval<'Texture>) =
        map |> UniformMap.texture (name, texture)

    [<Extension>]
    static member inline Texture<'Texture when 'Texture :> ITexture>(map : UniformMap, name : Symbol, texture : aval<'Texture>) =
        map |> UniformMap.texture (name, texture)

    [<Extension>]
    static member inline Texture(map : UniformMap, name : string, texture : ITexture) =
        map |> UniformMap.texture (name, texture)

    [<Extension>]
    static member inline Texture(map : UniformMap, name : Symbol, texture : ITexture) =
        map |> UniformMap.texture (name, texture)

    // ================================================================================================================
    // Buffers
    // ================================================================================================================

    [<Extension>]
    static member inline Buffer<'Buffer when 'Buffer :> IBuffer>(map : UniformMap, name : string, buffer : aval<'Buffer>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer<'Buffer when 'Buffer :> IBuffer>(map : UniformMap, name : Symbol, buffer : aval<'Buffer>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer(map : UniformMap, name : string, buffer : aval<Array>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer(map : UniformMap, name : Symbol, buffer : aval<Array>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer<'T when 'T : unmanaged>(map : UniformMap, name : string, buffer : aval<'T[]>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer<'T when 'T : unmanaged>(map : UniformMap, name : Symbol, buffer : aval<'T[]>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer<'T when 'T : unmanaged>(map : UniformMap, name : TypedSymbol<'T>, buffer : aval<'T[]>) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer(map : UniformMap, name : string, buffer : IBuffer) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer(map : UniformMap, name : Symbol, buffer : IBuffer) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer(map : UniformMap, name : string, buffer : Array) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer(map : UniformMap, name : Symbol, buffer : Array) =
        map |> UniformMap.buffer (name, buffer)

    [<Extension>]
    static member inline Buffer<'T when 'T : unmanaged>(map : UniformMap, name : TypedSymbol<'T>, buffer : 'T[]) =
        map |> UniformMap.buffer (name, buffer)