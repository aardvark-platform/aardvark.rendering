namespace Aardvark.SceneGraph.Semantics

open System

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.SceneGraph

[<AutoOpen>]
module AttributeExtensions =
    type ISg with
        member x.FaceVertexCount : IMod<int> = x?FaceVertexCount
        member x.VertexAttributes : Map<Symbol, BufferView> = x?VertexAttributes
        member x.InstanceAttributes : Map<Symbol, BufferView> = x?InstanceAttributes
        member x.VertexIndexBuffer : Option<BufferView> = x?VertexIndexBuffer

    module Semantic =
        let faceVertexCount (s : ISg) : IMod<int> = s?FaceVertexCount
        let vertexAttributes (s : ISg) : Map<Symbol, BufferView> = s?VertexAttributes
        let instanceAttributes (s : ISg) : Map<Symbol, BufferView> = s?InstanceAttributes
        let vertexIndexBuffer (s : ISg) : Option<BufferView> = s?VertexIndexBuffer

module AttributeSemantics =

    let emptyIndex : IMod<Array> = Mod.constant ([||] :> Array)

    [<Semantic>]
    type AttributeSem() =
        static let zero = Mod.constant 0
        let (~%) (m : Map<Symbol, BufferView>) = m

        static let bufferViewCount (view : BufferView) =
            if view.IsSingleValue then
                Mod.constant 0
            else
                let elementSize = System.Runtime.InteropServices.Marshal.SizeOf view.ElementType
                view.Buffer |> Mod.map (fun b ->
                    match b with
                        | :? INativeBuffer as b -> (b.SizeInBytes - view.Offset) / elementSize
                        | _ -> failwithf "[Sg] could not determine buffer-size: %A" b
                )


        member x.FaceVertexCount (root : Root<ISg>) =
            root.Child?FaceVertexCount <- zero

        member x.FaceVertexCount (app : Sg.VertexIndexApplicator) =
            app.Child?FaceVertexCount <- app.Value |> bufferViewCount

        member x.FaceVertexCount (app : Sg.VertexAttributeApplicator) =
            let res = app.VertexIndexBuffer

            match res with
                | Some b ->
                    app.Child?FaceVertexCount <- app.FaceVertexCount
                | _ -> 
                    match Map.tryFind DefaultSemantic.Positions app.Values with
                        | Some pos ->
                            app.Child?FaceVertexCount <- 
                                pos.Buffer 
                                    |> Mod.bind (fun buffer ->
                                        match buffer with
                                            | :? ArrayBuffer as a ->
                                                Mod.constant (a.Data.Length - pos.Offset)
            
                                            | _ -> app.FaceVertexCount
                                       )
                        | _ -> app.Child?FaceVertexCount <- app.FaceVertexCount

        member x.InstanceAttributes(root : Root<ISg>) = 
            root.Child?InstanceAttributes <- %Map.empty

        member x.VertexIndexBuffer(e : Root<ISg>) =
            e.Child?VertexIndexBuffer <- Option<BufferView>.None

        member x.VertexIndexBuffer(v : Sg.VertexIndexApplicator) =
            v.Child?VertexIndexBuffer <- Some v.Value

        member x.VertexAttributes(e : Root<ISg>) =
            e.Child?VertexAttributes <- %Map.empty

        member x.VertexAttributes(v : Sg.VertexAttributeApplicator) =
            v.Child?VertexAttributes <- Map.union v.VertexAttributes v.Values

        member x.InstanceAttributes(v : Sg.InstanceAttributeApplicator) =
            v.Child?InstanceAttributes <- Map.union v.InstanceAttributes v.Values
