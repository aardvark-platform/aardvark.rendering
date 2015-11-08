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
        let color = runtime.CreateTexture(~~size, ~~TextureFormat.Rgba8, ~~1, ~~1)
        let depth = runtime.CreateRenderbuffer(~~size, ~~RenderbufferFormat.Depth24Stencil8, ~~1)


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

        clear.Run(null, fbo) |> ignore
        task.Run(null, fbo) |> ignore

        let pi = color.Download(0).[0].ToPixImage<byte>(Col.Format.BGRA)

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
                |> Sg.projTrafo ~~(frustum |> Frustum.toTrafo)

        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        using ctx.ResourceLock (fun _ -> 
            Log.line "vendor:   %s" runtime.Context.Driver.vendor
            Log.line "renderer: %s" runtime.Context.Driver.renderer
        )

        let clear = runtime.CompileClear(~~C4f.Black, ~~1.0)
        let task = runtime.CompileRender sg

        let color = runtime.CreateTexture(~~screen, ~~TextureFormat.Rgba8, ~~1, ~~1)
        let depth = runtime.CreateRenderbuffer(~~screen, ~~RenderbufferFormat.Depth24Stencil8, ~~1)

        let fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Colors, ~~({ texture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, ~~(depth :> IFramebufferOutput)
                ]
            )

        clear.Run fbo |> ignore
        let stats = task.Run fbo
        Log.line "%.0f objects" stats.Statistics.DrawCallCount

        let pi = color.Download(0).[0] //ctx.Download(color, PixFormat.ByteBGRA, 0).[0]
        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"

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
                |> Sg.projTrafo ~~(frustum |> Frustum.toTrafo)

        use runtime = new Runtime()
        use ctx = new Context(runtime)
        runtime.Context <- ctx

        using ctx.ResourceLock (fun _ -> 
            Log.line "vendor:   %s" runtime.Context.Driver.vendor
            Log.line "renderer: %s" runtime.Context.Driver.renderer
        )

        let clear = runtime.CompileClear(~~C4f.Black, ~~1.0)
        let renderJobs = sg.RenderObjects()
        let task = runtime.CompileRender renderJobs
        //let task2 = runtime.CompileRender renderJobs

        let color = runtime.CreateTexture(screen, TextureFormat.Rgba8, 1, 1, 1)
        let depth = runtime.CreateRenderbuffer(screen, RenderbufferFormat.Depth24Stencil8, 1)

        let fbo = 
            runtime.CreateFramebuffer(
                Map.ofList [
                    DefaultSemantic.Colors, ({ backendTexture = color; slice = 0; level = 0 } :> IFramebufferOutput)
                    DefaultSemantic.Depth, (depth :> IFramebufferOutput)
                ]
            )

        clear.Run fbo |> ignore
        OpenTK.Graphics.OpenGL4.GL.Sync()
        let stats = task.Run fbo
        OpenTK.Graphics.OpenGL4.GL.Sync()
        Log.line "%.0f objects" stats.Statistics.DrawCallCount
        let pi = ctx.Download((color |> unbox<Texture>), PixFormat.ByteRGBA, 0).[0] //.Download(0).[0] //ctx.Download(color, PixFormat.ByteBGRA, 0).[0]
        pi.SaveAsImage @"C:\Users\schorsch\Desktop\test.png"
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
            let t = runtime.CompileRender renderJobs
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