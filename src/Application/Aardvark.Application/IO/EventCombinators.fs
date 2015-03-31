namespace Aardvark.Application

open System
open Aardvark.Base
open System.Reactive
open System.Reactive.Linq

module Event =

    type private ObservableEvent<'a>(o : Lazy<IObservable<'a>>, getLatest : Option<unit -> 'a>) =
        let mutable latest = Unchecked.defaultof<'a>
        let o = lazy ( o.Value.Do(fun e -> latest <- e) )
        let untyped = lazy ( o.Value.Select (fun _ -> Unit.Default) )
        let mutable next = None

        interface IEvent with
            member x.Values = untyped.Value
            member x.Next = failwith "obsolete"

        interface IEvent<'a> with
            member x.Values = o.Value
            member x.Next = failwith "obsolete"
            member x.Latest = 
                match getLatest with
                    | Some f -> f()
                    | None -> latest

        new(o) = ObservableEvent(o, None)

    let private fromObservable ( o : unit -> IObservable<'a>) =
        ObservableEvent(lazy( o() )) :> IEvent<'a>

    let private fromObservableWithLatest (latest : unit -> 'a) ( o : unit -> IObservable<'a>) =
        ObservableEvent(lazy( o() ), Some latest) :> IEvent<'a>


    let collect (f : 'a -> #seq<'b>) (e : IEvent<'a>) : IEvent<'b> =
        fromObservable (fun () ->
            e.Values.SelectMany (fun a -> a |> f :> seq<_>)
        )
   
    let map (f : 'a -> 'b) (e : IEvent<'a>) : IEvent<'b> =
        fromObservable (fun () ->
            e.Values.Select(f)
        )

    let choose (f : 'a -> Option<'b>) (e : IEvent<'a>) : IEvent<'b> =
        fromObservable (fun () ->
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
        fromObservable (fun () ->
            e.Values.Where(f)
        )

    let concat (events : seq<IEvent<'a>>) : IEvent<'a> =
        fromObservable (fun () ->
            let obs = events |> Seq.map (fun e -> e.Values)
            obs.Concat()
        )


    let test() =
        let a = EventSource(1)

        let some = a |> filter (fun a -> a < 10)

        some.Values.Subscribe(printfn "got: %A") |> ignore

        a.Emit 10
        a.Emit 1
