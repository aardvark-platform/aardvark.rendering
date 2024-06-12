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
        | RUnorderedScenes of aset<ISg>
        | RClear of values : aval<ClearValues>
        | RGeometries of config : RenderGeometryConfig * geometries : aset<IndexedGeometry>
        | ROrdered of alist<RenderCommand>
        | ROrderedConstant of list<RenderCommand>
        | RIfThenElse of condition : aval<bool> * ifTrue : RenderCommand * ifFalse : RenderCommand
        | RLodTree of config : RenderGeometryConfig * geometries : LodTreeLoader<Geometry>

    static member Empty = REmpty

    static member Clear(values : aval<ClearValues>) = RClear values
    static member Clear(values : ClearValues)       = RenderCommand.Clear(~~values)

    static member Unordered(l : seq<ISg>) = RUnorderedScenes(ASet.ofSeq l)
    static member Unordered(l : list<ISg>) = RUnorderedScenes(ASet.ofList l)
    static member Unordered(l : aset<ISg>) = RUnorderedScenes(l)
    static member Render (s : ISg) = RUnorderedScenes(ASet.single s)

    static member Ordered(l : seq<ISg>) = ROrderedConstant(l |> Seq.map RenderCommand.Render |> Seq.toList)
    static member Ordered(l : list<ISg>) = ROrderedConstant(l |> List.map RenderCommand.Render)
    static member Ordered(l : alist<ISg>) = RenderCommand.Ordered(l |> AList.map RenderCommand.Render)

    static member IfThenElse(condition : aval<bool>, ifTrue : RenderCommand, ifFalse : RenderCommand) = RIfThenElse(condition, ifTrue, ifFalse)
    static member When(condition : aval<bool>, ifTrue : RenderCommand) = RIfThenElse(condition, ifTrue, REmpty)
    static member WhenNot(condition : aval<bool>, ifFalse : RenderCommand) = RIfThenElse(condition, REmpty, ifFalse)

    static member LodTree(config : RenderGeometryConfig, geometries : LodTreeLoader<Geometry>) = RLodTree(config,geometries)

    static member Geometries(config : RenderGeometryConfig,  geometries : aset<IndexedGeometry>) = RGeometries(config, geometries)
    static member Geometries(config : RenderGeometryConfig,  geometries : seq<IndexedGeometry>) = RGeometries(config, ASet.ofSeq geometries)
    static member Geometries(config : RenderGeometryConfig,  geometries : list<IndexedGeometry>) = RGeometries(config, ASet.ofList geometries)


    static member Ordered(cmds : list<RenderCommand>) =
        match cmds with
            | [] -> REmpty
            | _ -> ROrderedConstant cmds

    static member Ordered(cmds : seq<RenderCommand>) =
        RenderCommand.Ordered(Seq.toList cmds)

    static member Ordered(cmds : alist<RenderCommand>) =
        if cmds.IsConstant then
            let list = cmds.Content |> AVal.force |> IndexList.toList
            RenderCommand.Ordered list
        else
            ROrdered cmds

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

    module private RuntimeCommand =
        let rec ofRenderCommand (scope : Ag.Scope) (parent : ISg) (cmd : RenderCommand) =
            match cmd with
                | RenderCommand.REmpty ->
                    RuntimeCommand.Empty

                | RenderCommand.RUnorderedScenes scenes ->
                    let objects = scenes |> ASet.collect (fun s -> s.RenderObjects(scope))
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
                    let commands = list |> AList.map (ofRenderCommand scope parent)
                    RuntimeCommand.Ordered(commands)

                | RenderCommand.ROrderedConstant(list) ->
                    let commands = list |> List.map (ofRenderCommand scope parent)
                    RuntimeCommand.Ordered(AList.ofList commands)

                | RenderCommand.RIfThenElse(c,t,f) ->
                    RuntimeCommand.IfThenElse(c, ofRenderCommand scope parent t, ofRenderCommand scope parent f)

                | RenderCommand.RLodTree(config,g) ->
                    let state =
                        { PipelineState.ofScope scope with
                            Mode                = config.mode
                            VertexInputTypes    = config.vertexInputTypes
                            PerGeometryUniforms = config.perGeometryUniforms }

                    RuntimeCommand.LodTree(scope.Surface, state, g)

    [<Rule>]
    type RuntimeCommandSem() =
        member x.RenderObjects(n : Sg.RuntimeCommandNode, scope : Ag.Scope) : aset<IRenderObject> =
            let cmd = n.Command
            let runtimeCommand = RuntimeCommand.ofRenderCommand scope n cmd

            let pass = scope.RenderPass

            let obj = CommandRenderObject(pass, scope, runtimeCommand)
            ASet.single (obj :> IRenderObject)