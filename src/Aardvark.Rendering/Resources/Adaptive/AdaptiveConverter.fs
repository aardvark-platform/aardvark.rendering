namespace Aardvark.Rendering

open Aardvark.Base

open System
open System.Reflection
open FSharp.Data.Adaptive

[<AutoOpen>]
module AdaptivePrimitiveValueConverterExtensions =

    module PrimitiveValueConverter =

        type private IAdaptiveConverter =
            abstract member ConvertUntyped : IAdaptiveValue -> IAdaptiveValue

        type private IAdaptiveConverter<'T> =
            inherit IAdaptiveConverter
            abstract member Convert : IAdaptiveValue -> aval<'T>
            abstract member Convert : aval<Array> -> aval<'T[]>

        type private AdaptiveConverter<'T1, 'T2> private() =
            static let conv = PrimitiveValueConverter.converter<'T1, 'T2>
            static let instance = AdaptiveConverter<'T1, 'T2>()

            static member Instance = instance

            member x.Convert (value : IAdaptiveValue) =
                value |> unbox<aval<'T1>> |> AdaptiveResource.map conv

            member x.Convert (value : aval<Array>) =
                value |> AdaptiveResource.map (fun arr -> arr |> unbox<'T1[]> |> Array.map conv)

            interface IAdaptiveConverter<'T2> with
                member x.Convert(value : IAdaptiveValue) = x.Convert(value)
                member x.ConvertUntyped(value : IAdaptiveValue) = x.Convert(value)
                member x.Convert(array : aval<Array>) = x.Convert(array)

        let private staticFieldCache = Dict<Type * string, obj>()
        let private getStaticField (name : string) (t : Type) : 'T =
            staticFieldCache.GetOrCreate((t, name), fun (t, name) ->
                let p = t.GetProperty(name, BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                p.GetValue(null)
            ) |> unbox<'T>

        let convertArray (inputElementType : Type) (array : aval<Array>) : aval<'T[]> =
            if inputElementType = typeof<'T> then
                array |> AdaptiveResource.mapNonAdaptive unbox
            else
                let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputElementType; typeof<'T> |]
                let converter : IAdaptiveConverter<'T> = tconv |> getStaticField "Instance"
                converter.Convert(array)

        let convertValueUntyped (outputType : Type) (value : IAdaptiveValue) =
            let inputType = value.ContentType
            if inputType = outputType then
                value
            else
                let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputType; outputType |]
                let converter : IAdaptiveConverter = tconv |> getStaticField "Instance"
                converter.ConvertUntyped(value)

        let convertValue (value : IAdaptiveValue) : aval<'T> =
            let inputType = value.ContentType
            if inputType = typeof<'T> then
                unbox value
            else
                let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputType; typeof<'T> |]
                let converter : IAdaptiveConverter<'T> = tconv |> getStaticField "Instance"
                converter.Convert(value)