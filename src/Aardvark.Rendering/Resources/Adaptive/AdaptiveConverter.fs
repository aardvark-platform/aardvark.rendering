namespace Aardvark.Rendering

open Aardvark.Base

open System
open System.Reflection
open System.Collections.Concurrent
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

        let private converterCache = ConcurrentDictionary<struct (Type * Type), IAdaptiveConverter>()

        let private getUntypedConverter (inputType: Type) (outputType: Type) : IAdaptiveConverter =
            converterCache.GetOrAdd(struct (inputType, outputType), fun struct (inputType, outputType) ->
                let tconv = typedefof<AdaptiveConverter<_,_>>.MakeGenericType [| inputType; outputType |]
                let prop = tconv.GetProperty(nameof(AdaptiveConverter.Instance), BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public)
                prop.GetValue(null) |> unbox<IAdaptiveConverter>
            )

        let inline private getConverter<'T> (inputType: Type) : IAdaptiveConverter<'T> =
            getUntypedConverter inputType typeof<'T> |> unbox<IAdaptiveConverter<'T>>

        let convertArray (inputElementType : Type) (array : aval<Array>) : aval<'T[]> =
            if inputElementType = typeof<'T> then
                array |> AdaptiveResource.mapNonAdaptive unbox
            else
                try
                    let converter = getConverter<'T> inputElementType
                    converter.Convert(array)
                with
                | InvalidConversion exn -> raise exn

        let convertValueUntyped (outputType : Type) (value : IAdaptiveValue) =
            let inputType = value.ContentType
            if inputType = outputType then
                value
            else
                try
                    let converter = getUntypedConverter inputType outputType
                    converter.ConvertUntyped(value)
                with
                | InvalidConversion exn -> raise exn

        let convertValue (value : IAdaptiveValue) : aval<'T> =
            let inputType = value.ContentType
            if inputType = typeof<'T> then
                unbox value
            else
                try
                    let converter = getConverter<'T> inputType
                    converter.Convert(value)
                with
                | InvalidConversion exn -> raise exn