open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application

// This example shows how to write tesselation shaders.
// Thanks to Bernhard Rainer for providing the code.

// typically we use extra modules for specifying FShade shaders
module Shader =
    open FShade // open FShade namespace
    // this one makes vertex structs available, which are used by the standard
    // shaders (DefaultSurfaces.*)
    open Aardvark.Base.Rendering.Effects

    // Helper functions need to be reflectable for fshade in order to generate code
    // (you can also put ReflectedDefinition on the whole module)
    [<ReflectedDefinition>]
    let lerp3_ (v0 : V4d)(v1 : V4d)(v2 : V4d)(coord : V3d) = 
        v0 * coord.X + v1 * coord.Y + v2 * coord.Z

    [<ReflectedDefinition>]
    let lerp3 (v0 : V3d)(v1 : V3d)(v2 : V3d)(coord : V3d) = 
        v0 * coord.X + v1 * coord.Y + v2 * coord.Z

    //                                 |  the shader uses patches of size 3 (3 here is a type level literal)
    //                                 v  
    let tesselationShader(triangle : Patch<3 N, Vertex>) =
        // tesselation shaders run in the tesselation computation expression builder
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

[<EntryPoint>]
let main argv = 
    
    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    let win = 
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug false
            samples 8
        }
    
    // use tesselation 4 as base tesselation (rest is done in tesselation shader adaptively)
    let model = Sg.unitSphere' 4 C4b.Red
        
    let sg =   
        model   
            |> Sg.effect [
                    // No Vertex shader since the tessellation evaluation shader adopts the final vertex transform
                    Shader.tesselationShader        |> toEffect       
                    DefaultSurfaces.vertexColor     |> toEffect
                    DefaultSurfaces.simpleLighting  |> toEffect
                ]
             |> Sg.blendMode (Rendering.BlendMode.Blend |> Mod.constant )
             |> Sg.fillMode  (FillMode.Line             |> Mod.constant)
             |> Sg.cullMode  (CullMode.Back        |> Mod.constant)
             |> Sg.trafo     (Trafo3d.Identity          |> Mod.constant)
             |> Sg.uniform   "ViewportSize"         win.Sizes
             |> Sg.uniform   "MaxEdgeLengthInPixel" (50 |> Mod.constant )
    

    win.Scene <- sg
    win.Run()

    0
