namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.ShaderReflection
open Aardvark.Base.Rendering
open Aardvark.Base.Runtime
open Microsoft.FSharp.NativeInterop
open OpenTK
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module GLAssemblerExtensions =

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
            let v = view.Handle.GetValue()
            x.BeginCall(5)
            x.PushArg(v.Size)
            x.PushArg(v.Offset)
            x.PushArg(v.Buffer.Handle)
            x.PushArg(slot)
            x.PushArg(int OpenGl.Enums.BufferTarget.UniformBuffer)
            x.Call(OpenGl.Pointers.BindBufferRange)

        member x.BindStorageBuffer(slot : int, view : IResource<Buffer, int>) =
            x.BeginCall(3)
            x.PushIntArg(NativePtr.toNativeInt view.Pointer)
            x.PushArg(slot)
            x.PushArg(int OpenGl.Enums.BufferTarget.ShaderStorageBuffer)
            x.Call(OpenGl.Pointers.BindBufferBase)

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

            
        member x.BindVertexAttributes(ctx : nativeptr<nativeint>, handle : VertexInputBindingHandle) =
            x.BeginCall(2)
            x.PushArg(NativePtr.toNativeInt handle.Pointer)
            x.PushArg(NativePtr.toNativeInt ctx)
            x.Call(OpenGl.Pointers.HBindVertexAttributes)

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
            
        member x.DrawArraysIndirect(stats : nativeptr<V2i>, isActive : nativeptr<int>, beginMode : nativeptr<GLBeginMode>, indirect : nativeptr<V2i>) =
            x.BeginCall(4)
            x.PushArg(NativePtr.toNativeInt indirect)
            x.PushArg(NativePtr.toNativeInt beginMode)
            x.PushArg(NativePtr.toNativeInt isActive)
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
            
        member x.DrawElementsIndirect(stats : nativeptr<V2i>, isActive : nativeptr<int>, beginMode : nativeptr<GLBeginMode>, indexType : int, indirect : nativeptr<V2i>) =
            x.BeginCall(5)
            x.PushArg(NativePtr.toNativeInt indirect)
            x.PushArg(indexType)
            x.PushArg(NativePtr.toNativeInt beginMode)
            x.PushArg(NativePtr.toNativeInt isActive)
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


type CompilerInfo =
    {
        //stats : ref<FrameStatistics>
        contextHandle : nativeptr<nativeint>
        runtimeStats : nativeptr<V2i>
        currentContext : IMod<ContextHandle>
        drawBuffers : nativeint
        drawBufferCount : int
        
        structuralChange        : IMod<unit>
        usedTextureSlots        : ref<RefSet<int>>
        usedUniformBufferSlots  : ref<RefSet<int>>

        task                    : IRenderTask

    }
