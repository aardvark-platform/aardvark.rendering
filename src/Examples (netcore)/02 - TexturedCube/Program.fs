open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Rendering.Text
open System


module Shader =
    open FShade
    open Aardvark.Base.Rendering.Effects

    type UniformScope with
        // constant
        member x.TextSize : float = uniform?TextSize
        member x.TextWorldPos : V3d = uniform?TextWorldPos
        // per frame
        member x.Hfov : float = uniform?hf?Hfov
        member x.RealViewTrafo : M44d = uniform?hf?RealViewTrafo
        member x.RealProjTrafo : M44d = uniform?hf?RealProjTrafo
        member x.CameraShiftPos : V3d = uniform?hf?CameraShiftPos

    [<GLSLIntrinsic("float(distance(dvec3({0}),dvec3({1})));"); KeepCall; Inline>] 
    let inline GetDistanceHighPercision (a: 'a) (b : 'a) : 'c = 
        failwith ""

    [<GLSLIntrinsic("vec3(dvec3({0}) - dvec3({1}));"); KeepCall; Inline>] 
    let inline GetDiffHighPercision (a: 'a) (b : 'a) : 'c = 
        failwith ""

    [<GLSLIntrinsic("break;"); KeepCall; Inline>] 
    let inline private brk() = 
        onlyInShaderCode<unit> "break"

    let cameraAlignedConstantTextSize (p : Vertex) = 
      vertex {

        let wp = uniform.TextWorldPos // currently local-pos (shifted space)

        // constant pixel scale and positioning within scene
        let hfov_rad = uniform.Hfov * Constant.RadiansPerDegree
        let wz = tan (hfov_rad / 2.0) * ((2.0 * uniform.TextSize) / float uniform.ViewportSize.Y)
        let dist = GetDistanceHighPercision wp uniform.CameraShiftPos
        let invarScale = wz * dist

        // screenAligned rotation
        let right = uniform.RealViewTrafo.R0.XYZ
        let up    = uniform.RealViewTrafo.R1.XYZ
        let forw  = uniform.RealViewTrafo.R2.XYZ * -1.0

        let screenAlignedTrafo = 
          new M33d(
            right.X, up.X, forw.X,
            right.Y, up.Y, forw.Y,
            right.Z, up.Z, forw.Z)

        let alinged = screenAlignedTrafo * p.pos.XYZ
        let scaled = invarScale * alinged
        let shifted = wp + scaled
        let newPos = uniform.RealProjTrafo * uniform.RealViewTrafo * V4d(shifted, 1.0)

        return { p with pos = newPos }
      }    

[<EntryPoint>]
let main argv = 
    
        
    let win =
        window {
            backend Backend.GL
            display Display.Mono
            debug false
            samples 8
        }

    // first we need to initialize Aardvark's core components
    Ag.initialize()
    Aardvark.Init()

    // lets define the bounds/color for our box
    // NOTE that the color is going to be ignored since we're using a texture
    let box = Box3d(-V3d.III, V3d.III)
    let color = C4b.Red

    //let textScreenAlignedConstantSize (localPos:IMod<V3d>) (textconfig:TextConfig) (text:IMod<string>)  = 
 
    //  Sg.textWithConfig { textconfig with flipViewDependent = false; renderStyle = RenderStyle.Billboard } text
    //      |> Sg.trafo (Mod.constant(Trafo3d.Identity))
    //      |> Sg.viewTrafo (Mod.constant(Trafo3d.Identity))
    //      |> Sg.projTrafo (Mod.constant(Trafo3d.Identity))
    //      |> Sg.uniform "TextWorldPos" localPos


    let view = win.View |> Mod.map(fun x -> x.[0])
    let proj = win.Proj |> Mod.map(fun x -> x.[0])

    
    let ranStr n : string = 
        let r = new System.Random()
        System.String(Array.init n (fun _ -> char (r.Next(97,123)))).ToUpper()

    //let text =
    //  [ 
    //    for i in 1..10..400 do
    //      for j in 1..10..400 do 
    //        yield textScreenAlignedConstantSize (Mod.constant(V3d(i, j, 0))) TextConfig.Default (Mod.constant (ranStr 5))
    //  ]   
    //    |> Sg.ofList
    //    |> Sg.afterEffect [ toEffect Shader.cameraAlignedConstantTextSize ]
    //    |> Sg.uniform "CameraShiftPos" (view |> Mod.map(fun x -> x.GetViewPosition()))
    //    |> Sg.uniform "RealViewTrafo" view
    //    |> Sg.uniform "TextSize" (Mod.constant 10.0)
    //    |> Sg.uniform "Hfov" (Mod.constant 60.0)
    //    |> Sg.uniform "RealProjTrafo" proj

    let text =

      let instances =
        ASet.ofList [ 
          let rand = RandomSystem()
          for i in -400..20..400 do
            for j in -400..20..400 do 
              for k in -400..20..400 do 
                let text = Mod.constant (ranStr 5)
                let t = 
                  Trafo3d.Scale(0.9 * rand.UniformDouble() + 0.1) *
                  Trafo3d.Translation (float i, float j,  float k) 
                  |> Mod.constant
                yield t, text
        ]

      Sg.textsWithConfig { TextConfig.Default with font = Font "Comic Sans MS"; renderStyle = RenderStyle.Billboard }  instances
      |> Sg.uniform "DepthBias" (Mod.constant 0.0)
      //[ 
      //  for i in 1..10..400 do
      //    for j in 1..10..400 do 
      //      yield 
              
      //         //Sg.box (Mod.constant color) (Mod.constant box)
      //         // |> Sg.shader {
      //         //     do! DefaultSurfaces.trafo
      //         //     do! DefaultSurfaces.diffuseTexture
      //         // }
      //         // |> Sg.trafo (Mod.constant(Trafo3d.Translation(V3d(i,j,0))))
      //        Sg.textWithConfig { TextConfig.Default with font = Font "Kunstler Script" } (Mod.constant (ranStr 5)) 
      //        |> Sg.translate (float i )(float j )(float 0 )
      //]|> Sg.ofList

    let boxSG = 
        // thankfully aardvark defines a primitive box
        Sg.box (Mod.constant color) (Mod.constant box)

            // apply the texture as "DiffuseTexture"
            |> Sg.diffuseTexture DefaultTextures.checkerboard

            // apply a shader ...
            // * transforming all vertices
            // * looking up the DiffuseTexture 
            // * applying a simple lighting to the geometry (headlight)
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }

    let sg = Sg.ofList[boxSG; text]


    let run () =
        win.Scene <- sg
        win.Run()
    
    run()

    0
