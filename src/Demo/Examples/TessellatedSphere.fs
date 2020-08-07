namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open FSharp.Data.Adaptive.Operators
open Aardvark.Rendering.Vulkan
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.Text

open Aardvark.Rendering.Effects

module TessellatedSphere =
    
    module Shader =
        open FShade

        [<ReflectedDefinition>]
        let lerp3_ (v0 : V4d)(v1 : V4d)(v2 : V4d)(coord : V3d) = 
            v0 * coord.X + v1 * coord.Y + v2 * coord.Z

        [<ReflectedDefinition>]
        let lerp3 (v0 : V3d)(v1 : V3d)(v2 : V3d)(coord : V3d) = 
            v0 * coord.X + v1 * coord.Y + v2 * coord.Z


        let tesselationShader(triangle : Patch<3 N, Vertex>) =

             tessellation  {
                
                let viewportSize = V2d uniform.ViewportSize
                let maxEdgeLengthInPixel = float uniform?MaxEdgeLengthInPixel

                let v0 = triangle.[0]
                let v1 = triangle.[1]
                let v2 = triangle.[2]

                // Transform positions to screen space
                let sp0 = uniform.ModelViewProjTrafo * V4d(v0.pos.XYZ , 1.0)
                let sp1 = uniform.ModelViewProjTrafo * V4d(v1.pos.XYZ , 1.0)
                let sp2 = uniform.ModelViewProjTrafo * V4d(v2.pos.XYZ , 1.0)


                // Transform into image space
                let pp0 = (sp0.XY / sp0.W * 0.5 + 0.5 ) * viewportSize
                let pp1 = (sp1.XY / sp1.W * 0.5 + 0.5 ) * viewportSize
                let pp2 = (sp2.XY / sp2.W * 0.5 + 0.5 ) * viewportSize
            
                // Edge vectors in image space
                let v01 = pp1 - pp0
                let v02 = pp2 - pp0
                let v12 = pp2 - pp1
            
                // Calculate length of each line (in pixels) in order to control the tessellation factors
                let outer01 = ceil (v01.Length / maxEdgeLengthInPixel )
                let outer02 = ceil (v02.Length / maxEdgeLengthInPixel )
                let outer12 = ceil (v12.Length / maxEdgeLengthInPixel )
                
                let inner   = ceil (outer01 +  outer02 + outer12) / 3.0

                // Perform Tessellation 
                // Here, the GLSL tessellation control shader ends and the tessellation evaluation shader begins
                let! coord = tessellateTriangle inner (outer12, outer02, outer01)


                // Interpolate Vertex according to the barycentric coordinates from the tessellator
                let pos = lerp3  triangle.[0].pos.XYZ triangle.[1].pos.XYZ triangle.[2].pos.XYZ coord
                let c   = lerp3_ triangle.[0].c triangle.[1].c triangle.[2].c coord
                let n   = lerp3  triangle.[0].n triangle.[1].n triangle.[2].n coord

                // Send new vertex to the surface of the sphere
                let pos = pos.Normalized

                // Transform vertex to world space
                let n   = uniform.NormalMatrix * n
                let wp  = uniform.ModelTrafo * V4d(pos, 1.0)

                // Transform the vertex to screen space
                let pos = uniform.ViewProjTrafo * wp

                return 
                    { v0 with
                        wp  = wp
                        pos = pos
                        c   = c
                        n   = n
                    }
                }


    let run() =
        //use app = new VulkanApplication(true)
        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(8)


        let cameraView  =  DefaultCameraController.control win.Mouse win.Keyboard win.Time (CameraView.LookAt(V3d.III, V3d.OOO, V3d.OOI))    
        let frustum     =  win.Sizes    |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))       
            
        let viewTrafo   = cameraView    |> AVal.map CameraView.viewTrafo
        let projTrafo   = frustum       |> AVal.map Frustum.projTrafo        

        
        let model = Sg.unitSphere' 4 C4b.DarkBlue
        
        let sg =   
            model   |> Sg.effect [
                            // No Vertex shader since the tessellation evaluation shader adopts the final vertex transform
                            Shader.tesselationShader        |> toEffect       
                            DefaultSurfaces.vertexColor     |> toEffect
                            DefaultSurfaces.simpleLighting  |> toEffect
                                ]
                    |> Sg.blendMode (BlendMode.Blend |> AVal.constant )
                    |> Sg.fillMode  (FillMode.Line |> AVal.constant)
                    |> Sg.cullMode  (CullMode.Back |> AVal.constant)
                    |> Sg.trafo     (Trafo3d.Identity |> AVal.constant)
                    |> Sg.viewTrafo viewTrafo
                    |> Sg.projTrafo projTrafo
                    |> Sg.uniform   "ViewportSize" win.Sizes
                    |> Sg.uniform   "MaxEdgeLengthInPixel" (50 |> AVal.constant )
                    
    
        let clearTask = app.Runtime.CompileClear(win.FramebufferSignature, AVal.constant C4f.White, AVal.constant 1.0)

        let renderTask =
            app.Runtime.CompileRender(win.FramebufferSignature,BackendConfiguration.Default, sg)
                //|> DefaultOverlays.withStatistics
        

        win.RenderTask <- RenderTask.ofList [clearTask; renderTask]


        win.Run()
        win.Dispose()

