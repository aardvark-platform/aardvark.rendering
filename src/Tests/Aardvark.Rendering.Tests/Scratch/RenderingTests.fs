namespace Aardvark.Rendering.Tests

// TODO: This is a graveyard. RIP!
//       We should check if we can salvage anything relevant

//open System
//open NUnit.Framework
//open FsUnit
//open OpenTK.Graphics.OpenGL4
//open Aardvark.Rendering.GL
//open Aardvark.Base
//open FSharp.Data.Adaptive
//open Aardvark.SceneGraph
//open FSharp.Data.Adaptive.Operators
//open Aardvark.Application
//open System.Diagnostics
//open Aardvark.SceneGraph.Semantics
//open Aardvark.Application.WinForms

//open Aardvark.Rendering


//module ``Rendering Tests`` =
//    open System.IO
//    do Report.LogFileName <- Path.GetTempFileName()
//    let quadGeometry =
//        IndexedGeometry(
//            Mode = IndexedGeometryMode.TriangleList,
//            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
//            IndexedAttributes =
//                SymDict.ofList [
//                    DefaultSemantic.Positions, [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
//                    DefaultSemantic.DiffuseColorCoordinates, [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array

//                ]
//        )

//    let checkerBoardImage = 
//        let img = PixImage<byte>(Col.Format.RGBA, V2i(256, 256))

//        img.GetMatrix<C4b>().SetByCoord (fun (c : V2l) ->
//            let xy = c.X / 32L + c.Y / 32L
//            if xy % 2L = 0L then C4b.White
//            else C4b.Gray
//        ) |> ignore

//        img

//    let checkerBoardTexture =
//        PixTexture2d(PixImageMipMap [|checkerBoardImage :> PixImage|], true) :> ITexture |> AVal.constant


//    [<Test; Ignore("Broken")>]
//    let ``[Vulkan] textures working``() =
//        Aardvark.Init()

//        use app = new OpenGlApplication()
//        let runtime = app.Runtime

//        let fbos =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Colors, { format = TextureFormat.Rgba8; samples = 1 }
//            ]

//        let tex = runtime.CreateTexture(checkerBoardImage.Size, TextureFormat.Rgba8, 1, 1, 1)

//        let fbo =
//            runtime.CreateFramebuffer(fbos, 
//                [
//                    DefaultSemantic.Colors, { texture = tex; level = 0; slice = 0 } :> IFramebufferOutput
//                ])

//        let render = 
//            quadGeometry
//                |> Sg.ofIndexedGeometry
//                |> Sg.diffuseTexture checkerBoardTexture
//                |> Sg.effect [ DefaultSurfaces.diffuseTexture |> toEffect ]
//                |> Sg.depthTest' DepthTest.None
//                |> Sg.compile runtime fbos


//        render.Run(RenderToken.Empty, fbo)

//        let test = runtime.Download(tex)
        
//        checkerBoardImage.SaveAsImage @"C:\Users\schorsch\Desktop\in.jpg"
//        test.SaveAsImage @"C:\Users\schorsch\Desktop\test.jpg"
//        match test with
//            | :? PixImage<byte> as test ->
//                let eq = test.Volume.InnerProduct(checkerBoardImage.Volume, (=), true, (&&))
//                if not eq then 
//                    failwithf "unexpected image content (not a checkerboard)"
//            | _ ->
//                failwithf "unexpected image type: %A" test

//        ()


//module Vector =

//    let inline ofArray (arr : 'a[]) =
//        Vector<'a>(arr)

//    let inline ofSeq (s : seq<'a>) =
//        s |> Seq.toArray |> ofArray

//    let inline ofList (s : list<'a>) =
//        s |> List.toArray |> ofArray

//    let inline toSeq (v : Vector<'a>) =
//        v.Data :> seq<_>

//    let inline toList (v : Vector<'a>) =
//        v.Data |> Array.toList

//    let inline toArray (v : Vector<'a>) =
//        v.Data

//    let init (size : int) (f : int -> 'a) =
//        Vector<'a>(Array.init size f)

//    let create (size : int) (value : 'a) =
//        Vector<'a>(Array.create size value)

//    let zeroCreate (size : int) =
//        Vector<'a>(Array.zeroCreate size)

//    let inline map (f : 'a -> 'b) (v : Vector<'a>) =
//        v.Map(f)

//    let inline map2 (f : 'a -> 'b -> 'c) (l : Vector<'a>) (r : Vector<'b>) =
//        let res = Vector<'c>(min l.Size r.Size)
//        res.SetMap2(l, r, f)

//    let inline fold (f : 's -> 'a -> 's) (seed : 's) (v : Vector<'a>) =
//        v.Norm(id, seed, f)

//    let inline fold2 (f : 's -> 'a -> 'b -> 's) (seed : 's) (a : Vector<'a>) (b : Vector<'b>) =
//        a.InnerProduct(b, (fun a b -> (a,b)), seed, fun s (a,b) -> f s a b)

//    let inline dot (l : Vector<'a>) (r : Vector<'b>) : 'c =
//        l.InnerProduct(r, (*), LanguagePrimitives.GenericZero, (+))

//    let inline normSquared (v : Vector<'a>) : 'b =
//        v.Norm((fun a -> a * a), LanguagePrimitives.GenericZero, (+))

//    let inline norm (v : Vector<'a>) : 'b =
//        sqrt (normSquared v)

//module Matrix =

//    let init (size : V2i) (f : V2i -> 'a) =
//        Matrix<'a>(size).SetByCoord(fun (c : V2l) -> f (V2i c))
   
//    let inline fold (f : 's -> 'a -> 's) (seed : 's) (m : Matrix<'a>) =
//        m.Norm(id, seed, f)

//    let inline fold2 (f : 's -> 'a -> 'b -> 's) (seed : 's) (a : Matrix<'a>) (b : Matrix<'b>) =
//        a.InnerProduct(b, (fun a b -> a,b), seed, fun s (a,b) -> f s a b)

//    let inline equal (l : Matrix<'a>) (r : Matrix<'a>) =
//        l.InnerProduct(r, (=), true, (&&), not)

//    let inline notEqual (l : Matrix<'a>) (r : Matrix<'a>) =
//        equal l r |> not

//[<AutoOpen>]
//module TensorSlices =
    
//    type Vector<'a> with
//        member x.GetSlice(first : Option<int>, last : Option<int>) =
//            let first = defaultArg first 0
//            let last = defaultArg last (int x.Size - 1)

//            x.SubVector(first, 1 + last - first)

//        member x.SetSlice(first : Option<int>, last : Option<int>, value : 'a) =
//            x.GetSlice(first, last).Set(value) |> ignore

//    type Vector<'a, 'b> with
//        member x.GetSlice(first : Option<int>, last : Option<int>) =
//            let first = defaultArg first 0
//            let last = defaultArg last (int x.Size - 1)

//            x.SubVector(first, 1 + last - first)

//        member x.SetSlice(first : Option<int>, last : Option<int>, value : 'b) =
//            x.GetSlice(first, last).Set(value) |> ignore


//    type Matrix<'a> with

//        member x.GetSlice(first : Option<V2i>, last : Option<V2i>) =
//            let first = defaultArg first V2i.Zero
//            let last = defaultArg last (V2i x.Size - V2i.II)

//            x.SubMatrix(first, V2i.II + last - first)

//        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>) =
//            let xStart = defaultArg xStart 0
//            let xEnd = defaultArg xEnd (int x.Size.X - 1)
//            let yStart = defaultArg yStart 0
//            let yEnd = defaultArg yEnd (int x.Size.Y - 1)
            
//            let p0 = V2i(xStart, yStart)
//            let size = V2i(xEnd + 1, yEnd + 1) - p0

//            x.SubMatrix(p0, size)

//        member m.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>) =
//            m.SubYVector(int64 x).GetSlice(yStart, yEnd)

//        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int) =
//            x.SubXVector(int64 y).GetSlice(xStart, xEnd)

//    type Matrix<'a, 'b> with

//        member x.GetSlice(first : Option<V2i>, last : Option<V2i>) =
//            let first = defaultArg first V2i.Zero
//            let last = defaultArg last (V2i x.Size - V2i.II)

//            x.SubMatrix(first, V2i.II + last - first)

//        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>) =
//            let xStart = defaultArg xStart 0
//            let xEnd = defaultArg xEnd (int x.Size.X - 1)
//            let yStart = defaultArg yStart 0
//            let yEnd = defaultArg yEnd (int x.Size.Y - 1)
            
//            let p0 = V2i(xStart, yStart)
//            let size = V2i(xEnd + 1, yEnd + 1) - p0

//            x.SubMatrix(p0, size)

//        member m.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>) =
//            m.SubYVector(int64 x).GetSlice(yStart, yEnd)

//        member x.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int) =
//            x.SubXVector(int64 y).GetSlice(xStart, xEnd)


//    type Volume<'a> with
//        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
//            v.SubYZMatrix(int64 x).GetSlice(yStart, yEnd, zStart, zEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, zStart : Option<int>, zEnd : Option<int>) =
//            v.SubXZMatrix(int64 y).GetSlice(xStart, xEnd, zStart, zEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, z : int) =
//            v.SubXYMatrix(int64 z).GetSlice(xStart, xEnd, yStart, yEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, z : int) =
//            v.SubXYMatrix(int64 z).SubXVector(int64 y).GetSlice(xStart, xEnd)

//        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, z : int) =
//            v.SubXYMatrix(int64 z).SubYVector(int64 x).GetSlice(yStart, yEnd)

//        member v.GetSlice(x : int, y : int, zStart : Option<int>, zEnd : Option<int>) =
//            v.SubYZMatrix(int64 x).SubYVector(int64 y).GetSlice(zStart, zEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
//            let xStart = defaultArg xStart 0
//            let xEnd = defaultArg xEnd (int v.Size.X - 1)
//            let yStart = defaultArg yStart 0
//            let yEnd = defaultArg yEnd (int v.Size.Y - 1)
//            let zStart = defaultArg zStart 0
//            let zEnd = defaultArg zEnd (int v.Size.Z - 1)
            
//            let p0 = V3i(xStart, yStart, zStart)
//            let s = V3i(1 + xEnd, 1 + yEnd, 1 + zEnd) - p0

//            v.SubVolume(p0, s)

//    type Volume<'a, 'b> with
//        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
//            v.SubYZMatrix(int64 x).GetSlice(yStart, yEnd, zStart, zEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, zStart : Option<int>, zEnd : Option<int>) =
//            v.SubXZMatrix(int64 y).GetSlice(xStart, xEnd, zStart, zEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, z : int) =
//            v.SubXYMatrix(int64 z).GetSlice(xStart, xEnd, yStart, yEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, y : int, z : int) =
//            v.SubXYMatrix(int64 z).SubXVector(int64 y).GetSlice(xStart, xEnd)

//        member v.GetSlice(x : int, yStart : Option<int>, yEnd : Option<int>, z : int) =
//            v.SubXYMatrix(int64 z).SubYVector(int64 x).GetSlice(yStart, yEnd)

//        member v.GetSlice(x : int, y : int, zStart : Option<int>, zEnd : Option<int>) =
//            v.SubYZMatrix(int64 x).SubYVector(int64 y).GetSlice(zStart, zEnd)

//        member v.GetSlice(xStart : Option<int>, xEnd : Option<int>, yStart : Option<int>, yEnd : Option<int>, zStart : Option<int>, zEnd : Option<int>) =
//            let xStart = defaultArg xStart 0
//            let xEnd = defaultArg xEnd (int v.Size.X - 1)
//            let yStart = defaultArg yStart 0
//            let yEnd = defaultArg yEnd (int v.Size.Y - 1)
//            let zStart = defaultArg zStart 0
//            let zEnd = defaultArg zEnd (int v.Size.Z - 1)
            
//            let p0 = V3i(xStart, yStart, zStart)
//            let s = V3i(1 + xEnd, 1 + yEnd, 1 + zEnd) - p0

//            v.SubVolume(p0, s)



//module RenderingTests =
    
//    do IntrospectionProperties.CustomEntryAssembly <- System.Reflection.Assembly.GetAssembly(typeof<ISg>)
//    do Aardvark.Init()

//    let quad = 
//        IndexedGeometry(
//            Mode = IndexedGeometryMode.TriangleList,
//            IndexArray = [| 0; 1; 2; 0; 2; 3 |],
//            IndexedAttributes =
//                SymDict.ofList [
//                    DefaultSemantic.Positions,                  [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
//                    DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
//                    DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
//                ]
//        )
      
      
     

//    [<Test; Ignore("Broken")>]
//    let ``[GL] simple render to texture``() =
        
//        let vec = Vector.zeroCreate 1000
//        vec.[0..9] <- 1.0
        
//        let len = vec |> Vector.norm

//        printfn "%A" len

//        use runtime = new Runtime()
//        use ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create false)
//        runtime.Initialize(ctx)

//        let size = V2i(1024,768)
        
//        let color = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
//        let depth = runtime.CreateRenderbuffer(size, TextureFormat.Depth24Stencil8, 1)


//        let signature =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Colors, { format = TextureFormat.Rgba8; samples = 1 }
//                DefaultSemantic.Depth, { format = TextureFormat.Depth24Stencil8; samples = 1 }
//            ]

//        let fbo = 
//            runtime.CreateFramebuffer(
//                signature, 
//                Map.ofList [
//                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
//                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
//                ]
//            ) 
//        let outputDesc = OutputDescription.ofFramebuffer fbo


        
//        let sg =
//            quad 
//                |> Sg.ofIndexedGeometry
//                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

        
//        use task = runtime.CompileRender(signature, sg)
//        use clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)

//        clear.Run(null, outputDesc) |> ignore
//        task.Run(null, outputDesc) |> ignore
        

//        let pi = PixImage<byte>(Col.Format.BGRA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
//        runtime.Download(color, 0, 0, pi)

//        let cmp = PixImage<byte>(Col.Format.BGRA, size)
//        cmp.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
//            if c.X >= int64 size.X / 2L && c.Y >= int64 size.Y / 2L then
//                C4b.White
//            else
//                C4b.Black
//        ) |> ignore




//        pi.SaveAsImage @"C:\Users\haaser\Desktop\test.png"


//        ()

//    [<Test; Ignore("Broken")>]
//    let ``[GL] simple render to multiple texture``() =

//        use runtime = new Runtime()
//        use ctx = new Context(runtime, (fun () -> ContextHandleOpenTK.create false))
//        runtime.Initialize(ctx)

//        let size = V2i(1024,768)
//        let color = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
//        let normals = runtime.CreateTexture(size, TextureFormat.Rgba32f, 1, 1, 1)
//        let depth = runtime.CreateRenderbuffer(size, TextureFormat.Depth24Stencil8, 1)


//        let signature =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Colors, { format = TextureFormat.Rgba8; samples = 1 }
//                DefaultSemantic.Normals, { format = TextureFormat.Rgba32f; samples = 1 }
//                DefaultSemantic.Depth, { format = TextureFormat.Depth24Stencil8; samples = 1 }
//            ]

//        let fbo = 
//            runtime.CreateFramebuffer(
//                signature, 
//                Map.ofList [
//                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
//                    DefaultSemantic.Normals, ({ texture = normals; slice = 0; level = 0 } :> IFramebufferOutput)
//                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
//                ]
//            )
//        let outputDesc = OutputDescription.ofFramebuffer fbo


        
//        let sg =
//            quad 
//                |> Sg.ofIndexedGeometry
//                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]

        
//        use task = runtime.CompileRender(signature, sg)
//        use clear = 
//            runtime.CompileClear(
//                signature, 
//                ~~[DefaultSemantic.Colors, C4f.Black; DefaultSemantic.Normals, C4f.Red], 
//                ~~1.0
//            )

//        clear.Run(null, outputDesc) |> ignore
//        task.Run(null, outputDesc) |> ignore

//        let pi = PixImage<byte>(Col.Format.BGRA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
//        runtime.Download(color, 0, 0, pi)

//        let npi = PixImage<float32>(Col.Format.RGBA, size) //color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)
//        runtime.Download(normals, 0, 0, npi)


//        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"
//        PixImage<float32>(npi.Volume.Map(fun f -> f)).ToPixImage<byte>(Col.Format.RGB).SaveAsImage @"C:\Users\schorsch\Desktop\testNormals.png"


//        ()

//    [<Test; Ignore("Broken")>]
//    let ``[GL] nested trafos``() =
        
//        let leaf = quad |> Sg.ofIndexedGeometry
//        let screen = V2i(2048, 2048)

//        let grid (size : V2i) (inner : ISg) =
//            Sg.ofList [
//                for x in -size.X/2..size.X/2 do
//                    for y in -size.Y/2..size.Y/2 do
//                        yield inner |> Sg.trafo (~~Trafo3d.Translation(float x, float y, 0.0))
//            ]


//        let rec buildGrid (depth : int) =
//            if depth <= 0 then 
//                leaf |> Sg.trafo (~~Trafo3d.Scale(0.5))  
//            else 
//                depth - 1 
//                    |> buildGrid 
//                    |> grid (5 * V2i.II)
//                    |> Sg.trafo (~~Trafo3d.Scale(0.125))


//        let cam = CameraView.lookAt (0.5 * V3d.OOI) V3d.Zero V3d.OIO
//        let frustum = Frustum.perspective 60.0 0.1 1000.0 (float screen.X / float screen.Y)

//        let rootTrafo = AVal.init Trafo3d.Identity

//        let sg =
//            buildGrid 3
//                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]
//                |> Sg.trafo rootTrafo
//                |> Sg.viewTrafo ~~(cam |> CameraView.viewTrafo)
//                |> Sg.projTrafo ~~(frustum |> Frustum.projTrafo)

//        use runtime = new Runtime()
//        use ctx = new Context(runtime, (fun () -> ContextHandleOpenTK.create false))
//        runtime.Initialize(ctx)

//        using ctx.ResourceLock (fun _ -> 
//            Log.line "vendor:   %s" runtime.Context.Driver.vendor
//            Log.line "renderer: %s" runtime.Context.Driver.renderer
//        )

//        let signature =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Colors, { format = TextureFormat.Rgba8; samples = 1 }
//                DefaultSemantic.Depth, { format = TextureFormat.Depth24Stencil8; samples = 1 }
//            ]

//        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
//        let task = runtime.CompileRender(signature, sg)

//        let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 2, 1, 1)
//        let depth = runtime.CreateRenderbuffer(screen, TextureFormat.Depth24Stencil8, 1)

//        let fbo = 
//            runtime.CreateFramebuffer(
//                signature,
//                Map.ofList [
//                    DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
//                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
//                ]
//            )

//        clear.Run(RenderToken.Empty, fbo)
//        let token = RenderToken()
//        task.Run(token, fbo)
//        Log.line "%d objects" token.DrawCallCount


//        runtime.GenerateMipMaps(color)


//        let pi = runtime.Download(color, PixFormat.ByteRGBA)
//        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"

//        let level1 = runtime.Download(color, 1, PixFormat.ByteRGBA)
//        level1.SaveAsImage @"C:\Users\schorsch\Desktop\level1.png"


//        Log.line "starting pure render test"
//        let mutable iterations = 0
//        let sw = Stopwatch()
//        sw.Start()
//        while sw.Elapsed.TotalSeconds < 10.0 do
//            clear.Run(RenderToken.Empty, fbo)
//            task.Run(RenderToken.Empty, fbo)
//            iterations <- iterations + 1
//        sw.Stop()

//        let pureRenderTime = sw.Elapsed.TotalSeconds / float iterations

//        Log.line "starting update test"
//        let mutable iterations = 0
//        let sw = Stopwatch()
//        sw.Start()
//        while sw.Elapsed.TotalSeconds < 20.0 || iterations < 50 do
//            transact(fun () -> rootTrafo.Value <- (rootTrafo.Value * Trafo3d.Scale(1.00001)))
//            clear.Run(RenderToken.Empty, fbo) |> ignore
//            task.Run(RenderToken.Empty, fbo) |> ignore
//            iterations <- iterations + 1
//        sw.Stop()

//        let updateAndRenderTime = sw.Elapsed.TotalSeconds / float iterations
            

//        let updateTime = updateAndRenderTime - pureRenderTime

//        Log.line "total:        %.2ffps" (1.0 / updateAndRenderTime)
//        Log.line "rendering:    %.2ffps" (1.0 / pureRenderTime)
//        Log.line "updates:      %.2ffps" (1.0 / updateTime)

//        ()

//    [<Test; Ignore("Broken")>]
//    let ``[GL] compile performance``() =
//        let leaf = quad |> Sg.ofIndexedGeometry
//        let screen = V2i(2048, 2048)

//        let grid (size : V2i) (inner : ISg) =
//            Sg.ofList [
//                for x in -size.X/2..size.X/2 do
//                    for y in -size.Y/2..size.Y/2 do
//                        yield inner |> Sg.trafo (~~Trafo3d.Translation(float x, float y, 0.0))
//            ]


//        let rec buildGrid (depth : int) =
//            if depth <= 0 then 
//                leaf |> Sg.trafo (~~Trafo3d.Scale(0.5))  
//            else 
//                depth - 1 
//                    |> buildGrid 
//                    |> grid (5 * V2i.II)
//                    |> Sg.trafo (~~Trafo3d.Scale(0.125))


//        let cam = CameraView.lookAt (0.5 * V3d.OOI) V3d.Zero V3d.OIO
//        let frustum = Frustum.perspective 60.0 0.1 1000.0 (float screen.X / float screen.Y)

//        let rootTrafo = AVal.init Trafo3d.Identity

//        let sg =
//            buildGrid 3
//                |> Sg.effect [DefaultSurfaces.trafo |> toEffect; DefaultSurfaces.constantColor C4f.White |> toEffect]
//                |> Sg.trafo rootTrafo
//                |> Sg.viewTrafo ~~(cam |> CameraView.viewTrafo)
//                |> Sg.projTrafo ~~(frustum |> Frustum.projTrafo)

//        use runtime = new Runtime()
//        use ctx = new Context(runtime, (fun () -> ContextHandleOpenTK.create false))
//        runtime.Initialize(ctx)

//        using ctx.ResourceLock (fun _ -> 
//            Log.line "vendor:   %s" runtime.Context.Driver.vendor
//            Log.line "renderer: %s" runtime.Context.Driver.renderer
//        )

//        let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 1, 1, 1)
//        let depth = runtime.CreateRenderbuffer(screen, TextureFormat.Depth24Stencil8, 1)

//        let signature =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Colors, TextureFormat.ofTextureFormat color.Format
//                DefaultSemantic.Depth, depth.Format
//            ]

//        let fbo = 
//            signature.CreateFramebuffer [
//                DefaultSemantic.Colors, ({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
//                DefaultSemantic.Depth, (depth :> IFramebufferOutput)
//            ]

//        let clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
//        let renderJobs = sg.RenderObjects(Ag.Scope.Root)
//        let task = runtime.CompileRender(signature, renderJobs)
//        //let task2 = runtime.CompileRender renderJobs



//        clear.Run(RenderToken.Empty, fbo) |> ignore
//        GL.Sync()
//        let token = RenderToken()
//        task.Run(token, fbo)
//        GL.Sync()
//        Log.line "%d objects" token.DrawCallCount
//        let pi = runtime.Download(color, PixFormat.ByteRGBA)
//        pi.SaveAsImage(@"C:\Aardwork\gugu.png")
//        GL.Sync()
//        //task2.Run fbo |> ignore
//        //OpenTK.Graphics.OpenGL4.GL.Sync()


//        Log.line "starting update test"
//        let mutable iterations = 0
//        let sw = Stopwatch()
//        let disp = System.Collections.Generic.List()
//        sw.Start()
//        while sw.Elapsed.TotalSeconds < 20.0 do
//            let t = runtime.CompileRender(signature,renderJobs)
//            clear.Run(RenderToken.Empty, fbo)
//            t.Run(RenderToken.Empty, fbo)
//            iterations <- iterations + 1
//            disp.Add t
//        sw.Stop()

//        for t in disp do
//            t.Dispose()
//        let updateAndRenderTime = sw.Elapsed.TotalSeconds / float iterations
            
//        Log.line "compile + render: %.2fs" (updateAndRenderTime)

//        ()

//    module RenderObjects =
//        let emptyUniforms =
//            { new IUniformProvider with
//                member x.TryGetUniform(scope, name) = None
//                member x.Dispose() = ()
//            }

//        let uniformProvider (color : aval<ITexture>) (depth : aval<ITexture>) =
//            { new IUniformProvider with
//                member x.TryGetUniform(scope : Ag.Scope, semantic : Symbol) =
//                    if semantic = DefaultSemantic.ColorTexture then Some (color :> IAdaptiveValue)
//                    elif semantic = DefaultSemantic.DepthTexture then Some (depth :> IAdaptiveValue)
//                    else None

//                member x.Dispose() =
//                    ()
//            }


//        let emptyAttributes =
//            { new IAttributeProvider with
//                member x.All = Seq.empty
//                member x.TryGetAttribute name = None
//                member x.Dispose() = ()
//            }

//        let attributeProvider =
//            let positions =  ArrayBuffer ([|V3f(-1.0f, -1.0f, 1.0f); V3f(1.0f, -1.0f, 1.0f); V3f(1.0f, 1.0f, 1.0f); V3f(-1.0f, 1.0f, 1.0f)|] :> Array)
//            let texCoords = ArrayBuffer ([|V2f.OO; V2f.IO; V2f.II; V2f.OI|] :> Array)

//            let pView = BufferView(AVal.constant (positions :> IBuffer), typeof<V3f>)
//            let tcView = BufferView(AVal.constant (texCoords :> IBuffer), typeof<V2f>)

//            { new IAttributeProvider with
//                member x.All = 
//                    Seq.ofList [
//                        DefaultSemantic.Positions, pView
//                        DefaultSemantic.DiffuseColorCoordinates, tcView
//                    ]
//                member x.TryGetAttribute(name : Symbol) = 
//                    if name = DefaultSemantic.Positions then Some pView
//                    elif name = DefaultSemantic.DiffuseColorCoordinates then Some tcView
//                    else None
//                member x.Dispose() = ()
//            }

//        let baseObject =
//            { RenderObject.Create() with
//                AttributeScope = Ag.Scope.Root
//                IsActive = AVal.constant true
//                RenderPass = RenderPass.main
//                DrawCalls = Direct(AVal.constant [DrawCallInfo(InstanceCount = 1, FaceVertexCount = 6)])
//                Mode = IndexedGeometryMode.TriangleList
//                Surface = DefaultSurfaces.constantColor C4f.Gray |> toEffect |> Surface.FShadeSimple
//                DepthState      = DepthState.Default
//                BlendState      = { BlendState.Default with Mode = AVal.constant BlendMode.Blend }
//                StencilState    = StencilState.Default
//                RasterizerState = RasterizerState.Default
//                Indices = BufferView.ofArray [|0;1;2; 0;2;3|] |> Some
//                InstanceAttributes = emptyAttributes
//                VertexAttributes = attributeProvider
//                Uniforms = emptyUniforms
//            }

//module UseTest =
    
//    let bla () =
        
//        Aardvark.Init()

//        use runtime = new Runtime()
//        use ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create false)
//        runtime.Initialize(ctx)

//        let size = V2i(1024,1024)
        
//        let color0 = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
//        let color1 = runtime.CreateTexture(size, TextureFormat.Rgba8, 1, 1, 1)
//        let depth = runtime.CreateRenderbuffer(size, TextureFormat.Depth24Stencil8, 1)


//        let signature =
//            runtime.CreateFramebufferSignature [
//                DefaultSemantic.Colors, { format = TextureFormat.Rgba8; samples = 1 }
//                DefaultSemantic.Depth, { format = TextureFormat.Depth24Stencil8; samples = 1 }
//            ]

//        let fbo0 = 
//            runtime.CreateFramebuffer(
//                signature, 
//                Map.ofList [
//                    DefaultSemantic.Colors, ({ texture = color0; slice = 0; level = 0 } :> IFramebufferOutput)
//                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
//                ]
//            ) 

//        let fbo1 = 
//            runtime.CreateFramebuffer(
//                signature, 
//                Map.ofList [
//                    DefaultSemantic.Colors, ({ texture = color1; slice = 0; level = 0 } :> IFramebufferOutput)
//                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
//                ]
//            ) 

//        let o0 = OutputDescription.ofFramebuffer fbo0
//        let o1 = OutputDescription.ofFramebuffer fbo1


//        let cam = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
//        let model = AVal.init Trafo3d.Identity
//        let view = AVal.init cam
//        let sg =
//            Sg.box' C4b.White (Box3d(-V3d.III, V3d.III))
//                |> Sg.effect [
//                    DefaultSurfaces.trafo |> toEffect
//                    DefaultSurfaces.constantColor C4f.White |> toEffect
//                ]
//                |> Sg.trafo model
//                |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
//                |> Sg.projTrafo (Frustum.perspective 60.0 0.1 100.0 1.0 |> Frustum.projTrafo |> AVal.constant)

        
//        use clear = runtime.CompileClear(signature, ~~C4f.Black, ~~1.0)
//        use render = runtime.CompileRender(signature, sg)
//        let task = RenderTask.ofList [ clear; render ]

//        let render (cam0 : CameraView) (cam1 : CameraView) =
//            task.Use (fun () ->
//                transact (fun () -> view.Value <- cam0)
//                task.Run(RenderToken.Empty, o0) |> ignore

//                transact (fun () -> view.Value <- cam1)
//                task.Run(RenderToken.Empty, o1) |> ignore
//            )

//        let trafos =  [| Trafo3d.Scale 0.1 ; Trafo3d.Scale 1.0 |]
//        let changer =
//            async {
//                do! Async.SwitchToNewThread()
//                let mutable i = 0
//                while true do
//                    transact (fun () -> model.Value <- trafos.[i])
//                    i <- (i + 1) % 2
//            }
//        Async.Start changer

//        let cam0 = CameraView.lookAt (V3d.III * 3.0) V3d.Zero V3d.OOI
//        let cam1 = CameraView.lookAt (-V3d.III * 3.0) V3d.Zero V3d.OOI
//        for i in 0 .. 20 do
//            render cam0 cam1

//            let p0 = runtime.Download(color0, PixFormat.ByteRGBA) |> unbox<PixImage<byte>>
//            let pp1 = runtime.Download(color1, PixFormat.ByteRGBA) |> unbox<PixImage<byte>>
//            let p1 = PixImage<byte>(pp1.Format, pp1.Volume.Transformed(ImageTrafo.MirrorY).ToImage())

//            let m0 = p0.GetMatrix<C4b>()
//            let m1 = p1.GetMatrix<C4b>()

//            let mutable iter = 0
//            printfn "storing: %A" i
//            p0.SaveAsImage(sprintf @"C:\Users\schorsch\Desktop\urdar\p%d_left.tif" i)
//            p1.SaveAsImage(sprintf @"C:\Users\schorsch\Desktop\urdar\p%d_right.tif" i)

//            let equal = m0.InnerProduct(m1, (=), true, (&&)) 
//            if not equal then
//                p0.SaveAsImage @"C:\Users\schorsch\Desktop\p0.tif"
//                p1.SaveAsImage @"C:\Users\schorsch\Desktop\p1.tif"
//                failwith "unequal"
//            ()





//        ()
