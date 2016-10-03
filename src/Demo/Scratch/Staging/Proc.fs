namespace Aardvark.Base.Incremental

open System
open System.Collections.Generic
open System.Collections.Concurrent
open Aardvark.Base
open Aardvark.Base.Monads.State

type ProcState<'s> =
    {
        time : Time
        userState : 's
    }


[<AllowNullLiteral>]
type IObservable =
    abstract member Subscribe : (obj -> unit) -> IDisposable


[<AbstractClass>]
type Pattern() =
    abstract member Relevant : PersistentHashSet<IObservable>
    abstract member MatchUntyped : ProcState<'s> * IObservable * obj -> Option<obj>
    abstract member DependsOnTime : bool

[<AbstractClass>]
type Pattern<'a>() =
    inherit Pattern()

    abstract member Match : ProcState<'s> * IObservable * obj -> Option<'a>

    override x.MatchUntyped(state, source, value) =
        match x.Match(state, source, value) with
            | Some v -> Some (v :> obj)
            | None -> None


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Pattern =
    type private NeverPattern<'a> private() =
        inherit Pattern<'a>()

        static let instance = NeverPattern<'a>() :> Pattern<'a>
        static member Instance = instance

        override x.DependsOnTime = false
        override x.Relevant = PersistentHashSet.empty
        override x.Match(_,_,_) = None

    type private Obs<'a>(inner : IObservable<'a>) =
        member private x.Inner = inner

        override x.GetHashCode() = inner.GetHashCode()
        override x.Equals o =
            match o with
                | :? Obs<'a> as o -> inner = o.Inner 
                | _ -> false

        interface IObservable with
            member x.Subscribe f = inner.Subscribe(fun v -> v :> obj |> f)

    type private M<'a>(inner : IMod<'a>) =
        member private x.Inner = inner

        override x.GetHashCode() = inner.GetHashCode()
        override x.Equals o =
            match o with
                | :? M<'a> as o -> inner = o.Inner 
                | _ -> false

        interface IObservable with
            member x.Subscribe f = 
                let mutable first = true
                inner |> Mod.unsafeRegisterCallbackKeepDisposable (fun v -> 
                    if first then first <- false
                    else v :> obj |> f
                )

    type private T<'a>(inner : System.Threading.Tasks.Task<'a>) =
        member private x.Inner = inner

        override x.GetHashCode() = inner.GetHashCode()
        override x.Equals o =
            match o with
                | :? T<'a> as o -> inner = o.Inner 
                | _ -> false

        interface IObservable with
            member x.Subscribe f = 
                let mutable live = true
                let c (t : System.Threading.Tasks.Task<'a>) =
                    if live then 
                        f (t.Result :> obj)

                inner.ContinueWith(System.Action<_>(c)) |> ignore
                { new IDisposable with member x.Dispose() = live <- false }

    type private TimePattern private() =
        inherit Pattern<Time>()

        static let instance = TimePattern() :> Pattern<Time>
        static member Instance = instance
        
        override x.DependsOnTime = true
        override x.Relevant = PersistentHashSet.empty
        override x.Match(s,source,_) = 
            if isNull source then Some s.time
            else None

    type private AnyPattern private() =
        inherit Pattern<unit>()

        static let instance = AnyPattern() :> Pattern<unit>
        static member Instance = instance

        override x.DependsOnTime = false
        override x.Relevant = PersistentHashSet.empty
        override x.Match(_,_,_) = Some ()

    let time = TimePattern.Instance

    let never<'a> = NeverPattern<'a>.Instance

    let next (source : IObservable<'a>) =
        let source = Obs(source) :> IObservable
        let set = PersistentHashSet.singleton source
        { new Pattern<'a>() with
            member x.DependsOnTime = false
            member x.Relevant = set
            member x.Match(state, s, v) =
                match v with
                    | :? 'a as v when Object.Equals(source,s) ->
                        Some v
                    | _ -> 
                        None
        }

    let ofMod (source : IMod<'a>) =
        let source = M(source) :> IObservable
        let set = PersistentHashSet.singleton source
        { new Pattern<'a>() with
            member x.DependsOnTime = false
            member x.Relevant = set
            member x.Match(state, s, v) =
                match v with
                    | :? 'a as v when Object.Equals(source,s) ->
                        Some v
                    | _ -> 
                        None
        }

    let ofTask (t : System.Threading.Tasks.Task<'a>) =
        let source = T(t) :> IObservable
        let set = PersistentHashSet.singleton source
        { new Pattern<'a>() with
            member x.DependsOnTime = false
            member x.Relevant = set
            member x.Match(state, s, v) =
                match v with
                    | :? 'a as v when Object.Equals(source,s) ->
                        Some v
                    | _ -> 
                        None
        }

    let rec map (f : 'a -> 'b) (m : Pattern<'a>) =
        { new Pattern<'b>() with
            member x.DependsOnTime = m.DependsOnTime
            member x.Relevant = m.Relevant
            member x.Match(state, source, value) =
                match m.Match(state, source, value) with
                    | Some v -> Some (f v)
                    | None -> None
        }

    let rec map' (f : obj -> 'b) (m : Pattern) =
        { new Pattern<'b>() with
            member x.DependsOnTime = m.DependsOnTime
            member x.Relevant = m.Relevant
            member x.Match(state, source, value) =
                match m.MatchUntyped(state, source, value) with
                    | Some v -> Some (f v)
                    | None -> None
        }

    let rec any (patterns : list<Pattern<'a>>) =
        let dt = patterns |> List.exists (fun p -> p.DependsOnTime)
        let relevant = patterns |> List.map (fun p -> p.Relevant) |> PersistentHashSet.unionMany
        { new Pattern<'a>() with
            member x.DependsOnTime = dt
            member x.Relevant = relevant

            member x.Match(state, source, value) =
                let rec run (ps : list<Pattern<'a>>) =
                    match ps with
                        | [] -> None
                        | p :: rest ->
                            match p.Match(state, source, value) with
                                | Some v -> Some v
                                | None -> run rest

                run patterns
        }       

    let choice (l : Pattern<'a>) (r : Pattern<'b>) =
        any [
            l |> map Choice1Of2
            r |> map Choice2Of2
        ]

    let choice' (l : Pattern) (r : Pattern) =
        any [
            l |> map' Choice1Of2
            r |> map' Choice2Of2
        ]

    let par (l : Pattern) (r : Pattern) =
        let dt = l.DependsOnTime || r.DependsOnTime
        let rel = PersistentHashSet.union l.Relevant r.Relevant
        { new Pattern<Option<obj> * Option<obj>>() with
            member x.DependsOnTime = dt
            member x.Relevant = rel
            member x.Match(state, source, value) =
                match l.MatchUntyped(state, source, value), r.MatchUntyped(state, source, value) with
                    | None, None -> None
                    | l, r -> Some (l, r)
        }


    let all (ps : list<Pattern>) : Pattern<list<Option<obj>>> =
        match ps with
            | [] -> AnyPattern.Instance |> map (fun _ -> [])
            | [p] -> p |> map' (fun v -> [Some v])
            | many ->
                let rel = many |> List.map (fun p -> p.Relevant) |> PersistentHashSet.unionMany
                let dt = many |> List.exists (fun p -> p.DependsOnTime)

                { new Pattern<list<Option<obj>>>() with
                    member x.DependsOnTime = dt
                    member x.Relevant = rel
                    member x.Match(state, source, value) =
                        let res = many |> List.map (fun p -> p.MatchUntyped(state, source, value))
                        if List.exists Option.isSome res then
                            Some res
                        else
                            None
                }

    let rec any' (patterns : list<Pattern>) =
        let dt = patterns |> List.exists (fun p -> p.DependsOnTime)
        let relevant = patterns |> List.map (fun p -> p.Relevant) |> PersistentHashSet.unionMany
        { new Pattern<int * obj>() with
            member x.DependsOnTime = dt
            member x.Relevant = relevant

            member x.Match(state, source, value) =
                let rec run (i : int) (ps : list<Pattern>) =
                    match ps with
                        | [] -> None
                        | p :: rest ->
                            match p.MatchUntyped(state, source, value) with
                                | Some v -> Some (i,v)
                                | None -> run (i + 1) rest

                run 0 patterns
        }       


[<AbstractClass>]
type Event<'a>() = 
    abstract member run : ProcState<'s> -> EventResult<'a>

and EventResult<'a> =
    | EFinished of 'a
    | EContinue of Pattern * (obj -> Event<'a>)

and Proc<'s, 'a> = { run : State<ProcState<'s>, ProcResult<'s, 'a>> } with
    member x.Filter(a : 'a -> bool) =
        { run =
            state {
                let! r = x.run
                match r with
                    | Finished v ->
                        if a v then return Finished a
                        else return! x.Filter(a).run
                    | Continue(p, c) ->
                        return Continue(p, fun o -> c(o).Filter a)
            }
        }

and ProcResult<'s, 'a> =
    | Finished of 'a
    | Continue of Pattern * (obj -> Proc<'s, 'a>)

and Assertion<'s, 'a> =
    private
        | That of list<Proc<'s, 'a>>

type Value<'s, 'a> = { current : 'a; next : Option<Proc<'s, Value<'s, 'a>>> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Event =


    let private ofResult (r : EventResult<'a>) =
        { new Event<'a>() with
            member x.run s = r 
        }

    let value (v : 'a) =
        { new Event<'a>() with
            member x.run s = EFinished v
        }

    let ofObservable (o : IObservable<'a>) =
        let u = Pattern.next o
        { new Event<'a>() with
            member x.run s =
                EContinue(u, fun v -> value (unbox<'a> v))
        }

    let rec map (f : 'a -> 'b) (a : Event<'a>) =
        { new Event<'b>() with
            member x.run s = 
                match a.run s with
                    | EFinished v -> EFinished (f v)
                    | EContinue(p,cont) -> EContinue(p, cont >> map f)
        }

    let rec bind (f : 'a -> Event<'b>) (m : Event<'a>) =
        { new Event<'b>() with
            member x.run(s) =
                match m.run(s) with
                    | EFinished v -> f(v).run(s)
                    | EContinue(p,c) -> EContinue(p, c >> bind f)
        }

    let rec choose (f : 'a -> Option<'b>) (a : Event<'a>) =
        a |> bind (fun v -> match f v with | Some v -> value v | None -> choose f a)

    let rec filter (f : 'a -> bool) (a : Event<'a>) =
        a |> bind (fun v -> if f v then value v else filter f a)

    let ignore (m : Event<'a>) =
        m |> map ignore

    let rec any (es : list<Event<'a>>) =
        { new Event<'a>() with
            member x.run s =
                let rec run (p : list<Event<'a>>) =
                    match p with
                        | [] -> Choice2Of2 []
                        | head :: rest ->
                            let r = head.run s
                            match r with
                                | EFinished v -> Choice1Of2 v
                                | EContinue(p,c) ->
                                    let rest = run rest
                                    match rest with
                                        | Choice1Of2 v -> Choice1Of2 v
                                        | Choice2Of2 ps -> Choice2Of2 ((r,p,c)::ps)

                                             
                let res = run es
                match res with
                    | Choice1Of2 v -> 
                        EFinished v

                    | Choice2Of2 ps ->
                        let p = Pattern.any' (List.map (fun (_,p,_) -> p) ps)
                        EContinue(p, fun res ->
                            let idx,m = unbox<int * obj> res

                            let next = 
                                ps |> List.mapi (fun i (r,p,c) ->
                                    if i = idx then c m
                                    else ofResult r
                                )

                            any next
                        )

        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Proc =

    let private ofResult (r : ProcResult<'s, 'a>) =
        { run = State.value r }

    let internal fix (f : ref<'a> -> 'a) : 'a =
        let r = ref Unchecked.defaultof<'a>
        r := f r
        !r

    /// creates a proc returning the given value immediately
    let value (v : 'a) =
       { run = State.value (Finished v) }

    /// a proc never returning a value
    let never<'s, 'a> : Proc<'s, 'a> =
        { run = State.value (Continue(Pattern.never, fun o -> failwith ""))}

    /// creates a proc using the given event
    let rec ofEvent (e : Event<'a>) : Proc<'s, 'a> =
        { run =
            state {
                let! s = State.get
                match e.run s with
                    | EFinished v -> return Finished v
                    | EContinue(p, c) -> return Continue(p, c >> ofEvent)
            }
        }

    let rec ofTask (e : System.Threading.Tasks.Task<'a>) : Proc<'s, 'a> =
        { run =
            state {
                let! s = State.get
                return Continue(Pattern.ofTask e, fun o -> value (unbox<'a> o))
            }
        }

    /// a proc returning the current time immediately
    let now<'s> : Proc<'s, Time> =
        { run = State.get |> State.map (fun s -> Finished s.time) }

    /// a proc waiting for the next time-step
    let nextTime<'s> : Proc<'s,Time> =
        { run = State.value ( Continue( Pattern.time, fun _ -> now) ) }

    /// a proc waiting for the next time-step and returning the elapsed time
    let dt<'s> : Proc<'s, MicroTime> =   
        { run = 
            let getTime = State.get |> State.map (fun s -> s.time)
            state {
                let! t = getTime
                return Continue(Pattern.time, fun o -> { run = getTime |> State.map (fun n -> n - t |> Finished) })
            }
        } 

    /// applies a computation to the result of a proc
    let rec map (f : 'a -> 'b) (m : Proc<'s, 'a>) =
        { run =
            state {
                let! v = m.run
                match v with
                    | Finished v -> return v |> f |> Finished
                    | Continue(p,c) -> return Continue(p, c >> map f)
            }
        }

    /// applies a monadic computation to the result of a proc
    let rec bind (f : 'a -> Proc<'s, 'b>) (m : Proc<'s, 'a>) =
        { run =
            state {
                let! r = m.run
                match r with
                    | Finished v -> return! f(v).run
                    | Continue(p,cont) -> return Continue(p, cont >> bind f)
            }
        }


    /// returns the value of the first proc being finished
    /// NOTE: when multiple procs are finished it returns the first one in list-order
    let rec any (ls : list<Proc<'s, 'a>>) =
        match ls with
            | [] -> never
            | [p] -> p
            | _ -> 
                let rec run (p : list<Proc<'s, 'a>>) =
                    state {
                        match p with
                            | [] -> return Choice2Of2 []
                            | head :: rest ->
                                let! r = head.run
                                match r with
                                    | Finished v -> return Choice1Of2 v
                                    | Continue(p,c) ->
                                        let! rest = run rest
                                        match rest with
                                            | Choice1Of2 v -> return Choice1Of2 v
                                            | Choice2Of2 ps -> return Choice2Of2 ((r,p,c)::ps)
                                             
                    }

                { run =
                    state {
                        let! res = run ls
                        match res with
                            | Choice1Of2 v -> 
                                return Finished v

                            | Choice2Of2 ps ->
                                let p = Pattern.any' (List.map (fun (_,p,_) -> p) ps)
                                return Continue(p, fun res ->
                                    let idx,m = unbox<int * obj> res

                                    let next = 
                                        ps |> List.mapi (fun i (r,p,c) ->
                                            if i = idx then c m
                                            else ofResult r
                                        )

                                    any next
                                )
                    }
                }

    /// a proc which is done when all the given ones are too.
    let rec par (l : list<Proc<'s, unit>>) =
        match l with
            | [] -> value ()
            | [p] -> p
            | _ -> 
                { run =
                    state {
                        let! conts = 
                            l |> List.chooseS (fun p -> 
                                state {
                                    let! r = p.run
                                    match r with
                                        | Finished () -> return None
                                        | Continue(p,c) -> return Some(r,p,c)
                                }
                            )

                        match conts with
                            | [] -> 
                                return Finished ()

                            | [(_,p,c)] ->
                                return Continue(p,c)

                            | many ->
                                let p = Pattern.all (List.map (fun (_,p,_) -> p) conts)
                                return Continue(p, fun o ->
                                    let matches = unbox<list<Option<obj>>> o
                                    List.map2 (fun (r,_,c) m -> match m with | Some v -> c v | None -> ofResult r) conts matches
                                        |> par
                                )

                    }
                }

    /// a proc returning the first value being ready
    let choice (l : Proc<'s, 'a>) (r : Proc<'s, 'b>) =
        any [ map Choice1Of2 l; map Choice2Of2 r ]

    /// a proc waiting for l to exit and then starting r
    let rec append (l : Proc<'s, unit>) (r : Proc<'s, 'a>) =
        { run =
            state {
                let! res = l.run
                match res with
                    | Finished v -> return! r.run
                    | Continue(p,cont) -> return Continue(p, fun o -> append (cont o) r)
            }
        }

    let repeatWhile (guard : Proc<'s, bool>) (body : Proc<'s, unit>) =
        fix (fun self ->
            guard |> bind (fun v -> if v then append body !self else value ())
        )

    let delay (f : unit -> Proc<'s, 'a>) =
        { run =
            state {
                return! f().run
            }
        }

    let foreach (elements : Proc<'s, 'a>) (body : 'a -> Proc<'s, unit>) : Proc<'s, unit> =
        fix (fun self ->
            append (bind body elements) (delay (fun () -> !self))
        )


    let repeat (inner : Proc<'s, unit>) =
        fix (fun self ->
            append inner (delay (fun () -> !self))
        )

    let choose (f : 'a -> Option<'b>) (m : Proc<'s, 'a>) =
        fix (fun self ->
            m |> bind (fun v -> match f v with | Some v -> value v | None -> !self)
        )

    let filter (f : 'a -> bool) (m : Proc<'s, 'a>) =
        fix (fun self ->
            m |> bind (fun v -> if f v then value v else !self)
        )

    type private Runner<'s>(adjustTime : Time -> Time, state : 's, proc : Proc<'s, unit>) as this =
        inherit Mod.AbstractMod<'s>()

        let subscriptions = Dictionary<IObservable, IDisposable>()

        let mutable pending : Option<Pattern * (obj -> Proc<'s, unit>)> = None
        let queue = Queue<IObservable * obj * Time>()
        let mutable state = { time = Time.Now; userState = state }
        let mutable dependsOnTime = false

        let push (source : IObservable) (value : obj) =
            lock this (fun () -> queue.Enqueue(source, value, Time.Now))
            transact (fun () -> this.MarkOutdated())

        let processEvent (source : IObservable) (value : obj) =
            match pending with
                | Some (p,cont) ->
                    let mutable dt = false
                    let mutable newPending = None
                    match p.MatchUntyped(state,source,value) with
                        | Some v -> 

                            let c = cont v
                            match c.run.Run(&state) with
                                | Continue(p,c) ->
                                    newPending <- Some (p,c)
                                    dt <- dt || p.DependsOnTime
                                | _ ->
                                    ()
                        | None ->
                            newPending <- Some (p,cont)
                            dt <- dt || p.DependsOnTime



                    let added, removed =
                        match pending, newPending with
                            | Some (o,_), Some (n,_) ->
                                let added = n.Relevant |> Seq.filter (fun v -> PersistentHashSet.contains v o.Relevant |> not) |> Seq.toList
                                let removed = o.Relevant |> Seq.filter (fun v -> PersistentHashSet.contains v n.Relevant |> not) |> Seq.toList
                                added, removed

                            | None, Some (n,_) ->
                                Seq.toList n.Relevant, []

                            | Some (o,_), None ->
                                [], Seq.toList o.Relevant

                            | None, None ->
                                [], []

                    for a in added do
                        subscriptions.[a] <- a.Subscribe(fun o -> push a o)

                    for r in removed do
                        match subscriptions.TryGetValue r with
                            | (true, s) -> 
                                subscriptions.Remove r |> ignore
                                s.Dispose()
                            | _ -> ()

                    pending <- newPending
                    dependsOnTime <- dt

                | None -> 
                    dependsOnTime <- false
                    subscriptions.Values |> Seq.iter (fun d -> d.Dispose())
                    subscriptions.Clear()
                    queue.Clear()

        let setTime (t : Time) =
            if t > state.time then
                state <- { state with time = t }
                if dependsOnTime then processEvent null null

        let step (tTarget : Time) =
            while queue.Count > 0 do
                let (s,e,t) = queue.Dequeue()
                setTime t
                processEvent s e

            setTime tTarget
                    
        do match proc.run.Run(&state) with
            | Finished _ -> ()
            | Continue(p, cont) ->
                pending <- Some (p,cont)

                if p.DependsOnTime then
                    dependsOnTime <- true

                for r in p.Relevant do
                    if not (subscriptions.ContainsKey r) then
                        subscriptions.[r] <- r.Subscribe(fun o -> push r o)

//        open System
//        let run () =
//            let sw = System.Diagnostics.Stopwatch()
//            let rec run (cnt : int) (current : TimeSpan) =
//                let s = sw.Elapsed
//                if s <> current then
//                    if cnt < 100 then
//                        printfn "%.3fµs" (float (s.Ticks - current.Ticks) / 10.0)
//                        run (cnt + 1) s
//                else
//                    run cnt current
//            System.Threading.Tasks.Task.Factory.StartNew (fun () -> run 0 TimeSpan.Zero) |> ignore
//            sw.Start()

        override x.Compute() =
            let now = adjustTime Time.Now
            step now
            
            if dependsOnTime then
                AdaptiveObject.Time.Outputs.Add x |> ignore
            
            state.userState

    let toMod (adjustTime : Time -> Time) (s : 's) (p : Proc<'s,unit>) =
        Runner<'s>(adjustTime, s, p) :> IMod<_>

    let inline ignore (m : Proc<'s, 'a>) =
        map ignore m

//
//    let rec bindValue' (f : 'a -> Proc<'s, unit>) (m : Value<'s, 'a>) : Value<'s, 'x> =
//        let current = m.current |> f
//        choice current m.next |> bind (fun r ->
//            match r with
//                | Choice1Of2 () -> m.next |> bind (fun v -> bindValue f v)
//                | Choice2Of2 v -> bindValue f v
//        )

    let nextValue (m : IMod<'a>) : Proc<'s, 'a> =
        { run =
            state {
                return Continue(Pattern.ofMod m, fun v -> v |> unbox<'a> |> value)
            }
        }

    let rec bindValue (f : 'a -> Proc<'s, 'b>) (m : Value<'s, 'a>) : Proc<'s, 'b> =
        let current = m.current |> f
        match m.next with
            | None -> current
            | Some n ->
                choice current n |> bind (fun r ->
                    match r with
                        | Choice1Of2 _ -> n |> bind (fun v -> bindValue f v)
                        | Choice2Of2 v -> bindValue f v
                )

    let rec bindMod (f : 'a -> Proc<'s, unit>) (m : IMod<'a>) : Proc<'s, 'b> =
        let current = m |> Mod.force |> f
        choice current (nextValue m) |> bind (fun r ->
            match r with
                | Choice1Of2 () -> (nextValue m) |> bind (fun v -> bindMod f m)
                | Choice2Of2 v -> bindMod f m
        )

    let rec fold (f : 'x -> 'a -> 'x) (initial : 'x) (m : Proc<'s, 'a>) : Value<'s, 'x> =
        { 
            current = initial
            next = m |> map (fun v -> (fold f (f initial v) m)) |> Some
        }



[<AutoOpen>]
module ``Proc Builders`` =

    let inline (.=) a b = a |> Proc.filter (fun a -> a = b)
    let inline (.<>) a b = a |> Proc.filter (fun a -> a <> b)
    let inline (.>) a b = a |> Proc.filter (fun a -> a > b)
    let inline (.<) a b = a |> Proc.filter (fun a -> a < b)
    let inline (.>=) a b = a |> Proc.filter (fun a -> a >= b)
    let inline (.<=) a b = a |> Proc.filter (fun a -> a >= b)

    type ProcBuilder<'s, 'a, 'r> = private { build : Proc<'s, 'r> -> Proc<'s, 'a> }

    type NewProcBuilder() =
        let lift (f : 'a -> 'b) (m : State<'s, 'a>) =
            { new State<ProcState<'s>, 'b>() with
                member x.Run(state) =
                    let mutable u = state.userState
                    let res = m.Run(&u)
                    state <- { state with userState = u }
                    let v = f res
                    v
            }

        member x.Bind(m : Proc<'s, 'a>, f : 'a -> ProcBuilder<'s, 'b, 'r>) =
            { build = fun self ->
                m |> Proc.bind (fun v -> f(v).build self)
            }

        member x.Bind(m : System.Threading.Tasks.Task<'a>, f : 'a -> ProcBuilder<'s, 'b, 'r>) =
            { build = fun self ->
                m |> Proc.ofTask |> Proc.bind (fun v -> f(v).build self)
            }

        member x.Bind(m : ProcBuilder<'s, 'a, 'r>, f : 'a -> ProcBuilder<'s, 'b, 'r>) =
            { build = fun self ->
                m.build self |> Proc.bind (fun v -> f(v).build self)
            }

        member x.Bind(m : Event<'a>, f : 'a -> ProcBuilder<'s, 'b, 'r>) =
            { build = fun self ->
                m |> Proc.ofEvent |> Proc.bind (fun v -> f(v).build self)
            }

        member x.Bind(m : Value<'s, 'a>, f : 'a -> ProcBuilder<'s, unit, 'r>) =
            { build = fun self ->
                m |> Proc.bindValue (fun v -> f(v).build self)
            }

        member x.Bind(m : IMod<'a>, f : 'a -> ProcBuilder<'s, unit, 'r>) =
            { build = fun self ->
                m |> Proc.bindMod (fun v -> f(v).build self)
            }

        member x.Bind(m : State<'s, 'a>, f : 'a -> ProcBuilder<'s, 'b, 'r>) =
            { build = fun self ->
                { run = m |> lift Finished } |> Proc.bind (fun v -> f(v).build self)
            }

        member x.Bind(m : 's -> 's, f : unit -> ProcBuilder<'s, 'b, 'r>) =
            { build = fun self ->
                { run = m |> State.modify |> lift Finished } |> Proc.bind (fun v -> f(v).build self)
            }

        member x.Return(v : 'a) =
            { build = fun self ->
                Proc.value v
            }

        member x.ReturnFrom(sf : Proc<'s, 'a>) =
            { build = fun self -> sf }

        member x.ReturnFrom(sf : ProcBuilder<'s, 'r, 'r>) =
            { build = fun self -> sf.build self }


        member x.Run(b : ProcBuilder<'s, 'a, 'a>) =
            Proc.fix (fun self ->
                b.build (Proc.delay (fun () -> !self))
            )

        member x.TryWith(m : Assertion<'s,'x> * ProcBuilder<'s, 'a, 'r>, comp : 'x -> ProcBuilder<'s, 'a, 'r>) : ProcBuilder<'s, 'a, 'r> =
            { build = fun self ->
                let (That assertions), body = m

                let guard = 
                    match assertions with
                        | [a] -> a
                        | _ -> assertions |> Proc.any

                Proc.choice guard (body.build self) |> Proc.bind (fun v ->
                    match v with
                        | Choice1Of2 v -> comp(v).build self
                        | Choice2Of2 v -> Proc.value v
                )
            }

        member x.Bind(b : Assertion<'s, 'x>, m : unit -> ProcBuilder<'s, 'a, 'r>) =
            b, m()

        member x.Delay(f : unit -> Assertion<'s, 'x> * ProcBuilder<'s, 'a, 'r>) = f()

        member x.Delay(f : unit -> ProcBuilder<'s, 'a, 'r>) =
            { build = fun self ->
                { run = state { return! f().build(self).run } }
            }

        member x.For(m : seq<'a>, f : 'a -> ProcBuilder<'s, unit, 'r>) : ProcBuilder<'s, unit, 'r> =
            { build = fun self ->
                m |> Seq.map (fun v -> (f v).build self) |> Seq.fold Proc.append (Proc.value())
            }

        member x.For(m : Proc<'s, 'a>, f : 'a -> ProcBuilder<'s, unit, 'r>) : ProcBuilder<'s, unit, 'r> =
            { build = fun self -> Proc.foreach m (fun v -> f(v).build self) }

        member x.For(m : Event<'a>, f : 'a -> ProcBuilder<'s, unit, 'r>) : ProcBuilder<'s, unit, 'r> =
            { build = fun self -> Proc.foreach (Proc.ofEvent m) (fun v -> f(v).build self) }


        member x.While(guard : unit -> Proc<'s, bool>, body : ProcBuilder<'s, unit, 'r>) =
            { build = fun self -> Proc.repeatWhile (guard()) (body.build self) }

        member x.While(guard : unit -> bool, body : ProcBuilder<'s, unit, 'r>) =
            x.While((fun () -> { run = state { return guard() |> Finished } }), body)

        member x.While(guard : unit -> Event<bool>, body : ProcBuilder<'s, unit, 'r>) =
            x.While((fun () -> Proc.ofEvent (guard())), body)



        member x.Combine(l : ProcBuilder<'s, unit, 'r>, r : ProcBuilder<'s, 'a, 'r>) =
            { build = fun self ->
                Proc.append (l.build self) (r.build self)
            }

        member x.Zero() = { build = fun self -> Proc.value () }

    let self = { build = fun self -> self }


    type ProcBuilder() =
        let lift (f : 'a -> 'b) (m : State<'s, 'a>) =
            { new State<ProcState<'s>, 'b>() with
                member x.Run(state) =
                    let mutable u = state.userState
                    let res = m.Run(&u)
                    state <- { state with userState = u }
                    let v = f res
                    v
            }

        member x.Bind(m : Event<'a>, f : 'a -> Proc<'s, 'b>) =
            Proc.bind f (Proc.ofEvent m)

        member x.Bind(m : Proc<'s, 'a>, f : 'a -> Proc<'s, 'b>) =
            Proc.bind f m

        member x.Bind(m : Value<'s, 'a>, f : 'a -> Proc<'s, unit>) =
            Proc.bindValue f m

        member x.Bind(m : IMod<'a>, f : 'a -> Proc<'s, unit>) =
            Proc.bindMod f m


        member x.Bind(m : State<'s, 'a>, f : 'a -> Proc<'s, 'b>) =
            Proc.bind f { run = m |> lift Finished }

        member x.Bind(m : 's -> 's, f : unit -> Proc<'s, 'b>) =
            Proc.bind f { run = m |> State.modify |> lift Finished }

        member x.Return(v : 'a) = 
            Proc.value v

        member x.ReturnFrom(sf : Proc<'s, 'a>) =
            sf
//
//        member x.ReturnFrom(sf : Event<'a>) =
//            Proc.ofEvent sf
//
//        member x.ReturnFrom(s : State<'s, 'a>) =
//            { run = s |> lift Finished }

        member x.TryWith(m : Assertion<'s,'x> * Proc<'s, 'a>, comp : 'x -> Proc<'s, 'a>) : Proc<'s, 'a> =
            let (That assertions), body = m

            let guard = 
                match assertions with
                    | [a] -> a
                    | _ -> assertions |> Proc.any

            Proc.choice guard body |> Proc.bind (fun v ->
                match v with
                    | Choice1Of2 v -> comp v
                    | Choice2Of2 v -> Proc.value v
            )

        member x.Bind(b : Assertion<'s, 'x>, m : unit -> Proc<'s, 'a>) =
            b, m()

        member x.Delay(f : unit -> Assertion<'s, 'x> * Proc<'s, 'a>) = f()

        member x.For(m : Proc<'s, 'a>, f : 'a -> Proc<'s, unit>) : Proc<'s, unit> =
            Proc.foreach m f

        member x.For(m : Event<'a>, f : 'a -> Proc<'s, unit>) : Proc<'s, unit> =
            Proc.foreach (Proc.ofEvent m) f

        member x.While(guard : unit -> Proc<'s, bool>, body : Proc<'s, unit>) =
            Proc.repeatWhile (guard()) body

        member x.While(guard : unit -> bool, body : Proc<'s, unit>) =
            Proc.repeatWhile { run = state { return guard() |> Finished } } body

        member x.While(guard : unit -> Event<bool>, body : Proc<'s, unit>) =
            Proc.repeatWhile (Proc.ofEvent (guard())) body


        member x.Delay(f : unit -> Proc<'s, 'a>) =
            { run =
                state {
                    return! f().run
                }
            }


        member x.Combine(l : Proc<'s, unit>, r : Proc<'s, 'a>) =
            Proc.append l r

        member x.Zero() = Proc.value ()

    let proc = NewProcBuilder()

    let until (v : list<Proc<'s, 'a>>) = That v

    let asdasd (v : Proc<'x, int>) (move : Event<V2i>) =
        proc {
            try
                do! until [ v ]

                for m in move do
                    printfn "%A" m

                return 1
            with s ->
                return 2
        }


[<AutoOpen>]
module ``Extendend Proc Stuff`` =

    type ProcStartStopBuilder<'s>(run : Proc<'s, unit> -> Proc<'s, unit>) =
        inherit ProcBuilder()

        member x.Run(m : Proc<'s, unit>) =
            run m
        

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Proc =
        let startStop (start : Proc<'s, _>) (stop : Proc<'s, _>) =
            ProcStartStopBuilder<'s>(fun body ->
                start |> Proc.bind (fun _ ->
                    Proc.fix (fun self ->
                        Proc.choice stop body |> Proc.bind (fun r ->
                            match r with
                                | Choice1Of2 v -> Proc.value ()
                                | Choice2Of2 () -> !self
                        )
                    )
                ) |> Proc.repeat
            )


        let fix (body : Proc<'s, 'a> -> Proc<'s, 'a>) : Proc<'s, 'a> =
            let r = ref Unchecked.defaultof<_>
            let self = Proc.delay (fun () -> !r)
            r := body self
            !r


open Aardvark.Base.Incremental

type ILoadTask<'a> =
    inherit IMod<Option<'a>>
    abstract member IsRunning : bool
    abstract member Start : unit -> unit
    abstract member Stop : unit -> unit

module Load =
    open System.IO
    open System.Threading
    open System.Reactive.Subjects

    type LoadState =
        {
            undo : list<unit -> unit>
        }

    type Load<'a> = { run : Proc<LoadState, 'a> }

    module Load =
        let map (f : 'a -> 'b) (m : Load<'a>) =
            { run =
                proc {
                    let! r = m.run
                    return f r
                }
            }

        let onCancel (f : unit -> unit) : Load<unit> =
            { run =
                proc {
                    do! State.modify (fun s -> { s with undo = f :: s.undo})    
                }
            }

        let disposeOnCancel<'a when 'a :> IDisposable> (d : 'a) =
            onCancel (fun () -> (d :> IDisposable).Dispose())

        let ofTask (t : System.Threading.Tasks.Task<'a>) =
            { run = Proc.ofTask t }

        let bind (f : 'a -> Load<'b>) (m : Load<'a>) =
            { run = Proc.bind (fun v -> (f v).run) m.run }

        let append (l : Load<unit>) (r : Load<'a>) =
            { run = Proc.append l.run r.run }

        let value (a : 'a) =
            { run = Proc.value a }



        type private IRunner =
            abstract member Step : Time -> bool
        type private Runner<'a>(initialState : LoadState, proc : Proc<LoadState, 'a>, dirty : unit -> unit) as this =
            inherit Mod.AbstractMod<Option<'a>>()

            let subscriptions = Dictionary<IObservable, IDisposable>()
            let queue = Queue<IObservable * obj * Time>()
            let mutable pending : Option<Pattern * (obj -> Proc<LoadState, 'a>)> = None
            let mutable state = { time = Time.Now; userState = initialState }
            let mutable dependsOnTime = false
            let mutable result = None
            let mutable isRunning = false

            let push (source : IObservable) (value : obj) =
                lock this (fun () -> 
                    queue.Enqueue(source, value, Time.Now)
                    dirty()
                )

            let processEvent (source : IObservable) (value : obj) =
                match pending with
                    | Some (p,cont) ->
                        let mutable dt = false
                        let mutable newPending = None

                        let finished = 
                            match p.MatchUntyped(state,source,value) with
                                | Some v -> 
                                    let c = cont v
                                    match c.run.Run(&state) with
                                        | Continue(p,c) ->
                                            newPending <- Some (p,c)
                                            dt <- dt || p.DependsOnTime
                                            false
                                        | Finished v ->
                                            result <- Some v
                                            transact (fun () -> this.MarkOutdated())
                                            true
                                | None ->
                                    newPending <- Some (p,cont)
                                    dt <- dt || p.DependsOnTime
                                    false

                        if finished then
                            dependsOnTime <- false
                            subscriptions.Values |> Seq.iter (fun d -> d.Dispose())
                            subscriptions.Clear()
                            queue.Clear()

                            true

                        else 
                            let added, removed =
                                match pending, newPending with
                                    | Some (o,_), Some (n,_) ->
                                        let added = n.Relevant |> Seq.filter (fun v -> PersistentHashSet.contains v o.Relevant |> not) |> Seq.toList
                                        let removed = o.Relevant |> Seq.filter (fun v -> PersistentHashSet.contains v n.Relevant |> not) |> Seq.toList
                                        added, removed

                                    | None, Some (n,_) ->
                                        Seq.toList n.Relevant, []

                                    | Some (o,_), None ->
                                        [], Seq.toList o.Relevant

                                    | None, None ->
                                        [], []

                            for a in added do
                                subscriptions.[a] <- a.Subscribe(fun o -> push a o)

                            for r in removed do
                                match subscriptions.TryGetValue r with
                                    | (true, s) -> 
                                        subscriptions.Remove r |> ignore
                                        s.Dispose()
                                    | _ -> ()

                            pending <- newPending
                            dependsOnTime <- dt
                            false

                    | None -> 
                        dependsOnTime <- false
                        subscriptions.Values |> Seq.iter (fun d -> d.Dispose())
                        subscriptions.Clear()
                        queue.Clear()
                        true

            let setTime (t : Time) =
                if t > state.time then
                    state <- { state with time = t }
                    if dependsOnTime then processEvent null null
                    else false
                else
                    false

            let step (tTarget : Time) =
                let mutable finished = false
                while queue.Count > 0 do
                    let (s,e,t) = queue.Dequeue()
                    finished <- finished || setTime t
                    finished <- finished || processEvent s e

                finished || setTime tTarget
                    
            let kill() =
                isRunning <- false
                dependsOnTime <- false
                subscriptions.Values |> Seq.iter (fun d -> d.Dispose())
                subscriptions.Clear()
                queue.Clear()
                result <- None 
                for u in state.userState.undo do u()
                state <- { time = Time.Now; userState = initialState }


            member x.Start () =
                lock x (fun () ->
                    if not isRunning then
                        isRunning <- true
                        match proc.run.Run(&state) with
                            | Finished v ->
                                result <- Some v
                                transact (fun () -> x.MarkOutdated())

                            | Continue(p, cont) ->
                                pending <- Some (p,cont)

                                if p.DependsOnTime then
                                    dependsOnTime <- true

                                for r in p.Relevant do
                                    if not (subscriptions.ContainsKey r) then
                                        subscriptions.[r] <- r.Subscribe(fun o -> push r o)
                )

            member x.Stop() =
                lock x (fun () ->
                    if isRunning then
                        isRunning <- false
                        kill()
                )

            member x.Step(t : Time) =
                lock x (fun () ->
                    if isRunning then
                        step t
                    else
                        true
                )

            member x.IsRunning = 
                isRunning

            override x.Compute() =
                result

            interface IRunner with
                member x.Step t = x.Step t

            interface ILoadTask<'a> with
                member x.Start() = x.Start()
                member x.Stop() = x.Stop()
                member x.IsRunning = x.IsRunning

        type Runner(threads : int) =
            let dirtyRunners = ConcurrentHashQueue<IRunner>()
            let sem = new SemaphoreSlim(0)
            let cancel = new CancellationTokenSource()

            [<VolatileField>]
            let mutable remaining = threads

            let mainLoop =
                let token = cancel.Token
                async {
                    try
                        do! Async.SwitchToNewThread()
                        while true do
                            sem.Wait(token)
                            match dirtyRunners.TryDequeue() with
                                | (true, r) ->
                                    r.Step(Time.Now) |> ignore
                                | _ ->
                                    ()
                    with :? OperationCanceledException ->
                        Interlocked.Decrement(&remaining) |> ignore
                }

            do for i in 1 .. threads do Async.Start mainLoop

            new() = new Runner(1)


            member x.Enqueue (p : Load<'a>) : ILoadTask<'a> =
                let mutable self = Unchecked.defaultof<_>
                let dirty() =
                    if dirtyRunners.Enqueue (self :> IRunner) then
                        sem.Release() |> ignore

                self <- Runner<'a>({ undo = [] }, p.run, dirty)
                self :> ILoadTask<'a>

            member x.Dispose() =
                cancel.Cancel()
                while remaining > 0 do ()
                sem.Dispose()
                cancel.Dispose()
                dirtyRunners.Clear()

            interface IDisposable with
                member x.Dispose() = x.Dispose()

    [<AutoOpen>]
    module ``Load Builder`` =
        type LoadBuilder() =

            member x.Using<'a, 'b when 'a :> IDisposable> (v : 'a, f : 'a -> Load<'b>) : Load<'b> =
                let c = Load.disposeOnCancel v
                let body =
                    v |> f |> Load.map(fun r ->
                        (v :> IDisposable).Dispose()
                        r
                    )

                Load.append c body


            member x.Bind(m : System.Threading.Tasks.Task<'a>, f : 'a -> Load<'b>) =
                Load.bind f (Load.ofTask m)

            member x.Bind(m : Async<'a>, f : 'a -> Load<'b>) =
                Load.bind f (Load.ofTask (Async.StartAsTask m))

            member x.Bind(m : Load<'a>, f : 'a -> Load<'b>) =
                Load.bind f m

            member x.Return(v : 'a) = 
                Load.value v

            member x.Combine(l : Load<unit>, r : Load<'a>) =
                { run =
                    Proc.append l.run r.run
                }

            member x.Delay(f : unit -> Load<'a>) =
                { run = Proc.delay (fun () -> f().run)}

            member x.Zero() =
                Load.value ()

            member x.For(s : seq<'a>, f : 'a -> Load<unit>) =
                { run =
                    proc {
                        for e in s do
                            do! (f e).run
                    }
                }

            member x.While(guard : unit -> bool, body : Load<unit>) =
                { run =
                    proc {
                        while guard() do
                            do! body.run
                    }
                }

        let load = LoadBuilder() 

    open System.Runtime.InteropServices

    [<Demo("Load Test")>]
    let loadTest() =
        use r = new Load.Runner(4)
        let mutable allocated = 0

        let myLoad (file : string) =
            load {
                use stream = File.OpenRead(file)
                
                let buffer = Array.zeroCreate 4096
                let mutable remaining = stream.Length

                let ptr = Marshal.AllocHGlobal (int stream.Length)
                Interlocked.Increment(&allocated) |> ignore
                do! Load.onCancel (fun () -> 
                    Interlocked.Decrement(&allocated) |> ignore
                    Marshal.FreeHGlobal ptr
                )


                let mutable c = ptr
                while remaining > 0L do
                    let! r = stream.ReadAsync(buffer, 0, min buffer.Length (int remaining))
                    Marshal.Copy(buffer, 0, c, int r)
                    remaining <- remaining - int64 r
                    c <- c + nativeint r

                return ptr
            }


        let load = myLoad @"C:\Users\schorsch\Desktop\clipper_ver6.2.1.zip"
        let tasks = Array.init 4096 (fun _ -> r.Enqueue load)

        for t in tasks do
            t.Start()

        Console.ReadLine() |> ignore

        for t in tasks do
            t.Stop()

        printfn "allocated: %A" allocated




        ()
