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


module Blub =
 
    type TraversalState(parent : Option<TraversalState>, node : obj, cache : Dictionary<string, Option<obj>>) =
        static let root = TraversalState(None, null, Dictionary.empty)
        static member Root = root

        member x.Parent = parent
        member x.Node = node

        member x.ChildState(n : obj) =
            if isNull node then TraversalState(None, n, Dictionary.empty)
            else TraversalState(Some x, n, Dictionary.empty)


        member x.Item
            with get (name : string) = cache.[name]
            and set (name : string) (value : Option<obj>) = cache.[name] <- value

        member x.TryGet(name : string) =
            match cache.TryGetValue name with
                | (true, v) -> v
                | _ -> None

        member x.GetOrCreate(name : string, f : string -> Option<'a>) =
            match cache.TryGetValue name with
                | (true, v) ->  v |> Option.map unbox
                | _ ->
                    let v = f name
                    cache.[name] <- v |> Option.map (fun v -> v :> obj)
                    v
    

    [<StructuredFormatDisplay("{AsString}")>]
    type TypeMap<'a>(store : list<Type * 'a>, count : int) =

        let rec tryAdd (found : ref<bool>) (t : Type) (value : 'a) (l : list<Type * 'a>) =
            match l with
                | [] -> [(t, value)]
                | (tc,vc) :: rest ->

                    let ta = t.IsInterface || t = typeof<obj>
                    let tca = tc.IsInterface || tc = typeof<obj>

                    if not ta && tca then 
                        (t,value) :: (tc, vc) :: rest
                    elif not tca && tca then
                        (tc,vc) :: (tryAdd found t value rest)
                    else
                        if tc = t then 
                            found := true
                            (tc,value) :: rest
                        elif tc.IsAssignableFrom t then
                            (t,value) :: (tc, vc) :: rest
                        
                        elif t.IsAssignableFrom tc then
                            (tc,vc) :: (tryAdd found t value rest)

                        else
                            (tc,vc) :: (tryAdd found t value rest)

        member x.Add(t : Type, value : 'a) =
            if count = 0 then
                TypeMap<'a>([t,value], 1)
            else
                
                let found = ref false
                let l = tryAdd found t value store
                if !found then x
                else TypeMap<'a>(l, count + 1)
             
        member internal x.List = store
        member private x.Store = store
        member x.Count = count

        member private x.AsString =
            store |> List.map (fun (t,v) -> sprintf "(%s, %A)" (Aardvark.Base.ReflectionHelpers.getPrettyName t) v) |> String.concat "; " |> sprintf "[%s]"
                
        override x.ToString() = x.AsString

        override x.GetHashCode() = store |> Seq.fold (fun h (t,v) -> HashCode.Combine(h, t.GetHashCode() ^^^ (v :> obj).GetHashCode())) 0

        override x.Equals(o) =
            match o with
                | :? TypeMap<'a> as o -> 
                    if count = o.Count then
                        Seq.forall2 (fun (tl,vl) (tr, vr) -> tl = tr && Object.Equals(vl, vr)) store o.Store
                    else
                        false
                | _ ->
                    false

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = (store :> seq<_>).GetEnumerator() :> System.Collections.IEnumerator

        interface IEnumerable<Type * 'a> with
            member x.GetEnumerator() = (store :> seq<_>).GetEnumerator() :> IEnumerator<_>

    module TypeMap =
        type private EmptyImpl<'a>() =
            static let instance = TypeMap<'a>([], 0)
            static member Instance = instance

        let empty<'a> = EmptyImpl<'a>.Instance

        let add (t : Type) (value : 'a) (m : TypeMap<'a>) = m.Add(t,value)
        let count (m : TypeMap<'a>) = m.Count
        let toSeq (m : TypeMap<'a>) = m.List :> seq<_>
        let toList (m : TypeMap<'a>) = m.List 
        let toArray (m : TypeMap<'a>) = m.List |> List.toArray

        let ofSeq (l : seq<Type * 'a>) =
            let mutable res = empty
            for (t,v) in l do
                res <- add t v res
            res

        let ofList (l : list<Type * 'a>) =
            let mutable res = empty
            for (t,v) in l do
                res <- add t v res
            res

        let ofArray (l : array<Type * 'a>) =
            ofSeq l

        let map (f : Type -> 'a -> 'b) (m : TypeMap<'a>) =
            TypeMap<'b>(m.List |> List.map (fun (t,v) -> t, f t v), m.Count)

        let choose (f : Type -> 'a -> Option<'b>) (m : TypeMap<'a>) =
            let mutable len = 0
            let store = 
                m.List |> List.choose (fun (t,v) ->
                    match f t v with
                        | Some r -> 
                            len <- len + 1
                            Some (t,r)
                        | None ->
                            None
                )
            TypeMap<'b>(store, len)

        let filter (f : Type -> 'a -> bool) (m : TypeMap<'a>) =
            let mutable len = 0
            let store = 
                m.List |> List.filter (fun (t,v) ->
                    if f t v then
                        len <- len + 1
                        true
                    else
                        false
                )
            TypeMap<'a>(store, len)


    [<Demo("Bla")>]
    let run() =
        let m = TypeMap.ofList [typeof<Aardvark.SceneGraph.ISg>, 1; typeof<Aardvark.SceneGraph.IApplicator>, 3; typeof<Aardvark.SceneGraph.IGroup>, 2; typeof<Aardvark.SceneGraph.Sg.Group>, 5; typeof<Aardvark.SceneGraph.Sg.TrafoApplicator>, 1; typeof<Aardvark.SceneGraph.Sg.AbstractApplicator>, 1]
        printfn "%A" m

    type Dispatcher<'a> = { cases : HashMap<Type, 'a> }

    module Dispatcher = 
        let tryResolve (argType : Type) (d : Dispatcher<'a>) =
            match HashMap.tryFind argType d.cases with
                | Some v -> Some (argType, v)
                | None ->
                    let other = d.cases |> HashMap.toSeq |> Seq.filter (fun (t,v) -> t.IsAssignableFrom argType) |> Seq.toList
                    match other with
                        | [] -> None
                        | h :: _ -> Some h

        let merge (merge : 'a -> 'a -> 'a) (l : Dispatcher<'a>) (r : Dispatcher<'a>) =
        
            let lt = l.cases |> HashMap.toSeq |> Seq.map fst |> PersistentHashSet.ofSeq
            let rt = r.cases |> HashMap.toSeq |> Seq.map fst |> PersistentHashSet.ofSeq

            let types = PersistentHashSet.union lt rt

            let cases = 
                types 
                    |> Seq.map (fun t ->
                        match tryResolve t l, tryResolve t r with
                            | Some (lt,lv), Some (rt, rv) -> 
                                if lt.IsAssignableFrom rt then
                                    lt, merge lv rv
                                elif rt.IsAssignableFrom lt then
                                    rt, merge lv rv
                                else
                                    failwith "unexpected"
                            | Some r, None | None, Some r ->
                                r
                            | _ ->
                                failwith "unexpected"
                    )
                    |> HashMap.ofSeq
            { cases = cases }



    type Root<'a> = class end
    
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


        let ofSeq (name : string) (methods : seq<MethodInfo>) =
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
                let convert (t : Type) (e : Expr) = Expr.Coerce(e, t)

                let rec getInfo (strictInh : HashSet<string>) (strictSyn : HashSet<string>) (attType : ref<Type>) (kind : ref<SemanticFunctionKind>) (e : Expr) =
                    match e with
                        | Application(Call(None, mi, [o; Value(:? string as nn, _)]), Value(_)) when mi.Name = "op_Dynamic" ->
                            // syn case
                            kind := Synthesize
                            strictSyn.Add nn |> ignore
                   

                        | Call(None, mi, [o; Value(:? string as nn, _)]) when mi.Name = "op_Dynamic" ->
                            strictInh.Add nn |> ignore

                        | Call(None, mi, [_; value]) when mi.Name = "op_LessLessEquals" ->
                            attType := value.Type


                        | ShapeVar(v) ->
                            ()

                        | ShapeLambda(v,b) ->
                            getInfo strictInh strictSyn attType kind b

                        | ShapeCombination(o, args) ->
                            args |> List.iter (getInfo strictInh strictSyn attType kind)


                definitions |> List.choose (fun d ->
                    match d.definition with
                        | Lambda(nodeVar,body) ->
                            let kind = ref Synthesize
                            let strictInh = HashSet()
                            let strictSyn = HashSet()
                            let attType = ref body.Type
                            getInfo strictInh strictSyn attType kind body
                            strictInh.Remove d.name |> ignore
              
                            let (|RootVar|_|) (v : Var) = 
                                let t = v.Type
                                if t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Root<_>> then
                                    let t = t.GetGenericArguments().[0]
                                    Some t
                                else
                                    None

                            let isRoot, nodeVar =
                                match nodeVar with
                                    | RootVar t ->
                                        true, Var("caller", t)
                                    | _ -> 
                                        false, nodeVar
                      

                            let res = 
                                { d with 
                                    definition = Expr.Lambda(nodeVar, body) 
                                    kind = !kind
                                    strictInh = Set.ofSeq strictInh
                                    strictSyn = Set.ofSeq strictSyn
                                    isRoot = isRoot
                                    nodeType = nodeVar.Type
                                    valueType = !attType
                                }
                            Some res

                        | _ ->
                            None
                    
                )


            { cases = 
                definitions
                    |> List.map (fun sf -> sf.nodeType, sf)
                    |> HashMap.ofList
            }



module Test =
 
    type TraversalState(parent : Option<TraversalState>, node : obj, cache : Dictionary<string, Option<obj>>) =
        static let root = TraversalState(None, null, Dictionary.empty)
        static member Root = root

        member x.Parent = parent
        member x.Node = node

        member x.ChildState(n : obj) =
            if isNull node then TraversalState(None, n, Dictionary.empty)
            else TraversalState(Some x, n, Dictionary.empty)


        member x.TryGet(name : string) =
            match cache.TryGetValue name with
                | (true, v) -> v
                | _ -> None

        member x.Set(name : string, value : Option<obj>) =
            cache.[name] <- value

        member x.GetOrCreate(name : string, f : string -> Option<'a>) =
            match cache.TryGetValue name with
                | (true, v) ->  v |> Option.map unbox
                | _ ->
                    let v = f name
                    cache.[name] <- v |> Option.map (fun v -> v :> obj)
                    v


    type Root<'a> = class end
    type Inh = class end
    let inh = Unchecked.defaultof<Inh> 

    let (<<=) (i : Inh) (value : 'a) =
        ()


    type ITraversal<'a> =
        abstract member RunUnit : obj -> unit
        abstract member Run : obj -> 'a
        abstract member WithState : TraversalState -> ITraversal<'a>
        abstract member State : TraversalState


    type SynthesizeTraversal<'a>(syn : SynthesizeTraversal<'a> -> obj -> Option<'a>, state : TraversalState, strict : TraversalState -> obj -> unit) =

        member x.State : TraversalState = 
            state

        member x.WithState (state : TraversalState) =
            SynthesizeTraversal<'a>(syn, state, strict)

        member x.Run(o : obj) =
            let childState = state.ChildState o
            strict childState o
            syn (x.WithState childState) o |> Option.get

        member x.RunUnit(o : obj) : unit =
            failwith "not supported"

        member x.GetValue<'a>(name : string) =
            match state.TryGet(name) with
                | Some (:? 'a as v) -> v
                | _ -> failwith ""

        interface ITraversal<'a> with
            member x.Run(o) = x.Run(o)
            member x.RunUnit(o) = x.RunUnit(o)
            member x.WithState s = x.WithState s :> ITraversal<_>
            member x.State = x.State

    type InheritTraversal<'a>(name : string, inh : InheritTraversal<'a> -> obj -> Option<'a>, root : InheritTraversal<'a> -> obj -> Option<'a>, state : TraversalState) =

        member private x.create o name =
            match state.Parent with
                | Some p -> inh (x.WithState p) p.Node
                | None -> root x o

        member x.State : TraversalState = 
            state

        member x.WithState (state : TraversalState) =
            InheritTraversal<'a>(name, inh, root, state)

        member x.Run(o : obj) =
            state.GetOrCreate(name, x.create o) |> Option.get
 
        member x.RunUnit(o : obj) =
            state.GetOrCreate(name, x.create o) |> ignore

        member x.GetValue<'a>(name : string) =
            match state.TryGet(name) with
                | Some (:? 'a as v) -> v
                | _ -> failwith ""

        interface ITraversal<'a> with
            member x.Run(o) = x.Run(o)
            member x.RunUnit(o) = x.RunUnit(o)
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

    let state : TraversalState = TraversalState.Root

    [<Semantic; ReflectedDefinition>]
    type Sems() =


        member x.Sum(n : Nil) : int =
            n?Index - n?Blubber + n?Bla - n?Gabbl + n?Gobbl

        member x.Sum(c : Cons) =
            c.Head + c.Tail?Sum()

        member x.Index(l : Root<IList>) = 
            inh <<= 0

        member x.Index(c : Cons) = 
            let id = c?Index
            inh <<= id + 1
    

        member x.Blubber(l : Root<IList>) = 
            inh <<= 0

        member x.Blubber(c : Cons) = 
            let id = c?Blubber
            inh <<= id + 1   

        member x.Bla(l : Root<IList>) = 
            inh <<= 0

        member x.Bla(c : Cons) = 
            let id = c?Bla
            inh <<= id + 1   

        member x.Gabbl(l : Root<IList>) = 
            inh <<= 0

        member x.Gabbl(c : Cons) = 
            let id = c?Gabbl
            inh <<= id + 1   


        member x.Gobbl(l : Root<IList>) = 
            inh <<= 0

        member x.Gobbl(c : Cons) = 
            let id = c?Gobbl
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


            let definitions =
                definitions |> List.map (fun sf ->
                    let rec retType (e : Expr) =
                        match e with
                            | Call(None, mi, [_; value]) when mi.Name = "op_LessLessEquals" ->
                                value.Type


                            | Value(v,t) -> t


                            | Var(v) ->
                                v.Type

                            | Lambda(v,b) ->
                                retType b

                            | Sequential(_,r) ->
                                retType r

                            | Let(_,_,b) ->
                                retType b

                            | Coerce(_,t) ->
                                t

                            | IfThenElse(_,i,_) -> retType i

                            | WhileLoop(_,_) -> typeof<unit>

                            | ForIntegerRangeLoop(_,_,_,_) -> typeof<unit>

                            | Call(_,mi,_) -> mi.ReturnType

                            | Application(b, arg) ->
                                retType b

                            | e -> 
                                failwithf "unknown expression %A" e
                
                    { sf with valueType = retType sf.definition }

                )
            
            
            // replace all attribute-lookups with traversal-calls and add the traversal argument
            // to all semantic functions
            let definitions = 
                let run t = typedefof<ITraversal<_>>.MakeGenericType([|t|]).GetMethod("Run")
                let withState (self : Expr) (t : Expr) =
                    Expr.Call(t, t.Type.GetMethod "WithState", [Expr.PropertyGet(self, self.Type.GetProperty "State")])

                let convert (t : Type) (e : Expr) = Expr.Coerce(e, t)
                
                let stateProp = 
                    match <@ state @> with
                        | PropertyGet(None, pi, []) -> pi
                        | _ -> failwith ""
             

                let rec substituteAttributeLookups (traversal : Var) (strictInh : HashSet<string>) (strictSyn : HashSet<string>) (kind : ref<SemanticFunctionKind>) (e : Expr) =
                    match e with

                        | PropertyGet(None, pi, []) when pi = stateProp ->
                            Expr.PropertyGet(Expr.Var traversal, traversal.Type.GetProperty "State")

                        | Application(Call(None, mi, [o; Value(:? string as nn, _)]), Value(_)) when mi.Name = "op_Dynamic" ->
                            // syn case
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
                            substituteAttributeLookups traversal strictInh strictSyn kind value


                        | ShapeVar(v) ->
                            e

                        | ShapeLambda(v,b) ->
                            Expr.Lambda(v, substituteAttributeLookups traversal strictInh strictSyn kind b)

                        | ShapeCombination(o, args) ->
                            RebuildShapeCombination(o, args |> List.map (substituteAttributeLookups traversal strictInh strictSyn kind))


                definitions |> List.map (fun d ->
                    let mutable kind = ref Synthesize
                    let strictInh = HashSet()
                    let strictSyn = HashSet()
                    let traversal = Var("traversal", typedefof<ITraversal<_>>.MakeGenericType [|d.valueType|])

                    let def = substituteAttributeLookups traversal strictInh strictSyn kind d.definition
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

    let mutable traversals : obj[] = null

    type Helper =
        static member Lazy (f : unit -> 'a) = Lazy<'a>(f)

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


            let runInherit (t : Type) (n : string) =
                if n = name then
                    Expr.Call(Expr.Var traversal, run t, [Expr.Var node])
                else
                    let t = getTraversal n t
                    let t = Expr.Call(t, t.Type.GetMethod "WithState", [Expr.PropertyGet(Expr.Var traversal, traversal.Type.GetProperty "State")])

                    Expr.Call(t, t.Type.GetMethod "Run", [Expr.Var node])

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
                                            runInherit v.Type n

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



        let functionTypes =
            functions |> Dictionary.map (fun name sfs ->
                let types = sfs |> List.map (fun s -> s.valueType) |> HashSet
                if types.Count = 1 then types |> Seq.head
                else failwith "sadsadsdsad"
            )

        for (name, sfs) in Dictionary.toSeq functions do
            let rootSfs, sfs = sfs |> List.partition(fun sf -> sf.isRoot)

            // get a set of strict attributes
            let strict = 
                sfs 
                    |> List.filter (fun sf -> not <| Set.contains name sf.strictSyn)    // consider only leaves
                    |> List.map (fun sf -> sf.strictInh)                                // take all strict inh attributes
                    |> Set.intersectMany                                                // take only those needed by all leaf-productions

            //let strict = Set.empty


            let kind, retType, dispatcher = createDispatcher strict name sfs
            let f = QuotationCompiler.ToDynamicAssembly(dispatcher, "Ag").Invoke(null, [||])

            let instance =
                match kind with
                    | Synthesize -> 
                        let t = typedefof<SynthesizeTraversal<_>>.MakeGenericType [|retType|]
                        let ctor = t.GetConstructor [| dispatcher.Type; typeof<TraversalState>; typeof<TraversalState -> obj -> unit> |]

                        let strictFs = 
                            let state = Var("state", typeof<TraversalState>)
                            let node = Var("node", typeof<obj>)

                            let traversals =
                                strict |> Set.toList |> List.mapi (fun i n ->
                                    let tt = getTraversal n functionTypes.[n]
                                    let l = typedefof<Lazy<_>>.MakeGenericType [| tt.Type |]
                                    Var(sprintf "t%d" i, l, true), tt
                                )


                            let body =
                                traversals
                                    |> List.map (fun (v,_) ->
                                        let prop = v.Type.GetProperty "Value"
                                        let tt = Expr.PropertyGet(Expr.Var v, prop)
                                        let tt = Expr.Call(tt, tt.Type.GetMethod "WithState", [Expr.Var state])
                                        Expr.Call(tt, tt.Type.GetMethod "RunUnit", [Expr.Var node])
                                    )
                            
                            let rec all (e : list<Expr>) =
                                match e with
                                    | [] -> Expr.Value(())
                                    | [e] -> e
                                    | e :: rest ->
                                        Expr.Sequential(e, all rest)


                            let rec lets (v : list<Var * Expr>) (b : Expr) =
                                match v with
                                    | [] -> b
                                    | (v,e) :: rest -> 
                                        let create = typeof<Helper>.GetMethod("Lazy").MakeGenericMethod [|e.Type|]
                                        Expr.Let(v, Expr.Call(create, [Expr.Lambda(Var("unitVar", typeof<unit>), e)]), lets rest b)


                            let lambda = 
                                Expr.Lambda(state, 
                                    Expr.Lambda(node, 
                                        all body
                                    )
                                )

                            let lambda = lets traversals lambda

                            QuotationCompiler.ToDynamicAssembly(lambda, "Ag").Invoke(null, [||]) |> unbox<TraversalState -> obj -> unit>




                        ctor.Invoke [|f; TraversalState.Root :> obj; strictFs :> obj|]

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


        let rec long (n : int) =
            if n = 0 then Nil() :> IList
            else Cons(n, long (n-1)) :> IList


        let len = 1000
        let iter = 1000
        let bla = long len

        let sum x = tsum.Run(x)
        let sw = System.Diagnostics.Stopwatch()
        

        for i in 1..10 do
            let results = Array.zeroCreate iter

            sw.Restart()
            for i in 0..iter-1 do
                let s = TraversalState(None, null, Dictionary.ofList ["Index", Some (0 :> obj)])
                results.[i] <- bla.Sum(s)
            sw.Stop()
            Log.line "virtual: %A" (sw.MicroTime / (iter))
            results |> Set.ofArray |> Log.line "values: %A"

            let results = Array.zeroCreate iter

            sw.Restart()
            for i in 0..iter-1 do
                results.[i] <- sum (bla)
            sw.Stop()
            Log.line "ag: %A" (sw.MicroTime / (iter))
            results |> Array.map unbox<int> |> Set.ofArray |> Log.line "values: %A"




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
