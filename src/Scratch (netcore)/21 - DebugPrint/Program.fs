open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application

module Config =
    let Backend = Backend.Vulkan

    let Debug : IDebugConfig =
        match Backend with
        | Backend.GL -> DebugLevel.Normal
        | Backend.Vulkan ->
            { Vulkan.DebugConfig.Normal with
                ValidationLayer = Some Vulkan.ValidationLayerConfig.DebugPrint
                PrintRenderTaskRecompile = false
                VerifyShaderCacheIntegrity = true }

module Shader =
    open FShade

    let printPosition (v : Effects.Vertex) =
        vertex {
            Debug.Printfn("P = %v4f", v.pos)
            return v
        }

[<EntryPoint>]
let main argv =
    Aardvark.Init()

    let win =
        window {
            backend Config.Backend
            display Display.Mono
            debug Config.Debug
            samples 8
        }

    let sg =
        Sg.quad
            |> Sg.shader {
                do! DefaultSurfaces.trafo

                if Config.Backend = Backend.Vulkan then
                    do! Shader.printPosition

                do! DefaultSurfaces.constantColor C4f.IndianRed
                do! DefaultSurfaces.simpleLighting
            }

    win.Scene <- sg
    win.Run()

    0
