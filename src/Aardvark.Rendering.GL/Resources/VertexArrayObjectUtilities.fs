namespace Aardvark.Rendering.GL

open Aardvark.Base
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module VertexArrayObjectExtensions =
    open AttributeDescriptionExtensions

    let private matrixTypes =
        Dict.ofList [
            typeof<M22i>, (2, 2, VertexAttribPointerType.Int, typeof<int>)
            typeof<M22f>, (2, 2, VertexAttribPointerType.Float, typeof<float32>)
            typeof<M22d>, (2, 2, VertexAttribPointerType.Double, typeof<float>)

            typeof<M23i>, (2, 3, VertexAttribPointerType.Int, typeof<int>)
            typeof<M23f>, (2, 3, VertexAttribPointerType.Float, typeof<float32>)
            typeof<M23d>, (2, 3, VertexAttribPointerType.Double, typeof<float>)

            typeof<M33i>, (3, 3, VertexAttribPointerType.Int, typeof<int>)
            typeof<M33f>, (3, 3, VertexAttribPointerType.Float, typeof<float32>)
            typeof<M33d>, (3, 3, VertexAttribPointerType.Double, typeof<float>)

            typeof<M34i>, (3, 4, VertexAttribPointerType.Int, typeof<int>)
            typeof<M34f>, (3, 4, VertexAttribPointerType.Float, typeof<float32>)
            typeof<M34d>, (3, 4, VertexAttribPointerType.Double, typeof<float>)

            typeof<M44i>, (4, 4, VertexAttribPointerType.Int, typeof<int>)
            typeof<M44f>, (4, 4, VertexAttribPointerType.Float, typeof<float32>)
            typeof<M44d>, (4, 4, VertexAttribPointerType.Double, typeof<float>)
        ]

    let unpackAttributeBindingForMatrices (id : int) (att : AttributeDescription) =
        match matrixTypes.TryGetValue att.Type with
            | (true, (rows,cols,_,elementType)) ->
                let elementSize = elementType.GLSize
                let stride = rows * cols * elementSize
                let rowDelta = cols * elementSize
                [ for i in 0 .. rows - 1 do
                    let patched =
                        { att with 
                            Type = typeof<V4f> //hackhacknowork
                            Stride = stride
                            Offset = att.Offset + i 
                        }
                    yield id + i, patched
                ]
            | _ -> [id, att]