namespace Aardvark.Rendering.Vulkan

open Aardvark.Base

type ILogger =
    abstract member section<'a, 'x>     : Printf.StringFormat<'a, (unit -> 'x) -> 'x> -> 'a
    abstract member line<'a, 'x>        : Printf.StringFormat<'a, unit> -> 'a
    abstract member WithVerbosity       : int -> ILogger
    abstract member Verbosity           : int

type Logger private(verbosity : int) =
    static let instances = Array.init 6 (fun i -> Logger(i) :> ILogger)

    static member Default = instances.[2]
    static member Get v = instances.[v]

    interface ILogger with
        member x.Verbosity = verbosity
        member x.WithVerbosity(v) = instances.[v]
        member x.section (fmt : Printf.StringFormat<'a, (unit -> 'x) -> 'x>) =
            fmt |> Printf.kprintf (fun (str : string) ->
                fun cont ->
                    try
                        Report.Begin(verbosity, "{0}", str)
                        cont()
                    finally
                        Report.End(verbosity) |> ignore
            )

        member x.line fmt = Printf.kprintf (fun str -> Report.Line(verbosity, str)) fmt

module internal Log =

    module Vulkan =
        let warn fmt = Printf.kprintf (fun str -> Report.Warn("[Vulkan] {0}", str)) fmt
        let debug fmt = Printf.kprintf (fun str -> Report.Line(2, "[Vulkan] {0}", str)) fmt