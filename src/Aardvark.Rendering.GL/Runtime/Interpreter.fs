namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open System.Threading
open System.Collections.Generic
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Base.Runtime
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection

//[<AutoOpen>]
//module private Values =

//    [<Literal>]
//    let GL_FRONT = 1028
//    [<Literal>]
//    let GL_BACK = 1029
//    [<Literal>]
//    let GL_FRONT_AND_BACK = 1032
//    [<Literal>]
//    let GL_PATCH_VERTICES = 36466
//    [<Literal>]
//    let GL_DEPTH_TEST = 2929
//    [<Literal>]
//    let GL_CULL_FACE = 2884
//    [<Literal>]
//    let GL_BLEND = 3042
//    [<Literal>]
//    let GL_STENCIL_TEST = 2960
//    [<Literal>]
//    let GL_UNIFORM_BUFFER = 35345
//    [<Literal>]
//    let GL_DRAW_INDIRECT_BUFFER = 36671
//    [<Literal>]
//    let GL_TEXTURE0 = 33984 //int OpenGl.Enums.TextureUnit.Texture0
//    [<Literal>]
//    let GL_DEPTH_CLAMP = 0x864F

//[<AutoOpen>]
//module OpenGLInterpreter =
//    module GL = OpenGl.Unsafe

//    type GLState(contextHandle : nativeptr<nativeint>) =
//        let mutable effectiveInstructions   = 0
//        let mutable removedInstructions     = 0


//        let mutable currentActiveTexture    = -1
//        let mutable currentVAO              = -1
//        let mutable currentVIBH             = NativePtr.zero
//        let mutable currentProgram          = -1
//        let mutable currentDepthFunc        = -1
//        let mutable currentCullFace         = -1
//        let mutable currentBlendFunc        = V4i(-1,-1,-1,-1)
//        let mutable currentBlendEq          = V2i(-1, -1)
//        let mutable currentBlendColor       = C4f(-1.0f, -1.0f, -1.0f, -1.0f)
//        let mutable currentPolygonModeFront = -1
//        let mutable currentPolygonModeBack  = -1
//        let mutable currentStencilFuncFront = V3i(-1,-1,-1)
//        let mutable currentStencilFuncBack  = V3i(-1,-1,-1)
//        let mutable currentStencilOpFront   = V3i(-1,-1,-1)
//        let mutable currentStencilOpBack    = V3i(-1,-1,-1)
//        let mutable currentPatchVertices    = -1
//        let mutable currentDepthMask        = 1
//        let mutable currentStencilMask      = 0xFFFFFFFF
//        let mutable currentViewport         = Box2i.Invalid
//        let mutable currentDrawBuffers      = None

//        let currentColorMasks               = Dictionary<int, V4i>(32)
//        let currentSamplers                 = Dictionary<int, int>(32)
//        let currentTextures                 = Dictionary<V2i, int>(32)
//        let currentRangeBuffers             = Dictionary<V2i, V3l>(32)
//        let currentBuffers                  = Dictionary<int, int>(32)
//        let currentEnabled                  = HashSet<int>()
//        let currentDisabled                 = HashSet<int>()
//        let currentFramebuffers             = Dictionary<int, int>(32)
        
//        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
//        let removed() =
//            removedInstructions <- removedInstructions + 1
//            false

//        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
//        member private x.set (cell : byref<'a>, value : 'a) =
//            if cell = value then
//                removedInstructions <- removedInstructions + 1
//                false
//            else
//                effectiveInstructions <- effectiveInstructions + 1
//                cell <- value 
//                true

//        [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
//        member private x.set2 (cell1 : byref<'a>, cell2 : byref<'a>, value : 'a) =
//            if cell1 = value && cell2 = value then
//                removedInstructions <- removedInstructions + 1
//                false
//            else
//                effectiveInstructions <- effectiveInstructions + 1
//                cell1 <- value 
//                cell2 <- value 
//                true


//        member x.EffectiveInstructions = effectiveInstructions
//        member x.TotalInstructions = effectiveInstructions + removedInstructions
//        member x.ContextHandle = contextHandle

//        member x.Clear() =
//            effectiveInstructions   <- 0
//            removedInstructions     <- 0

//            currentActiveTexture    <- -1
//            currentVAO              <- -1
//            currentProgram          <- -1
//            currentDepthFunc        <- -1
//            currentCullFace         <- -1
//            currentBlendFunc        <- V4i(-1,-1,-1,-1)
//            currentBlendEq          <- V2i(-1, -1)
//            currentBlendColor       <- C4f(-1.0f, -1.0f, -1.0f, -1.0f)
//            currentPolygonModeFront <- -1
//            currentPolygonModeBack  <- -1
//            currentStencilFuncFront <- V3i(-1,-1,-1)
//            currentStencilFuncBack  <- V3i(-1,-1,-1)
//            currentStencilOpFront   <- V3i(-1,-1,-1)
//            currentStencilOpBack    <- V3i(-1,-1,-1)
//            currentPatchVertices    <- -1
//            currentDepthMask        <- 1
//            currentViewport         <- Box2i.Invalid
//            currentDrawBuffers      <- None

//            currentColorMasks.Clear()
//            currentSamplers.Clear() 
//            currentTextures.Clear() 
//            currentRangeBuffers.Clear()
//            currentBuffers.Clear()
//            currentDisabled.Clear()
//            currentEnabled.Clear()
//            currentFramebuffers.Clear()

//        member x.ShouldSetVertexArray (vao : int) =
//            x.set(&currentVAO, vao)

//        member x.ShouldBindVertexAttributes (vibh : nativeptr<VertexInputBinding>) =
//            x.set(&currentVIBH, vibh)

//        member x.ShouldSetProgram (program : int) =
//            x.set(&currentProgram, program)

//        member x.ShouldSetActiveTexture (slot : int) =
//            x.set(&currentActiveTexture, slot)

//        member x.ShouldSetSampler (slot : int, sampler : int) =
//            match currentSamplers.TryGetValue slot with
//                | (true, o) when o = sampler -> 
//                    removed()
//                | _ -> 
//                    effectiveInstructions <- effectiveInstructions + 1
//                    currentSamplers.[slot] <- sampler
//                    true

//        member x.ShouldSetTexture (target : int, texture : int) =
//            let key = V2i(target, currentActiveTexture)
//            match currentTextures.TryGetValue key with
//                | (true, ot) when ot = texture -> 
//                    removed()

//                | _ ->
//                    effectiveInstructions <- effectiveInstructions + 1
//                    currentTextures.[key] <- texture
//                    true

//        member x.ShouldSetBuffer (target : int, slot : int, buffer : int, offset : nativeint, size : nativeint) =
//            let key = V2i(target, slot)
//            let value = V3l(int64 buffer, int64 offset, int64 size)

//            match currentRangeBuffers.TryGetValue key with
//                | (true, o) when o = value ->
//                    removed()

//                | _ ->
//                    effectiveInstructions <- effectiveInstructions + 1
//                    currentRangeBuffers.[key] <- value
//                    true

//        member x.ShouldSetBuffer (target : int, buffer : int) =
//            match currentBuffers.TryGetValue target with
//                | (true, o) when o = buffer ->
//                    removed()

//                | _ ->
//                    effectiveInstructions <- effectiveInstructions + 1
//                    currentBuffers.[target] <- buffer
//                    true


//        member x.ShouldEnable (cap : int) =
//            if currentEnabled.Add cap || currentDisabled.Remove cap then
//                effectiveInstructions <- effectiveInstructions + 1
//                true
//            else
//                removed()

//        member x.ShouldDisable (cap : int) =
//            if currentDisabled.Add cap || currentEnabled.Remove cap then
//                effectiveInstructions <- effectiveInstructions + 1
//                true
//            else
//                removed()

//        member x.ShouldSetDepthFunc (f : int) =
//            x.set(&currentDepthFunc, f)

//        member x.ShouldSetCullFace (f : int) =
//            x.set(&currentCullFace, f)

//        member x.ShouldSetBlendFunction (srcRgb : int, dstRgb : int, srcAlpha : int, dstAlpha : int) =
//            let value = V4i(srcRgb, dstRgb, srcAlpha, dstAlpha)
//            x.set(&currentBlendFunc, value)

//        member x.ShouldSetBlendEquation(front : int, back : int) =
//            let value = V2i(front, back)
//            x.set(&currentBlendEq, value)

//        member x.ShouldSetBlendColor(c : C4f) =
//            x.set(&currentBlendColor, c)

//        member x.ShouldSetPolygonMode (face : int, mode : int) =
//            match face with
//                | GL_FRONT              -> x.set (&currentPolygonModeFront, mode)
//                | GL_BACK               -> x.set (&currentPolygonModeBack, mode)
//                | GL_FRONT_AND_BACK     -> x.set2 (&currentPolygonModeFront, &currentPolygonModeBack, mode)
//                | _                     -> removed()

//        member x.ShouldSetStencilFunction (face : int, func : int, ref : int, mask : int) =
//            let value = V3i(func, ref, mask)
//            match face with
//                | GL_FRONT              -> x.set (&currentStencilFuncFront, value)
//                | GL_BACK               -> x.set (&currentStencilFuncBack, value)
//                | GL_FRONT_AND_BACK     -> x.set2 (&currentStencilFuncFront, &currentStencilFuncBack, value)
//                | _                     -> removed()

//        member x.ShouldSetStencilOperation (face : int, sfail : int, dfail : int, dpass : int) =
//            let value = V3i(sfail, dfail, dpass)
//            match face with
//                | GL_FRONT              -> x.set (&currentStencilOpFront, value)
//                | GL_BACK               -> x.set (&currentStencilOpBack, value)
//                | GL_FRONT_AND_BACK     -> x.set2 (&currentStencilOpFront, &currentStencilOpBack, value)
//                | _                     -> removed()

//        member x.ShouldSetPatchVertices (p : int) =
//            x.set(&currentPatchVertices, p)

//        member x.ShouldSetDepthMask (mask : int) =
//            x.set(&currentDepthMask, mask)

//        member x.ShouldSetStencilMask (mask : int) =
//            x.set(&currentStencilMask, mask)

//        member x.ShouldSetColorMask (index : int, mask : V4i) =
//            match currentColorMasks.TryGetValue index with
//                | (true, o) when o = mask ->
//                    removed()

//                | _ ->
//                    effectiveInstructions <- effectiveInstructions + 1
//                    currentColorMasks.[index] <- mask
//                    true

//        member x.ShouldSetDrawBuffers (all : Set<int>, n : int, ptr : nativeint) =
//            let ptr : nativeptr<int> = NativePtr.ofNativeInt ptr
//            let value = 
//                if n < 0 then 
//                    None
//                else
//                    let s = ptr |> NativePtr.toArray n |> Set.ofArray
//                    if s = all then None else Some s

//            x.set(&currentDrawBuffers, value)


//        member x.ShouldSetFramebuffer(target : int, fbo : int) =
//            match currentFramebuffers.TryGetValue(target) with
//                | (true, o) when o = fbo ->
//                    removed()

//                | _ ->
//                    effectiveInstructions <- effectiveInstructions + 1
//                    currentFramebuffers.[target] <- fbo
//                    true

//        member x.ShouldSetViewport(b : Box2i) =
//            x.set(&currentViewport, b)

//    type GLState with
//        member inline x.bindVertexArray (vao : int) =
//            if x.ShouldSetVertexArray vao then
//                GL.BindVertexArray vao
                
//        member inline x.bindVertexAttributes (contextHandle : nativeptr<nativeint>) (vibh : nativeptr<VertexInputBinding>) =
//            if x.ShouldBindVertexAttributes vibh then
//                GL.HBindVertexAttributes (NativePtr.toNativeInt contextHandle) (NativePtr.toNativeInt vibh)

//        member inline x.bindProgram (prog : int) =
//            if x.ShouldSetProgram prog then
//                GL.BindProgram prog

//        member inline x.activeTexture (slot : int) =
//            if x.ShouldSetActiveTexture slot then
//                GL.ActiveTexture slot

//        member inline x.bindSampler (slot : int) (sampler : int) =
//            if x.ShouldSetSampler(slot, sampler) then
//                GL.BindSampler slot sampler

//        member inline x.bindTexture (target : int) (texture : int) =
//            if x.ShouldSetTexture(target, texture) then
//                GL.BindTexture target texture

//        member inline x.bindBufferBase (target : int) (index : int) (buffer : int) =
//            if x.ShouldSetBuffer(target, index, buffer, 0n, 0n) then
//                GL.BindBufferBase target index buffer

//        member inline x.bindBufferRange (target : int) (index : int) (buffer : int) (offset : nativeint) (size : nativeint) =
//            if x.ShouldSetBuffer(target, index, buffer, offset, size) then
//                GL.BindBufferRange target index buffer offset size

//        member inline x.bindBuffer (target : int) (buffer : int) =
//            if x.ShouldSetBuffer(target, buffer) then
//                GL.BindBuffer target buffer


//        member inline x.enable  (cap : int) =
//            if x.ShouldEnable cap then
//                GL.Enable cap

//        member inline x.disable (cap : int) =
//            if x.ShouldDisable cap then
//                GL.Disable cap

//        member inline x.depthFunc (func : int) =
//            if x.ShouldSetDepthFunc func then
//                GL.DepthFunc func

//        member inline x.cullFace (face : int) =
//            if x.ShouldSetCullFace face then
//                GL.CullFace face

//        member inline x.blendFunction (srcRgb : int) (dstRgb : int) (srcAlpha : int) (dstAlpha : int) =
//            if x.ShouldSetBlendFunction(srcRgb, dstRgb, srcAlpha, dstAlpha) then
//                GL.BlendFuncSeparate srcRgb dstRgb srcAlpha dstAlpha

//        member inline x.blendEquation (front : int) (back : int) =
//            if x.ShouldSetBlendEquation(front, back) then
//                GL.BlendEquationSeparate front back

//        member inline x.blendColor (color : C4f) =
//            if x.ShouldSetBlendColor color then
//                GL.BlendColorF color.R color.G color.B color.A

//        member inline x.polygonMode (face : int) (mode : int) =
//            if x.ShouldSetPolygonMode(face, mode) then 
//                GL.PolygonMode face mode

//        member inline x.stencilFunction (face : int) (func : int) (ref : int) (mask : int) =
//            if x.ShouldSetStencilFunction(face, func, ref, mask) then
//                GL.StencilFuncSeparate face func ref mask

//        member inline x.stencilOperation (face : int) (sfail : int) (dfail : int) (dpass : int) =
//            if x.ShouldSetStencilOperation(face, sfail, dfail, dpass) then
//                GL.StencilOpSeparate face sfail dfail dpass

//        member inline x.patchVertices (p : int) =
//            if x.ShouldSetPatchVertices p then
//                GL.PatchParameter GL_PATCH_VERTICES p

//        member inline x.depthMask (mask : int) =
//            if x.ShouldSetDepthMask mask then
//                GL.DepthMask mask

//        member inline x.stencilMask (mask : int) =
//            if x.ShouldSetStencilMask mask then
//                GL.StencilMask mask


//        member inline x.colorMask (index : int) (mask : V4i) =
//            if x.ShouldSetColorMask(index, mask) then
//                GL.ColorMask index mask.X mask.Y mask.Z mask.W

//        member inline x.drawBuffers (all : Set<int>) (n : int) (ptr : nativeint) =
//            if x.ShouldSetDrawBuffers(all, n, ptr) then
//                GL.DrawBuffers n ptr

//        member inline x.bindFramebuffer (target : int) (fbo : int) =
//            if x.ShouldSetFramebuffer(target, fbo) then
//                GL.BindFramebuffer target fbo

//        member inline x.viewport (target : int) (viewport : Box2i) =
//            if x.ShouldSetViewport(viewport) then
//                OpenTK.Graphics.OpenGL4.GL.Viewport(viewport.Min.X, viewport.Min.Y, viewport.SizeX + 1, viewport.SizeY + 1)


//        member inline x.drawElements (mode : int) (count : int) (indexType : int) (indices : nativeint) =
//            GL.DrawElements mode count indexType indices

//        member inline x.drawArrays (mode : int) (first : int) (count : int) =
//            GL.DrawArrays mode first count

//        member inline x.drawElementsInstanced (mode : int) (count : int) (indexType : int) (indices : nativeint) (primCount : int) =
//            GL.DrawElementsInstanced mode count indexType indices primCount

//        member inline x.drawArraysInstanced (mode : int) (first : int) (count : int) (primCount : int) =
//            GL.DrawArraysInstanced mode first count primCount


//        member inline x.multiDrawArraysIndirect (mode : int) (indirect : nativeint) (count : int) (stride : int) =
//            GL.MultiDrawArraysIndirect mode indirect count stride

//        member inline x.multiDrawElementsIndirect (mode : int) (indexType : int) (indirect : nativeint) (count : int) (stride : int) =
//            GL.MultiDrawElementsIndirect mode indexType indirect count stride

//        member inline x.uniform1fv (l : int) (c : int) (p : nativeint) = GL.Uniform1fv l c p
//        member inline x.uniform2fv (l : int) (c : int) (p : nativeint) = GL.Uniform2fv l c p
//        member inline x.uniform3fv (l : int) (c : int) (p : nativeint) = GL.Uniform3fv l c p
//        member inline x.uniform4fv (l : int) (c : int) (p : nativeint) = GL.Uniform4fv l c p
//        member inline x.uniform1iv (l : int) (c : int) (p : nativeint) = GL.Uniform1iv l c p
//        member inline x.uniform2iv (l : int) (c : int) (p : nativeint) = GL.Uniform2iv l c p
//        member inline x.uniform3iv (l : int) (c : int) (p : nativeint) = GL.Uniform3iv l c p
//        member inline x.uniform4iv (l : int) (c : int) (p : nativeint) = GL.Uniform4iv l c p
//        member inline x.uniformMatrix2fv (l : int) (c : int) (t : int) (p : nativeint) = GL.UniformMatrix2fv l c t p
//        member inline x.uniformMatrix3fv (l : int) (c : int) (t : int) (p : nativeint) = GL.UniformMatrix3fv l c t p
//        member inline x.uniformMatrix4fv (l : int) (c : int) (t : int) (p : nativeint) = GL.UniformMatrix4fv l c t p
//        member inline x.vertexAttrib4f (index : int) (value : V4f) = GL.VertexAttrib4f index value.X value.Y value.Z value.W

//    module Interpreter = 

//        let start(contextHandle : nativeptr<nativeint>) = 
//            let s = GLState(contextHandle)
//            s

//        let stop(s : GLState) =
//            s.Clear()

//        let inline run (contextHandle : nativeptr<nativeint>) (f : GLState -> 'a) =
//            let state = start(contextHandle)
//            try f state
//            finally stop state

//[<AutoOpen>]
//module OpenGLObjectInterpreter =

//    type GLState with

//        member gl.setDepthMask (mask : bool) =
//            gl.depthMask (if mask then 1 else 0)

//        member gl.setStencilMask (mask : bool) =
//            gl.stencilMask (if mask then 0xFFFFFFFF else 0)

//        member gl.setColorMasks (masks : list<V4i>) =
//            let mutable i = 0
//            for m in masks do
//                gl.colorMask i m
//                i <- i + 1

//        member gl.setDepthTestMode (mode : DepthTestMode) =
//            if mode.IsEnabled then
//                gl.enable GL_DEPTH_TEST
//                gl.depthFunc (Translations.toGLComparison mode.Comparison)
//                if mode.Clamp then gl.enable GL_DEPTH_CLAMP
//                else gl.disable GL_DEPTH_CLAMP
//            else
//                gl.disable GL_DEPTH_TEST  
//                gl.disable GL_DEPTH_CLAMP

//        member gl.setFillMode (mode : FillMode) =
//            gl.polygonMode GL_FRONT_AND_BACK (Translations.toGLPolygonMode mode)

//        member gl.setCullMode (mode : CullMode) =
//            if mode = CullMode.None then
//                gl.disable GL_CULL_FACE
//            else
//                gl.enable GL_CULL_FACE
//                gl.cullFace (Translations.toGLFace mode)

//        member gl.setBlendMode (bm : BlendMode) =
//            if bm.Enabled then
//                let src = Translations.toGLFactor bm.SourceFactor
//                let dst = Translations.toGLFactor bm.DestinationFactor
//                let op = Translations.toGLOperation bm.Operation
//                let srcA = Translations.toGLFactor bm.SourceAlphaFactor
//                let dstA = Translations.toGLFactor bm.DestinationAlphaFactor
//                let opA = Translations.toGLOperation bm.AlphaOperation

//                gl.enable GL_BLEND
//                gl.blendFunction src dst srcA dstA
//                gl.blendEquation op opA
//            else
//                gl.disable GL_BLEND

//        member gl.setStencilMode (sm : StencilMode) =
//            if sm.IsEnabled then
//                let cmpFront = Translations.toGLFunction sm.CompareFront.Function
//                let cmpBack= Translations.toGLFunction sm.CompareBack.Function
//                let opFrontSF = Translations.toGLStencilOperation sm.OperationFront.StencilFail
//                let opBackSF = Translations.toGLStencilOperation sm.OperationBack.StencilFail
//                let opFrontDF = Translations.toGLStencilOperation sm.OperationFront.DepthFail
//                let opBackDF = Translations.toGLStencilOperation sm.OperationBack.DepthFail
//                let opFrontP = Translations.toGLStencilOperation sm.OperationFront.DepthPass
//                let opBackP = Translations.toGLStencilOperation sm.OperationBack.DepthPass
//                gl.enable GL_STENCIL_TEST
//                gl.stencilFunction GL_FRONT cmpFront sm.CompareFront.Reference (int sm.CompareFront.Mask)
//                gl.stencilFunction GL_BACK cmpBack sm.CompareBack.Reference (int sm.CompareBack.Mask)
//                gl.stencilOperation GL_FRONT opFrontSF opFrontDF opFrontP
//                gl.stencilOperation GL_BACK opBackSF opBackDF opBackP
//            else
//                gl.disable GL_STENCIL_TEST

//        member gl.bindUniformLocation (l : int) (loc : UniformLocation)=
//            match loc.Type with
//                | Vector(Float, 1) | Float  -> gl.uniform1fv l 1 loc.Data
//                | Vector(Int, 1) | Int      -> gl.uniform1iv l 1 loc.Data
//                | Vector(Float, 2)          -> gl.uniform2fv l 1 loc.Data
//                | Vector(Int, 2)            -> gl.uniform2iv l 1 loc.Data
//                | Vector(Float, 3)          -> gl.uniform3fv l 1 loc.Data
//                | Vector(Int, 3)            -> gl.uniform3iv l 1 loc.Data
//                | Vector(Float, 4)          -> gl.uniform4fv l 1 loc.Data
//                | Vector(Int, 4)            -> gl.uniform4iv l 1 loc.Data
//                | Matrix(Float, 2, 2, true) -> gl.uniformMatrix2fv l 1 1 loc.Data
//                | Matrix(Float, 3, 3, true) -> gl.uniformMatrix3fv l 1 1 loc.Data
//                | Matrix(Float, 4, 4, true) -> gl.uniformMatrix4fv l 1 1 loc.Data
//                | _                         -> failwithf "no uniform-setter for: %A" loc



//        member gl.render (o : PreparedRenderObject) =
//            if (not <| isNull o.Original.IsActive) && Mod.force o.Original.IsActive then // empty objects make null here. Further investigate
//                gl.setDepthMask o.DepthBufferMask
//                gl.setStencilMask o.StencilBufferMask

//                let allBuffers = o.FramebufferSignature.ColorAttachments |> Map.toSeq |> Seq.map fst |> Set.ofSeq

//                match o.ColorBufferMasks with
//                    | Some masks -> gl.setColorMasks masks
//                    | None -> gl.setColorMasks (List.init o.ColorAttachmentCount (fun _ -> V4i.IIII))

//                match o.DrawBuffers with
//                    | Some b ->
//                        gl.drawBuffers allBuffers b.Count (NativePtr.toNativeInt b.Buffers)
//                    | _ ->
//                        gl.drawBuffers allBuffers -1 0n



//                let depthMode = o.Original.DepthTest.GetValue()
//                let fillMode = o.Original.FillMode.GetValue()
//                let cullMode = o.Original.CullMode.GetValue()
//                let blendMode = o.Original.BlendMode.GetValue()
//                let stencilMode = o.Original.StencilMode.GetValue()

//                gl.setDepthTestMode depthMode
//                gl.setFillMode fillMode
//                gl.setCullMode cullMode
//                gl.setBlendMode blendMode
//                gl.setStencilMode stencilMode

//                let program = o.Program.Handle.GetValue()
//                let vibh = o.VertexInputBinding.Handle.GetValue()
//                let indexed = Option.isSome o.IndexBuffer

//                let hasTess = program.HasTessellation

//                let patchSize = o.Original.Mode |> Translations.toPatchCount

//                let mode =
//                    let igMode = o.Original.Mode

//                    if hasTess then 
//                        int OpenGl.Enums.DrawMode.Patches
//                    else 
//                        let realMode = 
//                            match program.SupportedModes with
//                                | Some set ->
//                                    if Set.contains igMode set then 
//                                        igMode
//                                    else failwithf "invalid mode for program: %A (should be in: %A)" igMode set
//                                | None -> 
//                                    igMode

//                        Translations.toGLMode realMode

//                let (indexType, indexSize) =
//                    match o.Original.Indices with
//                        | None -> (0, 0)
//                        | Some view ->

//                            let indexType = view.ElementType
//                            if indexType = typeof<byte> then (int OpenGl.Enums.IndexType.UnsignedByte, 1)
//                            elif indexType = typeof<uint16> then (int OpenGl.Enums.IndexType.UnsignedShort, 2)
//                            elif indexType = typeof<uint32> then (int OpenGl.Enums.IndexType.UnsignedInt, 4)
//                            elif indexType = typeof<sbyte> then (int OpenGl.Enums.IndexType.UnsignedByte, 1)
//                            elif indexType = typeof<int16> then (int OpenGl.Enums.IndexType.UnsignedShort, 2)
//                            elif indexType = typeof<int32> then (int OpenGl.Enums.IndexType.UnsignedInt, 4)
//                            else failwithf "unsupported index type: %A"  indexType

//                gl.bindProgram program.Handle

//                for (id, ub) in Map.toSeq o.UniformBuffers do
//                    let ub = ub.Handle.GetValue()
//                    let b = ub.Buffer
//                    gl.bindBufferRange GL_UNIFORM_BUFFER id b.Handle ub.Offset (nativeint ub.Size)

//                failwith "implement textures"
//                //for (id, (tex, sam)) in Map.toSeq o.Textures do
//                //    let tex = tex.Handle.GetValue()
//                //    let sam = sam.Handle.GetValue()

//                //    let target = Translations.toGLTarget tex.Dimension tex.IsArray tex.Multisamples

//                //    gl.activeTexture (GL_TEXTURE0 + id)
//                //    gl.bindTexture target tex.Handle
//                //    gl.bindSampler id sam.Handle

//                for (id,u) in Map.toSeq o.Uniforms do
//                    let u = u.Handle.GetValue()
//                    gl.bindUniformLocation id u

//                gl.bindVertexAttributes gl.ContextHandle vibh.Pointer

//                if hasTess then
//                    gl.patchVertices patchSize

//                match o.IndirectBuffer with
//                    | Some ib ->
//                        let ib = ib.Handle.GetValue()
//                        let cnt = ib.Count

//                        if cnt > 0 then
//                            let cnt =
//                                let cap = int ib.Buffer.SizeInBytes / sizeof<DrawCallInfo>
//                                if cnt > cap then
//                                    Log.warn "indirect too small"
//                                    cap
//                                else
//                                    cnt

//                            gl.bindBuffer GL_DRAW_INDIRECT_BUFFER ib.Buffer.Handle

//                            if indexed then
//                                gl.multiDrawElementsIndirect mode indexType 0n cnt ib.Stride
//                            else
//                                gl.multiDrawArraysIndirect mode 0n cnt ib.Stride

//                    | None ->
//                        let calls = o.Original.DrawCallInfos.GetValue()
//                        if indexed then
//                            for c in calls do
//                                if c.FaceVertexCount > 0 then
//                                    if c.InstanceCount = 1 then
//                                        gl.drawElements mode c.FaceVertexCount indexType (nativeint (c.FirstIndex * indexSize))
//                                    elif c.InstanceCount > 0 then
//                                        gl.drawElementsInstanced mode c.FaceVertexCount indexType (nativeint (c.FirstIndex * indexSize)) c.InstanceCount
                                    
//                        else
//                            for c in calls do
//                                if c.FaceVertexCount > 0 then
//                                    if c.InstanceCount = 1 then
//                                        gl.drawArrays mode c.FirstIndex c.FaceVertexCount
//                                    elif c.InstanceCount > 0 then
//                                        gl.drawArraysInstanced mode c.FirstIndex c.FaceVertexCount c.InstanceCount

//                #if DEBUG
//                OpenTK.Graphics.OpenGL4.GL.Flush()
//                OpenTK.Graphics.OpenGL4.GL.Finish()
//                OpenTK.Graphics.OpenGL4.GL.Check "Interpreter Render"
//                #endif


//        member gl.render (o : PreparedMultiRenderObject) =
//            for o in o.Children do
//                gl.render o
            

//[<AutoOpen>]
//module ``Interpreter Extensions`` =
//    type private InterpreterProgram(scope : Aardvark.Rendering.GL.Compiler.CompilerInfo, content : seq<PreparedMultiRenderObject>) =
//        inherit AbstractRenderProgram()

//        override x.PerformUpdate(token,t) = ()
//        override x.Dispose() = ()
//        override x.Run(t) = 
//            Interpreter.run scope.contextHandle (fun gl -> 
//                for o in content do gl.render o
//                t.AddInstructions(gl.TotalInstructions, gl.EffectiveInstructions)

//                // epilog
//                gl.setDepthMask true
//                gl.setStencilMask true
//                gl.bindProgram 0
//                gl.bindBuffer GL_DRAW_INDIRECT_BUFFER 0
//                //gl.drawBuffers TODO
//            )
 
//    module RenderProgram =
//        module Interpreter =
//            let runtime scope content = new InterpreterProgram(scope, content) :> IRenderProgram