namespace Aardvark.SceneGraph

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.SceneGraph

type RenderGeometryConfig =
    {
        mode                : IndexedGeometryMode
        vertexInputTypes    : Map<Symbol, Type>
        perGeometryUniforms : Map<string, Type>
    }

type RenderCommand =
    internal
        | REmpty
        | RUnorderedScenes of scenes : aset<ISg>
        | RClear           of values : aval<ClearValues>
        | RGeometries      of config : RenderGeometryConfig * geometries : aset<IndexedGeometry>
        | ROrdered         of commands : alist<RenderCommand>
        | RIfThenElse      of condition : aval<bool> * ifTrue : RenderCommand * ifFalse : RenderCommand
        | RLodTree         of config : RenderGeometryConfig * geometries : LodTreeLoader<Geometry>

    static member Empty = REmpty

    static member Clear(values : aval<ClearValues>) = RClear values
    static member Clear(values : ClearValues)       = RenderCommand.Clear(~~values)

    static member Unordered(scenes : seq<ISg>) = if Seq.isEmpty scenes then REmpty else RUnorderedScenes(ASet.ofSeq scenes)
    static member Unordered(scenes : list<ISg>) = match scenes with [] -> REmpty | _  -> RUnorderedScenes(ASet.ofList scenes)
    static member Unordered(scenes : aset<ISg>) = if scenes.IsConstant && scenes.Content.GetValue().IsEmpty then REmpty else RUnorderedScenes(scenes)
    static member Render(scene : ISg) = RUnorderedScenes(ASet.single scene)

    static member Ordered(commands : seq<RenderCommand>) = if Seq.isEmpty commands then REmpty else ROrdered(AList.ofSeq commands)
    static member Ordered(commands : list<RenderCommand>) = match commands with [] -> REmpty | _ -> ROrdered(AList.ofList commands)
    static member Ordered(commands : alist<RenderCommand>) = if commands.IsConstant && commands.Content.GetValue().Count = 0 then REmpty else ROrdered commands

    static member Ordered(scenes : seq<ISg>) = RenderCommand.Ordered(scenes |> Seq.map RenderCommand.Render)
    static member Ordered(scenes : list<ISg>) = RenderCommand.Ordered(scenes |> List.map RenderCommand.Render)
    static member Ordered(scenes : alist<ISg>)  = RenderCommand.Ordered(scenes |> AList.map RenderCommand.Render)

    static member IfThenElse(condition : aval<bool>, ifTrue : RenderCommand, ifFalse : RenderCommand) = RIfThenElse(condition, ifTrue, ifFalse)
    static member IfThenElse(condition : aval<bool>, ifTrue : ISg, ifFalse : ISg) = RIfThenElse(condition, RenderCommand.Render ifTrue, RenderCommand.Render ifFalse)
    static member When(condition : aval<bool>, ifTrue : RenderCommand) = RIfThenElse(condition, ifTrue, REmpty)
    static member When(condition : aval<bool>, ifTrue : ISg) = RIfThenElse(condition, RenderCommand.Render ifTrue, REmpty)
    static member WhenNot(condition : aval<bool>, ifFalse : RenderCommand) = RIfThenElse(condition, REmpty, ifFalse)
    static member WhenNot(condition : aval<bool>, ifFalse : ISg) = RIfThenElse(condition, REmpty, RenderCommand.Render ifFalse)

    static member LodTree(config : RenderGeometryConfig, geometries : LodTreeLoader<Geometry>) = RLodTree(config,geometries)

    static member Geometries(config : RenderGeometryConfig,  geometries : aset<IndexedGeometry>) = RGeometries(config, geometries)
    static member Geometries(config : RenderGeometryConfig,  geometries : seq<IndexedGeometry>) = RGeometries(config, ASet.ofSeq geometries)
    static member Geometries(config : RenderGeometryConfig,  geometries : list<IndexedGeometry>) = RGeometries(config, ASet.ofList geometries)


[<AutoOpen>]
module RenderCommandFSharpExtensions =

    // These extensions use SRTPs so MUST NOT be exposed to C#
    type RenderCommand with

        static member inline Clear(color : aval< ^Color>) =
            let values = color |> AVal.map (fun c -> clear { color c })
            RenderCommand.Clear(values)

        static member inline Clear(color : aval< ^Color>, depth : aval< ^Depth>) =
            let values = (color, depth) ||> AVal.map2 (fun c d -> clear { color c; depth d })
            RenderCommand.Clear(values)

        static member inline Clear(color : aval< ^Color>, depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
            let values = (color, depth, stencil) |||> AVal.map3 (fun c d s -> clear { color c; depth d; stencil s })
            RenderCommand.Clear(values)

        static member inline ClearDepth(depth : aval< ^Depth>) =
            let values = depth |> AVal.map (fun d -> clear { depth d })
            RenderCommand.Clear(values)

        static member inline ClearStencil(stencil : aval< ^Stencil>) =
            let values = stencil |> AVal.map (fun s -> clear { stencil s })
            RenderCommand.Clear(values)

        static member inline ClearDepthStencil(depth : aval< ^Depth>, stencil : aval< ^Stencil>) =
            let values = (depth, stencil) ||> AVal.map2 (fun d s -> clear { depth d; stencil s })
            RenderCommand.Clear(values)

        static member inline Clear(color : ^Color)                                     = RenderCommand.Clear(~~color)
        static member inline Clear(color : ^Color, depth : ^Depth)                     = RenderCommand.Clear(~~color, ~~depth)
        static member inline Clear(color : ^Color, depth : ^Depth, stencil : ^Stencil) = RenderCommand.Clear(~~color, ~~depth, ~~stencil)
        static member inline ClearDepth(depth : ^Depth)                                = RenderCommand.ClearDepth(~~depth)
        static member inline ClearStencil(stencil : ^Stencil)                          = RenderCommand.ClearStencil(~~stencil)
        static member inline ClearDepthStencil(depth : ^Depth, stencil : ^Stencil)     = RenderCommand.ClearDepthStencil(~~depth, ~~stencil)

        static member inline Clear(colors : Map<Symbol, ^Color>) =
            let values = colors |> (fun c -> clear { colors c })
            RenderCommand.Clear(values)

        static member inline Clear(colors : seq<Symbol * ^Color>) =
            let values = colors |> (fun c -> clear { colors c })
            RenderCommand.Clear(values)

        static member inline Clear(colors : Map<Symbol, ^Color>, depth : ^Depth) =
            let values = (colors, depth) ||> (fun c d -> clear { colors c; depth d })
            RenderCommand.Clear(values)

        static member inline Clear(colors : seq<Symbol * ^Color>, depth : ^Depth) =
            let values = (colors, depth) ||> (fun c d -> clear { colors c; depth d })
            RenderCommand.Clear(values)

        static member inline Clear(colors : Map<Symbol, ^Color>, depth : ^Depth, stencil : ^Stencil) =
            let values = (colors, depth, stencil) |||> (fun c d s -> clear { colors c; depth d; stencil s })
            RenderCommand.Clear(values)

        static member inline Clear(colors : seq<Symbol * ^Color>, depth : ^Depth, stencil : ^Stencil) =
            let values = (colors, depth, stencil) |||> (fun c d s -> clear { colors c; depth d; stencil s })
            RenderCommand.Clear(values)

[<AutoOpen>]
module ``Sg RuntimeCommand Extensions`` =

    module Sg =
        type RuntimeCommandNode(command : RenderCommand) =
            interface ISg
            member x.Command = command


        let execute (cmd : RenderCommand) =
            RuntimeCommandNode(cmd) :> ISg


namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

[<AutoOpen>]
module RuntimeCommandSemantics =

    module private RenderCommand =
        let rec toRuntimeCommand (scope : Ag.Scope) (cmd : RenderCommand) =
            match cmd with
            | RenderCommand.REmpty ->
                RuntimeCommand.Empty

            | RenderCommand.RUnorderedScenes scenes ->
                let objects = scenes |> ASet.collect _.RenderObjects(scope)
                RuntimeCommand.Render(objects)

            | RenderCommand.RClear values ->
                RuntimeCommand.Clear(values)

            | RenderCommand.RGeometries(config, geometries) ->
                let effect =
                    match scope.Surface with
                    | Surface.Effect e -> e
                    | s -> failwithf "[Sg] cannot create GeometryCommand with shader: %A" s

                let state =
                    { PipelineState.ofScope scope with
                        Mode                = config.mode
                        VertexInputTypes    = config.vertexInputTypes
                        PerGeometryUniforms = config.perGeometryUniforms }

                RuntimeCommand.Geometries(effect, state, geometries)

            | RenderCommand.ROrdered(list) ->
                let commands = list |> AList.map (toRuntimeCommand scope)
                RuntimeCommand.Ordered(commands)

            | RenderCommand.RIfThenElse(c, t, f) ->
                RuntimeCommand.IfThenElse(c, toRuntimeCommand scope t, toRuntimeCommand scope f)

            | RenderCommand.RLodTree(config, g) ->
                let state =
                    { PipelineState.ofScope scope with
                        Mode                = config.mode
                        VertexInputTypes    = config.vertexInputTypes
                        PerGeometryUniforms = config.perGeometryUniforms }

                RuntimeCommand.LodTree(scope.Surface, state, g)

        let rec getBoundingBox (scope : Ag.Scope) (cmd : RenderCommand) =
            match cmd with
            | RenderCommand.REmpty ->
                Box3d.invalid

            | RenderCommand.RClear _ ->
                Box3d.invalid

            | RenderCommand.RIfThenElse (c, t, f) ->
                let t = getBoundingBox scope t
                let f = getBoundingBox scope f
                c |> AVal.bind (fun c -> if c then t else f)

            | RenderCommand.ROrdered commands ->
                commands |> AList.mapA (getBoundingBox scope) |> Box3d.ofAList

            | RenderCommand.RUnorderedScenes objects ->
                objects |> ASet.mapA _.GlobalBoundingBox(scope) |> Box3d.ofASet

            | RenderCommand.RLodTree _
            | RenderCommand.RGeometries _ ->
                Log.warn "[Sg] Bounding box computation for %A not implemented" cmd
                Box3d.invalid

    [<Rule>]
    type RuntimeCommandSem() =
        member x.RenderObjects(n : Sg.RuntimeCommandNode, scope : Ag.Scope) : aset<IRenderObject> =
            let runtimeCommand = n.Command |> RenderCommand.toRuntimeCommand scope
            let obj = CommandRenderObject(scope.RenderPass, scope, runtimeCommand)
            ASet.single (obj :> IRenderObject)

        member _.GlobalBoundingBox(n : Sg.RuntimeCommandNode, scope : Ag.Scope) : aval<Box3d> =
            n.Command |> RenderCommand.getBoundingBox scope

        member this.LocalBoundingBox(n : Sg.RuntimeCommandNode, scope: Ag.Scope) : aval<Box3d> =
            this.GlobalBoundingBox(n, scope)