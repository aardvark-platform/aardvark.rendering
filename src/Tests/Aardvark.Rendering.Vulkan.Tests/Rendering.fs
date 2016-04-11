namespace Aardvark.Rendering.Vulkan.Tests

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open NUnit.Framework
open FsUnit
open Aardvark.Rendering.Vulkan
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Incremental.Operators
open System.Diagnostics
open Aardvark.Application.WinForms
open System.IO

module ``Rendering Tests`` =
    
    let quadGeometry =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates, [| V2f.OI; V2f.II; V2f.IO; V2f.OO |] :> Array

                ]
        )

    let checkerBoardImage = 
        let img = PixImage<byte>(Col.Format.RGBA, V2i(256, 256))

        img.GetMatrix<C4b>().SetByCoord (fun (c : V2l) ->
            let xy = c.X / 32L + c.Y / 32L
            if xy % 2L = 0L then C4b.White
            else C4b.Gray
        ) |> ignore

        img

    let checkerBoardTexture =
        PixTexture2d(PixImageMipMap [|checkerBoardImage :> PixImage|], true) :> ITexture |> Mod.constant

    [<Test>]
    let ``[Vulkan] textures working``() =
        Report.LogFileName <- Path.GetTempFileName()
        Aardvark.Init()

        use app = new VulkanApplication()
        let runtime = app.Runtime

        let fbos =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
            ]

        let tex = runtime.CreateTexture(checkerBoardImage.Size, TextureFormat.Rgba8, 1, 1, 1)

        let fbo =
            runtime.CreateFramebuffer(fbos, 
                [
                    DefaultSemantic.Colors, { texture = tex; level = 0; slice = 0 } :> IFramebufferOutput
                ])

        let render = 
            quadGeometry
                |> Sg.ofIndexedGeometry
                |> Sg.diffuseTexture checkerBoardTexture
                |> Sg.effect [ DefaultSurfaces.diffuseTexture |> toEffect ]
                |> Sg.depthTest ~~DepthTestMode.None
                |> Sg.compile runtime fbos


        render.Run(fbo) |> ignore

        let test = runtime.Download(tex)
        
        checkerBoardImage.SaveAsImage @"C:\Users\schorsch\Desktop\in.jpg"
        test.SaveAsImage @"C:\Users\schorsch\Desktop\test.jpg"
        match test with
            | :? PixImage<byte> as test ->
                let eq = test.Volume.InnerProduct(checkerBoardImage.Volume, (=), true, (&&))
                if not eq then 
                    failwithf "unexpected image content (not a checkerboard)"
            | _ ->
                failwithf "unexpected image type: %A" test

        ()






