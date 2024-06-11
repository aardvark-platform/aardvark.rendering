namespace Aardvark.Rendering

open Aardvark.Base

open System
open System.Reflection
open FSharp.Data.Adaptive

[<AutoOpen>]
module AdaptivePrimitiveValueConverterExtensions =

    module PrimitiveValueConverter =

        let rec private (|InvalidConversion|_|) (e : exn) =
            match e with
            | :? PrimitiveValueConverter.InvalidConversionException as e -> Some e
            | _ ->
                match e.InnerException with
                | null -> None
                | InvalidConversion e -> Some e
                | _ -> None

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

        let private staticInstanceCache = Dict<Type, obj>()
        let private getStaticInstance (t : Type) : 'T =
            lock staticInstanceCache (fun () ->
                staticInstanceCache.GetOrCreate(t, fun t ->
                    let p = t.GetProperty("Instance", BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                    p.GetValue(null)
                ) |> unbox<'T>
            )

        let convertArray (inputElementType : Type) (array : aval<Array>) : aval<'T[]> =
            if inputElementType = typeof<'T> then
                array |> AdaptiveResource.mapNonAdaptive unbox
            else
                try
                    let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputElementType; typeof<'T> |]
                    let converter : IAdaptiveConverter<'T> = tconv |> getStaticInstance
                    converter.Convert(array)
                with
                | InvalidConversion exn -> raise exn

        let convertValueUntyped (outputType : Type) (value : IAdaptiveValue) =
            let inputType = value.ContentType
            if inputType = outputType then
                value
            else
                try
                    let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputType; outputType |]
                    let converter : IAdaptiveConverter = tconv |> getStaticInstance
                    converter.ConvertUntyped(value)
                with
                | InvalidConversion exn -> raise exn

        let convertValue (value : IAdaptiveValue) : aval<'T> =
            let inputType = value.ContentType
            if inputType = typeof<'T> then
                unbox value
            else
                try
                    let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputType; typeof<'T> |]
                    let converter : IAdaptiveConverter<'T> = tconv |> getStaticInstance
                    converter.Convert(value)
                with
                | InvalidConversion exn -> raise exn