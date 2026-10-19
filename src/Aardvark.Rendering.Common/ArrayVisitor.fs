namespace Aardvark.Rendering

open System
open System.Reflection
open System.Reflection.Emit
open System.Runtime.CompilerServices

/// Dispatches .NET vector arrays (T[]), including jagged and covariant vectors,
/// by runtime element type. Rectangular and non-vector rank-one arrays are unsupported.
[<AbstractClass>]
type ArrayVisitor<'r>() =
    static let cache = System.Collections.Concurrent.ConcurrentDictionary<Type, ArrayVisitor<'r> -> Array -> 'r>()

    static let mRunGen =
        typeof<ArrayVisitor<'r>>.GetMethods(BindingFlags.DeclaredOnly ||| BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance)
            |> Array.filter (fun m -> m.Name = "Run" && m.IsGenericMethod)
            |> Array.head

    static let get (t : Type) =
        cache.GetOrAdd(t, Func<_,_>(fun t ->
            let dMeth =
                DynamicMethod(
                    "ArrayVisitor",
                    MethodAttributes.Static ||| MethodAttributes.Public,
                    CallingConventions.Standard,
                    typeof<'r>,
                    [| typeof<ArrayVisitor<'r>>; typeof<Array> |],
                    typeof<obj>,
                    true
                )
            let il = dMeth.GetILGenerator()
            let mRun = mRunGen.MakeGenericMethod [| t |]
            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Ldarg_1)
            il.Emit(OpCodes.Castclass, t.MakeArrayType())
            il.EmitCall(OpCodes.Callvirt, mRun, null)
            il.Emit(OpCodes.Ret)

            let t = typeof<Func<ArrayVisitor<'r>, Array, 'r>>
            let del = dMeth.CreateDelegate(t) |> unbox<Func<ArrayVisitor<'r>, Array, 'r>>
            fun v a -> del.Invoke(v,a)

        ))

    /// <summary>Visits a .NET vector array using its runtime element type.</summary>
    /// <exception cref="T:System.InvalidCastException">The array is rectangular or a non-vector rank-one array.</exception>
    member x.Run(a : Array) =
        let t = a.GetType().GetElementType()
        get t x a

    abstract member Run : 'a[] -> 'r

[<AbstractClass; Sealed; Extension>]
type ArrayVisitorExtensions private() =

    /// Visits a .NET vector array (T[]) using its runtime element type.
    [<Extension>]
    static member inline Visit (x : Array, v : ArrayVisitor<'r>) = v.Run x