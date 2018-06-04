namespace Aardvark.Rendering.GL

#nowarn "9"

open System
open System.Linq
open System.Diagnostics
open System.Threading
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Runtime
open Aardvark.Base.Incremental
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL.Compiler
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL




module RenderTasks =
    open System.Collections.Generic




    [<AbstractClass>]
    type AbstractOpenGlRenderTask(manager : ResourceManager, fboSignature : IFramebufferSignature, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) =
        inherit AbstractRenderTask()
        let ctx = manager.Context
        let renderTaskLock = RenderTaskLock()
        let manager = ResourceManager(manager, Some (fboSignature, renderTaskLock), shareTextures, shareBuffers)
        let allBuffers = manager.DrawBufferManager.CreateConfig(fboSignature.ColorAttachments |> Map.toSeq |> Seq.map (snd >> fst) |> Set.ofSeq)
        let structureChanged = Mod.custom ignore
        let runtimeStats = NativePtr.alloc 1

        let mutable isDisposed = false
        let currentContext = Mod.init Unchecked.defaultof<ContextHandle>
        let contextHandle = NativePtr.alloc 1
        do NativePtr.write contextHandle 0n


        let scope =
            { 
                runtimeStats = runtimeStats
                currentContext = currentContext
                contextHandle = contextHandle
                drawBuffers = NativePtr.toNativeInt allBuffers.Buffers
                drawBufferCount = allBuffers.Count 
                usedTextureSlots = ref RefSet.empty
                usedUniformBufferSlots = ref RefSet.empty
                structuralChange = structureChanged
            }

//        let drawBuffers = 
//            fboSignature.ColorAttachments 
//                |> Map.toList 
//                |> List.map (fun (i,_) -> int DrawBuffersEnum.ColorAttachment0 + i |> unbox<DrawBuffersEnum>)
//                |> List.toArray
        
        let beforeRender = new System.Reactive.Subjects.Subject<unit>()
        let afterRender = new System.Reactive.Subjects.Subject<unit>()

        member x.BeforeRender = beforeRender
        member x.AfterRender = afterRender

        member x.StructureChanged() =
            transact (fun () -> structureChanged.MarkOutdated())

        member private x.pushDebugOutput(token : AdaptiveToken) =
            let c = config.GetValue token
            match ContextHandle.Current with
                | Some ctx -> let oldState = ctx.DebugOutputEnabled // get manually tracked state of GL.IsEnabled EnableCap.DebugOutput
                              ctx.DebugOutputEnabled <- c.useDebugOutput
                              oldState
                | None -> Report.Warn("No active context handle in RenderTask.Run")
                          false
            
        member private x.popDebugOutput(token : AdaptiveToken, wasEnabled : bool) =
            match ContextHandle.Current with
                | Some ctx -> ctx.DebugOutputEnabled <- wasEnabled
                | None -> Report.Warn("Still no active context handle in RenderTask.Run")

        member private x.pushFbo (desc : OutputDescription) =
            let fbo = desc.framebuffer |> unbox<Framebuffer>

            let handle = fbo.Handle |> unbox<int> 

            if ExecutionContext.framebuffersSupported then
                GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                GL.Check "could not bind framebuffer"
        
                // DepthMask, ColorMask, StencilMask are by default writing everything when creating an FBO
                
                fbo.Signature.Images |> Map.iter (fun index sem ->
                    match Map.tryFind sem desc.images with
                        | Some img ->
                            let tex = img.texture |> unbox<Texture>
                            GL.BindImageTexture(index, tex.Handle, img.level, false, img.slice, TextureAccess.ReadWrite, unbox (int tex.Format))
                        | None -> 
                            GL.ActiveTexture(int TextureUnit.Texture0 + index |> unbox)
                            GL.BindTexture(TextureTarget.Texture2D, 0)
                    )

            elif handle <> 0 then
                failwithf "cannot render to texture on this OpenGL driver"

            GL.Viewport(desc.viewport.Min.X, desc.viewport.Min.Y, desc.viewport.SizeX + 1, desc.viewport.SizeY + 1)
            GL.Check "could not set viewport"
            

        member private x.popFbo (desc : OutputDescription) =
            if ExecutionContext.framebuffersSupported then
                // execution of RenderObjects might change Color/Depth/StencilMask or DrawBuffers
                // Color/Depth/StencilMask are reset by epilog RenderObject
                // Reset of DrawBuffers is not possible as this is dependent on the FBO?
                // -> Reset DrawBuffers in popFbo
              
                let fbo = desc.framebuffer |> unbox<Framebuffer>
                if fbo.Handle = 0 then
                    GL.DrawBuffer(DrawBufferMode.BackLeft);
                else
                    let drawBuffers = Array.init desc.framebuffer.Signature.ColorAttachments.Count (fun index -> DrawBuffersEnum.ColorAttachment0 + unbox index)
                    GL.DrawBuffers(drawBuffers.Length, drawBuffers);


        abstract member ProcessDeltas : AdaptiveToken * RenderToken -> unit
        abstract member UpdateResources : AdaptiveToken * RenderToken -> unit
        abstract member Perform : AdaptiveToken * RenderToken * Framebuffer * OutputDescription -> unit
        abstract member Release2 : unit -> unit



        member x.Config = config
        member x.Context = ctx
        member x.Scope = scope
        member x.RenderTaskLock = renderTaskLock
        member x.ResourceManager = manager

        override x.PerformUpdate(token, t) =
            use ct = ctx.ResourceLock
            x.ProcessDeltas(token, t)
            x.UpdateResources(token, t)

        override x.Release() =
            if not isDisposed then
                isDisposed <- true
                let dummy = ref 0
                currentContext.Outputs.Consume(dummy) |> ignore
                x.Release2()
        override x.FramebufferSignature = Some fboSignature
        override x.Runtime = Some ctx.Runtime
        override x.Perform(token : AdaptiveToken, t : RenderToken, desc : OutputDescription) =
            
            GL.Check "[RenderTask.Run] Entry"

            let fbo = desc.framebuffer // TODO: fix outputdesc
            if not <| fboSignature.IsAssignableFrom fbo.Signature then
                failwithf "incompatible FramebufferSignature\nexpected: %A but got: %A" fboSignature fbo.Signature

            use __ = ctx.ResourceLock 
            if currentContext.UnsafeCache <> ctx.CurrentContextHandle.Value then
                let intCtx = ctx.CurrentContextHandle.Value.Handle |> unbox<OpenTK.Graphics.IGraphicsContextInternal>
                NativePtr.write contextHandle intCtx.Context.Handle
                transact (fun () -> Mod.change currentContext ctx.CurrentContextHandle.Value)

            let fbo =
                match fbo with
                    | :? Framebuffer as fbo -> fbo
                    | _ -> failwithf "unsupported framebuffer: %A" fbo

            x.ProcessDeltas(token, t)
            x.UpdateResources(token, t)

            let debugState = x.pushDebugOutput(token)
            x.pushFbo desc

            renderTaskLock.Run (fun () ->
                beforeRender.OnNext()
                NativePtr.write runtimeStats V2i.Zero
                let stats = x.Perform(token, t, fbo, desc)
                GL.Check "[RenderTask.Run] Perform"
                afterRender.OnNext()
                let rt = NativePtr.read runtimeStats
                t.AddDrawCalls(rt.X, rt.Y)
            )

            x.popFbo desc
            x.popDebugOutput(token, debugState)
                            
            GL.BindVertexArray 0
            GL.BindBuffer(BufferTarget.DrawIndirectBuffer,0)
            

            

    [<AbstractClass>]
    type AbstractSubTask() =
        static let nop = System.Lazy<unit>(id)

        let programUpdateWatch  = Stopwatch()
        let sortWatch           = Stopwatch()
        let runWatch            = OpenGlStopwatch()

        let fragments = HashSet<RenderFragment>()

        member x.ProgramUpdate (t : RenderToken, f : unit -> 'a) =
            if RenderToken.isEmpty t then
                f()
            else
                programUpdateWatch.Restart()
                let res = f()
                programUpdateWatch.Stop()
                res

        member x.Sorting (t : RenderToken, f : unit -> 'a) =
            if RenderToken.isEmpty t then
                f()
            else
                sortWatch.Restart()
                let res = f()
                sortWatch.Stop()
                res

        member x.Execution (t : RenderToken, f : unit -> 'a) =
            if RenderToken.isEmpty t then
                f()
            else
                runWatch.Restart()
                let res = f()
                runWatch.Stop()
                res

        //member x.Parent = parent

        abstract member Update : AdaptiveToken * RenderToken -> unit
        abstract member Perform : AdaptiveToken * RenderToken -> unit
        abstract member Dispose : unit -> unit
        abstract member Add : PreparedMultiRenderObject -> unit
        abstract member Remove : PreparedMultiRenderObject -> unit

        member x.Add(t : RenderFragment) = 
            fragments.Add t |> ignore

        member x.Remove(t : RenderFragment) = 
            fragments.Remove t |> ignore


        member x.Run(token : AdaptiveToken, t : RenderToken, output : OutputDescription) =

            for task in fragments do
                task.Run(token, t, output)

            x.Perform(token, t)
            if RenderToken.isEmpty t then
                nop
            else
                lazy (
                    t.AddSubTask(
                        MicroTime sortWatch.Elapsed,
                        MicroTime programUpdateWatch.Elapsed,
                        runWatch.ElapsedGPU,
                        runWatch.ElapsedCPU
                    )
                )

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type SortKey = list<int>

    type ProjectionComparer(projections : list<RenderObject -> obj>) =

        let rec getRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as ro -> ro
                | :? MultiRenderObject as ro -> ro.Children |> List.head |> getRenderObject
                | :? PreparedRenderObject as ro -> ro.Original
                | :? PreparedMultiRenderObject as ro -> ro.First.Original
                | :? RenderTaskObject as t -> failwith "needs some info"
                | _ -> failwithf "[ProjectionComparer] unknown RenderObject: %A" ro

        let mutable constantId = 0
        let constantTable = Dict<obj, int>()

        let getConstantId (m : obj) =
            constantTable.GetOrCreate(m, fun m ->
                Interlocked.Increment(&constantId)
            )

        let getId (m : obj) =
            getConstantId m

        let maxKey = Int32.MaxValue :: (projections |> List.map (fun _ -> Int32.MaxValue))

        let keys = ConditionalWeakTable<IRenderObject, SortKey>()
        let project (ro : IRenderObject) =
            let ro = getRenderObject ro

            match keys.TryGetValue ro with
                | (true, key) -> key
                | _ ->
                    if ro.Id < 0 then
                        maxKey
                    else
                        let key = projections |> List.map (fun p -> p ro |> getId)
                        let key = key @ [ro.Id]
                        keys.Add(ro, key)
                        key


        interface IComparer<IRenderObject> with
            member x.Compare(l : IRenderObject, r : IRenderObject) =
                let left = project l
                let right = project r
                compare left right



    type IAssemblerStream with
        
        member x.SetDepthMask(mask : bool) =
            x.BeginCall(1)
            x.PushArg(if mask then 1 else 0)
            x.Call(OpenGl.Pointers.DepthMask)

        member x.SetStencilMask(mask : bool) =
            x.BeginCall(1)
            x.PushArg(if mask then 0b11111111111111111111111111111111 else 0)
            x.Call(OpenGl.Pointers.StencilMask)
        
        member x.SetDrawBuffers(count : int, ptr : nativeint) =
            x.BeginCall(2)
            x.PushArg(ptr)
            x.PushArg(count)
            x.Call(OpenGl.Pointers.DrawBuffers)
            
        member x.SetDepthTest(m : IResource<'a, DepthTestInfo>) =
             x.BeginCall(1)
             x.PushArg(NativePtr.toNativeInt m.Pointer)
             x.Call(OpenGl.Pointers.HSetDepthTest)
             
        member x.SetPolygonMode(m : IResource<'a, int>) =
             x.BeginCall(1)
             x.PushArg(NativePtr.toNativeInt m.Pointer)
             x.Call(OpenGl.Pointers.HSetPolygonMode)
             
        member x.SetCullMode(m : IResource<'a, int>) =
             x.BeginCall(1)
             x.PushArg(NativePtr.toNativeInt m.Pointer)
             x.Call(OpenGl.Pointers.HSetCullFace)
             
        member x.SetBlendMode(m : IResource<'a, GLBlendMode>) =
             x.BeginCall(1)
             x.PushArg(NativePtr.toNativeInt m.Pointer)
             x.Call(OpenGl.Pointers.HSetBlendMode)

        member x.SetStencilMode(m : IResource<'a, GLStencilMode>) =
             x.BeginCall(1)
             x.PushArg(NativePtr.toNativeInt m.Pointer)
             x.Call(OpenGl.Pointers.HSetStencilMode)
             
        member x.UseProgram(m : IResource<Program, int>) =
             x.BeginCall(1)
             x.PushIntArg(NativePtr.toNativeInt m.Pointer)
             x.Call(OpenGl.Pointers.BindProgram)
             
        member x.UseProgram(p : int) =
             x.BeginCall(1)
             x.PushArg(p)
             x.Call(OpenGl.Pointers.BindProgram)

        member x.Enable(v : int) =
             x.BeginCall(1)
             x.PushArg(v)
             x.Call(OpenGl.Pointers.Enable)

        member x.Disable(v : int) =
             x.BeginCall(1)
             x.PushArg(v)
             x.Call(OpenGl.Pointers.Disable)

        member x.BindUniformBufferView(slot : int, view : IResource<UniformBufferView, int>) =
            ()
            let v = view.Handle.GetValue()
            x.BeginCall(5)
            x.PushArg(v.Size)
            x.PushArg(v.Offset)
            x.PushArg(v.Buffer.Handle)
            x.PushArg(slot)
            x.PushArg(int OpenGl.Enums.BufferTarget.UniformBuffer)
            x.Call(OpenGl.Pointers.BindBufferRange)

        member x.BindBuffer(target : int, buffer : int) =
            x.BeginCall(2)
            x.PushArg(buffer)
            x.PushArg(target)
            x.Call(OpenGl.Pointers.BindBuffer)

        member x.SetActiveTexture(slot : int) =
            x.BeginCall(1)
            x.PushArg(int OpenGl.Enums.TextureUnit.Texture0 + slot)
            x.Call(OpenGl.Pointers.ActiveTexture)
            
        member x.BindTexture (texture : IResource<Texture, V2i>) =
            x.BeginCall(2)
            x.PushIntArg(texture.Pointer |> NativePtr.toNativeInt)
            x.PushIntArg(4n + NativePtr.toNativeInt texture.Pointer)
            x.Call(OpenGl.Pointers.BindTexture)

        member x.TexParameteri(target : int, name : TextureParameterName, value : int) =
            x.BeginCall(3)
            x.PushArg(value)
            x.PushArg(int name)
            x.PushArg(target)
            x.Call(OpenGl.Pointers.TexParameteri)

        member x.TexParameterf(target : int, name : TextureParameterName, value : float32) =
            x.BeginCall(3)
            x.PushArg(value)
            x.PushArg(int name)
            x.PushArg(target)
            x.Call(OpenGl.Pointers.TexParameterf)

        member x.BindSampler (slot : int, sampler : IResource<Sampler, int>) =
            if ExecutionContext.samplersSupported then
                x.BeginCall(2)
                x.PushIntArg(NativePtr.toNativeInt sampler.Pointer)
                x.PushArg(slot)
                x.Call(OpenGl.Pointers.BindSampler)
            else
                let s = sampler.Handle.GetValue().Description
                let target = int OpenGl.Enums.TextureTarget.Texture2D
                let unit = int OpenGl.Enums.TextureUnit.Texture0 + slot 
                x.TexParameteri(target, TextureParameterName.TextureWrapS, SamplerStateHelpers.wrapMode s.AddressU)
                x.TexParameteri(target, TextureParameterName.TextureWrapT, SamplerStateHelpers.wrapMode s.AddressV)
                x.TexParameteri(target, TextureParameterName.TextureWrapR, SamplerStateHelpers.wrapMode s.AddressW)
                x.TexParameteri(target, TextureParameterName.TextureMinFilter, SamplerStateHelpers.minFilter s.Filter.Min s.Filter.Mip)
                x.TexParameteri(target, TextureParameterName.TextureMagFilter, SamplerStateHelpers.magFilter s.Filter.Mag)
                x.TexParameterf(target, TextureParameterName.TextureMinLod, s.MinLod)
                x.TexParameterf(target, TextureParameterName.TextureMaxLod, s.MaxLod)

        member x.BindTexturesAndSamplers (textureBinding : IResource<TextureBinding, TextureBinding>) =
            let handle = textureBinding.Update(AdaptiveToken.Top, RenderToken.Empty); textureBinding.Handle.GetValue()
            if handle.count > 0 then
                x.BeginCall(4)
                x.PushArg(handle.textures |> NativePtr.toNativeInt)
                x.PushArg(handle.targets |> NativePtr.toNativeInt)
                x.PushArg(handle.count)
                x.PushArg(handle.offset)
                x.Call(OpenGl.Pointers.HBindTextures)

                x.BeginCall(3)
                x.PushArg(handle.samplers |> NativePtr.toNativeInt)
                x.PushArg(handle.count)
                x.PushArg(handle.offset)
                x.Call(OpenGl.Pointers.HBindSamplers)            

        member x.Uniform1fv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform1fv)

        member x.Uniform1iv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform1iv)

        member x.Uniform2fv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform2fv)

        member x.Uniform2iv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform2iv)

        member x.Uniform3fv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform3fv)

        member x.Uniform3iv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform3iv)
            
        member x.Uniform4fv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform4fv)

        member x.Uniform4iv(location : int, cnt : int, ptr : nativeint) =
            x.BeginCall(3)
            x.PushArg(ptr)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.Uniform4iv)

        member x.UniformMatrix2fv(location : int, cnt : int, transpose : int, ptr : nativeint) =
            x.BeginCall(4)
            x.PushArg(ptr)
            x.PushArg(transpose)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.UniformMatrix2fv)

        member x.UniformMatrix3fv(location : int, cnt : int, transpose : int, ptr : nativeint) =
            x.BeginCall(4)
            x.PushArg(ptr)
            x.PushArg(transpose)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.UniformMatrix3fv)

        member x.UniformMatrix4fv(location : int, cnt : int, transpose : int, ptr : nativeint) =
            x.BeginCall(4)
            x.PushArg(ptr)
            x.PushArg(transpose)
            x.PushArg(cnt)
            x.PushArg(location)
            x.Call(OpenGl.Pointers.UniformMatrix4fv)

        member x.BindUniformLocation(l : int, loc : IResource<UniformLocation, nativeint>) : unit = 
            let loc = loc.Handle.GetValue()

            match loc.Type with
                | Vector(Float, 1) | Float      -> x.Uniform1fv(l, 1, loc.Data)
                | Vector(Int, 1) | Int          -> x.Uniform1iv(l, 1, loc.Data)
                | Vector(Float, 2)              -> x.Uniform2fv(l, 1, loc.Data)
                | Vector(Int, 2)                -> x.Uniform2iv(l, 1, loc.Data)
                | Vector(Float, 3)              -> x.Uniform3fv(l, 1, loc.Data)
                | Vector(Int, 3)                -> x.Uniform3iv(l, 1, loc.Data)
                | Vector(Float, 4)              -> x.Uniform4fv(l, 1, loc.Data)
                | Vector(Int, 4)                -> x.Uniform4iv(l, 1, loc.Data)
                | Matrix(Float, 2, 2, true)     -> x.UniformMatrix2fv(l, 1, 0, loc.Data)
                | Matrix(Float, 3, 3, true)     -> x.UniformMatrix3fv(l, 1, 0, loc.Data)
                | Matrix(Float, 4, 4, true)     -> x.UniformMatrix4fv(l, 1, 0, loc.Data)
                | _                             -> failwithf "no uniform-setter for: %A" loc

            
        member x.BindVertexAttributes(ctx : nativeptr<nativeint>, vao : IResource<VertexInputBindingHandle,_>) =
            let handle = vao.Handle.GetValue() // unchangeable
            x.BeginCall(2)
            x.PushArg(NativePtr.toNativeInt handle.Pointer)
            x.PushArg(NativePtr.toNativeInt ctx)
            x.Call(OpenGl.Pointers.HBindVertexAttributes)

        member x.SetConservativeRaster(r : IResource<_,int>) =
            x.BeginCall(1)
            x.PushArg(NativePtr.toNativeInt r.Pointer)
            x.Call(OpenGl.Pointers.HSetConservativeRaster)

        member x.SetMultisample(r : IResource<_,int>) =
            x.BeginCall(1)
            x.PushArg(NativePtr.toNativeInt r.Pointer)
            x.Call(OpenGl.Pointers.HSetMultisample)
            

        member x.DrawArrays(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, calls : IResource<_,DrawCallInfoList>) =
            x.BeginCall(4)
            x.PushArg(NativePtr.toNativeInt calls.Pointer)
            x.PushArg(NativePtr.toNativeInt beginMode.Pointer)
            x.PushArg(NativePtr.toNativeInt isActive.Pointer)
            x.PushArg(NativePtr.toNativeInt stats)
            x.Call(OpenGl.Pointers.HDrawArrays)

        member x.DrawElements(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, indexType : int, calls : IResource<_,DrawCallInfoList>) =
            x.BeginCall(5)
            x.PushArg(NativePtr.toNativeInt calls.Pointer)
            x.PushArg(indexType)
            x.PushArg(NativePtr.toNativeInt beginMode.Pointer)
            x.PushArg(NativePtr.toNativeInt isActive.Pointer)
            x.PushArg(NativePtr.toNativeInt stats)
            x.Call(OpenGl.Pointers.HDrawElements)

        member x.DrawArraysIndirect(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, indirect : IResource<_, V2i>) =
            x.BeginCall(4)
            x.PushArg(NativePtr.toNativeInt indirect.Pointer)
            x.PushArg(NativePtr.toNativeInt beginMode.Pointer)
            x.PushArg(NativePtr.toNativeInt isActive.Pointer)
            x.PushArg(NativePtr.toNativeInt stats)
            x.Call(OpenGl.Pointers.HDrawArraysIndirect)

        member x.DrawElementsIndirect(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, indexType : int, indirect : IResource<_, V2i>) =
            x.BeginCall(5)
            x.PushArg(NativePtr.toNativeInt indirect.Pointer)
            x.PushArg(indexType)
            x.PushArg(NativePtr.toNativeInt beginMode.Pointer)
            x.PushArg(NativePtr.toNativeInt isActive.Pointer)
            x.PushArg(NativePtr.toNativeInt stats)
            x.Call(OpenGl.Pointers.HDrawElementsIndirect)

        member x.ClearColor(c : IResource<C4f, C4f>) =
            x.BeginCall(4)
            x.PushFloatArg(12n + NativePtr.toNativeInt c.Pointer)
            x.PushFloatArg(8n + NativePtr.toNativeInt c.Pointer)
            x.PushFloatArg(4n + NativePtr.toNativeInt c.Pointer)
            x.PushFloatArg(0n + NativePtr.toNativeInt c.Pointer)
            x.Call(OpenGl.Pointers.ClearColor)
            
        member x.ClearDepth(c : IResource<float, float>) =
            x.BeginCall(1)
            x.PushDoubleArg(NativePtr.toNativeInt c.Pointer)
            x.Call(OpenGl.Pointers.ClearDepth)
            
        member x.ClearStencil(c : IResource<int, int>) =
            x.BeginCall(1)
            x.PushIntArg(NativePtr.toNativeInt c.Pointer)
            x.Call(OpenGl.Pointers.ClearStencil)

        member x.Clear(mask : ClearBufferMask) =
            x.BeginCall(1)
            x.PushArg(mask |> int)
            x.Call(OpenGl.Pointers.Clear)

        member x.Clear(s : CompilerInfo, colors : list<int * IResource<C4f, C4f>>, depth : Option<IResource<float, float>>, stencil : Option<IResource<int, int>>) =
            let mutable mask = ClearBufferMask.None

            match colors with
                | [] ->
                    ()
                | [0, color] ->
                    x.ClearColor(color)
                    mask <- mask ||| ClearBufferMask.ColorBufferBit

                | colors ->
                    let buffers = s.drawBuffers
                    for (i,c) in colors do
                        x.SetDrawBuffers(1, buffers + 4n * nativeint i)
                        x.ClearColor(c)
                        x.Clear(ClearBufferMask.ColorBufferBit)

                    x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)

            match depth with
                | Some d ->
                    x.ClearDepth(d)
                    mask <- mask ||| ClearBufferMask.DepthBufferBit
                | None ->
                    ()

            match stencil with
                | Some s ->
                    x.ClearStencil(s)
                    mask <- mask ||| ClearBufferMask.StencilBufferBit
                | None ->
                    ()

            if mask <> ClearBufferMask.None then
                x.Clear(mask)


        member x.Compile(s : CompilerInfo, prev: PreparedRenderObject, me : PreparedRenderObject) : unit =
            if prev.DepthBufferMask <> me.DepthBufferMask then
                x.SetDepthMask(me.DepthBufferMask)

            if prev.StencilBufferMask <> me.StencilBufferMask then
                x.SetStencilMask(me.StencilBufferMask)

            if prev.DrawBuffers <> me.DrawBuffers then
                match me.DrawBuffers with
                    | None ->
                        x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
                    | Some b ->
                        x.SetDrawBuffers(b.Count, NativePtr.toNativeInt b.Buffers)
                   
            if prev.DepthTestMode <> me.DepthTestMode then
                x.SetDepthTest(me.DepthTestMode)  
                
            if prev.PolygonMode <> me.PolygonMode then
                x.SetPolygonMode(me.PolygonMode)
                
            if prev.CullMode <> me.CullMode then
                x.SetCullMode(me.CullMode)

            if prev.BlendMode <> me.BlendMode then
                x.SetBlendMode(me.BlendMode)

            if prev.StencilMode <> me.StencilMode then
                x.SetStencilMode(me.StencilMode)
            
//            if prev.ConservativeRaster <> me.ConservativeRaster then
//                x.SetConservativeRaster(me.ConservativeRaster)
//            
            if prev.Multisample <> me.Multisample then
                x.SetMultisample(me.Multisample)

            if prev.Program <> me.Program then
                let myProg = me.Program.Handle.GetValue()
                x.UseProgram(me.Program)
                if myProg.WritesPointSize then
                    x.Enable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
                else
                    x.Disable(int OpenTK.Graphics.OpenGL4.EnableCap.ProgramPointSize)
            

            // bind all uniform-buffers (if needed)
            for (id,ub) in Map.toSeq me.UniformBuffers do
                //do! useUniformBufferSlot id
                
                match Map.tryFind id prev.UniformBuffers with
                    | Some old when old = ub -> 
                        // the same UniformBuffer has already been bound
                        ()
                    | _ -> 
                        x.BindUniformBufferView(id, ub)

            // bind all textures/samplers (if needed)

            let binding = me.Textures.Handle.GetValue()
            
            if prev.Textures <> me.Textures then
                x.BindTexturesAndSamplers(me.Textures)
            //let latestSlot = ref prev.LastTextureSlot
            //for (id,(tex,sam)) in Map.toSeq me.Textures do
            //    //do! useTextureSlot id

            //    let texEqual, samEqual =
            //        match Map.tryFind id prev.Textures with
            //            | Some (ot, os) -> (ot = tex), (os = sam)
            //            | _ -> false, false


            //    if id <> !latestSlot then
            //        x.SetActiveTexture(id)
            //        latestSlot := id 

            //    if not texEqual then 
            //        x.BindTexture(tex)

            //    if not samEqual || (not ExecutionContext.samplersSupported && not texEqual) then
            //        x.BindSampler(id, sam)

            // bind all top-level uniforms (if needed)
            for (id,u) in Map.toSeq me.Uniforms do
                match Map.tryFind id prev.Uniforms with
                    | Some old when old = u -> ()
                    | _ -> x.BindUniformLocation(id, u)


            // bind the VAO (if needed)
            if prev.VertexInputBinding <> me.VertexInputBinding then
                x.BindVertexAttributes(s.contextHandle, me.VertexInputBinding)

            // draw the thing
            let isActive = me.IsActive
            let beginMode = me.BeginMode

            match me.IndirectBuffer with
                | Some indirect ->
                    match me.IndexBuffer with
                        | Some (it,_) ->
                            x.DrawElementsIndirect(s.runtimeStats, isActive, beginMode, int it, indirect)
                        | None ->
                            x.DrawArraysIndirect(s.runtimeStats, isActive, beginMode, indirect)

                | None ->
                    match me.IndexBuffer with
                        | Some (it,_) ->
                            x.DrawElements(s.runtimeStats, isActive, beginMode, (int it), me.DrawCallInfos)
                        | None ->
                            x.DrawArrays(s.runtimeStats, isActive, beginMode, me.DrawCallInfos)

        member x.CompileEpilog(s : CompilerInfo, prev : Option<PreparedRenderObject>) =
            //TODO: unbind textures/uniformbuffers
            x.SetDepthMask(true)
            x.SetStencilMask(true)
            x.SetDrawBuffers(s.drawBufferCount, s.drawBuffers)
            x.UseProgram(0)
            x.BindBuffer(int OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer, 0)

        member x.Compile(s : CompilerInfo, l : Option<PreparedRenderObject>, self : PreparedRenderObject) : unit =
            if self <> PreparedRenderObject.empty then
                let l = l |> Option.defaultValue PreparedRenderObject.empty
                x.Compile(s, l, self)
            else
                x.CompileEpilog(s, l)

        member x.Compile(state : CompilerInfo, last : Option<PreparedMultiRenderObject>, self : PreparedMultiRenderObject) =
            let mutable last = 
                match last with
                    | Some l -> Some l.Last
                    | None -> None

            for s in self.Children do
                x.Compile(state, last, s)
                last <- Some s



    type NativeRenderProgram(cmp : IComparer<IRenderObject>, scope : CompilerInfo, content : aset<IRenderObject * PreparedMultiRenderObject>) =
        inherit NativeProgram<IRenderObject * PreparedMultiRenderObject>(ASet.sortWith (fun (l,_) (r,_) -> cmp.Compare(l,r)) content, fun l (_,r) s -> s.Compile(scope,Option.map snd l,r))


        let mutable stats = NativeProgramUpdateStatistics.Zero
        member x.Count = stats.Count

        member private x.UpdateInt(t) =
            let s = x.Update(t)
            if s <> NativeProgramUpdateStatistics.Zero then
                stats <- s


        interface IAdaptiveProgram<unit> with
            member x.Disassemble() = null
            member x.Run(_) = x.Run()
            member x.Update(t) = x.UpdateInt(t); AdaptiveProgramStatistics.Zero
            member x.StartDefragmentation() = Threading.Tasks.Task.FromResult(TimeSpan.Zero)
            member x.AutoDefragmentation
                with get() = false
                and set _ = ()
            member x.FragmentCount = 10
            member x.NativeCallCount = 10
            member x.ProgramSizeInBytes = 10L
            member x.TotalJumpDistanceInBytes = 10L

        interface IRenderProgram with
            member x.Run(t) = 
                x.Run()

            member x.Update(at,rt) = 
                x.UpdateInt(at)
                AdaptiveProgramStatistics.Zero

    type ObjectOrTask =
        | Object of PreparedMultiRenderObject
        | Task of RenderPass * RenderFragment

        interface IDisposable with
            member x.Dispose() =
                match x with
                    | Object o -> o.Dispose()
                    | Task(_,t) -> t.RemoveRef()  

    type StaticOrderSubTask(scope : CompilerInfo, config : IMod<BackendConfiguration>) =
        inherit AbstractSubTask()
        static let empty = new PreparedMultiRenderObject([PreparedRenderObject.empty])
        let objects = CSet.ofList [empty]

        let mutable hasProgram = false
        let mutable currentConfig = BackendConfiguration.Default
        let mutable program : IRenderProgram = Unchecked.defaultof<_>
        let structuralChange = Mod.custom ignore
        let scope = { scope with structuralChange = structuralChange }

        // TODO: add AdaptiveProgram creator not taking a separate key but simply comparing the values
        let objectsWithKeys = objects |> ASet.map (fun o -> (o :> IRenderObject, o))

        let reinit (self : StaticOrderSubTask) (config : BackendConfiguration) =
            // if the config changed or we never compiled a program
            // we need to do something
            if config <> currentConfig || not hasProgram then

                // if we have a program we'll dispose it now
                if hasProgram then program.Dispose()

                // use the config to create a comparer for IRenderObjects
                let comparer =
                    match config.sorting with
                        | RenderObjectSorting.Grouping projections -> 
                            ProjectionComparer(projections) :> IComparer<_>

                        | RenderObjectSorting.Static comparer -> 
                            { new IComparer<_> with 
                                member x.Compare(l, r) =
                                    if l.Id = r.Id then 0
                                    elif l.Id < 0 then -1
                                    elif r.Id < 0 then 1
                                    else comparer.Compare(l,r)
                            }

                        | Arbitrary ->
                            { new IComparer<_> with 
                                member x.Compare(l, r) =
                                    if l.Id < 0 then -1
                                    elif r.Id < 0 then 1
                                    else 0
                            }

                        | RenderObjectSorting.Dynamic create ->
                            failwith "[AbstractRenderTask] dynamic sorting not implemented"

                // create the new program
                let newProgram = 
                    match config.execution, config.redundancy with
                        | ExecutionEngine.Interpreter, _ ->
                            Log.line "using interpreted program"
                            RenderProgram.Interpreter.runtime scope objects

                        | ExecutionEngine.Native, RedundancyRemoval.Static -> 
                            Log.line "using optimized native program"
                            new NativeRenderProgram(comparer, scope, objectsWithKeys) :> IRenderProgram
                            //RenderProgram.Native.optimized scope comparer objectsWithKeys

                        | ExecutionEngine.Native, RedundancyRemoval.None -> 
                            Log.line "using unoptimized native program"
                            RenderProgram.Native.unoptimized scope comparer objectsWithKeys

                        | ExecutionEngine.Managed, RedundancyRemoval.Static -> 
                            Log.line "using optimized managed program"
                            RenderProgram.Managed.optimized scope comparer objectsWithKeys

                        | ExecutionEngine.Managed, RedundancyRemoval.None -> 
                            Log.line "using unoptimized managed program"
                            RenderProgram.Managed.unoptimized scope comparer objectsWithKeys

                        | ExecutionEngine.Debug, RedundancyRemoval.Static -> 
                            Log.line "using optimized debug program"
                            RenderProgram.Debug.optimized scope comparer objectsWithKeys

                        | ExecutionEngine.Debug, RedundancyRemoval.None -> 
                            Log.line "using unoptimized debug program"
                            RenderProgram.Debug.unoptimized scope comparer objectsWithKeys


                        | ExecutionEngine.Unmanaged, RedundancyRemoval.Static -> 
                            Log.line "using optimized unmanaged program"
                            RenderProgram.GLVM.optimized scope comparer objectsWithKeys

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.Runtime -> 
                            Log.line "using runtime-optimized unmanaged program"
                            RenderProgram.GLVM.runtime scope comparer objectsWithKeys

                        | ExecutionEngine.Unmanaged, RedundancyRemoval.None -> 
                            Log.line "using unoptimized unmanaged program"
                            RenderProgram.GLVM.unoptimized scope comparer objectsWithKeys

                        | t ->
                            failwithf "[GL] unsupported backend configuration: %A" t


                // finally we store the current config/ program and set hasProgram to true
                program <- newProgram
                hasProgram <- true
                currentConfig <- config

        override x.Update(token, t) =
            let config = config.GetValue token
            reinit x config

            //TODO
            let programStats = x.ProgramUpdate (t, fun () -> program.Update AdaptiveToken.Top)
            ()
        override x.Perform(token, t) =
            x.Update(token, t) |> ignore

            let stats = x.Execution (t, fun () -> program.Run(t))

            stats
               

        override x.Dispose() =
            if hasProgram then
                hasProgram <- false
                program.Dispose()

                let mutable foo = 0
                (objects :> aset<_>).Content.Outputs.Consume(&foo) |> ignore

                objects.Clear()
        
        override x.Add(o) = 
            transact (fun () -> 
                structuralChange.MarkOutdated()
                objects.Add o |> ignore
            )

        override x.Remove(o) = 
            transact (fun () -> 
                structuralChange.MarkOutdated()
                objects.Remove o |> ignore
            )

                
    [<AllowNullLiteral>]
    type AdaptiveGLVMFragment(obj : PreparedMultiRenderObject, adaptiveCode : IAdaptiveCode<Instruction>) =
        inherit AdaptiveObject()

        let boundingBox : IMod<Box3d> =
            if obj.First.Id < 0 then Mod.constant Box3d.Invalid
            else
                match Ag.tryGetAttributeValue obj.First.Original.AttributeScope "GlobalBoundingBox" with
                    | Success box -> box
                    | _ -> failwith "[GL] could not get BoundingBox for RenderObject"
        let mutable currentBox = Box3d.Invalid

        let mutable prev : AdaptiveGLVMFragment = null
        let mutable next : AdaptiveGLVMFragment = null

        let code = List.toArray adaptiveCode.Content
        let frag = GLVM.vmCreate()
        let blocksWithContent = code |> Array.map (fun content -> (GLVM.vmNewBlock frag, content))

        let blockTable =
            code 
                |> Array.mapi (fun i m ->
                    if m.IsConstant then 
                        None
                    else
                        Some (m :> IAdaptiveObject, blocksWithContent.[i])
                   )
                |> Array.choose id
                |> Dictionary.ofArray

        let getArgs (o : Instruction) =
            o.Arguments |> Array.map (fun arg ->
                match arg with
                    | :? int as i -> nativeint i
                    | :? int64 as i -> nativeint i
                    | :? nativeint as i -> i
                    | :? float32 as f -> BitConverter.ToInt32(BitConverter.GetBytes(f), 0) |> nativeint
                    | :? PtrArgument as p ->
                        match p with
                            | Ptr32 p -> p
                            | Ptr64 p -> p
                    | _ -> failwith "invalid argument"
            )

        let writeBlock (id : int) (instructions : seq<Instruction>) =
            GLVM.vmClearBlock(frag, id)
            for i in instructions do
                match getArgs i with
                    | [| a |] -> GLVM.vmAppend1(frag, id, int i.Operation, a)
                    | [| a; b |] -> GLVM.vmAppend2(frag, id, int i.Operation, a, b)
                    | [| a; b; c |] -> GLVM.vmAppend3(frag, id, int i.Operation, a, b, c)
                    | [| a; b; c; d |] -> GLVM.vmAppend4(frag, id, int i.Operation, a, b, c, d)
                    | [| a; b; c; d; e |] -> GLVM.vmAppend5(frag, id, int i.Operation, a, b, c, d, e)
                    | [| a; b; c; d; e; f |] -> GLVM.vmAppend6(frag, id, int i.Operation, a, b, c, d, e, f)
                    | _ -> failwithf "invalid instruction: %A" i

        let dirtyBlocks = HashSet blocksWithContent
        
        override x.InputChanged (transaction : obj, o : IAdaptiveObject) =
            match blockTable.TryGetValue o with
                | (true, dirty) -> lock dirtyBlocks (fun () -> dirtyBlocks.Add dirty |> ignore)
                | _ -> ()

        member x.Object = obj

        member x.BoundingBox = currentBox

        member x.Update(token : AdaptiveToken, rt : RenderToken) =
            x.EvaluateAlways token (fun token ->
                if x.OutOfDate then
                    let blocks = 
                        lock dirtyBlocks (fun () ->
                            let all = Seq.toList dirtyBlocks
                            dirtyBlocks.Clear()
                            all
                        )

                    for (block, content) in blocks do
                        let c = content.GetValue token
                        writeBlock block c

                    currentBox <- boundingBox.GetValue token

            )

        member x.Handle = frag

        member x.Next
            with get() = next
            and set v = 
                next <- v
                if isNull v then GLVM.vmLink(frag, 0n)
                else GLVM.vmLink(frag, v.Handle)

        member x.Prev
            with get() = prev
            and set v = prev <- v

        member x.Dispose() =
            adaptiveCode.Dispose()
            if not (isNull prev) then prev.Next <- next
            if not (isNull next) then next.Prev <- prev
            GLVM.vmDelete frag

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    type SortedGLVMProgram(parent : CameraSortedSubTask, objects : aset<PreparedMultiRenderObject>, createComparer : Ag.Scope -> IMod<IComparer<PreparedMultiRenderObject>>) =
        inherit AbstractRenderProgram<AdaptiveGLVMFragment>()
        
        static let empty = new PreparedMultiRenderObject([PreparedRenderObject.empty])
        static let mutable initialized = false
        do if not initialized then
            initialized <- true
            GLVM.vmInit()
        
        let fragments = objects |> ASet.mapUse (fun o -> new AdaptiveGLVMFragment(o, RenderProgram.Compiler.compileFull parent.Scope o))
        let fragmentReader = fragments.GetReader()
        let mutable vmStats = VMStats()
        let last = new AdaptiveGLVMFragment(empty, RenderProgram.Compiler.compileFull parent.Scope empty)
        let mutable first : AdaptiveGLVMFragment = last

        let mutable comparer = None

        let mutable disposeCnt = 0

        let getComparer (f : seq<AdaptiveGLVMFragment>) =
            match comparer with
                | Some cmp -> cmp
                | None ->
                    if Seq.isEmpty f then
                        Mod.constant { new IComparer<_> with member x.Compare(a,b) = 0 }
                    else
                        let fst = Seq.head f
                        let c = createComparer fst.Object.Original.AttributeScope
                        comparer <- Some c
                        c


        member private x.sort (token : AdaptiveToken, f : seq<AdaptiveGLVMFragment>) : list<AdaptiveGLVMFragment> =
            let comparer = getComparer f
            let cmp = comparer.GetValue token
            f |> Seq.sortWith (fun a b -> cmp.Compare(a.Object, b.Object)) |> Seq.toList

        override x.Update(token : AdaptiveToken, rt : RenderToken, dirty : HashSet<_>) =
            let deltas = fragmentReader.GetOperations(token)
            for d in deltas do
                match d with
                    | Add(_,f) -> dirty.Add f |> ignore
                    | Rem(_,f) -> dirty.Remove f |> ignore

            for d in dirty do d.Update(token, rt)

            parent.Sorting (rt, fun () ->
                let ordered = x.sort(token, fragmentReader.State)

                let mutable current = null
                for f in ordered do
                    f.Prev <- current
                    if isNull current then first <- f
                    else current.Next <- f
                    current <- f

                if not <| isNull current then current.Next <- last
                else first <- last
                last.Next <- null
            )

        override x.Run(t) =
            if disposeCnt > 0 then
                failwithf "Running disposed glvmprogram"

            vmStats.TotalInstructions <- 0
            vmStats.RemovedInstructions <- 0
            if not (isNull first) then
                GLVM.vmRun(first.Handle, VMMode.RuntimeRedundancyChecks, &vmStats)

            t.AddInstructions(vmStats.TotalInstructions, vmStats.TotalInstructions - vmStats.RemovedInstructions)

        override x.Dispose() =
            if Interlocked.Increment &disposeCnt = 1 then
                last.Dispose()
                fragmentReader.Dispose()    
            else
                Log.warn "double dispose"

    and SortedInterpreterProgram(parent : CameraSortedSubTask, objects : aset<PreparedMultiRenderObject>, createComparer : Ag.Scope -> IMod<IComparer<PreparedMultiRenderObject>>) =
        inherit AbstractRenderProgram()

        let reader = objects.GetReader()
        let mutable arr = null

        let mutable comparer = None
        let mutable activeInstructions = 0
        let mutable totalInstructions = 0

        let getComparer (f : seq<PreparedMultiRenderObject>) =
            match comparer with
                | Some cmp -> cmp
                | None ->
                    if Seq.isEmpty f then
                        Mod.constant { new IComparer<_> with member x.Compare(a,b) = 0 }
                    else
                        let fst = Seq.head f
                        let c = createComparer fst.Original.AttributeScope
                        comparer <- Some c
                        c


        override x.PerformUpdate(token : AdaptiveToken, t : RenderToken) =
            reader.GetOperations(token) |> ignore

            parent.Sorting (t, fun () ->
                let comparer = getComparer reader.State
                let cmp = comparer.GetValue token
                arr <- reader.State |> Seq.sortWith (fun a b -> cmp.Compare(a,b)) |> Seq.toArray
            )

        override x.Run(t) =
            Interpreter.run parent.Scope.contextHandle (fun gl ->
                for a in arr do gl.render a

                t.AddInstructions(gl.TotalInstructions, gl.EffectiveInstructions)
            )

        override x.Dispose() =
            reader.Dispose()

    and CameraSortedSubTask(order : RenderPassOrder, parent : AbstractOpenGlRenderTask) =
        inherit AbstractSubTask()
        do GLVM.vmInit()

        let structuralChange = Mod.custom ignore
        let scope = { parent.Scope with structuralChange = structuralChange }

        let mutable hasCameraView = false
        let mutable cameraView = Mod.constant Trafo3d.Identity
        
        let objects = CSet.empty
        let boundingBoxes = Dictionary<PreparedMultiRenderObject, IMod<Box3d>>()
        let mutable compareToken = AdaptiveToken.Top
        let bb (o : PreparedMultiRenderObject) =
            boundingBoxes.[o].GetValue(compareToken)

        let mutable program = Unchecked.defaultof<IRenderProgram>
        let mutable hasProgram = false
        let mutable currentConfig = BackendConfiguration.Debug

        let createComparer (scope : Ag.Scope) =
            Mod.custom (fun self ->
                let cam = cameraView.GetValue self
                let pos = cam.GetViewPosition()

                match order with
                    | RenderPassOrder.BackToFront ->
                        { new IComparer<PreparedMultiRenderObject> with
                            member x.Compare(l,r) = compare ((bb r).GetMinimalDistanceTo pos) ((bb l).GetMinimalDistanceTo pos)
                        }
                    | _ ->
                        { new IComparer<PreparedMultiRenderObject> with
                            member x.Compare(l,r) = compare ((bb l).GetMinimalDistanceTo pos) ((bb r).GetMinimalDistanceTo pos)
                        }
            )


        let reinit (self : CameraSortedSubTask) (c : BackendConfiguration) =
            if currentConfig <> c || not hasProgram then
                if hasProgram then
                    program.Dispose()

                let newProgram = 
                    match c.execution with
                        | ExecutionEngine.Interpreter -> new SortedInterpreterProgram(self, objects, createComparer) :> IRenderProgram
                        | _ -> new SortedGLVMProgram(self, objects, createComparer) :> IRenderProgram

                program <- newProgram
                hasProgram <- true
                currentConfig <- c

        member x.Scope = scope

        override x.Update(token, t) = 
            compareToken <- token
            let cfg = parent.Config.GetValue token
            reinit x cfg

            let updateStats = x.ProgramUpdate (t, fun () -> program.Update(AdaptiveToken.Top, t))
            ()

        override x.Perform(token, t) =
            compareToken <- token
            x.Update(token, t) |> ignore
            x.Execution (t, fun () -> program.Run(t))



        override x.Dispose() =
            if hasProgram then
                program.Dispose()
                hasProgram <- false

            objects.Clear()
            hasCameraView <- false
            cameraView <- Mod.constant Trafo3d.Identity
        
        override x.Add(o) = 
            if not hasCameraView then
                let o = o.First.Original

                match o.Uniforms.TryGetUniform (o.AttributeScope, Symbol.Create "ViewTrafo") with
                    | Some (:? IMod<Trafo3d> as view) -> 
                        hasCameraView <- true
                        cameraView <- view
                    | _ -> ()

            if o.First.Id < 0 then
                 boundingBoxes.[o] <- Mod.constant Box3d.Invalid
            else
                match Ag.tryGetAttributeValue o.Original.AttributeScope "GlobalBoundingBox" with
                    | Success b -> boundingBoxes.[o] <- b
                    | _ -> failwithf "[GL] could not get bounding-box for RenderObject"

            transact (fun () -> 
                structuralChange.MarkOutdated()
                objects.Add o |> ignore
            )

        override x.Remove(o) =
            boundingBoxes.Remove o |> ignore
            transact (fun () -> 
                structuralChange.MarkOutdated()
                objects.Remove o |> ignore
            )


    type RenderTask(man : ResourceManager, fboSignature : IFramebufferSignature, objects : aset<IRenderObject>, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) as this =
        inherit AbstractOpenGlRenderTask(man, fboSignature, config, shareTextures, shareBuffers)
        
        let ctx = man.Context
        let resources = new Aardvark.Base.Rendering.ResourceInputSet()
        let inputSet = InputSet(this) 
        let resourceUpdateWatch = OpenGlStopwatch()
        let structuralChange = Mod.init ()
        
        let primitivesGenerated = OpenGlQuery(QueryTarget.PrimitivesGenerated)

        //let vaoCache = ResourceCache(None, Some this.RenderTaskLock)

        let add (ro : PreparedRenderObject) = 
            let all = ro.Resources |> Seq.toList
            for r in all do resources.Add(r)

            
            let old = ro.Activation
            ro.Activation <- 
                { new IDisposable with
                    member x.Dispose() =
                        old.Dispose()
                        for r in all do resources.Remove r
                        //callStats.Remove ro
                        ro.Activation <- old
                }

            ro

        let rec prepareRenderObject (ro : IRenderObject) =
            match ro with
                | :? RenderObject as r ->
                    let hooked = this.HookRenderObject r 
                    new PreparedMultiRenderObject([this.ResourceManager.Prepare(fboSignature, hooked) |> add]) |> Object

                | :? PreparedRenderObject as prep ->
                    new PreparedMultiRenderObject([prep |> PreparedRenderObject.clone |> add]) |> Object

                | :? MultiRenderObject as seq ->
                    let all = 
                        seq.Children |> List.collect(fun o -> 
                            match prepareRenderObject o with
                                | Object a -> a.Children
                                | _ -> failwith "no work"
                        )
                    new PreparedMultiRenderObject(all) |> Object

                | :? PreparedMultiRenderObject as seq ->
                    new PreparedMultiRenderObject (seq.Children |> List.map (PreparedRenderObject.clone >> add)) |> Object

                | :? RenderTaskObject as t ->
                    t.Fragment.AddRef()
                    Task(t.Pass, t.Fragment)

                | _ ->
                    failwithf "[RenderTask] unsupported IRenderObject: %A" ro

        let preparedObjects = objects |> ASet.mapUse prepareRenderObject
        let preparedObjectReader = preparedObjects.GetReader()

        let mutable subtasks = Map.empty

        let getSubTask (pass : RenderPass) : AbstractSubTask =
            match Map.tryFind pass subtasks with
                | Some task -> task
                | _ ->
                    let task = 
                        match pass.Order with
                            | RenderPassOrder.Arbitrary ->
                                new StaticOrderSubTask(this.Scope, this.Config) :> AbstractSubTask

                            | order ->
                                new CameraSortedSubTask(order, this) :> AbstractSubTask

                    subtasks <- Map.add pass task subtasks
                    task

        let processDeltas (x : AdaptiveToken) (parent : AbstractOpenGlRenderTask) (t : RenderToken) =
            let deltas = preparedObjectReader.GetOperations x

            if not (HDeltaSet.isEmpty deltas) then
                parent.StructureChanged()

            let mutable added = 0
            let mutable removed = 0
            for d in deltas do 
                match d with
                    | Add(_,Object v) ->
                        let task = getSubTask v.RenderPass
                        added <- added + 1
                        task.Add v
                    | Rem(_,Object v) ->
                        let task = getSubTask v.RenderPass
                        removed <- removed + 1
                        task.Remove v      
                              
                    | Add(_, Task(p, t)) ->
                        let task = getSubTask p
                        task.Add t

                    | Rem(_, Task(p, t)) ->
                        let task = getSubTask p
                        task.Add t


                        
            if added > 0 || removed > 0 then
                Log.line "[GL] RenderObjects: +%d/-%d" added removed
            t.RenderObjectDeltas(added, removed)

        let updateResources (x : AdaptiveToken) (t : RenderToken) =
            if RenderToken.isEmpty t then
                resources.Update(x, t)
            else
                resourceUpdateWatch.Restart()
                resources.Update(x, t)
                resourceUpdateWatch.Stop()

                t.AddResourceUpdate(resourceUpdateWatch.ElapsedCPU, resourceUpdateWatch.ElapsedGPU)


        override x.ProcessDeltas(token, t) =
            processDeltas token x t

        override x.UpdateResources(token,t) =
            updateResources token t

        override x.Perform(token : AdaptiveToken, rt : RenderToken, fbo : Framebuffer, output : OutputDescription) =
            x.ResourceManager.DrawBufferManager.Write(fbo)

            if not RuntimeConfig.SupressGLTimers && RenderToken.isValid rt then
                primitivesGenerated.Restart()

            let mutable runStats = []
            for (_,t) in Map.toSeq subtasks do
                let s = t.Run(token,rt, output)
                runStats <- s::runStats

            if RuntimeConfig.SyncUploadsAndFrames then
                GL.Sync()
            
            if not RuntimeConfig.SupressGLTimers && RenderToken.isValid rt then 
                primitivesGenerated.Stop()
                runStats |> List.iter (fun l -> l.Value)
                rt.AddPrimitiveCount(primitivesGenerated.Value)



        override x.Release2() =
            preparedObjectReader.Dispose()
            resources.Dispose()
            for (_,t) in Map.toSeq subtasks do
                t.Dispose()

            subtasks <- Map.empty

        override x.Use (f : unit -> 'a) =
            lock x (fun () ->
                x.RenderTaskLock.Run (fun () ->
                    lock resources (fun () ->
                        f()
                    )
                )
            )

    type ClearTask(runtime : IRuntime, fboSignature : IFramebufferSignature, color : IMod<list<Option<C4f>>>, depth : IMod<Option<float>>, ctx : Context) =
        inherit AbstractRenderTask()

        override x.PerformUpdate(token, t) = ()
        override x.Perform(token : AdaptiveToken, t : RenderToken, desc : OutputDescription) =
            let fbo = desc.framebuffer
            using ctx.ResourceLock (fun _ ->

                let old = Array.create 4 0
                let mutable oldFbo = 0
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.Viewport, old)
                OpenTK.Graphics.OpenGL.GL.GetInteger(OpenTK.Graphics.OpenGL.GetPName.FramebufferBinding, &oldFbo)

                let handle = fbo.GetHandle null |> unbox<int>

                if ExecutionContext.framebuffersSupported then
                    GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, handle)
                    GL.Check "could not bind framebuffer"
                elif handle <> 0 then
                    failwithf "cannot render to texture on this OpenGL driver"

                GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)
                GL.Check "could not bind framebuffer"

                

                let depthValue = depth.GetValue token
                let colorValues = color.GetValue token
                    
                colorValues |> List.iteri (fun i _ ->
                    GL.ColorMask(i, true, true, true, true)
                )
                GL.DepthMask(true)
                GL.StencilMask(0xFFFFFFFFu)

                match colorValues, depthValue with
                    | [Some c], Some depth ->
                        GL.ClearColor(c.R, c.G, c.B, c.A)
                        GL.ClearDepth(depth)
                        GL.Clear(ClearBufferMask.ColorBufferBit ||| ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                        
                    | [Some c], None ->
                        GL.ClearColor(c.R, c.G, c.B, c.A)
                        GL.Clear(ClearBufferMask.ColorBufferBit)

                    | l, Some depth when List.forall Option.isNone l ->
                        GL.ClearDepth(depth)
                        GL.Clear(ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                    | l, d ->
                            
                        let mutable i = 0
                        for c in l do
                            match c with
                                | Some c ->
                                    GL.DrawBuffer(int DrawBufferMode.ColorAttachment0 + i |> unbox)
                                    GL.ClearColor(c.R, c.G, c.B, c.A)
                                    GL.Clear(ClearBufferMask.ColorBufferBit)
                                | None ->
                                    ()
                            i <- i + 1

                        match d with
                            | Some depth -> 
                                GL.ClearDepth(depth)
                                GL.Clear(ClearBufferMask.DepthBufferBit ||| ClearBufferMask.StencilBufferBit)
                            | None ->
                                ()


                if ExecutionContext.framebuffersSupported then
                    GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)

                GL.Viewport(old.[0], old.[1], old.[2], old.[3])
                GL.Check "could not bind framebuffer"
            )

        override x.Release() =
            color.RemoveOutput x
            depth.RemoveOutput x
        override x.FramebufferSignature = fboSignature |> Some
        override x.Runtime = runtime |> Some

        override x.Use f = lock x f

[<AutoOpen>]
module ``Prepared Commands`` =
    type ICommandRenderTask =
        inherit IRenderTask
        inherit IResource
        abstract member EntryPointer : nativeptr<nativeint>

    [<RequireQualifiedAccess>]
    type PreparedRenderCommand =
        | Render of PreparedMultiRenderObject
        | Call of ICommandRenderTask
        | Custom of IResource<PinnedDelegate, nativeint> * list<IResource>
        | Clear of list<int * IResource<C4f, C4f>> * Option<IResource<float, float>> * Option<IResource<int, int>>
        | IfThenElse of IResource<bool, int> * list<PreparedRenderCommand> * list<PreparedRenderCommand> with
        
        member x.Dispose() =
            match x with
                | PreparedRenderCommand.Render o -> o.Dispose()
                | PreparedRenderCommand.Call t -> ()
                | PreparedRenderCommand.Clear(colors, depth, stencil) ->
                    colors |> List.iter (fun (_,c) -> c.Dispose())
                    depth |> Option.iter (fun o -> o.Dispose())
                    stencil |> Option.iter (fun o -> o.Dispose())
                | PreparedRenderCommand.IfThenElse(cond, i, e) ->
                    cond.Dispose()
                    i |> List.iter (fun c -> c.Dispose())
                    e |> List.iter (fun c -> c.Dispose())

                | PreparedRenderCommand.Custom(c, res) ->
                    c.Dispose()
                    for r in res do r.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        member x.Update(token : AdaptiveToken, rt : RenderToken) =
            match x with
                | PreparedRenderCommand.Render o -> o.Update(token, rt)
                | PreparedRenderCommand.Call t -> t.Update(token, rt)
                | PreparedRenderCommand.Clear(c,d,s) ->
                    c |> List.iter (fun (_,c) -> c.Update(token, rt))
                    d |> Option.iter (fun d -> d.Update(token, rt))
                    s |> Option.iter (fun s -> s.Update(token, rt))
                | PreparedRenderCommand.IfThenElse(cond, i, e) ->
                    cond.Update(token, rt)
                    i |> List.iter (fun c -> c.Update(token, rt))
                    e |> List.iter (fun c -> c.Update(token, rt))

                | PreparedRenderCommand.Custom(c,res) ->
                    c.Update(token, rt)
                    for r in res do r.Update(token, rt)

        member x.Resources =
            match x with
                | PreparedRenderCommand.Render o -> o.Children |> Seq.collect (fun c -> c.Resources)
                | PreparedRenderCommand.Call t -> Seq.singleton (t :> IResource)
                | PreparedRenderCommand.Clear(c,d,s) ->
                    seq {
                        yield! Seq.map (fun (_,c) -> c :> IResource) c

                        match d with
                            | Some d -> yield d :> IResource
                            | _ -> ()

                        match s with
                            | Some s -> yield s :> IResource
                            | _ -> ()
                    }
                | PreparedRenderCommand.IfThenElse(cond, i, e) ->
                    seq {
                        yield cond :> IResource
                        for c in i do yield! c.Resources
                        for c in e do yield! c.Resources
                    }
                | PreparedRenderCommand.Custom (ptr, res) ->
                    seq {
                        yield ptr :> IResource
                        yield! res
                    }
                    

    type private CustomCommand = IRuntime -> AdaptiveToken -> RenderToken -> OutputDescription -> unit
    let private clearColorCache = new ResourceCache<C4f, C4f>(None, None)
    let private clearDepthCache = new ResourceCache<float, float>(None, None)
    let private clearStencilCache = new ResourceCache<int, int>(None, None)
    let private conditionalFlagCache = new ResourceCache<bool, int>(None, None)
    let private customCache = new ResourceCache<PinnedDelegate, nativeint>(None, None)

    type RuntimeValues =
        {
            runtime     : IRuntime
            token       : AdaptiveToken
            renderToken : RenderToken
            output      : OutputDescription
        }


    open System.Runtime.InteropServices

    type private RenderTaskResource(r : IRenderTask) =
        inherit AdaptiveObject()

        member x.RenderTask = r

        member x.Update(token : AdaptiveToken, rt : RenderToken) =
            x.EvaluateIfNeeded token () (fun token ->
                r.Update(token, rt)
            )

        override x.GetHashCode() = r.GetHashCode()
        override x.Equals o =
            match o with
                | :? RenderTaskResource as o -> r = o.RenderTask
                | _ -> false

        interface IResource with
            member x.HandleType = typeof<IRenderTask>
            member x.IsDisposed = false
            member x.Dispose() = ()
            member x.AddRef() = ()
            member x.RemoveRef() = ()
            member x.Info = ResourceInfo.Zero
            member x.Kind = ResourceKind.Unknown
            member x.Update(t,rt) = x.Update(t, rt)

    type private RenderTaskCustomCommand(r : IRenderTask) =
        let res = [ new RenderTaskResource(r) :> IResource ]

        member x.RenderTask = r

        override x.GetHashCode() = r.GetHashCode()
        override x.Equals o =
            match o with
                | :? RenderTaskCustomCommand as o -> r = o.RenderTask
                | _ -> false

        interface ICustomRenderCommand with
            member x.AddRef() = ()
            member x.RemoveRef() = ()
            member x.Run(_,t,rt,o) = r.Run(t,rt,o)
            member x.UsedResources = res

    type ResourceManager with

        member x.PrepareClearColor(c : IMod<C4f>) =
            clearColorCache.GetOrCreate(
                c, 
                {
                    create = fun v -> v
                    update = fun o v -> v
                    delete = fun _ -> ()
                    view = fun h -> h
                    info = fun h -> ResourceInfo.Zero
                    kind = ResourceKind.Unknown
                }
            )

        member x.PrepareClearDepth(c : IMod<float>) =
            clearDepthCache.GetOrCreate(
                c, 
                {
                    create = fun v -> v
                    update = fun o v -> v
                    delete = fun _ -> ()
                    view = fun h -> h
                    info = fun h -> ResourceInfo.Zero
                    kind = ResourceKind.Unknown
                }
            )

        member x.PrepareClearStencil(c : IMod<int>) =
            clearStencilCache.GetOrCreate(
                c, 
                {
                    create = fun v -> v
                    update = fun o v -> v
                    delete = fun _ -> ()
                    view = fun h -> h
                    info = fun h -> ResourceInfo.Zero
                    kind = ResourceKind.Unknown
                }
            )
            
        member x.PrepareConditional(c : IMod<bool>) =
            conditionalFlagCache.GetOrCreate(
                c, 
                {
                    create = fun v -> v
                    update = fun o v -> v
                    delete = fun _ -> ()
                    view = fun h -> if h then 1 else 0
                    info = fun h -> ResourceInfo.Zero
                    kind = ResourceKind.Unknown
                }
            )

        member x.PrepareObject (signature : IFramebufferSignature, o : IRenderObject) =
            match o with
                | :? RenderObject as o -> 
                    let p = x.Prepare(signature, o) 
                    new PreparedMultiRenderObject([p])

                | :? MultiRenderObject as o ->
                    let children = o.Children |> List.collect (fun o -> x.PrepareObject(signature, o).Children)
                    new PreparedMultiRenderObject(children)

                | :? PreparedMultiRenderObject as o ->
                    let children = o.Children |> List.collect (fun o -> x.PrepareObject(signature, o).Children)
                    new PreparedMultiRenderObject(children)

                | :? PreparedRenderObject as o ->
                    for r in o.Resources do r.AddRef()
                    new PreparedMultiRenderObject([o])

                | _ ->
                    failwith ""
            
        member x.PrepareCustom (get : unit -> RuntimeValues, f : ICustomRenderCommand) =
            
            let create (cmd : ICustomRenderCommand) = 
                let run() = 
                    let values = get()
                    cmd.Run(values.runtime, values.token, values.renderToken, values.output)

                Marshal.PinDelegate(Action(run))

            let update (o : PinnedDelegate) (v : ICustomRenderCommand) = 
                o.Dispose()
                create v

            let view (o : PinnedDelegate) = 
                o.Pointer

            let delete (o : PinnedDelegate) = 
                o.Dispose()

            customCache.GetOrCreate(
                Mod.constant f,  [get :> obj],
                {
                    create = create
                    update = update
                    delete = delete
                    view = view
                    info = fun h -> ResourceInfo.Zero
                    kind = ResourceKind.Unknown
                }
            )

        member x.PrepareCommand (signature : IFramebufferSignature, runtimeValues : unit -> RuntimeValues, cmd : RenderCommand) : PreparedRenderCommand =
            match cmd with
                | RenderCommand.RenderC o -> 
                    x.PrepareObject(signature, o) |> PreparedRenderCommand.Render

                | RenderCommand.ClearC(c,d,s) ->
                    let colors = 
                        signature.ColorAttachments |> Map.toList |> List.choose (fun (i,(s,_)) ->
                            match Map.tryFind s c with
                                | Some color -> Some (i, x.PrepareClearColor color)
                                | _ -> None
                        )

                    let d = d |> Option.map x.PrepareClearDepth
                    let s = s |> Option.map x.PrepareClearStencil
                    PreparedRenderCommand.Clear(colors, d, s)

                | RenderCommand.CallC t ->
                    match t with
                        | :? ICommandRenderTask as t ->
                            PreparedRenderCommand.Call(t)

                        | _ ->
                            let ptr = 
                                x.PrepareCustom(runtimeValues, RenderTaskCustomCommand(t))

                            let resource = new RenderTaskResource(t)
                            PreparedRenderCommand.Custom(ptr, [resource :> IResource])

                | RenderCommand.IfThenElseC(cond, ifTrue, ifFalse) ->
                    let ifTrue = ifTrue |> List.map (fun c -> x.PrepareCommand(signature, runtimeValues, c))
                    let ifFalse = ifFalse |> List.map (fun c -> x.PrepareCommand(signature, runtimeValues, c))
                    let active = x.PrepareConditional cond
                    PreparedRenderCommand.IfThenElse(active, ifTrue, ifFalse)

                | RenderCommand.CustomC f ->
                    let ptr = x.PrepareCustom(runtimeValues, f)
                    PreparedRenderCommand.Custom(ptr, [])
                    


[<AutoOpen>]
module ``Command Tasks`` =
    open RenderTasks

    type IAssemblerStream with
        member x.Compile(state : CompilerInfo, l : Option<PreparedRenderCommand>, r : PreparedRenderCommand) =
            match l, r with
                | Some (PreparedRenderCommand.Render p), PreparedRenderCommand.Render o -> 
                    x.Compile(state, Some p, o)

                | _, PreparedRenderCommand.Render o ->  
                    let o = unbox<PreparedMultiRenderObject> o
                    x.Compile(state, None, o)

                | _, PreparedRenderCommand.Clear(c,d,s) -> 
                    x.Clear(state, c, d, s)

                | _, PreparedRenderCommand.Call t ->
                    x.BeginCall(0)
                    x.CallIndirect(t.EntryPointer)

                | _, PreparedRenderCommand.IfThenElse(cond, ifTrue, ifFalse) ->
                    match ifTrue, ifFalse with
                        | [], [] -> ()
                        | cmd, [] ->
                            let lEnd = x.NewLabel()
                            x.Cmp(NativePtr.toNativeInt cond.Pointer, 0)
                            x.Jump(JumpCondition.Equal, lEnd)
                            for c in cmd do
                                x.Compile(state, l, c)
                            x.Mark(lEnd)
                        | [], cmd ->
                            let lEnd = x.NewLabel()
                            x.Cmp(NativePtr.toNativeInt cond.Pointer, 0)
                            x.Jump(JumpCondition.NotEqual, lEnd)
                            for c in cmd do
                                x.Compile(state, l, c)
                            x.Mark(lEnd)

                        | i, e ->
                            let lEnd = x.NewLabel()
                            let lFalse = x.NewLabel()
                            x.Cmp(NativePtr.toNativeInt cond.Pointer, 0)
                            x.Jump(JumpCondition.Equal, lFalse)
                            for c in i do x.Compile(state, l, c)
                            x.Jump(lEnd)
                            x.Mark(lFalse)
                            for c in e do x.Compile(state, l, c)
                            x.Mark(lEnd)

                | _, PreparedRenderCommand.Custom(ptr,_) ->
                    x.BeginCall(0)
                    x.CallIndirect(ptr.Pointer)


    [<AbstractClass>]
    type AbstractCommandSubTask() =
        static let nop = System.Lazy<unit>(id)

        let programUpdateWatch  = Stopwatch()
        let sortWatch           = Stopwatch()
        let runWatch            = OpenGlStopwatch()


        member x.ProgramUpdate (t : RenderToken, f : unit -> 'a) =
            if RenderToken.isEmpty t then
                f()
            else
                programUpdateWatch.Restart()
                let res = f()
                programUpdateWatch.Stop()
                res

        member x.Sorting (t : RenderToken, f : unit -> 'a) =
            if RenderToken.isEmpty t then
                f()
            else
                sortWatch.Restart()
                let res = f()
                sortWatch.Stop()
                res

        member x.Execution (t : RenderToken, f : unit -> 'a) =
            if RenderToken.isEmpty t then
                f()
            else
                runWatch.Restart()
                let res = f()
                runWatch.Stop()
                res

        //member x.Parent = parent

        abstract member Update : AdaptiveToken * RenderToken -> unit
        abstract member Perform : AdaptiveToken * RenderToken -> unit
        abstract member Dispose : unit -> unit
        abstract member Set : Index * PreparedRenderCommand -> unit
        abstract member Remove : Index -> unit


        member x.Run(token : AdaptiveToken, t : RenderToken, output : OutputDescription) =

            x.Perform(token, t)
            if RenderToken.isEmpty t then
                nop
            else
                lazy (
                    t.AddSubTask(
                        MicroTime sortWatch.Elapsed,
                        MicroTime programUpdateWatch.Elapsed,
                        runWatch.ElapsedGPU,
                        runWatch.ElapsedCPU
                    )
                )

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    and CommandSubTask(scope : CompilerInfo, config : IMod<BackendConfiguration>) =
        inherit AbstractCommandSubTask()
        static let empty = new PreparedMultiRenderObject([PreparedRenderObject.empty])
        let objects = clist [PreparedRenderCommand.Render empty]

        let mutable hasProgram = false
        let mutable currentConfig = BackendConfiguration.Default
        let mutable program : NativeProgram<PreparedRenderCommand> = Unchecked.defaultof<_>
        let structuralChange = Mod.custom ignore
        let scope = { scope with structuralChange = structuralChange }


        static let toIndexedASet (l : alist<'a>) =
            ASet.create (fun scope ->
                let r = l.GetReader()
                {
                    new AbstractReader<hdeltaset<Index * 'a>>(scope, HDeltaSet.monoid) with
                        member x.Compute(t) =
                            let state = r.State
                            let ops = r.GetOperations t

                            ops |> PDeltaList.toSeq 
                                |> Seq.collect (fun (i,op) -> 
                                    match op with
                                        | Set v ->
                                            match PList.tryGet i state with
                                                | Some o -> [ Rem(i,o); Add(i,v) ]
                                                | None -> [Add(i,v)]
                                        | Remove ->
                                            match PList.tryGet i state with
                                                | Some o -> [ Rem(i,o) ]
                                                | None -> []
                                )
                                |> HDeltaSet.ofSeq


                        member x.Release() =
                            r.Dispose()
                }
            )


        // TODO: add AdaptiveProgram creator not taking a separate key but simply comparing the values
        let objectsWithKeys = objects |> toIndexedASet //|> ASet.map (fun o -> (o :> IRenderObject, o))

        let reinit (self : CommandSubTask) (config : BackendConfiguration) =
            // if the config changed or we never compiled a program
            // we need to do something
            if config <> currentConfig || not hasProgram then

                // if we have a program we'll dispose it now
                if hasProgram then program.Dispose()

                // use the config to create a comparer for IRenderObjects
                let comparer = Comparer<Index>.Default
                 

                // create the new program
                let newProgram = 
                    match config.execution, config.redundancy with
                        | ExecutionEngine.Native, RedundancyRemoval.Static -> 
                            Log.line "using optimized native program"

                            let compile (l : Option<PreparedRenderCommand>) (r : PreparedRenderCommand) (s : IAssemblerStream) =
                                s.Compile(scope, l, r)

                            NativeProgram.differential compile objects
                            //RenderProgram.Native.optimizedCommand scope comparer objectsWithKeys

                        | t ->
                            failwithf "[GL] unsupported backend configuration: %A" t


                // finally we store the current config/ program and set hasProgram to true
                program <- newProgram
                hasProgram <- true
                currentConfig <- config

        member x.EntryPointer =
            let config = config.GetValue AdaptiveToken.Top
            reinit x config
            program.EntryPointer

        member x.UpdateForCall(token, t) =
            let config = config.GetValue token
            reinit x config
            let programStats = x.ProgramUpdate (t, fun () -> program.Update AdaptiveToken.Top)

            let intCtx = ContextHandle.Current.Value.Handle |> unbox<OpenTK.Graphics.IGraphicsContextInternal>
            NativePtr.write scope.contextHandle intCtx.Context.Handle

        override x.Update(token, t) =
            let config = config.GetValue token
            reinit x config
            let programStats = x.ProgramUpdate (t, fun () -> program.Update AdaptiveToken.Top)
            ()

        override x.Perform(token, t) =
            x.Update(token, t) |> ignore
            let stats = x.Execution (t, fun () -> program.Run())
            stats
               

        override x.Dispose() =
            if hasProgram then
                hasProgram <- false
                program.Dispose()

                let mutable foo = 0
                (objects :> alist<_>).Content.Outputs.Consume(&foo) |> ignore

                objects.Clear()
        
        override x.Set(i : Index, o : PreparedRenderCommand) = 
            transact (fun () -> 
                structuralChange.MarkOutdated()
                objects.[i] <- o
            )

        override x.Remove(i) = 
            transact (fun () -> 
                structuralChange.MarkOutdated()
                objects.Remove i |> ignore
            )
 
    and CommandRenderTask(man : ResourceManager, fboSignature : IFramebufferSignature, commands : alist<RenderCommand>, config : IMod<BackendConfiguration>, shareTextures : bool, shareBuffers : bool) as this =
        inherit AbstractOpenGlRenderTask(man, fboSignature, config, shareTextures, shareBuffers)
      
        let resourceUpdateWatch = OpenGlStopwatch()
        let primitivesGenerated = OpenGlQuery(QueryTarget.PrimitivesGenerated)
        
        let ctx = man.Context
        let resources = new Aardvark.Base.Rendering.ResourceInputSet()
        let commandReader = commands.GetReader()


        let mutable runtimeValues =
            {
                runtime = man.Context.Runtime
                token = AdaptiveToken.Top
                renderToken = RenderToken.Empty
                output = Unchecked.defaultof<_>
            }

        let getRuntimeValues() = runtimeValues

        let rec add (cmd : PreparedRenderCommand) =
            let all = cmd.Resources |> Seq.toList
            for r in all do resources.Add(r)


        let rec rem (cmd : PreparedRenderCommand) =
            let all = cmd.Resources |> Seq.toList
            for r in all do resources.Remove(r)
//            match cmd with
//                | PreparedRenderCommand.Render o ->
//                    for ro in o.Children do
//                        let all = ro.Resources |> Seq.toList
//                        for r in all do resources.Remove(r)
//
//                | PreparedRenderCommand.Call t ->
//                    resources.Remove t
//
//                | PreparedRenderCommand.Clear(c,d,s) ->
//                    c |> List.iter (fun (_,c) -> resources.Remove c)
//                    d |> Option.iter (fun d -> resources.Remove d)
//                    s |> Option.iter (fun s -> resources.Remove s)
//
//                | PreparedRenderCommand.Conditional(cond, cmd) ->
//                    resources.Remove cond
//                    rem cmd
            
        let rec hookObject (o : IRenderObject) =
            match o with
                | :? RenderObject as o -> this.HookRenderObject(o) :> IRenderObject
                | :? MultiRenderObject as o -> o.Children |> List.map hookObject |> MultiRenderObject :> IRenderObject
                | _ ->  o

        let rec hook (o : RenderCommand) =
            match o with
                | RenderCommand.ClearC _ -> o
                | RenderCommand.CallC _ -> o
                | RenderCommand.CustomC _ -> o

                | RenderCommand.RenderC(o) ->
                    RenderCommand.RenderC(hookObject o)

                | RenderCommand.IfThenElseC(cond, i, e) ->
                    RenderCommand.IfThenElseC(cond, i |> List.map hook, e |> List.map hook)


        let prepare (o : RenderCommand) =
            let o = hook o            
            let res = this.ResourceManager.PrepareCommand(fboSignature, getRuntimeValues, o)
            add res
            res

        let cache = Cache(prepare)


        let subtask = new CommandSubTask(this.Scope, this.Config)


        let processDeltas (x : AdaptiveToken) (parent : AbstractOpenGlRenderTask) (t : RenderToken) =
            let oldState = commandReader.State
            let deltas = commandReader.GetOperations x

            if not (PDeltaList.isEmpty deltas) then
                parent.StructureChanged()

            let mutable added = 0
            let mutable removed = 0

            let dead = HashSet<RenderCommand>()

            let preparedDeltas =
                deltas |> PDeltaList.map (fun i op ->
                    match op with
                        | Remove -> 
                            match PList.tryGet i oldState with
                                | Some oldCommand ->
                                    dead.Add oldCommand |> ignore
                                | None ->
                                    ()
                            Remove
                        | Set cmd ->
                            cache.Invoke cmd |> Set
                )

            for (i, op) in PDeltaList.toSeq preparedDeltas do
                match op with
                    | Set v -> subtask.Set(i, v)
                    | Remove -> subtask.Remove i

            for d in dead do
                let (deleted, po) = cache.RevokeAndGetDeleted(d)
                if deleted then 
                    rem po
                    po.Dispose()

        let updateResources (x : AdaptiveToken) (t : RenderToken) =
            if RenderToken.isEmpty t then
                resources.Update(x, t)
            else
                resourceUpdateWatch.Restart()
                resources.Update(x, t)
                resourceUpdateWatch.Stop()

                t.AddResourceUpdate(resourceUpdateWatch.ElapsedCPU, resourceUpdateWatch.ElapsedGPU)

        interface IResource with
            member x.HandleType = typeof<ICommandRenderTask>
            member x.AddRef() = ()
            member x.RemoveRef() = ()
            member x.Update(t,rt) = 
                x.Update(t, rt)
                subtask.UpdateForCall(t, rt)

            member x.Info = ResourceInfo.Zero
            member x.IsDisposed = false
            member x.Kind = ResourceKind.Unknown

        interface ICommandRenderTask with
            member x.EntryPointer = subtask.EntryPointer

        override x.ProcessDeltas(token, t) =
            processDeltas token x t

        override x.UpdateResources(token,t) =
            updateResources token t

        override x.Perform(token : AdaptiveToken, rt : RenderToken, fbo : Framebuffer, output : OutputDescription) =

            runtimeValues <-
                { runtimeValues with
                    token = token
                    renderToken = rt
                    output = output
                }

            x.ResourceManager.DrawBufferManager.Write(fbo)

            if not RuntimeConfig.SupressGLTimers && RenderToken.isValid rt then
                primitivesGenerated.Restart()

            let mutable runStats = []
            let s = subtask.Run(token,rt, output)
            runStats <- s::runStats

            if RuntimeConfig.SyncUploadsAndFrames then
                GL.Sync()
            
            if not RuntimeConfig.SupressGLTimers && RenderToken.isValid rt then 
                primitivesGenerated.Stop()
                runStats |> List.iter (fun l -> l.Value)
                rt.AddPrimitiveCount(primitivesGenerated.Value)

        override x.Release2() =
            commandReader.Dispose()
            resources.Dispose()
            subtask.Dispose()

        override x.Use (f : unit -> 'a) =
            lock x (fun () ->
                x.RenderTaskLock.Run (fun () ->
                    lock resources (fun () ->
                        f()
                    )
                )
            )
  