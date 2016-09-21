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

[<StructuredFormatDisplay("{AsString}")>]
type MethodTable(items : list<obj * MethodInfo>) =
    static let pretty (t : Type) = Aardvark.Base.ReflectionHelpers.getPrettyName t

    let methods = items |> List.map snd
    let targets = 
        items 
            |> List.map (fun (target,mi) ->
                if mi.IsGenericMethod then mi.GetGenericMethodDefinition(), target
                else mi, target
               )
            |> Dictionary.ofList 

    member x.Methods = methods

    member x.TryGetTarget(mi : MethodInfo) =
        if mi.IsGenericMethod then
            match targets.TryGetValue(mi.GetGenericMethodDefinition()) with
                | (true, t) -> Some t
                | _ -> None
        else    
            match targets.TryGetValue(mi) with
                | (true, t) -> Some t
                | _ -> None

    member private x.AsString =
        items 
            |> List.map (fun (_,mi) -> 
                let t = pretty mi.DeclaringType

                let generic =
                    if mi.IsGenericMethod then 
                        mi.GetGenericArguments() 
                            |> Seq.map pretty
                            |> String.concat ", "
                            |> sprintf "<%s>"
                    else
                        ""

                let args =
                    mi.GetParameters()
                        |> Array.map (fun p -> sprintf "%s : %s" p.Name (pretty p.ParameterType))
                        |> String.concat ", "

                if FSharpType.IsFunction mi.DeclaringType then
                    sprintf "%s" t
                else
                    sprintf "%s.%s%s(%s)" t mi.Name generic args
           )
        |> String.concat "\r\n"

    override x.ToString() = x.AsString

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OverloadResolution =
    [<AutoOpen>]
    module private Implementation = 
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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MethodTable =

    let ofList (items : list<obj * MethodInfo>) = 
        MethodTable(items)

    let ofSeq (seq : seq<obj * MethodInfo>) = 
        seq |> Seq.toList |> ofList

    let ofArray (seq : array<obj * MethodInfo>) = 
        seq |> Array.toList |> ofList

    let tryResolve (retType : Type) (types : Type[]) (table : MethodTable) =
        let goodOnes = 
            table.Methods
                |> List.choose (fun mi -> 
                    match OverloadResolution.tryInstantiate mi types with
                        | Some mi when retType.IsAssignableFrom mi.ReturnType ->
                            Some (mi :> MethodBase)
                        | _ ->
                            None
                   )
                |> List.toArray

        if goodOnes.Length > 0 then
            let selected =
                if goodOnes.Length = 1 then 
                    goodOnes.[0]
                else 
                    Type.DefaultBinder.SelectMethod(
                        BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.InvokeMethod,
                        goodOnes,
                        types,
                        null
                    ) 
                
            let selected = unbox<MethodInfo> selected
            match table.TryGetTarget selected with
                | Some t -> Some (t, selected)
                | None -> None
        else
            None



[<AutoOpen>]
module private ``ILGenerator Extensions`` =
    open Aardvark.Base.IL

    let private inlineCode = System.Collections.Concurrent.ConcurrentDictionary<MethodInfo, list<Instruction>>()

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

   
        member x.InlineCall(meth : MethodInfo) =
            let code =
                inlineCode.GetOrAdd(meth, fun meth ->
                    let code = Disassembler.disassemble meth



                    let parameters = meth.GetParameters() |> Array.map (fun p -> Local(p.ParameterType))
                    let this = 
                        if meth.IsStatic then None
                        else Some(Local(meth.DeclaringType))

                    let arg (i : int) =
                        match this with
                            | Some t ->
                                if i = 0 then t
                                else parameters.[i-1]
                            | _ ->
                                parameters.[i]

            
                    let endLabel = Label()

                    let usedArgs = 
                        code.Body 
                            |> List.choose (function Ldarg a -> Some a | _ -> None)
                            |> Set.ofList
                            |> Set.map arg

                    let code =
                        [
                            for p in Array.rev parameters do
                                if Set.contains p usedArgs then yield Stloc(p)
                                else yield Pop

                            match this with
                                | Some t -> 
                                    if Set.contains t usedArgs then yield Stloc(t)
                                    else yield Pop
                                | _ -> ()

                            let mutable needsEndLabel = false
                            let body = List.toArray code.Body
                            for i in 0..body.Length-1 do
                                match body.[i] with
                                    | Start | Nop -> ()
                                    | Ldarg a -> yield Ldloc (arg a)
                                    | Ret -> 
                                        if i <> body.Length-1 then 
                                            needsEndLabel <- true
                                            yield Jump(endLabel)

                                    | i -> 
                                        yield i

                            if needsEndLabel then
                                yield Mark(endLabel)
                        ]

                    Log.start "inlining %A" meth
                    for i in code do
                        match i with
                            | Start | Nop -> ()
                            | _ -> Log.line "%A" i
                    Log.stop()

                    code
                )

            Assembler.assembleTo x code

        member x.Call(meth : MethodInfo, ?tryInline : bool) =
            let tryInline = defaultArg tryInline false
            if tryInline then
                x.InlineCall(meth)
            else 
                if meth.IsVirtual then x.EmitCall(OpCodes.Callvirt, meth, null)
                else x.EmitCall(OpCodes.Call, meth, null)
            

    type DynamicMethod with
        member x.CreateDelegate<'a>() =
            x.CreateDelegate(typeof<'a>) |> unbox<'a>

    type System.Type with
        member x.DeconstructLambda (cnt : int) = 
            let rec args (c : int) (t : Type) =
                if c = 0 then [], t
                else
                    let (a,ret) = FSharpType.GetFunctionElements t
                    let args, ret = args (c - 1) ret
                    a :: args, ret
            args cnt x

        member x.InvokeMethod (cnt : int) =
            let args, ret = x.DeconstructLambda(cnt)

            x.GetMethod(
                "Invoke",
                BindingFlags.Public ||| BindingFlags.Instance, 
                Type.DefaultBinder, 
                CallingConventions.Any, 
                List.toArray args, 
                null
            )


type private DispatcherInfo<'f> =
    class
        val mutable public self        : 'f
        val mutable public targets     : obj[]
        val mutable public tableSize   : int
        val mutable public collisions  : int

        new() = { self = Unchecked.defaultof<'f>; targets = null; tableSize = 0; collisions = 0 }
    end

module private DispatcherConfig =

    [<Literal>]
    let MaxCollisionPercentage = 2

    let inline LogTable (n : Dictionary<_,_>) (size : int) (collisions : int) =
        #if DEBUG
        Log.line "table: { n = %A; |table| = %A; collisions = %A }" n.Count size collisions
        #else
        ()
        #endif

type Dispatcher<'r> (tryGet : Type -> Option<obj * MethodInfo>) =

    static let emptyTargets : obj[]     = Array.zeroCreate 0
    static let rebuildMeth              = typeof<Dispatcher<'r>>.GetMethod("Rebuild", BindingFlags.NonPublic ||| BindingFlags.Instance)
    static let getTypeMeth              = typeof<obj>.GetMethod("GetType")

    static let initial =
        FuncRef1<Dispatcher<'r>, obj[], obj, 'r, bool>(fun d targets a res ->
            d.Rebuild(a,&res)
        )

    let implementations = Dictionary<Type, obj * MethodInfo>()
    let mutable info =
        DispatcherInfo(
            self = initial,
            targets = emptyTargets,
            tableSize = 0,
            collisions = 0
        )


    let perfectTable () =
        let minId = implementations.Keys |> Seq.map (fun t -> t.TypeHandle.Value) |> Seq.min

        let rec createTable (size : int) =  
            let table = Array.create size []
            let mutable collisions = 0
                
            for (t,(tar, meth)) in Dictionary.toSeq implementations do
                let e = int (t.TypeHandle.Value - minId) % table.Length
                match table.[e] with
                    | [] -> 
                        table.[e] <- [(t, tar, meth)]
                    | _ ->
                        table.[e] <- (t, tar, meth) :: table.[e] 
                        collisions <- collisions + 1

   
            if implementations.Count = 1 then
                collisions, table
            elif 100 * collisions < DispatcherConfig.MaxCollisionPercentage * implementations.Count then
                DispatcherConfig.LogTable implementations size collisions
                collisions, table
            else
                createTable (size + 1)

        let collisions, table = createTable implementations.Count
        minId, collisions, table

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
        for (t,_) in implementations.Values do
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


        let callWhenType (otherwise : Label) (t : Type) (target : obj) (meth : MethodInfo) =    
            // if the types don't match goto error
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(t.TypeHandle.Value)
            il.Emit(OpCodes.Bne_Un, otherwise)

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

                il.Call(meth, true)

                il.Convert(meth.ReturnType, typeof<'r>)
                let l = il.DeclareLocal(typeof<'r>)
                il.Emit(OpCodes.Stloc, l)

                il.Emit(OpCodes.Ldarg_3)
                il.Emit(OpCodes.Ldloc, l)
                il.Emit(OpCodes.Stobj, typeof<'r>)

                il.Emit(OpCodes.Ldc_I4_1)
                il.Emit(OpCodes.Ret)

        let rec buildCascade (l : list<Type * obj * MethodInfo>) =
            match l with
                | [] -> 
                    il.Emit(OpCodes.Br, errorLabel)

                | [t,target,meth] ->
                    callWhenType errorLabel t target meth

                | (t,target,meth) :: rest ->
                    let label = il.DefineLabel()
                    callWhenType label t target meth
                    il.MarkLabel(label)
                    buildCascade rest


        let mutable tableSize = 0
        let mutable collisions = 0

        if implementations.Count = 0 then
            tableSize <- 0
            il.Emit(OpCodes.Br, errorLabel)

        if implementations.Count = 1 then
            tableSize <- 1
            let (KeyValue(t, (target, meth))) = implementations |> Seq.head
            callWhenType errorLabel t target meth

        else
            let minHandle, c, table = perfectTable()
            let caseLabels = table |> Array.map (fun _ -> il.DefineLabel())
            tableSize <- table.Length
            collisions <- c


            // it := (<type> - minHandle) % table.Length
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(minHandle)
            il.Emit(OpCodes.Sub)
            il.Ldc(nativeint table.Length)
            il.Emit(OpCodes.Rem)
            il.Emit(OpCodes.Conv_I4)


            // switch(it)
            il.Emit(OpCodes.Switch, caseLabels)
            il.Emit(OpCodes.Br, errorLabel)

            for i in 0 .. table.Length-1 do
                let caseLabel = caseLabels.[i]
                il.MarkLabel(caseLabel)
                buildCascade table.[i]



        il.MarkLabel(errorLabel)

 
        il.Emit(OpCodes.Ldarg_0)
        il.Emit(OpCodes.Ldarg_2)
        il.Emit(OpCodes.Ldarg_3)
        il.Call(rebuildMeth, false)
        il.Emit(OpCodes.Ret)

        let newSelf = meth.CreateDelegate<FuncRef1<Dispatcher<'r>, obj[], obj, 'r, bool>>()

        let newInfo =
            DispatcherInfo(
                self = newSelf,
                targets = targets.ToArray(),
                tableSize = tableSize,
                collisions = collisions           
            )
        
        info <- newInfo

    member x.TableSize = info.tableSize
    member x.Collisions = info.collisions

    member x.TryInvoke(a : obj, [<Out>] res : byref<'r>) : bool =
//        match a with
//            | null -> false
//            | _ ->
                let info = info
                info.self.Invoke(x, info.targets, a, &res)

    member x.Invoke(a : obj) =
        match x.TryInvoke(a) with
            | (true, r) -> r
            | _ -> failwithf "[Dispatcher] could not run with %A" a

    member private x.Rebuild(a : obj, res : byref<'r>) : bool =
        let dynArg = a.GetType()
        lock x (fun () ->
            if implementations.ContainsKey dynArg then
                ()
            else
                match tryGet dynArg with
                    | Some (target, meth) -> 
                        let parameters = meth.GetParameters()

                        if typeof<'r>.IsAssignableFrom meth.ReturnType && 
                           parameters.Length = 1
                        then
                            implementations.[dynArg] <- (target, meth)
                            rebuild x
                        else
                            failwithf "[Dispatcher] invalid implementation: %A" meth

                    | None ->
                        implementations.[dynArg] <- (null, null)
                        rebuild x
        )
        x.TryInvoke(a,&res)

    static member Create (methods : list<obj * MethodInfo>) =
        let table = MethodTable.ofList methods
        let dispatcher =
            Dispatcher<'r>(fun t ->
                table |> MethodTable.tryResolve typeof<'r> [| t |]
            )

        dispatcher  

    static member Create (lambdas : list<obj>) =
        lambdas 
            |> List.choose (fun l ->
                let best = l.GetType().InvokeMethod 1
                if isNull best then None
                else Some(l, best)
               )
            |> Dispatcher<'r>.Create

    static member Create (f : Type -> Option<obj>) =
        Dispatcher<'r>(fun t ->
            match f t with
                | Some lambda ->
                    let t = lambda.GetType()
                    let a, res = FSharpType.GetFunctionElements t
                    let best = 
                        t.GetMethod(
                            "Invoke", 
                            BindingFlags.Public ||| BindingFlags.Instance, 
                            Type.DefaultBinder, 
                            CallingConventions.Any, 
                            [| a |], 
                            null
                        )   
                    Some (lambda, best)
                | None ->
                    None
        )

    static member Compiled (f : Type -> Option<Expr>) =
        Dispatcher<'r>(fun t ->
            match f t with
                | Some e ->
                    let lambda = QuotationCompiler.ToDynamicAssembly(e, "Dispatcher").Invoke(null, [||])
                    let best =  lambda.GetType().InvokeMethod 1
                    if isNull best then None
                    else Some (lambda, best)

                | None ->
                    None
        )

type Dispatcher<'b, 'r> (tryGet : Type -> Option<obj * MethodInfo>) =

    static let emptyTargets : obj[]     = Array.zeroCreate 0
    static let rebuildMeth              = typeof<Dispatcher<'b, 'r>>.GetMethod("Rebuild", BindingFlags.NonPublic ||| BindingFlags.Instance)
    static let getTypeMeth              = typeof<obj>.GetMethod("GetType")

    static let initial =
        FuncRef1<Dispatcher<'b, 'r>, obj[], obj, 'b, 'r, bool>(fun d targets a b res ->
            d.Rebuild(a,b,&res)
        )

    let implementations = Dictionary<Type, obj * MethodInfo>()
    let mutable info =
        DispatcherInfo(
            self = initial,
            targets = emptyTargets,
            tableSize = 0,
            collisions = 0
        )


    let perfectTable () =
        let minId = implementations.Keys |> Seq.map (fun t -> t.TypeHandle.Value) |> Seq.min

        let rec createTable (size : int) =  
            let table = Array.create size []
            let mutable collisions = 0
                
            for (t,(tar, meth)) in Dictionary.toSeq implementations do
                let e = int (t.TypeHandle.Value - minId) % table.Length
                match table.[e] with
                    | [] -> 
                        table.[e] <- [(t, tar, meth)]
                    | _ ->
                        table.[e] <- (t, tar, meth) :: table.[e] 
                        collisions <- collisions + 1

   
            if implementations.Count = 1 then
                collisions, table
            elif 100 * collisions < DispatcherConfig.MaxCollisionPercentage * implementations.Count then
                DispatcherConfig.LogTable implementations size collisions
                collisions, table
            else
                createTable (size + 1)

        let collisions, table = createTable implementations.Count
        minId, collisions, table

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
        for (t,_) in implementations.Values do
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


        let callWhenType (otherwise : Label) (t : Type) (target : obj) (meth : MethodInfo) =    
            // if the types don't match goto error
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(t.TypeHandle.Value)
            il.Emit(OpCodes.Bne_Un, otherwise)

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

                il.Call(meth, true)

                il.Convert(meth.ReturnType, typeof<'r>)
                let l = il.DeclareLocal(typeof<'r>)
                il.Emit(OpCodes.Stloc, l)


                il.Emit(OpCodes.Ldarg_S, 4uy)
                il.Emit(OpCodes.Ldloc, l)
                il.Emit(OpCodes.Stobj, meth.ReturnType)

                il.Emit(OpCodes.Ldc_I4_1)
                il.Emit(OpCodes.Ret)

        let rec buildCascade (l : list<Type * obj * MethodInfo>) =
            match l with
                | [] -> 
                    il.Emit(OpCodes.Br, errorLabel)

                | [t,target,meth] ->
                    callWhenType errorLabel t target meth

                | (t,target,meth) :: rest ->
                    let label = il.DefineLabel()
                    callWhenType label t target meth
                    il.MarkLabel(label)
                    buildCascade rest


        let mutable tableSize = 0
        let mutable collisions = 0

        if implementations.Count = 0 then
            tableSize <- 0
            il.Emit(OpCodes.Br, errorLabel)

        if implementations.Count = 1 then
            tableSize <- 1
            let (KeyValue(t, (target, meth))) = implementations |> Seq.head
            callWhenType errorLabel t target meth

        else
            let minHandle, c, table = perfectTable()
            let caseLabels = table |> Array.map (fun _ -> il.DefineLabel())
            tableSize <- table.Length
            collisions <- c


            // it := (<type> - minHandle) % table.Length
            il.Emit(OpCodes.Ldloc, dynType)
            il.Ldc(minHandle)
            il.Emit(OpCodes.Sub)
            il.Ldc(nativeint table.Length)
            il.Emit(OpCodes.Rem)
            il.Emit(OpCodes.Conv_I4)


            // switch(it)
            il.Emit(OpCodes.Switch, caseLabels)
            il.Emit(OpCodes.Br, errorLabel)

            for i in 0 .. table.Length-1 do
                let caseLabel = caseLabels.[i]
                il.MarkLabel(caseLabel)
                buildCascade table.[i]



        il.MarkLabel(errorLabel)

 
        il.Emit(OpCodes.Ldarg_0)
        il.Emit(OpCodes.Ldarg_2)
        il.Emit(OpCodes.Ldarg_3)
        il.Emit(OpCodes.Ldarg_S, 4uy)
        il.Call(rebuildMeth, false)
        il.Emit(OpCodes.Ret)

        let newSelf = meth.CreateDelegate<FuncRef1<Dispatcher<'b, 'r>, obj[], obj, 'b, 'r, bool>>()

        let newInfo =
            DispatcherInfo(
                self = newSelf,
                targets = targets.ToArray(),
                tableSize = tableSize,
                collisions = collisions           
            )
        
        info <- newInfo

    member x.TableSize = info.tableSize
    member x.Collisions = info.collisions

    member x.TryInvoke(a : obj, b : 'b, [<Out>] res : byref<'r>) : bool =
//        match a with
//            | null -> false
//            | _ ->
                let info = info
                info.self.Invoke(x, info.targets, a, b, &res)

    member x.Invoke(a : obj, b : 'b) =
        match x.TryInvoke(a,b) with
            | (true, r) -> r
            | _ -> failwithf "[Dispatcher] could not run with (%A, %A)" a b

    member private x.Rebuild(a : obj, b : 'b, res : byref<'r>) : bool =
        let dynArg = a.GetType()
        lock x (fun () ->
            if implementations.ContainsKey dynArg then
                ()
            else
                match tryGet dynArg with
                    | Some (target, meth) -> 
                        let parameters = meth.GetParameters()

                        if typeof<'r>.IsAssignableFrom meth.ReturnType && 
                           parameters.Length = 2 && 
                           parameters.[1].ParameterType.IsAssignableFrom typeof<'b> 
                        then
                            implementations.[dynArg] <- (target, meth)
                            rebuild x
                        else
                            failwithf "[Dispatcher] invalid implementation: %A" meth

                    | None ->
                        implementations.[dynArg] <- (null, null)
                        rebuild x
        )
        x.TryInvoke(a,b,&res)

    static member Create (methods : list<obj * MethodInfo>) =
        let table = MethodTable.ofList methods
        let dispatcher =
            Dispatcher<'b, 'r>(fun t ->
                table |> MethodTable.tryResolve typeof<'r> [| t; typeof<'b> |]
            )

        dispatcher  

    static member Create (lambdas : list<obj>) =
        lambdas 
            |> List.choose (fun l ->
                let best = l.GetType().InvokeMethod 2
                if isNull best then None
                else Some (l, best)
               )
            |> Dispatcher<'b, 'r>.Create

    static member Create (f : Type -> Option<obj>) =
        Dispatcher<'b, 'r>(fun t ->
            match f t with
                | Some lambda ->
                    let t = lambda.GetType()
                    let best =  lambda.GetType().InvokeMethod 2
                    Some (lambda, best)
                | None ->
                    None
        )

    static member Compiled (f : Type -> Option<Expr>) =
        Dispatcher<'b, 'r>(fun t ->
            match f t with
                | Some e ->
                    let lambda = QuotationCompiler.ToDynamicAssembly(e, "Dispatcher").Invoke(null, [||])
                    let best =  lambda.GetType().InvokeMethod 2
            
                    if isNull best then  None
                    else Some (lambda, best)

                | None ->
                    None
        )


