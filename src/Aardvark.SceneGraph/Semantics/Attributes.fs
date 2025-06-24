namespace Aardvark.SceneGraph.Semantics

open System

open Aardvark.Base
open Aardvark.Base.Ag
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive

[<AutoOpen>]
module AttributeExtensions =
    type Ag.Scope with
        member x.FaceVertexCount : aval<int> = x?FaceVertexCount
        member x.VertexAttributes : Map<Symbol, BufferView> = x?VertexAttributes
        member x.InstanceAttributes : Map<Symbol, BufferView> = x?InstanceAttributes
        member x.VertexIndexBuffer : Option<BufferView> = x?VertexIndexBuffer

    module Semantic =
        let faceVertexCount (s : Ag.Scope) : aval<int> = s?FaceVertexCount
        let vertexAttributes (s : Ag.Scope) : Map<Symbol, BufferView> = s?VertexAttributes
        let instanceAttributes (s : Ag.Scope) : Map<Symbol, BufferView> = s?InstanceAttributes
        let vertexIndexBuffer (s : Ag.Scope) : Option<BufferView> = s?VertexIndexBuffer

module AttributeSemantics =

    let emptyIndex : aval<Array> = AVal.constant ([||] :> Array)

    [<Rule>]
    type AttributeSem() =
        static let zero = AVal.constant 0
        let (~%) (m : Map<Symbol, BufferView>) = m

        static let bufferViewCount (view : BufferView) =
            if view.IsSingleValue then
                AVal.constant 0
            else
                let elementSize = System.Runtime.InteropServices.Marshal.SizeOf view.ElementType
                view.Buffer |> AVal.map (fun b ->
                    match b with
                        | :? INativeBuffer as b -> int ((b.SizeInBytes - uint64 view.Offset) / uint64 elementSize)
                        | _ -> failwithf "[Sg] could not determine buffer-size: %A" b
                )


        member x.FaceVertexCount (root : Root<ISg>, scope : Ag.Scope) =
            root.Child?FaceVertexCount <- zero

        member x.FaceVertexCount (app : Sg.VertexIndexApplicator, scope : Ag.Scope) =
            app.Child?FaceVertexCount <- app.Value |> bufferViewCount

        member x.FaceVertexCount (app : Sg.VertexAttributeApplicator, scope : Ag.Scope) =
            let res = scope.VertexIndexBuffer

            match res with
                | Some b ->
                    app.Child?FaceVertexCount <- scope.FaceVertexCount
                | _ -> 
                    match Map.tryFind DefaultSemantic.Positions app.Values with
                        | Some pos ->
                            app.Child?FaceVertexCount <- 
                                pos.Buffer 
                                    |> AVal.bind (fun buffer ->
                                        match buffer with
                                            | :? ArrayBuffer as a ->
                                                AVal.constant (a.Data.Length - pos.Offset)
            
                                            | _ -> scope.FaceVertexCount
                                       )
                        | _ -> app.Child?FaceVertexCount <- scope.FaceVertexCount

        member x.InstanceAttributes(root : Root<ISg>, scope : Ag.Scope) = 
            root.Child?InstanceAttributes <- %Map.empty

        member x.VertexIndexBuffer(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?VertexIndexBuffer <- Option<BufferView>.None

        member x.VertexIndexBuffer(v : Sg.VertexIndexApplicator, scope : Ag.Scope) =
            v.Child?VertexIndexBuffer <- Some v.Value

        member x.VertexAttributes(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?VertexAttributes <- %Map.empty

        member x.VertexAttributes(v : Sg.VertexAttributeApplicator, scope : Ag.Scope) =
            v.Child?VertexAttributes <- Map.union scope.VertexAttributes v.Values

        member x.InstanceAttributes(v : Sg.InstanceAttributeApplicator, scope : Ag.Scope) =
            v.Child?InstanceAttributes <- Map.union scope.InstanceAttributes v.Values
