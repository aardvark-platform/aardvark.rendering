namespace Aardvark.Base

open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Quotations.ExprShape
open Aardvark.Base.Monads.State
open Aardvark.Base.IL
open System.Reflection

module Patterns =
    open System.Text.RegularExpressions
    let private rx = Regex @"Dynamic invocation of (?<name>[a-zA-Z_0-9]+) is not supported"

    let (|StaticInvoke|_|) (code : list<Instruction>) =
        match code with
            | LdConst (String str) :: NewObj ctor :: Throw :: rest ->
                let m = rx.Match(str)
                if m.Success then
                    Some(m.Groups.["name"].Value, rest)
                else
                    None
            | _ ->
                None

    

module Compiler =

    type ConcList<'a> =
        | Empty
        | Leaf of list<'a>
        | Concat of ConcList<'a> * ConcList<'a>

    module ConcList =
        let rec toList (l : ConcList<'a>) =
            match l with
                | Empty -> []
                | Leaf l -> l
                | Concat(l,r) -> toList l @ toList r


    type CompilerState =
        {
            variables : Map<Var, Local>
            closure : hmap<obj, Local>
            instructions : ConcList<Instruction>
        }
    
    type Compiled<'a> = State<CompilerState, 'a>

    type CompiledBuilder() =
        inherit StateBuilder()

        member x.Yield(i : Instruction) =
            State.modify (fun s -> { s with CompilerState.instructions = Concat (s.instructions, Leaf [i]) } )

        member x.Yield(i : list<Instruction>) =
            State.modify (fun s -> { s with CompilerState.instructions = Concat (s.instructions, Leaf i) } )

        member x.YieldFrom(l : Compiled<unit>) =
            l

    let compiled = CompiledBuilder()

    module Compiled =
        let spill (o : obj) (t : Type) =
            state {
                let! s = State.get
                match HMap.tryFind o s.closure with
                    | Some l -> return l
                    | None ->
                        let l = Local(t)
                        do! State.put { s with closure = HMap.add o l s.closure }
                        return l
            }

        let resolve (v : Var) =
            state {
                let! s = State.get
                match Map.tryFind v s.variables with
                    | Some v -> return v
                    | None -> return failwith "sadsadasds"
            }

        let def (v : Var) =
            state {
                let! s = State.get
                let l = Local(v.Type)
                do! State.put { s with variables = Map.add v l s.variables }
                return l
            }

        let undef (v : Var) =
            State.modify (fun s -> { s with variables = Map.remove v s.variables })
        
    let (|Constant|_|) (v : obj) =
        match v with
            | :? int8 as v -> Some (Int8 v) 
            | :? uint8 as v -> Some (UInt8 v) 
            | :? int16 as v -> Some (Int16 v) 
            | :? uint16 as v -> Some (UInt16 v) 
            | :? int32 as v -> Some (Int32 v) 
            | :? uint32 as v -> Some (UInt32 v) 
            | :? int64 as v -> Some (Int64 v) 
            | :? uint64 as v -> Some (UInt64 v) 
            | :? float32 as v -> Some (Float32 v) 
            | :? float as v -> Some (Float64 v) 
            | :? nativeint as v -> Some (NativeInt v) 
            | :? unativeint as v -> Some (UNativeInt v) 
            | :? string as v -> Some (String v) 
            | _ -> None


    let ops =
        Dictionary.ofList [
            "op_Addition", Add
            "op_Subtraction", Sub
            "op_Multiplication", Mul
            "op_Division", Div
            "op_Modulus", Rem
            "op_LeftShift", Shl
            "op_RightShift", Shr
            "op_BitwiseAnd", And
            "op_BitwiseOr", Or
            "op_ExclusiveOr", Xor
        ]

    let (|Binary|_|) (e : Expr) =
        match e with
            | Microsoft.FSharp.Quotations.Patterns.Call(None, mi, [a;b]) ->
                match ops.TryGetValue mi.Name with
                    | (true, op) ->
                        if a.Type = b.Type && a.Type.IsPrimitive then
                            let body = mi.Instructions
                            printfn "%A" body
                            Some (op, a, b)
                        else
                            None
                    | _ ->
                        None
            | _ ->
                None


    let rec hasClosure (e : Expr) =
        match e with
            | Value(Constant v,_) -> false
            | Value(null, _) -> false
            | Value _ -> true
            | ShapeVar _ -> false
            | ShapeLambda(_,b) -> hasClosure b
            | ShapeCombination(_,args) -> args |> List.exists hasClosure

    let rec compileBody (args : Map<Var, int>) (e : Expr) : Compiled<unit> =
        //let args = args |> List.mapi (fun i v -> v,(i + 1)) |> Map.ofList

        compiled {
            match e with
                | Value(Constant v,_) -> 
                    yield LdConst(v)


                | Value(v,t) -> 
                    let! l = Compiled.spill v t
                    yield Ldloc l

                | Var(v) ->
                    match Map.tryFind v args with
                        | Some arg -> 
                            yield Ldarg arg
                        | None ->
                            let! l = Compiled.resolve v
                            yield Ldloc l

                | Let(v, e, b) ->
                    do! compileBody args e
                    let! l = Compiled.def v
                    yield Stloc l
                    do! compileBody args b
                    do! Compiled.undef v

                | Binary(op, a, b) ->
                    do! compileBody args a
                    do! compileBody args b
                    yield op

                | IfThenElse(guard, ib, eb) ->
                    do! compileBody args guard
                    let f = Label()
                    let e = Label()
                    yield ConditionalJump(JumpCondition.False, f)
                    do! compileBody args ib
                    yield Jump e
                    yield Mark f
                    do! compileBody args eb
                    yield Mark e

                | Microsoft.FSharp.Quotations.Patterns.Call(t, mi, p) ->
                    if mi.IsGenericMethod then
                        let def = mi.GetGenericMethodDefinition()

                        let parameters = def.GetParameters()
                        printfn "%A" (def.GetCustomAttributes())

                    if false && mi.DeclaringType.Name = "Operators" && mi.DeclaringType.Assembly.FullName.Contains "FSharp.Core" then
                        let body = mi.Instructions
                        yield body
                    else
                        match t with
                            | Some t -> do! compileBody args t
                            | None -> ()

                        for a in p do
                            do! compileBody args a

                        yield Call(mi)


                |  e ->
                    failwithf "cannot compile %A" e
        }



    open System.Reflection.Emit
    let dAss = AppDomain.CurrentDomain.DefineDynamicAssembly(AssemblyName("Closures"), AssemblyBuilderAccess.RunAndSave)
    let dMod = dAss.DefineDynamicModule("MainModule")

    let compileLambda (args : list<Var>) (e : Expr) =
        let hasClosure = hasClosure e
        let offset = if hasClosure then 1 else 0
        let m = args |> List.mapi (fun i a -> a,(i + offset)) |> Map.ofList
        let res = compileBody m e

        let mutable state =
            {
                variables = Map.empty
                closure = HMap.empty
                instructions = Empty
            }
        res.RunUnit(&state)

       
        let realArgTypes = args |> List.map (fun a -> a.Type)

        let closureType =
            if hasClosure then
                let dType = dMod.DefineType(Guid.NewGuid() |> string)

                let closure = state.closure |> HMap.toList

                let closureTypes = closure |> List.map (fun (_,l) -> l.Type) |> List.toArray

                let fields =
                    closure |> List.mapi (fun i (v,l) ->
                        let name = sprintf "c%d" i
                        dType.DefineField(name, l.Type, FieldAttributes.Public), l, v
                    )
                    

                let ctor = dType.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, closureTypes)

                let il = ctor.GetILGenerator()


                let mutable i = 0uy
                il.Emit(OpCodes.Ldarg_0)
                il.Emit(OpCodes.Call, typeof<obj>.GetConstructor [||])     

                for (f,_,v) in fields do
                    il.Emit(OpCodes.Ldarg_0)
                    il.Emit(OpCodes.Ldarg_S, i + 1uy)
                    il.Emit(OpCodes.Stfld, f)
                    i <- i + 1uy

                il.Emit(OpCodes.Ret)

                let t = dType.CreateType()
                let ctor = dType.GetConstructor(closureTypes)
                let instance = ctor.Invoke(closure |> List.map fst |> List.toArray)

                let loadFields = 
                    [
                        let mutable i = 0
                        for (_,l) in closure do
                            let f = t.GetField(sprintf "c%d" i)
                            yield Ldarg 0
                            yield Ldfld f
                            yield Stloc l
                            i <- i + 1
                    ]

                Some(t, instance, loadFields)
            else
                None

        let argTypes =
            match closureType with
                | Some (t,_,_) -> t :: realArgTypes
                | None -> realArgTypes

        let meth = 
            DynamicMethod(
                Guid.NewGuid() |> string,
                MethodAttributes.Public ||| MethodAttributes.Static,
                CallingConventions.Standard,
                e.Type,
                List.toArray argTypes,
                typeof<obj>,
                true
            )

        let code = 
            match closureType with
                | Some (_,_,load) ->
                    Concat(Leaf load, state.instructions) |> ConcList.toList
                | _ ->
                    state.instructions |> ConcList.toList
        
        let il = meth.GetILGenerator()
        code |> Assembler.assembleTo il
        il.Emit(OpCodes.Ret)

        let dType = System.Linq.Expressions.Expression.GetDelegateType(List.toArray (realArgTypes @ [e.Type]))
        match closureType with
            | Some (_,o,_) -> meth.CreateDelegate(dType, o)
            | None -> meth.CreateDelegate(dType)

    let rec deconstruct (e : Expr) =
        match e with
            | Lambda(v,b) ->
                let args, body = deconstruct b
                v::args, body
            | _ -> [], e

    let (|Lambdas|_|) (e : Expr) =
        match deconstruct e with
            | [],_ -> None
            | args, body -> Some(args, body)

    let compile (e : Expr) =
        match e with
            | Lambdas(args,b) -> compileLambda args b
            | _ -> failwith ""

    let sepp(a : float) = a

//    let inline lenny (a : ^a) =
//        printfn "asdasdsadsad"
//        (^a : (member Length : ^b) (a))
//
//
//    let seppy() =
//        lenny V2d.II

    [<Demo("Compiler")>]
    let test () =

        let value = V2i.II
        let test = compile <@ fun (a : float) (b : float) -> value @> |> unbox<Func<float, float, float>>


        test.Invoke(10.0, 3.0) |> printfn "equal: %A"
    
        ()
