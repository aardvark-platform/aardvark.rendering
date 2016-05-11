namespace Aardvark.Rendering.GL.Tests

open System
open NUnit.Framework
open FsUnit
open Aardvark.Rendering.GL
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Base.Incremental.Operators
open Aardvark.Application
open System.Diagnostics
open Aardvark.SceneGraph.Semantics
open Aardvark.Application.WinForms
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental.Telemetry


module ``Rendering Tests`` =
    open System.IO
    do Report.LogFileName <- Path.GetTempFileName()
    let quadGeometry =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                    DefaultSemantic.DiffuseColorCoordinates, [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array

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
        Aardvark.Init()

        use app = new OpenGlApplication()
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


module Vector =

    let inline ofArray (arr : 'a[]) =
        Vector<'a>(arr)

    let inline ofSeq (s : seq<'a>) =
        s |> Seq.toArray |> ofArray

    let inline ofList (s : list<'a>) =
        s |> List.toArray |> ofArray

    let inline toSeq (v : Vector<'a>) =
        v.Data :> seq<_>

    let inline toList (v : Vector<'a>) =
        v.Data |> Array.toList

    let inline toArray (v : Vector<'a>) =
        v.Data

    let init (size : int) (f : int -> 'a) =
        Vector<'a>(Array.init size f)

    let create (size : int) (value : 'a) =
        Vector<'a>(Array.create size value)

    let zeroCreate (size : int) =
        Vector<'a>(Array.zeroCreate size)

    let inline map (f : 'a -> 'b) (v : Vector<'a>) =
        v.Map(f)

    let inline map2 (f : 'a -> 'b -> 'c) (l : Vector<'a>) (r : Vector<'b>) =
        let res = Vector<'c>(min l.Size r.Size)
        res.SetMap2(l, r, f)

    let inline fold (f : 's -> 'a -> 's) (seed : 's) (v : Vector<'a>) =
        v.Norm(id, seed, f)

    let inline fold2 (f : 's -> 'a -> 'b -> 's) (seed : 's) (a : Vector<'a>) (b : Vector<'b>) =
        a.InnerProduct(b, (fun a b -> (a,b)), seed, fun s (a,b) -> f s a b)

    let inline dot (l : Vector<'a>) (r : Vector<'b>) : 'c =
        l.InnerProduct(r, (*), LanguagePrimitives.GenericZero, (+))

    let inline normSquared (v : Vector<'a>) : 'b =
        v.Norm((fun a -> a * a), LanguagePrimitives.GenericZero, (+))

    let inline norm (v : Vector<'a>) : 'b =
        sqrt (normSquared v)

module Matrix =

    let init (size : V2i) (f : V2i -> 'a) =
        Matrix<'a>(size).SetByCoord(fun (c : V2l) -> f (V2i c))
   
    let inline fold (f : 's -> 'a -> 's) (seed : 's) (m : Matrix<'a>) =
        m.Norm(id, seed, f)

    let inline fold2 (f : 's -> 'a -> 'b -> 's) (seed : 's) (a : Matrix<'a>) (b : Matrix<'b>) =
        a.InnerProduct(b, (fun a b -> a,b), seed, fun s (a,b) -> f s a b)

    let inline equal (l : Matrix<'a>) (r : Matrix<'a>) =
        l.InnerProduct(r, (=), true, (&&), not)

    let inline notEqual (l : Matrix<'a>) (r : Matrix<'a>) =
        equal l r |> not

[<AutoOpen>]
module TensorSlices =
    
    type Vector<'a> with
        member x.GetSlice(first : Option<int>, last : Option<int>) =
            let first = defaultArg first 0
            let last = defaultArg last (int x.Size - 1)

            x.SubVector(first, 1 + last - first)

        member x.SetSlice(first : Option<int>, last : Option<int>, value : 'a) =
            x.GetSlice(first, last).Set(value) |> ignore

    type Vector<'a, 'b> with
        member x.GetSlice(first : Option<int>, last : Option<int>) =
            let first = defaultArg first 0
            let last = defaultArg last (int x.Size - 1)

            x.SubVector(first, 1 + last - first)

        member x.SetSlice(first : Option<int>, last : Option<int>, value : 'b) =
            x.GetSlice(first, last).Set(value) |> ignore


    type Matrix<'a> with

        member x.GetSlice(first : Option<V2i>, last : Option<V2i>) =
            let first = defaultArg first V2i.Zero
            let last = defaultArg last (V2i x.Size - V2i.II)

            x.SubMatrix(first, V2i.II + last - first)

        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>) =
            let xStart = defaultArg xStart 0
            let xEnd = defaultArg xEnd (int x.Size.X - 1)
            let yStart = defaultArg yStart 0
            let yEnd = defaultArg yEnd (int x.Size.Y - 1)
            
            let p0 = V2i(xStart, yStart)
            let size = V2i(xEnd + 1, yEnd + 1) - p0

            x.SubMatrix(p0, size)

        member m.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>) =
            m.SubYVector(int64 x).GetSlice(yStart, yEnd)

        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int) =
            x.SubXVector(int64 y).GetSlice(xStart, xEnd)

    type Matrix<'a, 'b> with

        member x.GetSlice(first : Option<V2i>, last : Option<V2i>) =
            let first = defaultArg first V2i.Zero
            let last = defaultArg last (V2i x.Size - V2i.II)

            x.SubMatrix(first, V2i.II + last - first)

        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>) =
            let xStart = defaultArg xStart 0
            let xEnd = defaultArg xEnd (int x.Size.X - 1)
            let yStart = defaultArg yStart 0
            let yEnd = defaultArg yEnd (int x.Size.Y - 1)
            
            let p0 = V2i(xStart, yStart)
            let size = V2i(xEnd + 1, yEnd + 1) - p0

            x.SubMatrix(p0, size)

        member m.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>) =
            m.SubYVector(int64 x).GetSlice(yStart, yEnd)

        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int) =
            x.SubXVector(int64 y).GetSlice(xStart, xEnd)


    type Volume<'a> with
        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
            v.SubYZMatrix(int64 x).GetSlice(yStart, yEnd, zStart, zEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, zStart : Option<int>, zEnd : Option<int>) =
            v.SubXZMatrix(int64 y).GetSlice(xStart, xEnd, zStart, zEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, z : int) =
            v.SubXYMatrix(int64 z).GetSlice(xStart, xEnd, yStart, yEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, z : int) =
            v.SubXYMatrix(int64 z).SubXVector(int64 y).GetSlice(xStart, xEnd)

        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, z : int) =
            v.SubXYMatrix(int64 z).SubYVector(int64 x).GetSlice(yStart, yEnd)

        member v.GetSlice(x : int, y : int, zStart : Option<int>, zEnd : Option<int>) =
            v.SubYZMatrix(int64 x).SubYVector(int64 y).GetSlice(zStart, zEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
            let xStart = defaultArg xStart 0
            let xEnd = defaultArg xEnd (int v.Size.X - 1)
            let yStart = defaultArg yStart 0
            let yEnd = defaultArg yEnd (int v.Size.Y - 1)
            let zStart = defaultArg zStart 0
            let zEnd = defaultArg zEnd (int v.Size.Z - 1)
            
            let p0 = V3i(xStart, yStart, zStart)
            let s = V3i(1 + xEnd, 1 + yEnd, 1 + zEnd) - p0

            v.SubVolume(p0, s)

    type Volume<'a, 'b> with
        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
            v.SubYZMatrix(int64 x).GetSlice(yStart, yEnd, zStart, zEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, zStart : Option<int>, zEnd : Option<int>) =
            v.SubXZMatrix(int64 y).GetSlice(xStart, xEnd, zStart, zEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, z : int) =
            v.SubXYMatrix(int64 z).GetSlice(xStart, xEnd, yStart, yEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, z : int) =
            v.SubXYMatrix(int64 z).SubXVector(int64 y).GetSlice(xStart, xEnd)

        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, z : int) =
            v.SubXYMatrix(int64 z).SubYVector(int64 x).GetSlice(yStart, yEnd)

        member v.GetSlice(x : int, y : int, zStart : Option<int>, zEnd : Option<int>) =
            v.SubYZMatrix(int64 x).SubYVector(int64 y).GetSlice(zStart, zEnd)

        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
            let xStart = defaultArg xStart 0
            let xEnd = defaultArg xEnd (int v.Size.X - 1)
            let yStart = defaultArg yStart 0
            let yEnd = defaultArg yEnd (int v.Size.Y - 1)
            let zStart = defaultArg zStart 0
            let zEnd = defaultArg zEnd (int v.Size.Z - 1)
            
            let p0 = V3i(xStart, yStart, zStart)
            let s = V3i(1 + xEnd, 1 + yEnd, 1 + zEnd) - p0

            v.SubVolume(p0, s)



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
        
        let vec = Vector.zeroCreate 1000
        vec.[0..9] <- 1.0
        
        let len = vec |> Vector.norm

        printfn "%A" len

        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        let size = V2i(1024,768)
        
        let color = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
        let depth = runtime.CreateRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, 1)


        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]

        let fbo = 
            runtime.CreateFramebuffer(
                signature, 
                Map.ofList [
                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
            ) 
        let outputDesc = OutputDescription.ofFramebuffer fbo


        
        let sg =
            quad 
                |> Sg.ofIndexedGeometry
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

        
        use task = runtime.CompileRender(signature, sg)
        use clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)

        clear.Run(null, outputDesc) |> ignore
        task.Run(null, outputDesc) |> ignore
        

        let pi = PixImage<byte>(Col.Format.BGRA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
        runtime.Download(color, 0, 0, pi)

        let cmp = PixImage<byte>(Col.Format.BGRA, size)
        cmp.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            if c.X >= int64 size.X / 2L && c.Y >= int64 size.Y / 2L then
                C4b.White
            else
                C4b.Black
        ) |> ignore




        pi.SaveAsImage @"C:\Users\haaser\Desktop\test.png"


        ()

    [<Test>]
    let ``[GL] simple render to multiple texture``() =

        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        let size = V2i(1024,768)
        let color = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
        let normals = runtime.CreateTexture(size, TextureFormat.Rgba32f, 1, 1, 1)
        let depth = runtime.CreateRenderbuffer(size, RenderbufferFormat.Depth24Stencil8, 1)


        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                DefaultSemantic.Normals, { format = RenderbufferFormat.Rgba32f; samples = 1 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]

        let fbo = 
            runtime.CreateFramebuffer(
                signature, 
                Map.ofList [
                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Normals, ({ texture = normals; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
            )
        let outputDesc = OutputDescription.ofFramebuffer fbo


        
        let sg =
            quad 
                |> Sg.ofIndexedGeometry
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

        
        use task = runtime.CompileRender(signature, sg)
        use clear = 
            runtime.CompileClear(
                signature, 
                ~~[DefaultSemantic.Colors, C4f.Black; DefaultSemantic.Normals, C4f.Red], 
                ~~1.0
            )

        clear.Run(null, outputDesc) |> ignore
        task.Run(null, outputDesc) |> ignore

        let pi = PixImage<byte>(Col.Format.BGRA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
        runtime.Download(color, 0, 0, pi)

        let npi = PixImage<float32>(Col.Format.RGBA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
        runtime.Download(normals, 0, 0, npi)


        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"
        PixImage<float32>(npi.Volume.Map(fun f -> f)).ToPixImage<byte>(Col.Format.RGB).SaveAsImage @"C:\Users\schorsch\Desktop\testNormals.png"


        ()

    [<Test>]
    let ``[GL] nested trafos``() =
        
        let leaf = quad |> Sg.ofIndexedGeometry
        let screen = V2i(2048, 2048)

        let grid (size : V2i) (inner : ISg) =
            Sg.group' [
                for x in -size.X/2..size.X/2 do
                    for y in -size.Y/2..size.Y/2 do
                        yield inner |> Sg.trafo (~~Trafo3d.Translation(float x, float y, 0.0))
            ]


        let rec buildGrid (depth : int) =
            if depth <= 0 then 
                leaf |> Sg.trafo (~~Trafo3d.Scale(0.5))  
            else 
                depth - 1 
                    |> buildGrid 
                    |> grid (5 * V2i.II)
                    |> Sg.trafo (~~Trafo3d.Scale(0.125))


        let cam = CameraView.lookAt (0.5 * V3d.OOI) V3d.Zero V3d.OIO
        let frustum = Frustum.perspective 60.0 0.1 1000.0 (float screen.X / float screen.Y)

        let rootTrafo = Mod.init Trafo3d.Identity

        let sg =
            buildGrid 3
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]
                |> Sg.trafo rootTrafo
                |> Sg.viewTrafo ~~(cam |> CameraView.viewTrafo)
                |> Sg.projTrafo ~~(frustum |> Frustum.projTrafo)

        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        using ctx.ResourceLock (fun _ -> 
            Log.line "vendor:   %s" runtime.Context.Driver.vendor
            Log.line "renderer: %s" runtime.Context.Driver.renderer
        )

        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, { format = RenderbufferFormat.Rgba8; samples = 1 }
                DefaultSemantic.Depth, { format = RenderbufferFormat.Depth24Stencil8; samples = 1 }
            ]

        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
        let task = runtime.CompileRender(signature, sg)

        let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 2, 1, 1)
        let depth = runtime.CreateRenderbuffer(screen, RenderbufferFormat.Depth24Stencil8, 1)

        let fbo = 
            runtime.CreateFramebuffer(
                signature,
                Map.ofList [
                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
            )

        clear.Run fbo |> ignore
        let stats = task.Run fbo
        Log.line "%.0f objects" stats.Statistics.DrawCallCount


        runtime.GenerateMipMaps(color)


        let pi = runtime.Download(color, PixFormat.ByteRGBA)
        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"

        let level1 = runtime.Download(color, 1, PixFormat.ByteRGBA)
        level1.SaveAsImage @"C:\Users\schorsch\Desktop\level1.png"


        Log.line "starting pure render test"
        let mutable iterations = 0
        let sw = Stopwatch()
        sw.Start()
        while sw.Elapsed.TotalSeconds < 10.0 do
            clear.Run fbo |> ignore
            task.Run fbo |> ignore
            iterations <- iterations + 1
        sw.Stop()

        let pureRenderTime = sw.Elapsed.TotalSeconds / float iterations

        Telemetry.reset()
        Log.line "starting update test"
        let mutable iterations = 0
        let sw = Stopwatch()
        sw.Start()
        while sw.Elapsed.TotalSeconds < 20.0 || iterations < 50 do
            transact(fun () -> Mod.change rootTrafo (rootTrafo.Value * Trafo3d.Scale(1.00001)))
            clear.Run fbo |> ignore
            task.Run fbo |> ignore
            iterations <- iterations + 1
        sw.Stop()

        let updateAndRenderTime = sw.Elapsed.TotalSeconds / float iterations
            

        let updateTime = updateAndRenderTime - pureRenderTime

        Log.line "total:        %.2ffps" (1.0 / updateAndRenderTime)
        Log.line "rendering:    %.2ffps" (1.0 / pureRenderTime)
        Log.line "updates:      %.2ffps" (1.0 / updateTime)

        let rep = Telemetry.resetAndGetReport()
        Telemetry.print ({ totalTime = rep.totalTime / iterations; probeTimes = rep.probeTimes |> Map.map (fun _ t -> t / iterations) } )

        ()

    [<Test>]
    let ``[GL] compile performance``() =
        let leaf = quad |> Sg.ofIndexedGeometry
        let screen = V2i(2048, 2048)

        let grid (size : V2i) (inner : ISg) =
            Sg.group' [
                for x in -size.X/2..size.X/2 do
                    for y in -size.Y/2..size.Y/2 do
                        yield inner |> Sg.trafo (~~Trafo3d.Translation(float x, float y, 0.0))
            ]


        let rec buildGrid (depth : int) =
            if depth <= 0 then 
                leaf |> Sg.trafo (~~Trafo3d.Scale(0.5))  
            else 
                depth - 1 
                    |> buildGrid 
                    |> grid (5 * V2i.II)
                    |> Sg.trafo (~~Trafo3d.Scale(0.125))


        let cam = CameraView.lookAt (0.5 * V3d.OOI) V3d.Zero V3d.OIO
        let frustum = Frustum.perspective 60.0 0.1 1000.0 (float screen.X / float screen.Y)

        let rootTrafo = Mod.init Trafo3d.Identity

        let sg =
            buildGrid 3
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]
                |> Sg.trafo rootTrafo
                |> Sg.viewTrafo ~~(cam |> CameraView.viewTrafo)
                |> Sg.projTrafo ~~(frustum |> Frustum.projTrafo)

        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        using ctx.ResourceLock (fun _ -> 
            Log.line "vendor:   %s" runtime.Context.Driver.vendor
            Log.line "renderer: %s" runtime.Context.Driver.renderer
        )

        let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 1, 1, 1)
        let depth = runtime.CreateRenderbuffer(screen, RenderbufferFormat.Depth24Stencil8, 1)

        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, RenderbufferFormat.ofTextureFormat color.Format
                DefaultSemantic.Depth, depth.Format
            ]

        let fbo = 
            signature.CreateFramebuffer [
                DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                DefaultSemantic.Depth, (depth :> IFramebufferOutput)
            ]

        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
        let renderJobs = sg.RenderObjects()
        let task = runtime.CompileRender(signature, renderJobs)
        //let task2 = runtime.CompileRender renderJobs



        clear.Run fbo |> ignore
        OpenTK.Graphics.OpenGL4.GL.Sync()
        let stats = task.Run fbo
        OpenTK.Graphics.OpenGL4.GL.Sync()
        Log.line "%.0f objects" stats.Statistics.DrawCallCount
        let pi = runtime.Download(color, PixFormat.ByteRGBA)
        pi.SaveAsImage(@"C:\Aardwork\gugu.png")
        OpenTK.Graphics.OpenGL4.GL.Sync()
        //task2.Run fbo |> ignore
        //OpenTK.Graphics.OpenGL4.GL.Sync()



        Telemetry.reset()
        Log.line "starting update test"
        let mutable iterations = 0
        let sw = Stopwatch()
        let disp = System.Collections.Generic.List()
        sw.Start()
        while sw.Elapsed.TotalSeconds < 20.0 do
            let t = runtime.CompileRender(signature,renderJobs)
            clear.Run fbo |> ignore
            t.Run fbo |> ignore
            iterations <- iterations + 1
            disp.Add t
        sw.Stop()

        for t in disp do
            t.Dispose()
        let updateAndRenderTime = sw.Elapsed.TotalSeconds / float iterations
            
        Log.line "compile + render: %.2fs" (updateAndRenderTime)

        let rep = Telemetry.resetAndGetReport()
        Telemetry.print ({ totalTime = rep.totalTime / iterations; probeTimes = rep.probeTimes |> Map.map (fun _ t -> t / iterations) } )

        ()

    [<Test>]
    let ``[GL] concurrent group change``() =
        let leaf = quad |> Sg.ofIndexedGeometry
        let screen = V2i(2048, 2048)

        let cnt = 1000
        let s = int (ceil (sqrt (float cnt) / 2.0))

        let grid (inner : ISg) =
            [
                for x in -s .. s do
                    for y in -s .. s do
                        yield inner |> Sg.trafo (~~Trafo3d.Translation(float x, float y, 0.0))
            ]

        let mutable candidates = (grid leaf).RandomOrder() |> Seq.toList

        let g = Sg.group []//candidates

        let sg =
            g
                |> Sg.trafo (~~Trafo3d.Scale(0.5 / float s))
                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]
                |> Sg.viewTrafo ~~(Trafo3d.Identity)
                |> Sg.projTrafo ~~(Trafo3d.Identity)

        let useWindow = true
        let runtime, app =
            if useWindow then
                let app = new Aardvark.Application.WinForms.OpenGlApplication()
                let win = app.CreateGameWindow()
                
                app.Runtime, Some (app, win)
            else
                let runtime = new Runtime()
                let ctx = new Context(runtime)
                using ctx.ResourceLock (fun _ -> 
                    Log.line "vendor:   %s" runtime.Context.Driver.vendor
                    Log.line "renderer: %s" runtime.Context.Driver.renderer
                )
                runtime.Context <- ctx
                runtime, None


        let win = app.Value |> snd


        let renderJobs = sg.RenderObjects()
        let clear = runtime.CompileClear(app.Value |> snd |> (fun s -> s.FramebufferSignature), ~~C4f.Black, ~~1.0)
        let task = runtime.CompileRender(app.Value |> snd |> (fun s -> s.FramebufferSignature), BackendConfiguration.NativeOptimized, renderJobs)

//        win.Keyboard.KeyDown(Keys.P).Values.Subscribe(fun _ ->
//            lock task (fun () ->
//                let task = task |> unbox<Aardvark.Rendering.GL.GroupedRenderTask.RenderTask>
//                let code = task.Program.Disassemble() |> unbox<Instruction[][]>
//
//                let mutable fragment = 0
//                for part in code do
//                    Log.start "fragment %d" fragment
//                    for i in part do
//                        Log.line "%A" i
//                    Log.stop()
//                    fragment <- fragment + 1
//                printfn "press Enter to continue"
//                Console.ReadLine() |> ignore
//            )
//        ) |> ignore

        for i in 0..cnt/2 do
            match candidates with
                | x::xs -> 
                    candidates <- xs
                    g.Add x |> ignore
                | _ -> printfn "out of candiates"

     
        let r = System.Random()
        let t = System.Threading.Tasks.Task.Factory.StartNew(fun () ->
            while true do
                System.Threading.Thread.Sleep 10
                printfn "c %A" g.Count
                if g.Count > 0 && r.NextDouble() > 0.5 then
                    let a = g |> Seq.toArray |> (flip Array.get) (r.Next(0,g.Count))
                    g.Remove(a) |> ignore
                    candidates <- a::candidates
                else
                    if List.isEmpty candidates |> not then
                        match candidates with
                            | x::xs -> 
                                candidates <- xs
                                g.Add x |> ignore
                            | _ -> printfn "out of candiates"

            renderJobs |> ASet.toList |> List.length |> printfn "got %d render objs"
        , System.Threading.Tasks.TaskCreationOptions.LongRunning) 


        if useWindow then app.Value |> snd |> (fun s -> s.RenderTask <- RenderTask.ofList [clear; task]; s.Run())
        else



            let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 1, 1, 1)
            let depth = runtime.CreateRenderbuffer(screen, RenderbufferFormat.Depth24Stencil8, 1)

            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, RenderbufferFormat.ofTextureFormat color.Format
                    DefaultSemantic.Depth, depth.Format
                ]

            let fbo = 
                signature.CreateFramebuffer [
                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
        

            clear.Run fbo |> ignore
            OpenTK.Graphics.OpenGL4.GL.Sync()
            let stats = task.Run fbo
            OpenTK.Graphics.OpenGL4.GL.Sync()
            Log.line "%.0f objects" stats.Statistics.DrawCallCount
            let pi = runtime.Download(color, PixFormat.ByteRGBA)
            pi.SaveAsImage(@"C:\Aardwork\urdar.png")
            OpenTK.Graphics.OpenGL4.GL.Sync()
            Telemetry.reset()
            Log.line "starting update test"
            let mutable iterations = 0
            let sw = Stopwatch()
            let disp = System.Collections.Generic.List()
            sw.Start()
            while sw.Elapsed.TotalSeconds < 5.0 do
                clear.Run fbo |> ignore
                task.Run fbo |> ignore
            sw.Stop()

        ()


    module RenderObjects =
        let emptyUniforms =
            { new IUniformProvider with
                member x.TryGetUniform(scope, name) = None
                member x.Dispose() = ()
            }

        let uniformProvider (color : IMod<ITexture>) (depth : IMod<ITexture>) =
            { new IUniformProvider with
                member x.TryGetUniform(scope : Ag.Scope, semantic : Symbol) =
                    if semantic = DefaultSemantic.ColorTexture then Some (color :> IMod)
                    elif semantic = DefaultSemantic.DepthTexture then Some (depth :> IMod)
                    else None

                member x.Dispose() =
                    ()
            }


        let emptyAttributes =
            { new IAttributeProvider with
                member x.All = Seq.empty
                member x.TryGetAttribute name = None
                member x.Dispose() = ()
            }

        let attributeProvider =
            let positions =  ArrayBuffer ([|V3f(-1.0f, -1.0f, 1.0f); V3f(1.0f, -1.0f, 1.0f); V3f(1.0f, 1.0f, 1.0f); V3f(-1.0f, 1.0f, 1.0f)|] :> Array)
            let texCoords = ArrayBuffer ([|V2f.OO; V2f.IO; V2f.II; V2f.OI|] :> Array)

            let pView = BufferView(Mod.constant (positions :> IBuffer), typeof<V3f>)
            let tcView = BufferView(Mod.constant (texCoords :> IBuffer), typeof<V2f>)

            { new IAttributeProvider with
                member x.All = 
                    Seq.ofList [
                        DefaultSemantic.Positions, pView
                        DefaultSemantic.DiffuseColorCoordinates, tcView
                    ]
                member x.TryGetAttribute(name : Symbol) = 
                    if name = DefaultSemantic.Positions then Some pView
                    elif name = DefaultSemantic.DiffuseColorCoordinates then Some tcView
                    else None
                member x.Dispose() = ()
            }

        let baseObject =
            { RenderObject.Create() with
                AttributeScope = Ag.emptyScope
                IsActive = Mod.constant true
                RenderPass = RenderPass.main
                DrawCallInfos = Mod.constant [DrawCallInfo(InstanceCount = 1, FaceVertexCount = 6)]
                Mode = Mod.constant IndexedGeometryMode.TriangleList
                Surface = DefaultSurfaces.constantColor C4f.Gray |> toEffect |> toFShadeSurface |> Mod.constant
                DepthTest = Mod.constant Aardvark.Base.Rendering.DepthTestMode.LessOrEqual
                CullMode = Mod.constant Aardvark.Base.Rendering.CullMode.None
                BlendMode = Mod.constant Aardvark.Base.Rendering.BlendMode.Blend
                FillMode = Mod.constant Aardvark.Base.Rendering.FillMode.Fill
                StencilMode = Mod.constant Aardvark.Base.Rendering.StencilMode.Disabled
                Indices = Mod.constant ([|0;1;2; 0;2;3|] :> Array)
                InstanceAttributes = emptyAttributes
                VertexAttributes = attributeProvider
                Uniforms = emptyUniforms
            }

    [<Test>]
    let ``[GL] memory leak test``() =

        let useWindow = false
        let runtime, app =
            if useWindow then
                let app = new Aardvark.Application.WinForms.OpenGlApplication()
                let win = app.CreateGameWindow()
                
                app.Runtime, Some (app, win)
            else
                let app = new Aardvark.Application.WinForms.OpenGlApplication()
                let runtime = new Runtime()
                let ctx = new Context(runtime)
                runtime.Context <- ctx
                runtime, None


        let ro = RenderObjects.baseObject

        let screen = V2i(1024,1024)


        if useWindow then
             let clear = runtime.CompileClear(app.Value |> snd |> (fun s -> s.FramebufferSignature), ~~C4f.Black, ~~1.0)
             let task = runtime.CompileRender(app.Value |> snd |> (fun s -> s.FramebufferSignature), BackendConfiguration.NativeOptimized, ASet.empty)

             app.Value |> snd |> (fun s -> s.RenderTask <- RenderTask.ofList [clear; task]; s.Run())
        else
            let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 1, 1, 1)
            let depth = runtime.CreateRenderbuffer(screen, RenderbufferFormat.Depth24Stencil8, 1)

            let signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, RenderbufferFormat.ofTextureFormat color.Format
                    DefaultSemantic.Depth, depth.Format
                ]

            let fbo = 
                signature.CreateFramebuffer [
                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]

            let mutable po = Unchecked.defaultof<IPreparedRenderObject>
        
            while true do
                using runtime.Context.ResourceLock (fun _ -> 
                    let rj = RenderObject.Create
                    if po <> Unchecked.defaultof<_> then po.Dispose()
                    po <- runtime.PrepareRenderObject(signature, ro)
                    printfn "GL memory: %A" runtime.Context.MemoryUsage
                )
        ()
