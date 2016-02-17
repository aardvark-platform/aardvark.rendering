namespace Rendering.Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Rendering.GL

module Shaders =
    open FShade

    type Vertex = { [<TexCoord>] tc : V2d; [<Position>] p : V4d; [<WorldPosition>] wp : V4d }

    let input =
        sampler2d {
            texture uniform?DiffuseColorTexture
            filter Filter.MinMagMipPoint
        }

//    let cube =
//        SamplerCube(uniform?CubeMap, { SamplerState.Empty with Filter = Some Filter.MinMagLinear })

    // for a given filterSize and sigma calculate the weights CPU-side
    let filterSize = 15
    let sigma = 6.0

    let halfFilterSize = filterSize / 2
    let weights =
        let res = 
            Array.init filterSize (fun i ->
                let x = abs (i - halfFilterSize)
                exp (-float (x*x) / (2.0 * sigma * sigma))
            )

        // normalize the weights
        let sum = Array.sum res
        res |> Array.map (fun v -> v / sum)


    let gaussX (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(1.0 / float uniform.ViewportSize.X, 0.0)
            for x in -halfFilterSize..halfFilterSize do
                let w = weights.[x+halfFilterSize]
                color <- color + w * input.Sample(v.tc + (float x) * off)

            return V4d(color.XYZ, 1.0)
        }

    let gaussY (v : Vertex) =
        fragment {
            let mutable color = V4d.Zero
            let off = V2d(0.0, 1.0 / float uniform.ViewportSize.Y)
            for y in -halfFilterSize..halfFilterSize do
                let w = weights.[y+halfFilterSize]
                color <- color + w * input.Sample(v.tc + (float y) * off)

            return V4d(color.XYZ, 1.0)
        }


    let envMap = SamplerCube(ShaderTextureHandle("EnvironmentMap", uniform), { SamplerState.Empty with Filter = Some Filter.MinMagLinear; AddressU = Some WrapMode.Clamp; AddressV = Some WrapMode.Clamp; AddressW = Some WrapMode.Clamp })


 
    let environmentMap (v : Vertex) =
        fragment {
            let worldPos = uniform.ViewProjTrafoInv * v.p //V4d(2.0 * v.tc.X - 1.0, 1.0 - 2.0 * v.tc.Y, 1.0, 0.0)
            let worldDir = (worldPos.XYZ / worldPos.W) - uniform.CameraLocation
            let coord = Vec.normalize worldDir
            let tv = envMap.Sample(coord)

            return V4d(tv.XYZ, 1.0) //V4d(0.5 * (coord + V3d.III) + tv.XYZ, 1.0) //V4d((envMap.Sample(Vec.normalize worldDir).XYZ + V3d.III) * 0.5, 1.0)

        }


// TODO: move to base libs
// ==============================================================================
open System.Runtime.CompilerServices
[<AutoOpen>]
module FSharpPixImageCubeExtensions = 

    type PixImageCube with

        member x.Transformed (m : CubeSide -> CubeSide * ImageTrafo) =
            PixImageCube(
                x.MipMapArray 
                    |> Array.mapi (fun i mipMap -> 
                        let side = unbox<CubeSide> i
                        let (newSide, trafo) = m side
                        newSide, PixImageMipMap (mipMap.ImageArray |> Array.map (fun pi -> pi.Transformed(trafo)))
                    )
                    |> Map.ofArray
                    |> Map.toArray
                    |> Array.map snd
            )

        member x.Transformed (m : Map<CubeSide, CubeSide * ImageTrafo>) =
            x.Transformed (fun side ->
                match Map.tryFind side m with
                    | Some t -> t
                    | None -> 
                        Log.warn "incomplete CubeMap trafo"
                        (side, ImageTrafo.Rot0)

            )


        static member create (images : Map<CubeSide, PixImageMipMap>) =
            PixImageCube(images |> Map.toArray |> Array.map snd)

        static member create (images : Map<CubeSide, PixImage>) =
            PixImageCube(images |> Map.toArray |> Array.map (fun (_,pi) -> PixImageMipMap [|pi|]))

        static member create (images : Map<CubeSide, string>) =
            PixImageCube(images |> Map.toArray |> Array.map (fun (_,file) -> PixImageMipMap [|PixImage.Create file|]))

        static member create (images : Map<CubeSide, string>, loadOptions : PixLoadOptions) =
            PixImageCube(images |> Map.toArray |> Array.map (fun (_,file) -> PixImageMipMap [|PixImage.Create(file, loadOptions)|]))


    module PixImageCube =

        module Trafo = 
            let private figureOutComposeOp () =
            
                let v = Volume<byte>(V3i.III * 10)

                let direct = Array.zeroCreate 8
                for o0 in 0..7 do
                    direct.[o0] <- v.Transformed(unbox o0).Info

                for o0 in 0..7 do
                    for o1 in 0..7 do
                        let nv = v.Transformed(unbox o0).Transformed(unbox o1).Info

                        let res = direct |> Array.findIndex (fun di -> di.Equals(nv))

                        printfn "(ImageTrafo.%A, ImageTrafo.%A), ImageTrafo.%A" (unbox<ImageTrafo> o0) (unbox<ImageTrafo> o1) (unbox<ImageTrafo> res)

            let private composeTable =
                Dictionary.ofList [
                    (ImageTrafo.Rot0, ImageTrafo.Rot0), ImageTrafo.Rot0
                    (ImageTrafo.Rot0, ImageTrafo.Rot90), ImageTrafo.Rot90
                    (ImageTrafo.Rot0, ImageTrafo.Rot180), ImageTrafo.Rot180
                    (ImageTrafo.Rot0, ImageTrafo.Rot270), ImageTrafo.Rot270
                    (ImageTrafo.Rot0, ImageTrafo.MirrorX), ImageTrafo.MirrorX
                    (ImageTrafo.Rot0, ImageTrafo.Transpose), ImageTrafo.Transpose
                    (ImageTrafo.Rot0, ImageTrafo.MirrorY), ImageTrafo.MirrorY
                    (ImageTrafo.Rot0, ImageTrafo.Transverse), ImageTrafo.Transverse
                    (ImageTrafo.Rot90, ImageTrafo.Rot0), ImageTrafo.Rot90
                    (ImageTrafo.Rot90, ImageTrafo.Rot90), ImageTrafo.Rot180
                    (ImageTrafo.Rot90, ImageTrafo.Rot180), ImageTrafo.Rot270
                    (ImageTrafo.Rot90, ImageTrafo.Rot270), ImageTrafo.Rot0
                    (ImageTrafo.Rot90, ImageTrafo.MirrorX), ImageTrafo.Transverse
                    (ImageTrafo.Rot90, ImageTrafo.Transpose), ImageTrafo.MirrorX
                    (ImageTrafo.Rot90, ImageTrafo.MirrorY), ImageTrafo.Transpose
                    (ImageTrafo.Rot90, ImageTrafo.Transverse), ImageTrafo.MirrorY
                    (ImageTrafo.Rot180, ImageTrafo.Rot0), ImageTrafo.Rot180
                    (ImageTrafo.Rot180, ImageTrafo.Rot90), ImageTrafo.Rot270
                    (ImageTrafo.Rot180, ImageTrafo.Rot180), ImageTrafo.Rot0
                    (ImageTrafo.Rot180, ImageTrafo.Rot270), ImageTrafo.Rot90
                    (ImageTrafo.Rot180, ImageTrafo.MirrorX), ImageTrafo.MirrorY
                    (ImageTrafo.Rot180, ImageTrafo.Transpose), ImageTrafo.Transverse
                    (ImageTrafo.Rot180, ImageTrafo.MirrorY), ImageTrafo.MirrorX
                    (ImageTrafo.Rot180, ImageTrafo.Transverse), ImageTrafo.Transpose
                    (ImageTrafo.Rot270, ImageTrafo.Rot0), ImageTrafo.Rot270
                    (ImageTrafo.Rot270, ImageTrafo.Rot90), ImageTrafo.Rot0
                    (ImageTrafo.Rot270, ImageTrafo.Rot180), ImageTrafo.Rot90
                    (ImageTrafo.Rot270, ImageTrafo.Rot270), ImageTrafo.Rot180
                    (ImageTrafo.Rot270, ImageTrafo.MirrorX), ImageTrafo.Transpose
                    (ImageTrafo.Rot270, ImageTrafo.Transpose), ImageTrafo.MirrorY
                    (ImageTrafo.Rot270, ImageTrafo.MirrorY), ImageTrafo.Transverse
                    (ImageTrafo.Rot270, ImageTrafo.Transverse), ImageTrafo.MirrorX
                    (ImageTrafo.MirrorX, ImageTrafo.Rot0), ImageTrafo.MirrorX
                    (ImageTrafo.MirrorX, ImageTrafo.Rot90), ImageTrafo.Transpose
                    (ImageTrafo.MirrorX, ImageTrafo.Rot180), ImageTrafo.MirrorY
                    (ImageTrafo.MirrorX, ImageTrafo.Rot270), ImageTrafo.Transverse
                    (ImageTrafo.MirrorX, ImageTrafo.MirrorX), ImageTrafo.Rot0
                    (ImageTrafo.MirrorX, ImageTrafo.Transpose), ImageTrafo.Rot90
                    (ImageTrafo.MirrorX, ImageTrafo.MirrorY), ImageTrafo.Rot180
                    (ImageTrafo.MirrorX, ImageTrafo.Transverse), ImageTrafo.Rot270
                    (ImageTrafo.Transpose, ImageTrafo.Rot0), ImageTrafo.Transpose
                    (ImageTrafo.Transpose, ImageTrafo.Rot90), ImageTrafo.MirrorY
                    (ImageTrafo.Transpose, ImageTrafo.Rot180), ImageTrafo.Transverse
                    (ImageTrafo.Transpose, ImageTrafo.Rot270), ImageTrafo.MirrorX
                    (ImageTrafo.Transpose, ImageTrafo.MirrorX), ImageTrafo.Rot270
                    (ImageTrafo.Transpose, ImageTrafo.Transpose), ImageTrafo.Rot0
                    (ImageTrafo.Transpose, ImageTrafo.MirrorY), ImageTrafo.Rot90
                    (ImageTrafo.Transpose, ImageTrafo.Transverse), ImageTrafo.Rot180
                    (ImageTrafo.MirrorY, ImageTrafo.Rot0), ImageTrafo.MirrorY
                    (ImageTrafo.MirrorY, ImageTrafo.Rot90), ImageTrafo.Transverse
                    (ImageTrafo.MirrorY, ImageTrafo.Rot180), ImageTrafo.MirrorX
                    (ImageTrafo.MirrorY, ImageTrafo.Rot270), ImageTrafo.Transpose
                    (ImageTrafo.MirrorY, ImageTrafo.MirrorX), ImageTrafo.Rot180
                    (ImageTrafo.MirrorY, ImageTrafo.Transpose), ImageTrafo.Rot270
                    (ImageTrafo.MirrorY, ImageTrafo.MirrorY), ImageTrafo.Rot0
                    (ImageTrafo.MirrorY, ImageTrafo.Transverse), ImageTrafo.Rot90
                    (ImageTrafo.Transverse, ImageTrafo.Rot0), ImageTrafo.Transverse
                    (ImageTrafo.Transverse, ImageTrafo.Rot90), ImageTrafo.MirrorX
                    (ImageTrafo.Transverse, ImageTrafo.Rot180), ImageTrafo.Transpose
                    (ImageTrafo.Transverse, ImageTrafo.Rot270), ImageTrafo.MirrorY
                    (ImageTrafo.Transverse, ImageTrafo.MirrorX), ImageTrafo.Rot90
                    (ImageTrafo.Transverse, ImageTrafo.Transpose), ImageTrafo.Rot180
                    (ImageTrafo.Transverse, ImageTrafo.MirrorY), ImageTrafo.Rot270
                    (ImageTrafo.Transverse, ImageTrafo.Transverse), ImageTrafo.Rot0
                ]

            let private composeTrafo (l : ImageTrafo) (r : ImageTrafo) =
                composeTable.[(l,r)]

            let private identity =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.PositiveX, ImageTrafo.Rot0)
                    CubeSide.PositiveY, (CubeSide.PositiveY, ImageTrafo.Rot0)
                    CubeSide.PositiveZ, (CubeSide.PositiveZ, ImageTrafo.Rot0)
                    CubeSide.NegativeX, (CubeSide.NegativeX, ImageTrafo.Rot0)
                    CubeSide.NegativeY, (CubeSide.NegativeY, ImageTrafo.Rot0)
                    CubeSide.NegativeZ, (CubeSide.NegativeZ, ImageTrafo.Rot0)
                ]

            let private compose (l : Map<CubeSide, CubeSide * ImageTrafo>) (r : Map<CubeSide, CubeSide * ImageTrafo>) : Map<CubeSide, CubeSide * ImageTrafo> =
                l |> Map.map (fun s (ts, lt) ->
                    match Map.tryFind ts r with
                        | Some (fs, rt) -> fs, composeTrafo lt rt
                        | None -> ts, lt
                )

            let private all (l : seq<Map<CubeSide, CubeSide * ImageTrafo>>) =
                l |> Seq.fold compose identity

            let RotX90 =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.PositiveX, ImageTrafo.Rot90)   
                    CubeSide.NegativeX, (CubeSide.NegativeX, ImageTrafo.Rot270)   
                    CubeSide.PositiveZ, (CubeSide.NegativeY, ImageTrafo.Rot0)   
                    CubeSide.NegativeZ, (CubeSide.PositiveY, ImageTrafo.Rot180)   
                    CubeSide.NegativeY, (CubeSide.NegativeZ, ImageTrafo.Rot180)   
                    CubeSide.PositiveY, (CubeSide.PositiveZ, ImageTrafo.Rot0)   
                ]

            let RotX180 = compose RotX90 RotX90
            let RotX270 = compose RotX180 RotX90


            let RotY90 =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.PositiveZ, ImageTrafo.Rot0)   
                    CubeSide.NegativeX, (CubeSide.NegativeZ, ImageTrafo.Rot0)   
                    CubeSide.PositiveZ, (CubeSide.NegativeX, ImageTrafo.Rot0)   
                    CubeSide.NegativeZ, (CubeSide.PositiveX, ImageTrafo.Rot0)   
                    CubeSide.NegativeY, (CubeSide.NegativeY, ImageTrafo.Rot90)   
                    CubeSide.PositiveY, (CubeSide.PositiveY, ImageTrafo.Rot270)   
                ]

            let RotY180 = compose RotY90 RotY90
            let RotY270 = compose RotY180 RotY90




            let RotZ90 =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.PositiveY, ImageTrafo.Rot90)   
                    CubeSide.NegativeX, (CubeSide.NegativeY, ImageTrafo.Rot90)   
                    CubeSide.PositiveZ, (CubeSide.PositiveZ, ImageTrafo.Rot90)   
                    CubeSide.NegativeZ, (CubeSide.NegativeZ, ImageTrafo.Rot270)   
                    CubeSide.NegativeY, (CubeSide.PositiveX, ImageTrafo.Rot90)   
                    CubeSide.PositiveY, (CubeSide.NegativeX, ImageTrafo.Rot90)   
                ]

            let RotZ180 = compose RotZ90 RotZ90
            let RotZ270 = compose RotZ180 RotZ90



            let InvertX =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.NegativeX, ImageTrafo.MirrorX)   
                    CubeSide.NegativeX, (CubeSide.PositiveX, ImageTrafo.MirrorX)   
                    CubeSide.PositiveZ, (CubeSide.PositiveZ, ImageTrafo.MirrorX)   
                    CubeSide.NegativeZ, (CubeSide.NegativeZ, ImageTrafo.MirrorX)   
                    CubeSide.NegativeY, (CubeSide.NegativeY, ImageTrafo.MirrorX)   
                    CubeSide.PositiveY, (CubeSide.PositiveY, ImageTrafo.MirrorX)   
                ]

            let InvertY =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.PositiveX, ImageTrafo.MirrorY)
                    CubeSide.NegativeX, (CubeSide.NegativeX, ImageTrafo.MirrorY)
                    CubeSide.PositiveY, (CubeSide.NegativeY, ImageTrafo.MirrorY)
                    CubeSide.NegativeY, (CubeSide.PositiveY, ImageTrafo.MirrorY)
                    CubeSide.PositiveZ, (CubeSide.PositiveZ, ImageTrafo.MirrorY)
                    CubeSide.NegativeZ, (CubeSide.NegativeZ, ImageTrafo.MirrorY)
                ]

            let InvertZ =
                Map.ofList [
                    CubeSide.PositiveX, (CubeSide.PositiveX, ImageTrafo.MirrorX)   
                    CubeSide.NegativeX, (CubeSide.NegativeX, ImageTrafo.MirrorX)   
                    CubeSide.PositiveZ, (CubeSide.NegativeZ, ImageTrafo.MirrorX)   
                    CubeSide.NegativeZ, (CubeSide.PositiveZ, ImageTrafo.MirrorX)   
                    CubeSide.NegativeY, (CubeSide.NegativeY, ImageTrafo.MirrorY)   
                    CubeSide.PositiveY, (CubeSide.PositiveY, ImageTrafo.MirrorY)   
                ]


            let OpenGlConventionTrafo = compose InvertY RotX270
            

        let rotX90 (c : PixImageCube) =
            c.Transformed Trafo.RotX90

        let rotX180 (c : PixImageCube) =
            c.Transformed Trafo.RotX180

        let rotX270 (c : PixImageCube) =
            c.Transformed Trafo.RotX270

        let rotY90 (c : PixImageCube) =
            c.Transformed Trafo.RotY90

        let rotY180 (c : PixImageCube) =
            c.Transformed Trafo.RotY180

        let rotY270 (c : PixImageCube) =
            c.Transformed Trafo.RotY270

        let rotZ90 (c : PixImageCube) =
            c.Transformed Trafo.RotZ90

        let rotZ180 (c : PixImageCube) =
            c.Transformed Trafo.RotZ180

        let rotZ270 (c : PixImageCube) =
            c.Transformed Trafo.RotZ270

        let invertX (c : PixImageCube) =
            c.Transformed Trafo.InvertX

        let invertY (c : PixImageCube) =
            c.Transformed Trafo.InvertY

        let invertZ (c : PixImageCube) =
            c.Transformed Trafo.InvertZ


        let ofOpenGlConvention (c : PixImageCube) =
            c.Transformed Trafo.OpenGlConventionTrafo
            
        let toOpenGlConvention (c : PixImageCube) =
            c.Transformed Trafo.OpenGlConventionTrafo
            
        let toTexture (mipMaps : bool) (c : PixImageCube) =
            PixTextureCube(c, mipMaps) :> ITexture

[<AbstractClass; Sealed; Extension>]
type PixImageCubeExtensions private() =

    [<Extension>]
    static member Transformed(x : PixImageCube, f : System.Func<CubeSide, Tup<CubeSide, ImageTrafo>>) =
        x.Transformed(fun side ->
            let t = f.Invoke(side)
            t.E0, t.E1
        )
   
    [<Extension>]
    static member Transformed(x : PixImageCube, d : System.Collections.Generic.Dictionary<CubeSide, Tup<CubeSide, ImageTrafo>>) =
        x.Transformed (fun side ->
            match d.TryGetValue side with
                | (true, t) -> t.E0, t.E1
                | _ ->
                    Log.warn "incomplete CubeMap trafo" 
                    side, ImageTrafo.Rot0
        )
   

    [<Extension>] static member RotX90(x : PixImageCube) = PixImageCube.rotX90 x
    [<Extension>] static member RotX180(x : PixImageCube) = PixImageCube.rotX180 x
    [<Extension>] static member RotX270(x : PixImageCube) = PixImageCube.rotX270 x

    [<Extension>] static member RotY90(x : PixImageCube) = PixImageCube.rotY90 x
    [<Extension>] static member RotY180(x : PixImageCube) = PixImageCube.rotY180 x
    [<Extension>] static member RotY270(x : PixImageCube) = PixImageCube.rotY270 x  

    [<Extension>] static member RotZ90(x : PixImageCube) = PixImageCube.rotZ90 x
    [<Extension>] static member RotZ180(x : PixImageCube) = PixImageCube.rotZ180 x
    [<Extension>] static member RotZ270(x : PixImageCube) = PixImageCube.rotZ270 x  

    [<Extension>] static member InvertX(x : PixImageCube) = PixImageCube.invertX x
    [<Extension>] static member InvertY(x : PixImageCube) = PixImageCube.invertY x
    [<Extension>] static member InvertZ(x : PixImageCube) = PixImageCube.invertZ x  
    [<Extension>] static member FromOpenGlConvention(x : PixImageCube) = PixImageCube.ofOpenGlConvention x  
    [<Extension>] static member ToOpenGlConvention(x : PixImageCube) = PixImageCube.toOpenGlConvention x  

// ==============================================================================

module HelloWorld =


    let run () =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
   
        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                let index = [|0;1;2; 0;2;3|]
                let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

                IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array], SymDict.empty)

            quad |> Sg.ofIndexedGeometry

        let sg =
            quadSg |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.White |> toEffect
                  ]
               |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()
        0

    let envTest() =
        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
   
        let view = CameraView.LookAt(V3d(0.0, -2.0, 0.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 120.0 0.1 10.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let faceFiles = 
            Map.ofList [
                CubeSide.PositiveX, "lazarus2_positive_x.jpg"
                CubeSide.NegativeX, "lazarus2_negative_x.jpg"
                CubeSide.PositiveY, "lazarus2_positive_y.jpg"
                CubeSide.NegativeY, "lazarus2_negative_y.jpg"
                CubeSide.PositiveZ, "lazarus2_positive_z.jpg"
                CubeSide.NegativeZ, "lazarus2_negative_z.jpg"
            ]
        let envCubePix = 
            faceFiles   
                |> Map.map (fun _ f -> Path.combine [@"C:\Aardwork"; f])
                |> PixImageCube.create
                |> PixImageCube.ofOpenGlConvention
                |> PixImageCube.toTexture true

        let envCube = 
            envCubePix |> Mod.constant

        let ctx = app.Runtime.Context

        let cube = ctx.CreateTexture(envCubePix)
        let newCube = ctx.CreateTextureCube(cube.Size2D, 1, cube.Format, 1)

        for f in 0..5 do
            ctx.Copy(cube, 0, f, V2i.Zero, newCube, 0, f, V2i.Zero, cube.Size2D)

        let faceX = ctx.CreateTextureView(newCube, Range1i(0,0), Range1i(0,0))
        //ctx.Download(faceX).SaveAsImage @"C:\Users\Schorsch\Desktop\x.jpg"

        let fullscreenQuad =
            Sg.draw IndexedGeometryMode.TriangleStrip
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f(-1.0,-1.0,1.0); V3f(1.0,-1.0,1.0); V3f(-1.0,1.0,1.0);V3f(1.0,1.0,1.0) |])
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])
                |> Sg.uniform "ViewportSize" win.Sizes

        let envSg =
            fullscreenQuad
                |> Sg.effect [toEffect Shaders.environmentMap]
                |> Sg.texture (Symbol.Create "EnvironmentMap") envCube
                

        let cross =
            Sg.draw IndexedGeometryMode.PointList
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f.OOO; V3f.IOO; V3f.OIO; V3f.OOI|])
                |> Sg.vertexAttribute DefaultSemantic.Colors (Mod.constant [|C4b.White; C4b.Red; C4b.Green; C4b.Blue|])
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.pointSprite |> toEffect; DefaultSurfaces.vertexColor |> toEffect; DefaultSurfaces.pointSpriteFragment |> toEffect]
                |> Sg.uniform "PointSize" (Mod.constant 20.0)
                |> Sg.uniform "ViewportSize" (win.Sizes)
        let sg = 
            Sg.group' [ cross; envSg ]
                |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)
                //|> Sg.depthTest (Mod.constant DepthTestMode.None)

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()
        0

    let testGeometrySet() =

        //Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
   
        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let rand = Random()
        let randomPoints pointCount =
            let randomV3f() = 3.0*V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) |> V3f.op_Explicit
            let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

            IndexedGeometry(
                Mode = IndexedGeometryMode.PointList,
                IndexArray = Array.init pointCount id,
                IndexedAttributes = 
                    SymDict.ofList [
                         DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                         DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
                    ]
            )
                                    
        let geometries = Array.init 10 (fun _ -> randomPoints 1000) |> CSet.ofArray 
        let sg = Sg.GeometrySet( geometries, IndexedGeometryMode.PointList, 
                                    Map.ofList [ DefaultSemantic.Positions, typeof<V3f>
                                                 DefaultSemantic.Colors, typeof<C4b> ] ) :> ISg



        let add = win.Keyboard.IsDown(Keys.T)
        let rem = win.Keyboard.IsDown(Keys.R)
        let lockObj = obj()

        let mutable added = geometries.Count
        let mutable removed = 0

        async {
            do! Async.SwitchToNewThread()
            while true do
                if Mod.force add then
                    let g = randomPoints 10
                    transact (fun () ->
                        lock lockObj (fun () -> geometries.Add g |> ignore)
                        added <- added + 1
                    )
                System.Threading.Thread.SpinWait(50000)
        } |> Async.Start

        async {
            do! Async.SwitchToNewThread()
            while true do
                if Mod.force rem then
                    if geometries.Count > 0 then
                        transact (fun () ->
                            lock lockObj (fun () -> 
                                let g = geometries |> Seq.head
                                geometries.Remove g |> ignore
                                removed <- removed + 1
                            )
                        )
                System.Threading.Thread.SpinWait(50000)
        } |> Async.Start


//        win.Keyboard.DownWithRepeats.Values.Add (fun k ->
//            if k = Keys.R then
//                if geometries.Count > 0 then
//                    let g = geometries |> Seq.head
//                    transact (fun () ->
//                        geometries.Remove g |> ignore
//                        printfn "removed"
//                    )
//            elif k = Keys.T then
//                transact (fun () ->
//                    geometries.Add (randomPoints 1000) |> ignore
//                    printfn "added"
//                )
//        )

        let mutable blendMode = BlendMode.Blend
        blendMode.Operation <- BlendOperation.Add
        blendMode.AlphaOperation <- BlendOperation.Add
        blendMode.SourceFactor <- BlendFactor.One
        blendMode.DestinationFactor <- BlendFactor.One

        let final =
            sg |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect                  
                    DefaultSurfaces.vertexColor |> toEffect 
                    ]
                |> Sg.depthTest (Mod.constant DepthTestMode.None)
                |> Sg.blendMode (Mod.constant blendMode)
                // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
                |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo ) 
                // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
                // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
                |> Sg.projTrafo (perspective  |> Mod.map Frustum.projTrafo    )
                //|> Sg.normalizeAdaptive

        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.NativeOptimized, final)

        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()
        0

    type Geometry = { bounds : Box3d; createLevel : int -> IndexedGeometry; levels : int }

    let testLoD() =

        //Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
   
        let view = CameraView.LookAt(V3d(10.0,10.0,10.0), V3d(0,0,0), V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let colors = [| C4b.Gray; C4b.Green; C4b.Red; C4b.VRVisGreen; C4b.Blue; |]

        let rand = Random()
        let randomPoints (bounds : Box3d) level pointCount =
            let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) * bounds.Size + bounds.Min |> V3f.op_Explicit
            let randomColor() = colors.[level]// C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

            IndexedGeometry(
                Mode = IndexedGeometryMode.PointList,
                IndexedAttributes = 
                    SymDict.ofList [
                         DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                         DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
                    ]
            )

        let scene = 
            aset {
                for x in 0 .. 20 do
                    for y in 0 .. 20 do
                        for z in 0 .. 20 do
                            let bounds = Box3d.FromMinAndSize(V3d(x,y,z), V3d.III)
                            yield { bounds = bounds; levels = 3; createLevel = fun level -> randomPoints bounds level ((1 <<< level)*5) }
            }

        let getLodRepr (vt : IMod<CameraView>) (proj : IMod<Frustum>) (g : Geometry) : IMod<IndexedGeometry> =
            let mutable current = None
            adaptive  {
                let! vt = vt
                let level = 
                    match V3d.Distance(g.bounds.GetClosestPointOn vt.Location, vt.Location) with
                    | v when v < 4.0 -> 0
                    | v when v < 10.0 -> 0
                    | v when v < 20.0 -> 0
                    | _ -> 0
                match current with 
                 | Some (oldLevel,g) when oldLevel = level -> 
                    return g
                 | _ -> 
                    //printfn "%A" level
                    let g = g.createLevel level
                    current <- Some (level,g)
                    return g
            }

        let attributeTypes = 
             Map.ofList [ 
                DefaultSemantic.Positions, typeof<V3f>
                DefaultSemantic.Colors, typeof<C4b> 
             ]

        let sg = 
            scene |> ASet.mapM ( getLodRepr viewTrafo perspective ) 
            |> Sg.geometrySet IndexedGeometryMode.PointList attributeTypes
            //|> ASet.map Sg.ofIndexedGeometry |> Sg.set


        let final =
            sg |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect                  
                    DefaultSurfaces.vertexColor |> toEffect 
                    ]
                // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
                |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo ) 
                // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
                // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
                |> Sg.projTrafo (perspective  |> Mod.map Frustum.projTrafo    )
                //|> Sg.normalizeAdaptive

        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.NativeOptimized, final)

        win.RenderTask <- task |> DefaultOverlays.withStatistics
        win.Run()
        0

module LoD =

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

        let mutable private initialized = 0

        let private winFormsApp = lazy ( new OpenGlApplication() )




        let init() =
            let debugPath = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "bin", "Debug")
            let releasePath = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "bin", "Release")
            let path = if System.IO.File.Exists(System.IO.Path.Combine(debugPath, "Examples.exe")) then debugPath else releasePath
        
            if Interlocked.Exchange(&initialized, 1) = 0 then
                System.Environment.CurrentDirectory <- path
                IntrospectionProperties.CustomEntryAssembly <- System.Reflection.Assembly.LoadFile <| System.IO.Path.Combine(path, "Examples.exe")

                Ag.initialize()
                Aardvark.Base.Ag.unpack <- fun o ->
                        match o with
                            | :? IMod as o -> o.GetValue(null)
                            | _ -> o
 
        let openWindow() =
            init()

            let app = winFormsApp.Value
            let win = app.CreateSimpleRenderWindow(1)
            let task = app.Runtime.CompileRender(win.FramebufferSignature, Sg.group [])

            win.Text <- @"Aardvark rocks \o/"
            win.Visible <- true 
            win.RenderTask <- task

            win :> IRenderControl

        let closeWindow (c : IRenderControl) =
            match c with
                | :? SimpleRenderWindow as w ->
                    w.Close()
                    w.RenderTask.Dispose()
                    w.Dispose()
                | _ ->
                    ()

        let showSg (w : IRenderControl) (sg : ISg) =
            init()

            try
                let task = w.Runtime.CompileRender(w.FramebufferSignature, sg)

                let old = w.RenderTask
                w.RenderTask <- task
            
                match w with
                    | :? SimpleRenderWindow as w ->
                        w.Invalidate()
                    | _ -> ()

                old.Dispose()
            with e ->
                Log.warn "failed to set sg: %A" e

        let clearSg (w : IRenderControl) =
            init()
            showSg w (Sg.group [])

        let viewTrafo (win : IRenderControl) =
            let view =  CameraView.LookAt(V3d(3.0, 3.0, 3.0), V3d.Zero, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let perspective (win : IRenderControl) = 
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.01 10.0 (float s.X / float s.Y))


        let runInteractive () =
           
            let debugPath = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "bin", "Debug")
            let releasePath = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "..", "bin", "Release")
            let path = if System.IO.File.Exists(System.IO.Path.Combine(debugPath, "Examples.exe")) then debugPath else releasePath
        
            System.Environment.CurrentDirectory <- path 
            IntrospectionProperties.CustomEntryAssembly <- System.Reflection.Assembly.LoadFile <| System.IO.Path.Combine(path, "Examples.exe")

            Ag.initialize()
            Aardvark.Base.Ag.unpack <- fun o ->
                    match o with
                        | :? IMod as o -> o.GetValue(null)
                        | _ -> o


            let app = new OpenGlApplication()
            let win = app.CreateSimpleRenderWindow(1)
            let root = Mod.init <| (Sg.group [] :> ISg)


            let task = app.Runtime.CompileRender(win.FramebufferSignature, Sg.DynamicNode root) |> DefaultOverlays.withStatistics

            win.Text <- @"Aardvark rocks \o/"
            win.Visible <- true

            let fixupRenderTask () =
                if win.RenderTask = Unchecked.defaultof<_> then win.RenderTask <- task

            (fun s -> fixupRenderTask () ; transact (fun () -> Mod.change root s)), win, task
 

    let setSg, win, mainTask = runInteractive ()

    module Default =
        let quadSg =
            let quad =
                let index = [|0;1;2; 0;2;3|]
                let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]
                let coords = [|V2f(0.0,0.0); V2f(1.0,0.0); V2f(1.0,1.0); V2f(0.0,1.0) |]

                IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array; DefaultSemantic.DiffuseColorCoordinates, coords :> Array], SymDict.empty)

            quad |> Sg.ofIndexedGeometry

        let viewTrafo () =
            let view =  CameraView.LookAt(V3d(3.0, 3.0, 3.0), V3d.Zero, V3d.OOI)
            DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let perspective () = 
            win.Sizes |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))

      
    

    open System
    open System.Collections.Generic
    open Aardvark.Base
    open Aardvark.Base.Rendering
    open Aardvark.Base.Incremental
    open Aardvark.SceneGraph
    open Aardvark.Application
    open Default
    // ===================================================================================
    // kill entirely?
    // ===================================================================================
    [<CustomEquality; NoComparison>]
    type GridCell =
        struct
            val mutable public Id : V3l
            val mutable public Exponent : int


            member x.Box =
                let size = pow 2.0 (float x.Exponent)
                let center = V3d(x.Id) * size
                Box3d.FromMinAndSize(center, V3d(size, size, size))

            member x.Children =
                let baseId = 2L * x.Id
                let exp = x.Exponent - 1
                [
                    GridCell(baseId + V3l.OOO, exp)
                    GridCell(baseId + V3l.OOI, exp)
                    GridCell(baseId + V3l.OIO, exp)
                    GridCell(baseId + V3l.OII, exp)
                    GridCell(baseId + V3l.IOO, exp)
                    GridCell(baseId + V3l.IOI, exp)
                    GridCell(baseId + V3l.IIO, exp)
                    GridCell(baseId + V3l.III, exp)
                ]

            member x.Parent =
                let fp = V3d.op_Explicit x.Id / 2.0

                let id =
                    V3l(
                        (if fp.X < 0.0 then floor fp.X else ceil fp.X),
                        (if fp.Y < 0.0 then floor fp.Y else ceil fp.Y),
                        (if fp.Z < 0.0 then floor fp.Z else ceil fp.Z)
                    )

                GridCell(id, x.Exponent + 1)

            override x.ToString() =
                sprintf "{ id = %A; exponent = %d; box = %A }" x.Id x.Exponent x.Box

            override x.GetHashCode() =
                HashCode.Combine(x.Id.GetHashCode(), x.Exponent.GetHashCode())

            override x.Equals o =
                match o with
                    | :? GridCell as o -> x.Id = o.Id && x.Exponent = o.Exponent
                    | _ -> false

            new(id : V3l, exp : int) = { Id = id; Exponent = exp }
        end

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module GridCell =
    
        let inline parent (c : GridCell) = c.Parent
        let inline children (c : GridCell) = c.Children
        let inline box (c : GridCell) = c.Box

        type V3l with
            static member Floor(v : V3d) = V3l(floor v.X, floor v.Y, floor v.Z)
            static member Round(v : V3d) = V3l(round v.X, round v.Y, round v.Z)
            static member Ceil(v : V3d) = V3l(ceil v.X, ceil v.Y, ceil v.Z)

        let containingCells (b : Box3d) =
            let exp = Fun.Log2 b.Size.NormMax |> ceil
            let mutable size = pow 2.0 exp
            let mutable exp = int exp
            let mutable minId = (b.Min + 10.0 * Constant<float>.PositiveTinyValue) / size |> V3l.Floor
            let mutable maxId = (b.Max - 10.0 * Constant<float>.PositiveTinyValue) / size |> V3l.Floor
                                 
            [ for x in minId.X..maxId.X do
                for y in minId.Y..maxId.Y do
                    for z in minId.Z..maxId.Z do
                        yield GridCell(V3l(x,y,z), exp)
            ]

        let inline px (c : GridCell) = GridCell(c.Id + V3l.IOO, c.Exponent)
        let inline py (c : GridCell) = GridCell(c.Id + V3l.OIO, c.Exponent)
        let inline pz (c : GridCell) = GridCell(c.Id + V3l.OOI, c.Exponent)
        let inline nx (c : GridCell) = GridCell(c.Id - V3l.IOO, c.Exponent)
        let inline ny (c : GridCell) = GridCell(c.Id - V3l.OIO, c.Exponent)
        let inline nz (c : GridCell) = GridCell(c.Id - V3l.OOI, c.Exponent)

        let viewVolumeCells (viewProj : Trafo3d) =
            Box3d(-V3d.III, V3d.III).ComputeCorners() 
                |> Array.map (fun p -> viewProj.Backward.TransformPosProj(p))
                |> Box3d
                |> containingCells



        let raster (split : GridCell -> Polygon2d -> Range1d -> bool) (view : CameraView) (proj : Frustum) =
            let view = CameraView.viewTrafo view
            let projTrafo = Frustum.projTrafo proj
            let viewProj = view * projTrafo

            let split (b : GridCell) =
        
                let viewSpacePoints =
                    b.Box.ComputeCorners()
                        |> Array.map (fun v ->
                            let vp = view.Forward.TransformPos(v)
                            let res = projTrafo.Forward.TransformPosProj(vp)

                            let ld = -vp.Z / (proj.far - proj.near)
                            V3d(res.XY, ld)
                        )

                let poly =
                    viewSpacePoints 
                        |> Array.map Vec.xy
                        |> Polygon2d

                let clipped = 
                    poly.ComputeConvexHullIndexPolygon().ToPolygon2d().ConvexClipped(Box2d(-V2d.II, V2d.II))


                let zrange = viewSpacePoints |> Array.map (fun v -> clamp -1.0 1.0 v.Z) |> Range1d


                //printfn "{ area = %A; distance = %A }" area distance
                //printfn "%A, %A -> %A" distance area f
                split b clipped zrange
        
            let rec splitCells (current : list<GridCell>) =
                match current with
                    | [] -> []
                    | _ ->
                        let split, keep = 
                            current |> List.partition split

                        let nested = 
                            split 
                                |> List.collect children 
                                |> List.filter (fun c -> c.Box.IntersectsFrustum(viewProj.Forward))
                                |> splitCells

                        keep @ nested

            viewProj 
                |> viewVolumeCells 
                |> splitCells
                |> List.filter (fun c -> c.Box.IntersectsFrustum(viewProj.Forward))



        let test() =
            let view = CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI
            let proj = Frustum.perspective 60.0 0.1 1000.0 1.0

            let decide _ (poly : Polygon2d) (range : Range1d) =
                let f = poly.ComputeArea() / (range.Min * range.Min)
                f > 0.4

            let mutable cnt = 0
            let mutable bounds = Box3d.Invalid
            let cells = raster decide view proj

            let iter = 100
            let sw = System.Diagnostics.Stopwatch()
            sw.Start()
            for i in 1..iter do
                let cells = raster decide view proj
                ()
            sw.Stop()

            for c in cells do
                printfn "%A" c
                cnt <- cnt + 1
                bounds <- Box3d.Union(bounds, c.Box)

            printfn "took %.3f ms" (sw.Elapsed.TotalMilliseconds / float iter)
            printfn "count = %A" cnt
            printfn "box = %A" bounds

    module Helpers = 
        let rand = Random()
        let randomPoints (bounds : Box3d) (pointCount : int) =
            let size = bounds.Size
            let randomV3f() = V3d(rand.NextDouble(), rand.NextDouble(), rand.NextDouble()) * size + bounds.Min |> V3f.op_Explicit
            let randomColor() = C4b(rand.NextDouble(), rand.NextDouble(), rand.NextDouble(), 1.0)

            IndexedGeometry(
                Mode = IndexedGeometryMode.PointList,
                IndexedAttributes = 
                    SymDict.ofList [
                         DefaultSemantic.Positions, Array.init pointCount (fun _ -> randomV3f()) :> Array
                         DefaultSemantic.Colors, Array.init pointCount (fun _ -> randomColor()) :> Array
                    ]
            )

        let randomColor() =
            C4b(128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 128 + rand.Next(127) |> byte, 255uy)
        let randomColor2(alpha) =
            C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, alpha)

        let box (color : C4b) (box : Box3d) =

            let randomColor = color //C4b(rand.Next(255) |> byte, rand.Next(255) |> byte, rand.Next(255) |> byte, 255uy)

            let indices =
                [|
                    1;2;6; 1;6;5
                    2;3;7; 2;7;6
                    4;5;6; 4;6;7
                    3;0;4; 3;4;7
                    0;1;5; 0;5;4
                    0;3;2; 0;2;1
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> randomColor) :> Array
                    ]

            )

        let wireBox (color : C4b) (box : Box3d) =
            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let positions = 
                [|
                    V3f(box.Min.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Min.Y, box.Min.Z)
                    V3f(box.Max.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Max.Y, box.Min.Z)
                    V3f(box.Min.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Min.Y, box.Max.Z)
                    V3f(box.Max.X, box.Max.Y, box.Max.Z)
                    V3f(box.Min.X, box.Max.Y, box.Max.Z)
                |]

            let normals = 
                [| 
                    V3f.IOO;
                    V3f.OIO;
                    V3f.OOI;

                    -V3f.IOO;
                    -V3f.OIO;
                    -V3f.OOI;
                |]

            IndexedGeometry(
                Mode = IndexedGeometryMode.LineList,

                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                        DefaultSemantic.Normals, indices |> Array.mapi (fun ti _ -> normals.[ti / 6]) :> Array
                        DefaultSemantic.Colors, indices |> Array.map (fun _ -> color) :> Array
                    ]

            )

        let frustum (f : IMod<CameraView>) (proj : IMod<Frustum>) =
            let invViewProj = Mod.map2 (fun v p -> (CameraView.viewTrafo v * Frustum.projTrafo p).Inverse) f proj

            let positions = 
                [|
                    V3f(-1.0, -1.0, -1.0)
                    V3f(1.0, -1.0, -1.0)
                    V3f(1.0, 1.0, -1.0)
                    V3f(-1.0, 1.0, -1.0)
                    V3f(-1.0, -1.0, 1.0)
                    V3f(1.0, -1.0, 1.0)
                    V3f(1.0, 1.0, 1.0)
                    V3f(-1.0, 1.0, 1.0)
                |]

            let indices =
                [|
                    1;2; 2;6; 6;5; 5;1;
                    2;3; 3;7; 7;6; 4;5; 
                    7;4; 3;0; 0;4; 0;1;
                |]

            let geometry =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.LineList,
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, indices |> Array.map (fun i -> positions.[i]) :> Array
                            DefaultSemantic.Colors, Array.create indices.Length C4b.Red :> Array
                        ]
                )

            geometry
                |> Sg.ofIndexedGeometry
                |> Sg.trafo invViewProj



    // ===================================================================================
    // move to Aardvark.Base
    // ===================================================================================
    module ASet =
        type CustomReader<'a>(f : IReader<'a> -> list<Delta<'a>>) =
            inherit ASetReaders.AbstractReader<'a>()

            override x.ComputeDelta() =
                f x

            override x.Release() =
                ()

            override x.Inputs = Seq.empty
    
        let custom (f : IReader<'a> -> list<Delta<'a>>) =
            ASet.AdaptiveSet(fun () -> new CustomReader<'a>(f) :> IReader<_>) :> aset<_>


        type WithUpdateReader<'a>(m : IMod<unit>, r : IReader<'a>) =
            inherit ASetReaders.AbstractReader<'a>()


            override x.ComputeDelta() =
                m.GetValue x
                r.GetDelta x

            override x.Release() =
                r.Dispose()

            override x.Inputs = Seq.ofList [m; r]


        let withUpdater (m : IMod<unit>) (s : aset<'a>) =
            ASet.AdaptiveSet(fun () -> new WithUpdateReader<_>(m, s.GetReader()) :> IReader<_>) :> aset<_>


    // ===================================================================================
    // LoD stuff
    // ===================================================================================
    [<CustomEquality; NoComparison>]
    type Node =
        {
            id : obj
            bounds : Box3d
            inner : bool
            granularity : float
        }

        override x.GetHashCode() = x.id.GetHashCode()
        override x.Equals o =
            match o with
                | :? Node as o -> x.id.Equals(o.id)
                | _ -> false


    type IDataProvider =
        abstract member BoundingBox : Box3d
        abstract member Traverse : (Node -> bool) -> unit
        abstract member GetData : node : Node -> count : int -> Async<IndexedGeometry>


    type DummyDataProvider(root : Box3d) =
    
        interface IDataProvider with
            member x.BoundingBox = root

            member x.Traverse f =
                let rec traverse (level : int) (b : Box3d) =
                    let box = b
                    let n = 100.0
                    let node = { id = b; bounds = box; inner = true; granularity = Fun.Cbrt(box.Volume / (n*n*n)) }

                    if f node then
                        let center = b.Center

                        let children =
                            let l = b.Min
                            let u = b.Max
                            let c = center
                            [
                                Box3d(V3d(l.X, l.Y, l.Z), V3d(c.X, c.Y, c.Z))
                                Box3d(V3d(c.X, l.Y, l.Z), V3d(u.X, c.Y, c.Z))
                                Box3d(V3d(l.X, c.Y, l.Z), V3d(c.X, u.Y, c.Z))
                                Box3d(V3d(c.X, c.Y, l.Z), V3d(u.X, u.Y, c.Z))
                                Box3d(V3d(l.X, l.Y, c.Z), V3d(c.X, c.Y, u.Z))
                                Box3d(V3d(c.X, l.Y, c.Z), V3d(u.X, c.Y, u.Z))
                                Box3d(V3d(l.X, c.Y, c.Z), V3d(c.X, u.Y, u.Z))
                                Box3d(V3d(c.X, c.Y, c.Z), V3d(u.X, u.Y, u.Z))
                            ]

                        children |> List.iter (traverse (level + 1))
                    else
                        ()
                traverse 0 root

            member x.GetData (cell : Node) (count : int) =
                async {
                    //do! Async.SwitchToNewThread()
                    let b = Helpers.box (Helpers.randomColor()) cell.bounds
                    //do! Async.Sleep 5000
                    return b
                }

    [<AutoOpen>]
    module ``Data Provider Extensions`` =
        open System.Collections.Concurrent

        type IDataProvider with
            member x.Rasterize(view : Trafo3d, frustum : Frustum, wantedNearPlaneDistance : float) =
                let projTrafo = Frustum.projTrafo frustum
                let viewProj = view * projTrafo
                let camLocation = view.GetViewPosition()

                let result = List<Node>()

                x.Traverse(fun node ->
                    if node.bounds.IntersectsFrustum viewProj.Forward then
                        if node.inner then
                            let bounds = node.bounds

                            let depthRange =
                                bounds.ComputeCorners()
                                    |> Array.map view.Forward.TransformPos
                                    |> Array.map (fun v -> -v.Z)
                                    |> Range1d

                            if depthRange.Max < frustum.near || depthRange.Min > frustum.far then
                                false
                            else
                                let projAvgDistance =
                                    abs (node.granularity / depthRange.Min)

                                if projAvgDistance > wantedNearPlaneDistance then
                                    true
                                else
                                    result.Add node
                                    false
                        else
                            result.Add node
                            false
                    else
                        false
                )

                result


    // ===================================================================================
    // example usage
    // ===================================================================================
    let data = DummyDataProvider(Box3d(V3d.OOO, 5.0 * V3d.III)) :> IDataProvider

    [<AutoOpen>]
    module Camera =
        type Mode =
            | Main
            | Test

        let mode = Mod.init Main

        let currentMain = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)
        let currentTest = ref (CameraView.lookAt (V3d(3,3,3)) V3d.Zero V3d.OOI)

        let mainCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Main ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentMain
                        currentMain := m
                        return m
                    | _ ->
                        return !currentMain
            }

        let gridCam =
            adaptive {
                let! mode = mode
                match mode with
                    | Test ->
                        let! m = DefaultCameraController.control win.Mouse win.Keyboard win.Time !currentTest
                        currentTest := m
                        return m
                    | _ ->
                        return !currentTest
            }

        let view =
            adaptive {
                let! mode = mode
                match mode with
                    | Main -> return! mainCam
                    | Test -> return! gridCam
            }

        win.Keyboard.KeyDown(Keys.Space).Values.Add(fun _ ->
            transact (fun () ->
                match mode.Value with
                    | Main -> Mod.change mode Test
                    | Test -> Mod.change mode Main

                printfn "mode: %A" mode.Value
            )
        )

        let mainProj = perspective()
        let gridProj = Frustum.perspective 60.0 1.0 50.0 1.0 |> Mod.constant

        let proj =
            adaptive {
                let! mode = mode 
                match mode with
                    | Main -> return! mainProj
                    | Test -> return! gridProj
            }

    let nodes =
        ASet.custom (fun self ->
            let proj = gridProj.GetValue self
            let view = gridCam.GetValue self
            let set = data.Rasterize(CameraView.viewTrafo view, proj, 0.005)

            let viewProj = CameraView.viewTrafo view * Frustum.projTrafo proj

            let add = set |> Seq.filter (self.Content.Contains >> not) |> Seq.map Add
            let rem = self.Content |> Seq.filter (set.Contains >> not) |> Seq.map Rem

            Seq.append add rem |> Seq.toList
        )

    let attributeTypes =
        Map.ofList [
            DefaultSemantic.Positions, typeof<V3f>
            DefaultSemantic.Colors, typeof<C4b>
            DefaultSemantic.Normals, typeof<V3f>
        ]

    module Sg =
        open Aardvark.SceneGraph.Semantics
        open System.Collections.Concurrent

        type PointCloud(data : IDataProvider, avgDistance : float) =
            interface ISg

            member x.AverageDistance = avgDistance
            member x.DataProvider = data


    let boxes = 
        nodes 
            |> ASet.map (fun n -> data.GetData n 100 |> Async.RunSynchronously)
            //|> ASet.map (fun n -> data.GetData n 100 |> Async.RunSynchronously)
            |> Sg.geometrySet IndexedGeometryMode.TriangleList attributeTypes
            //|> ASet.map (fun n -> Helpers.box (Helpers.randomColor()) n.bounds)

                                    
    let sg = 
        Sg.group' [
            boxes
            Helpers.frustum gridCam gridProj

            data.BoundingBox.EnlargedByRelativeEps(0.005)
                |> Helpers.wireBox C4b.VRVisGreen
                |> Sg.ofIndexedGeometry
        ]

    let final =
        sg |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect                  
                DefaultSurfaces.vertexColor  |> toEffect 
              ]
            // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
            |> Sg.viewTrafo (view |> Mod.map CameraView.viewTrafo ) 
            // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
            // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
            |> Sg.projTrafo (proj |> Mod.map Frustum.projTrafo    )
            //|> Sg.fillMode (Mod.constant FillMode.Line)
            //|> Sg.trafo (Mod.constant (Trafo3d.Scale 0.1))
    
    let run() =
        setSg final
        win.Run()



