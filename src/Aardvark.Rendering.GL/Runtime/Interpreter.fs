namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Runtime
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering

[<AutoOpen>]
module private Values =

    [<Literal>]
    let GL_FRONT = 1028
    [<Literal>]
    let GL_BACK = 1029
    [<Literal>]
    let GL_FRONT_AND_BACK = 1032
    [<Literal>]
    let GL_PATCH_VERTICES = 36466
    [<Literal>]
    let GL_DEPTH_TEST = 2929
    [<Literal>]
    let GL_CULL_FACE = 2884
    [<Literal>]
    let GL_BLEND = 3042
    [<Literal>]
    let GL_STENCIL_TEST = 2960
    [<Literal>]
    let GL_UNIFORM_BUFFER = 35345
    [<Literal>]
    let GL_DRAW_INDIRECT_BUFFER = 36671

[<AutoOpen>]
module OpenGLInterpreter =
    module GL = OpenGl.Unsafe

    type GLState() =
        let mutable effectiveInstructions   = 0
        let mutable removedInstructions     = 0


        let mutable currentActiveTexture    = -1
        let mutable currentVAO              = -1
        let mutable currentProgram          = -1
        let mutable currentDepthFunc        = -1
        let mutable currentCullFace         = -1
        let mutable currentBlendFunc        = V4i(-1,-1,-1,-1)
        let mutable currentBlendEq          = V2i(-1, -1)
        let mutable currentBlendColor       = C4f(-1.0f, -1.0f, -1.0f, -1.0f)
        let mutable currentPolygonModeFront = -1
        let mutable currentPolygonModeBack  = -1
        let mutable currentStencilFuncFront = V3i(-1,-1,-1)
        let mutable currentStencilFuncBack  = V3i(-1,-1,-1)
        let mutable currentStencilOpFront   = V3i(-1,-1,-1)
        let mutable currentStencilOpBack    = V3i(-1,-1,-1)
        let mutable currentPatchVertices    = -1
        let mutable currentDepthMask        = 1
        let mutable currentViewport         = Box2i.Invalid

        let currentColorMasks               = Dictionary<int, V4i>(32)
        let currentSamplers                 = Dictionary<int, int>(32)
        let currentTextures                 = Dictionary<V2i, int>(32)
        let currentRangeBuffers             = Dictionary<V2i, V3l>(32)
        let currentBuffers                  = Dictionary<int, int>(32)
        let currentEnabled                  = HashSet<int>()
        let currentFramebuffers             = Dictionary<int, int>(32)
        
        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        let removed() =
            removedInstructions <- removedInstructions + 1
            false

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member private x.set (cell : byref<'a>, value : 'a) =
            if cell = value then
                removedInstructions <- removedInstructions + 1
                false
            else
                effectiveInstructions <- effectiveInstructions + 1
                cell <- value 
                true

        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
        member private x.set2 (cell1 : byref<'a>, cell2 : byref<'a>, value : 'a) =
            if cell1 = value && cell2 = value then
                removedInstructions <- removedInstructions + 1
                false
            else
                effectiveInstructions <- effectiveInstructions + 1
                cell1 <- value 
                cell2 <- value 
                true


        member x.EffectiveInstructions = effectiveInstructions
        member x.TotalInstructions = effectiveInstructions + removedInstructions

        member x.Clear() =
            effectiveInstructions   <- 0
            removedInstructions     <- 0

            currentActiveTexture    <- -1
            currentVAO              <- -1
            currentProgram          <- -1
            currentDepthFunc        <- -1
            currentCullFace         <- -1
            currentBlendFunc        <- V4i(-1,-1,-1,-1)
            currentBlendEq          <- V2i(-1, -1)
            currentBlendColor       <- C4f(-1.0f, -1.0f, -1.0f, -1.0f)
            currentPolygonModeFront <- -1
            currentPolygonModeBack  <- -1
            currentStencilFuncFront <- V3i(-1,-1,-1)
            currentStencilFuncBack  <- V3i(-1,-1,-1)
            currentStencilOpFront   <- V3i(-1,-1,-1)
            currentStencilOpBack    <- V3i(-1,-1,-1)
            currentPatchVertices    <- -1
            currentDepthMask        <- 1
            currentViewport         <- Box2i.Invalid

            currentColorMasks.Clear()
            currentSamplers.Clear() 
            currentTextures.Clear() 
            currentRangeBuffers.Clear()
            currentBuffers.Clear()
            currentEnabled.Clear()
            currentFramebuffers.Clear()

        member x.ShouldSetVertexArray (vao : int) =
            x.set(&currentVAO, vao)

        member x.ShouldSetProgram (program : int) =
            x.set(&currentProgram, program)

        member x.ShouldSetActiveTexture (slot : int) =
            x.set(&currentActiveTexture, slot)

        member x.ShouldSetSampler (slot : int, sampler : int) =
            match currentSamplers.TryGetValue slot with
                | (true, o) when o = sampler -> 
                    removed()
                | _ -> 
                    effectiveInstructions <- effectiveInstructions + 1
                    currentSamplers.[slot] <- sampler
                    true

        member x.ShouldSetTexture (target : int, texture : int) =
            let key = V2i(target, currentActiveTexture)
            match currentTextures.TryGetValue key with
                | (true, ot) when ot = texture -> 
                    removed()

                | _ ->
                    effectiveInstructions <- effectiveInstructions + 1
                    currentTextures.[key] <- texture
                    true

        member x.ShouldSetBuffer (target : int, slot : int, buffer : int, offset : nativeint, size : nativeint) =
            let key = V2i(target, slot)
            let value = V3l(int64 buffer, int64 offset, int64 size)

            match currentRangeBuffers.TryGetValue key with
                | (true, o) when o = value ->
                    removed()

                | _ ->
                    effectiveInstructions <- effectiveInstructions + 1
                    currentRangeBuffers.[key] <- value
                    true

        member x.ShouldSetBuffer (target : int, buffer : int) =
            match currentBuffers.TryGetValue target with
                | (true, o) when o = buffer ->
                    removed()

                | _ ->
                    effectiveInstructions <- effectiveInstructions + 1
                    currentBuffers.[target] <- buffer
                    true


        member x.ShouldEnable (cap : int) =
            if currentEnabled.Add cap then
                effectiveInstructions <- effectiveInstructions + 1
                true
            else
                removed()

        member x.ShouldDisable (cap : int) =
            if currentEnabled.Remove cap then
                effectiveInstructions <- effectiveInstructions + 1
                true
            else
                removed()

        member x.ShouldSetDepthFunc (f : int) =
            x.set(&currentDepthFunc, f)

        member x.ShouldSetCullFace (f : int) =
            x.set(&currentCullFace, f)

        member x.ShouldSetBlendFunction (srcRgb : int, dstRgb : int, srcAlpha : int, dstAlpha : int) =
            let value = V4i(srcRgb, dstRgb, srcAlpha, dstAlpha)
            x.set(&currentBlendFunc, value)

        member x.ShouldSetBlendEquation(front : int, back : int) =
            let value = V2i(front, back)
            x.set(&currentBlendEq, value)

        member x.ShouldSetBlendColor(c : C4f) =
            x.set(&currentBlendColor, c)

        member x.ShouldSetPolygonMode (face : int, mode : int) =
            match face with
                | GL_FRONT              -> x.set (&currentPolygonModeFront, mode)
                | GL_BACK               -> x.set (&currentPolygonModeBack, mode)
                | GL_FRONT_AND_BACK     -> x.set2 (&currentPolygonModeFront, &currentPolygonModeBack, mode)
                | _                     -> removed()

        member x.ShouldSetStencilFunction (face : int, func : int, ref : int, mask : int) =
            let value = V3i(func, ref, mask)
            match face with
                | GL_FRONT              -> x.set (&currentStencilFuncFront, value)
                | GL_BACK               -> x.set (&currentStencilFuncBack, value)
                | GL_FRONT_AND_BACK     -> x.set2 (&currentStencilFuncFront, &currentStencilFuncBack, value)
                | _                     -> removed()

        member x.ShouldSetStencilOperation (face : int, sfail : int, dfail : int, dpass : int) =
            let value = V3i(sfail, dfail, dpass)
            match face with
                | GL_FRONT              -> x.set (&currentStencilOpFront, value)
                | GL_BACK               -> x.set (&currentStencilOpBack, value)
                | GL_FRONT_AND_BACK     -> x.set2 (&currentStencilOpFront, &currentStencilOpBack, value)
                | _                     -> removed()

        member x.ShouldSetPatchVertices (p : int) =
            x.set(&currentPatchVertices, p)

        member x.ShouldSetDepthMask (mask : int) =
            x.set(&currentDepthMask, mask)

        member x.ShouldSetColorMask (index : int, mask : V4i) =
            match currentColorMasks.TryGetValue index with
                | (true, o) when o = mask ->
                    removed()

                | _ ->
                    effectiveInstructions <- effectiveInstructions + 1
                    currentColorMasks.[index] <- mask
                    true

        member x.ShouldSetFramebuffer(target : int, fbo : int) =
            match currentFramebuffers.TryGetValue(target) with
                | (true, o) when o = fbo ->
                    removed()

                | _ ->
                    effectiveInstructions <- effectiveInstructions + 1
                    currentFramebuffers.[target] <- fbo
                    true

        member x.ShouldSetViewport(b : Box2i) =
            x.set(&currentViewport, b)

    let private statePool = new System.Collections.Concurrent.ConcurrentBag<GLState>()

    type GLState with
        member inline x.bindVertexArray (vao : int) =
            if x.ShouldSetVertexArray vao then
                GL.BindVertexArray vao

        member inline x.bindProgram (prog : int) =
            if x.ShouldSetProgram prog then
                GL.BindProgram prog

        member inline x.activeTexture (slot : int) =
            if x.ShouldSetActiveTexture slot then
                GL.ActiveTexture slot

        member inline x.bindSampler (slot : int) (sampler : int) =
            if x.ShouldSetSampler(slot, sampler) then
                GL.BindSampler slot sampler

        member inline x.bindTexture (target : int) (texture : int) =
            if x.ShouldSetTexture(target, texture) then
                GL.BindTexture target texture

        member inline x.bindBufferBase (target : int) (index : int) (buffer : int) =
            if x.ShouldSetBuffer(target, index, buffer, 0n, 0n) then
                GL.BindBufferBase target index buffer

        member inline x.bindBufferRange (target : int) (index : int) (buffer : int) (offset : nativeint) (size : nativeint) =
            if x.ShouldSetBuffer(target, index, buffer, offset, size) then
                GL.BindBufferRange target index buffer offset size

        member inline x.bindBuffer (target : int) (buffer : int) =
            if x.ShouldSetBuffer(target, buffer) then
                GL.BindBuffer target buffer


        member inline x.enable  (cap : int) =
            if x.ShouldEnable cap then
                GL.Enable cap

        member inline x.disable (cap : int) =
            if x.ShouldDisable cap then
                GL.Disable cap

        member inline x.depthFunc (func : int) =
            if x.ShouldSetDepthFunc func then
                GL.DepthFunc func

        member inline x.cullFace (face : int) =
            if x.ShouldSetCullFace face then
                GL.CullFace face

        member inline x.blendFunction (srcRgb : int) (dstRgb : int) (srcAlpha : int) (dstAlpha : int) =
            if x.ShouldSetBlendFunction(srcRgb, dstRgb, srcAlpha, dstAlpha) then
                GL.BlendFuncSeparate srcRgb dstRgb srcAlpha dstAlpha

        member inline x.blendEquation (front : int) (back : int) =
            if x.ShouldSetBlendEquation(front, back) then
                GL.BlendEquationSeparate front back

        member inline x.blendColor (color : C4f) =
            if x.ShouldSetBlendColor color then
                GL.BlendColorF color.R color.G color.B color.A

        member inline x.polygonMode (face : int) (mode : int) =
            if x.ShouldSetPolygonMode(face, mode) then 
                GL.PolygonMode face mode

        member inline x.stencilFunction (face : int) (func : int) (ref : int) (mask : int) =
            if x.ShouldSetStencilFunction(face, func, ref, mask) then
                GL.StencilFuncSeparate face func ref mask

        member inline x.stencilOperation (face : int) (sfail : int) (dfail : int) (dpass : int) =
            if x.ShouldSetStencilFunction(face, sfail, dfail, dpass) then
                GL.StencilOpSeparate face sfail dfail dpass

        member inline x.patchVertices (p : int) =
            if x.ShouldSetPatchVertices p then
                GL.PatchParameter GL_PATCH_VERTICES p

        member inline x.depthMask (mask : int) =
            if x.ShouldSetDepthMask mask then
                GL.DepthMask mask

        member inline x.colorMask (index : int) (mask : V4i) =
            if x.ShouldSetColorMask(index, mask) then
                GL.ColorMask index mask.X mask.Y mask.Z mask.W

        member inline x.bindFramebuffer (target : int) (fbo : int) =
            if x.ShouldSetFramebuffer(target, fbo) then
                GL.BindFramebuffer target fbo

        member inline x.viewport (target : int) (viewport : Box2i) =
            if x.ShouldSetViewport(viewport) then
                OpenTK.Graphics.OpenGL4.GL.Viewport(viewport.Min.X, viewport.Min.Y, viewport.SizeX, viewport.SizeY)


        member inline x.drawElements (mode : int) (count : int) (indexType : int) (indices : nativeint) =
            GL.DrawElements mode count indexType indices

        member inline x.drawArrays (mode : int) (first : int) (count : int) =
            GL.DrawArrays mode first count

        member inline x.drawElementsInstanced (mode : int) (count : int) (indexType : int) (indices : nativeint) (primCount : int) =
            GL.DrawElementsInstanced mode count indexType indices primCount

        member inline x.drawArraysInstanced (mode : int) (first : int) (count : int) (primCount : int) =
            GL.DrawArraysInstanced mode first count primCount


        member inline x.multiDrawArraysIndirect (mode : int) (indirect : nativeint) (count : int) (stride : int) =
            GL.MultiDrawArraysIndirect mode indirect count stride

        member inline x.multiDrawElementsIndirect (mode : int) (indexType : int) (indirect : nativeint) (count : int) (stride : int) =
            GL.MultiDrawElementsIndirect mode indexType indirect count stride

        member inline x.uniform1fv (l : int) (c : int) (p : nativeint) = GL.Uniform1fv l c p
        member inline x.uniform2fv (l : int) (c : int) (p : nativeint) = GL.Uniform2fv l c p
        member inline x.uniform3fv (l : int) (c : int) (p : nativeint) = GL.Uniform3fv l c p
        member inline x.uniform4fv (l : int) (c : int) (p : nativeint) = GL.Uniform4fv l c p
        member inline x.uniform1iv (l : int) (c : int) (p : nativeint) = GL.Uniform1iv l c p
        member inline x.uniform2iv (l : int) (c : int) (p : nativeint) = GL.Uniform2iv l c p
        member inline x.uniform3iv (l : int) (c : int) (p : nativeint) = GL.Uniform3iv l c p
        member inline x.uniform4iv (l : int) (c : int) (p : nativeint) = GL.Uniform4iv l c p
        member inline x.uniformMatrix2fv (l : int) (c : int) (t : int) (p : nativeint) = GL.UniformMatrix2fv l c t p
        member inline x.uniformMatrix3fv (l : int) (c : int) (t : int) (p : nativeint) = GL.UniformMatrix3fv l c t p
        member inline x.uniformMatrix4fv (l : int) (c : int) (t : int) (p : nativeint) = GL.UniformMatrix4fv l c t p
        member inline x.vertexAttrib4f (index : int) (value : V4f) = GL.VertexAttrib4f index value.X value.Y value.Z value.W

    module Interpreter = 

        let start() = 
            match statePool.TryTake() with
                | (true, state) -> 
                    state
                | _ -> 
                    let s = GLState()
                    s

        let stop(s : GLState) =
            s.Clear()
            statePool.Add(s)

        let inline run (f : GLState -> 'a) =
            let state = start()
            try f state
            finally stop state

[<AutoOpen>]
module OpenGLObjectInterpreter =

    type GLState with

        member gl.setDepthMask (mask : bool) =
            gl.depthMask (if mask then 1 else 0)

        member gl.setColorMasks (masks : list<V4i>) =
            let mutable i = 0
            for m in masks do
                gl.colorMask i m
                i <- i + 1

        member gl.setDepthTestMode (mode : DepthTestMode) =
            if mode = DepthTestMode.None then
                gl.disable GL_DEPTH_TEST  
            else
                gl.enable GL_DEPTH_TEST
                gl.depthFunc (Translations.toGLComparison mode)

        member gl.setFillMode (mode : FillMode) =
            gl.polygonMode GL_FRONT_AND_BACK (Translations.toGLPolygonMode mode)

        member gl.setCullMode (mode : CullMode) =
            if mode = CullMode.None then
                gl.disable GL_CULL_FACE
            else
                gl.enable GL_CULL_FACE
                gl.cullFace (Translations.toGLFace mode)

        member gl.setBlendMode (bm : BlendMode) =
            if bm.Enabled then
                let src = Translations.toGLFactor bm.SourceFactor
                let dst = Translations.toGLFactor bm.DestinationFactor
                let op = Translations.toGLOperation bm.Operation
                let srcA = Translations.toGLFactor bm.SourceAlphaFactor
                let dstA = Translations.toGLFactor bm.DestinationAlphaFactor
                let opA = Translations.toGLOperation bm.AlphaOperation

                gl.enable GL_BLEND
                gl.blendFunction src dst srcA dstA
                gl.blendEquation op opA
            else
                gl.disable GL_BLEND

        member gl.setStencilMode (sm : StencilMode) =
            if sm.IsEnabled then
                let cmpFront = Translations.toGLFunction sm.CompareFront.Function
                let cmpBack= Translations.toGLFunction sm.CompareBack.Function
                let opFrontSF = Translations.toGLStencilOperation sm.OperationFront.StencilFail
                let opBackSF = Translations.toGLStencilOperation sm.OperationBack.StencilFail
                let opFrontDF = Translations.toGLStencilOperation sm.OperationFront.DepthFail
                let opBackDF = Translations.toGLStencilOperation sm.OperationBack.DepthFail
                let opFrontP = Translations.toGLStencilOperation sm.OperationFront.DepthPass
                let opBackP = Translations.toGLStencilOperation sm.OperationBack.DepthPass
                gl.enable GL_STENCIL_TEST
                gl.stencilFunction GL_FRONT cmpFront sm.CompareFront.Reference (int sm.CompareFront.Mask)
                gl.stencilFunction GL_BACK cmpBack sm.CompareBack.Reference (int sm.CompareBack.Mask)
                gl.stencilOperation GL_FRONT opFrontSF opFrontDF opFrontP
                gl.stencilOperation GL_BACK opBackSF opBackDF opBackP
            else
                gl.disable GL_STENCIL_TEST

        member gl.bindUniformLocation (l : int) (loc : UniformLocation)=
            match loc.Type with
                | FloatVectorType 1 ->
                    gl.uniform1fv l 1 loc.Data
                | IntVectorType 1 ->
                    gl.uniform1iv l 1 loc.Data

                | FloatVectorType 2 ->
                    gl.uniform2fv l 1 loc.Data
                | IntVectorType 2 ->
                    gl.uniform2iv l 1 loc.Data

                | FloatVectorType 3 ->
                    gl.uniform3fv l 1 loc.Data
                | IntVectorType 3 ->
                    gl.uniform3iv l 1 loc.Data

                | FloatVectorType 4 ->
                    gl.uniform4fv l 1 loc.Data
                | IntVectorType 4 ->
                    gl.uniform4iv l 1 loc.Data


                | FloatMatrixType(2,2) ->
                    gl.uniformMatrix2fv l 1 1 loc.Data

                | FloatMatrixType(3,3) ->
                    gl.uniformMatrix3fv l 1 1 loc.Data

                | FloatMatrixType(4,4) ->
                    gl.uniformMatrix4fv l 1 1 loc.Data

                | _ ->
                    failwithf "no uniform-setter for: %A" loc

        member gl.render (o : PreparedRenderObject) =
            if Mod.force o.IsActive then
                gl.setDepthMask o.DepthBufferMask

                match o.ColorBufferMasks with
                    | Some masks -> gl.setColorMasks masks
                    | None -> gl.setColorMasks (List.init o.ColorAttachmentCount (fun _ -> V4i.IIII))


                let depthMode = o.DepthTest.GetValue()
                let fillMode = o.FillMode.GetValue()
                let cullMode = o.CullMode.GetValue()
                let blendMode = o.BlendMode.GetValue()
                let stencilMode = o.StencilMode.GetValue()

                gl.setDepthTestMode depthMode
                gl.setFillMode fillMode
                gl.setCullMode cullMode
                gl.setBlendMode blendMode
                gl.setStencilMode stencilMode

                let program = o.Program.Handle.GetValue()
                let vao = o.VertexArray.Handle.GetValue().Handle
                let indexed = Option.isSome o.IndexBuffer

                let hasTess = program.Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl)

                let patchSize =
                    match o.Mode.GetValue() with
                        | IndexedGeometryMode.LineList -> 2
                        | IndexedGeometryMode.PointList -> 1
                        | IndexedGeometryMode.TriangleList -> 3
                        | IndexedGeometryMode.LineStrip -> 2
                        | IndexedGeometryMode.TriangleStrip -> 3
                        | m -> failwithf "unsupported patch-mode: %A" m

                let mode =
                    let igMode = o.Mode.GetValue()

                    if hasTess then 
                        int OpenGl.Enums.DrawMode.Patches
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

                let indexType =
                    if isNull o.Original.Indices then
                        0
                    else
                        let indexType = o.Original.Indices.GetValue().GetType().GetElementType()
                        if indexType = typeof<byte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<uint16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<uint32> then int OpenGl.Enums.IndexType.UnsignedInt
                        elif indexType = typeof<sbyte> then int OpenGl.Enums.IndexType.UnsignedByte
                        elif indexType = typeof<int16> then int OpenGl.Enums.IndexType.UnsignedShort
                        elif indexType = typeof<int32> then int OpenGl.Enums.IndexType.UnsignedInt
                        else failwithf "unsupported index type: %A"  indexType

                gl.bindProgram program.Handle

                for (id, ub) in Map.toSeq o.UniformBuffers do
                    let ub = ub.Handle.GetValue()
                    let b = ub.Buffer.GetValue() |> unbox<Aardvark.Rendering.GL.Buffer>
                    gl.bindBufferRange GL_UNIFORM_BUFFER id b.Handle ub.Offset (nativeint ub.Size)

                for (id, (tex, sam)) in Map.toSeq o.Textures do
                    let tex = tex.Handle.GetValue()
                    let sam = sam.Handle.GetValue()

                    let target = Translations.toGLTarget tex.Dimension tex.IsArray tex.Multisamples

                    gl.activeTexture id
                    gl.bindTexture target tex.Handle
                    gl.bindSampler id sam.Handle

                for (id,u) in Map.toSeq o.Uniforms do
                    let u = u.Handle.GetValue()
                    gl.bindUniformLocation id u

                gl.bindVertexArray vao

                for (id,v) in Map.toSeq o.VertexAttributeValues do
                    match v.GetValue() with
                        | Some v -> gl.vertexAttrib4f id v
                        | None -> ()

                if hasTess then
                    gl.patchVertices patchSize

                match o.IndirectBuffer with
                    | Some ib ->
                        let ib = ib.Handle.GetValue()
                        let cnt = ib.Count |> Microsoft.FSharp.NativeInterop.NativePtr.read

                        if cnt > 0 then
                            let cnt =
                                let cap = int ib.Buffer.SizeInBytes / sizeof<DrawCallInfo>
                                if cnt > cap then
                                    Log.warn "indirect too small"
                                    cap
                                else
                                    cnt

                            gl.bindBuffer GL_DRAW_INDIRECT_BUFFER ib.Buffer.Handle

                            if indexed then
                                gl.multiDrawElementsIndirect mode indexType 0n cnt ib.Stride
                            else
                                gl.multiDrawArraysIndirect mode 0n cnt ib.Stride

                    | None ->
                        let calls = o.DrawCallInfos.Handle.GetValue()
                        if indexed then
                            for c in calls do
                                if c.InstanceCount = 1 then
                                    gl.drawElements mode c.FaceVertexCount indexType (nativeint c.FirstIndex)
                                elif c.InstanceCount > 0 then
                                    gl.drawElementsInstanced mode c.FaceVertexCount indexType (nativeint c.FirstIndex) c.InstanceCount
                                    
                        else
                            for c in calls do
                                if c.InstanceCount = 1 then
                                    gl.drawArrays mode c.FirstIndex c.FaceVertexCount
                                elif c.InstanceCount > 0 then
                                    gl.drawArraysInstanced mode c.FirstIndex c.FaceVertexCount c.InstanceCount

                OpenTK.Graphics.OpenGL4.GL.Flush()
                OpenTK.Graphics.OpenGL4.GL.Finish()


        member gl.render (o : PreparedMultiRenderObject) =
            for o in o.Children do
                gl.render o


[<AbstractClass>]
type AbstractAdaptiveProgram<'input when 'input :> IAdaptiveObject>() =
    inherit DirtyTrackingAdaptiveObject<'input>()
    
    abstract member Dispose : unit -> unit
    abstract member Update : HashSet<'input> -> unit
    abstract member Run : unit -> unit

    interface IAdaptiveProgram<unit> with
        member x.Update caller =
            x.EvaluateIfNeeded' caller AdaptiveProgramStatistics.Zero (fun dirty ->
                x.Update dirty
                AdaptiveProgramStatistics.Zero
            )

        member x.Run s = x.Run()
        member x.Disassemble() = null
        member x.AutoDefragmentation 
            with get() = false
            and set _ = ()

        member x.StartDefragmentation() = System.Threading.Tasks.Task.FromResult(TimeSpan.Zero)
        member x.NativeCallCount = 0
        member x.FragmentCount = 1
        member x.ProgramSizeInBytes = 0L
        member x.TotalJumpDistanceInBytes = 0L
        member x.Dispose() = x.Dispose()
  

type InterpreterProgram(content : seq<PreparedMultiRenderObject>) =
    inherit AbstractAdaptiveProgram<IAdaptiveObject>()
    override x.Update _ = ()
    override x.Dispose() = ()
    override x.Run() = 
        Interpreter.run (fun gl -> 
            for o in content do gl.render o
//            { FrameStatistics.Zero with
//                InstructionCount = gl.TotalInstructions |> float
//                ActiveInstructionCount = gl.EffectiveInstructions |> float
//            }
        )
 