namespace Rendering.Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics

open Aardvark.Application
open Aardvark.Application.WinForms
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


    let envMap = SamplerCube(ShaderTextureHandle("EnvironmentMap", uniform), { SamplerState.empty with Filter = Some Filter.MinMagLinear; AddressU = Some WrapMode.Clamp; AddressV = Some WrapMode.Clamp; AddressW = Some WrapMode.Clamp })


 
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

module Playground =


    let run () =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                IndexedGeometry(
                    Mode = IndexedGeometryMode.TriangleList,
                    IndexArray = ([|0;1;2; 0;2;3|] :> Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                            DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                            DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                        ]
                )
                
            quad |> Sg.ofIndexedGeometry

        let sg =
            quadSg 
                |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.diffuseTexture |> toEffect
                    DefaultSurfaces.simpleLighting |> toEffect
                  ]
               |> Sg.diffuseFileTexture' @"E:\Development\WorkDirectory\DataSVN\pattern.jpg" true
               |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.ManagedOptimized, sg.RenderObjects())

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
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
        let newCube = ctx.CreateTextureCube(cube.Size2D.X, 1, cube.Format, 1)

        for f in 0..5 do
            ctx.Copy(cube, 0, f, V2i.Zero, newCube, 0, f, V2i.Zero, cube.Size2D)

        let faceX = ctx.CreateTextureView(newCube, Range1i(0,0), Range1i(0,0))
        //ctx.Download(faceX).SaveAsImage @"C:\Users\Schorsch\Desktop\x.jpg"

        let fullscreenQuad =
            Sg.draw IndexedGeometryMode.TriangleStrip
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant [|V3f(-1.0,-1.0,1.0); V3f(1.0,-1.0,1.0); V3f(-1.0,1.0,1.0);V3f(1.0,1.0,1.0) |])
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant [|V2f.OO; V2f.IO; V2f.OI; V2f.II|])

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
        let sg = 
            Sg.group' [ cross; envSg ]
                |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo)
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo)
                //|> Sg.depthTest (Mod.constant DepthTestMode.None)

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- task
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

        win.RenderTask <- task
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

        win.RenderTask <- task
        win.Run()
        0




