namespace Aardvark.Rendering.Interactive

[<AutoOpen>]
module FsiSetup =

    open System
    open System.Threading

    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering
    open Aardvark.Rendering.NanoVg
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    open Aardvark.Application
    open Aardvark.Application.WinForms

    let mutable defaultCamera = true

    let mutable private initialized = 0

    let init entryDir =
        let entry = "Aardvark.Rendering.Interactive.dll"
        let entryPath = Path.combine [entryDir; entry] 
        if entryPath |> System.IO.File.Exists then
            printfn "using %s as entry assembly." entryPath
        else failwithf "could not find entry assembly: %s" entryPath
        
        if Interlocked.Exchange(&initialized, 1) = 0 then
            #if INTERACTIVE
            System.Environment.CurrentDirectory <- entryDir
            IntrospectionProperties.CustomEntryAssembly <- 
                System.Reflection.Assembly.LoadFile(entryPath)
            #endif

            Ag.initialize()
            Aardvark.Base.Ag.unpack <- fun o ->
                    match o with
                        | :? IMod as o -> o.GetValue()
                        | _ -> o

            Aardvark.Init()

    let initFsi entryPath =
        if entryPath |> System.IO.File.Exists then
            printfn "using %s as entry assembly." entryPath
        else failwithf "could not find entry assembly: %s" entryPath
        
        if Interlocked.Exchange(&initialized, 1) = 0 then
            System.Environment.CurrentDirectory <- System.IO.Path.GetDirectoryName entryPath
            IntrospectionProperties.CustomEntryAssembly <- 
                System.Reflection.Assembly.LoadFile(entryPath)

            Ag.initialize()
            Aardvark.Base.Ag.unpack <- fun o ->
                    match o with
                        | :? IMod as o -> o.GetValue()
                        | _ -> o

            Aardvark.Init()
 