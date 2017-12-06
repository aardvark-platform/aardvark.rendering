﻿namespace Examples


open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.Text
open System.Runtime.InteropServices
open Aardvark.SceneGraph
open Aardvark.SceneGraph.IO
open System.Threading
open System.Threading.Tasks


module LevelOfDetail =

    type Octree(bounds : Box3d) =
        let children = 
            lazy (
                let min = bounds.Min
                let max = bounds.Max
                let c = bounds.Center
                MapExt.ofList [
                    0, Octree(Box3d(min.X, min.Y, min.Z, c.X, c.Y, c.Z))
                    1, Octree(Box3d(min.X, min.Y, c.Z, c.X, c.Y, max.Z))
                    2, Octree(Box3d(min.X, c.Y, min.Z, c.X, max.Y, c.Z))
                    3, Octree(Box3d(min.X, c.Y, c.Z, c.X, max.Y, max.Z))
                    4, Octree(Box3d(c.X, min.Y, min.Z, max.X, c.Y, c.Z))
                    5, Octree(Box3d(c.X, min.Y, c.Z, max.X, c.Y, max.Z))
                    6, Octree(Box3d(c.X, c.Y, min.Z, max.X, max.Y, c.Z))
                    7, Octree(Box3d(c.X, c.Y, c.Z, max.X, max.Y, max.Z))
                ]
            )

        let data =
            async {
                do! Async.Sleep 100
                return bounds
            }

        member x.Bounds = bounds

        interface LodTreeNode<Octree, Box3d> with
            member x.Load = Some data
            member x.Children = children.Value
 
    let runTest() =

        let tree = Octree(Box3d(-V3d.III, V3d.III))

        let desiredLen = Mod.init 1.0

        let visible (b : Octree) = true
        let descend (l : float) (b : Octree) = b.Bounds.Size.Length > l
        

        let root = Mod.constant (Some tree)

        let existing = System.Collections.Concurrent.ConcurrentHashSet<Box3d>()
        let boxes = System.Collections.Generic.HashSet<Box3d>()

        let view =
            {
                root = Mod.constant (Some tree)
                visible = Mod.constant visible
                descend = desiredLen |> Mod.map descend
                showInner = false
            }

        let loaderCfg =
            {
                prepare     = fun b -> existing.Add b |> ignore; b
                delete      = fun b -> existing.Remove b |> ignore
                activate    = boxes.Add >> ignore
                deactivate  = boxes.Remove >> ignore
                flush = id
            }

        let loader = LodTreeLoader.create view
        let thread = LodTreeLoader.start loaderCfg loader



        let print =
            async {
                do! Async.SwitchToNewThread()
                let mutable oldCount = (-1, -1)
                while true do
                    Thread.Sleep 5
                    let cnt = (boxes.Count, existing.Count)
                    if cnt <> oldCount then
                        let (active, existing) = cnt
                        Log.line "count: %A (%A)" active existing
                        oldCount <- cnt

            }

        Async.Start print

        while true do
            let line = Console.ReadLine()
            match line with
                | "+" ->
                    transact (fun () -> desiredLen.Value <- desiredLen.Value / 2.0)
                | _ ->
                    transact (fun () -> desiredLen.Value <- desiredLen.Value * 2.0)
            Log.line "len: %A" desiredLen.Value



        ()





    open Aardvark.Rendering.Vulkan

    type GeometryTree(bounds : Box3d) =
        let children = 
            lazy (
                let min = bounds.Min
                let max = bounds.Max
                let c = bounds.Center
                MapExt.ofList [
                    0, GeometryTree(Box3d(min.X, min.Y, min.Z, c.X, c.Y, c.Z))
                    1, GeometryTree(Box3d(min.X, min.Y, c.Z, c.X, c.Y, max.Z))
                    2, GeometryTree(Box3d(min.X, c.Y, min.Z, c.X, max.Y, c.Z))
                    3, GeometryTree(Box3d(min.X, c.Y, c.Z, c.X, max.Y, max.Z))
                    4, GeometryTree(Box3d(c.X, min.Y, min.Z, max.X, c.Y, c.Z))
                    5, GeometryTree(Box3d(c.X, min.Y, c.Z, max.X, c.Y, max.Z))
                    6, GeometryTree(Box3d(c.X, c.Y, min.Z, max.X, max.Y, c.Z))
                    7, GeometryTree(Box3d(c.X, c.Y, c.Z, max.X, max.Y, max.Z))
                ]
            )

        let data =
            async {
                do! Async.SwitchToThreadPool()
                let sphere = Primitives.unitSphere 5
                let trafo = Trafo3d.Scale(0.5 * bounds.Size) * Trafo3d.Translation(bounds.Center)
                let uniforms = Map.ofList ["ModelTrafo", Mod.constant trafo :> IMod]

                return Geometry.ofIndexedGeometry uniforms sphere
            }

        member x.Bounds = bounds

        interface LodTreeNode<GeometryTree, Geometry> with
            member x.Load = Some data
            member x.Children = children.Value
        

    open System.Runtime.CompilerServices

    let tcGradient (sx : int) (sy : int) =
        let size = V2i(sx, sy)
        let img = PixImage<byte>(Col.Format.RGBA, size)
        img.GetMatrix<C4b>().SetByCoord (fun (c : V2l) ->
            let c = (V2d c + V2d(0.5, 0.5)) / V2d size
            C4f(float32 c.X, float32 c.Y, 1.0f, 1.0f).ToC4b()
        ) |> ignore
        img

    
    open System.Windows.Forms


    let run() =
        let vk = new VulkanApplication(true)
        let gl = new OpenGlApplication(true)
        
        let app = new MultiApplication([|vk; gl|]) :> IApplication 
        let win = app.CreateSimpleRenderWindow(8)

        let view =
            CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
                |> Mod.map CameraView.viewTrafo

        let proj =
            win.Sizes 
                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
                |> Mod.map Frustum.projTrafo

        let sg = 
            Sg.box' C4b.Red Box3d.Unit
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DefaultSurfaces.simpleLighting
                }
                |> Sg.viewTrafo view
                |> Sg.projTrafo proj

        win.RenderTask <- app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.Run()

        ()


    let runOld() =

        let vk = new VulkanApplication(true)
        let gl = new OpenGlApplication(true)

        

        let rgl = gl.Runtime :> IRuntime
        let rvk = vk.Runtime :> IRuntime

        let testImage = PixImage<byte>(Col.Format.RGBA, V2i(1024, 1024))
        let ref = PixImage<byte>(Col.Format.RGBA, V2i(1024, 1024))

        let tgl = rgl.CreateTexture(V3i(1024, 1024, 1), TextureDimension.Texture2D, TextureFormat.Rgba8, 0, 1, 1)
        let tvk = rvk.CreateTexture(V3i(1024, 1024, 1), TextureDimension.Texture2D, TextureFormat.Rgba8, 0, 1, 1)
        let lgl = tgl.[Color, 0, 0]
        let lvk = tvk.[Color, 0, 0]

        let img = tcGradient 1024 1024
        rgl.Copy(img, lgl)
        rvk.Copy(img, lvk)
        ref.Set(img)

        rgl.Copy(lgl, testImage)
        let equal = img.Volume.InnerProduct(testImage.Volume, (=), true, (&&))
        printfn "GL: %A" equal
        
        rvk.Copy(lvk, testImage)
        let equal = img.Volume.InnerProduct(testImage.Volume, (=), true, (&&))
        printfn "VK: %A" equal

        
        let img = tcGradient 512 512
        rgl.Copy(img, lgl, V2i(512, 512), V2i(512, 512))
        rvk.Copy(img, lvk, V2i(512, 512), V2i(512, 512))
        ref.SubImage(V2i(512, 512), V2i(512,512)).Set img


        let img = PixImage.Create @"E:\Development\WorkDirectory\DataSVN\cliffs_color.jpg"
        let img = img.Transformed(ImageTrafo.Rot180) 
        lgl.[123.., 235..] <- img
        lvk.[123.., 235..] <- img
        ref.SubImage(V2i(123,235), img.Size).Set(img.ToPixImage<byte>(Col.Format.RGBA))


        rgl.Copy(lgl, testImage)
        testImage.SaveAsImage @"C:\Users\schorsch\Desktop\gl.png"

        rvk.Copy(lvk, testImage)
        testImage.SaveAsImage @"C:\Users\schorsch\Desktop\vulkan.png"

        ref.SaveAsImage @"C:\Users\schorsch\Desktop\ref.png"


        let cgl = new RenderControl(Width = 1024, Height = 1024)
        let cvk = new RenderControl(Width = 1024, Height = 1024)

        let form = new System.Windows.Forms.Form()
        form.ClientSize <- Drawing.Size(1024, 1024)

        let splitter = new SplitContainer(Dock = DockStyle.Fill)
        form.Controls.Add(splitter)
        splitter.Panel1.Controls.Add cgl
        splitter.Panel2.Controls.Add cvk
        splitter.SplitterMoved.Add (fun e ->
            cvk.Left <- -e.SplitX - splitter.SplitterWidth
        )

        splitter.Resize.Add (fun e ->
            cgl.Size <- splitter.Size
            cvk.Size <- splitter.Size
        )

        vk.Initialize(cvk)
        gl.Initialize(cgl)


        let sg =
            Sg.fullScreenQuad
                |> Sg.diffuseTexture (Mod.constant (tgl :> ITexture))
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
        cgl.RenderTask <- rgl.CompileRender(cgl.FramebufferSignature, sg)
        
        let sg =
            Sg.fullScreenQuad
                |> Sg.diffuseTexture (Mod.constant (tvk :> ITexture))
                |> Sg.shader {
                    do! DefaultSurfaces.diffuseTexture
                }
        cvk.RenderTask <- rvk.CompileRender(cvk.FramebufferSignature, sg)

        System.Windows.Forms.Application.Run(form)

        Environment.Exit 0



//
//
//
//        let view =
//            CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
//                |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
//                |> Mod.map CameraView.viewTrafo
//
//        let proj =
//            win.Sizes 
//                |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 1000.0 (float s.X / float s.Y))
//                |> Mod.map Frustum.projTrafo
//
//
//        let mutable frozen = false
//        let views = DefaultingModRef (view |> Mod.map Array.singleton)
//        let projs = DefaultingModRef (proj |> Mod.map Array.singleton)
//
//        let toggleFreeze() =
//            transact (fun () ->
//                if frozen then
//                    views.Reset()
//                    projs.Reset()
//                else
//                    views.Value <- Mod.force views
//                    projs.Value <- Mod.force projs
//                frozen <- not frozen
//            )
//
//        win.Keyboard.KeyDown(Keys.Space).Values.Add toggleFreeze
//
//
//
//        let tree = GeometryTree(Box3d(-10.0 * V3d.III, 10.0 * V3d.III))
//
//
//        let viewProj = Mod.map2 (Array.map2 (*)) views projs
//
//
//        let visible =
//            viewProj |> Mod.map (fun (vps : Trafo3d[]) (node : GeometryTree) ->
//                vps |> Array.exists (ViewProjection.intersects node.Bounds)
//            )
//
//        let descend =
//            viewProj |> Mod.map (fun (vps : Trafo3d[]) (node : GeometryTree) ->
//                let projectedLength (b : Box3d) (t : Trafo3d) =
//                    let ssb = b.ComputeCorners() |> Array.map (t.Forward.TransformPosProj) |> Box3d
//                    max ssb.Size.X ssb.Size.Y
//
//                let len = vps |> Array.map (projectedLength node.Bounds) |> Array.max
//                len > 1.0
//            )
//            
//
//
////        let active = CSet.empty
////
////        let mutable pending = ref HDeltaSet.empty
////
////        let flush () =
////            transact (fun () ->
////                let pending = !Interlocked.Exchange(&pending, ref HDeltaSet.empty)
////                for d in pending do
////                    match d with
////                        | Add(_,v) -> active.Add v |> ignore
////                        | Rem(_,v) -> active.Remove v |> ignore
////            )
////
////        let activate (g : Geometry) =
////            pending := HDeltaSet.add (Add g) !pending
////            //transact (fun () -> active.Add g |> ignore)
////            
////        let deactivate (g : Geometry) =
////            pending := HDeltaSet.add (Rem g) !pending
////            //transact (fun () -> active.Remove g |> ignore)
//
//        
//        let treeView = 
//            { 
//                root = Mod.constant (Some tree)
//                visible = visible
//                descend = descend
//                showInner = true
//            }
//
//        let loader = LodTreeLoader.create treeView
////
////        let thread = 
////            loader |> LodTreeLoader.start {
////                prepare     = id
////                delete      = ignore
////                activate    = activate
////                deactivate  = deactivate
////                flush       = flush
////            }
//
//        let runtime = win.Runtime |> unbox<Runtime>
//        let device = runtime.Device
//
//        let effect =
//            FShade.Effect.compose [
//                toEffect DefaultSurfaces.trafo
//                toEffect (DefaultSurfaces.constantColor C4f.Red)
//                toEffect DefaultSurfaces.simpleLighting
//            ]
//
//
//        let surface = Aardvark.Base.Surface.FShadeSimple effect
//
//        let state =
//            {
//                depthTest           = Mod.constant DepthTestMode.LessOrEqual
//                cullMode            = Mod.constant CullMode.None
//                blendMode           = Mod.constant BlendMode.None
//                fillMode            = Mod.constant FillMode.Fill
//                stencilMode         = Mod.constant StencilMode.Disabled
//                multisample         = Mod.constant true
//                writeBuffers        = None
//                globalUniforms      = 
//                    UniformProvider.ofList [
//                        "ViewTrafo", view :> IMod
//                        "ProjTrafo", proj :> IMod
//                        "LightLocation", view |> Mod.map (fun v -> v.Backward.C3.XYZ) :> IMod
//                        "CameraLocation", view |> Mod.map (fun v -> v.Backward.C3.XYZ) :> IMod
//                    ]
//
//                geometryMode        = IndexedGeometryMode.TriangleList
//                vertexInputTypes    = Map.ofList [ DefaultSemantic.Positions, typeof<V3f>; DefaultSemantic.Normals, typeof<V3f> ]
//                perGeometryUniforms = Map.ofList [ "ModelTrafo", typeof<Trafo3d> ]
//            }
//
//        let task = new RenderTask.CommandTask(device, unbox win.FramebufferSignature, RuntimeCommand.LodTree(surface, state, loader))
//
//
//        win.RenderTask <- task
//
//
//
//
//        win.Run()
