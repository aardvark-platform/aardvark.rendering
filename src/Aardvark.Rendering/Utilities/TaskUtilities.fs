#if COMPILED
namespace System.Threading.Tasks
#else
open System.Threading.Tasks
#endif

open System
open System.Threading


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Thread =
    
    let create (thread : unit -> unit) =
        let t = new Thread(ThreadStart(thread), IsBackground = true)
        t.Start()
        t

    let start (thread : unit -> unit) =
        let t = new Thread(ThreadStart(thread), IsBackground = true)
        t.Start()


type TaskResult<'a> =
    | Completed of 'a
    | Cancelled
    | Faulted of exn

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Task =
    
    let private (|ForAll|_|) (pattern : 'a -> Option<unit>) (l : list<'a>) =
        if l |> List.forall(fun v -> Option.isSome (pattern v)) then
            Some ()
        else
            None

    let rec private (|AggregateExn|_|) (e : exn) =
        match e with
            | :? AggregateException as e ->
                e.InnerExceptions 
                    |> Seq.toList 
                    |> List.collect (fun e ->
                        match e with
                            | AggregateExn e -> e
                            | _ -> [e]
                    )
                    |> Some
            | _ ->
                None

    let rec private (|CancelledExn|_|) (e : exn) =
        match e with
            | :? OperationCanceledException -> Some ()
            | AggregateExn (ForAll (|CancelledExn|_|)) -> Some ()
            | _ ->
                None

    let pollResult (t : Task<'a>) =
        if t.IsCanceled then Some Cancelled
        elif t.IsFaulted then Some (Faulted t.Exception)
        elif t.IsCompleted then Some (Completed t.Result)
        else None
        
    let getResult (t : Task<'a>) =
        if t.IsCanceled then Cancelled
        elif t.IsFaulted then Faulted t.Exception
        elif t.IsCompleted then Completed t.Result
        else
            try 
                Completed t.Result
            with 
                | CancelledExn -> Cancelled
                | AggregateExn [e] -> Faulted e
                | e -> Faulted e

    let inline status (t : Task) = t.Status 

    let inline create (value : 'a) = Task.FromResult(value)
    
    let map (mapping : 'a -> 'b) (task : Task<'a>) =
        task.ContinueWith(fun (t : Task<'a>) -> 
            match getResult t with
                | Completed v -> mapping v
                | Cancelled -> raise <| OperationCanceledException()
                | Faulted e -> raise e
        )
    
    let mapInline (mapping : 'a -> 'b) (task : Task<'a>) =
        let cont (t : Task<'a>) =
            match getResult t with
                | Completed v -> mapping v
                | Cancelled -> raise <| OperationCanceledException()
                | Faulted e -> raise e
        task.ContinueWith(System.Func<_,_>(cont), TaskContinuationOptions.ExecuteSynchronously)

    let map2 (mapping : 'a -> 'b -> 'c) (l : Task<'a>) (r : Task<'b>) =
        Task.WhenAll(l, r).ContinueWith(fun (_ : Task) ->
            match getResult l, getResult r with
                | Completed l, Completed r -> mapping l r
                | Faulted l, Faulted r -> raise <| AggregateException(l,r)
                | Faulted l, _ -> raise l
                | _, Faulted r -> raise r
                | _ -> raise <| OperationCanceledException()
        )

    let bind (mapping : 'a -> Task<'b>) (task : Task<'a>) =
        let tcs = TaskCompletionSource<'b>()

        let cont (v : Task<'a>) =
            match getResult v with
                | Cancelled ->
                    tcs.SetCanceled()
                | Faulted e ->
                    tcs.SetException e
                | Completed v ->
                    mapping(v).ContinueWith (fun (v : Task<'b>) ->
                        match getResult v with
                            | Cancelled -> tcs.SetCanceled()
                            | Faulted e -> tcs.SetException e
                            | Completed v -> tcs.SetResult v
                    ) |> ignore

        task.ContinueWith(System.Action<_>(cont)) |> ignore
        tcs.Task

    let mapN (mapping : 'a[] -> 'b) (tasks : #seq<Task<'a>>) =
        Task.WhenAll(Seq.toArray tasks).ContinueWith (fun (t : Task<'a[]>) ->
            match getResult t with
                | Cancelled -> raise <| OperationCanceledException()
                | Faulted e -> raise e
                | Completed v -> mapping v
        )

    let lift (t : Task) =
        let cont (t : Task) =
            if t.IsCanceled then raise <| OperationCanceledException()
            elif t.IsFaulted then raise <| t.Exception
            elif t.IsCompleted then ()
            else
                try t.Wait()
                with 
                    | CancelledExn -> raise <| OperationCanceledException()
                    | AggregateExn [e] -> raise e
                    | e -> raise e

        t.ContinueWith (System.Func<Task, unit>(cont), TaskContinuationOptions.ExecuteSynchronously)

    let ignore (t : Task<'a>) =
        let cont (t : Task<'a>) =
            if t.IsCanceled then raise <| OperationCanceledException()
            elif t.IsFaulted then raise <| t.Exception
            elif t.IsCompleted then ()
            else
                try t.Wait()
                with 
                    | CancelledExn -> raise <| OperationCanceledException()
                    | AggregateExn [e] -> raise e
                    | e -> raise e
        t.ContinueWith (System.Func<Task<'a>, unit>(cont), TaskContinuationOptions.ExecuteSynchronously)

    let inline zip (l : Task<'a>) (r : Task<'b>) = map2 (fun a b -> (a,b)) l r

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module List =
    let rec mapT (mapping : 'a -> Task<'b>) (l : list<'a>) : Task<list<'b>> =
        match l with
            | [] -> 
                Task.create []

            | [e] -> 
                e |> mapping |> Task.mapInline List.singleton

            | [l;r] ->
                Task.map2 (fun l r -> [l;r]) (mapping l) (mapping r)

            | h :: t ->
                let h = mapping h
                let t = mapT mapping t
                Task.map2 (fun a b -> a::b) h t

    let rec collectT (mapping : 'a -> Task<list<'b>>) (l : list<'a>) : Task<list<'b>> =
        match l with
            | [] -> 
                Task.create []
                
            | [e] -> 
                e |> mapping
                
            | [l;r] ->
                Task.map2 (@) (mapping l) (mapping r)

            | h :: t ->
                let t = collectT mapping t
                let h = mapping h
                Task.map2 (fun a b -> a @ b) h t

    let rec chooseT (mapping : 'a -> Task<Option<'b>>) (l : list<'a>) : Task<list<'b>> =
        match l with
            | [] -> 
                Task.create []
                
            | [e] -> 
                e |> mapping |> Task.mapInline Option.toList
                
            | [l;r] ->
                let concat (l : Option<'b>) (r : Option<'b>) =
                    match l, r with
                        | Some l, Some r -> [l;r]
                        | Some v, None | None, Some v -> [v]
                        | None, None -> []

                Task.map2 concat (mapping l) (mapping r)

            | h :: t ->
                let t = chooseT mapping t
                let h = mapping h

                let prepend (h : Option<'b>) (t : list<'b>) =
                    match h with
                        | Some h -> h :: t
                        | None -> t

                Task.map2 prepend h t

    let rec filterT (mapping : 'a -> Task<bool>) (l : list<'a>) : Task<list<'a>> =
        chooseT (fun v -> mapping v |> Task.map (function true -> Some v | _ -> None)) l 


module Test =

    let newTest() =
        let tcs = TaskCompletionSource<int>()
        let res = tcs.Task |> Task.bind (fun a -> printfn "sleep"; Task.Delay(1000) |> Task.lift |> Task.map (fun _ -> 2 * a))

        let thread = 
            Thread.create <| fun () ->
                match Task.getResult res with
                    | Completed v ->
                        printfn "finished: %A" v
                    | Cancelled ->
                        printfn "cancelled"
                    | Faulted e ->
                        printfn "faulted: %A" e


        tcs,thread

    let run() =
        let tcs,thread = newTest()
        Thread.Sleep 1000
        tcs.SetResult(10)

        thread.Join()







        