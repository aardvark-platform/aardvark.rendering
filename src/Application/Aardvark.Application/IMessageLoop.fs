namespace Aardvark.Application

open System
open System.Runtime.CompilerServices
open System.Threading.Tasks
open Aardvark.Base

type IMessageLoop =
    abstract member Enqueue : wait : TimeSpan * action : Action<TimeSpan> -> IDisposable

[<AbstractClass; Sealed; Extension>]
type MessageLoopExtensions private() =

    [<Extension>]
    static member Invoke(x : IMessageLoop, wait : TimeSpan, action : Func<TimeSpan, 'a>) =
        let s = TaskCompletionSource<'a>()
        x.Enqueue(wait, Action<TimeSpan>(fun r -> s.SetResult(action.Invoke(r)))) |> ignore
        s.Task

    [<Extension>]
    static member Invoke(x : IMessageLoop, wait : TimeSpan, action : Action<TimeSpan>) =
        let s = TaskCompletionSource<unit>()
        x.Enqueue(wait, Action<TimeSpan>(fun r -> s.SetResult(action.Invoke(r)))) |> ignore
        s.Task :> Task
       
    [<Extension>]
    static member Invoke(x : IMessageLoop, wait : TimeSpan, action : Action) =
        MessageLoopExtensions.Invoke(x, wait, Func<TimeSpan, unit>(fun _ -> action.Invoke())) :> Task

    [<Extension>]
    static member Invoke(x : IMessageLoop, wait : TimeSpan, action : Func<'a>) =
        MessageLoopExtensions.Invoke(x, wait, Func<TimeSpan, 'a>(fun _ -> action.Invoke()))
       


    [<Extension>]
    static member Invoke(x : IMessageLoop, action : Action<TimeSpan>) =
        MessageLoopExtensions.Invoke(x, TimeSpan.Zero, Func<TimeSpan, unit>(action.Invoke)) :> Task
       
    [<Extension>]
    static member Invoke(x : IMessageLoop, action : Action) =
        MessageLoopExtensions.Invoke(x, TimeSpan.Zero, Func<TimeSpan, unit>(fun _ -> action.Invoke())) :> Task

    [<Extension>]
    static member Invoke(x : IMessageLoop, action : Func<TimeSpan,'a>) =
        MessageLoopExtensions.Invoke(x, TimeSpan.Zero, action)
       
    [<Extension>]
    static member Invoke(x : IMessageLoop, action : Func<'a>) =
        MessageLoopExtensions.Invoke(x, TimeSpan.Zero, Func<TimeSpan, 'a>(fun _ -> action.Invoke()))
       




    [<Extension>]
    static member Enqueue(x : IMessageLoop, action : Action<TimeSpan>) =
        x.Enqueue(TimeSpan.Zero, action)

    [<Extension>]
    static member EnqueuePeriodic(x : IMessageLoop, wait : TimeSpan, interval : TimeSpan, action : Action<TimeSpan>) =
        let self = ref null
        let disp = ref null

        self := 
            Action<TimeSpan>(fun r ->
                disp := x.Enqueue(interval, action)
                action.Invoke(r)
            )

        disp := x.Enqueue(wait, !self)
        { new IDisposable with member x.Dispose() = disp.Value.Dispose() }

    [<Extension>]
    static member EnqueuePeriodic(x : IMessageLoop, interval : TimeSpan, action : Action<TimeSpan>) =
        MessageLoopExtensions.EnqueuePeriodic(x, TimeSpan.Zero, interval, action)

[<AutoOpen>]
module ``F# MessageLoop Extensions`` =
    type IMessageLoop with

        member x.Invoke (wait : TimeSpan, action : TimeSpan -> 'a) =
            Async.FromContinuations(fun (succes, error, cancel) -> 
                
                x.Enqueue(wait, fun tr ->
                    try 
                        succes (action tr)
                    with 
                        | :? OperationCanceledException as e -> cancel e
                        | e -> error e
                        
                ) |> ignore
      
            )

        member x.Invoke (wait : TimeSpan, action : unit -> 'a) =
            x.Invoke(wait, fun _ -> action()) 

        member x.Invoke (action : TimeSpan -> 'a) =
            x.Invoke(TimeSpan.Zero, action)

        member x.Invoke (action : unit -> 'a) =
            x.Invoke(TimeSpan.Zero, action)

        // all overloads need to be specialized for unit since F#
        // will otherwise take the Action-overloads from above
        member x.Invoke (wait : TimeSpan, action : TimeSpan -> unit) =
            x.Invoke(wait, action)

        member x.Invoke (wait : TimeSpan, action : unit -> unit) =
            x.Invoke(wait, action) 

        member x.Invoke (action : TimeSpan -> unit) =
            x.Invoke(action)

        member x.Invoke (action : unit -> unit) =
            x.Invoke(action) 



        member x.Enqueue(wait : TimeSpan, action : TimeSpan -> unit) =
            x.Enqueue(wait, Action<TimeSpan>(action))      
             
        member x.Enqueue(action : TimeSpan -> unit) =
            x.Enqueue(TimeSpan.Zero, Action<TimeSpan>(action))    

        member x.EnqueuePeriodic(wait : TimeSpan, interval : TimeSpan, action : TimeSpan -> unit) =
            x.EnqueuePeriodic(wait, interval, Action<TimeSpan>(action))    

        member x.EnqueuePeriodic(interval : TimeSpan, action : TimeSpan -> unit) =
            x.EnqueuePeriodic(TimeSpan.Zero, interval, Action<TimeSpan>(action))    

module Test =
    let a (m : IMessageLoop) =
        m.Invoke (fun () -> ())

//[<AutoOpen>]
//module private TimeRangeConstants =
//    let printThreshold = 0.5
//    let ticksPerSecond = float TimeSpan.TicksPerSecond
//    let ticksPerHour = ticksPerSecond * 3600.0
//    let ticksPerMinute = ticksPerSecond * 60.0
//    let ticksPerMillisecond = ticksPerSecond / 1.0E3
//    let ticksPerMicrosecond = ticksPerSecond / 1.0E6
//    let ticksPerNanosecond = ticksPerSecond / 1.0E9
//    let ticksPerPicosecond = ticksPerSecond / 1.0E12
//    let ticksPerFemtosecond = ticksPerSecond / 1.0E15    
//
//[<CustomEquality; CustomComparison>]
//type TimeRange =
//    struct
//        val public Ticks : float
//
//        static member TicksPerHour          = ticksPerHour
//        static member TicksPerMinute        = ticksPerMinute
//        static member TicksPerSecond        = ticksPerSecond
//        static member TicksPerMillisecond   = ticksPerMillisecond
//        static member TicksPerMicrosecond   = ticksPerMicrosecond
//        static member TicksPerNanosecond    = ticksPerNanosecond
//        static member TicksPerPicosecond    = ticksPerPicosecond
//        static member TicksPerFemtosecond   = ticksPerFemtosecond
//
//        static member FromHours (h : float)             = TimeRange(h * ticksPerHour)
//        static member FromMinutes (m : float)           = TimeRange(m * ticksPerMinute)
//        static member FromSeconds (s : float)           = TimeRange(s * ticksPerSecond)
//        static member FromMilliseconds (ms : float)     = TimeRange(ms * ticksPerMillisecond)
//        static member FromMicroseconds (µs: float)      = TimeRange(µs * ticksPerMicrosecond)
//        static member FromNanoseconds (ns : float)      = TimeRange(ns * ticksPerNanosecond)
//        static member FromPicoseconds (ps : float)      = TimeRange(ps * ticksPerPicosecond)
//        static member FromFemtoseconds (fs : float)     = TimeRange(fs * ticksPerFemtosecond)
//
//
//        member x.Hours          = x.Ticks / ticksPerHour
//        member x.Minutes        = x.Ticks / ticksPerMinute
//        member x.Seconds        = x.Ticks / ticksPerSecond
//        member x.Milliseconds   = x.Ticks / ticksPerMillisecond
//        member x.Microseconds   = x.Ticks / ticksPerMicrosecond
//        member x.Nanoseconds    = x.Ticks / ticksPerNanosecond
//        member x.Picoseconds    = x.Ticks / ticksPerPicosecond
//        member x.Femtoseconds   = x.Ticks / ticksPerFemtosecond
//
//        interface IComparable with
//            member x.CompareTo o =
//                match o with
//                    | :? TimeRange as o -> compare x.Ticks o.Ticks
//                    | _ -> failwith "uncomparable"
//
//        override x.GetHashCode() =
//            x.Ticks.GetHashCode()
//
//        override x.Equals(o) =
//            match o with
//                | :? TimeRange as o -> x.Ticks = o.Ticks
//                | _ -> false
//
//        override x.ToString() =
//            let seconds = x.Ticks / ticksPerSecond
//            if seconds >= 60.0 then
//                // hh:mm:ss.ss
//                let minutes = floor (seconds / 60.0)
//                let seconds = seconds - 60.0 * minutes
//
//                let hours = floor (minutes / 60.0)
//                let minutes = minutes - 60.0 * hours
//
//                if hours > 0.0 then
//                    sprintf "%.0f:%.0f:%.0f" hours minutes seconds
//                else
//                    sprintf "%.0f:%.2f" minutes seconds
//                
//            else
//                if seconds = 0.0 then "0"
//                else if seconds > printThreshold then sprintf "%.3fs" seconds
//                elif seconds > printThreshold * 1E-3 then sprintf "%.2fms" (seconds * 1.0E3)
//                elif seconds > printThreshold * 1E-6 then sprintf "%.2fµs" (seconds * 1.0E6)
//                elif seconds > printThreshold * 1E-9 then sprintf "%.1fns" (seconds * 1.0E9)
//                elif seconds > printThreshold * 1E-12 then sprintf "%.1fps" (seconds * 1.0E12)
//                else sprintf "%.0ffs" (seconds * 1.0E15)
//
//        static member Zero = TimeRange(0.0)
//
//        static member (+) (l : TimeRange, r : TimeRange) =
//            TimeRange(l.Ticks + r.Ticks)
//
//        static member (-) (l : TimeRange, r : TimeRange) =
//            TimeRange(l.Ticks - r.Ticks)
//
//        static member (*) (l : TimeRange, factor : float) =
//            TimeRange(l.Ticks * factor)
//
//        static member (*) (factor : float, r : TimeRange) =
//            TimeRange(factor * r.Ticks)
//
//        static member (*) (l : TimeRange, r : TimeRange) =
//            TimeRange(l.Ticks * r.Ticks)
//
//        static member (/) (l : TimeRange, factor : float) =
//            TimeRange(l.Ticks / factor)
//
//        static member (/) (l : TimeRange, r : TimeRange) =
//            l.Ticks / r.Ticks
//
//
//        static member (+) (l : TimeRange, r : TimeSpan) =
//            TimeRange(l.Ticks + float r.Ticks)
//
//        static member (+) (l : TimeSpan, r : TimeRange) =
//            TimeRange(float l.Ticks + r.Ticks)
//
//        static member (-) (l : TimeRange, r : TimeSpan) =
//            TimeRange(l.Ticks - float r.Ticks)
//
//        static member (-) (l : TimeSpan, r : TimeRange) =
//            TimeRange(float l.Ticks - r.Ticks)
//
//        static member (*) (l : TimeRange, r : TimeSpan) =
//            TimeRange(l.Ticks * float r.Ticks)
//
//        static member (*) (l : TimeSpan, r : TimeRange) =
//            TimeRange(float l.Ticks * r.Ticks)
//
//        static member (/) (l : TimeRange, r : TimeSpan) =
//            l.Ticks / float r.Ticks
//
//        static member (/) (l : TimeSpan, r : TimeRange) =
//            float l.Ticks / r.Ticks
//
//
//        new(span : TimeSpan) = { Ticks = float span.Ticks }
//        new(ticks : float) = { Ticks = ticks }
//    end
