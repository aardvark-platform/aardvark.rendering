namespace Aardvark.SceneGraph.Semantics

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

    [<Rule>]
    type AttributeSem() =
        static let zero = AVal.constant 0

        member x.FaceVertexCount (root : Root<ISg>, scope : Ag.Scope) =
            root.Child?FaceVertexCount <- zero

        member x.FaceVertexCount (app : Sg.VertexIndexApplicator, scope : Ag.Scope) =
            app.Child?FaceVertexCount <- BufferView.getCount app.Value

        member x.FaceVertexCount (app : Sg.VertexAttributeApplicator, scope : Ag.Scope) =
            if scope.VertexIndexBuffer.IsNone then
                match Map.tryFind DefaultSemantic.Positions app.Values with
                | Some positions -> app.Child?FaceVertexCount <- BufferView.getCount positions
                | _ -> ()

        member x.InstanceAttributes(root : Root<ISg>, scope : Ag.Scope) = 
            root.Child?InstanceAttributes <- Map.empty<Symbol, BufferView>

        member x.VertexIndexBuffer(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?VertexIndexBuffer <- Option<BufferView>.None

        member x.VertexIndexBuffer(v : Sg.VertexIndexApplicator, scope : Ag.Scope) =
            v.Child?VertexIndexBuffer <- Some v.Value

        member x.VertexAttributes(e : Root<ISg>, scope : Ag.Scope) =
            e.Child?VertexAttributes <- Map.empty<Symbol, BufferView>

        member x.VertexAttributes(v : Sg.VertexAttributeApplicator, scope : Ag.Scope) =
            v.Child?VertexAttributes <- Map.union scope.VertexAttributes v.Values

        member x.InstanceAttributes(v : Sg.InstanceAttributeApplicator, scope : Ag.Scope) =
            v.Child?InstanceAttributes <- Map.union scope.InstanceAttributes v.Values
