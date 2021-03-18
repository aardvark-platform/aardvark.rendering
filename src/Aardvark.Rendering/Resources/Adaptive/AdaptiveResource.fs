namespace Aardvark.Rendering

open System
open System.Threading
open FSharp.Data.Adaptive
open System.Runtime.CompilerServices

/// Interface for adaptive reference-counted resources.
type IAdaptiveResource =
    inherit IAdaptiveValue

    /// Increases the reference count and creates the resource if necessary.
    abstract member Acquire : unit -> unit

    /// Decreases the reference count and destroys the resource if it is no longer used.
    abstract member Release : unit -> unit

    /// Resets the reference count and destroys the resource.
    abstract member ReleaseAll : unit -> unit

    /// Gets the resource handle.
    abstract member GetValue : AdaptiveToken * RenderToken -> obj

/// Generic interface for adaptive reference-counted resources.
type IAdaptiveResource<'T> =
    inherit IAdaptiveValue<'T>
    inherit IAdaptiveResource

    /// Gets the resource handle.
    abstract member GetValue : AdaptiveToken * RenderToken -> 'T

/// Base class for adaptive reference-counted resources.
[<AbstractClass>]
type AdaptiveResource<'T>() =
    inherit AdaptiveObject()
    let mutable cache = Unchecked.defaultof<'T>
    let mutable refCount = 0

    /// Called when the resource is first acquired.
    abstract member Create : unit -> unit

    // Called when the resource is released.
    abstract member Destroy : unit -> unit

    // Computes and returns the resource handle.
    abstract member Compute : AdaptiveToken * RenderToken -> 'T

    member private x.Reset() =
        cache <- Unchecked.defaultof<'T>
        transact x.MarkOutdated

    member x.Acquire() =
        if Interlocked.Increment(&refCount) = 1 then
            lock x (fun _ ->
                x.Create()
            )

    member x.Release() =
        if Interlocked.Decrement(&refCount) = 0 then
            lock x (fun _ ->
                x.Destroy()
                x.Reset()
            )

    member x.ReleaseAll() =
        if Interlocked.Exchange(&refCount, 0) > 0 then
            lock x (fun _ ->
                x.Destroy()
                x.Reset()
            )

    member x.GetValue(token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token (fun token ->
            if x.OutOfDate then
                cache <- x.Compute (token, rt)
            cache
        )

    member x.GetValue(token : AdaptiveToken) =
        x.GetValue(token, RenderToken.Empty)

    interface IAdaptiveValue with
        member x.IsConstant = false
        member x.ContentType = typeof<'T>
        member x.GetValueUntyped(c) = x.GetValue(c) :> obj
        member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x

    interface IAdaptiveValue<'T> with
        member x.GetValue(c) = x.GetValue(c)

    interface IAdaptiveResource with
        member x.Acquire() = x.Acquire()
        member x.Release() = x.Release()
        member x.ReleaseAll() = x.ReleaseAll()
        member x.GetValue(c,t) = x.GetValue(c,t) :> obj

    interface IAdaptiveResource<'T> with
        member x.GetValue(c,t) = x.GetValue(c,t)


[<AbstractClass; Sealed; Extension>]
type IAdaptiveResourceExtensions() =

    [<Extension>]
    static member GetValue(this : aval<'T>, c : AdaptiveToken, t : RenderToken) =
        match this with
        | :? IAdaptiveResource<'T> as x -> x.GetValue(c, t)
        | _ -> this.GetValue(c)

    [<Extension>]
    static member GetValue(this : IAdaptiveValue, c : AdaptiveToken, t : RenderToken) =
        match this with
        | :? IAdaptiveResource as x -> x.GetValue(c, t)
        | _ -> this.GetValueUntyped(c)

    [<Extension>]
    static member Acquire(this : IAdaptiveValue) =
        match this with
        | :? IAdaptiveResource as o -> o.Acquire()
        | _ -> ()

    [<Extension>]
    static member Release(this : IAdaptiveValue) =
        match this with
        | :? IAdaptiveResource as o -> o.Release()
        | _ -> ()

    [<Extension>]
    static member ReleaseAll(this : IAdaptiveValue) =
        match this with
        | :? IAdaptiveResource as o -> o.ReleaseAll()
        | _ -> ()


module private AdaptiveResourceImplementations =

    let private cheapEqual (x : 'T) (y : 'T) =
        ShallowEqualityComparer<'T>.Instance.Equals(x, y)


    type AdaptiveValueWrapper<'T>(value : aval<'T>, inputs : IAdaptiveValue list) =

        new(value : aval<'T>, input : IAdaptiveValue) =
            AdaptiveValueWrapper(value, [input])

        new(value : aval<'T>, input1 : IAdaptiveValue, input2 : IAdaptiveValue) =
            AdaptiveValueWrapper(value, [input1; input2])

        new(value : aval<'T>, input1 : IAdaptiveValue, input2 : IAdaptiveValue, input3 : IAdaptiveValue) =
            AdaptiveValueWrapper(value, [input1; input2; input3])

        member x.Value = value

        override x.GetHashCode() = value.GetHashCode()
        override x.Equals(o) =
            match o with
            | :? AdaptiveValueWrapper<'T> as other ->
                DefaultEquality.equals value other.Value
            | _ ->
                false

        interface IAdaptiveObject with
            member x.AllInputsProcessed(a) = value.AllInputsProcessed(a)
            member x.InputChanged(a,b) = value.InputChanged(a,b)
            member x.Mark() = value.Mark()
            member x.IsConstant = value.IsConstant
            member x.Level
                with get() = value.Level
                and set v = value.Level <- v
            member x.OutOfDate
                with get() = value.OutOfDate
                and set v = value.OutOfDate <- v
            member x.Outputs = value.Outputs
            member x.Tag
                with get() = value.Tag
                and set t = value.Tag <- t
            member x.Weak = value.Weak

        interface IAdaptiveValue with
            member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x
            member x.ContentType = typeof<'T>
            member x.GetValueUntyped(t) = value.GetValue(t) :> obj

        interface IAdaptiveValue<'T> with
            member x.GetValue(t) = value.GetValue(t)

        interface IAdaptiveResource with
            member x.Acquire() = inputs |> List.iter (fun i -> i.Acquire())
            member x.Release() = inputs |> List.iter (fun i -> i.Release())
            member x.ReleaseAll() = inputs |> List.iter (fun i -> i.ReleaseAll())
            member x.GetValue(c,t) = value.GetValue(c,t) :> obj

        interface IAdaptiveResource<'T> with
            member x.GetValue(c,t) = value.GetValue(c,t)


    type ConstantResource<'T>(create : unit -> 'T, destroy : 'T -> unit) =
        inherit AdaptiveResource<'T>()

        let mutable cache = Unchecked.defaultof<'T>

        override x.Create() =
            cache <- create()

        override x.Destroy() =
            destroy cache
            cache <- Unchecked.defaultof<'T>

        override x.Compute(_, _) =
            cache


    [<AbstractClass; StructuredFormatDisplay("{AsString}")>]
    type AbstractBind<'Input, 'T>(mapping : 'Input -> aval<'T>) =
        inherit AdaptiveObject()

        let mutable valueCache = Unchecked.defaultof<'T>
        let mutable inner : ValueOption< struct ('Input * aval<'T>) > = ValueNone
        let mutable inputDirty = 1

        abstract member AcquireInput : unit -> unit
        abstract member ReleaseInput : unit -> unit
        abstract member ReleaseInputAll : unit -> unit
        abstract member InputEquals : 'Input * 'Input -> bool
        abstract member IsInput : IAdaptiveObject -> bool
        abstract member GetInput : AdaptiveToken * RenderToken -> 'Input

        member private x.AsString =
            if x.OutOfDate then sprintf "aval*(%A)" valueCache
            else sprintf "aval(%A)" valueCache

        override x.ToString() =
            if x.OutOfDate then String.Format("aval*({0})", valueCache)
            else String.Format("aval({0})", valueCache)

        member x.ReleaseResult(release : aval<'T> -> unit) =
            lock x (fun _ ->
                match inner with
                | ValueSome (struct (_, result)) ->
                    release result
                    inner <- ValueNone
                | _ ->
                    ()
            )

        member x.Acquire() =
            x.AcquireInput()

        member x.Release() =
            x.ReleaseResult(fun r -> r.Release())
            x.ReleaseInput()

        member x.ReleaseAll() =
            x.ReleaseResult(fun r -> r.ReleaseAll())
            x.ReleaseInputAll()

        override x.InputChangedObject(_, o) =
            if x.IsInput o then inputDirty <- 1

        member x.Compute(t : AdaptiveToken, rt : RenderToken) =
            let i = x.GetInput(t, rt)
            let inputDirty = Interlocked.Exchange(&inputDirty, 0) <> 0

            match inner with
            | ValueNone ->
                let result = mapping i
                inner <- ValueSome (struct (i, result))
                result.Acquire()
                result.GetValue(t, rt)

            | ValueSome(struct (io, old)) when not inputDirty || x.InputEquals(io, i) ->
                old.GetValue(t, rt)

            | ValueSome(struct (_, old)) ->
                old.Outputs.Remove x |> ignore
                let result = mapping i
                if not <| DefaultEquality.equals old result then
                    old.Release()
                    result.Acquire()

                inner <- ValueSome (struct (i, result))
                result.GetValue(t, rt)

        member x.GetValue(t : AdaptiveToken, rt : RenderToken) =
            x.EvaluateAlways t (fun t ->
                if x.OutOfDate then
                    let v = x.Compute(t, rt)
                    valueCache <- v
                    v
                else
                    valueCache
            )

        member x.GetValue(t : AdaptiveToken) =
            x.GetValue(t, RenderToken.Empty)

        interface IAdaptiveValue with
            member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x
            member x.GetValueUntyped t = x.GetValue t :> obj
            member x.ContentType = typeof<'T>

        interface IAdaptiveValue<'T> with
            member x.GetValue t = x.GetValue t

        interface IAdaptiveResource with
            member x.Acquire() = x.Acquire()
            member x.Release() = x.Release()
            member x.ReleaseAll() = x.ReleaseAll()
            member x.GetValue(t, rt) = x.GetValue(t, rt) :> obj

        interface IAdaptiveResource<'T> with
            member x.GetValue(t, rt) = x.GetValue(t, rt)


    type BindRes<'T1, 'T2>(mapping: 'T1 -> aval<'T2>, input: aval<'T1>) =
        inherit AbstractBind<'T1, 'T2>(mapping)

        override x.AcquireInput()    = input.Acquire()
        override x.ReleaseInput()    = input.Release()
        override x.ReleaseInputAll() = input.ReleaseAll()
        override x.InputEquals(a, b) = cheapEqual a b
        override x.IsInput(o)        = Object.ReferenceEquals(o, input)     // FIXME: Broken in FSharp.Data.Adaptive as well!
        override x.GetInput(t, rt)   = input.GetValue(t, rt)


    type Bind2Res<'T1, 'T2, 'T3>(mapping: 'T1 -> 'T2 -> aval<'T3>, input1: aval<'T1>, input2: aval<'T2>) =
        inherit AbstractBind<struct ('T1 * 'T2), 'T3>(fun (struct (a, b)) -> mapping a b)

        override x.AcquireInput()          = input1.Acquire(); input2.Acquire()
        override x.ReleaseInput()          = input2.Release(); input1.Release()
        override x.ReleaseInputAll()       = input2.ReleaseAll(); input1.ReleaseAll()

        override x.InputEquals(struct(oa, ob), struct(va, vb)) =
            cheapEqual oa va && cheapEqual ob vb

        override x.IsInput(o) =
            Object.ReferenceEquals(o, input1) || Object.ReferenceEquals(o, input2)

        override x.GetInput(t, rt) =
            struct (input1.GetValue(t, rt), input2.GetValue(t, rt))


    type Bind3Res<'T1, 'T2, 'T3, 'T4>(mapping: 'T1 -> 'T2 -> 'T3 -> aval<'T4>, input1: aval<'T1>, input2: aval<'T2>, input3: aval<'T3>) =
        inherit AbstractBind<struct ('T1 * 'T2 * 'T3), 'T4>(fun (struct (a, b, c)) -> mapping a b c)

        override x.AcquireInput()          = input1.Acquire(); input2.Acquire(); input3.Acquire()
        override x.ReleaseInput()          = input3.Release(); input2.Release(); input1.Release()
        override x.ReleaseInputAll()       = input3.ReleaseAll(); input2.ReleaseAll(); input1.ReleaseAll()

        override x.InputEquals(struct(oa, ob, oc), struct(va, vb, vc)) =
            cheapEqual oa va && cheapEqual ob vb && cheapEqual oc vc

        override x.IsInput(o) =
            Object.ReferenceEquals(o, input1) || Object.ReferenceEquals(o, input2) || Object.ReferenceEquals(o, input3)

        override x.GetInput(t, rt) =
            struct (input1.GetValue(t, rt), input2.GetValue(t, rt), input3.GetValue(t, rt))


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdaptiveResource =
    open AdaptiveResourceImplementations

    /// Increases the reference count and creates the resource if necessary.
    let acquire (r : IAdaptiveResource) =
        r.Acquire()

    /// Decreases the reference count and destroys the resource if it is no longer used.
    let release (r : IAdaptiveResource) =
        r.Release()

    /// Resets the reference count and destroys the resource.
    let releaseAll (r : IAdaptiveResource) =
        r.ReleaseAll()

    /// Gets the resource handle.
    let getValue (t : AdaptiveToken) (rt : RenderToken) (r : IAdaptiveResource<'a>) =
        r.GetValue(t, rt)

    /// Creates an adaptive resource using the given create and destroy functions.
    let constant (create : unit -> 'T) (destroy : 'T -> unit) =
        ConstantResource(create, destroy) :> IAdaptiveResource<'T>

    /// Returns a new adaptive resource that adaptively applies the mapping function to the given adaptive inputs.
    let map (mapping : 'T1 -> 'T2) (value : aval<'T1>) =
        let mapped = value |> AVal.map mapping

        if value :? IAdaptiveResource then
            AdaptiveValueWrapper(mapped, value) :> aval<_>
        else
            mapped

    /// Returns a new adaptive resource that applies the mapping function whenever a value is demanded.
    /// This is useful when applying very cheap mapping functions (like unbox, fst, etc.)
    /// WARNING: the mapping function will also be called for unchanged inputs.
    let mapNonAdaptive (mapping : 'T1 -> 'T2) (value : aval<'T1>) =
        let mapped = value |> AVal.mapNonAdaptive mapping

        if value :? IAdaptiveResource then
            AdaptiveValueWrapper(mapped, value) :> aval<_>
        else
            mapped

    /// Casts the given adaptive resource to the specified type. Raises InvalidCastException *immediately*
    /// when the specified cast is not valid (similar to Seq.cast).
    let cast<'T> (value : IAdaptiveValue) =
        let mapped = value |> AVal.cast<'T>

        if value :? IAdaptiveResource then
            AdaptiveValueWrapper(mapped, value) :> aval<_>
        else
            mapped

    /// Returns a new adaptive resource that adaptively applies the mapping function to the given adaptive inputs.
    let map2 (mapping : 'T1 -> 'T2 -> 'T3) (value1 : aval<'T1>) (value2 : aval<'T2>) =
        let mapped = AVal.map2 mapping value1 value2

        if value1 :? IAdaptiveResource || value2 :? IAdaptiveResource then
            AdaptiveValueWrapper(mapped, value1, value2) :> aval<_>
        else
            mapped

    /// Returns a new adaptive value that adaptively applies the mapping function to the given adaptive inputs.
    let map3 (mapping : 'T1 -> 'T2 -> 'T3 -> 'T4) (value1 : aval<'T1>) (value2 : aval<'T2>) (value3 : aval<'T3>) =
        let mapped = AVal.map3 mapping value1 value2 value3

        if value1 :? IAdaptiveResource || value2 :? IAdaptiveResource || value3 :? IAdaptiveResource then
            AdaptiveValueWrapper(mapped, value1, value2, value3) :> aval<_>
        else
            mapped

    /// Returns a new adaptive resource that adaptively applies the mapping function to the given
    /// input and adaptively depends on the resulting adaptive value.
    /// The resulting adaptive resource will hold the latest value of the aval<_> returned by mapping.
    let bind (mapping: 'T1 -> aval<'T2>) (value: aval<'T1>) =
        if value.IsConstant then
            value |> AVal.force |> mapping
        else
            BindRes<'T1, 'T2>(mapping, value) :> aval<_>

    /// Adaptively applies the mapping function to the given adaptive values and
    /// adaptively depends on the adaptive resource returned by mapping.
    /// The resulting adaptive resource will hold the latest value of the aval<_> returned by mapping.
    let bind2 (mapping: 'T1 -> 'T2 -> aval<'T3>) (value1: aval<'T1>) (value2: aval<'T2>) =
        if value1.IsConstant && value2.IsConstant then
            mapping (AVal.force value1) (AVal.force value2)

        elif value1.IsConstant then
            let a = AVal.force value1
            bind (fun b -> mapping a b) value2

        elif value2.IsConstant then
            let b = AVal.force value2
            bind (fun a -> mapping a b) value1

        else
            Bind2Res<'T1, 'T2, 'T3>(mapping, value1, value2) :> aval<_>

    /// Adaptively applies the mapping function to the given adaptive values and
    /// adaptively depends on the adaptive resource returned by mapping.
    /// The resulting adpative resource will hold the latest value of the aval<_> returned by mapping.
    let bind3 (mapping: 'T1 -> 'T2 -> 'T3 -> aval<'T4>) (value1: aval<'T1>) (value2: aval<'T2>) (value3: aval<'T3>) =
        if value1.IsConstant && value2.IsConstant && value3.IsConstant then
            mapping (AVal.force value1) (AVal.force value2) (AVal.force value3)

        elif value1.IsConstant then
            let a = AVal.force value1
            bind2 (fun b c -> mapping a b c) value2 value3

        elif value2.IsConstant then
            let b = AVal.force value2
            bind2 (fun a c -> mapping a b c) value1 value3

        elif value3.IsConstant then
            let c = AVal.force value3
            bind2 (fun a b -> mapping a b c) value1 value2
        else
            Bind3Res<'T1, 'T2, 'T3, 'T4>(mapping, value1, value2, value3) :> aval<_>