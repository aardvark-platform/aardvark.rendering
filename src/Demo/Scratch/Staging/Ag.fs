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


    module Dispatcher =
        let ofLambdas<'b, 'r> (lambdas : list<obj>) : Dispatcher<'b, 'r> =
            let methods =
                lambdas 
                |> List.map (fun l ->
                    let t = l.GetType()
                    let best = t.GetMethods() |> Array.filter (fun mi -> mi.Name = "Invoke") |> Array.maxBy (fun mi -> mi.GetParameters().Length)
                    let p = best.GetParameters()

                    p.[0].ParameterType, (l, best)
                ) 
                |> Dictionary.ofList

            let dispatcher =
                Dispatcher<'b, 'r>(fun self t ->
                    match methods.TryGetValue t with
                        | (true, t) -> Some t
                        | _ -> None
                )
            dispatcher   

    let mutable lambda = fun (a : float) -> (); a + 1.0

    type IAuto =
        abstract member Driven : int
        abstract member Drive : unit -> int
        

    [<AbstractClass>]
    type Auto() =
        abstract member Driven : int
        abstract member Drive : unit -> int

        interface IAuto with
            member x.Driven = x.Driven
            member x.Drive() = x.Drive()

    [<AbstractClass>]
    type Teuer() =
        abstract member Viel : int

    type Ferrari() =
        inherit Teuer()

        let mutable driven = 0

        override x.Viel = 100

        member x.Driven = driven
        member x.Drive() = 
            driven <- driven + 1
            driven

        interface ICloneable with
            member x.Clone() = obj()

        interface IAuto with
            member x.Driven = x.Driven
            member x.Drive() = x.Drive()


    type Fiat() =
        //inherit Auto()
        inherit Aardvark.Base.Incremental.AdaptiveObject()

        let mutable driven = 0
        member x.Driven = driven
        member x.Drive() = 
            driven <- driven + 1
            driven

        
        interface ICountable with
            member x.LongCount = 1L
        
        interface ICloneable with
            member x.Clone() = obj()


        interface IAuto with
            member x.Driven = x.Driven
            member x.Drive() = x.Drive()

    type Blubber(a : int) =
        member x.Length(l : list<'a>, bla : obj) =
            a

        member x.Length(v : V2i, bla : obj) = 
            a

    [<Demo("Bla 2")>]
    let runner() =
        let b = Blubber(10) :> obj
        let methods = typeof<Blubber>.GetMethods() |> Array.filter (fun mi -> mi.Name = "Length") |> Array.toList |> List.map (fun mi -> b, mi)

        let disp = Dispatcher<obj, int>.Create methods
        disp.Invoke([1;2;3], null) |> printfn "int: %A"
        disp.Invoke([1.0;2.0;3.0], null) |> printfn "double: %A"
        disp.Invoke(V2i.Zero, null) |> printfn "v2i: %A"

        let sw = System.Diagnostics.Stopwatch()
        let iter = 100000000
        let mutable res = 0
        let mutable a = V2i.II :> obj
        sw.Start()
        let mutable ret = 0
        for i in 1..iter do
            disp.TryInvoke(a, null, &ret) |> ignore
            res <- res + ret
        sw.Stop()
        printfn "good: %A %A" (sw.MicroTime / iter) (res / iter)


    [<Demo("Bla")>]
    let run() =

        let lambdas =
            [
                (fun (a : list<int>)        -> 10.0) :> obj
                (fun (a : Option<int>)      -> 10.0) :> obj
                (fun (a : obj)              -> 11.0) :> obj

                (fun (a : int8)             -> 10.0) :> obj
                (fun (a : int16)            -> 10.0) :> obj
                (fun (a : int32)            -> 10.0) :> obj
                (fun (a : int64)            -> 10.0) :> obj
                (fun (a : uint8)            -> 10.0) :> obj
                (fun (a : uint16)           -> 10.0) :> obj
                (fun (a : uint32)           -> 10.0) :> obj
                (fun (a : uint64)           -> 10.0) :> obj
                (fun (a : V2i)              -> 10.0) :> obj
                (fun (a : V2f)              -> 10.0) :> obj
                (fun (a : V2d)              -> 10.0) :> obj
                (fun (a : V3i)              -> 10.0) :> obj
                (fun (a : V3f)              -> 10.0) :> obj
                (fun (a : V3d)              -> 10.0) :> obj
                (fun (a : V4i)              -> 10.0) :> obj
                (fun (a : V4f)              -> 10.0) :> obj
                (fun (a : V4d)              -> 10.0) :> obj

            ]

        let values = 
            [
                obj(); Some 1 :> obj
                8y :> obj; 8s :> obj; 8 :> obj; 8L :> obj; 8uy :> obj; 8us :> obj; 8u :> obj; 8UL :> obj; 
                V2i.Zero :> obj; V2f.Zero :> obj; V2d.Zero :> obj;
                V3i.Zero :> obj; V3f.Zero :> obj; V3d.Zero :> obj;
                V4i.Zero :> obj; V4f.Zero :> obj; V4d.Zero :> obj;
                List.empty<int> :> obj; List<int>() :> obj
            ]

        let meth = Dispatcher<float>.Create lambdas


        for v in values do
            let mutable foo = Unchecked.defaultof<_>
            meth.TryInvoke(v, &foo) |> ignore


        Log.line "table:      %A" meth.TableSize
        Log.line "collisions: %A" meth.Collisions


        let sw = System.Diagnostics.Stopwatch()
        let iter = 100000000
        let mutable res = 0.0
        let mutable a = [1]
        sw.Start()
        let mutable ret = 0.0
        for i in 1..iter do
            meth.TryInvoke(a, &ret) |> ignore
            res <- res + ret
        sw.Stop()

        Log.line "disp([1]):  %A (%A)" (res / float iter) (sw.MicroTime / iter)



        let a = Activator.CreateInstance(typeof<Ferrari>) |> unbox<IAuto>
        let b = Activator.CreateInstance(typeof<Fiat>) |> unbox<IAuto>

        let mutable auto = a
        let mutable res = 0
        sw.Restart()
        for i in 1..iter do
            res <- auto.Drive()
            if i &&& 1 = 0 then auto <- b
            else auto <- a 

        sw.Stop()
        Log.line "virtual:    %A" (sw.MicroTime / iter)

    [<Demo("Compiled")>]
    let run2() =
        
        let disp = 
            Dispatcher<float>.Create(fun t ->
                if t = typeof<list<int>> then (fun (value : list<int>) -> 1.0) :> obj |> Some
                elif t = typeof<obj> then (fun (value : obj) -> 0.0) :> obj |> Some
                else None
            )

//        disp.Invoke(1) |> Log.line "%A"
//        disp.Invoke(2.0f) |> Log.line "%A"
//        disp.Invoke(3.0) |> Log.line "%A"
        disp.Invoke([1]) |> Log.line "%A"
        disp.Invoke(obj()) |> Log.line "%A"


        let sw = System.Diagnostics.Stopwatch()
        let iter = 100000000
        let mutable res = 0.0
        let mutable a = [1]
        sw.Start()
        let mutable ret = 0.0
        for i in 1..iter do
            disp.TryInvoke(a, &ret) |> ignore
        sw.Stop()

        Log.line "disp(1.0f):  %A (%A)" (res / float iter) (sw.MicroTime / iter)


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
                                    let value = Expr.Value(getInstance v.Type, v.Type)
                                    b.Substitute (fun vi -> if v = vi then Some value else None)
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


            definitions
                |> List.map (fun sf -> sf.nodeType, sf)
                |> HashMap.ofList

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
            let f = QuotationCompiler.ToObject(dispatcher, "Ag")

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

                            QuotationCompiler.ToObject(lambda, "Ag") |> unbox<TraversalState -> obj -> unit>




                        ctor.Invoke [|f; TraversalState.Root :> obj; strictFs :> obj|]

                    | Inherit -> 
                        let _,_,root = createDispatcher Set.empty name rootSfs
                        let g = QuotationCompiler.ToObject(root, "Ag")

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



module NewestAg =
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.ExprShape

    type SemanticAttribute() = inherit Attribute()

    type Root<'a> = class end

    [<AutoOpen>]
    module Operators =
        
        type Inh = class end
        let inh : Inh = Unchecked.defaultof<Inh>

        let (?) (o : 'a) (name : string) : 'b =
            failwith ""

        let (<<=) (m : Inh) (value : 'a) =
            ()

        let private isUnitFunction (t : Type) =
            if t.Name.StartsWith "FSharpFunc" then
                let (d,_) = FSharpType.GetFunctionElements t
                d = typeof<unit>
            else
                false

        let (|Synthesize|_|) (e : Expr) =
            match e with
                | Application(Call(None, mi, [o; Value(:? string as name,_)]), Value(:? unit,_)) when mi.Name = "op_Dynamic" ->
                    Some(name, o)
                | _ ->
                    None

        let (|Inherit|_|) (e : Expr) =
            match e with
                | Call(None, mi, [o; Value(:? string as name,_)]) when not (isUnitFunction e.Type) && mi.Name = "op_Dynamic" ->
                    Some(name, o)
                | _ ->
                    None

        let (|AssignInherit|_|) (e : Expr) =
            match e with
                | Call(None, mi, [_;value]) when mi.Name = "op_LessLessEquals" ->
                    Some value
                | _ ->
                    None
    
    [<AllowNullLiteral>]
    type Scope(parent : Option<Scope>, node : obj) =
        static let root = Scope(None, null)
        
        static let getOrCreate (k : 'k) (f : 'k -> 'v) (m : Map<'k, 'v>) =
            match Map.tryFind k m with
                | Some r -> r, m
                | None ->
                    let r = f k
                    r, Map.add k r m

        //let cache : IntDict<obj> = IntDict()
        let mutable cache = Map.empty

        static member Root = root

        member x.ChildScope (child : obj) : Scope =
            if isNull node then Scope(None, child)
            else Scope(Some x, child)

        member x.Parent = parent
        member x.Node = node
        member x.Cache = cache

        member x.Get(i : int) =
            cache.[i]

        member x.Set(i : int, o : obj) =
            cache <- Map.add i o cache
            //cache.[i] <- o

        member x.Inherit(i : int, disp : Dispatcher<Scope, 'r>, root : Dispatcher<'r>) =
            let result, nc = 
                cache |> getOrCreate i (fun i ->
                    match parent with
                        | Some p -> 
                            match disp.TryInvoke(p.Node, p) with
                                | (true, res) -> (res :> obj)
                                | _ -> p.Inherit(i, disp, root) :> obj 
                        | None -> 
                            match root.TryInvoke(node) with
                                | (true, v) -> (v :> obj)
                                | _ -> failwith ""
                )
            cache <- nc
            unbox<'r> result

//            match cache.TryGetValue i with
//                | (true, v) ->
//                    match v with
//                        | Some v -> unbox<'r> v
//                        | None -> failwith ""
//                | _ ->
//                    let res = 
//                        match parent with
//                            | Some p -> 
//                                match disp.TryInvoke(p.Node, p) with
//                                    | (true, res) -> res
//                                    | _ -> p.Inherit(i, disp, root)
//                            | None -> root.Invoke(node)
//                    cache.[i] <- Some (res :> obj)
//                    res

        override x.ToString() =
            match parent with
                | None -> node.GetType().PrettyName
                | Some p -> sprintf "%s/%s" (p.ToString()) (node.GetType().PrettyName)

    type AttributeKind =
        | None          = 0x00
        | Inherited     = 0x01
        | Synthesized   = 0x02
        | Mixed         = 0x03


    type SemanticFunctions =
        {
            index           : int
            name            : string
            kind            : AttributeKind
            valueType       : Type
            implementations : list<Expr>
            originals       : list<MethodInfo>
        }

    

    module Globals =
        open System.Threading
        open System.Collections.Concurrent
        type Marker = Marker

        let scope = Scope.Root

        let mutable private currentIndex = -1
        let private attributeIndices = ConcurrentDictionary<string, int>()

        let mutable synDispatchers : IDispatcher<Scope>[] = null
        let mutable inhDispatchers : IDispatcher<Scope>[] = null
        let mutable rootDispatchers : IDispatcher[] = null

        let semInstances = ConcurrentDictionary<Type, obj>()

        let getInstance (t : Type) =
            semInstances.GetOrAdd(t, fun t -> Activator.CreateInstance t)

        type Instance<'a> private() =
            static let a = getInstance (typeof<'a>) |> unbox<'a>
            static member Instance = a

        let getTypedInstance<'a>() = Instance<'a>.Instance

        let instance<'a> = Instance<'a>.Instance

        let getAttributeIndex (name : string) =
            attributeIndices.GetOrAdd(name, fun _ -> Interlocked.Increment(&currentIndex))

        let attributeCount() = currentIndex + 1


    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SemanticFunctions =
        open Aardvark.Base.Monads.Option

        let private scopeProp = typeof<Globals.Marker>.DeclaringType.GetProperty("scope")
        let private instance = typeof<Globals.Marker>.DeclaringType.GetMethod("getTypedInstance")
        let private inheritMeth = typeof<Scope>.GetMethod "Inherit"

        let (|Scope|_|) (e : Expr) =
            match e with
                | PropertyGet(None, p, []) when p = scopeProp ->
                    Some ()
                | _ ->
                    None


        module private List =
            let rec mapOpt (f : 'a -> Option<'b>) (l : list<'a>) : Option<list<'b>> =
                match l with
                    | [] -> Some []
                    | h :: rest ->
                        match f h with
                            | Some v -> 
                                match mapOpt f rest with
                                    | Some rest -> Some (v :: rest)
                                    | None -> None
                            | None ->
                                None

        let rec private tryGetInheritType (e : Expr) =
            match e with
                | AssignInherit(v) -> Some v.Type
                | ShapeVar(_) -> None
                | ShapeLambda(_,b) -> tryGetInheritType b
                | ShapeCombination(o, args) -> args |> List.tryPick tryGetInheritType
        
        let private kindAndType (mi : MethodInfo) (e : Expr) =
            match tryGetInheritType e with
                | Some t -> AttributeKind.Inherited, t
                | None -> AttributeKind.Synthesized, mi.ReturnType
        
        let ofMethods (name : string) (methods : list<MethodInfo>) =
            option {

                let expressions =
                    methods |> List.choose (fun m ->
                        match Expr.TryGetReflectedDefinition m with
                            | Some e -> Some e
                            | None ->
                                Log.warn "[Ag] could not get reflected definition for semantic function '%s'" m.PrettyName
                                None
                    )

                let kindsAndTypes = List.map2 kindAndType methods expressions

                let! kind = 
                    match kindsAndTypes |> List.fold (fun k (kk,_) -> k ||| kk) AttributeKind.None with
                        | AttributeKind.Mixed | AttributeKind.None -> None
                        | kind -> Some kind

                let! retType =
                    let retTypes = kindsAndTypes |> List.map snd |> HashSet
                    if retTypes.Count = 1 then Some (Seq.head retTypes)
                    else 
                        Log.warn "[Ag] rules for %s have ambigous return types: [%s]" name (retTypes |> Seq.map (fun t -> t.PrettyName) |> String.concat "; ")
                        None

                
                return {
                    index           = Globals.getAttributeIndex name
                    name            = name
                    kind            = kind
                    valueType       = retType
                    implementations = expressions
                    originals       = methods
                }
            }


        let private inlineSemTypes(e : Expr) = 
            match e with
                | Lambda(v,b) when v.Type.IsDefined(typeof<SemanticAttribute>) ->
                    let value = Expr.Value(Globals.getInstance v.Type, v.Type)
                    b.Substitute (fun vi -> if vi = v then Some value else None)
                | _ -> e


        let private force (msg : string) (o : Option<'a>) =
            match o with
                | Some o -> o
                | None -> failwith msg

        let private compileNormal (attName : string) (valueType : Type) (methods : list<MethodInfo>) =

            let td = typedefof<Dispatcher<_,_>>.MakeGenericType [| typeof<Scope>; valueType |]
            let create = td.GetMethod("CreateUntyped", BindingFlags.Static ||| BindingFlags.Public, Type.DefaultBinder, [| typeof<obj -> Type -> Option<obj * MethodInfo>> |], null)
            let dispInvoke = td.GetMethod("Invoke")

            let someScope, noneScope =
                let cases = FSharpType.GetUnionCases(typeof<Option<Scope>>)
                let s = cases |> Seq.find (fun ci -> ci.Name = "Some")
                let n = cases |> Seq.find (fun ci -> ci.Name = "None")
                s,n

            let someObj, noneObj =
                let cases = FSharpType.GetUnionCases(typeof<Option<obj>>)
                let s = cases |> Seq.find (fun ci -> ci.Name = "Some")
                let n = cases |> Seq.find (fun ci -> ci.Name = "None")
                s,n

            let toInvokable (self : obj) (mi : MethodInfo) =
                let e = Expr.TryGetReflectedDefinition(mi) |> force "should not be possible"
                let expression = 
                    match inlineSemTypes e with
                        | Lambda(node, body) ->
                                    
                            let vscope = Var("scope", typeof<Scope>)
                            let scope = Expr.Var vscope
                            let rec repair (e : Expr) =
                                match e with
                                    | Scope ->
                                        scope

                                    | Synthesize(name, node) ->
                                        let node = repair node

                                        if name = attName then
                                            let self = Expr.Value(self, td)
                                            let vo = Var("child", typeof<obj>)
                                            let o = Expr.Var vo
                                            Expr.Let(
                                                vo, Expr.Coerce(node, typeof<obj>), 
                                                Expr.Call(self, dispInvoke, [o; <@@ (%%scope : Scope).ChildScope((%%o : obj)) @@>])
                                            )
                                        else
                                            let td = typedefof<Dispatcher<_,_>>.MakeGenericType [| typeof<Scope>; e.Type |]
                                            let invoke = td.GetMethod("Invoke")

                                            let index = Globals.getAttributeIndex name
                                            let dispatcher = <@@ Globals.synDispatchers.[index] @@>
                                            let vo = Var("child", typeof<obj>)
                                            let o = Expr.Var vo
                            
                                            let dispatcher = Expr.Coerce(dispatcher, td)
                                            Expr.Let(
                                                vo, Expr.Coerce(node, typeof<obj>), 
                                                Expr.Call(dispatcher, invoke, [o; <@@ (%%scope : Scope).ChildScope((%%o : obj)) @@>])
                                            )

                                    | Inherit(name,node) ->
                                        let node = repair node

                                        let inh = inheritMeth.MakeGenericMethod [|e.Type|]
                                        let td =typedefof<Dispatcher<_,_>>.MakeGenericType [| typeof<Scope>; e.Type |]
                                        let tr = typedefof<Dispatcher<_>>.MakeGenericType [| e.Type |]
                                        let index = Globals.getAttributeIndex name
                                        let dispatcher = 
                                            if name = attName then Expr.Value(self, td)
                                            else Expr.Coerce(<@@ Globals.inhDispatchers.[index] @@>, td)
                                        let rootDispatcher = Expr.Coerce(<@@ Globals.rootDispatchers.[index] @@>, tr)
                                        
                                        Expr.Call(scope, inh, [Expr.Value(index); dispatcher; rootDispatcher])


                                    | AssignInherit(value) ->
                                        repair value

                                    | ShapeVar _ -> e
                                    | ShapeLambda(v,b) -> Expr.Lambda(v, repair b)
                                    | ShapeCombination(o, args) -> RebuildShapeCombination(o, args |> List.map repair)

                            Expr.Lambda(node, Expr.Lambda(vscope, repair body))
                        | _ ->
                            failwith "sadasdasdasdasd"

                let lambda = QuotationCompiler.ToObject(expression, "Ag")
                let invoke = lambda.GetType().GetMethod("Invoke", [| mi.GetParameters().[0].ParameterType; typeof<Scope> |])
                if isNull invoke then Log.warn "hate"
                Some (lambda, invoke)
       

            let table = methods |> List.map (fun mi -> null, mi) |> MethodTable.ofList

            let resolve (self : obj) (t : Type) =
                let good = table |> MethodTable.tryResolve [| t |]   
                match good with
                    | Some (_,mi) ->
                        toInvokable self mi
                    | None ->
                        None

            create.Invoke(null, [|resolve :> obj|]) |> unbox<IDispatcher<Scope>>

        let private compileRoot (attName : string) (valueType : Type) (methods : list<MethodInfo>) =
            let td = typedefof<Dispatcher<_>>.MakeGenericType [| valueType |]
            let create = td.GetMethod("Create", BindingFlags.Static ||| BindingFlags.Public, Type.DefaultBinder, [| typeof<list<obj * MethodInfo>> |], null)

            let realMethods =
                methods |> List.mapi (fun i mi ->
                    if mi.IsGenericMethod then
                        failwith "[Ag] root rules must not be generic"

                    let e = Expr.TryGetReflectedDefinition(mi) |> force "not possible"
                    let rec repair (e : Expr) =
                        match e with
                            | Scope -> failwith "[Ag] root rules must not use the current scope"
                            | Synthesize(name, node) -> failwith "[Ag] root rules must not synthesize attributes"
                            | Inherit(name) -> failwith "[Ag] root rules must not inherit attributes"

                            | AssignInherit(value) ->
                                value

                            | ShapeVar _ -> e
                            | ShapeLambda(v,b) -> Expr.Lambda(v, repair b)
                            | ShapeCombination(o, args) -> RebuildShapeCombination(o, args |> List.map repair)

                    match repair (inlineSemTypes e) with
                        | Lambda(v,body) ->
                            let free = body.GetFreeVars() |> Set.ofSeq
                            let t = v.Type.GetGenericArguments().[0]
                            if Set.isEmpty free then 
                                sprintf "f%d" i, [Var(v.Name, t)], body
                            else 
                                failwith "[Ag] root rule does not take an argument"
                        | _ -> 
                            failwith "[Ag] root rule does not take an argument"
                )

            let instance,methods = QuotationCompiler.CreateInstance realMethods

            let arg = methods |> Array.map (fun mi -> instance,mi) |> Array.toList
            create.Invoke(null, [|arg|]) |> unbox<IDispatcher>

        let compile (sf : SemanticFunctions) =
            if sf.kind = AttributeKind.Inherited then 
                let root, other = 
                    sf.originals |> List.partition(fun mi -> 
                        let args = mi.GetParameters()
                        if args.Length = 1 then
                            let t = args.[0].ParameterType
                            t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Root<_>>
                        else
                            false
                    )
                Globals.rootDispatchers.[sf.index] <- compileRoot sf.name sf.valueType root
                Globals.inhDispatchers.[sf.index] <- compileNormal sf.name sf.valueType other
            else 
                Globals.synDispatchers.[sf.index] <- compileNormal sf.name sf.valueType sf.originals

    
    type IList =
        abstract member Sum : Scope -> int
        abstract member Sum : unit -> int


    
    type Nil() = 
        interface IList with
            member x.Sum (s : Scope) =
                s.Get(1) |> unbox<int>
            member x.Sum () = 0

    type Cons(head : int, tail : IList) =
        interface IList with
            member x.Sum s = 
                let child = s.ChildScope tail
                let i = s.Get(1) |> unbox<int>
                child.Set(1, 1 + i)
                x.Head + x.Tail.Sum child

            member x.Sum() = 
                head + tail.Sum()

        member x.Head = head
        member x.Tail = tail



    let private root = Some 1

    let ag = obj()
    open Aardvark.Base.Incremental

    [<Semantic; ReflectedDefinition>]
    type Sems() =
        member x.Sum(n : Nil) : int = n?Index

        member x.Sum(c : Cons) : int = c.Head + c.Tail?Sum()

        member x.Index(r : Root<IList>) =
            inh <<= 0

        member x.Index(c : Cons) =
            inh <<= 1 + c?Index


//        member x.Set(n : Nil) = 
//            ASet.single 1
//
//        member x.Set(c : Cons) = 
//            aset {
//                yield c.Head
//                yield! c.Tail?Set()
//            }


    [<Demo("aaaag")>]
    let run() =
        let functions = 
            Introspection.GetAllTypesWithAttribute<SemanticAttribute>()
                |> Seq.map (fun t -> t.E0)
                |> Seq.collect (fun t -> 
                    let all = t.GetMethods(BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                    all |> Seq.filter (fun mi -> mi.DeclaringType = t && not (mi.Name.StartsWith "get_" || mi.Name.StartsWith "set_"))
                   )
                |> Seq.groupBy (fun mi -> mi.Name)
                |> Seq.map (fun (name, mis) -> name, Seq.toList mis)
                |> Seq.choose (fun (name, mis) -> SemanticFunctions.ofMethods name mis)
                |> Seq.toArray



        let cnt = Globals.attributeCount()
        Globals.synDispatchers <- Array.zeroCreate cnt
        Globals.inhDispatchers <- Array.zeroCreate cnt
        Globals.rootDispatchers <- Array.zeroCreate cnt

        for f in functions do
            SemanticFunctions.compile f


        let disp = Globals.synDispatchers.[Globals.getAttributeIndex "Sum"] |> unbox<Dispatcher<Scope, int>>

        let list = Cons(1, Cons(2, Nil()))
        let sum n = 
            disp.Invoke(n, Scope.Root.ChildScope n)
        Log.line "sum = %A" (sum list)



        let rec long (n : int) =
            if n = 0 then Nil() :> IList
            else Cons(n, long (n-1)) :> IList

        let test = long 1000
        let testList = List.init 2000 (fun i -> 2000 - i)

        for i in 1..10 do
            sum test |> ignore


        let iter = 100000
        let sw = System.Diagnostics.Stopwatch()
        sw.Start()
        for i in 1 .. iter do
            List.sum testList |> ignore
        sw.Stop()
        Log.line "list: %A" (sw.MicroTime / iter)

        let sw = System.Diagnostics.Stopwatch()
        sw.Start()
        for i in 1 .. iter do
            let s = Scope.Root.ChildScope test
            s.Set(1, 0)
            test.Sum s |> ignore
        sw.Stop()
        Log.line "virt: %A" (sw.MicroTime / iter)

        let sw = System.Diagnostics.Stopwatch()
        sw.Start()
        for i in 1 .. iter do
            sum test |> ignore
        sw.Stop()
        Log.line "ag:   %A" (sw.MicroTime / iter)

        let sw = System.Diagnostics.Stopwatch()
        let set = [0..9999] |> HashSet
        sw.Start()
        for i in 1 .. iter do
            for i in 1 .. 1000 do
                set.Contains i |> ignore
        sw.Stop()
        Log.line "hash: %A" (sw.MicroTime / iter)




        ()