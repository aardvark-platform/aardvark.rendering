namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module ARB_pipeline_statistics_query =

    type GL private() =
        static member ARB_pipeline_statistics_query =
            ExtensionHelpers.isSupported (Version(4, 6)) "GL_ARB_pipeline_statistics_query"

    [<AutoOpen>]
    module private Values =
        let VerticesSubmitted               = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.VerticesSubmittedArb)
        let PrimitivesSubmitted             = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.PrimitivesSubmittedArb)
        let VertexShaderInvocations         = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.VertexShaderInvocationsArb)
        let GeometryShaderInvocations       = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.GeometryShaderInvocations)
        let GeometryShaderPrimitivesEmitted = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.GeometryShaderPrimitivesEmittedArb)
        let ClippingInputPrimitives         = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.ClippingInputPrimitivesArb)
        let ClippingOutputPrimitives        = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.ClippingOutputPrimitivesArb)
        let FragmentShaderInvocations       = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.FragmentShaderInvocationsArb)
        let TessControlShaderPatches        = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.TessControlShaderPatchesArb)
        let TessEvaluationShaderInvocations = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.TessEvaluationShaderInvocationsArb)
        let ComputeShaderInvocations        = unbox<QueryTarget> (int ArbPipelineStatisticsQuery.ComputeShaderInvocationsArb)

    type QueryTarget with
        static member VerticesSubmitted               = VerticesSubmitted
        static member PrimitivesSubmitted             = PrimitivesSubmitted
        static member VertexShaderInvocations         = VertexShaderInvocations
        static member GeometryShaderInvocations       = GeometryShaderInvocations
        static member GeometryShaderPrimitivesEmitted = GeometryShaderPrimitivesEmitted
        static member ClippingInputPrimitives         = ClippingInputPrimitives
        static member ClippingOutputPrimitives        = ClippingOutputPrimitives
        static member FragmentShaderInvocations       = FragmentShaderInvocations
        static member TessControlShaderPatches        = TessControlShaderPatches
        static member TessEvaluationShaderInvocations = TessEvaluationShaderInvocations
        static member ComputeShaderInvocations        = ComputeShaderInvocations