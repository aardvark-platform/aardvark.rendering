module Program

open System
open System.IO
open Rendering.Examples

open System.Runtime.InteropServices
open System.Diagnostics
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

#nowarn "9"
#nowarn "51"

module TimerTest =

    type TimerCB = delegate of nativeint * bool -> unit

    [<AutoOpen>]
    module private Helpers =
        [<DllImport("kernel32.dll")>]
        extern nativeint CreateWaitableTimer(nativeint timerAttributes, bool manualReset, string timerName)

        [<DllImport("kernel32.dll")>]
        extern bool SetWaitableTimer(nativeint timer, int64* ft, int period, void* completionCallback, void* completionArg, bool resume)
        
        [<DllImport("kernel32.dll")>]
        extern bool CancelWaitableTimer(nativeint timer)


    type WaitableTimer() as this =
        inherit WaitHandle()
        let mutable handle = CreateWaitableTimer(0n, true, Guid.NewGuid() |> string)
        do this.SafeWaitHandle <- new Microsoft.Win32.SafeHandles.SafeWaitHandle(handle,true)

        member x.Set(timeout : System.Int64) =
            let mutable tt = -timeout
            SetWaitableTimer(handle, &&tt, 0, 0n, 0n, false)

        override x.Dispose(d : bool) =
            CancelWaitableTimer(handle) |> ignore
            base.Dispose(d)

    [<DllImport("kernel32.dll")>]
    extern bool CreateTimerQueueTimer(nativeint& phNewTimer, nativeint timerQueue, nativeint callback, void* parameter, uint32 due, uint32 period, uint64 flags)
    
    [<DllImport("kernel32.dll"); Security.SuppressUnmanagedCodeSecurity>]
    extern void SleepEx(uint32 ms, bool alertable)

    [<DllImport("kernel32.dll"); Security.SuppressUnmanagedCodeSecurity>]
    extern void timeBeginPeriod(uint32 ms)
     
    [<DllImport("kernel32.dll"); Security.SuppressUnmanagedCodeSecurity>]
    extern void timeEndPeriod(uint32 ms)

    [<DllImport("ntdll.dll"); Security.SuppressUnmanagedCodeSecurity>]
    extern void NtDelayExecution(bool alertable, int64* delay)
                         
    let mmTimer() = 
//        let timer = new WaitableTimer()
//        let sw = Stopwatch()
//        let mutable sum = 0.0
//        let mutable count = 0
//        timer.Set(0L) |> ignore
//        let thread =
//            async {
//                do! Async.SwitchToNewThread()
//                while true do
//                    timer.WaitOne() |> ignore
//                    sw.Stop()
//                    let e = sw.Elapsed.TotalMilliseconds
//                    sw.Restart()
//                    if e <> 0.0 then
//                        sum <- sum + e
//                        count <- count + 1
//
//                    timer.Set(100L) |> ignore
//            }
//
//        Async.Start thread


        let sw = Stopwatch()
        let mutable sum = 0.0
        let mutable count = 0
        let callback (a : nativeint) (f : bool) =
            sw.Stop()
            let e = sw.Elapsed.TotalMilliseconds
            sw.Restart()
            if e <> 0.0 then
                sum <- sum + e
                count <- count + 1

        let d = TimerCB(callback)
        let gc = GCHandle.Alloc d
        let cb = Marshal.GetFunctionPointerForDelegate(d)

        let mutable timer = 0n
        CreateTimerQueueTimer(&timer, 0n, cb, 0n, 0u, 1u, 0x00000020UL ||| 0x00000080UL) |> ignore
        Thread.Sleep(5000)
         

        printfn "mm: %fms" (sum / float count)

    let timerPrecision() =
        let sw = Stopwatch()
        let mutable sum = 0.0
        let mutable count = 0

        let tick o =
            sw.Stop()
            let e = sw.Elapsed.TotalMilliseconds
            sw.Restart()
            if e <> 0.0 then
                sum <- sum + e
                count <- count + 1

        let t = new Timer(TimerCallback(tick), null, 0u, 1u)

        Thread.Sleep(5000)
        t.Dispose()

        printfn "timer: %fms" (sum / float count)

    let minAsyncSleep() =
        async {
            let iter = 1000
            let sw = Stopwatch()
            sw.Start()
            for i in 1 .. iter do
                do! Async.Sleep(1)
            sw.Stop()

            printfn "async %fms" (sw.Elapsed.TotalMilliseconds / float iter)
        } |> Async.RunSynchronously

    let minSleep() =
        let w = SpinWait()
        let sleep (ms : float) =
            let sw = Stopwatch.StartNew()
            while sw.Elapsed.TotalMilliseconds < ms do
                ()
                
        timeBeginPeriod(1u)
        
        let thread = new Thread(ThreadStart(fun () ->

            let milliseconds = 1L
            let mutable delay =  -1L //if milliseconds > 1L then -10000L * (milliseconds - 1L) else -1L
            for i in 1 .. 20 do
                NtDelayExecution(false, &&delay)

            let iter = 1000
            let sw = Stopwatch()

            sw.Start()
            for i in 1 .. iter do
                NtDelayExecution(false, &&delay)
            sw.Stop()

            printfn "sleep %fms" (sw.Elapsed.TotalMilliseconds / float iter)
        
        ))
        thread.Start()
        thread.Join()

        timeEndPeriod(1u)

let sepp (a : nativeptr<int>) =
    NativePtr.write a 10

let a (x : int) =
    let mutable x = 1
    let ptr = sepp &&x
    5


module Proc =
    open Aardvark.Base.Monads.State

    [<RequireQualifiedAccess>]
    type ProcResult<'a> =
        | Cancelled
        | Faulted of exn
        | Done of 'a

    type ProcState<'s> =
        {
            ct : CancellationToken
            state : 's
        }

    type proc<'s, 'a> = 
        abstract member ContinueWith : ProcState<'s> * (ProcState<'s> -> ProcResult<'a> -> unit) -> unit

    module Proc =
        [<AbstractClass>]
        type private Proc<'s, 'a>() =
            
            abstract member ContinueWith : ProcState<'s> * (ProcState<'s> -> ProcResult<'a> -> unit) -> unit
            
            interface proc<'s, 'a> with
                member x.ContinueWith(s, cont) =
                    if s.ct.IsCancellationRequested then
                        cont s ProcResult.Cancelled
                    else
                        x.ContinueWith(s, cont)
            
        let inline private create<'s, 'a> (f : ProcState<'s> -> (ProcState<'s> -> ProcResult<'a> -> unit) -> unit) =
            { new Proc<'s, 'a>() with
                override x.ContinueWith(s,cont) = f s cont
            } :> proc<_,_>
                        
        let map (f : 'a -> 'b) (p : proc<'s, 'a>) =
            create<'s, 'b> <| fun s cont ->
                p.ContinueWith(s, fun s v ->
                    match v with
                        | ProcResult.Cancelled -> cont s ProcResult.Cancelled
                        | ProcResult.Faulted e -> cont s (ProcResult.Faulted e)
                        | ProcResult.Done v -> 
                            if s.ct.IsCancellationRequested then 
                                cont s ProcResult.Cancelled
                            else    
                                try 
                                    cont s (ProcResult.Done (f v))
                                with 
                                    | :? OperationCanceledException -> cont s ProcResult.Cancelled
                                    | e -> cont s (ProcResult.Faulted e)
                )

        let mapS (f : 'a -> State<'s, 'b>) (p : proc<'s, 'a>) =
            create<'s, 'b> <| fun s cont ->
                p.ContinueWith(s, fun s v ->
                    match v with
                        | ProcResult.Cancelled -> cont s ProcResult.Cancelled
                        | ProcResult.Faulted e -> cont s (ProcResult.Faulted e)
                        | ProcResult.Done v -> 
                            if s.ct.IsCancellationRequested then 
                                cont s ProcResult.Cancelled
                            else 
                                let mutable state = s.state
                                try
                                    let res = f(v).Run(&state)
                                    cont { s with state = state } (ProcResult.Done res)
                                with 
                                    | :? OperationCanceledException -> cont { s with state = state } ProcResult.Cancelled
                                    | e -> cont { s with state = state } (ProcResult.Faulted e)
                )

        let bind (f : 'a -> proc<'s, 'b>) (p : proc<'s, 'a>) =
            create<'s, 'b> <| fun s cont -> 
                p.ContinueWith(s, fun s r ->
                    match r with
                        | ProcResult.Cancelled -> cont s ProcResult.Cancelled
                        | ProcResult.Faulted e -> cont s (ProcResult.Faulted e)
                        | ProcResult.Done(v) ->
                            try
                                let res = f(v)
                                res.ContinueWith(s, cont)
                            with 
                                | :? OperationCanceledException -> cont s ProcResult.Cancelled
                                | e -> cont s (ProcResult.Faulted e)
                )  

        let value (v : 'a) =
            create<'s, 'a> <| fun s cont ->
                cont s (ProcResult.Done v)

        let delay (f : unit -> proc<'s, 'a>) =
            create<'s, 'a> <| fun s cont ->
                try f().ContinueWith(s, cont)
                with
                    | :? OperationCanceledException -> cont s ProcResult.Cancelled
                    | e -> cont s (ProcResult.Faulted e)

        let combine (l : proc<'s, unit>) (r : proc<'s, 'a>) =
            create<'s, 'a> <| fun s cont ->
                l.ContinueWith(s, fun s res ->
                    match res with
                        | ProcResult.Cancelled -> cont s ProcResult.Cancelled 
                        | ProcResult.Faulted e -> cont s (ProcResult.Faulted e)
                        | ProcResult.Done () -> r.ContinueWith(s, cont)
                )

    type ProcBuilder() =
        member x.Bind(m : proc<'s, 'a>, f : 'a -> proc<'s, 'b>) =
            Proc.bind f m

        member x.Return(v : 'a) =
            Proc.value v

        member x.Zero() =
            Proc.value ()

        member x.Delay (f : unit -> proc<'s, 'a>) =
            Proc.delay f

        member x.Combine(l : proc<'s, unit>, r : proc<'s, 'a>) =
            Proc.combine l r



[<EntryPoint>]
[<STAThread>]
let main args =
//    let cam = Mod.init V3d.Zero
//    let priority (op : SetOperation<V3d>) =
//        
//        match op with 
//            | Add(_,v) -> V3d.Distance(cam.Value, v) / 100.0
//            | Rem(_,v) -> V3d.Distance(cam.Value, v)
//
//    let queue = ConcurrentDeltaPriorityQueue<V3d, float>(fun s -> priority s)
//
//
//    queue.Enqueue(Add(V3d.III))
//    queue.Enqueue(Add(V3d(2,3,4)))
//    queue.Enqueue(Add(V3d(4,3,4)))
//    queue.Enqueue(Rem(V3d(0,1,2)))
//
//    while queue.Count > 0 do
//        let e = queue.Dequeue()
//        printfn "%A" e
//        
//
//    let rand = RandomSystem()
//
//    let log = @"C:\Users\Schorsch\Desktop\updateHeap.csv"
//    for s in 1000 .. 1000 .. 50000 do
//        printf "%d: " s
//        let queue = ConcurrentDeltaPriorityQueue<V3d, float>(fun s -> priority s)
//        for i in 1 .. s do
//            queue.Enqueue(Add(rand.UniformV3d()))
//    
//        let sw = System.Diagnostics.Stopwatch()
//        sw.Start()
//        for i in 1 .. 100 do
//            transact (fun () -> cam.Value <- rand.UniformV3d())
//            let hist = queue.UpdatePriorities()
//            ()
//        sw.Stop()
//
//        File.AppendAllLines(log, [sprintf "%d;%f" s (sw.Elapsed.TotalMilliseconds / 100.0)])
//        printfn "%A" (sw.MicroTime / 100.0)
//
////
////    while queue.Count > 0 do
////        let e = queue.Dequeue()
////        printfn "%A" e
////
//
//    Environment.Exit 0


    //TimerTest.mmTimer()
    //
    //TimerTest.timerPrecision()
    //TimerTest.minAsyncSleep()
    //TimerTest.minSleep()
    //Environment.Exit 0
    //Examples.Tutorial.run()
    //Examples.Instancing.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    //Examples.ComputeTest.run()
    Ag.initialize()
    Aardvark.Init()
    Examples.LoD.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.ShaderSignatureTest.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
