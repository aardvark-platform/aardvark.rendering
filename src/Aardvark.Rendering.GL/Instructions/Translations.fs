namespace Aardvark.Rendering.GL


open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL.OpenGl
        

module Translations =

    type private ABlendFactor = Aardvark.Base.Rendering.BlendFactor
    type private GLBlendFactor = Aardvark.Rendering.GL.OpenGl.Enums.BlendFactor
    type private ABlendOperation = Aardvark.Base.Rendering.BlendOperation
    type private GLBlendOperation = Aardvark.Rendering.GL.OpenGl.Enums.BlendOperation

    let toGLMode (m : IndexedGeometryMode) =
        match m with 
            | IndexedGeometryMode.TriangleList -> DrawMode.Triangles |> int
            | IndexedGeometryMode.TriangleStrip -> DrawMode.TriangleStrip |> int
            | IndexedGeometryMode.LineList -> DrawMode.Lines |> int
            | IndexedGeometryMode.LineStrip -> DrawMode.LineStrip |> int
            | IndexedGeometryMode.PointList -> DrawMode.Points |> int
            | _ -> failwith "not handled IndexedGeometryMode"

    let toPatchCount (m : IndexedGeometryMode) =
            match m with 
            | IndexedGeometryMode.TriangleList -> 3
            | IndexedGeometryMode.TriangleStrip -> 3 
            | IndexedGeometryMode.LineList -> 2
            | IndexedGeometryMode.LineStrip -> 2
            | IndexedGeometryMode.PointList -> 1
            | _ -> failwith "not handled IndexedGeometryMode"

    let toGLFactor (f : ABlendFactor) =
        match f with
            | ABlendFactor.Zero -> GLBlendFactor.Zero |> int
            | ABlendFactor.One -> GLBlendFactor.One |> int
            | ABlendFactor.DestinationAlpha -> GLBlendFactor.DstAlpha |> int
            | ABlendFactor.DestinationColor -> GLBlendFactor.DstColor |> int
            | ABlendFactor.SourceAlpha -> GLBlendFactor.SrcAlpha |> int
            | ABlendFactor.SourceColor -> GLBlendFactor.SrcColor |> int
            | ABlendFactor.InvDestinationAlpha -> GLBlendFactor.InvDstAlpha |> int
            | ABlendFactor.InvDestinationColor -> GLBlendFactor.InvDstColor |> int
            | ABlendFactor.InvSourceAlpha -> GLBlendFactor.InvSrcAlpha |> int
            | ABlendFactor.InvSourceColor -> GLBlendFactor.InvSrcColor |> int
            | _ -> failwithf "unknown blend factor: %A" f

    let toGLOperation (f : ABlendOperation) =
        match f with
            | ABlendOperation.Add -> GLBlendOperation.Add |> int
            | ABlendOperation.Subtract -> GLBlendOperation.Subtract |> int
            | ABlendOperation.ReverseSubtract -> GLBlendOperation.ReverseSubtract |> int
            | ABlendOperation.Minimum -> GLBlendOperation.Minimum |> int
            | ABlendOperation.Maximum -> GLBlendOperation.Maximum |> int
            | _ -> failwithf "unknown blend operation %A" f

    let toGLComparison (f : DepthTestMode) =
        match f with
            | DepthTestMode.Greater -> CompareFunction.Greater |> int
            | DepthTestMode.GreaterOrEqual -> CompareFunction.GreaterEqual |> int
            | DepthTestMode.Less -> CompareFunction.Less |> int
            | DepthTestMode.LessOrEqual -> CompareFunction.LessEqual |> int
            | _ -> failwithf "unknown comparison %A" f

    let toGLFace (f : CullMode) =
        match f with
            | CullMode.Clockwise -> Face.Back |> int
            | CullMode.CounterClockwise -> Face.Front |> int
            | CullMode.None -> 0
            | _ -> failwithf "unknown comparison %A" f

    let toGLFunction (f : StencilCompareFunction) =
        match f with
            | StencilCompareFunction.Always -> CompareFunction.Always |> int
            | StencilCompareFunction.Equal -> CompareFunction.Equal |> int
            | StencilCompareFunction.Greater -> CompareFunction.Greater |> int
            | StencilCompareFunction.GreaterOrEqual -> CompareFunction.GreaterEqual |> int
            | StencilCompareFunction.Less -> CompareFunction.Less |> int
            | StencilCompareFunction.LessOrEqual -> CompareFunction.LessEqual |> int
            | StencilCompareFunction.Never -> CompareFunction.Never |> int
            | StencilCompareFunction.NotEqual -> CompareFunction.NotEqual |> int
            | _ -> failwithf "unknown comparison %A" f

    let toGLStencilOperation (o : StencilOperationFunction) =
        match o with
            | StencilOperationFunction.Decrement -> StencilOperation.Decrement |> int
            | StencilOperationFunction.DecrementWrap -> StencilOperation.DecrementWrap  |> int
            | StencilOperationFunction.Increment -> StencilOperation.Increment  |> int
            | StencilOperationFunction.IncrementWrap -> StencilOperation.IncrementWrap  |> int
            | StencilOperationFunction.Invert -> StencilOperation.Invert  |> int
            | StencilOperationFunction.Keep -> StencilOperation.Keep  |> int
            | StencilOperationFunction.Replace -> StencilOperation.Replace  |> int
            | StencilOperationFunction.Zero -> StencilOperation.Zero  |> int
            | _ -> failwithf "unknown stencil operation %A" o

    let toGLPolygonMode (f : FillMode) =
        match f with
            | FillMode.Fill -> Enums.PolygonMode.Fill |> int
            | FillMode.Line -> Enums.PolygonMode.Line |> int
            | FillMode.Point -> Enums.PolygonMode.Point |> int
            | _ -> failwithf "unknown FillMode: %A" f

    let toGLTarget (d : TextureDimension) (isArray : bool) (samples : int) =
        match d with
            | TextureDimension.Texture1D -> int TextureTarget.Texture1D
            | TextureDimension.Texture2D -> int TextureTarget.Texture2D
            | TextureDimension.Texture3D -> int TextureTarget.Texture3D
            | TextureDimension.TextureCube -> int TextureTarget.TextureCubeMap
            | _ -> failwithf "unknown TextureDimension: %A" d
