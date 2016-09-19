namespace Aardvark.Ag

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

type SemanticAttribute() = inherit Attribute()

[<AutoOpen>]
module Operators =
    let (?) (o : 'a) (name : string) : 'b =
        failwith ""



module Test =
 
    type TraversalState(parent : Option<TraversalState>, node : obj, values : Map<string, Option<obj>>) =
        let mutable cache = values
        static let root = TraversalState(None, null, Map.empty)
        static member Root = root

        member x.Parent = parent
        member x.Node = node

        member x.ChildState(n : obj) =
            if isNull node then TraversalState(None, n, Map.empty)
            else TraversalState(Some x, n, Map.empty)

        member x.ChildState(n : obj, converters : list<string * (obj -> obj)>) =
            let values = 
                converters |> List.map (fun (name, f) ->
                    match cache.[name] with
                        | Some v -> name, Some (f v)
                        | None -> name, None
                )
                |> Map.ofList

            if isNull node then TraversalState(None, n, values)
            else TraversalState(Some x, n, values)

        member x.TryGet(name : string) =
            match Map.tryFind name cache with
                | Some v -> v
                | _ -> None

        member x.Set(name : string, value : Option<obj>) =
            cache <- Map.add name value cache

        member x.GetOrCreate(name : string, f : string -> Option<'a>) =
            match Map.tryFind name cache with
                | Some v -> v |> Option.map unbox
                | None ->
                    let v = f name
                    cache <- Map.add name (v |> Option.map (fun v -> v :> obj)) cache
                    v


    type Root<'a> = class end
    type Inh = class end
    let inh = Unchecked.defaultof<Inh> 

    let (<<=) (i : Inh) (value : 'a) =
        ()


    type ITraversal<'a> =
        abstract member Run : obj -> 'a
        abstract member WithState : TraversalState -> ITraversal<'a>
        abstract member State : TraversalState

    [<Struct>]
    type SynthesizeTraversal<'a>(syn : SynthesizeTraversal<'a> -> obj -> Option<'a>, state : TraversalState) =

        member x.State : TraversalState = 
            state

        member x.WithState (state : TraversalState) =
            SynthesizeTraversal<'a>(syn, state)

        member x.Run(o : obj) =
            syn (x.WithState (state.ChildState o)) o |> Option.get
 

        member x.GetValue<'a>(name : string) =
            match state.TryGet(name) with
                | Some (:? 'a as v) -> v
                | _ -> failwith ""

        interface ITraversal<'a> with
            member x.Run(o) = x.Run(o)
            member x.WithState s = x.WithState s :> ITraversal<_>
            member x.State = x.State
                


    [<Struct>]
    type InheritTraversal<'a>(name : string, inh : InheritTraversal<'a> -> obj -> Option<'a>, root : InheritTraversal<'a> -> obj -> Option<'a>, state : TraversalState) =

        member private x.create o name =
            match state.Parent with
                | Some p -> inh (x.WithState p) o
                | None -> root x o

        member x.State : TraversalState = 
            state

        member x.WithState (state : TraversalState) =
            InheritTraversal<'a>(name, inh, root, state)

        member x.Run(o : obj) =
            state.GetOrCreate(name, x.create o) |> Option.get
 
 

        interface ITraversal<'a> with
            member x.Run(o) = x.Run(o)
            member x.WithState s = x.WithState s :> ITraversal<_>
            member x.State = x.State
                

                      

    
    type IList =
        abstract member Sum : TraversalState -> int

    type Nil() = 
        interface IList with
            member x.Sum(s) = s.TryGet "Index" |> Option.get |> unbox<int>

    type Cons(h : int, t : IList) =
        interface IList with
            member x.Sum(s) =
                let i = s.TryGet "Index" |> Option.get |> unbox<int>
                let s = s.ChildState(t)
                s.Set("Index", Some ((i + 1) :> obj))
                h + t.Sum(s)

        member x.Head = h
        member x.Tail = t


    [<Semantic; ReflectedDefinition>]
    type Sems() =

        member x.Sum(n : Nil) : int =
            0

        member x.Sum(c : Cons) =
            c.Head + c.Tail?Sum()



        member x.Index(l : Root<IList>) = 
            inh <<= 0

        member x.Index(c : Cons) = 
            let id = c?Index
            inh <<= id + 1
    
    
    
    type SemanticFunctionKind =
        | Inherit
        | Synthesize

    type SemanticFunction =
        {
            name        : string
            kind        : SemanticFunctionKind
            strictInh   : Set<string>
            strictSyn   : Set<string>
            original    : MethodInfo
            isRoot      : bool
            nodeType    : Type
            valueType   : Type
            definition  : Expr
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SemanticFunction =
        
        let private all = BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.CreateInstance
        let private prettyName (t : Type) =
            Aardvark.Base.ReflectionHelpers.getPrettyName t

        let private methodName (m : MethodInfo) =
            let t = prettyName m.DeclaringType
            let args = m.GetParameters() |> Array.map (fun p -> sprintf "%s : %s" p.Name (prettyName p.ParameterType)) |> String.concat ", "
            sprintf "%s.%s(%s)" t m.Name args

        let private instances = Dict<Type, obj>()
        let private getInstance (t : Type) =
            instances.GetOrCreate(t, fun t ->
                let ctor = t.GetConstructor(all, Type.DefaultBinder, CallingConventions.Any, [||], null)
                if isNull ctor then
                    failwithf "[Ag] cannot create semantic-type '%s' (no empty constructor)" (prettyName t)

                ctor.Invoke [||]
            )


        let ofSeq (name : string) (getTraversal : string -> Type -> Expr) (methods : seq<MethodInfo>) =
            let methods = Seq.toList methods

            // try to get definitions for all methods
            let definitions = 
                methods |> List.choose (fun mi ->
                    if mi.IsGenericMethodDefinition || mi.GetParameters().Length <> 1 then
                        Log.warn "[Ag] ill-formed semantic function '%s' (skipping)" (methodName mi)
                        None
                    else
                        match Expr.TryGetReflectedDefinition mi with
                            | Some d -> 
                                Some {
                                    name        = mi.Name
                                    kind        = Synthesize
                                    strictInh   = Set.empty
                                    strictSyn   = Set.empty
                                    original    = mi
                                    isRoot      = false
                                    nodeType    = mi.GetParameters().[0].ParameterType
                                    valueType   = mi.ReturnType
                                    definition  = d
                                }
                            | _ ->
                                Log.warn "[Ag] could not get definition for '%s' (skipping)" (methodName mi)
                                None
                )

            // remove all 'this' references from the methods and replace them with an appropriate 
            // cached instance (created on demand)
            let definitions = 
                definitions |> List.map (fun sf ->
                    { sf with
                        definition =
                            match sf.definition with
                                | Lambda(v,b) when v.Type.IsDefined(typeof<SemanticAttribute>) -> 
                                    b.Substitute (fun vi -> if v = vi then Some (Expr.Value(getInstance v.Type, v.Type)) else None)
                                | e -> 
                                    e
                    }
                )

            
            
            // replace all attribute-lookups with traversal-calls and add the traversal argument
            // to all semantic functions
            let definitions = 
                let run t = typedefof<ITraversal<_>>.MakeGenericType([|t|]).GetMethod("Run")
                let withState (self : Expr) (t : Expr) =
                    Expr.Call(t, t.Type.GetMethod "WithState", [Expr.PropertyGet(self, self.Type.GetProperty "State")])

                let convert (t : Type) (e : Expr) = Expr.Coerce(e, t)
                let rec substituteAttributeLookups (self : ref<Option<Var>>) (strictInh : HashSet<string>) (strictSyn : HashSet<string>) (kind : ref<SemanticFunctionKind>) (e : Expr) =
                    match e with
                        | Application(Call(None, mi, [o; Value(:? string as nn, _)]), Value(_)) when mi.Name = "op_Dynamic" ->
                            // syn case

                            let traversal =
                                match !self with
                                    | Some t -> t
                                    | None ->
                                        let t = typedefof<ITraversal<_>>.MakeGenericType [|e.Type|]
                                        let v = Var("traversal", t)
                                        self := Some v
                                        v

                            kind := Synthesize
                            strictSyn.Add nn |> ignore
                            if nn = name then
                                Expr.Call(Expr.Var traversal, run e.Type, [Expr.Coerce(o, typeof<obj>)])
                            else
                                let t = getTraversal nn e.Type |> withState (Expr.Var traversal)
                                Expr.Call(t, run e.Type, [Expr.Coerce(o, typeof<obj>)])

                        | Call(None, mi, [o; Value(:? string as nn, _)]) when mi.Name = "op_Dynamic" ->
                            // inh case
                            strictInh.Add nn |> ignore
                            let var = Var(sprintf "__inh%s" nn, e.Type)
                            Expr.Var var

                        | Call(None, mi, [_; value]) when mi.Name = "op_LessLessEquals" ->
                            kind := Inherit
                            substituteAttributeLookups self strictInh strictSyn kind value


                        | ShapeVar(v) ->
                            e

                        | ShapeLambda(v,b) ->
                            Expr.Lambda(v, substituteAttributeLookups self strictInh strictSyn kind b)

                        | ShapeCombination(o, args) ->
                            RebuildShapeCombination(o, args |> List.map (substituteAttributeLookups self strictInh strictSyn kind))


                definitions |> List.map (fun d ->
                    let mutable kind = ref Synthesize
                    let strictInh = HashSet()
                    let strictSyn = HashSet()
                    let self = ref None
                    let def = substituteAttributeLookups self strictInh strictSyn kind d.definition
                    strictInh.Remove d.name |> ignore
              
                    let (|RootVar|_|) (v : Var) = 
                        let t = v.Type
                        if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Root<_>> then
                            let t = t.GetGenericArguments().[0]
                            Some t
                        else
                            None

                    let isRoot, def =
                        match def with
                            | Lambda(RootVar t,b) ->
                                let def = Expr.Lambda(Var("caller", t), b)
                                true, def
                            | _ -> 
                                false, def
                                    

                    let nodeType, retType = FSharpType.GetFunctionElements(def.Type)

                    let traversal = 
                        match !self with
                            | Some t -> t
                            | None -> 
                                let t = typedefof<ITraversal<_>>.MakeGenericType [| retType |]
                                Var("traversal", t)
            

                    { d with 
                        definition = Expr.Lambda(traversal, def) 
                        kind = !kind
                        strictInh = Set.ofSeq strictInh
                        strictSyn = Set.ofSeq strictSyn
                        isRoot = isRoot
                        nodeType = nodeType
                        valueType = retType
                    }
                    
                )




            definitions

        let single (name : string) (getTraversal : string -> Type -> Expr) (m : MethodInfo) =
            match ofSeq name getTraversal [m]  with
                | [] -> failwithf "[Ag] could not create semantic function for '%s'" (methodName m)
                | sf :: _ -> sf

    let mutable private traversals : obj[] = null

    

    [<Demo("Ag")>]
    let generate2() =
        let types = 
            Introspection.GetAllTypesWithAttribute<SemanticAttribute>()
                |> Seq.map (fun t -> t.E0)
                |> HashSet

        let methods =
            types 
                |> Seq.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic))
                |> Seq.filter (fun mi -> mi.GetParameters().Length = 1 && mi.Name <> "Equals" && not mi.IsGenericMethod)
                |> Seq.groupBy (fun mi -> mi.Name)
                |> Dictionary.ofSeq

        let mutable index = -1
        let indices = methods |> Dictionary.map (fun _ _ -> index <- index + 1 ; index)
        traversals <- Array.zeroCreate indices.Count

        let getTraversal (name : string) (t : Type) =
            match indices.TryGetValue name with
                | (true, index) ->
                    Expr.Coerce(<@@ traversals.[index] @@>, typedefof<ITraversal<_>>.MakeGenericType [|t|])
                | _ ->
                    failwithf "[Ag] could not get traversal for attribute '%s'" name

        let functions =
            methods |> Dictionary.map (fun k meths -> SemanticFunction.ofSeq k getTraversal meths) 


        let createDispatcher (strictInh : Set<string>) (name : string) (functions : list<SemanticFunction>) =
            let retTypes = functions |> List.map (fun sf -> sf.valueType) |> HashSet
            if retTypes.Count > 1 then
                failwithf "[Ag] ambiguous return-type for ag-semantic '%s' %A" name (Seq.toList retTypes)
            let retType = retTypes |> Seq.head


            let kinds = functions |> List.map (fun sf -> sf.kind) |> HashSet
            if kinds.Count > 1 then
                failwithf "[Ag] attribute '%s' is inh and syn" name

            let kind = kinds |> Seq.head

            let traversalType =
                match kind with
                    | Synthesize -> typedefof<SynthesizeTraversal<_>>.MakeGenericType [| retType |]
                    | Inherit -> typedefof<InheritTraversal<_>>.MakeGenericType [| retType |]


            let optionType = typedefof<Option<_>>.MakeGenericType [| retType |]
            let optionCases = FSharpType.GetUnionCases(optionType)
            let optionNone = optionCases |> Seq.find (fun c -> c.Name = "None")
            let optionSome = optionCases |> Seq.find (fun c -> c.Name = "Some")
            let optionValue = optionType.GetProperty("Value")

            let traversalGetValue = traversalType.GetMethod "GetValue"

            let traversal = Var("traversal", traversalType)
            let node = Var("node", typeof<obj>)
            
            let run t = typedefof<ITraversal<_>>.MakeGenericType([|t|]).GetMethod("Run")

            let rec build (sfs : list<SemanticFunction>) =
                match sfs with
                    | [] ->
                        match kind with
                            | Inherit -> 
                                // TODO: auto-inherit
                                Expr.NewUnionCase(optionNone, [])
                            | Synthesize -> Expr.NewUnionCase(optionNone, [])

                    | sf :: sfs ->
                        
                        let test = Expr.TypeTest(Expr.Var node, sf.nodeType)
                        let self = sf.definition

                        let freevars = self.GetFreeVars()

                        let replacements =
                            freevars
                                |> Seq.filter (fun v -> v.Name.StartsWith "__inh")
                                |> Seq.map (fun v ->
                                    let n = v.Name.Substring 5
                                    let replacement =
                                        if Set.contains n strictInh then
                                            let get = traversalGetValue.MakeGenericMethod [| v.Type |]
                                            Expr.Call(Expr.Var traversal, get, [Expr.Value n])

                                        else
                                            if n = name then
                                                let get = run v.Type
                                                Expr.Call(Expr.Var traversal, get, [Expr.Var node])
                                            else
                                                let t = getTraversal n v.Type
                                                let get = run v.Type
                                                Expr.Call(t, get, [Expr.Var node])

                                    v, replacement
                                   )
                                |> Map.ofSeq


                        let self = self.Substitute (fun v -> Map.tryFind v replacements)

                        let call =
                            match self with
                                | Lambda(a0, Lambda(a1, body)) ->
                                    let m = Map.ofList [a0, Expr.Var traversal; a1, Expr.Coerce(Expr.Var node, a1.Type)]
                                    body.Substitute (fun vi -> Map.tryFind vi m)
                                | _ ->
                                    Expr.Application(Expr.Application(self, Expr.Var traversal), Expr.Var node)


                        let res = Var("res", retType)
                        Expr.IfThenElse(
                            test,
                            Expr.Let(res, call, Expr.NewUnionCase(optionSome, [Expr.Var res])),
                            build sfs
                        )

            let lambda = 
                Expr.Lambda(traversal,
                    Expr.Lambda(node,
                        build functions
                    )
                )

            kind, retType, lambda

        for (name, sfs) in Dictionary.toSeq functions do
            let rootSfs, sfs = sfs |> List.partition(fun sf -> sf.isRoot)

            // get a set of strict attributes
            let strict = 
                sfs 
                    |> List.filter (fun sf -> not <| Set.contains name sf.strictSyn)    // consider only leaves
                    |> List.map (fun sf -> sf.strictInh)                                // take all strict inh attributes
                    |> Set.intersectMany                                                // take only those needed by all leaf-productions



            let kind, retType, dispatcher = createDispatcher strict name sfs
            let f = QuotationCompiler.ToDynamicAssembly(dispatcher, "Ag").Invoke(null, [||])

            let instance =
                match kind with
                    | Synthesize -> 
                        let t = typedefof<SynthesizeTraversal<_>>.MakeGenericType [|retType|]
                        let ctor = t.GetConstructor [| dispatcher.Type; typeof<TraversalState> |]

                        ctor.Invoke [|f; TraversalState.Root :> obj|]

                    | Inherit -> 
                        let _,_,root = createDispatcher Set.empty name rootSfs
                        let g = QuotationCompiler.ToDynamicAssembly(root, "Ag").Invoke(null, [||])

                        let t = typedefof<InheritTraversal<_>>.MakeGenericType [|retType|]
                        let ctor = t.GetConstructor [| typeof<string>; dispatcher.Type; root.Type; typeof<TraversalState> |]


                        ctor.Invoke [| name :> obj; f; g; TraversalState.Root :> obj|]

            traversals.[indices.[name]] <- instance

            ()

        let tsum = traversals.[indices.["Sum"]] |> unbox<ITraversal<int>>
        let res = tsum.Run(Cons(2, Cons(1, Nil())))
        printfn "%A" res
        ()

//    let generate() =
//        let types = 
//            Introspection.GetAllTypesWithAttribute<SemanticAttribute>()
//                |> Seq.map (fun t -> t.E0)
//                |> HashSet
//
//        let methods =
//            types 
//                |> Seq.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic))
//                |> Seq.filter (fun mi -> mi.GetParameters().Length = 1 && mi.Name <> "Equals" && not mi.IsGenericMethod)
//                |> Seq.groupBy (fun mi -> mi.Name)
//                |> Dictionary.ofSeq
//
//        let functions =
//            methods
//                |> Dictionary.map (fun k meths -> SemanticFunction.ofSeq k (failwith "") meths)
//
//        let convert (t : Type) (e : Expr)  = Expr.Coerce(e, t)
//
//        let invoke (lambda : Expr) (self : Expr) (o : Expr) =
//            match lambda with
//                | Lambda(traversal,Lambda(a, b)) -> Expr.Let(traversal, self, Expr.Let(a, o, b))
//                | _ -> failwithf "[Ag] cannot invoke semantic function %A" lambda
//
//        let createStateTransitions (attributes : list<string>) : TraversalState -> unit =
//            failwith ""
//
//
//        let rec createDispatcher (fallback : Expr<Traversal -> obj -> Option<obj>>) (strict : Set<string>) (sfs : list<SemanticFunction>) =
//            let inh = sfs |> Seq.forall (fun sf -> sf.kind = Inherit)
//            let o = Var("arg", typeof<obj>)
//            let t = Var("t", typeof<Type>)
//            let self = Var("self", typeof<Traversal>)
//            let cases = FSharpType.GetUnionCases(typeof<Option<obj>>)
//            let noneCase = cases |> Seq.find (fun c -> c.Name = "None")
//            let someCase = cases |> Seq.find (fun c -> c.Name = "Some")
//            let withAttribute = typeof<Traversal>.GetMethod "WithAttribute"
//            let inhTraversal = typeof<Traversal>.GetMethod "Inherit"
//            let stateProp = typeof<Traversal>.GetProperty "State"
//            let getTraversal = typeof<Traversal>.GetMethod "GetValue"
//            let setState = typeof<TraversalState>.GetMethod "Set"
//
//            let rec build (es : list<SemanticFunction>) =
//                match es with
//                    | [] -> 
//                        invoke fallback (Expr.Var self) (Expr.Var o)
//
//                    | sf :: rest ->
//                        let good = Expr.TypeTest(Expr.Var o, sf.nodeType)
//                        
//
//                        let inh = 
//                            sf.definition.GetFreeVars() 
//                                |> Seq.map (fun v -> 
//                                    let nn = v.Name.Substring(5)
//                                    if Set.contains nn strict then
//                                        let self = Expr.Var self
//                                        v, <@@ (%%self : Traversal).GetValue(nn) |> Option.get @@> //Expr.Call(Expr.Var self, getTraversal, [Expr.Value nn])
//                                    else
//                                        let t = Var("newTraversal", self.Type)
//                                        v,Expr.Let(t, Expr.Call(Expr.Var self, withAttribute, [ Expr.Value(nn) ]), 
//                                            Expr.Call(Expr.Var t, inhTraversal, [Expr.Var o])
//                                        )
//                                   )
//                                |> Map.ofSeq
//
//                        let def = sf.definition.Substitute(fun v -> Map.tryFind v inh)
//                        let call = invoke def (Expr.Var self) (convert sf.nodeType (Expr.Var o))
//                        let call = Expr.NewUnionCase(someCase, [Expr.Coerce(call, typeof<obj>)])
//
//       
//                        let rest = build rest
//                        Expr.IfThenElse(good, call, rest)
//
//            let dispatch = build sfs
//
//            let cleanSyn (e : Expr) =
//                let dispatchers = 
//                    strict |> Seq.map (fun s -> 
//                        s, functions.[s] |> List.filter (fun sf -> not sf.isRoot) |> createDispatcher <@ fun (t : Traversal) o -> t.GetValue(s) @> strict
//                    )
//                    |> Map.ofSeq
//
//
//                let rec replaceSynCalls (e : Expr) =
//                    match e with
//                                
//                        | Call(Some t, mi, [o]) when mi.Name = "Synthesize" ->
//                                    
//                            let t = Expr.Var self
//                            let ntv = Var("newTraversal", typeof<Traversal>)
//                            let nt = Expr.Var ntv
//                            let newTraversal = <@ (%%t : Traversal).WithState((%%t : Traversal).State.ChildState(%%o : obj)) @>
//                            let values = dispatchers |> Map.map (fun n d -> invoke d nt o) |> Map.toList
//
//                            let rec build (v : list<string * Expr>) =
//                                match v with
//                                    | [] -> 
//                                        <@@ (%%nt : Traversal).Self %%nt %%o |> Option.get @@>
//
//                                    | (name, value) :: rest ->
//                                        Expr.Sequential(
//                                            let s = <@ (%%nt : Traversal).State @>
//                                            Expr.Call(s, setState, [Expr.Value name; value]),
//                                            build rest
//                                        ) 
//
//
//                            Expr.Let(ntv, newTraversal, 
//                                build values
//                            )
//                         
//
//                                    
//
//
//                        | ShapeLambda(v,b) -> Expr.Lambda(v,replaceSynCalls b)
//                        | ShapeVar(v) -> e
//                        | ShapeCombination(o,args) -> RebuildShapeCombination(o, args |> List.map replaceSynCalls)
//
//
//                replaceSynCalls e
//
//            Expr.Lambda(self, 
//                Expr.Lambda(o, 
//                    if inh then dispatch else cleanSyn dispatch
//                )
//            )
//
//
//        for (name, sfs) in Dictionary.toSeq functions do
//            let rootFunctions, sfs = sfs |> List.partition (fun sf -> sf.isRoot)
//            let inh = sfs |> Seq.forall (fun sf -> sf.kind = Inherit)
//            let invoke (lambda : Expr) (self : Expr) (o : Expr) =
//                match lambda with
//                    | Lambda(traversal,Lambda(a, b)) -> Expr.Let(traversal, self, Expr.Let(a, o, b))
//                    | _ -> failwithf "[Ag] cannot invoke semantic function %A" lambda
//
//
//            match sfs with
//                | [] -> ()
//                | _ ->
//                    let fallback =
//                        if inh then <@ fun (traversal : Traversal) (caller : obj) -> traversal.Inherit() |> Some @>
//                        else <@ fun _ _ -> None @>
//
//                    let strict = 
//                        if inh then Set.empty
//                        else sfs |> Seq.map (fun sf -> sf.strictInh) |> Set.unionMany |> Set.remove name
//
//                    let d = createDispatcher fallback strict sfs
//                    let dispatcher = QuotationCompiler.ToDynamicAssembly(d, "Ag").Invoke(null, [||]) |> unbox<Traversal -> obj -> Option<obj>>
//                    Traversal.AddRule(name, dispatcher)
//                
//            match rootFunctions with
//                | [] -> ()
//                | _ ->
//                    let rd = createDispatcher <@ fun _ _ -> None @> Set.empty rootFunctions
//                    let rootDispatcher = QuotationCompiler.ToDynamicAssembly(rd, "Ag").Invoke(null, [||]) |> unbox<Traversal -> obj -> Option<obj>>
//
//                    Traversal.AddRootRule(name, rootDispatcher)
//            
//        
//        let rec long (n : int) =
//            if n = 0 then Nil() :> IList
//            else Cons(n, long (n-1)) :> IList
//
//
////        let test = sum (long 1 :> obj)
////        Log.line "Sum(Cons(1,Nil())) = %A" test
////        Environment.Exit 0
//
//        let len = 1000
//        let iter = 1000
//        let bla = long len
//
//        let sum x = Traversal.Get(x, "Sum").Synthesize(x)
//
//
//        let sw = System.Diagnostics.Stopwatch()
//        
//
//        for i in 1..10 do
//            let results = Array.zeroCreate iter
//
//            sw.Restart()
//            for i in 0..iter-1 do
//                let s = TraversalState(None, null, Map.ofList ["Index", Some (0 :> obj)])
//                results.[i] <- bla.Sum(s)
//            sw.Stop()
//            Log.line "virtual: %A" (sw.MicroTime / (len * iter))
//            results |> Set.ofArray |> Log.line "values: %A"
//
//            let results = Array.zeroCreate iter
//
//            sw.Restart()
//            for i in 0..iter-1 do
//                results.[i] <- sum (bla)
//            sw.Stop()
//            Log.line "ag: %A" (sw.MicroTime / (len * iter))
//            results |> Array.map unbox<int> |> Set.ofArray |> Log.line "values: %A"
//
//
//
//
//        ()
//
//
//
//
