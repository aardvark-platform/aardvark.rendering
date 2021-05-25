namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

[<RequireQualifiedAccess>]
type RuntimeCommand =
    | EmptyCmd
    | RenderCmd of objects : aset<IRenderObject>
    | OrderedCmd of commands : alist<RuntimeCommand>
    | IfThenElseCmd of condition : aval<bool> * ifTrue : RuntimeCommand * ifFalse : RuntimeCommand
    | ClearCmd of colors : Map<Symbol, aval<C4f>> * depth : Option<aval<float>> * stencil : Option<aval<uint32>>
    | DispatchCmd of shader : IComputeShader * groups : aval<V3i> * arguments : Map<string, obj>
    | GeometriesCmd of surface : Surface * pipeline : PipelineState * geometries : aset<Geometry>
    | LodTreeCmd of surface : Surface * pipeline : PipelineState * geometries : LodTreeLoader<Geometry>
    | GeometriesSimpleCmd of effect : FShade.Effect * pipeline : PipelineState * geometries : aset<IndexedGeometry>
    | TraceRaysCmd of pipeline : Raytracing.PipelineState * scene : aval<Symbol> * raygen : aval<Symbol> * size : aval<V3i>

    static member Empty = RuntimeCommand.EmptyCmd

    static member Render(objects : aset<IRenderObject>) =
        RuntimeCommand.RenderCmd(objects)

    static member Dispatch(shader : IComputeShader, groups : aval<V3i>, arguments : Map<string, obj>) =
        RuntimeCommand.DispatchCmd(shader, groups, arguments)

    static member Clear(colors : Map<Symbol, aval<C4f>>, depth : Option<aval<float>>, stencil : Option<aval<uint32>>) =
        RuntimeCommand.ClearCmd(colors, depth, stencil)

    static member Ordered(commands : alist<RuntimeCommand>) =
        RuntimeCommand.OrderedCmd(commands)

    static member IfThenElse(condition : aval<bool>, ifTrue : RuntimeCommand, ifFalse : RuntimeCommand) =
        RuntimeCommand.IfThenElseCmd(condition, ifTrue, ifFalse)

    static member Geometries(surface : Surface, pipeline : PipelineState, geometries : aset<Geometry>) =
        RuntimeCommand.GeometriesCmd(surface, pipeline, geometries)

    static member Geometries(surface : FShade.Effect, pipeline : PipelineState, geometries : aset<IndexedGeometry>) =
        RuntimeCommand.GeometriesSimpleCmd(surface, pipeline, geometries)

    static member Geometries(effects : FShade.Effect[], activeEffect : aval<int>, pipeline : PipelineState, geometries : aset<Geometry>) =
        let surface =
            Surface.FShade (fun cfg ->
                let modules = effects |> Array.map (FShade.Effect.toModule cfg)
                let signature = FShade.EffectInputLayout.ofModules modules
                let modules = modules |> Array.map (FShade.EffectInputLayout.apply signature)

                signature, activeEffect |> AVal.map (Array.get modules)
            )
        RuntimeCommand.GeometriesCmd(surface, pipeline, geometries)

    static member LodTree(surface : Surface, pipeline : PipelineState, geometries : LodTreeLoader<Geometry>) =
        RuntimeCommand.LodTreeCmd(surface, pipeline, geometries)

    static member TraceRays(pipeline : Raytracing.PipelineState, scene : aval<Symbol>, raygen : aval<Symbol>, size : aval<V3i>) =
        RuntimeCommand.TraceRaysCmd(pipeline, scene, raygen, size)

    static member TraceRays(pipeline : Raytracing.PipelineState, scene : aval<Symbol>, raygen : aval<Symbol>, size : aval<V2i>) =
        RuntimeCommand.TraceRaysCmd(pipeline, scene, raygen, size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1)))

    static member TraceRays(pipeline : Raytracing.PipelineState, scene : aval<Symbol>, size : aval<V3i>) =
        let rg = pipeline.Effect.RayGenerationShaders |> Map.toList |> List.head |> fst
        RuntimeCommand.TraceRaysCmd(pipeline, scene, AVal.constant rg, size)

    static member TraceRays(pipeline : Raytracing.PipelineState, scene : aval<Symbol>, size : aval<V2i>) =
        RuntimeCommand.TraceRays(pipeline, scene, size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1)))

    static member TraceRays(pipeline : Raytracing.PipelineState, size : aval<V3i>) =
        let sc = pipeline.Scenes |> AMap.keys |> ASet.tryMin |> AVal.mapNonAdaptive Option.get
        RuntimeCommand.TraceRays(pipeline, sc, size)

    static member TraceRays(pipeline : Raytracing.PipelineState, size : aval<V2i>) =
        RuntimeCommand.TraceRays(pipeline, size |> AVal.mapNonAdaptive (fun s -> V3i(s, 1)))

type CommandRenderObject(pass : RenderPass, scope : Ag.Scope, command : RuntimeCommand) =
    let id = newId()

    member x.Id = id
    member x.RenderPass = pass
    member x.AttributeScope = scope
    member x.Command = command

    interface IRenderObject with
        member x.Id = id
        member x.RenderPass = pass
        member x.AttributeScope = scope

    override x.GetHashCode() = id.GetHashCode()
    override x.Equals o =
        match o with
            | :? CommandRenderObject as o -> id = o.Id
            | _ -> false
