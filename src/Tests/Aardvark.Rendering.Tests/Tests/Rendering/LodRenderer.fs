namespace Aardvark.Rendering.Tests.Rendering

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Aardvark.Application
open System
open System.Threading
open FSharp.Data.Adaptive

module LodRenderer =

    module Semantic =
        let V3f  = Sym.ofString "V3f"
        let V4f  = Sym.ofString "V4f"
        let M22f = Sym.ofString "M22f"
        let M33f = Sym.ofString "M33f"
        let M34f = Sym.ofString "M34f"
        let M44f = Sym.ofString "M44f"

    module Shader =
        open FShade

        type Vertex =
            {
                [<VertexId>]         id   : int
                [<Semantic("V3f")>]  v3f  : V3f
                [<Semantic("V4f")>]  v4f  : V4f
                [<Semantic("M22f")>] m22f : M22f
                [<Semantic("M33f")>] m33f : M33f
                [<Semantic("M34f")>] m34f : M34f
                [<Semantic("M44f")>] m44f : M44f
            }

        let colorV3f (v: Vertex) =
            fragment {
                return v.v3f
            }

        let colorV4f (v: Vertex) =
            fragment {
                return v.v4f
            }

        let colorM22f (v: Vertex) =
            fragment {
                return v.m22f.Row(v.id % 2)
            }

        let colorM33f (v: Vertex) =
            fragment {
                return v.m33f.Row(v.id % 3)
            }

        let colorM34f (v: Vertex) =
            fragment {
                return v.m34f.Row(v.id % 3)
            }

        let colorM44f (v: Vertex) =
            fragment {
                return v.m44f.Row(v.id % 4)
            }

    module Cases =

        let private attribute<'T, 'R when 'R : equality> (attributeName: Symbol) (attributeValue: 'T)
                                                         (attributeEffect: FShade.Effect) (result: 'R[])
                                                         (perGeometry: bool) (runtime: IRuntime) =
            let format =
                match result.Length with
                | 3 | 4 -> TextureFormat.Rgba32f
                | 2     -> TextureFormat.Rg32f
                | 1     -> TextureFormat.R32f
                | n -> failwith $"Unexpected channel count: {n}"

            use signature = runtime.CreateFramebufferSignature [ DefaultSemantic.Colors, format ]

            let quad =
                let geometry =
                    let index = [| 0; 1; 2; 0; 2; 3|]
                    let positions = [| V3f(-1, -1, 0); V3f(1, -1, 0); V3f(1, 1, 0); V3f(-1, 1, 0) |]
                    let attributeValues = Array.replicate positions.Length attributeValue

                    IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [
                        DefaultSemantic.Positions, positions :> Array
                        if not perGeometry then
                            attributeName, attributeValues :> Array
                    ], SymDict.empty)

                let uniforms =
                    MapExt.ofList [
                        if perGeometry then
                            string attributeName, [| attributeValue |] :> Array
                    ]

                let node =
                    { new ILodTreeNode with
                        member this.Level                      = 0
                        member this.Name                       = "Quad"
                        member this.Root                       = this
                        member this.Parent                     = None
                        member this.Children                   = Seq.empty
                        member this.Id                         = this
                        member this.DataSource                 = Sym.empty
                        member this.DataSize                   = 1
                        member this.TotalDataSize              = 1
                        member this.GetData(_, _)              = geometry, uniforms
                        member this.ShouldSplit(_, _, _, _)    = false
                        member this.ShouldCollapse(_, _, _, _) = false
                        member this.SplitQuality(_, _, _)      = 1.0
                        member this.CollapseQuality(_, _, _)   = 1.0
                        member this.WorldBoundingBox           = Box3d.Unit
                        member this.WorldCellBoundingBox       = Box3d.Unit
                        member this.Cell                       = Cell.Unit
                        member this.DataTrafo                  = Trafo3d.Identity
                        member this.Acquire()                  = ()
                        member this.Release()                  = ()
                    }

                { root = node; uniforms = MapExt.empty }

            use renderer =
                let vertexInputTypes =
                    Map.ofList [
                        DefaultSemantic.Positions, typeof<V3f>
                        if not perGeometry then
                            attributeName, typeof<'T>
                    ]

                let perGeometryAttributes =
                    Map.ofList [
                        if perGeometry then
                            string attributeName, typeof<'T>
                    ]

                let pipelineState =
                    {
                        Mode                = IndexedGeometryMode.TriangleList
                        VertexInputTypes    = vertexInputTypes

                        DepthState          = DepthState.Default
                        BlendState          = BlendState.Default
                        StencilState        = StencilState.Default
                        RasterizerState     = RasterizerState.Default
                        ViewportState       = ViewportState.Default

                        GlobalUniforms      = UniformProvider.Empty
                        PerGeometryUniforms = perGeometryAttributes
                    }

                let stats =
                    {
                        quality         = 1.0
                        maxQuality      = 1.0
                        totalPrimitives = 0L
                        totalNodes      = 0
                        allocatedMemory = Mem.Zero
                        usedMemory      = Mem.Zero
                        renderTime      = MicroTime.Zero
                    }

                let config =
                    {
                        fbo             = signature
                        time            = AVal.constant DateTime.Now
                        surface         = Surface.Effect attributeEffect
                        state           = pipelineState
                        pass            = RenderPass.main
                        model           = AVal.constant Trafo3d.Identity
                        view            = AVal.constant Trafo3d.Identity
                        proj            = AVal.constant Trafo3d.Identity
                        budget          = AVal.constant 1L
                        splitfactor     = AVal.constant 1.0
                        renderBounds    = AVal.constant false
                        maxSplits       = AVal.constant 1
                        stats           = AVal.init stats
                        pickTrees       = None
                        alphaToCoverage = false
                    }

                runtime.CreateLodRenderer(config, ASet.single quad) :?> GL.LodRenderer

            use colorBuffer = runtime.CreateTexture2D(V2i(256), format)
            use framebuffer = runtime.CreateFramebuffer(signature, [ DefaultSemantic.Colors, colorBuffer.GetOutputView() ])

            use task = runtime.CompileRender(signature, ASet.single (renderer :> IRenderObject))
            task.Run framebuffer

            // Check if the geometry has actually been added, if not: repeat
            while renderer.UsedMemory = Mem.zero do
                Thread.Sleep 100
                task.Run framebuffer

            let output = colorBuffer.Download().AsPixImage<'R>()
            output |> PixImage.isColor result

        let v3f (perGeometry: bool) (runtime: IRuntime) =
            let value = V3f(1.0f, 3.0f, 5.0f)
            let effect = FShade.Effect.ofFunction Shader.colorV3f
            attribute Semantic.V3f value effect (value.ToArray()) perGeometry runtime

        let v4f (perGeometry: bool) (runtime: IRuntime) =
            let value = V4f(1.0f, 3.0f, 5.0f, 0.5f)
            let effect = FShade.Effect.ofFunction Shader.colorV4f
            attribute Semantic.V4f value effect (value.ToArray()) perGeometry runtime

        let m22f (perGeometry: bool) (runtime: IRuntime) =
            let value = V2f(1.0f, 3.0f)
            let mat = M22f.FromRows(value, value)
            let effect = FShade.Effect.ofFunction Shader.colorM22f
            attribute Semantic.M22f mat effect (value.ToArray()) perGeometry runtime

        let m33f (perGeometry: bool) (runtime: IRuntime) =
            let value = V3f(1.0f, 3.0f, 5.0f)
            let mat = M33f.FromRows(value, value, value)
            let effect = FShade.Effect.ofFunction Shader.colorM33f
            attribute Semantic.M33f mat effect (value.ToArray()) perGeometry runtime

        let m34f (perGeometry: bool) (runtime: IRuntime) =
            let value = V4f(1.0f, 3.0f, 5.0f, 0.5f)
            let mat = M34f.FromRows(value, value, value)
            let effect = FShade.Effect.ofFunction Shader.colorM34f
            attribute Semantic.M34f mat effect (value.ToArray()) perGeometry runtime

        let m44f (perGeometry: bool) (runtime: IRuntime) =
            let value = V4f(1.0f, 3.0f, 5.0f, 0.5f)
            let mat = M44f.FromRows(value, value, value, value)
            let effect = FShade.Effect.ofFunction Shader.colorM44f
            attribute Semantic.M44f mat effect (value.ToArray()) perGeometry runtime

    let tests (backend: Backend) =
        [
            if (backend = Backend.GL) then
                yield! [
                  "V3f attribute",  Cases.v3f
                  "V4f attribute",  Cases.v4f
                  "M22f attribute", Cases.m22f
                  "M33f attribute", Cases.m33f
                  "M34f attribute", Cases.m34f
                  "M44f attribute", Cases.m44f
                ]
                |> List.collect (fun (n, f) -> [
                    $"{n} (per vertex)",   f false
                    $"{n} (per geometry)", f true
                ])
        ]
        |> prepareCases backend "LodRenderer"