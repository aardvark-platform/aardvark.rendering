namespace Aardvark.Base

open System
open FSharp.Data.Adaptive
open Aardvark.Base.Rendering

type PipelineState =
    {
        depthTest           : aval<DepthTestMode>
        depthBias           : aval<DepthBiasState>
        cullMode            : aval<CullMode>
        frontFace           : aval<WindingOrder>
        blendMode           : aval<BlendMode>
        fillMode            : aval<FillMode>
        stencilMode         : aval<StencilMode>
        multisample         : aval<bool>
        writeBuffers        : Option<Set<Symbol>>
        globalUniforms      : IUniformProvider

        geometryMode        : IndexedGeometryMode
        vertexInputTypes    : Map<Symbol, Type>
        perGeometryUniforms : Map<string, Type>
    }
