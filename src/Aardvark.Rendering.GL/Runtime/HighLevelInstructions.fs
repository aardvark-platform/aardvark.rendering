namespace Aardvark.Rendering.GL.Compiler

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
open Microsoft.FSharp.NativeInterop

#nowarn "9"

module Instructions =
    open OpenTK.Graphics.OpenGL4

    let setDepthMask (active : bool) =
        Instruction.DepthMask(if active then 1 else 0)

    let setStencilMask (active : bool) =
        Instruction.StencilMask(if active then 0b11111111111111111111111111111111 else 0)


    let setColorMasks (masks : list<V4i>) =
        masks |> List.mapi (fun i mask ->
            Instruction.ColorMask i mask.X mask.Y mask.Z mask.W
        )

    let setDepthTest (m : IResource<DepthTestModeHandle>) =
        m.Handle |> Mod.force |> Instruction.HSetDepthTest


    let setPolygonMode (m : IResource<PolygonModeHandle>) =
        m.Handle |> Mod.force |> Instruction.HSetPolygonMode

    let setCullMode (m : IResource<CullModeHandle>) =        
        m.Handle |> Mod.force |> Instruction.HSetCullFace

    let setBlendMode (m : IResource<BlendModeHandle>) =
        m.Handle |> Mod.force |> Instruction.HSetBlendMode

    let setStencilMode (m : IResource<StencilModeHandle>) =
        m.Handle |> Mod.force |> Instruction.HSetStencilMode 

    let bindProgram (p : IResource<Program>) =
        p.Handle |> Mod.map (fun r -> Instruction.BindProgram(r.Handle))

    let bindUniformBuffer (index : int) (u : IResource<UniformBuffer>) =   
        u.Handle |> Mod.map (fun r -> 
            //ExecutionContext.bindUniformBuffer index r
            Instruction.BindBufferRange (int OpenGl.Enums.BufferTarget.UniformBuffer) index r.Handle 0n (nativeint r.Size)
        )

    let bindUniformBufferView (index : int) (u : IResource<UniformBufferView>) =   
        u.Handle |> Mod.bind (fun r ->
            r.Buffer |> Mod.map (fun b ->
                let b = unbox<Buffer> b
                //ExecutionContext.bindUniformBuffer index r
                Instruction.BindBufferRange (int OpenGl.Enums.BufferTarget.UniformBuffer) index b.Handle r.Offset (nativeint r.Size)
            )
        )

    let bindIndirectBuffer (u : IResource<IndirectBuffer>) =   
        u.Handle |> Mod.map (fun r -> 
            //ExecutionContext.bindUniformBuffer index r
            Instruction.BindBuffer (int OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer) r.Buffer.Handle
        )

   

    let setActiveTexture (index : int) =
        Instruction.ActiveTexture ((int OpenGl.Enums.TextureUnit.Texture0) + index)

    let bindSampler (index : int) (sampler : IResource<Sampler>) =
        if ExecutionContext.samplersSupported then
            sampler.Handle |> Mod.map (fun r -> [Instruction.BindSampler index r.Handle])
        else
            let s = sampler.Handle.GetValue().Description
            let target = int OpenGl.Enums.TextureTarget.Texture2D
            let unit = int OpenGl.Enums.TextureUnit.Texture0 + index 
            Mod.constant [
                Instruction.TexParameteri target (int TextureParameterName.TextureWrapS) (SamplerStateHelpers.wrapMode s.AddressU)
                Instruction.TexParameteri target (int TextureParameterName.TextureWrapT) (SamplerStateHelpers.wrapMode s.AddressV)
                Instruction.TexParameteri target (int TextureParameterName.TextureWrapR) (SamplerStateHelpers.wrapMode s.AddressW)
                Instruction.TexParameteri target (int TextureParameterName.TextureMinFilter) (SamplerStateHelpers.minFilter s.Filter.Min s.Filter.Mip)
                Instruction.TexParameteri target (int TextureParameterName.TextureMagFilter) (SamplerStateHelpers.magFilter s.Filter.Mag)
                Instruction.TexParameterf target (int TextureParameterName.TextureLodBias) (s.MipLodBias)
                Instruction.TexParameterf target (int TextureParameterName.TextureMinLod) (s.MinLod)
                Instruction.TexParameterf target (int TextureParameterName.TextureMaxLod) (s.MaxLod)
            ]

    let bindTexture (tex : IResource<Texture>) =
        tex.Handle |> Mod.map(fun r -> 
            let target = Translations.toGLTarget r.Dimension r.IsArray r.Multisamples
            [ Instruction.BindTexture target r.Handle ]
        )

    let bindVertexAttribValue (index : int) (value : IMod<Option<V4f>>) =
        value |> Mod.map (fun v ->
            match v with
                | Some v -> [Instruction.VertexAttrib4f index v.X v.Y v.Z v.W]
                | _ -> []
        )

    let drawIndirect (program : Program) (indexBuffer : Option<BufferView>) (buffer : IResource<IndirectBuffer>) (mode : IMod<IndexedGeometryMode>) (isActive : IMod<bool>) =
        let hasTess = program.Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl)

        let indexed, indexType = 
            match indexBuffer with
                | Some view -> true, view.ElementType
                | _ -> false, typeof<obj>
//            if indexArray <> null then
//                indexArray |> Mod.map (fun ia -> (ia <> null, if ia <> null then ia.GetType().GetElementType() else typeof<obj>))
//            else
//                Mod.constant (false, typeof<obj>) 

        let patchSize (mode : IndexedGeometryMode) =
            Translations.toPatchCount mode

        let instruction  =
            adaptive {
                let! buffer = buffer.Handle
                let! igMode = mode
                let count = NativePtr.toNativeInt buffer.Count
                let mode =
                    if hasTess then int OpenGl.Enums.DrawMode.Patches
                    else 
                        let realMode = 
                            match program.SupportedModes with
                                | Some set ->
                                    if Set.contains igMode set then 
                                        igMode
                                    else failwithf "invalid mode for program: %A (should be in: %A)" igMode set
                                | None -> 
                                    igMode

                        Translations.toGLMode realMode
                

                if indexed then

                    let indexType =
                        if indexType = typeof<byte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<uint16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<uint32> then int OpenGl.Enums.IndexType.UnsignedInt
                        elif indexType = typeof<sbyte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<int16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<int32> then int OpenGl.Enums.IndexType.UnsignedInt
                        else failwithf "unsupported index type: %A"  indexType

                    return igMode, Instruction.MultiDrawElementsIndirectPtr mode indexType 0n count buffer.Stride
                else
                    return igMode, Instruction.MultiDrawArraysIndirectPtr mode 0n count buffer.Stride
            }


        instruction |> Mod.map (fun (mode,i) ->
            if hasTess then
                let size = patchSize mode
                [ Instruction.PatchParameter (int OpenTK.Graphics.OpenGL4.PatchParameterInt.PatchVertices) size; i]
            else
                [i]
        )

    let draw (program : Program) (indexBuffer : Option<BufferView>) (call : IMod<list<DrawCallInfo>>) (mode : IMod<IndexedGeometryMode>) (isActive : IMod<bool>) =
        let hasTess = program.Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl)

        let indexed, indexType = 
            match indexBuffer with
                | Some view -> true, view.ElementType
                | None -> false, typeof<obj>
//            if indexArray <> null then
//                indexArray |> Mod.map (fun ia -> (ia <> null, if ia <> null then ia.GetType().GetElementType() else typeof<obj>))
//            else
//                Mod.constant (false, typeof<obj>)

        let patchSize (mode : IndexedGeometryMode) =
            Translations.toPatchCount mode

        let instruction  =
            adaptive {
                let! igMode = mode
                let! (isActive) = isActive

                let! calls = call
                return 
                    igMode,
                    calls |> List.map (fun call ->
                        let faceVertexCount =
                            if isActive then call.FaceVertexCount
                            else 0

                        let mode =
                            if hasTess then int OpenGl.Enums.DrawMode.Patches
                            else 
                                let realMode = 
                                    match program.SupportedModes with
                                        | Some set ->
                                            if Set.contains igMode set then 
                                                igMode
                                            else failwithf "invalid mode for program: %A (should be in: %A)" igMode set
                                        | None -> 
                                            igMode

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
                                | 1 -> Instruction.DrawElements mode faceVertexCount indexType offset
                                | n -> Instruction.DrawElementsInstanced mode faceVertexCount indexType offset n
                        else
                            match call.InstanceCount with
                                | 1 -> Instruction.DrawArrays mode call.FirstIndex faceVertexCount
                                | n -> Instruction.DrawArraysInstanced mode call.FirstIndex faceVertexCount n
                    )
            }

        instruction |> Mod.map (fun (mode,i) ->
            if hasTess then
                let size = patchSize mode
                [ Instruction.PatchParameter (int OpenTK.Graphics.OpenGL4.PatchParameterInt.PatchVertices) size ] @ i
            else
                i
        )
