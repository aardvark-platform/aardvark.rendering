namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL

module Instructions =
    
    let setDepthTest (m : IMod<DepthTestMode>) =
        m |> Mod.map (fun dt ->
            if dt <> DepthTestMode.None then
                [ 
                    Instruction.Enable(int OpenGl.Enums.State.DepthTest)
                    Instruction.DepthFunc(Translations.toGLComparison dt) 
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.DepthTest) ]
        )

    let setFillMode (m : IMod<FillMode>) =
        m |> Mod.map (fun fm -> 
            [ Instruction.PolygonMode (int OpenGl.Enums.Face.FrontAndBack) (Translations.toGLPolygonMode fm) ]
        )

    let setCullMode (m : IMod<CullMode>) =
        m |> Mod.map (fun cm -> 
            if cm <> CullMode.None then
                [ 
                    Instruction.Enable(int OpenGl.Enums.State.CullFace)
                    Instruction.CullFace(Translations.toGLFace cm) 
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.CullFace) ]
        )

    let setBlendMode (m : IMod<BlendMode>) =
        m |> Mod.map (fun bm -> 
            //TODO: actually depending on the Framebuffer (premultiplied alpha)
            if bm.Enabled then
                let src = Translations.toGLFactor bm.SourceFactor
                let dst = Translations.toGLFactor bm.DestinationFactor
                let op = Translations.toGLOperation bm.Operation

                let srcA = Translations.toGLFactor bm.SourceAlphaFactor
                let dstA = Translations.toGLFactor bm.DestinationAlphaFactor
                let opA = Translations.toGLOperation bm.AlphaOperation

                [ 
                    Instruction.Enable(int OpenGl.Enums.State.Blend)
                    Instruction.BlendFuncSeparate src dst srcA dstA
                    Instruction.BlendEquationSeparate op opA
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.Blend) ]
        )

    let setStencilMode (m : IMod<StencilMode>) =
        m |> Mod.map (fun sm -> 
            //TODO: actually depending on the Framebuffer (premultiplied alpha)
            if sm.IsEnabled then
                let cmpFront = Translations.toGLFunction sm.CompareFront.Function
                let cmpBack= Translations.toGLFunction sm.CompareBack.Function
                let opFrontSF = Translations.toGLStencilOperation sm.OperationFront.StencilFail
                let opBackSF = Translations.toGLStencilOperation sm.OperationBack.StencilFail
                let opFrontDF = Translations.toGLStencilOperation sm.OperationFront.DepthFail
                let opBackDF = Translations.toGLStencilOperation sm.OperationBack.DepthFail
                let opFrontP = Translations.toGLStencilOperation sm.OperationFront.DepthPass
                let opBackP = Translations.toGLStencilOperation sm.OperationBack.DepthPass

                [ 
                    Instruction.Enable(int OpenGl.Enums.State.StencilTest) 
                    Instruction.StencilFuncSeparate(int OpenGl.Enums.Face.Front) cmpFront sm.CompareFront.Reference (int sm.CompareFront.Mask)
                    Instruction.StencilFuncSeparate(int OpenGl.Enums.Face.Back) cmpBack sm.CompareBack.Reference (int sm.CompareBack.Mask)
                    Instruction.StencilOpSeparate(int OpenGl.Enums.Face.Front) opFrontSF opFrontDF opFrontP
                    Instruction.StencilOpSeparate(int OpenGl.Enums.Face.Back) opBackSF opBackDF opBackP
                ]
            else
                [ Instruction.Disable(int OpenGl.Enums.State.StencilTest) ]
        )

    let bindProgram (p : ChangeableResource<Program>) =
        p.Resource |> Mod.map (fun r -> Instruction.BindProgram(r.Handle))

    let bindUniformBuffer (index : int) (u : ChangeableResource<UniformBuffer>) =   
        u.Resource |> Mod.map (fun r -> 
            //ExecutionContext.bindUniformBuffer index r
            Instruction.BindBufferRange (int OpenGl.Enums.BufferTarget.UniformBuffer) index r.Handle 0n (nativeint r.Size)
        )

    let setActiveTexture (index : int) =
        Instruction.ActiveTexture ((int OpenGl.Enums.TextureUnit.Texture0) + index)

    let bindSampler (index : int) (sampler : ChangeableResource<Sampler>) =
        sampler.Resource |> Mod.map (fun r -> [Instruction.BindSampler index r.Handle])

    let bindTexture (tex : ChangeableResource<Texture>) =
        tex.Resource |> Mod.map(fun r -> 
            let target = Translations.toGLTarget r.Dimension r.IsArray r.Multisamples
            [ Instruction.BindTexture target r.Handle ]
        )

    let bindVertexArray (vao : ChangeableResource<VertexArrayObject>) =
        fun (ctx : ContextHandle) -> Instruction.BindVertexArray(vao.Resource.GetValue().Handle)

    let draw (program : Program) (indexArray : IMod<System.Array>) (call : IMod<DrawCallInfo>) (isActive : IMod<bool>) =
        let hasTess = program.Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl)

        let indexType = 
            if indexArray <> null then
                indexArray |> Mod.map (fun ia -> (ia <> null, if ia <> null then ia.GetType().GetElementType() else typeof<obj>))
            else
                Mod.constant (false, typeof<obj>)

        let patchSize (mode : IndexedGeometryMode) =
            match mode with
                | IndexedGeometryMode.LineList -> 2
                | IndexedGeometryMode.PointList -> 1
                | IndexedGeometryMode.TriangleList -> 3
                | m -> failwithf "unsupported patch-mode: %A" m

        let instruction =
            adaptive {
                let! (indexed, indexType) = indexType
                let! (call, isActive) = call, isActive

                let faceVertexCount =
                    if isActive then call.FaceVertexCount
                    else 0

                let mode =
                    if hasTess then int OpenGl.Enums.DrawMode.Patches
                    else 
                        let realMode = 
                            match program.SupportedModes with
                                | Some set ->
                                    if Set.contains call.Mode set then 
                                        call.Mode
                                    elif Set.contains IndexedGeometryMode.PointList set then
                                        IndexedGeometryMode.PointList
                                    else failwith "invalid mode for program: %A (should be in: %A)" call.Mode set
                                | None -> 
                                    call.Mode

                        Translations.toGLMode realMode

                if indexed then
                    let offset = nativeint (call.FirstIndex * indexType.GLSize)

                    let indexType =
                        if indexType = typeof<byte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<uint16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<uint32> then int OpenGl.Enums.IndexType.UnsignedInt
                        elif indexType = typeof<sbyte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<int16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<int32> then int OpenGl.Enums.IndexType.UnsignedInt
                        else failwithf "unsupported index type: %A"  indexType

                    match call.InstanceCount with
                        | 1 -> return call.Mode, Instruction.DrawElements mode faceVertexCount indexType offset
                        | n -> return call.Mode, Instruction.DrawElementsInstanced mode faceVertexCount indexType offset n
                else
                    match call.InstanceCount with
                        | 1 -> return call.Mode, Instruction.DrawArrays mode call.FirstIndex faceVertexCount
                        | n -> return call.Mode, Instruction.DrawArraysInstanced mode call.FirstIndex faceVertexCount n
            }

        instruction |> Mod.map (fun (mode,i) ->
            if hasTess then
                let size = patchSize mode
                [ 
                    Instruction.PatchParameter (int OpenTK.Graphics.OpenGL4.PatchParameterInt.PatchVertices) size
                    i
                ]
            else
                [i]
        )
