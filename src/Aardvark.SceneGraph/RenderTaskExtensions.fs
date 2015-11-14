namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

[<AutoOpen>]
module ExtendedDefaultSemantics =
    
    module DefaultSemantic =
        let ColorTexture = Symbol.Create "ColorTexture"
        let DepthTexture = Symbol.Create "DepthTexture"


module RenderTask =
    open FShade

    module private Shaders =
        open FShade

        type Vertex = { [<Position>] pos : V4d; [<TexCoord>] tc : V2d }
        type Fragment = { [<Color>] color : V4d; [<Depth>] depth : float }

        let colorSampler =
            sampler2d {
                texture uniform?ColorTexture
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }

        let depthSampler =
            sampler2d {
                texture uniform?DepthTexture
                filter Filter.MinMagPoint
                addressU WrapMode.Clamp
                addressV WrapMode.Clamp
            }


        let fs (v : Vertex) =
            fragment {
                let c = colorSampler.Sample(v.tc)
                let d = depthSampler.Sample(v.tc).X

                return { color = c; depth = d }
            }

    module private RenderObjects =

        
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
                RenderPass = 0UL
                DrawCallInfo = Mod.constant (DrawCallInfo(InstanceCount = 1, FaceVertexCount = 6))
                Mode = Mod.constant IndexedGeometryMode.TriangleList
                Surface = Shaders.fs |> toEffect |> toFShadeSurface |> Mod.constant
                DepthTest = Mod.constant DepthTestMode.LessOrEqual
                CullMode = Mod.constant CullMode.None
                BlendMode = Mod.constant BlendMode.Blend
                FillMode = Mod.constant FillMode.Fill
                StencilMode = Mod.constant StencilMode.Disabled
                Indices = Mod.constant ([|0;1;2; 0;2;3|] :> Array)
                InstanceAttributes = emptyAttributes
                VertexAttributes = attributeProvider
                Uniforms = emptyUniforms
            }

        let create (color : IMod<ITexture>) (depth : IMod<ITexture>) =
            { RenderObject.Clone(baseObject) with
                Uniforms = uniformProvider color depth
            }

    let cache (t : IRenderTask) : IRenderTask =
        
        match t.Runtime with
            | Some runtime ->
                
                let size = Mod.init V2i.II
                let format = Mod.init TextureFormat.Rgba8

                let color, depth =
                    t |> RenderTask.renderToColorAndDepth size format

                let compose =
                    ASet.ofList [
                        RenderObjects.create color depth :> IRenderObject
                    ]

                let composeTask = runtime.CompileRender(t.FramebufferSignature, compose)


                RenderTask.ofList [
                    RenderTask.custom (fun (self, target) ->
                        if target.Size <> size.Value then
                            transact (fun () -> Mod.change size target.Size)

                        RenderingResult(target, FrameStatistics.Zero)
                    )
                    composeTask
                ]

            | None ->
                Log.warn "[RenderTask] unable to cache RenderTask since it has no Runtime"
                t

    let postProcess (effect : list<FShadeEffect>) (t : IRenderTask) =
        match t.Runtime with
            | Some runtime ->
                
                let size = Mod.init V2i.II
                let format = Mod.init TextureFormat.Rgba8

                let color, depth =
                    t |> RenderTask.renderToColorAndDepth size format

                let compose =
                    ASet.ofList [
                        { RenderObjects.create color depth with
                            Surface = effect |> SequentialComposition.compose |> toFShadeSurface |> Mod.constant
                        }:> IRenderObject
                    ]

                let composeTask = runtime.CompileRender(t.FramebufferSignature, compose)


                RenderTask.ofList [
                    RenderTask.custom (fun (self, target) ->
                        if target.Size <> size.Value then
                            transact (fun () -> Mod.change size target.Size)

                        RenderingResult(target, FrameStatistics.Zero)
                    )
                    composeTask
                ]

            | None ->
                Log.warn "[RenderTask] unable to cache RenderTask since it has no Runtime"
                t
