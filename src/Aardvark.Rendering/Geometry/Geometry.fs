namespace Aardvark.Rendering

open Aardvark.Base
open FSharp.Data.Adaptive

[<ReferenceEquality>]
type Geometry =
    {
        vertexAttributes    : Map<Symbol, aval<IBuffer>>
        indices             : Option<BufferView>
        uniforms            : Map<string, IAdaptiveValue>
        call                : aval<DrawCallInfo[]>
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Geometry =
    /// <summary>
    /// Converts an indexed geometry, preferring the positions array length for non-indexed draw counts.
    /// Without positions, the first vertex attribute determines the non-indexed count.
    /// </summary>
    let ofIndexedGeometry (uniforms : Map<string, IAdaptiveValue>) (g : IndexedGeometry) =
        let index, fvc =
            match g.IndexArray with
                | null ->
                    let attribute =
                        match g.IndexedAttributes.TryGetValue DefaultSemantic.Positions with
                        | true, positions -> positions
                        | _ -> g.IndexedAttributes.Values |> Seq.head
                    None, attribute.Length
                | index ->
                    let buffer = AVal.constant (ArrayBuffer(index) :> IBuffer)
                    let view = BufferView(buffer, index.GetType().GetElementType())

                    Some view, index.Length

        let attributes =
            g.IndexedAttributes |> SymDict.toMap |> Map.map (fun name att ->
                let buffer = AVal.constant (ArrayBuffer(att) :> IBuffer)
                buffer
            )

        let gUniforms =
            if isNull g.SingleAttributes then
                Map.empty
            else
                let tc = typedefof<ConstantVal<_>>
                g.SingleAttributes |> SymDict.toSeq |> Seq.choose (fun (name, value) ->
                    try
                        let vt = value.GetType()
                        let t = tc.MakeGenericType(vt)
                        let ctor = t.GetConstructor [| vt |]
                        Some (name.ToString(), ctor.Invoke([| value |]) |> unbox<IAdaptiveValue>)
                    with _ ->
                        None
                )
                |> Map.ofSeq

        let call =
            DrawCallInfo(FaceVertexCount = fvc, InstanceCount = 1)

        {
            vertexAttributes    = attributes
            indices             = index
            uniforms            = Map.union gUniforms uniforms
            call                = AVal.constant [| call |]
        }

