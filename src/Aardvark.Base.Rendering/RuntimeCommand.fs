namespace Aardvark.Base

open FSharp.Data.Adaptive
open Aardvark.Base.Rendering

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
