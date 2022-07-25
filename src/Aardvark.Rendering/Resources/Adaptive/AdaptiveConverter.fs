namespace Aardvark.Rendering

open Aardvark.Base

open System
open System.Reflection
open FSharp.Data.Adaptive

[<AutoOpen>]
module AdaptivePrimitiveValueConverterExtensions =

    module PrimitiveValueConverter =

        [<AbstractClass>]
        type private AdaptiveConverter<'T>() =
            abstract member Convert : IAdaptiveValue -> aval<'T>
            abstract member Convert : aval<Array> -> aval<'T[]>

        type private AdaptiveConverter<'T1, 'T2> private() =
            inherit AdaptiveConverter<'T2>()
            static let conv = PrimitiveValueConverter.converter<'T1, 'T2>
            static let instance = AdaptiveConverter<'T1, 'T2>()

            static member Instance = instance

            override x.Convert (m : IAdaptiveValue) =
                m |> unbox<aval<'T1>> |> AdaptiveResource.map conv

            override x.Convert (m : aval<Array>) =
                m |> AdaptiveResource.map (fun arr -> arr |> unbox<'T1[]> |> Array.map conv)

        let private staticFieldCache = Dict<Type * string, obj>()
        let private getStaticField (name : string) (t : Type) : 'T =
            staticFieldCache.GetOrCreate((t, name), fun (t, name) ->
                let p = t.GetProperty(name, BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                p.GetValue(null)
            ) |> unbox<'T>

        let convertArray (inputElementType : Type) (m : aval<Array>) : aval<'T[]> =
            if inputElementType = typeof<'T> then
                m |> AdaptiveResource.cast
            else
                let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputElementType; typeof<'T> |]
                let converter : AdaptiveConverter<'T> = tconv |> getStaticField "Instance"
                converter.Convert(m)

        let convertValue (m : IAdaptiveValue) : aval<'T> =
            let inputType = m.ContentType
            if inputType = typeof<'T> then
                unbox m
            else
                let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputType; typeof<'T> |]
                let converter : AdaptiveConverter<'T> = tconv |> getStaticField "Instance"
                converter.Convert(m)