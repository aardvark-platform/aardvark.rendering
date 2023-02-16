namespace Aardvark.Rendering

open FSharp.Data.Adaptive
open Aardvark.Base

open System
open System.Reflection
open System.Collections.Generic
open System.Runtime.CompilerServices

module private MutableComputeBindingInternals =

    module AVal =

        [<AutoOpen>]
        module private Dispatch =

            type private Dispatcher() =
                static member Create<'T>() = AVal.init Unchecked.defaultof<'T>
                static member Change(cval : ChangeableValue<'T>, value : 'T) = cval.Value <- value

            module Method =
                let private flags = BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public
                let create = typeof<Dispatcher>.GetMethod("Create", flags)
                let change = typeof<Dispatcher>.GetMethod("Change", flags)

        let create (contentType : Type) =
            let mi = Method.create.MakeGenericMethod [| contentType |]
            mi.Invoke(null, [||]) |> unbox<IAdaptiveValue>

        let change (value : obj) (cval : IAdaptiveValue) =
            let mi = Method.change.MakeGenericMethod [| cval.ContentType |]
            mi.Invoke(null, [| cval; value |]) |> ignore

    // When an input is first set, the provided value might be of a specialized type.
    // The type of that value determines the content type of cval value that is created at that point.
    // This might be troublesome if a highly specialized value is set and a more general one afterwards.
    let private knownBaseTypes =
        [| typeof<ITexture>
           typeof<ITextureLevel>
           typeof<IBuffer> |]

    let getBaseType (value : obj) =
        let typ = value.GetType()

        knownBaseTypes |> Array.tryFind (fun baseType ->
            baseType.IsAssignableFrom typ
        )
        |> Option.defaultValue typ

    // Wrap arrays so they are compatible with buffers.
    let inline wrapArray (value : obj) : obj =
        match value with
        | :? Array as arr -> ArrayBuffer(arr)
        | _ -> value


open MutableComputeBindingInternals

/// Mutable variant for compute input bindings.
/// For backwards-compatible only, new code should create input bindings from an IUniformProvider.
type MutableComputeInputBinding internal(shader : IComputeShader) =
    let cvals = Dictionary<string, IAdaptiveValue>()
    let pending = Dictionary<string, obj>()

    member x.TryGetValue(name : string) =
        lock x (fun _ ->
            match cvals.TryGetValue(name) with
            | (true, cval) -> Some cval
            | _ -> None
        )

    member x.Shader = shader

    member x.Item
        with set (name : string) (value : obj) =
            if isNull value then
                raise <| ArgumentNullException(nameof value)

            lock x (fun _ ->
                let wrapped = wrapArray value

                match cvals.TryGetValue name with
                | (true, cval) ->
                    if not <| cval.ContentType.IsAssignableFrom(wrapped.GetType()) then
                        raise <| ArgumentException($"Cannot set input {name} (type = {cval.ContentType}) to value of type {value.GetType()}.")
                | _ ->
                    let baseType = getBaseType wrapped
                    cvals.[name] <- AVal.create baseType

                pending.[name] <- wrapped
            )

    member x.Flush() =
        lock x (fun _ ->
            if pending.Count > 0 then
                transact (fun _ ->
                    for KeyValue(name, value) in pending do
                        cvals.[name] |> AVal.change value
                )
                pending.Clear()
        )

    member x.Dispose() =
        lock x (fun _ ->
            cvals.Clear()
            pending.Clear()
        )

    interface IComputeInputBinding with
        member x.Shader = x.Shader

    interface IUniformProvider with
        member x.TryGetUniform(_scope, name) = x.TryGetValue(string name)

    interface IDisposable with
        member x.Dispose() = x.Dispose()


[<AbstractClass; Sealed; Extension>]
type IComputeRuntimeMutableInputBindingExtensions private() =

    /// Creates a mutable input binding for the given shader.
    /// For backwards-compatible only, new code should create input bindings from an IUniformProvider.
    [<Extension>]
    static member NewInputBinding(_runtime : IComputeRuntime, shader : IComputeShader) =
        new MutableComputeInputBinding(shader)