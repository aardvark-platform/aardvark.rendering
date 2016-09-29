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

    module Globals =
        open System.Threading
        open System.Collections.Concurrent
        type Marker = Marker


        let mutable rootDispatchers : IObjectDispatcher[] = null
        let mutable synDispatchers : IObjectDispatcher2[] = null
        let mutable inhDispatchers : IObjectDispatcher2[] = null

        let mutable private currentIndex = -1
        let private attributeIndices = ConcurrentDictionary<string, int>()

        let private semInstances = ConcurrentDictionary<Type, obj>()

        let getInstance (t : Type) =
            semInstances.GetOrAdd(t, fun t -> Activator.CreateInstance t)

        type Instance<'a> private() =
            static let a = getInstance (typeof<'a>) |> unbox<'a>
            static member Instance = a

        let getTypedInstance<'a>() = Instance<'a>.Instance

        let instance<'a> = Instance<'a>.Instance

        let getAttributeIndex (name : string) =
            attributeIndices.GetOrAdd(name, fun _ -> Interlocked.Increment(&currentIndex))

        let tryGetAttributeIndex (name : string) =
            match attributeIndices.TryGetValue name with
                | (true, i) -> Some i
                | _ -> None

        let attributeCount() = currentIndex + 1

        let mutable semanticFunctions = Map.empty


    [<AllowNullLiteral>]
    type Scope =
        class
            val mutable public Parent   : Scope
            val mutable public Node     : obj
            val mutable public Cache    : Map<int, Option<obj>>

            member inline x.ChildScope (child : obj) : Scope =
                Scope(x, child)


            abstract member TryGet : int -> Option<obj>

            default x.TryGet(i : int) =
                if isNull x.Parent then
                    match Map.tryFind i x.Cache with
                        | Some a -> a
                        | _ -> 
                            match Globals.rootDispatchers.[i].TryInvoke(x.Node) with
                                | (true, res) ->
                                    x.Cache <- Map.add i (Some res) x.Cache
                                    Some res
                                | _ ->
                                    x.Cache <- Map.add i None x.Cache
                                    None
                else
                    match Map.tryFind i x.Cache with
                        | Some c -> c
                        | None -> None

            member x.TryInheritInternal(i : int, disp : IDispatcher<Scope, 'r>, root : IDispatcher<'r>) =
                match x.TryGet i with
                    | Some r -> 
                        match r with
                            | :? 'r as r -> Some r
                            | _ -> None
                    | None ->
                        let res = 
                            match x.Parent with
                                | null -> 
                                    match root.TryInvoke(x.Node) with
                                        | (true, v) -> Some v
                                        | _ -> None

                                | p -> 
                                    match disp.TryInvoke(p.Node, p) with
                                        | (true, res) -> Some res
                                        | _ -> p.TryInheritInternal(i, disp, root)
                        x.Cache <- Map.add i (res |> Option.map (fun v -> v :> obj)) x.Cache
                        res

            member x.Get(i : int) =
                match x.TryGet i with
                    | Some v -> v
                    | None -> failwithf "[Ag] could not get attribute %A" i

            member x.InheritInternal(i : int, disp : IDispatcher<Scope, 'r>, root : IDispatcher<'r>) =
                match x.TryInheritInternal(i, disp, root) with
                    | Some res -> res
                    | None -> failwithf "[Ag] could not inherit attribute %A" i

            member x.Inherit<'r>(i : int) : 'r =
                let disp = Globals.inhDispatchers.[i] |> unbox<IDispatcher<Scope, 'r>>
                let root = Globals.rootDispatchers.[i] |> unbox<IDispatcher<'r>>
                x.InheritInternal(i, disp, root)

            member x.TryInherit<'r>(i : int) : Option<'r> =
                let disp = Globals.inhDispatchers.[i] |> unbox<IDispatcher<Scope, 'r>>
                let root = Globals.rootDispatchers.[i] |> unbox<IDispatcher<'r>>
                x.TryInheritInternal(i, disp, root)

            override x.ToString() =
                match x.Parent with
                    | null -> x.Node.GetType().PrettyName
                    | p -> sprintf "%s/%s" (p.ToString()) (x.Node.GetType().PrettyName)


            new(p : Scope, n : obj) = { Parent = p; Node = n; Cache = Map.empty }
            new(n : obj) = { Parent = null; Node = n; Cache = Map.empty }


        end


    module CodeGen =
        open System
        open System.IO

        let builder = new System.Text.StringBuilder()

        let printfn fmt = Printf.kprintf(fun str -> builder.AppendLine(str) |> ignore; Console.WriteLine(str)) fmt

        let genArg (i : int) =
            let rec create (i : int) =
                if i >= 26 then
                    create (i / 26 - 1) + create (i % 26)
                else
                    sprintf "%c" ('a' + char i)

            sprintf "'%s" (create i)

        let generate (cnt : int) =

            if cnt = 0 then
                failwith "cannot generate normal code"

            let args = List.init cnt genArg
            let fields = args |> List.mapi (fun i t -> sprintf "F%d" i, t)
            let indices = List.init cnt (sprintf "i%d")
            let all = args |> String.concat ", "
            let defI = indices |> List.map (sprintf "%s : int") |> String.concat ", "
            let argI = indices |> String.concat ", "


            printfn "[<AllowNullLiteral>]"
            printfn "type Scope<%s>(%s, parent : Scope<%s>, node : obj) =" all defI all
            printfn "    inherit Scope(parent, node)"

            for (f,t) in fields do
                printfn "    [<DefaultValue>] val mutable public %s : %s" f t

            printfn ""
            printfn "    member x.ChildScope(child : obj) = Scope<%s>(%s, x, child)" all argI
            printfn ""
            printfn "    override x.TryGet(i : int) ="
            let mutable index = 0
            for ((f,t),i) in List.zip fields indices do
                let ifstr = if index = 0 then "if" else "elif"
                printfn "        %s i = %s then Some (x.%s :> obj)" ifstr i f
                index <- index + 1
            printfn "        else base.TryGet(i)"

            printfn ""
            printfn ""


            printfn "type WrappedDispatcher<%s, 'res>(%s, resolve : obj -> Type -> Option<obj * MethodInfo>) =" all defI
            printfn "    inherit Dispatcher<Scope<%s>, 'res>(resolve)" all
            printfn ""
            printfn "    member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) ="
            printfn "        let ns = Scope<%s>(%s, null, n)" all argI

            for ((f,t),i) in List.zip fields indices do
                printfn "        ns.%s <- s.Inherit<%s>(%s)" f t i

            printfn "        x.TryInvoke(n, ns, &res)"
            printfn ""
            printfn "    interface IObjectDispatcher2 with"
            printfn "        member x.TryInvoke(n : obj, s : obj, o : byref<obj>) ="
            printfn "            let mutable res = Unchecked.defaultof<'res>"
            printfn "            if x.TryInvoke(n, unbox s, &res) then"
            printfn "                o <- res"
            printfn "                true"
            printfn "            else"
            printfn "                false "
            printfn ""
            printfn "    interface IObjectDispatcher<Scope> with"
            printfn "        member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = "
            printfn "            let mutable res = Unchecked.defaultof<'res>"
            printfn "            if x.TryInvoke(n, s, &res) then"
            printfn "                o <- res"
            printfn "                true"
            printfn "            else"
            printfn "                false"
            printfn ""
            printfn "    interface IDispatcher<Scope, 'res> with"
            printfn "        member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)"

        let run() =
            builder.Clear() |> ignore

            let cnt = 32
            for i in 1 .. cnt-1 do
                generate i

            printfn "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            printfn "module Scope ="
            printfn "    let getType (strict : list<Type>) ="
            printfn "        match strict with"
            printfn "            | [] -> typeof<Scope>"
            for i in 1..cnt-1 do
                let args = List.init i (sprintf "t%d")
                let head = args |> String.concat "; "
                let underscores = List.init i (fun _ -> "_") |> String.concat ","
                printfn "            | [%s] -> typedefof<Scope<%s>>.MakeGenericType [| %s |]" head underscores head
            printfn "            | _ -> failwith \"[Ag] too many strict arguments\""

            printfn "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            printfn "module WrappedDispatcher ="
            printfn "    let getType (ret : Type) (strict : list<Type>) ="
            printfn "        match strict with"
            printfn "            | [] -> typedefof<Dispatcher<_,_>>.MakeGenericType [| typeof<Scope>; ret |]"
            for i in 1..cnt-1 do
                let args = List.init i (sprintf "t%d")
                let head = args |> String.concat "; "
                let underscores = List.init (1+i) (fun _ -> "_") |> String.concat ","
                printfn "            | [%s] -> typedefof<WrappedDispatcher<%s>>.MakeGenericType [| %s; ret |]" head underscores head
            printfn "            | _ -> failwith \"[Ag] too many strict arguments\""



            File.WriteAllText(@"C:\Users\schorsch\Desktop\scopes.fs", builder.ToString())

    //[<AutoOpen>]
    module HandCraftedGenericStuff = 
        [<AllowNullLiteral>]
        type Scope<'a> =
            class
                inherit Scope
            
                val mutable public I0 : int
                [<DefaultValue>] val mutable public F0 : 'a

                member x.ChildScope(child : obj) : Scope<'a> = Scope<'a>(x.I0, x, child)

                override x.TryGet(i : int) =
                    if i = x.I0 then Some (x.F0 :> obj)
                    else base.TryGet(i)

                new(i0 : int, p : Scope<'a>, n : obj) = { inherit Scope(p, n); I0 = i0 }
            end

        [<AllowNullLiteral>]
        type Scope<'a, 'b>(i0 : int, i1 : int, parent : Scope<'a, 'b>, node : obj) =
            inherit Scope(parent, node)

            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b

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

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'r>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

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

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'r>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

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

    [<AutoOpen>]
    module GeneratedGenericStuff =
        [<AllowNullLiteral>]
        type Scope<'a>(i0 : int, parent : Scope<'a>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a

            member x.ChildScope(child : obj) = Scope<'a>(i0, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'res>(i0 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a>(i0, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b>(i0 : int, i1 : int, parent : Scope<'a, 'b>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b

            member x.ChildScope(child : obj) = Scope<'a, 'b>(i0, i1, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'res>(i0 : int, i1 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b>(i0, i1, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c>(i0 : int, i1 : int, i2 : int, parent : Scope<'a, 'b, 'c>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c>(i0, i1, i2, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'res>(i0 : int, i1 : int, i2 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c>(i0, i1, i2, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd>(i0 : int, i1 : int, i2 : int, i3 : int, parent : Scope<'a, 'b, 'c, 'd>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd>(i0, i1, i2, i3, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd>(i0, i1, i2, i3, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, parent : Scope<'a, 'b, 'c, 'd, 'e>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e>(i0, i1, i2, i3, i4, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e>(i0, i1, i2, i3, i4, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f>(i0, i1, i2, i3, i4, i5, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f>(i0, i1, i2, i3, i4, i5, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g>(i0, i1, i2, i3, i4, i5, i6, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g>(i0, i1, i2, i3, i4, i5, i6, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h>(i0, i1, i2, i3, i4, i5, i6, i7, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h>(i0, i1, i2, i3, i4, i5, i6, i7, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i>(i0, i1, i2, i3, i4, i5, i6, i7, i8, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i>(i0, i1, i2, i3, i4, i5, i6, i7, i8, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y
            [<DefaultValue>] val mutable public F25 : 'z

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                elif i = i25 then Some (x.F25 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                ns.F25 <- s.Inherit<'z>(i25)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y
            [<DefaultValue>] val mutable public F25 : 'z
            [<DefaultValue>] val mutable public F26 : 'aa

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                elif i = i25 then Some (x.F25 :> obj)
                elif i = i26 then Some (x.F26 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                ns.F25 <- s.Inherit<'z>(i25)
                ns.F26 <- s.Inherit<'aa>(i26)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y
            [<DefaultValue>] val mutable public F25 : 'z
            [<DefaultValue>] val mutable public F26 : 'aa
            [<DefaultValue>] val mutable public F27 : 'ab

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                elif i = i25 then Some (x.F25 :> obj)
                elif i = i26 then Some (x.F26 :> obj)
                elif i = i27 then Some (x.F27 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                ns.F25 <- s.Inherit<'z>(i25)
                ns.F26 <- s.Inherit<'aa>(i26)
                ns.F27 <- s.Inherit<'ab>(i27)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, i28 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y
            [<DefaultValue>] val mutable public F25 : 'z
            [<DefaultValue>] val mutable public F26 : 'aa
            [<DefaultValue>] val mutable public F27 : 'ab
            [<DefaultValue>] val mutable public F28 : 'ac

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, i28, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                elif i = i25 then Some (x.F25 :> obj)
                elif i = i26 then Some (x.F26 :> obj)
                elif i = i27 then Some (x.F27 :> obj)
                elif i = i28 then Some (x.F28 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, i28 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, i28, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                ns.F25 <- s.Inherit<'z>(i25)
                ns.F26 <- s.Inherit<'aa>(i26)
                ns.F27 <- s.Inherit<'ab>(i27)
                ns.F28 <- s.Inherit<'ac>(i28)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, i28 : int, i29 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y
            [<DefaultValue>] val mutable public F25 : 'z
            [<DefaultValue>] val mutable public F26 : 'aa
            [<DefaultValue>] val mutable public F27 : 'ab
            [<DefaultValue>] val mutable public F28 : 'ac
            [<DefaultValue>] val mutable public F29 : 'ad

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, i28, i29, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                elif i = i25 then Some (x.F25 :> obj)
                elif i = i26 then Some (x.F26 :> obj)
                elif i = i27 then Some (x.F27 :> obj)
                elif i = i28 then Some (x.F28 :> obj)
                elif i = i29 then Some (x.F29 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, i28 : int, i29 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, i28, i29, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                ns.F25 <- s.Inherit<'z>(i25)
                ns.F26 <- s.Inherit<'aa>(i26)
                ns.F27 <- s.Inherit<'ab>(i27)
                ns.F28 <- s.Inherit<'ac>(i28)
                ns.F29 <- s.Inherit<'ad>(i29)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<AllowNullLiteral>]
        type Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'ae>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, i28 : int, i29 : int, i30 : int, parent : Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'ae>, node : obj) =
            inherit Scope(parent, node)
            [<DefaultValue>] val mutable public F0 : 'a
            [<DefaultValue>] val mutable public F1 : 'b
            [<DefaultValue>] val mutable public F2 : 'c
            [<DefaultValue>] val mutable public F3 : 'd
            [<DefaultValue>] val mutable public F4 : 'e
            [<DefaultValue>] val mutable public F5 : 'f
            [<DefaultValue>] val mutable public F6 : 'g
            [<DefaultValue>] val mutable public F7 : 'h
            [<DefaultValue>] val mutable public F8 : 'i
            [<DefaultValue>] val mutable public F9 : 'j
            [<DefaultValue>] val mutable public F10 : 'k
            [<DefaultValue>] val mutable public F11 : 'l
            [<DefaultValue>] val mutable public F12 : 'm
            [<DefaultValue>] val mutable public F13 : 'n
            [<DefaultValue>] val mutable public F14 : 'o
            [<DefaultValue>] val mutable public F15 : 'p
            [<DefaultValue>] val mutable public F16 : 'q
            [<DefaultValue>] val mutable public F17 : 'r
            [<DefaultValue>] val mutable public F18 : 's
            [<DefaultValue>] val mutable public F19 : 't
            [<DefaultValue>] val mutable public F20 : 'u
            [<DefaultValue>] val mutable public F21 : 'v
            [<DefaultValue>] val mutable public F22 : 'w
            [<DefaultValue>] val mutable public F23 : 'x
            [<DefaultValue>] val mutable public F24 : 'y
            [<DefaultValue>] val mutable public F25 : 'z
            [<DefaultValue>] val mutable public F26 : 'aa
            [<DefaultValue>] val mutable public F27 : 'ab
            [<DefaultValue>] val mutable public F28 : 'ac
            [<DefaultValue>] val mutable public F29 : 'ad
            [<DefaultValue>] val mutable public F30 : 'ae

            member x.ChildScope(child : obj) = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'ae>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, i28, i29, i30, x, child)

            override x.TryGet(i : int) =
                if i = i0 then Some (x.F0 :> obj)
                elif i = i1 then Some (x.F1 :> obj)
                elif i = i2 then Some (x.F2 :> obj)
                elif i = i3 then Some (x.F3 :> obj)
                elif i = i4 then Some (x.F4 :> obj)
                elif i = i5 then Some (x.F5 :> obj)
                elif i = i6 then Some (x.F6 :> obj)
                elif i = i7 then Some (x.F7 :> obj)
                elif i = i8 then Some (x.F8 :> obj)
                elif i = i9 then Some (x.F9 :> obj)
                elif i = i10 then Some (x.F10 :> obj)
                elif i = i11 then Some (x.F11 :> obj)
                elif i = i12 then Some (x.F12 :> obj)
                elif i = i13 then Some (x.F13 :> obj)
                elif i = i14 then Some (x.F14 :> obj)
                elif i = i15 then Some (x.F15 :> obj)
                elif i = i16 then Some (x.F16 :> obj)
                elif i = i17 then Some (x.F17 :> obj)
                elif i = i18 then Some (x.F18 :> obj)
                elif i = i19 then Some (x.F19 :> obj)
                elif i = i20 then Some (x.F20 :> obj)
                elif i = i21 then Some (x.F21 :> obj)
                elif i = i22 then Some (x.F22 :> obj)
                elif i = i23 then Some (x.F23 :> obj)
                elif i = i24 then Some (x.F24 :> obj)
                elif i = i25 then Some (x.F25 :> obj)
                elif i = i26 then Some (x.F26 :> obj)
                elif i = i27 then Some (x.F27 :> obj)
                elif i = i28 then Some (x.F28 :> obj)
                elif i = i29 then Some (x.F29 :> obj)
                elif i = i30 then Some (x.F30 :> obj)
                else base.TryGet(i)


        type WrappedDispatcher<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'ae, 'res>(i0 : int, i1 : int, i2 : int, i3 : int, i4 : int, i5 : int, i6 : int, i7 : int, i8 : int, i9 : int, i10 : int, i11 : int, i12 : int, i13 : int, i14 : int, i15 : int, i16 : int, i17 : int, i18 : int, i19 : int, i20 : int, i21 : int, i22 : int, i23 : int, i24 : int, i25 : int, i26 : int, i27 : int, i28 : int, i29 : int, i30 : int, resolve : obj -> Type -> Option<obj * MethodInfo>) =
            inherit Dispatcher<Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'ae>, 'res>(resolve)

            member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) =
                let ns = Scope<'a, 'b, 'c, 'd, 'e, 'f, 'g, 'h, 'i, 'j, 'k, 'l, 'm, 'n, 'o, 'p, 'q, 'r, 's, 't, 'u, 'v, 'w, 'x, 'y, 'z, 'aa, 'ab, 'ac, 'ad, 'ae>(i0, i1, i2, i3, i4, i5, i6, i7, i8, i9, i10, i11, i12, i13, i14, i15, i16, i17, i18, i19, i20, i21, i22, i23, i24, i25, i26, i27, i28, i29, i30, null, n)
                ns.F0 <- s.Inherit<'a>(i0)
                ns.F1 <- s.Inherit<'b>(i1)
                ns.F2 <- s.Inherit<'c>(i2)
                ns.F3 <- s.Inherit<'d>(i3)
                ns.F4 <- s.Inherit<'e>(i4)
                ns.F5 <- s.Inherit<'f>(i5)
                ns.F6 <- s.Inherit<'g>(i6)
                ns.F7 <- s.Inherit<'h>(i7)
                ns.F8 <- s.Inherit<'i>(i8)
                ns.F9 <- s.Inherit<'j>(i9)
                ns.F10 <- s.Inherit<'k>(i10)
                ns.F11 <- s.Inherit<'l>(i11)
                ns.F12 <- s.Inherit<'m>(i12)
                ns.F13 <- s.Inherit<'n>(i13)
                ns.F14 <- s.Inherit<'o>(i14)
                ns.F15 <- s.Inherit<'p>(i15)
                ns.F16 <- s.Inherit<'q>(i16)
                ns.F17 <- s.Inherit<'r>(i17)
                ns.F18 <- s.Inherit<'s>(i18)
                ns.F19 <- s.Inherit<'t>(i19)
                ns.F20 <- s.Inherit<'u>(i20)
                ns.F21 <- s.Inherit<'v>(i21)
                ns.F22 <- s.Inherit<'w>(i22)
                ns.F23 <- s.Inherit<'x>(i23)
                ns.F24 <- s.Inherit<'y>(i24)
                ns.F25 <- s.Inherit<'z>(i25)
                ns.F26 <- s.Inherit<'aa>(i26)
                ns.F27 <- s.Inherit<'ab>(i27)
                ns.F28 <- s.Inherit<'ac>(i28)
                ns.F29 <- s.Inherit<'ad>(i29)
                ns.F30 <- s.Inherit<'ae>(i30)
                x.TryInvoke(n, ns, &res)

            interface IObjectDispatcher2 with
                member x.TryInvoke(n : obj, s : obj, o : byref<obj>) =
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, unbox s, &res) then
                        o <- res
                        true
                    else
                        false 

            interface IObjectDispatcher<Scope> with
                member x.TryInvoke(n : obj, s : Scope, o : byref<obj>) = 
                    let mutable res = Unchecked.defaultof<'res>
                    if x.TryInvoke(n, s, &res) then
                        o <- res
                        true
                    else
                        false

            interface IDispatcher<Scope, 'res> with
                member x.TryInvoke(n : obj, s : Scope, res : byref<'res>) = x.TryInvoke(n, s, &res)
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module Scope =
            let getType (strict : list<Type>) =
                match strict with
                    | [] -> typeof<Scope>
                    | [t0] -> typedefof<Scope<_>>.MakeGenericType [| t0 |]
                    | [t0; t1] -> typedefof<Scope<_,_>>.MakeGenericType [| t0; t1 |]
                    | [t0; t1; t2] -> typedefof<Scope<_,_,_>>.MakeGenericType [| t0; t1; t2 |]
                    | [t0; t1; t2; t3] -> typedefof<Scope<_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3 |]
                    | [t0; t1; t2; t3; t4] -> typedefof<Scope<_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4 |]
                    | [t0; t1; t2; t3; t4; t5] -> typedefof<Scope<_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5 |]
                    | [t0; t1; t2; t3; t4; t5; t6] -> typedefof<Scope<_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7] -> typedefof<Scope<_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8] -> typedefof<Scope<_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29 |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29; t30] -> typedefof<Scope<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29; t30 |]
                    | _ -> failwith "[Ag] too many strict arguments"
        [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
        module WrappedDispatcher =
            let getType (ret : Type) (strict : list<Type>) =
                match strict with
                    | [] -> typedefof<Dispatcher<_,_>>.MakeGenericType [| typeof<Scope>; ret |]
                    | [t0] -> typedefof<WrappedDispatcher<_,_>>.MakeGenericType [| t0; ret |]
                    | [t0; t1] -> typedefof<WrappedDispatcher<_,_,_>>.MakeGenericType [| t0; t1; ret |]
                    | [t0; t1; t2] -> typedefof<WrappedDispatcher<_,_,_,_>>.MakeGenericType [| t0; t1; t2; ret |]
                    | [t0; t1; t2; t3] -> typedefof<WrappedDispatcher<_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; ret |]
                    | [t0; t1; t2; t3; t4] -> typedefof<WrappedDispatcher<_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; ret |]
                    | [t0; t1; t2; t3; t4; t5] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29; ret |]
                    | [t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29; t30] -> typedefof<WrappedDispatcher<_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_>>.MakeGenericType [| t0; t1; t2; t3; t4; t5; t6; t7; t8; t9; t10; t11; t12; t13; t14; t15; t16; t17; t18; t19; t20; t21; t22; t23; t24; t25; t26; t27; t28; t29; t30; ret |]
                    | _ -> failwith "[Ag] too many strict arguments"



    module Ag =
        open System.Threading
        open System.Collections.Concurrent

        let currentScope : Scope[] = Array.zeroCreate 2048


        let inline setScope (s : Scope) =
            let id = Thread.CurrentThread.ManagedThreadId
            currentScope.[id] <- s

        let synFunction<'a> (name : string) : obj -> 'a =
            match Globals.tryGetAttributeIndex name with
                | Some i ->
                    let d = Globals.synDispatchers.[i] |> unbox<IDispatcher<Scope, 'a>>
                    let f = fun (n : obj) -> d.Invoke(n, Scope(null, n))
                    f
                | None ->
                    failwithf "[Ag] could not get syn rule for '%s'" name


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
        let private inheritMeth = typeof<Scope>.GetMethod("InheritInternal")

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
                                            let rootDispatcher = Expr.Coerce(<@@ Globals.rootDispatchers.[index] @@>, tr)
                                        
                                            Expr.Call(scope, inh, [Expr.Value(index); dispatcher; rootDispatcher])

                                | AssignInherit(value) ->
                                    repair value

                                | ShapeVar _ -> e
                                | ShapeLambda(v,b) -> Expr.Lambda(v, repair b)
                                | ShapeCombination(o, args) -> RebuildShapeCombination(o, args |> List.map repair)



                        let setCurrent (scope : Expr) = 
                            let s : Expr<Scope> = Expr.Coerce(scope, typeof<Scope>) |> Expr.Cast
                            <@ Ag.setScope %s @>


                        let body =
                            let rep = repair body
                            rep
//                            if sf.kind = AttributeKind.Synthesized && Set.isEmpty sf.synthesizes then Expr.Sequential(setCurrent scope, rep)
//                            else rep

                        Expr.Lambda(node, Expr.Lambda(vscope, body))
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
                
                disp |> unbox<IObjectDispatcher2>

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
                Globals.rootDispatchers.[sf.index] <- compileRoot sf.name sf.valueType root
                Globals.inhDispatchers.[sf.index] <- compileNormal { sf with functions = other }
            else 
                Globals.synDispatchers.[sf.index] <- compileNormal sf

    [<AllowNullLiteral>]
    type SumScope =
        class 
            inherit Scope
            val mutable public Index : int

            member x.ChildScope(child : obj) =
                if isNull x.Node then SumScope(null, child, Index = 0)
                else SumScope(x, child)

            new(p,n) = { inherit Scope(p,n); Index = 0 }
        end
//    [<AllowNullLiteral>]
//    type SumScope(parent : SumScope, node : obj) =
//        inherit Scope(parent, node)
//
//        static let root = SumScope(null, null)
//
//
//        [<DefaultValue>] val mutable public Index : int
//
//        static member Root = root
//
//        member x.ChildScope(child : obj) =
//            if isNull node then SumScope(null, child, Index = 0)
//            else SumScope(x, child)


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
            Globals.rootDispatchers <- Array.zeroCreate cnt

            for f in functions do
                SemanticFunctions.compile f 

    [<Demo("aaaag")>]
    let run() =
        init()
        let all : obj -> list<int>  = Ag.synFunction "All"
        let sum : obj -> int        = Ag.synFunction "Sum"
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
                test.Sum (SumScope(null, test)) |> ignore
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