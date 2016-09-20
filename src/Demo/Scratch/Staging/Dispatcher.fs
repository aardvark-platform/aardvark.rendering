namespace Aardvark.Base

open System
open System.Collections.Generic
open System.Reflection
open System.Reflection.Emit
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.ExprShape
open Aardvark.Base
open QuotationCompiler


open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AutoOpen>]
module private ``ILGenerator Extensions`` =
    type ILGenerator with
        member x.Ldc(value : nativeint) =
            if sizeof<nativeint> = 8 then x.Emit(OpCodes.Ldc_I8, int64 value)
            else x.Emit(OpCodes.Ldc_I4, int value)


        member x.ToObj(has : Type) =
            if has = typeof<obj> then
                ()
            if has = typeof<System.Void> then 
                x.Emit(OpCodes.Ldnull)

            elif has.IsValueType then
                x.Emit(OpCodes.Box, has)


        member x.OfObj(should : Type) = 

            if should = typeof<obj> then 
                ()
            elif should = typeof<System.Void> then
                x.Emit(OpCodes.Pop)
            elif should.IsValueType then
                x.Emit(OpCodes.Unbox_Any, should)


        member x.Convert(is : Type, should : Type) =
            if is = should then ()
            elif should = typeof<obj> then x.ToObj is
            elif is = typeof<obj> then x.OfObj should
            else
                if should.IsAssignableFrom is then ()
                else failwith "bad dispatcher method"

    type DynamicMethod with
        member x.CreateDelegate<'a>() =
            x.CreateDelegate(typeof<'a>) |> unbox<'a>

module private OverloadResolution =
    
    type State = { assignment : HashMap<Type, Type> }
    type Result<'a> = { run : State -> Option<State * 'a> }

    type ResultBuilder() =
        member x.Bind(r : Result<'a>, f : 'a -> Result<'b>) =
            { run = fun s ->
                match r.run s with
                    | Some (s, v) ->
                        (f v).run s
                    | None ->
                        None
            }

        member x.Return(v : 'a) =
            { run = fun s -> Some(s,v) }

        member x.Zero() =
            { run = fun s -> Some(s, ()) }

        member x.Delay(f : unit -> Result<'a>) =
            { run = fun s -> f().run s }

        member x.Combine(l : Result<unit>, r : Result<'a>) =
            { run = fun s ->
                match l.run s with
                    | Some(s,()) -> r.run s
                    | None -> None
            }

        member x.For(seq : seq<'a>, f : 'a -> Result<unit>) =
            { run = fun s -> 
                let e = seq.GetEnumerator()

                let rec run(s) =
                    if e.MoveNext() then
                        match (f e.Current).run s with
                            | Some(s,()) ->
                                run s
                            | None ->
                                None
                    else
                        Some(s,())

                let res = run s
                e.Dispose()
                res
            }

        member x.While(guard : unit -> bool, body : Result<unit>) =
            { run = fun s ->
                if guard() then
                    match body.run s with
                        | Some (s,()) -> x.While(guard, body).run s
                        | None -> None
                else
                    Some(s,())
            }

    let result = ResultBuilder()

    let assign (t : Type) (o : Type) =
        { run = fun s ->
            match HashMap.tryFind t s.assignment with
                | Some(old) when old <> o ->
                    None
                | _ ->
                    Some({ s with assignment = HashMap.add t o s.assignment }, ())
        }

    let fail<'a> : Result<'a> = { run = fun s -> None }

    let success = { run = fun s -> Some(s, ()) }

    let rec tryInstantiateType (argType : Type) (realType : Type) =
        result {
            let genArg = if argType.IsGenericType then argType.GetGenericTypeDefinition() else argType

            if argType = realType then
                do! success
            elif argType.IsAssignableFrom realType then
                do! success

            elif argType.IsGenericParameter then
                do! assign argType realType

            elif argType.ContainsGenericParameters && realType.IsGenericType && realType.GetGenericTypeDefinition() = genArg then
                let argArgs = argType.GetGenericArguments()
                let realGen = realType.GetGenericTypeDefinition()
                let realArgs = realType.GetGenericArguments()

                for i in 0..realArgs.Length-1 do
                    let r = realArgs.[i]
                    let a = argArgs.[i]
                    do! tryInstantiateType a r

            elif argType.IsInterface then
                let iface = 
                    realType.GetInterfaces()
                        |> Array.tryFind (fun iface -> 
                            let gen = if iface.IsGenericType then iface.GetGenericTypeDefinition() else iface
                            gen = genArg
                        )

                match iface with
                    | Some iface ->
                        if iface.IsGenericType then
                            let ifaceArgs = iface.GetGenericArguments()
                            let argArgs = argType.GetGenericArguments()
                            for i in 0..ifaceArgs.Length - 1 do
                                do! tryInstantiateType argArgs.[i] ifaceArgs.[i]
                        else
                            ()
                    | None ->
                        do! fail

            else
                let baseType = realType.BaseType
                if isNull baseType then do! fail
                else do! tryInstantiateType argType baseType
        }

    let tryInstantiateMethodInfo (mi : MethodInfo) (real : Type[]) =
        result {
            let p = mi.GetParameters()
            if p.Length = real.Length then
                for i in 0..p.Length-1 do
                    do! tryInstantiateType (p.[i].ParameterType) real.[i]
            else
                do! fail
        }

    let tryInstantiate (mi : MethodInfo) (args : Type[]) =
        let parameters = mi.GetParameters()
        if parameters.Length = args.Length then
            if mi.IsGenericMethod then
                let mi = mi.GetGenericMethodDefinition()
                let m = tryInstantiateMethodInfo mi args
                match m.run { assignment = HashMap.empty } with
                    | Some (s,()) ->
                        let args = mi.GetGenericArguments() |> Array.map (fun a -> HashMap.find a s.assignment)
                        mi.MakeGenericMethod(args) |> Some

                    | None -> 
                        None
            else
                let works = Array.forall2 (fun (p : ParameterInfo) a -> p.ParameterType.IsAssignableFrom a) parameters args
                if works then Some mi
                else None
        else
            None

type Dispatcher<'b, 'r> (tryGet : Type -> Option<obj * MethodInfo>) =

    static let rebuildMeth = typeof<Dispatcher<'b, 'r>>.GetMethod("Rebuild", BindingFlags.NonPublic ||| BindingFlags.Instance)
    static let getTypeMeth = typeof<obj>.GetMethod("GetType")

    static let initial =
        FuncRef1<Dispatcher<'b, 'r>, obj[], obj, 'b, 'r, bool>(fun d targets a b res ->
            d.Rebuild(a,b,&res)
        )

    let all = Dictionary<Type, obj * MethodInfo>()
    let mutable self = initial
    let mutable targetArray : obj[] = null

    let perfectTable () =
        let minId = all.Keys |> Seq.map (fun t -> t.TypeHandle.Value) |> Seq.min

        let rec createTable (size : int) =
            let table = Array.zeroCreate size
            let mutable success = true
                
            for (t,(tar, meth)) in Dictionary.toSeq all do
                let e = int (t.TypeHandle.Value - minId) % table.Length
                match table.[e] with
                    | None -> 
                        table.[e] <- Some (t, tar, meth)
                    | _ ->
                        success <- false

            if success then
                //Log.line "table-size: %A/%A" all.Count size
                table
            else
                createTable (size + 1)

        minId, createTable all.Count

    let rebuild(x : Dispatcher<'b, 'r>) =
        let meth = 
            DynamicMethod(
                Guid.NewGuid() |> string,
                MethodAttributes.Static ||| MethodAttributes.Public,
                CallingConventions.Standard,
                typeof<bool>,
                [| typeof<Dispatcher<'b, 'r>>; typeof<obj[]>; typeof<obj>; typeof<'b>; typeof<'r>.MakeByRefType() |],
                typeof<Dispatcher<'b, 'r>>,
                true
            )



        
        let il = meth.GetILGenerator()
        let dyn = il.DeclareLocal(typeof<obj>)
        let dynType = if sizeof<nativeint> = 8 then il.DeclareLocal(typeof<int64>) else il.DeclareLocal(typeof<int>)

        let loadTarget (i : int) =
            il.Emit(OpCodes.Ldarg_1)
            il.Emit(OpCodes.Ldc_I4, i)
            il.Emit(OpCodes.Ldelem, typeof<obj>)

        // create a table for all instances
        let errorLabel = il.DefineLabel()

        let targets = List<obj>()
        let targetCache = Dict<obj, int>()
        for (t,_) in all.Values do
            if not (isNull t) then
                let index = 
                    targetCache.GetOrCreate(t, fun t ->
                        let i = targets.Count
                        targets.Add t
                        i
                    )
                ()



        // load the dynamic argument
        il.Emit(OpCodes.Ldarg_2)
        il.Emit(OpCodes.Stloc, dyn)
        
        // read its type-pointer
        il.Emit(OpCodes.Ldloc, dyn)
        if sizeof<nativeint> = 8 then il.Emit(OpCodes.Ldobj, typeof<int64>)
        else il.Emit(OpCodes.Ldobj, typeof<int>)
        il.Emit(OpCodes.Stloc, dynType)


        let callWhenType (t : Type) (target : obj) (meth : MethodInfo) =
            // if the types don't match goto error
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(t.TypeHandle.Value)
            il.Emit(OpCodes.Bne_Un, errorLabel)

            if isNull meth then
                il.Emit(OpCodes.Ldc_I4_0)
                il.Emit(OpCodes.Ret)
            else
                

                // load the target (if not static)
                if not meth.IsStatic then
                    loadTarget targetCache.[target]
                    il.OfObj(meth.DeclaringType)

                // load all parameters from the array (and unbox them)
                let parameters = meth.GetParameters()
                        
 
                il.Emit(OpCodes.Ldloc, dyn)  
                il.OfObj(parameters.[0].ParameterType)
                il.Emit(OpCodes.Ldarg_3)
                if meth.IsVirtual then il.EmitCall(OpCodes.Callvirt, meth, null)
                else il.EmitCall(OpCodes.Call, meth, null)

                il.Convert(meth.ReturnType, typeof<'r>)
                let l = il.DeclareLocal(typeof<'r>)
                il.Emit(OpCodes.Stloc, l)


                il.Emit(OpCodes.Ldarg_S, 4uy)
                il.Emit(OpCodes.Ldloc, l)
                il.Emit(OpCodes.Stobj, meth.ReturnType)

                il.Emit(OpCodes.Ldc_I4_1)
                il.Emit(OpCodes.Ret)


        if all.Count = 0 then
            il.Emit(OpCodes.Br, errorLabel)

        if all.Count = 1 then
            match all |> Seq.head with
                | KeyValue(t, (target, meth)) ->
                    callWhenType t target meth

        else
            let minHandle, table = perfectTable()
            let caseLabels = table |> Array.map (fun _ -> il.DefineLabel())

            // (<type> - minHandle) % table.Length
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(minHandle)
            il.Emit(OpCodes.Sub)
            il.Ldc(nativeint table.Length)
            il.Emit(OpCodes.Rem)
            il.Emit(OpCodes.Conv_I4)

            il.Emit(OpCodes.Switch, caseLabels)
            il.Emit(OpCodes.Br, errorLabel)

            for i in 0 .. table.Length-1 do
                let caseLabel = caseLabels.[i]
                il.MarkLabel(caseLabel)

                match table.[i] with
                    | Some (t, target, meth) ->
                        callWhenType t target meth

                    | None ->
                        il.Emit(OpCodes.Br, errorLabel)




        il.MarkLabel(errorLabel)

 
        il.Emit(OpCodes.Ldarg_0)
        il.Emit(OpCodes.Ldarg_2)
        il.Emit(OpCodes.Ldarg_3)
        il.Emit(OpCodes.Ldarg_S, 4)
        il.EmitCall(OpCodes.Call, rebuildMeth, null)
        il.Emit(OpCodes.Ret)

        let f = meth.CreateDelegate<FuncRef1<Dispatcher<'b, 'r>, obj[], obj, 'b, 'r, bool>>()

        self <- f
        targetArray <- targets.ToArray()

    member x.TryInvoke(a : obj, b : 'b, [<Out>] res : byref<'r>) : bool =
        self.Invoke(x, targetArray, a, b, &res)

    member x.Invoke(a : obj, b : 'b) =
        match x.TryInvoke(a,b) with
            | (true, r) -> r
            | _ -> failwith "sadasd"

    member private x.Rebuild(a : obj, b : 'b, res : byref<'r>) : bool =
        let dynArg = a.GetType()
        lock x (fun () ->
            if all.ContainsKey dynArg then
                ()
            else
                match tryGet dynArg with
                    | Some (target, meth) -> 
                        let parameters = meth.GetParameters()

                        if typeof<'r>.IsAssignableFrom meth.ReturnType && 
                           parameters.Length = 2 && 
                           parameters.[1].ParameterType.IsAssignableFrom typeof<'b> 
                        then
                            all.[dynArg] <- (target, meth)
                            rebuild x
                        else
                            failwithf "[Dispatcher] invalid implementation: %A" meth

                    | None ->
                        all.[dynArg] <- (null, null)
                        rebuild x
        )
        x.TryInvoke(a,b,&res)

    static member Create (methods : list<obj * MethodInfo>) =
        
        let targets =
            methods 
                |> Seq.map (fun (a,b) -> 
                    if b.IsGenericMethod then (b.GetGenericMethodDefinition(),a)
                    else (b,a)
                   )
                |> Dictionary.ofSeq

        let tryGetMethodInfo(retType : Type) (types : Type[])=
            let goodOnes = 
                targets.Keys
                    |> Seq.choose (fun mi -> OverloadResolution.tryInstantiate mi types)
                    |> Seq.filter (fun mi -> retType.IsAssignableFrom mi.ReturnType)
                    |> Seq.cast
                    |> Seq.toArray

            if goodOnes.Length > 0 then
                let selected =
                    if goodOnes.Length = 1 then 
                        goodOnes.[0] |> unbox<MethodInfo>
                    else 
                        
                        Type.DefaultBinder.SelectMethod(
                            BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.InvokeMethod,
                            goodOnes,
                            types,
                            types |> Array.map (fun _ -> ParameterModifier())
                        ) |> unbox<MethodInfo>
                
                if selected.IsGenericMethod then
                    let target = targets.[selected.GetGenericMethodDefinition()]
                    Some (target, selected)
                else
                    let target = targets.[selected]
                    Some (target, selected)
            else
                None

        let dispatcher =
            Dispatcher<'b, 'r>(fun t ->
                tryGetMethodInfo typeof<'r> [| t; typeof<'b> |]
            )

        dispatcher  

    static member Create (lambdas : list<obj>) =
        lambdas 
            |> List.choose (fun l ->
                let t = l.GetType()
                let best = t.GetMethods() |> Array.filter (fun mi -> mi.Name = "Invoke") |> Array.maxBy (fun mi -> mi.GetParameters().Length)
                let p = best.GetParameters()
                if p.Length = 2 && p.[1].ParameterType.IsAssignableFrom typeof<'b> && typeof<'r>.IsAssignableFrom best.ReturnType then
                    Some (l, best)
                else
                    None
            )
            |> Dispatcher<'b, 'r>.Create

type Dispatcher<'r> (tryGet : Type -> Option<obj * MethodInfo>) =

    static let rebuildMeth = typeof<Dispatcher<'r>>.GetMethod("Rebuild", BindingFlags.NonPublic ||| BindingFlags.Instance)
    static let getTypeMeth = typeof<obj>.GetMethod("GetType")

    static let initial =
        FuncRef1<Dispatcher<'r>, obj[], obj, 'r, bool>(fun d targets a res ->
            d.Rebuild(a,&res)
        )

    let all = Dictionary<Type, obj * MethodInfo>()
    let mutable self = initial
    let mutable targetArray : obj[] = null

    let perfectTable () =
        let minId = all.Keys |> Seq.map (fun t -> t.TypeHandle.Value) |> Seq.min

        let rec createTable (size : int) =
            let table = Array.zeroCreate size
            let mutable success = true
                
            for (t,(tar, meth)) in Dictionary.toSeq all do
                let e = int (t.TypeHandle.Value - minId) % table.Length
                match table.[e] with
                    | None -> 
                        table.[e] <- Some (t, tar, meth)
                    | _ ->
                        success <- false

            if success then
                //Log.line "table-size: %A/%A" all.Count size
                table
            else
                createTable (size + 1)

        minId, createTable all.Count

    let rebuild(x : Dispatcher<'r>) =
        let meth = 
            DynamicMethod(
                Guid.NewGuid() |> string,
                MethodAttributes.Static ||| MethodAttributes.Public,
                CallingConventions.Standard,
                typeof<bool>,
                [| typeof<Dispatcher<'r>>; typeof<obj[]>; typeof<obj>; typeof<'r>.MakeByRefType() |],
                typeof<Dispatcher<'r>>,
                true
            )



        
        let il = meth.GetILGenerator()
        let dyn = il.DeclareLocal(typeof<obj>)
        let dynType = if sizeof<nativeint> = 8 then il.DeclareLocal(typeof<int64>) else il.DeclareLocal(typeof<int>)

        let loadTarget (i : int) =
            il.Emit(OpCodes.Ldarg_1)
            il.Emit(OpCodes.Ldc_I4, i)
            il.Emit(OpCodes.Ldelem, typeof<obj>)

        // create a table for all instances
        let errorLabel = il.DefineLabel()

        let targets = List<obj>()
        let targetCache = Dict<obj, int>()
        for (t,_) in all.Values do
            if not (isNull t) then
                let index = 
                    targetCache.GetOrCreate(t, fun t ->
                        let i = targets.Count
                        targets.Add t
                        i
                    )
                ()



        // load the dynamic argument
        il.Emit(OpCodes.Ldarg_2)
        il.Emit(OpCodes.Stloc, dyn)
        
        // read its type-pointer
        il.Emit(OpCodes.Ldloc, dyn)
        if sizeof<nativeint> = 8 then il.Emit(OpCodes.Ldobj, typeof<int64>)
        else il.Emit(OpCodes.Ldobj, typeof<int>)
        il.Emit(OpCodes.Stloc, dynType)


        let callWhenType (t : Type) (target : obj) (meth : MethodInfo) =
            // if the types don't match goto error
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(t.TypeHandle.Value)
            il.Emit(OpCodes.Bne_Un, errorLabel)

            if isNull meth then
                il.Emit(OpCodes.Ldc_I4_0)
                il.Emit(OpCodes.Ret)
            else
                

                // load the target (if not static)
                if not meth.IsStatic then
                    loadTarget targetCache.[target]
                    il.OfObj(meth.DeclaringType)

                // load all parameters from the array (and unbox them)
                let parameters = meth.GetParameters()
                        
 
                il.Emit(OpCodes.Ldloc, dyn)  
                il.OfObj(parameters.[0].ParameterType)
                if meth.IsVirtual then il.EmitCall(OpCodes.Callvirt, meth, null)
                else il.EmitCall(OpCodes.Call, meth, null)

                il.Convert(meth.ReturnType, typeof<'r>)
                let l = il.DeclareLocal(typeof<'r>)
                il.Emit(OpCodes.Stloc, l)


                il.Emit(OpCodes.Ldarg_3)
                il.Emit(OpCodes.Ldloc, l)
                il.Emit(OpCodes.Stobj, meth.ReturnType)

                il.Emit(OpCodes.Ldc_I4_1)
                il.Emit(OpCodes.Ret)


        if all.Count = 0 then
            il.Emit(OpCodes.Br, errorLabel)

        if all.Count = 1 then
            match all |> Seq.head with
                | KeyValue(t, (target, meth)) ->
                    callWhenType t target meth

        else
            let minHandle, table = perfectTable()
            let caseLabels = table |> Array.map (fun _ -> il.DefineLabel())

            // (<type> - minHandle) % table.Length
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(minHandle)
            il.Emit(OpCodes.Sub)
            il.Ldc(nativeint table.Length)
            il.Emit(OpCodes.Rem)
            il.Emit(OpCodes.Conv_I4)

            il.Emit(OpCodes.Switch, caseLabels)
            il.Emit(OpCodes.Br, errorLabel)

            for i in 0 .. table.Length-1 do
                let caseLabel = caseLabels.[i]
                il.MarkLabel(caseLabel)

                match table.[i] with
                    | Some (t, target, meth) ->
                        callWhenType t target meth

                    | None ->
                        il.Emit(OpCodes.Br, errorLabel)




        il.MarkLabel(errorLabel)

 
        il.Emit(OpCodes.Ldarg_0)
        il.Emit(OpCodes.Ldarg_2)
        il.Emit(OpCodes.Ldarg_3)
        il.EmitCall(OpCodes.Call, rebuildMeth, null)
        il.Emit(OpCodes.Ret)

        let f = meth.CreateDelegate<FuncRef1<Dispatcher<'r>, obj[], obj, 'r, bool>>()

        self <- f
        targetArray <- targets.ToArray()


    member x.TryInvoke(a : obj, [<Out>] res : byref<'r>) : bool =
        self.Invoke(x, targetArray, a, &res)

    member x.Invoke(a : obj) =
        match x.TryInvoke(a) with
            | (true, r) -> r
            | _ -> failwith "sadasd"

    member private x.Rebuild(a : obj, res : byref<'r>) : bool =
        let dynArg = a.GetType()
        lock x (fun () ->
            if all.ContainsKey dynArg then
                ()
            else
                match tryGet dynArg with
                    | Some (target, meth) -> 
                        let parameters = meth.GetParameters()

                        if typeof<'r>.IsAssignableFrom meth.ReturnType && 
                           parameters.Length = 1 
                        then
                            all.[dynArg] <- (target, meth)
                            rebuild x
                        else
                            failwithf "[Dispatcher] invalid implementation: %A" meth

                    | None ->
                        all.[dynArg] <- (null, null)
                        rebuild x
        )
        x.TryInvoke(a,&res)

    static member Create (methods : list<obj * MethodInfo>) =
        
        let targets =
            methods 
                |> Seq.map (fun (a,b) -> 
                    if b.IsGenericMethod then (b.GetGenericMethodDefinition(),a)
                    else (b,a)
                   )
                |> Dictionary.ofSeq

        let tryGetMethodInfo(retType : Type) (types : Type[])=
            let goodOnes = 
                targets.Keys
                    |> Seq.choose (fun mi -> OverloadResolution.tryInstantiate mi types)
                    |> Seq.filter (fun mi -> retType.IsAssignableFrom mi.ReturnType)
                    |> Seq.cast
                    |> Seq.toArray

            if goodOnes.Length > 0 then
                let selected =
                    if goodOnes.Length = 1 then 
                        goodOnes.[0] |> unbox<MethodInfo>
                    else 
                        
                        Type.DefaultBinder.SelectMethod(
                            BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.InvokeMethod,
                            goodOnes,
                            types,
                            types |> Array.map (fun _ -> ParameterModifier())
                        ) |> unbox<MethodInfo>
                
                if selected.IsGenericMethod then
                    let target = targets.[selected.GetGenericMethodDefinition()]
                    Some (target, selected)
                else
                    let target = targets.[selected]
                    Some (target, selected)
            else
                None

        let dispatcher =
            Dispatcher<'r>(fun t ->
                tryGetMethodInfo typeof<'r> [| t |]
            )

        dispatcher  

    static member Create (lambdas : list<obj>) =
        lambdas 
            |> List.choose (fun l ->
                let t = l.GetType()
                let best = t.GetMethods() |> Array.filter (fun mi -> mi.Name = "Invoke") |> Array.maxBy (fun mi -> mi.GetParameters().Length)
                let p = best.GetParameters()
                if p.Length = 1 && typeof<'r>.IsAssignableFrom best.ReturnType then
                    Some (l, best)
                else
                    None
            )
            |> Dispatcher<'r>.Create


module Dispatcher =

    let inline create (methods : ^a) : ^b =
        (^a : (static member Create : ^a -> ^b) (methods))

//
//
//    let inline ofLambdas (lambdas : list<obj>) : ^a =
//        lambdas 
//            |> List.choose (fun l ->
//                let t = l.GetType()
//                let best = t.GetMethods() |> Array.filter (fun mi -> mi.Name = "Invoke") |> Array.maxBy (fun mi -> mi.GetParameters().Length)
//                let p = best.GetParameters()
//                if p.Length = 2 && p.[1].ParameterType.IsAssignableFrom typeof<'b> && typeof<'r>.IsAssignableFrom best.ReturnType then
//                    Some (l, best)
//                else
//                    None
//            )
//            |> ofMethods
