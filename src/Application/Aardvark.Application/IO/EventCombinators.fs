namespace Aardvark.Application

open System
open Aardvark.Base
open System.Reactive
open System.Reactive.Linq
open System.Threading.Tasks
open System.Reactive.Subjects

module Event =

    type private ObservableAwaiter<'a>(o : AsyncSubject<'a>) =
        interface IAwaiter with
            member x.OnCompleted(a) = o.OnCompleted(a)
            member x.GetResult() = o.GetResult() |> ignore
            member x.IsCompleted = o.IsCompleted
           
        interface IAwaiter<'a> with 
            member x.GetResult() = o.GetResult()

    type private ObservableAwaitable<'a>(o : IObservable<'a>) =
        
        interface IAwaitable<'a> with
            member x.GetAwaiter() =
                let a = o.GetAwaiter()
                ObservableAwaiter(a) :> IAwaiter<'a>

            member x.Result = failwith "not implemented"

        interface IAwaitable with
            member x.GetAwaiter() =
                let a = o.GetAwaiter()
                ObservableAwaiter(a) :> IAwaiter

            member x.IsCompleted = false

    type private ObservableEvent<'a>(o : Lazy<IObservable<'a>>, getLatest : Option<unit -> 'a>) =
        let mutable latest = Unchecked.defaultof<'a>
        let o = lazy ( o.Value.Do(fun e -> latest <- e) )
        let untyped = lazy ( o.Value.Select (fun _ -> Unit.Default) )
        let awaitable = lazy ( ObservableAwaitable(o.Value) )
        let mutable next = None

        interface IEvent with
            member x.Values = untyped.Value
            member x.Next = awaitable.Value :> IAwaitable

        interface IEvent<'a> with
            member x.Values = o.Value
            member x.Next = awaitable.Value :> IAwaitable<'a>
            member x.Latest = 
                match getLatest with
                    | Some f -> f()
                    | None -> latest

        new(o) = ObservableEvent(o, None)

    let private fromObservable ( o : unit -> IObservable<'a>) =
        ObservableEvent(lazy( o() )) :> IEvent<'a>

    let private fromObservableWithLatest (latest : unit -> 'a) ( o : unit -> IObservable<'a>) =
        ObservableEvent(lazy( o() ), Some latest) :> IEvent<'a>

    type private EmptyEvent<'a>() =
        static let instance = EmptyEvent<'a>() :> IEvent<'a>

        let o = lazy ( new Subject<'a>() )
        let awaitable = lazy ( ObservableAwaitable(o.Value) )
        let untyped = lazy ( o.Value.Select (fun _ -> Unit.Default) )

        static member Instance = instance

        interface IEvent<'a> with
            member x.Values = o.Value :> IObservable<'a>
            member x.Next = awaitable.Value :> IAwaitable<'a>
            member x.Latest = Unchecked.defaultof<'a>

        interface IEvent with
            member x.Values = untyped.Value
            member x.Next = awaitable.Value :> IAwaitable

    let empty<'a> : IEvent<'a> = EmptyEvent<'a>.Instance

    let collect (f : 'a -> #seq<'b>) (e : IEvent<'a>) : IEvent<'b> =
        let latest = ref Unchecked.defaultof<'b>
        let f a = 
            let res = f a |> Seq.toArray
            if res.Length > 0 then latest := res.[res.Length-1]

            res

        fromObservableWithLatest (fun () -> !latest) (fun () ->
            e.Values.SelectMany (fun a -> a |> f :> seq<_>)
        )
   
    let map (f : 'a -> 'b) (e : IEvent<'a>) : IEvent<'b> =
        let latest = ref Unchecked.defaultof<'b>
        let f a = 
            let res = f a
            latest := res
            res

        fromObservableWithLatest (fun () -> !latest) (fun () ->
            e.Values.Select(f)
        )

    let choose (f : 'a -> Option<'b>) (e : IEvent<'a>) : IEvent<'b> =
        let latest = ref Unchecked.defaultof<'b>
        let f a = 
            let res = f a 
            match res with
                | Some v -> latest := v
                | None -> ()

            res

        fromObservableWithLatest (fun () -> !latest) (fun () ->
            e.Values.SelectMany(fun v ->
                v |> f |> Option.toList :> seq<_>
            )
        )

    let chooseLatest (latest : unit -> 'b) (f : 'a -> Option<'b>) (e : IEvent<'a>) : IEvent<'b> =
        fromObservableWithLatest latest (fun () ->
            e.Values.SelectMany(fun v ->
                v |> f |> Option.toList :> seq<_>
            )
        )


    let filter (f : 'a -> bool) (e : IEvent<'a>) : IEvent<'a> =
        let latest = ref Unchecked.defaultof<'a>
        let f a = 
            let res = f a 
            if res then latest := a
            res

        fromObservableWithLatest (fun () -> !latest) (fun () ->
            e.Values.Where(f)
        )

    let concat (events : seq<IEvent<'a>>) : IEvent<'a> =
        let latest = ref Unchecked.defaultof<'a>
        let f v =
            latest := v
            v

        fromObservableWithLatest (fun () -> !latest) (fun () ->
            let obs = events |> Seq.map (fun e -> e.Values.Select f)
            obs.Concat()
        )


    let test() =
        let a = EventSource(1)

        let some = a |> filter (fun a -> a < 10)

        some.Values.Subscribe(printfn "got: %A") |> ignore

        a.Emit 10
        a.Emit 1
