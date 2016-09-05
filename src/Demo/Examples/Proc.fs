namespace Aardvark.Base.Incremental

open System
open Aardvark.Base
open Aardvark.Base.Monads.State

[<AutoOpen>]
module Temp =

    [<Struct; CustomEquality; CustomComparison; StructuredFormatDisplay("{AsString}")>]
    type Time(ticks : int64) =
        static let nanosecondsPerTick = 1000000000L / TimeSpan.TicksPerSecond
        static let sw = System.Diagnostics.Stopwatch()
        static let startTime = DateTime.Now.Ticks
        static do sw.Start()

    

        static member Now = Time(startTime + sw.Elapsed.Ticks)

        static member (-) (l : Time, r : Time) =
            MicroTime((l.Ticks - r.Ticks) * nanosecondsPerTick)

        static member (+) (l : Time, r : MicroTime) =
            Time(l.Ticks + r.TotalNanoseconds / nanosecondsPerTick)

        static member (+) (l : MicroTime, r : Time) =
            Time(l.TotalNanoseconds / nanosecondsPerTick + r.Ticks)

        member x.Day = DateTime(ticks).Day
        member x.DayOfWeek = DateTime(ticks).DayOfWeek
        member x.DayOfYear = DateTime(ticks).DayOfYear
        member x.Hour = DateTime(ticks).Hour
        member x.Millisecond = DateTime(ticks).Millisecond
        member x.Minute = DateTime(ticks).Minute
        member x.Month = DateTime(ticks).Month
        member x.Second = DateTime(ticks).Second
        member x.Year = DateTime(ticks).Year

        member private x.AsString = x.ToString()

        override x.GetHashCode() = ticks.GetHashCode()
        override x.Equals o =
            match o with
                | :? Time as o -> ticks = o.Ticks
                | _ -> false

        override x.ToString() = DateTime(ticks).ToString("yyyy-MM-dd\/HH:mm:ss.fff")

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? Time as o -> compare ticks o.Ticks
                    | _ -> failwithf "[Time] cannot compare Time to %A" o

        member x.Ticks = ticks


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

    type private TimePattern private() =
        inherit Pattern<Time>()

        static let instance = TimePattern() :> Pattern<Time>
        static member Instance = instance
        
        override x.DependsOnTime = true
        override x.Relevant = PersistentHashSet.empty
        override x.Match(s,source,_) = 
            if isNull source then Some s.time
            else None


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

[<AbstractClass>]
type Event<'a>() = 
    abstract member run : ProcState<'s> -> EventResult<'a>

and EventResult<'a> =
    | EFinished of 'a
    | EContinue of Pattern * (obj -> Event<'a>)

and Proc<'s, 'a> = { run : State<ProcState<'s>, ProcResult<'s, 'a>> } with
    member x.Check(a : 'a -> bool) =
        { run =
            state {
                let! r = x.run
                match r with
                    | Finished v ->
                        if a v then return Finished true
                        else return! x.Check(a).run
                    | Continue(p, c) ->
                        return Continue(p, fun o -> c(o).Check a)
            }
        }

and ProcResult<'s, 'a> =
    | Finished of 'a
    | Continue of Pattern * (obj -> Proc<'s, 'a>)

and Assertion<'s, 'a> =
    private
        | That of list<Proc<'s, 'a>>


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Event =

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

    let rec choose (f : 'a -> Option<'b>) (a : Event<'a>) =
        { new Event<'b>() with
            member x.run s = 
                match a.run s with
                    | EFinished v -> 
                        match f v with
                            | Some r -> EFinished r
                            | None -> (choose f a).run s

                    | EContinue(p,cont) -> EContinue(p, cont >> choose f)
        }

    let filter (f : 'a -> bool) (a : Event<'a>) =
        choose (fun v -> if f v then Some v else None) a


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Proc =
    let value (v : 'a) =
       { run = State.value (Finished v) }

    let rec ofEvent (e : Event<'a>) : Proc<'s, 'a> =
        { run =
            state {
                let! s = State.get
                match e.run s with
                    | EFinished v -> return Finished v
                    | EContinue(p, c) -> return Continue(p, c >> ofEvent)
            }
        }

    let now<'s> : Proc<'s, Time> =
        { run = State.get |> State.map (fun s -> Finished s.time) }

    let nextTime<'s> : Proc<'s,Time> =
        { run = State.value ( Continue( Pattern.time, fun _ -> now) ) }

    let dt<'s> : Proc<'s, MicroTime> =   
        let getTime =
            state {
                let! s = State.get
                return s.time
            }
        { run = 
            state {
                let! t = getTime
                return Continue(Pattern.time, fun o -> { run = getTime |> State.map (fun n -> n - t |> Finished) })
            }
        } 

    let rec map (f : 'a -> 'b) (m : Proc<'s, 'a>) =
        { run =
            state {
                let! v = m.run
                match v with
                    | Finished v -> return v |> f |> Finished
                    | Continue(p,c) -> return Continue(p, c >> map f)
            }
        }

    let never<'s, 'a> : Proc<'s, 'a> =
        { run = State.value (Continue(Pattern.never, fun o -> failwith ""))}


    let rec bind (f : 'a -> Proc<'s, 'b>) (m : Proc<'s, 'a>) =
        { run =
            state {
                let! r = m.run
                match r with
                    | Finished v -> return! f(v).run
                    | Continue(p,cont) -> return Continue(p, cont >> bind f)
            }
        }

    let rec filter (f : 'a -> bool) (m : Proc<'s, 'a>) =
        m |> bind (fun v ->
            if f v then value v
            else filter f m
        )


    let delay (r : ProcResult<'s, 'a>) =
        { run = State.value r }


    let rec any' (l : Proc<'s, 'a>) (r : Proc<'s, 'a>)  =
        { run =
            state {
                let! l' = l.run

                match l' with
                    | Finished l -> 
                        return Finished l

                    | Continue(lp, lcont) ->
                        let! r' = r.run
                        match r' with
                            | Finished r -> 
                                return Finished r

                            | Continue(rp, rcont) ->
                                let p = Pattern.choice' lp rp
                                return Continue(p, fun o ->
                                    match unbox<Choice<obj, obj>> o with
                                        | Choice1Of2 l -> any' (lcont l) (delay r')
                                        | Choice2Of2 r -> any' (delay l') (rcont r)
                                )
                            
            }
        }


    let choice (l : Proc<'s, 'a>) (r : Proc<'s, 'b>) =
        any' (map Choice1Of2 l) (map Choice2Of2 r)

    let any (ls : list<Proc<'s, 'a>>) =
        match ls with
            | [] -> never
            | h :: rest ->
                let mutable res = h
                for r in rest do
                    res <- any' res r
                res


    let rec par' (l : Proc<'s, unit>) (r : Proc<'s, unit>) = // (ls : list<Proc<'s, unit>>) =
        { run =
            state {
                let! l' = l.run
                let! r' = r.run

                match l', r' with
                    | Finished (), r' -> return r'
                    | l', Finished() -> return l'
                    | Continue(lp,lc), Continue(rp,rc) ->
                        return Continue(Pattern.par lp rp, fun o ->
                            let o = unbox<Option<obj> * Option<obj>> o
                            match o with
                                | Some l, Some r -> par' (lc l) (rc r)
                                | None, Some r -> par' (delay l') (rc r)
                                | Some l, None -> par' (lc l) (delay r')
                                | _ -> par' (delay l') (delay r')
                        )

            }
        }

    let par (l : list<Proc<'s, unit>>) =
        match l with
            | [] -> never
            | h :: rest ->
                let mutable res = h
                for e in rest do
                    res <- par' res e
                res

    let rec append (l : Proc<'s, unit>) (r : Proc<'s, 'a>) =
        { run =
            state {
                let! res = l.run
                match res with
                    | Finished v -> return! r.run
                    | Continue(p,cont) -> return Continue(p, fun o -> append (cont o) r)
            }
        }

    let repeatUntil (guard : Proc<'s, 'a>) (body : Proc<'s, unit>) =
        let cancelOrM = choice guard body

        let rec repeatUntil (m : Proc<'s, Choice<'a, unit>>) =
            { run =
                state {
                    let! r = m.run
                    match r with
                        | Finished v ->
                            match v with
                                | Choice1Of2 a -> return Finished ()
                                | Choice2Of2 () -> return! repeatUntil(cancelOrM).run

                        | Continue(p, c) ->
                            return Continue(p, c >> bind (function Choice1Of2 _ -> value () | _ -> repeatUntil cancelOrM))

                }
            }

        repeatUntil cancelOrM

    let rec repeatWhile (guard : Proc<'s, bool>) (body : Proc<'s, unit>) =
        let self() = repeatWhile guard body
        guard |> bind (fun v -> if v then append body (self()) else value ())

    let private delay' (f : unit -> Proc<'s, 'a>) =
        { run =
            state {
                return! f().run
            }
        }

    let rec foreach (elements : Proc<'s, 'a>) (body : 'a -> Proc<'s, unit>) : Proc<'s, unit> =
        append (bind body elements) (delay' (fun () -> foreach elements body))

    let guarded (guard : Proc<'s, 'a>) (inner : Proc<'s, 'b>) =
        let cancelOrM = choice guard inner

        let rec guarded (m : Proc<'s, Choice<'a, 'b>>) =
            { run =
                state {
                    let! r = m.run
                    match r with
                        | Finished v ->
                            return Finished v
//                            match v with
//                                | Choice1Of2 _ -> return Finished (Choice2Of2 
//                                | Choice2Of2 v -> return Finished (Some v)
                    
                        | Continue(p,c) ->
                            return Continue(p, c >> guarded)
                }
            }

        guarded cancelOrM 

    let rec repeat (inner : Proc<'s, unit>) =
        { run =
            state {
                let! v = inner.run
                match v with
                    | Finished () -> return! repeat(inner).run
                    | Continue(p,cont) -> return Continue(p, fun o -> append (cont o) (repeat inner))
            }
        }

    let inline check (b : ^b -> bool) (v : ^a) =
        (^a : (member Check : (^b -> bool) -> ^x ) (v, b))

    let inline occurs (v : ^a)  =
        v |> check (fun b' -> true)

    let inline eq (v : ^a) (b : ^b) =
        v |> check (fun b' -> b' = b)

    let inline neq (v : ^a) (b : ^b) =
        v |> check (fun b' -> b' <> b)


    open System.Collections.Generic

    type private MultiDict<'k, 'v when 'k : equality>() =
        let store = Dictionary<'k, List<'v>>()

        member x.ContainsKey (key : 'k) =
            store.ContainsKey key

        member x.Add(key : 'k, value : 'v) =
            let list = 
                match store.TryGetValue key with
                    | (true, l) -> l
                    | _ ->
                        let list = List()
                        store.[key] <- list
                        list

            list.Add value

        member x.Keys = store.Keys

        interface System.Collections.IEnumerable with
            member x.GetEnumerator() = store.GetEnumerator() :> _

        interface System.Collections.Generic.IEnumerable<KeyValuePair<'k, List<'v>>> with
            member x.GetEnumerator() = store.GetEnumerator() :> _

    type private Runner<'s>(state : 's) =
        inherit Mod.AbstractMod<'s>()

        let subscriptions = Dictionary<IObservable, IDisposable>()

        let mutable pending : MultiDict<Pattern, obj -> Proc<'s, unit>> = MultiDict()
        let queue = Queue<IObservable * obj * Time>()
        let mutable state = { time = Time.Now; userState = state }
        let mutable dependsOnTime = false


        let now () = Time.Now

        override x.Compute() =
            x.Evaluate()
            
            if dependsOnTime then
                AdaptiveObject.Time.Outputs.Add x |> ignore
            
            state.userState

        member private x.Push(source : IObservable, value : obj) =
            queue.Enqueue(source, value, now())
            transact (fun () -> x.MarkOutdated())

        member x.Enqueue(sf : Proc<'s, unit>) =
            lock x (fun () ->
                match sf.run.Run(&state) with
                    | Finished _ -> ()
                    | Continue(p, cont) ->
                        pending.Add(p, cont)

                        if p.DependsOnTime then
                            dependsOnTime <- true

                        for r in p.Relevant do
                            if not (subscriptions.ContainsKey r) then
                                subscriptions.[r] <- r.Subscribe(fun o -> x.Push(r, o))
            )
                            
        member private x.Step() =
            lock x (fun () ->
                while queue.Count > 0 do
                    let (s,e,t) = queue.Dequeue()
                    state <- { state with time = t }

                    let mutable dt = false
                    let mutable newPending = MultiDict()
                    let mutable tconts = 0
                    for KeyValue(p, conts) in pending do
                        match p.MatchUntyped(state,s,e) with
                            | Some v -> 
                                if isNull s then
                                    tconts <- tconts + 1

                                for cont in conts do
                                    let c = cont v
                                    match c.run.Run(&state) with
                                        | Continue(p,c) ->
                                            newPending.Add(p, c)
                                            dt <- dt || p.DependsOnTime
                                        | _ ->
                                            ()
                            | None ->
                                for cont in conts do
                                    newPending.Add(p,cont)
                                    dt <- dt || p.DependsOnTime


                    let oldEvents   = pending.Keys |> Seq.collect (fun p -> p.Relevant) |> HashSet
                    let newEvents   = newPending.Keys |> Seq.collect (fun p -> p.Relevant) |> HashSet
                    let removed     = oldEvents |> Seq.filter (newEvents.Contains >> not) |> Seq.toList
                    let added       = newEvents |> Seq.filter (oldEvents.Contains >> not) |> Seq.toList

                    for a in added do
                        subscriptions.[a] <- a.Subscribe(fun o -> x.Push(a, o))

                    for r in removed do
                        match subscriptions.TryGetValue r with
                            | (true, s) -> 
                                subscriptions.Remove r |> ignore
                                s.Dispose()
                            | _ -> ()

                    pending <- newPending
                    dependsOnTime <- dt

            )

        member x.Evaluate() =
            lock x (fun () ->
                queue.Enqueue(null, null, now ())
                x.Step()
            )

        member x.State = x :> IMod<_>

    let toMod (s : 's) (p : Proc<'s,unit>) =
        let r = Runner<'s>(s)
        r.Enqueue p
        r.State

    let inline ignore (m : Proc<'s, 'a>) =
        map ignore m


[<AutoOpen>]
module ``SF Builders`` =

    let inline (.=) a b = Proc.eq a b
    let inline (.<>) a b = Proc.neq a b
    let inline (.>) a b = a |> Proc.check (fun a -> a > b)
    let inline (.<) a b = a |> Proc.check (fun a -> a < b)
    let inline (.>=) a b = a |> Proc.check (fun a -> a >= b)
    let inline (.<=) a b = a |> Proc.check (fun a -> a >= b)

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

        member x.Bind(m : State<'s, 'a>, f : 'a -> Proc<'s, 'b>) =
            Proc.bind f { run = m |> lift Finished }

        member x.Return(v : 'a) = 
            Proc.value v

        member x.ReturnFrom(sf : Proc<'s, 'a>) =
            sf

        member x.ReturnFrom(sf : Event<'a>) =
            Proc.ofEvent sf

        member x.ReturnFrom(s : State<'s, 'a>) =
            { run = s |> lift Finished }

        member x.TryWith(m : Assertion<'s,'x> * Proc<'s, 'a>, comp : 'x -> Proc<'s, 'a>) : Proc<'s, 'a> =
            let (That assertions), body = m

            let guard = 
                match assertions with
                    | [a] -> a
                    | _ -> assertions |> Proc.any

            Proc.guarded guard body |> Proc.bind (fun v ->
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

    let proc = ProcBuilder()

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


