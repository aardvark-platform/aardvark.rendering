namespace Aardvark.Base.Incremental

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open System.Collections.Generic
open System.Reactive.Linq
open System.Reactive.Subjects

type astream<'a> =
    abstract member obs : IObservable<'a>

type cstream<'a> = { subject : Subject<'a> } with
    interface astream<'a> with
        member x.obs = x.subject :> IObservable<'a>

module AStream = 
    
    type internal AdaptiveStream<'a> = { obs : IObservable<'a> } with
        interface astream<'a> with
            member x.obs = x.obs

    type internal ConstantObservable<'a>(values : seq<'a>) =
        let arr = values |> Seq.toArray

        interface IObservable<'a> with
            member x.Subscribe obs =
                for v in arr do obs.OnNext(v)
                obs.OnCompleted()
                { new IDisposable with member x.Dispose() = ()}

    type private EmptyImpl<'a>() =
        static let instance = { obs = ConstantObservable [] } :> astream<'a>
        static member Instance = instance

    [<GeneralizableValue>]
    let empty<'a> : astream<'a> = EmptyImpl<'a>.Instance

    let single (v : 'a) = { obs = ConstantObservable [v] } :> astream<_>

    let ofSeq (v : seq<'a>) = { obs = ConstantObservable v } :> astream<_>

    let ofList (v : list<'a>) = { obs = ConstantObservable v } :> astream<_>

    let ofArray (v : 'a[]) = { obs = ConstantObservable v } :> astream<_>

    let ofObservable (o : IObservable<'a>) = { obs = o } :> astream<_>

    let toObservable (s : astream<'a>) = s.obs


    let map (f : 'a -> 'b) (s : astream<'a>) = { obs = s.obs.Select f } :> astream<_>

    let collect (f : 'a -> astream<'b>) (s : astream<'a>) = { obs = s.obs.SelectMany(f >> toObservable) } :> astream<_>

    let filter (f : 'a -> bool) (s : astream<'a>) = { obs = s.obs.Where f } :> astream<_>

    let choose (f : 'a -> Option<'b>) (s : astream<'a>) = 
        { obs = Observable.Create(fun (obs : IObserver<'b>) ->
            s.obs.Subscribe(fun v ->
                match f v with
                    | Some v -> obs.OnNext v
                    | None -> ()
            )
          )
        } :> astream<_>

    let union (streams : seq<astream<'a>>)=
        { obs = Observable.Merge(streams |> Seq.map toObservable) } :> astream<_>

    let concat (l : astream<'a>) (r : astream<'a>) =
        { obs = Observable.Concat([l.obs; r.obs]) } :> astream<_>

    let delay (f : unit -> astream<'a>) =
        { obs = Observable.Defer(fun () -> f().obs ) } :> astream<_>

    let intersperse (l : astream<'a>) (r : astream<'b>) =
        { obs =
            Observable.Create(fun (obs : IObserver<Either<'a, 'b>>) ->
                let ls = l.obs.Subscribe(fun v ->
                    obs.OnNext(Left v)
                )

                let rs = r.obs.Subscribe(fun v ->
                    obs.OnNext(Right v)
                )

                { new IDisposable with member x.Dispose() = ls.Dispose(); rs.Dispose() }
            )
        } :> astream<_>

    let ofMod (m : IMod<'a>) : astream<'a> =
        { obs = Observable.Create (fun (o : IObserver<'a>) ->
        
            m |> Mod.unsafeRegisterCallbackNoGcRoot (fun v -> o.OnNext v)

          )
        } :> astream<_>

        

    let splitWhile (cond : astream<bool>) (s : astream<'a>) =
        { obs =
            Observable.Create(fun (o : IObserver<astream<'a>>) ->
                let mutable active = false
                let mutable current = new Subject<'a>()

                let cs = 
                    cond.obs.Subscribe(fun v ->
                        if v && not active then
                            current <- new Subject<'a>()
                            o.OnNext ({ obs = current } :> astream<_>)
                        elif not v && active then
                            if not (isNull current) then
                                current.OnCompleted()
                                current.Dispose()
                                current <- null

                        active <- v
                    )


                let vs = 
                    s.obs.Subscribe (fun v ->
                        if active && not (isNull current) then 
                            current.OnNext v
                        elif not active && not (isNull current) then
                            current.OnCompleted()
                            current <- null
                    )
                { new IDisposable with member x.Dispose() = cs.Dispose(); vs.Dispose() }
            )
        } :> astream<_>

    let splitWhileMod (cond : IMod<bool>) (s : astream<'a>) =
        splitWhile (ofMod cond) s

    let fold (f : 's -> 'a -> 's) (seed : 's) (s : astream<'a>) =
        let mutable current = seed
        let res = Mod.custom(fun s -> current)

        let subscription =
            s.obs.Subscribe(fun v ->
                current <- f current v

                let ro = lock res (fun () -> res.OutOfDate)
                if not ro then transact (fun () -> res.MarkOutdated())
            )
        res

    let latest (s : astream<'a>) =
        let mutable current = None
        let res = Mod.custom (fun _ -> current)

        let subscription = 
            s.obs.Subscribe (fun v ->
                current <- Some v
                let o = lock res (fun () -> res.OutOfDate)
                if not o then transact (fun () -> res.MarkOutdated())
            )

        res

    let all (s : astream<'a>) =

        let newReader() =
            let self : ref<ASetReaders.AbstractReader<_>> = ref Unchecked.defaultof<_>
            let buffer = List()
            let subscription = s.obs.Subscribe (fun v ->
                let o = 
                    lock self.Value (fun () -> 
                        buffer.Add(Add v)
                        self.Value.OutOfDate
                    )
                if not o then
                    transact (fun () -> self.Value.MarkOutdated())
            )

            self :=
                { new ASetReaders.AbstractReader<'a>() with
                    override x.ComputeDelta() =
                        let res = buffer |> Seq.toList
                        buffer.Clear()
                        res

                    override x.Release() =
                        buffer.Clear()
                        subscription.Dispose()
                } 

            !self :> IReader<_>

        ASet.AdaptiveSet(newReader) :> aset<_>

    module Operators =
        let inline (<?) (l : astream<'a>) (r : astream<bool>) =
            splitWhile r l

        let inline (?>) (r : astream<bool>) (l : astream<'a>) =
            splitWhile r l

        let inline (>.<) (l : astream<'a>) (r : astream<'b>) =
            intersperse l r

        let inline (>|<) (l : astream<'a>) (r : astream<'a>) =
            union [l; r]

//module StreamPatterns =
//
//    type Parser<'s, 'i> =
//        abstract member parse : 's -> 'i -> Option<'s * Parser<'s, 'i>>
//
//    type Parser<'s, 'i, 'r> = { parse : 's -> 'i -> Option<'r * 's * Parser<'s, 'i>> } with
//        interface Parser<'s, 'i> with
//            member x.parse s i =
//                x.parse s i |> Option.map (fun (_,s,p) -> s,p)
//
//
//    module Parser =
//        
//        let map (f : 'r0 -> 'r1) (p : Parser<'s, 'i, 'r0>) =
//            { parse = fun s i ->
//                match p.parse s i with
//                    | Some (r,s,rest) ->
//                        Some (f r, s, rest)
//                    | None ->
//                        None
//            }
//
//        let append (l : Parser<'s, 'i, 'r0>) (r : Parser<'s, 'i, 'r1>) =
//            { parse = fun s i ->
//                
//            }
//
//
//    type Pattern<'a, 'b> = { run : 'a -> Option<'b> }
//
//    let value v = { run = fun a -> if a = v then Some v else None }
//
//    let map (f : 'a -> 'b) (p : Pattern<'x, 'a>) =
//        { run = fun v -> v |> p.run |> Option.map f}
//
//    let many (p : Pattern<'a, 'b>) =
//        { run = fun v ->
//            match p.run
//        }
//




module CStream =
    let empty<'a> = { subject = new Subject<'a>() }

    let push (v : 'a) (s : cstream<'a>) = s.subject.OnNext v

    let pushMany (v : seq<'a>) (s : cstream<'a>) = 
        for e in v do
            s.subject.OnNext e
    
    module Operators =
        let inline (<==) (s : cstream<'a>) (v : 'a) =
            push v s

        let inline (==>) (v : 'a) (s : cstream<'a>) =
            push v s


        let inline (<<=) (s : cstream<'a>) (v : seq<'a>) =
            pushMany v s

        let inline (=>>) (v : seq<'a>) (s : cstream<'a>) =
            pushMany v s

[<AutoOpen>]
module ``AStream Builder`` =
    type AStreamBuilder() =
        member x.For(s : astream<'a>, f : 'a -> astream<'b>) =
            s |> AStream.collect f

        member x.For(s : seq<'a>, f : 'a -> astream<'b>) =
            s |> AStream.ofSeq |> AStream.collect f

        member x.Yield (v : 'a) =
            AStream.single v

        member x.YieldFrom(s : astream<'a>) = s

        member x.YieldFrom(s : seq<'a>) = s |> AStream.ofSeq

        member x.Delay(f : unit -> astream<'a>) = f

        member x.Combine(l : astream<'a>, r : unit -> astream<'a>) =
            AStream.concat l (AStream.delay r)

        member x.Zero() = AStream.empty

        member x.While(guard : unit -> bool, body : unit -> astream<'a>) =
            if guard() then
                let rest = AStream.delay (fun () -> x.While(guard, body))
                AStream.concat (body()) rest
            else
                AStream.empty

        
        member x.TryWith(m : unit -> astream<'a>, handler : Exception -> astream<'a>) =
            try
                { AStream.obs = Observable.Catch(m().obs, handler >> AStream.toObservable) } :> astream<_>
            with e ->
                handler e

        member x.TryFinally(m : unit -> astream<'a>, fin : unit -> unit) =
            { AStream.obs = Observable.Finally(m().obs, Action(fin)) } :> astream<_>

        member x.Run(f : unit -> astream<'a>) = f()

        member x.Using<'d, 'a when 'd :> IDisposable>(value : 'd, f : 'd -> astream<'a>) =
            let s = f value
            { AStream.obs = Observable.Finally(s.obs, Action(value.Dispose)) } :> astream<_>

    let astream = AStreamBuilder()

type Result<'a, 'b> =
    | Intermediate of 'a
    | Final of 'b

type Workflow<'a, 'b> = { stream : astream<Result<'a, 'b>> }

module Workflow =
    let intermediates (w : Workflow<'a, 'b>) =
        w.stream |> AStream.choose (fun v -> match v with | Intermediate v -> Some v | _ -> None)

    let finals (w : Workflow<'a, 'b>) =
        w.stream |> AStream.choose (fun v -> match v with | Final v -> Some v | _ -> None)




[<AutoOpen>]
module ``Workflow Builder`` =
    
    type WorkflowBuilder() =
        member x.For(s : astream<'a>, f : 'a -> Workflow<'b, 'c>) =
            { stream = s |> AStream.collect (fun v -> (f v).stream) }

        member x.Bind(w : Workflow<'a, 'b>, f : 'b -> Workflow<'a, 'd>) =
            { stream = w.stream |> AStream.collect (fun v -> 
                match v with
                    | Final v -> (f v).stream
                    | Intermediate v -> AStream.single (Intermediate v)
              ) 
            }


        member x.For(w : Workflow<'a, 'b>, f : Result<'a, 'b> -> Workflow<'c, 'd>) =
            { stream = w.stream |> AStream.collect (fun v -> (f v).stream) }

        member x.For(w : Workflow<'a, 'a>, f : bool * 'a -> Workflow<'c, 'd>) =
            { stream = w.stream |> AStream.collect (fun v -> 
                match v with
                    | Intermediate v -> (f (false, v)).stream
                    | Final v -> (f (true, v)).stream
              ) 
            }


        member x.Yield (v : 'a) =
            { stream = AStream.single (Intermediate v)}

        member x.Return (v : 'a) =
            { stream = AStream.single (Final v)}

        member x.Delay(f : unit -> Workflow<'a, 'b>) = { stream = AStream.delay (fun () -> f().stream) }

        member x.Combine(l : Workflow<'a, 'b>, r : Workflow<'a, 'b>) =
            { stream = AStream.concat l.stream r.stream }

        member x.Zero() = { stream = AStream.empty }

    let workflow = WorkflowBuilder()


module Test =
    open AStream.Operators
    open CStream.Operators


    let sketch (active : IMod<bool>) (positions : astream<PixelPosition>) =
        workflow {
            for s in positions <? AStream.ofMod active do
                let result = List()
                printfn "sadsadasd"
                for e in s do
                    result.Add e
                    yield Seq.toList result
                
                return Seq.toArray result
                yield []
        }

    let sketchAndTransform (viewProj : IMod<Trafo3d>) (polygons : Workflow<_,PixelPosition[]>) =
        workflow {
            let! poly = polygons
            let vp = Mod.force viewProj

            if poly.Length > 2 then
                return poly |> Seq.map (fun v -> v.NormalizedPosition)
                            |> Seq.map (fun n -> V3d(2.0 * n.X - 1.0, 1.0 - 2.0 * n.Y, 0.0))
                            |> Seq.map (vp.Backward.TransformPosProj)
                            |> Polygon3d
        }

    let bla =
        astream {

            let mutable counter = 0
            while counter < 10 do
                yield 1
                counter <- counter + 1

            try 
                for i in 0..10 do
                    if i > 9 then raise <| IndexOutOfRangeException()
                    yield! [i; 2 * i]

            with :? IndexOutOfRangeException ->
                yield 123

        }

    let run() =

        

        bla.obs.Subscribe (printfn "bla: %A") |> ignore

        let pos = CStream.empty
        let active = Mod.init false
        let viewProj = Mod.init Trafo3d.Identity
        let result = sketch active pos
        
        let pp v = PixelPosition(v, Box2i(V2i.Zero, V2i(8, 8)))


        transact (fun () -> Mod.change active true)
        pos <<= [pp V2i.II; pp V2i.II; pp V2i.II]
        transact (fun () -> Mod.change active false)


        let finalValues = (result |> Workflow.finals).obs

        let e = Observable.MostRecent(finalValues, [||]) 

        let final = 
            result 
                |> Workflow.finals 
                |> AStream.all 
                |> ASet.toMod
                |> Mod.map (Seq.map (Array.map (fun pp -> pp.Position)) >> Seq.toList)

        let intermediate =
            result
                |> Workflow.intermediates
                |> AStream.latest
                |> Mod.map (fun o -> match o with | Some v -> v |> List.map (fun pp -> pp.NormalizedPosition) | None -> [])

        intermediate |> Mod.force |> printfn "intermediate: %A"
        final |> Mod.force |> printfn "final: %A"

        transact (fun () -> Mod.change active true)
        pos <<= [pp V2i.OO; pp V2i.IO; pp V2i.OI]
        transact (fun () -> Mod.change active false)

        intermediate |> Mod.force |> printfn "intermediate: %A"
        final |> Mod.force |> printfn "final: %A"

        pos <<= [pp V2i.OO; pp V2i.IO; pp V2i.OI]

        intermediate |> Mod.force |> printfn "intermediate: %A"
        final |> Mod.force |> printfn "final: %A"


    let run2() =
        let pos = CStream.empty
        let active = Mod.init false
        let viewProj = Mod.init Trafo3d.Identity
        let result = sketch active pos
        
        let pp v = PixelPosition(v, Box2i(V2i.Zero, V2i(8, 8)))

        let a = (result |> Workflow.finals).obs
        a.Subscribe(printfn "cb: %A") |> ignore

        transact (fun () -> Mod.change active true)
        pos <<= [pp V2i.II; pp V2i.II; pp V2i.II]
        transact (fun () -> Mod.change active false)

        ()


