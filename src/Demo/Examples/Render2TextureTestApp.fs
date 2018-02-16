namespace Examples

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

module TextureComposerTestApp = 
    
    module Shader =
        open Aardvark.Base.Rendering.Effects
        open FShade


        let private samplerTexture =
            sampler2d {
                texture uniform?diffTex
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let private samplerMask =
            sampler2d {
                texture uniform?TextureMask
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let private samplerObject =
            sampler2d {
                texture uniform?TextureObject
                filter Filter.MinMagPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        let composeSelectionFragmentShader (v : Vertex) = 
            fragment {
                let objectColor = samplerObject.Sample(v.tc).X
                let mask = samplerMask.Sample(v.tc).X
                let weight : float = uniform?Intensity

                let c = if mask > 0.01 then mask else objectColor * 0.7 * weight
                return V4d(c, c, c, 1.0)
            }
        

        let color2grayFragmentShader (v : Vertex) =
            fragment {
                let color = samplerTexture.Sample(v.tc)
                let c = (color.X + color.Y + color.Z) / 3.0
                return V4d(c, c, c, 1.0)
            }


    module Surfaces = 
        let create (effects : seq<FShade.Effect>) = 
            let e = FShade.Effect.compose effects
            FShadeSurface.Get(e) :> ISurface

        let TrafoDiffuseSurface = 
            let effects =
                Seq.ofList[
                    DefaultSurfaces.trafo |> toEffect;
                    Shader.color2grayFragmentShader |> toEffect;
                ]
            create effects

        let TrafoColorSurface = 
            let effects =
                Seq.ofList[
                    DefaultSurfaces.trafo |> toEffect;
                    DefaultSurfaces.vertexColor |> toEffect;
                ]
            create effects

        let TrafoComposerSurface =
            let effects =
                Seq.ofList[
                    DefaultSurfaces.trafo |> toEffect;
                    Shader.composeSelectionFragmentShader |> toEffect;
                ]
            create effects


    type SgMaker(x : int, y : int) =

        let xf = x |> float32
        let yf = y |> float32

        let uv1 =  [| V2f(0.0, 0.0); V2f(0.5, 0.0); V2f(0.9, 0.4); V2f(0.7, 0.8); V2f(0.2, 0.5)|]
        let uv2 =  [| V2f(0.3, 0.0); V2f(0.8, 0.1); V2f(0.5, 0.7); V2f(0.4, 0.5); V2f(0.2, 0.2)|]
        // let uv3 =  [| V2f(0.0, 0.0); V2f(0.8, 0.1); V2f(0.7, 0.7); V2f(0.4, 0.7); V2f(0.2, 0.2)|]

        let uv2set (v : V2f) = V2f(v.X * xf, v.Y * yf)

        let set1 = uv1 |> Array.map(uv2set)
        let set2 = uv2 |> Array.map(uv2set)
        // let set3 = uv3 |> Array.map(uv2set)

        let indi = [| 0; 1; 2;  0; 2; 3;  0; 3; 4|]
        let flatColors (set : V2f[]) = set |> Array.map (fun v -> C4b.White)



        // for selection
        // --> need two steps
        // 1) render mask --> flat color sufficient
        // 2) render texture --> need all the data (!) and if the polygon is used as base for rendering the texture --> everything else gets cut away!
        // finally --> combine them in shader
        member x.GetSelectionMaskSg (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) =
            let tex = Mod.constant (FileTexture(@"..\..\data\testTexture2.jpg", true) :> ITexture)
            let s = Sg.draw (IndexedGeometryMode.TriangleList)
                    |> Sg.vertexAttribute DefaultSemantic.Positions (set1 |> Mod.constant)
                    |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (uv1 |> Mod.constant)
                    |> Sg.index (indi |> Mod.constant)
                    |> Sg.viewTrafo (view)
                    |> Sg.projTrafo (proj)
                    |> Sg.texture (Symbol.Create "diffTex") tex
            Sg.SurfaceApplicator(Surfaces.TrafoDiffuseSurface, s) :> ISg

        member x.GetSelectionObjectSg (quadSg : ISg) (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) =
            let tex = Mod.constant (FileTexture(@"..\..\data\testTexture2.jpg", true) :> ITexture)
            let s = quadSg
                    |> Sg.viewTrafo (view)
                    |> Sg.projTrafo (proj)
                    |> Sg.texture (Symbol.Create "diffTex") tex
            Sg.SurfaceApplicator(Surfaces.TrafoDiffuseSurface, s) :> ISg


        member x.GetCombinedSelectionSg (quadSg : ISg) (maskTexture : IMod<ITexture>) (objectTexture : IMod<ITexture>) (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) (intensity : IMod<float>) =
            let s = quadSg
                    |> Sg.viewTrafo (view)
                    |> Sg.projTrafo (proj)
                    |> Sg.texture (Symbol.Create "TextureMask") maskTexture
                    |> Sg.texture (Symbol.Create "TextureObject") objectTexture
                    |> Sg.uniform "Intensity" intensity 
            Sg.SurfaceApplicator(Surfaces.TrafoComposerSurface, s) :> ISg


        
        member x.GetSg2 (view : IMod<Trafo3d>) (proj : IMod<Trafo3d>) =
            let tex = Mod.constant (FileTexture(@"..\..\data\testTexture1.jpg", true) :> ITexture)
            let s = Sg.draw (IndexedGeometryMode.TriangleList)
                    |> Sg.vertexAttribute DefaultSemantic.Positions (set2 |> Mod.constant)
                    |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (uv2 |> Mod.constant)
                    |> Sg.index (indi |> Mod.constant)
                    |> Sg.viewTrafo (view)
                    |> Sg.projTrafo (proj)
                    |> Sg.texture (Symbol.Create "diffTex") tex
            Sg.SurfaceApplicator(Surfaces.TrafoDiffuseSurface, s) :> ISg


    
    type Quad(x : int, y : int) =
        let sg =
            let z = 0
            let pos = [| V3f(0, 0, z); V3f(x, 0, z); V3f(0, y, z); V3f(x, y, z) |]
            let uvs = [| V2f(0,0); V2f(1,0); V2f(0,1); V2f(1,1) |]
            (DrawCallInfo(FaceVertexCount = 4, InstanceCount = 1))
                |> Sg.render IndexedGeometryMode.TriangleStrip
                |> Sg.vertexAttribute DefaultSemantic.Positions (Mod.constant pos)
                |> Sg.vertexAttribute DefaultSemantic.DiffuseColorCoordinates (Mod.constant uvs)  

        member x.GetISg() = sg


    type ComposeRender2Buffer(win: SimpleRenderWindow, x : int, y : int) =

        // Compose Signature --> required for rendering to texture
        let sign = win.Runtime.CreateFramebufferSignature [DefaultSemantic.Colors, {format = RenderbufferFormat.R16f; samples = 1}; ]

        let texture1 = win.Runtime.CreateTexture( V2i(x, y), TextureFormat.R16f, 1, 1)
        let texture2 = win.Runtime.CreateTexture( V2i(x, y), TextureFormat.R16f, 1, 1)
        let texture3 = win.Runtime.CreateTexture( V2i(x, y), TextureFormat.R16f, 1, 1)

        let fbo1 = win.Runtime.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = texture1; slice = 0; level = 0} :> IFramebufferOutput)])
        let fbo2 = win.Runtime.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = texture2; slice = 0; level = 0} :> IFramebufferOutput)])
        let fbo3 = win.Runtime.CreateFramebuffer(sign, Map.ofList[DefaultSemantic.Colors, ({texture = texture3; slice = 0; level = 0} :> IFramebufferOutput)])

        let clear = win.Runtime.CompileClear(sign, C4f.Black |> Mod.constant, 1.0 |> Mod.constant)

        member x.runTask1 (sg : ISg) : IBackendTexture =
            let task = win.Runtime.CompileRender(sign, sg)
            clear.Run(null, fbo1 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo1 |> OutputDescription.ofFramebuffer)
            texture1

        member x.runTask2 (sg : ISg) : IBackendTexture =
            let task = win.Runtime.CompileRender(sign, sg)
            clear.Run(null, fbo2 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo2 |> OutputDescription.ofFramebuffer)
            texture2

        member x.runTask3 (sg : ISg) : IBackendTexture =
            let task = win.Runtime.CompileRender(sign, sg)
            clear.Run(null, fbo3 |> OutputDescription.ofFramebuffer)
            task.Run(null, fbo3 |> OutputDescription.ofFramebuffer)
            texture3


module Render2TextureTestApp =

    let run () =

        Ag.initialize()
        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

        let x = 1920
        let y = 1080

        let xh = x / 2
        let yh = y / 2
        
        let view = Mod.constant(CameraView.lookAt (V3d(xh, yh, 20)) (V3d(xh, yh, 0)) V3d.OIO |> CameraView.viewTrafo)
        let proj = Mod.constant (Frustum.ortho (Box3d (-xh |> float, -yh |> float, -10.0, xh |> float, yh |> float, 30.0)) |> Frustum.orthoTrafo)
        
        let compose2buffer = new TextureComposerTestApp.ComposeRender2Buffer(win, x, y)
        let quad = new TextureComposerTestApp.Quad(x, y)
        let quadISg = quad.GetISg()
        let maker = new TextureComposerTestApp.SgMaker(x, y)

        let sg1 : ISg = maker.GetSelectionMaskSg view proj
        let sg2 : ISg = maker.GetSelectionObjectSg quadISg view proj
       
        let maskText = (compose2buffer.runTask1(sg1) :> ITexture)
        let objText  = (compose2buffer.runTask2(sg2) :> ITexture)

        let intensity = Mod.init(0.5)

        let funcU () =
            transact( fun _ ->
                let newValue =
                    if intensity.Value < 0.901 then intensity.Value + 0.1
                    else intensity.Value
                printfn "Value: %A" newValue
                intensity.Value <- newValue
            )

        let funcD () =
            transact( fun _ ->
                let newValue =
                    if intensity.Value > 0.001 then intensity.Value - 0.1
                    else intensity.Value
                printfn "Value: %A" newValue
                intensity.Value <- newValue
            )

        win.Keyboard.KeyDown(Keys.U).Values.Subscribe(funcU) |> ignore
        win.Keyboard.KeyDown(Keys.D).Values.Subscribe(funcD) |> ignore

        let sg3 : ISg = maker.GetCombinedSelectionSg quadISg (maskText |> Mod.constant) (objText |> Mod.constant) view proj intensity
        let tex = compose2buffer.runTask3(sg3) :> ITexture


        let sg = quadISg
                 |> Sg.effect [
                        DefaultSurfaces.trafo |> toEffect
                        DefaultSurfaces.diffuseTexture |> toEffect
                    ]
                 |> Sg.viewTrafo view
                 |> Sg.projTrafo proj
                 |> Sg.diffuseTexture' tex

        let task = app.Runtime.CompileRender(win.FramebufferSignature, BackendConfiguration.ManagedOptimized, sg3.RenderObjects())

        win.RenderTask <- task //|> DefaultOverlays.withStatistics
        win.Run()
        0

