namespace Aardvark.Base

open System

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
