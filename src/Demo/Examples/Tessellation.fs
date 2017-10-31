namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering.Text


module Tessellation =
    
    module Shader =
        open FShade

        type Vertex = { [<Position>] p : V4d; [<Color>] c : V4d }

        let tess (v : Patch<3 N, Vertex>) =
            tessellation {

                
                let level = 64.0
                let! coord = tessellateTriangle level (level,level,level)

                let pos = coord.X * v.[0].p + coord.Y * v.[1].p + coord.Z * v.[2].p
                return { p = pos; c = V4d(0.5 * (pos.XYZ + V3d.III), 1.0) }

            }

    let run() =
        use app = new VulkanApplication(true)
        let win = app.CreateSimpleRenderWindow(8)

        let geometry =
            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,
                IndexArray = ([| 0;1;2; 2;1;3|] :> Array),
                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, [| V3f(-1,-1,0); V3f(1,-1,0); V3f(-1,1,0); V3f(1,1,0) |] :> Array
                    ]
            )

        win.RenderTask <-
            geometry
                |> Sg.ofIndexedGeometry
                |> Sg.shader {
                    do! Shader.tess
                }
                |> Sg.fillMode (Mod.constant FillMode.Line)
                |> Sg.compile app.Runtime win.FramebufferSignature


        win.Run()
        win.Dispose()

