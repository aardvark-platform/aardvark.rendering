namespace Aardvark.Rendering.GL.Tests

open System
open NUnit.Framework
open FsUnit
open Aardvark.Rendering.GL
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Incremental.Operators

module RenderingTests =
    
    do Aardvark.Init()

    let quad = 
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = [| 0; 1; 2; 0; 2; 3 |],
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions,                  [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                ]
        )


    [<Test>]
    let ``[GL] simple render to texture``() =
        
        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        let color = runtime.CreateTexture(~~V2i(1024, 768), ~~PixFormat.ByteBGRA, ~~1, ~~1)
        let depth = runtime.CreateRenderbuffer(~~V2i(1024, 768), ~~RenderbufferFormat.Depth24Stencil8, ~~1)


        let fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Colors, ~~({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, ~~(depth :> IFramebufferOutput)
                ]
            )


        
        let sg =
            quad 
                |> Sg.ofIndexedGeometry
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

        
        use task = runtime.CompileRender(sg)
        use clear = runtime.CompileClear(~~C4f.Black, ~~1.0)

        clear.Run(fbo) |> ignore
        let res = task.Run(fbo)


        let pi = color.Download(0).[0]

        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"


        ()

