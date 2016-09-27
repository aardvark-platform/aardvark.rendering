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
open QuotationCompiler.Simple
open Aardvark.Base.Incremental


module NewestAg =
    open Microsoft.FSharp.Quotations
    open Microsoft.FSharp.Quotations.Patterns
    open Microsoft.FSharp.Quotations.ExprShape
    open System.Runtime.CompilerServices

    type AttributeKind =
        | None          = 0x00
        | Inherited     = 0x01
        | Synthesized   = 0x02
        | Mixed         = 0x03

        
    [<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
    type SemanticAttribute() = inherit Attribute()

    [<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Method, AllowMultiple = false); AllowNullLiteral>]
    type AttributeAttribute(name : string, kind : AttributeKind) =
        inherit Attribute()

        member x.Name = name
        member x.Kind = kind

        new(kind : AttributeKind) = AttributeAttribute(null, kind)

    type Root<'a> = class end

    [<AutoOpen>]
    module Operators =
        
        type Inh = class end
        let inh : Inh = Unchecked.defaultof<Inh>

        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let (?) (o : 'a) (name : string) : 'b =
            failwith ""
            
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let (<<=) (m : Inh) (value : 'a) =
            ()

        let private isUnitFunction (t : Type) =
            if t.Name.StartsWith "FSharpFunc" then
                let (d,_) = FSharpType.GetFunctionElements t
                d = typeof<unit>
            else
                false

        [<AutoOpen>]
        module LookupExtensions = 
            open Aardvark.Base.IL

            let private attributeLookupCache = System.Collections.Concurrent.ConcurrentDictionary<MethodInfo, Option<string * AttributeKind>>()
            let private rx = System.Text.RegularExpressions.Regex @"(get_)?(?<name>[a-zA-Z_0-9]+$)"
            let (|AttributeLookup|_|) (mi : MethodInfo) =
                attributeLookupCache.GetOrAdd (mi, fun mi ->
                    let att = mi.GetCustomAttribute<AttributeAttribute>()
                    if isNull att then
                        None
                    else
                        let name =
                            if isNull att.Name then 
                                let m = rx.Match mi.Name 
                                if m.Success then m.Groups.["name"].Value
                                else failwith "bad att name"
                            else 
                                att.Name
                        Some(name, att.Kind)
                )


        let (|Synthesize|_|) (e : Expr) =
            match e with
                | Application(Call(None, mi, [o; Value(:? string as name,_)]), Value(:? unit,_)) when mi.Name = "op_Dynamic" ->
                    Some(name, o)
                
                | Call(None, AttributeLookup(name, AttributeKind.Synthesized), o :: _) ->
                    Some(name, o)     
                              
                | Call(Some o, AttributeLookup(name, AttributeKind.Synthesized), []) ->
                    Some(name, o) 
                                     
                | _ ->
                    None

        let (|Inherit|_|) (e : Expr) =
            match e with
                | Call(None, mi, [o; Value(:? string as name,_)]) when not (isUnitFunction e.Type) && mi.Name = "op_Dynamic" ->
                    Some(name, o)

                | Call(None, AttributeLookup(name, AttributeKind.Inherited), [o]) ->
                    Some(name, o)

                | Call(Some o, AttributeLookup(name, AttributeKind.Inherited), []) ->
                    Some(name, o)

                | _ ->
                    None

        let (|AssignInherit|_|) (e : Expr) =
            match e with
                | Call(None, mi, [_;value]) when mi.Name = "op_LessLessEquals" ->
                    Some value
                | _ ->
                    None

    module Root =
        let mutable dispatchers : IObjectDispatcher[] = null

    [<AllowNullLiteral>]
    type Scope(parent : Scope, node : obj) =
        static let root = Scope(null, null)
        let mutable cache = Map.empty

        static member Root = root

        member x.ChildScope (child : obj) : Scope =
            if isNull node then Scope(null, child)
            else Scope(x, child)

        member x.Parent = parent
        member x.Node = node
        member x.Cache = cache

        abstract member TryGet : int -> Option<obj>

        default x.TryGet(i : int) =
            if isNull parent then
                match Map.tryFind i cache with
                    | Some a -> Some a
                    | _ -> 
                        match Root.dispatchers.[i].TryInvoke(node) with
                            | (true, res) ->
                                cache <- Map.add i res cache
                                Some res
                            | _ ->
                                None
            else
                Map.tryFind i cache

        member x.Set(i : int, value : obj) =
            cache <- Map.add i value cache

        member x.Get(i : int) =
            match x.TryGet i with
                | Some v -> v
                | None -> failwith ""

        member x.Inherit(i : int, disp : Dispatcher<Scope, 'r>, root : Dispatcher<'r>) =
            match x.TryGet i with
                | Some r -> unbox<'r> r
                | None ->
                    let res = 
                        match parent with
                            | null -> 
                                match root.TryInvoke(node) with
                                    | (true, v) -> v
                                    | _ -> failwith ""

                            | p -> 
                                match disp.TryInvoke(p.Node, p) with
                                    | (true, res) -> res
                                    | _ -> p.Inherit(i, disp, root)
                    cache <- Map.add i (res :> obj) cache
                    res

        override x.ToString() =
            match parent with
                | null -> node.GetType().PrettyName
                | p -> sprintf "%s/%s" (p.ToString()) (node.GetType().PrettyName)

    [<AutoOpen>]
    module GenericStuff = 
        [<AllowNullLiteral>]
        type Scope<'a>(i0 : int, parent : Scope<'a>, node : obj) =
            inherit Scope(parent, node)

            [<DefaultValue>]
            val mutable public F0 : 'a

            static member Root (i0 : int, n : obj) =
                let s = Scope<'a>(i0, null, n)
                let (_,v) = Root.dispatchers.[i0].TryInvoke(n)
                s.F0 <- unbox v
                s

            member x.ChildScope(child : obj) = Scope<'a>(i0, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                else base.TryGet(i)

        [<AllowNullLiteral>]
        type Scope<'a, 'b>(i0 : int, i1 : int, parent : Scope<'a, 'b>, node : obj) =
            inherit Scope(parent, node)

            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b

            static member Root (i0 : int, i1 : int, n : obj) =
                let s = Scope<'a, 'b>(i0, i1, null, n)
                let (_,f0) = Root.dispatchers.[i0].TryInvoke(n)
                let (_,f1) = Root.dispatchers.[i1].TryInvoke(n)
                s.F0 <- unbox f0
                s.F1 <- unbox f1
                s

            member x.ChildScope(child : obj) = Scope<'a, 'b>(i0, i1, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                else base.TryGet(i)

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Scope =
            let getType (strict : list<Type>) =
                match strict with
                    | [] -> typeof<Scope>
                    | [t0] -> typedefof<Scope<_>>.MakeGenericType [| t0 |]
                    | [t0; t1] -> typedefof<Scope<_,_>>.MakeGenericType [| t0; t1 |]
                    | _ -> failwith "kjasndajs"


        type WrappedDispatcher<'r>(resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope, 'r>(resolve)

        type WrappedDispatcher<'a, 'r>(i0 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a>, 'r>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'r>) =
                if isNull s.Parent then 
                    let ns = Scope<'a>(i0, null, n)
                    ns.F0 <- unbox (s.Get(i0))
                    x.TryInvoke(n, ns, &res)
                else
                    x.TryInvoke(n, unbox s, &res)

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'r>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'r> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'r>) = x.TryInvoke(n, s, &res)

        type WrappedDispatcher<'a, 'b, 'r>(i0 : int, i1 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b>, 'r>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'r>) =
                if isNull s.Parent then 
                    let ns = Scope<'a, 'b>(i0, i1, null, n)
                    ns.F0 <- unbox (s.Get(i0))
                    ns.F1 <- unbox (s.Get(i1))
                    x.TryInvoke(n, ns, &res)
                else
                    x.TryInvoke(n, unbox s, &res)

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'r>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'r> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'r>) = x.TryInvoke(n, s, &res)

        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module WrappedDispatcher =
            let getType (ret : Type) (strict : list<Type>) =
                match strict with
                    | [] -> typedefof<WrappedDispatcher<_>>.MakeGenericType [| ret |]
                    | [t0] -> typedefof<WrappedDispatcher<_,_>>.MakeGenericType [| t0; ret |]
                    | [t0; t1] -> typedefof<WrappedDispatcher<_,_,_>>.MakeGenericType [| t0; t1; ret |]
                    | _ -> failwith "kjasndajs"


    type SemanticFunction =
        {
            name        : string
            kind        : AttributeKind
            original    : MethodInfo
            nodeType    : Type
            code        : Expr
            isRoot      : bool
            inherits    : Set<string>
            synthesizes : Set<string>
        }

    type SemanticFunctions =
        {
            index           : int
            name            : string
            kind            : AttributeKind
            valueType       : Type
            functions       : list<SemanticFunction>
        }

    module Globals =
        open System.Threading
        open System.Collections.Concurrent
        type Marker = Marker

        let scope = Scope.Root

        let mutable private currentIndex = -1
        let private attributeIndices = ConcurrentDictionary<string, int>()

        let mutable synDispatchers : IObjectDispatcher<Scope>[] = null
        let mutable inhDispatchers : IObjectDispatcher<Scope>[] = null

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

        let mutable semanticFunctions = Map.empty

        let synFunction<'a> (name : string) : obj -> 'a =
            let d = synDispatchers.[attributeIndices.[name]] |> unbox<IDispatcher<Scope, 'a>>
            let f = fun (n : obj) -> d.Invoke(n, Scope(null, n))
            f

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SemanticFunctions =
        open Aardvark.Base.Monads.Option

        type Type with
            member x.MaybeAssignableFromSubtype(other : Type) =
                if x.IsAssignableFrom other then true
                elif other.IsAssignableFrom x then true
                else other.IsInterface

        let private assign = Aardvark.Base.QuotationReflectionHelpers.getMethodInfo <@@ (<<=) @@>
        let private dyn = Aardvark.Base.QuotationReflectionHelpers.getMethodInfo <@@ (?) @@>
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


        let rec private visit (syn : HashSet<string>) (inh : HashSet<string>) (retType : byref<Type>) (kind : byref<AttributeKind>) (e : Expr) =
            match e with
                | AssignInherit(value) ->
                    kind <- AttributeKind.Inherited
                    retType <- value.Type
                    visit syn inh &retType &kind value

                | Inherit(name, o) -> 
                    inh.Add name |> ignore
                    visit syn inh &retType &kind o

                | Synthesize(name, o) ->
                    syn.Add name |> ignore
                    visit syn inh &retType &kind o

                | ShapeVar _ -> ()
                | ShapeLambda(_,b) -> visit syn inh &retType &kind b
                | ShapeCombination(o,args) -> for a in args do visit syn inh &retType &kind a


        let ofMethods (name : string) (methods : list<MethodInfo>) =
            option {

                let expressions =
                    methods |> List.choose (fun m ->
                        if m.ReturnType.ContainsGenericParameters then
                            Log.warn "[Ag] semantic functions may not return generic values '%s'" m.PrettyName
                            None
                        else
                            match Expr.TryGetReflectedDefinition m with
                                | Some e -> Some (m, e)
                                | None ->
                                    Log.warn "[Ag] could not get reflected definition for semantic function '%s'" m.PrettyName
                                    None
                    )


                let functions =
                    expressions |> List.map (fun (mi, e) ->
                        
                        let syn = HashSet<string>()
                        let inh = HashSet<string>()
                        let mutable ret = mi.ReturnType
                        let mutable kind = AttributeKind.Synthesized
                        visit syn inh &ret &kind e

                        let isRoot =
                            let parameters = mi.GetParameters()
                            if parameters.Length = 1 then
                                let t = parameters.[0].ParameterType
                                t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<Root<_>>
                            else
                                false

                        let func = 
                            {
                                original    = mi
                                code        = e
                                isRoot      = isRoot
                                kind        = kind
                                name        = name
                                nodeType    = mi.GetParameters().[0].ParameterType
                                inherits    = HashSet.toSet inh
                                synthesizes = HashSet.toSet syn
                            }

                        ret, kind, func
                    )

                let! kind = 
                    match functions |> List.fold (fun k (_,kk,_) -> k ||| kk) AttributeKind.None with
                        | AttributeKind.Mixed | AttributeKind.None -> 
                            Log.warn "[Ag] attribute '%s' has conflicting kinds (omitting)" name 
                            None
                        | kind -> 
                            Some kind

                let! retType =
                    let retTypes = functions |> List.map (fun (t,_,_) -> t) |> HashSet
                    if retTypes.Count = 1 then Some (Seq.head retTypes)
                    else 
                        Log.warn "[Ag] rules for %s have ambigous return types: [%s]" name (retTypes |> Seq.map (fun t -> t.PrettyName) |> String.concat "; ")
                        None

                return {
                    index           = Globals.getAttributeIndex name
                    name            = name
                    kind            = kind
                    valueType       = retType
                    functions       = functions |> List.map (fun (_,_,f) -> f)
                }
            }
  
        let getMoreSpecific (t : Type) (sf : SemanticFunctions) : list<SemanticFunction> =
            let mutable foundSelf = false

            let functions = 
                sf.functions |> List.filter (fun sf ->
                    if sf.nodeType = t then foundSelf <- true
                    t.MaybeAssignableFromSubtype sf.nodeType
                )

            if foundSelf then
                functions
            else
                //<@ fun (x : #t) -> inh <<= (x?name : 'valueType) @>
                let autoInherit = 
                    let assign = assign.MakeGenericMethod [|sf.valueType|]
                    let dyn = dyn.MakeGenericMethod [| t; sf.valueType |]
                    let self = Var("node", t)
                    Expr.Lambda(
                        self,
                        Expr.Call(assign, [Expr.Value(inh); Expr.Call(dyn, [Expr.Var self; Expr.Value(sf.name)])])
                    )

                let self =
                    {
                        name        = sf.name
                        kind        = sf.kind
                        original    = null
                        nodeType    = t
                        code        = autoInherit
                        isRoot      = false
                        inherits    = Set.ofList [sf.name]
                        synthesizes = Set.empty
                    }

                self :: functions


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

        let private convert (selfVar : Var) (scopeType : Type) (assumeStrict : Map<string, int>) (sf : SemanticFunction) =
            let code = 
                match inlineSemTypes sf.code with
                    | Lambda(node, body) ->   
                        let vscope = Var("scope", scopeType)
                        let scope = Expr.Var vscope
                        let rec repair (e : Expr) =
                            match e with
                                | Scope ->
                                    scope

                                | Synthesize(name, node) ->
                                    let node = repair node
                                    let td = selfVar.Type
                                    let dispatcher =
                                        if name = sf.name then 
                                            Expr.Var selfVar
                                        else 
                                            let index = Globals.getAttributeIndex name
                                            let dispatcher = <@@ Globals.synDispatchers.[index] @@>
                                            Expr.Coerce(dispatcher, td)

                                    let vo = Var("child", typeof<obj>)
                                    let o = Expr.Var vo
                                    let childScope = Var("childScope", scopeType)

                                    let invoke = td.GetMethod("Invoke") //MethodInfo.Create(td, "Invoke", [|typeof<obj>; scopeType|], e.Type)
                                    let getChild = scopeType.GetMethod("ChildScope", BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly) //MethodInfo.Create(scope.Type, "ChildScope", [| typeof<obj> |], scope.Type)

                                    Expr.Let(
                                        vo, Expr.Coerce(node, typeof<obj>), 
                                        Expr.Let(
                                            childScope, Expr.Call(scope, getChild, [o]),
                                            Expr.Call(dispatcher, invoke, [o; Expr.Var childScope])
                                        )
                                    )

                                | Inherit(name,node) ->
                                    let node = repair node

                                    match Map.tryFind name assumeStrict with
                                        | Some i ->
                                            let field = scopeType.GetField(sprintf "F%d" i)
                                            Expr.FieldGet(scope, field)

                                        | None -> 
                                        
                                            let inh = inheritMeth.MakeGenericMethod [|e.Type|]
                                            let td = typedefof<Dispatcher<_,_>>.MakeGenericType [| scopeType; e.Type |]
                                            let tr = typedefof<Dispatcher<_>>.MakeGenericType [| e.Type |]
                                            let index = Globals.getAttributeIndex name
                                            let dispatcher = 
                                                if name = sf.name then Expr.Var(selfVar)
                                                else Expr.Coerce(<@@ Globals.inhDispatchers.[index] @@>, td)
                                            let rootDispatcher = Expr.Coerce(<@@ Root.dispatchers.[index] @@>, tr)
                                        
                                            Expr.Call(scope, inh, [Expr.Value(index); dispatcher; rootDispatcher])


                                | AssignInherit(value) ->
                                    repair value

                                | ShapeVar _ -> e
                                | ShapeLambda(v,b) -> Expr.Lambda(v, repair b)
                                | ShapeCombination(o, args) -> RebuildShapeCombination(o, args |> List.map repair)

                        Expr.Lambda(node, Expr.Lambda(vscope, repair body))
                        //attName, [node; vscope], repair body
                    | _ ->
                        failwith "sadasdasdasdasd"

            { sf with code = code }

        let private addStrictInh (selfVar : Var) (scopeType : Type) (strict : Map<string, int>) (sf : SemanticFunctions) (otherSF : SemanticFunctions) =
            { sf with
                functions =
                    sf.functions |> List.collect (fun synSF ->
                        let inhIndex = otherSF.index
                        let other = otherSF |> getMoreSpecific synSF.nodeType
                        let field = scopeType.GetField(sprintf "F%d" strict.[otherSF.name])

                        other |> List.map (fun inhSF ->
                            let inhSF = convert selfVar scopeType strict inhSF
                            match inhSF.code, synSF.code with
                                | Lambda(ni, Lambda(si, inh)), Lambda(ns, Lambda(ss, syn)) ->
                                    let nf =
                                        if ni.Type.IsAssignableFrom ns.Type then ns
                                        elif ns.Type.IsAssignableFrom ni.Type then ni
                                        else failwith "[Ag] interface-inherit rules not implemented atm."

                                    let rec inlineInh (e : Expr) =
                                        match e with
                                            | Let(cs, (Call(Some s, mi, [n]) as childScope), body) when mi.Name = "ChildScope" && typeof<Scope>.IsAssignableFrom mi.DeclaringType ->
                                                Expr.Let(
                                                    cs, inlineInh childScope,
                                                    Expr.Sequential(
                                                        Expr.FieldSet(Expr.Var cs, field, inh),
                                                        //Expr.Call(Expr.Var cs, scopeSet, [Expr.Value(inhIndex); Expr.Coerce(inh, typeof<obj>)]),
                                                        inlineInh body
                                                    )
                                                )
                                                
                                            | ShapeVar v ->
                                                if v = ns then Expr.Var nf
                                                elif v = ni then Expr.Var nf
                                                elif v = ss then Expr.Var si
                                                else e
                                            | ShapeLambda(v,b) ->
                                                Expr.Lambda(v, inlineInh b)
                                            | ShapeCombination(o, args) ->
                                                RebuildShapeCombination(o, args |> List.map inlineInh)

                                    { synSF with 
                                        nodeType = nf.Type
                                        code = Expr.Lambda(nf, Expr.Lambda(si, inlineInh syn)) 
                                    }

                                | _ ->
                                    failwith ""
                        )

                    )
            }

        let private compileNormal (sf : SemanticFunctions) =
            let strictInh =
                match sf.kind with
                    | AttributeKind.Synthesized ->
                        sf.functions
                            |> List.filter (fun sf ->Set.isEmpty sf.synthesizes)
                            |> List.map (fun sf -> sf.inherits)
                            |> Set.intersectMany
                    | _ ->
                        Set.empty

            let inhIndices = strictInh |> Seq.mapi (fun i n -> (n,i)) |> Seq.toList
            let types = inhIndices |> List.map (fun (n,i) -> Globals.semanticFunctions.[n].valueType)
            let attributeIndices = strictInh |> Seq.map (fun n -> Globals.getAttributeIndex(n)) |> Seq.toList
            let strictInh = Map.ofList inhIndices

            let scopeType = Scope.getType types
            let dispType = WrappedDispatcher.getType sf.valueType types

            let dispType = dispType
            let selfDisp = Var(sprintf "self_%s" (Guid.NewGuid().ToString("N")), dispType)

            let dispatcher =
                let mutable final = { sf with functions = sf.functions |> List.map (fun sf -> convert selfDisp scopeType strictInh sf) }
                for (KeyValue(s,_)) in strictInh do
                    let other = Globals.semanticFunctions.[s]
                    final <- addStrictInh selfDisp scopeType strictInh final other

                let methods = 
                    final.functions |> List.choose (fun e ->
                        match e.code with
                            | Lambda(n,Lambda(s, body)) -> Some (sf.name, [n;Var(s.Name, scopeType)], fun _ -> body)
                            | _ -> None
                    )

                let tModule = 
                    Compiler.compile {
                        Name = sf.name
                        Declarations =
                        [
                            Class {
                                Name = "Sem"
                                Arguments = [ selfDisp ]
                                BaseType = None
                                Fields = []
                                Members = methods
                            }
                        ]
                    }

                let tSem = tModule.GetNestedType("Sem")
                let semCtor = tSem.GetConstructors().[0]

                let mutable table = None
                let getTable (self : obj) =
                    match table with
                        | Some t -> t
                        | None ->
                            let instance = Activator.CreateInstance(tSem, self)
                            let methods = tSem.GetMethods(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                            let t = 
                                MethodTable.ofList [
                                    for m in methods do
                                        yield instance, m
                                ]
                            table <- Some t
                            t
            
                let resolve (self : obj) (t : Type) =
                    self |> getTable |> MethodTable.tryResolve [| t; scopeType |]   
                            
                let tResolve = typeof<obj -> Type -> Option<obj * MethodInfo>>
                let tIndices = attributeIndices |> List.map (fun _ -> typeof<int>) |> List.toArray
                

                let dispCtor = dispType.GetConstructor (Array.append tIndices [|tResolve|])
                let disp = dispCtor.Invoke (Array.append (List.toArray (List.map (fun a -> a :> obj) attributeIndices)) [|resolve|])
                
                disp |> unbox<IObjectDispatcher<Scope>>

            dispatcher

        let private compileRoot (attName : string) (valueType : Type) (methods : list<SemanticFunction>) =
            let td = typedefof<Dispatcher<_>>.MakeGenericType [| valueType |]
            let ctor = td.GetConstructor [| typeof<obj -> Type -> Option<obj * MethodInfo>> |]
            let realMethods =
                methods |> List.mapi (fun i sf ->
                    let mi = sf.original
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
                                attName, [Var(v.Name, t)], body
                            else 
                                failwith "[Ag] root rule does not take an argument"
                        | _ -> 
                            failwith "[Ag] root rule does not take an argument"
                )

            let instance,methods = QuotationCompiler.CreateInstance realMethods

            let table = methods |> Array.map (fun mi -> instance,mi) |> MethodTable.ofArray

            let resolve (self : obj) (t : Type) =
                MethodTable.tryResolve [| t |] table

            ctor.Invoke([|resolve|]) |> unbox<IObjectDispatcher>

        

        let compile (sf : SemanticFunctions) =
            if sf.kind = AttributeKind.Inherited then 
                let root, other = sf.functions |> List.partition(fun f -> f.isRoot)
                Root.dispatchers.[sf.index] <- compileRoot sf.name sf.valueType root
                Globals.inhDispatchers.[sf.index] <- compileNormal { sf with functions = other }
            else 
                Globals.synDispatchers.[sf.index] <- compileNormal sf

  
    [<AllowNullLiteral>]
    type SumScope(parent : SumScope, node : obj) =
        inherit Scope(parent, node)

        static let root = SumScope(null, null)


        [<DefaultValue>] val mutable public Index : int

        static member Root = root

        member x.ChildScope(child : obj) =
            if isNull node then SumScope(null, child, Index = 0)
            else SumScope(x, child)

        member x.Get(i : int) =
            match i with
                | 1 -> x.Index :> obj
                | _ -> base.Get(i)

        member x.Inherit(i : int, disp : Dispatcher<Scope, 'r>, root : Dispatcher<'r>) =
            match i with
                | 1 -> x.Index |> unbox<'r>
                | _ -> base.Inherit(i, disp, root)

    type IList =
        abstract member Sum : SumScope -> int
        abstract member All : SumScope -> list<int>
 
    type Nil() = 
        interface IList with
            member x.Sum (s : SumScope) =
                s.Index

            member x.All (s : SumScope) =
                [ s.Index ]

    type Cons(head : int, tail : IList) =
        interface IList with
            member x.Sum s = 
                let child = s.ChildScope tail
                child.Index <- s.Index + 1
                head + tail.Sum(child)


            member x.All (s : SumScope) =
                let child = s.ChildScope tail
                child.Index <- s.Index + 1
                head :: tail.All child


        member x.Head = head
        member x.Tail = tail


    module Exts =
        type IList with
            [<Attribute(AttributeKind.Synthesized)>]
            member x.Sum() : int = x?Sum()

            [<Attribute(AttributeKind.Synthesized)>]
            member x.All() : list<int> = x?All()

            [<Attribute(AttributeKind.Inherited)>]
            member x.Index : int = x?Index

            [<Attribute(AttributeKind.Inherited)>]
            member x.Bla : int = x?Bla

        [<Semantic; ReflectedDefinition>]
        type Sems() =
            member x.All(n : Nil) : list<int> = 
                [n.Index]

            member x.All(c : Cons) : list<int> = 
                c.Head :: c.Tail.All()

            member x.Sum(n : Nil) : int = 
                n.Index

            member x.Sum(c : Cons) : int = 
                c.Head + c.Tail.Sum()

            member x.Index(r : Root<IList>) =
                inh <<= 0

            member x.Index(c : Cons) =
                inh <<= 1 + c.Index

            member x.Bla(r : Root<IList>) =
                inh <<= 0

            member x.Bla(c : Cons) =
                inh <<= 10 + c.Bla

    let mutable private initialized = 0
    let init() =
        if System.Threading.Interlocked.Exchange(&initialized, 1) = 0 then
            let functions = 
                Introspection.GetAllTypesWithAttribute<SemanticAttribute>()
                    |> Seq.map (fun t -> t.E0)
                    |> Seq.collect (fun t -> 
                        let all = t.GetMethods(BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.DeclaredOnly)
                        all |> Seq.filter (fun mi -> not (mi.Name.StartsWith "get_" || mi.Name.StartsWith "set_"))
                       )
                    |> Seq.groupBy (fun mi -> mi.Name)
                    |> Seq.map (fun (name, mis) -> name, Seq.toList mis)
                    |> Seq.choose (fun (name, mis) -> SemanticFunctions.ofMethods name mis)
                    |> Seq.toArray

            let cnt = Globals.attributeCount()
            Globals.semanticFunctions <- functions |> Seq.map (fun sf -> sf.name,sf) |> Map.ofSeq
            Globals.synDispatchers <- Array.zeroCreate cnt
            Globals.inhDispatchers <- Array.zeroCreate cnt
            Root.dispatchers <- Array.zeroCreate cnt

            for f in functions do
                SemanticFunctions.compile f 

    [<Demo("aaaag")>]
    let run() =
        init()
        let all : obj -> list<int>  = Globals.synFunction "All"
        let sum : obj -> int        = Globals.synFunction "Sum"
        let list = Cons(0, Cons(1, Nil()))
        Log.line "sum = %A" (sum list)
        Log.line "all = %A" (all list)

        

        if true then

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
//                let s = Scope.Root.ChildScope test
//                s.Set(1, 0)
//                s.Set(2, 0)
                test.Sum (SumScope.Root.ChildScope test) |> ignore
            sw.Stop()
            Log.line "virt: %A" (sw.MicroTime / iter)

            let sw = System.Diagnostics.Stopwatch()
            let disp = Globals.synDispatchers.[Globals.getAttributeIndex "Sum"]// |> unbox<WrappedDispatcher<int, int>>
            let mutable res = null
            sw.Start()
            for i in 1 .. iter do
                sum test |> ignore
                //disp.TryInvoke(test, Scope.Root.ChildScope test, &res) |> ignore
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