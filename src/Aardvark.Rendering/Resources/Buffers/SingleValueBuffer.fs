namespace Aardvark.Rendering

open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open System

/// Interface for attribute buffers containing a single value that is repeated for all vertices or instances.
type ISingleValueBuffer =
    inherit IAdaptiveValue<IBuffer>

    /// The contained value.
    abstract member Value : IAdaptiveValue

/// Attribute buffer containing a single value that is repeated for all vertices or instances.
type SingleValueBuffer<'T when 'T : unmanaged>(value : aval<'T>) =

    static let zero = SingleValueBuffer(Unchecked.defaultof<'T>)

    [<Obsolete("Use SingleValueBuffer<'T>.Zero instead.")>]
    new() = SingleValueBuffer(~~Unchecked.defaultof<'T>)

    new(value : 'T) = SingleValueBuffer(~~value)

    /// Buffer containing zero as value.
    static member Zero = zero

    /// The contained value.
    member x.Value = value

    /// Returns the value wrapped in an ArrayBuffer.
    member private x.GetValue(token : AdaptiveToken) : IBuffer =
        ArrayBuffer [| value.GetValue token |]

    override x.GetHashCode() = value.GetHashCode()
    override x.Equals obj =
        match obj with
        | :? SingleValueBuffer<'T> as other -> value = other.Value
        | _ -> false

    interface ISingleValueBuffer with
        member x.Value = value
        member x.ContentType = typeof<IBuffer>
        member x.IsConstant = value.IsConstant

        member x.Level
            with get() = value.Level
            and set(level) = value.Level <- level

        member x.OutOfDate
            with get() = value.OutOfDate
            and set(outOfDate) = value.OutOfDate <- outOfDate

        member x.Tag
            with get() = value.Tag
            and set(tag) = value.Tag <- tag

        member x.Outputs = value.Outputs
        member x.Weak = value.Weak
        member x.GetValue(t) = x.GetValue(t)
        member x.GetValueUntyped(t) = x.GetValue(t)
        member x.Accept(visitor) = value.Accept(visitor)
        member x.AllInputsProcessed(obj) = value.AllInputsProcessed(obj)
        member x.InputChanged(t, o) = value.InputChanged(t, o)
        member x.Mark() = value.Mark()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SingleValueBuffer =

    [<AutoOpen>]
    module private GenericDispatch =
        open System.Reflection
        open System.Collections.Concurrent

        [<AbstractClass; Sealed>]
        type Dispatcher() =
            static member ToSingleValueBuffer<'T when 'T : unmanaged>(value: obj) : ISingleValueBuffer =
                SingleValueBuffer<'T>(unbox<'T> value)

        module ToSingleValueBuffer =
            type Delegate = delegate of obj -> ISingleValueBuffer
            let private flags = BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public
            let private definition = typeof<Dispatcher>.GetMethod(nameof Dispatcher.ToSingleValueBuffer, flags)
            let private delegates = ConcurrentDictionary<Type, Delegate>()

            let getDelegate (t: Type) =
                delegates.GetOrAdd(t, fun t ->
                    let mi = definition.MakeGenericMethod [| t |]
                    unbox<Delegate> <| Delegate.CreateDelegate(typeof<Delegate>, null, mi)
                )

    /// Creates a single value buffer from an untyped value.
    let create (value: obj) =
        let del = ToSingleValueBuffer.getDelegate <| value.GetType()
        del.Invoke(value)