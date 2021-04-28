namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.Rendering.ShaderReflection
open Aardvark.Base.Runtime
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL
open FSharp.Data.Traceable

#nowarn "9"

type ICommandStream =
    abstract member GenQueries : count : int * queries : nativeptr<int> -> unit
    abstract member DeleteQueries : count : int * queries : nativeptr<int> -> unit
    abstract member BeginQuery : target : QueryTarget * query : nativeptr<int> -> unit
    abstract member BeginQuery : target : QueryTarget * query : int -> unit
    abstract member EndQuery : target : QueryTarget -> unit
    abstract member GetQueryObject<'a when 'a : unmanaged> : query : nativeptr<int> * param : GetQueryObjectParam * ptr : nativeptr<'a> -> unit
    abstract member GetQueryObject<'a when 'a : unmanaged> : query : int * param : GetQueryObjectParam * ptr : nativeptr<'a> -> unit
    abstract member QueryCounter : target : QueryCounterTarget * id : int -> unit
    abstract member QueryCounter : target : QueryCounterTarget * id : nativeptr<int> -> unit

    abstract member Get<'a when 'a : unmanaged> : pname : GetPName * ptr : nativeptr<'a> -> unit
    abstract member Get<'a when 'a : unmanaged> : pname : GetIndexedPName * index : int * ptr : nativeptr<'a> -> unit

    abstract member SetDrawBuffers : count : int * ptr : nativeint -> unit

    abstract member SetBlendColor : m : nativeptr<C4f> -> unit
    abstract member SetBlendModes : count : int * ptr : nativeint -> unit
    abstract member SetColorMasks : count : int * ptr : nativeint -> unit
    abstract member SetColorMask : bool * bool * bool * bool -> unit

    abstract member SetDepthTest : m : nativeptr<int> -> unit
    abstract member SetDepthBias : m : nativeptr<DepthBiasInfo> -> unit
    abstract member SetDepthMask : m : bool -> unit
    abstract member SetDepthMask : m : nativeptr<int> -> unit
    abstract member SetDepthClamp : m : nativeptr<int> -> unit

    abstract member SetStencilModes : front : nativeptr<GLStencilMode> * back : nativeptr<GLStencilMode> -> unit
    abstract member SetStencilMask : f : StencilFace * m : nativeptr<uint32> -> unit
    abstract member SetStencilMask : m : bool -> unit

    abstract member SetPolygonMode : m : nativeptr<int> -> unit
    abstract member SetCullMode : m : nativeptr<int> -> unit
    abstract member SetFrontFace : m : nativeptr<int> -> unit
    abstract member SetConservativeRaster : r : nativeptr<int> -> unit
    abstract member SetMultisample : r : nativeptr<int> -> unit

    abstract member UseProgram : p : int -> unit
    abstract member UseProgram : m : nativeptr<int> -> unit
    abstract member DispatchCompute : gx : int * gy : int * gz : int -> unit
    abstract member DispatchCompute : groups : nativeptr<V3i> -> unit

    abstract member MemoryBarrier : MemoryBarrierFlags -> unit

    abstract member Enable : v : int -> unit
    abstract member Disable : v : int -> unit
    abstract member Enable : v : nativeptr<int> -> unit
    abstract member Disable : v : nativeptr<int> -> unit

    abstract member BindBuffer : target : int * buffer : int -> unit
    abstract member BindBuffer : target : int * buffer : nativeptr<int> -> unit
    abstract member BindBufferBase : target : BufferRangeTarget * slot : int * b : int -> unit
    abstract member BindBufferBase : target : BufferRangeTarget * slot : int * b : nativeptr<int> -> unit
    abstract member BindBufferRange : target : BufferRangeTarget * slot : int * b : int * offset : nativeint * size : nativeint -> unit
    abstract member BindBufferRange : target : BufferRangeTarget * slot : int * b : nativeptr<int> * offset : nativeptr<nativeint> * size : nativeptr<nativeint> -> unit

    abstract member BindBufferRangeFixed : target : BufferRangeTarget * slot : int * b : nativeptr<int> * offset : nativeint * size : nativeint -> unit

    abstract member NamedBufferData : buffer : int * size : nativeint * data : nativeint * usage : OpenTK.Graphics.OpenGL4.BufferUsageHint -> unit
    abstract member NamedBufferSubData : buffer : int * offset : nativeint * size : nativeint * data : nativeint -> unit


    abstract member SetActiveTexture : slot : int -> unit
    abstract member BindTexture : target : TextureTarget * t : int -> unit
    abstract member BindTexture : target : nativeptr<TextureTarget> * t : nativeptr<int> -> unit
    abstract member TexParameteri : target : int * name : TextureParameterName * value : int -> unit
    abstract member TexParameterf : target : int * name : TextureParameterName * value : float32 -> unit
    abstract member BindSampler  : slot : int * sampler : int -> unit
    abstract member BindSampler  : slot : int * sampler : nativeptr<int> -> unit
    abstract member BindTexturesAndSamplers  : textureBinding : TextureBinding -> unit

    abstract member BindImageTexture : unit : int * texture : int * level : int * layered : bool * layer : int * access : TextureAccess * format : TextureFormat -> unit
    abstract member Uniform1fv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform1iv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform2fv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform2iv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform3fv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform3iv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform4fv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member Uniform4iv : location : int * cnt : int * ptr : nativeint -> unit
    abstract member UniformMatrix2fv : location : int * cnt : int * transpose : int * ptr : nativeint -> unit
    abstract member UniformMatrix3fv : location : int * cnt : int * transpose : int * ptr : nativeint -> unit
    abstract member UniformMatrix4fv : location : int * cnt : int * transpose : int * ptr : nativeint -> unit

    abstract member BindVertexAttributes : ctx : nativeptr<nativeint> * handle : VertexInputBindingHandle -> unit

    abstract member DrawArrays : stats : nativeptr<V2i> * isActive : nativeptr<int> * beginMode : nativeptr<GLBeginMode> * calls : nativeptr<DrawCallInfoList> -> unit
    abstract member DrawElements : stats : nativeptr<V2i> * isActive : nativeptr<int> * beginMode : nativeptr<GLBeginMode> * indexType : int * calls : nativeptr<DrawCallInfoList> -> unit
    abstract member DrawArraysIndirect : stats : nativeptr<V2i> * isActive : nativeptr<int> * beginMode : nativeptr<GLBeginMode> * indirect : nativeptr<IndirectDrawArgs> -> unit
    abstract member DrawElementsIndirect : stats : nativeptr<V2i> * isActive : nativeptr<int> * beginMode : nativeptr<GLBeginMode> * indexType : int * indirect : nativeptr<IndirectDrawArgs> -> unit

    abstract member ClearColor : c : nativeptr<C4f> -> unit
    abstract member ClearDepth : c : nativeptr<float> -> unit
    abstract member ClearStencil : c : nativeptr<int> -> unit
    abstract member Clear : mask : ClearBufferMask -> unit

    abstract member Call : fptr : nativeint -> unit
    abstract member Call : fptr : nativeint * argument : int -> unit
    abstract member CallIndirect : fptr : nativeptr<nativeint> -> unit

    abstract member Copy<'a when 'a : unmanaged> : src : nativeptr<'a> * dst : nativeptr<'a> * cnt : int -> unit

    abstract member Conditional  : nativeptr<int> * (ICommandStream -> unit) -> unit




[<AutoOpen>]
module GLAssemblerExtensions =
    open System.Runtime.InteropServices

    type AssemblerCommandStream(s : IAssemblerStream) =
        static let queryObjectPointers =
            lazy (
                LookupTable.lookupTable [
                    typeof<int>, OpenGl.Pointers.GetQueryObjectInt32
                    typeof<uint32>, OpenGl.Pointers.GetQueryObjectUInt32
                    typeof<int64>, OpenGl.Pointers.GetQueryObjectInt64
                    typeof<uint64>, OpenGl.Pointers.GetQueryObjectUInt64
                ]
            )
        static let getInteger =
            lazy (
                LookupTable.lookupTable [
                    typeof<int>, OpenGl.Pointers.GetInteger
                    typeof<uint32>, OpenGl.Pointers.GetInteger
                    typeof<float32>, OpenGl.Pointers.GetFloat
                    typeof<float>, OpenGl.Pointers.GetDouble
                ]
            )
        static let getIndexedInteger =
            lazy (
                LookupTable.lookupTable [
                    yield typeof<int>, OpenGl.Pointers.GetIndexedInteger
                    yield typeof<uint32>, OpenGl.Pointers.GetIndexedInteger
                    yield typeof<int64>, OpenGl.Pointers.GetIndexedInteger64
                    yield typeof<uint64>, OpenGl.Pointers.GetIndexedInteger64

                    if sizeof<nativeint> = 8 then
                        yield typeof<nativeint>, OpenGl.Pointers.GetIndexedInteger64
                        yield typeof<unativeint>, OpenGl.Pointers.GetIndexedInteger64
                    else
                        yield typeof<nativeint>, OpenGl.Pointers.GetIndexedInteger
                        yield typeof<unativeint>, OpenGl.Pointers.GetIndexedInteger


                ]
            )

        static let memcpy =
            lazy (
                match System.Environment.OSVersion with
                | Windows ->
                    let lib = DynamicLinker.loadLibrary "msvcrt.dll"
                    lib.GetFunction "memcpy"
                | Linux ->
                    let lib = DynamicLinker.loadLibrary "libc"
                    lib.GetFunction "memcpy"
                | Mac ->
                    failwith "MacOS not supported atm."
            )

        member this.Toggle(cap : int, enabled : nativeptr<int>) =
            let d = s.NewLabel()
            let e = s.NewLabel()

            s.Cmp(NativePtr.toNativeInt enabled, 0)
            s.Jump(JumpCondition.Equal, d)

            // Enable
            s.BeginCall(1)
            s.PushArg(cap)
            s.Call(OpenGl.Pointers.Enable)
            s.Jump(e)

            s.Mark(d)

            // Disable
            s.BeginCall(1)
            s.PushArg(cap)
            s.Call(OpenGl.Pointers.Disable)

            s.Mark(e)

        member this.GenQueries(count : int, queries : nativeptr<int>) =
            s.BeginCall(2)
            s.PushArg (NativePtr.toNativeInt queries)
            s.PushArg count
            s.Call(OpenGl.Pointers.GenQueries)

        member this.DeleteQueries(count : int, queries : nativeptr<int>) =
            s.BeginCall(2)
            s.PushArg (NativePtr.toNativeInt queries)
            s.PushArg count
            s.Call(OpenGl.Pointers.DeleteQueries)

        member this.BeginQuery(target : QueryTarget, query : nativeptr<int>) =
            s.BeginCall(2)
            s.PushIntArg (NativePtr.toNativeInt query)
            s.PushArg (int target)
            s.Call(OpenGl.Pointers.BeginQuery)

        member this.BeginQuery(target : QueryTarget, query : int) =
            s.BeginCall(2)
            s.PushArg query
            s.PushArg (int target)
            s.Call(OpenGl.Pointers.BeginQuery)

        member this.EndQuery(target : QueryTarget) =
            s.BeginCall(1)
            s.PushArg (int target)
            s.Call(OpenGl.Pointers.EndQuery)

        member this.GetQueryObject<'a when 'a : unmanaged>(query : nativeptr<int>, param : GetQueryObjectParam, ptr : nativeptr<'a>) =
            let fptr = queryObjectPointers.Value typeof<'a>
            s.BeginCall(3)
            s.PushArg (NativePtr.toNativeInt ptr)
            s.PushArg (int param)
            s.PushIntArg (NativePtr.toNativeInt query)
            s.Call(fptr)

        member this.GetQueryObject<'a when 'a : unmanaged>(query : int, param : GetQueryObjectParam, ptr : nativeptr<'a>) =
            let fptr = queryObjectPointers.Value typeof<'a>
            s.BeginCall(3)
            s.PushArg (NativePtr.toNativeInt ptr)
            s.PushArg (int param)
            s.PushArg query
            s.Call(fptr)

        member this.QueryCounter(target : QueryCounterTarget, id : int) =
            s.BeginCall(2)
            s.PushArg (int target)
            s.PushArg id
            s.Call(OpenGl.Pointers.QueryCounter)

        member this.QueryCounter(target : QueryCounterTarget, id : nativeptr<int>) =
            s.BeginCall(2)
            s.PushArg (int target)
            s.PushIntArg (NativePtr.toNativeInt id)
            s.Call(OpenGl.Pointers.QueryCounter)

        member this.QueryCounterIndirect(target : QueryCounterTarget, id : nativeptr<int>) =
            s.BeginCall(2)
            s.PushArg (int target)
            s.PushIntArg (NativePtr.toNativeInt id)
            s.Call(OpenGl.Pointers.QueryCounter)


        member this.Get<'a when 'a : unmanaged>(pname : GetPName, ptr : nativeptr<'a>) =
            let fptr = getInteger.Value typeof<'a>
            s.BeginCall(2)
            s.PushArg(NativePtr.toNativeInt ptr)
            s.PushArg(int pname)
            s.Call(fptr)

        member this.Get<'a when 'a : unmanaged>(pname : GetIndexedPName, index : int, ptr : nativeptr<'a>) =
             let fptr = getIndexedInteger.Value typeof<'a>
             s.BeginCall(3)
             s.PushArg(NativePtr.toNativeInt ptr)
             s.PushArg(index)
             s.PushArg(int pname)
             s.Call(fptr)

        member this.SetDrawBuffers(count : int, ptr : nativeint) =
            s.BeginCall(2)
            s.PushArg(ptr)
            s.PushArg(count)
            s.Call(OpenGl.Pointers.DrawBuffers)


        // ================================================================================================================
        // Blending
        // ================================================================================================================

        member this.SetBlendColor(c : nativeptr<C4f>) =
            s.BeginCall(4)
            s.PushFloatArg(12n + NativePtr.toNativeInt c)
            s.PushFloatArg(8n + NativePtr.toNativeInt c)
            s.PushFloatArg(4n + NativePtr.toNativeInt c)
            s.PushFloatArg(0n + NativePtr.toNativeInt c)
            s.Call(OpenGl.Pointers.BlendColor)

        member this.SetBlendModes(count : int, ptr : nativeint) =
            s.BeginCall(2)
            s.PushArg(ptr)
            s.PushArg(count)
            s.Call(OpenGl.Pointers.HSetBlendModes)

        member this.SetColorMasks(count : int, ptr : nativeint) =
            s.BeginCall(2)
            s.PushArg(ptr)
            s.PushArg(count)
            s.Call(OpenGl.Pointers.HSetColorMasks)

        member this.SetColorMask(r : bool, g : bool, b : bool, a : bool) =
            s.BeginCall(4)
            s.PushArg(if a then 1 else 0)
            s.PushArg(if b then 1 else 0)
            s.PushArg(if g then 1 else 0)
            s.PushArg(if r then 1 else 0)
            s.Call(OpenGl.Pointers.ColorMask)

        // ================================================================================================================
        // Depth
        // ================================================================================================================
        member this.SetDepthTest(m : nativeptr<int>) =
            let d = s.NewLabel()
            let e = s.NewLabel()
            let ptr = NativePtr.toNativeInt m

            s.Cmp(ptr, 0)
            s.Jump(JumpCondition.Equal, d)

            // Enable
            s.BeginCall(1)
            s.PushArg(int EnableCap.DepthTest)
            s.Call(OpenGl.Pointers.Enable)

            s.BeginCall(1)
            s.PushIntArg(ptr)
            s.Call(OpenGl.Pointers.DepthFunc)

            s.Jump(e)

            s.Mark(d)

            // Disable
            s.BeginCall(1)
            s.PushArg(int EnableCap.DepthTest)
            s.Call(OpenGl.Pointers.Disable)

            s.Mark(e)

        member this.SetDepthClamp(m : nativeptr<int>) =
            this.Toggle(int EnableCap.DepthClamp, m)

        member this.SetDepthBias(m : nativeptr<DepthBiasInfo>) =
             s.BeginCall(1)
             s.PushArg(NativePtr.toNativeInt m)
             s.Call(OpenGl.Pointers.HSetDepthBias)

        member this.SetDepthMask(mask : bool) =
            s.BeginCall(1)
            s.PushArg(if mask then 1 else 0)
            s.Call(OpenGl.Pointers.DepthMask)

        member this.SetDepthMask(m : nativeptr<int>) =
            s.BeginCall(1)
            s.PushIntArg(NativePtr.toNativeInt m)
            s.Call(OpenGl.Pointers.DepthMask)

        // ================================================================================================================
        // Stencil
        // ================================================================================================================

        member this.SetStencilMask(mask : bool) =
            s.BeginCall(1)
            s.PushArg(if mask then 0xFFFFFFFF else 0)
            s.Call(OpenGl.Pointers.StencilMask)

        member this.SetStencilMask(face : StencilFace, m : nativeptr<uint32>) =
            s.BeginCall(2)
            s.PushIntArg(NativePtr.toNativeInt m)
            s.PushArg(int face)
            s.Call(OpenGl.Pointers.StencilMaskSeparate)

        member this.SetStencilModes(front : nativeptr<GLStencilMode>, back : nativeptr<GLStencilMode>) =
            s.BeginCall(2)
            s.PushArg(NativePtr.toNativeInt back)
            s.PushArg(NativePtr.toNativeInt front)
            s.Call(OpenGl.Pointers.HSetStencilMode)


        // ================================================================================================================
        // Rasterizer
        // ================================================================================================================

        member this.SetPolygonMode(m : nativeptr<int>) =
            s.BeginCall(2)
            s.PushIntArg(NativePtr.toNativeInt m)
            s.PushArg(int MaterialFace.FrontAndBack)
            s.Call(OpenGl.Pointers.PolygonMode)

        member this.SetCullMode(m : nativeptr<int>) =
            let d = s.NewLabel()
            let e = s.NewLabel()
            let ptr = NativePtr.toNativeInt m

            s.Cmp(ptr, 0)
            s.Jump(JumpCondition.Equal, d)

            // Enable
            s.BeginCall(1)
            s.PushArg(int EnableCap.CullFace)
            s.Call(OpenGl.Pointers.Enable)

            s.BeginCall(1)
            s.PushIntArg(ptr)
            s.Call(OpenGl.Pointers.CullFace)

            s.Jump(e)

            s.Mark(d)

            // Disable
            s.BeginCall(1)
            s.PushArg(int EnableCap.CullFace)
            s.Call(OpenGl.Pointers.Disable)

            s.Mark(e)

        member this.SetFrontFace(m : nativeptr<int>) =
            s.BeginCall(1)
            s.PushIntArg(NativePtr.toNativeInt m)
            s.Call(OpenGl.Pointers.FrontFace)

        member this.SetConservativeRaster(r : nativeptr<int>) =
            this.Toggle(int NvConservativeRaster.ConservativeRasterizationNv, r)

        member this.SetMultisample(m : nativeptr<int>) =
            this.Toggle(int EnableCap.Multisample, m)



        member this.UseProgram(p : int) =
             s.BeginCall(1)
             s.PushArg(p)
             s.Call(OpenGl.Pointers.BindProgram)

        member this.UseProgram(m : nativeptr<int>) =
             s.BeginCall(1)
             s.PushIntArg(NativePtr.toNativeInt m)
             s.Call(OpenGl.Pointers.BindProgram)

        member this.DispatchCompute(gx : int, gy : int, gz : int) =
            s.BeginCall(3)
            s.PushArg gz
            s.PushArg gy
            s.PushArg gx
            s.Call OpenGl.Pointers.DispatchCompute

        member this.DispatchCompute(groups : nativeptr<V3i>) =
            s.BeginCall(3)
            s.PushIntArg (NativePtr.toNativeInt groups + 8n)
            s.PushIntArg (NativePtr.toNativeInt groups + 4n)
            s.PushIntArg (NativePtr.toNativeInt groups + 0n)
            s.Call OpenGl.Pointers.DispatchCompute


        member this.Enable(v : int) =
             s.BeginCall(1)
             s.PushArg(v)
             s.Call(OpenGl.Pointers.Enable)

        member this.Disable(v : int) =
             s.BeginCall(1)
             s.PushArg(v)
             s.Call(OpenGl.Pointers.Disable)

        member this.Enable(v : nativeptr<int>) =
             s.BeginCall(1)
             s.PushIntArg(NativePtr.toNativeInt v)
             s.Call(OpenGl.Pointers.Enable)

        member this.Disable(v : nativeptr<int>) =
             s.BeginCall(1)
             s.PushIntArg(NativePtr.toNativeInt v)
             s.Call(OpenGl.Pointers.Disable)



        member this.BindBuffer(target : int, buffer : int) =
            s.BeginCall(2)
            s.PushArg(buffer)
            s.PushArg(target)
            s.Call(OpenGl.Pointers.BindBuffer)

        member this.BindBuffer(target : int, buffer : nativeptr<int>) =
            s.BeginCall(2)
            s.PushIntArg(NativePtr.toNativeInt buffer)
            s.PushArg(target)
            s.Call(OpenGl.Pointers.BindBuffer)

        member this.BindBufferBase(target : BufferRangeTarget, slot : int, b : int) =
            s.BeginCall(3)
            s.PushArg(b)
            s.PushArg(slot)
            s.PushArg(int target)
            s.Call(OpenGl.Pointers.BindBufferBase)

        member this.BindBufferBase(target : BufferRangeTarget, slot : int, b : nativeptr<int>) =
            s.BeginCall(3)
            s.PushIntArg(NativePtr.toNativeInt b)
            s.PushArg(slot)
            s.PushArg(int target)
            s.Call(OpenGl.Pointers.BindBufferBase)

        member this.BindBufferRange(target : BufferRangeTarget, slot : int, b : int, offset : nativeint, size : nativeint) =
            s.BeginCall(5)
            s.PushArg(size)
            s.PushArg(offset)
            s.PushArg(b)
            s.PushArg(slot)
            s.PushArg(int target)
            s.Call(OpenGl.Pointers.BindBufferRange)

        member this.BindBufferRange(target : BufferRangeTarget, slot : int, b : nativeptr<int>, offset : nativeptr<nativeint>, size : nativeptr<nativeint>) =
            s.BeginCall(5)
            s.PushPtrArg(NativePtr.toNativeInt size)
            s.PushPtrArg(NativePtr.toNativeInt offset)
            s.PushIntArg(NativePtr.toNativeInt b)
            s.PushArg(slot)
            s.PushArg(int target)
            s.Call(OpenGl.Pointers.BindBufferRange)

        member this.BindBufferRangeFixed(target : BufferRangeTarget, slot : int, b : nativeptr<int>, offset : nativeint, size : nativeint) =
            s.BeginCall(5)
            s.PushArg(size)
            s.PushArg(offset)
            s.PushIntArg(NativePtr.toNativeInt b)
            s.PushArg(slot)
            s.PushArg(int target)
            s.Call(OpenGl.Pointers.BindBufferRange)

        member this.NamedBufferData(buffer : int, size : nativeint, data : nativeint, usage : OpenTK.Graphics.OpenGL4.BufferUsageHint) =
            s.BeginCall(4)
            s.PushArg (int usage)
            s.PushArg data
            s.PushArg size
            s.PushArg buffer
            s.Call(OpenGl.Pointers.NamedBufferData)

        member this.NamedBufferSubData(buffer : int, offset : nativeint, size : nativeint, data : nativeint) =
            s.BeginCall(4)
            s.PushArg data
            s.PushArg size
            s.PushArg offset
            s.PushArg buffer
            s.Call(OpenGl.Pointers.NamedBufferSubData)


        member this.SetActiveTexture(slot : int) =
            s.BeginCall(1)
            s.PushArg(int OpenGl.Enums.TextureUnit.Texture0 + slot)
            s.Call(OpenGl.Pointers.ActiveTexture)

        member this.BindTexture (target : TextureTarget, t : int) =
            s.BeginCall(2)
            s.PushArg(t)
            s.PushArg(int target)
            s.Call(OpenGl.Pointers.BindTexture)

        member this.BindTexture (target : nativeptr<TextureTarget>, t : nativeptr<int>) =
            s.BeginCall(2)
            s.PushIntArg(NativePtr.toNativeInt t)
            s.PushIntArg(NativePtr.toNativeInt target)
            s.Call(OpenGl.Pointers.BindTexture)

        member this.TexParameteri(target : int, name : TextureParameterName, value : int) =
            s.BeginCall(3)
            s.PushArg(value)
            s.PushArg(int name)
            s.PushArg(target)
            s.Call(OpenGl.Pointers.TexParameteri)

        member this.TexParameterf(target : int, name : TextureParameterName, value : float32) =
            s.BeginCall(3)
            s.PushArg(value)
            s.PushArg(int name)
            s.PushArg(target)
            s.Call(OpenGl.Pointers.TexParameterf)

        member this.BindSampler (slot : int, sampler : int) =
            s.BeginCall(2)
            s.PushArg(sampler)
            s.PushArg(slot)
            s.Call(OpenGl.Pointers.BindSampler)

        member this.BindSampler (slot : int, sampler : nativeptr<int>) =
            s.BeginCall(2)
            s.PushIntArg(NativePtr.toNativeInt sampler)
            s.PushArg(slot)
            s.Call(OpenGl.Pointers.BindSampler)

        member this.BindTexturesAndSamplers (handle : TextureBinding) =
            s.BeginCall(4)
            s.PushArg(handle.textures |> NativePtr.toNativeInt)
            s.PushArg(handle.targets |> NativePtr.toNativeInt)
            s.PushArg(handle.count)
            s.PushArg(handle.offset)
            s.Call(OpenGl.Pointers.HBindTextures)

            s.BeginCall(3)
            s.PushArg(handle.samplers |> NativePtr.toNativeInt)
            s.PushArg(handle.count)
            s.PushArg(handle.offset)
            s.Call(OpenGl.Pointers.HBindSamplers)

        member this.BindImageTexture(unit : int, texture : int, level : int, layered : bool, layer : int, access : TextureAccess, format : TextureFormat) =
            s.BeginCall(7)
            s.PushArg(int format)
            s.PushArg(int access)
            s.PushArg(layer)
            s.PushArg(if layered then 1 else 0)
            s.PushArg(level)
            s.PushArg(texture)
            s.PushArg(unit)
            s.Call(OpenGl.Pointers.BindImageTexture)

        member this.Uniform1fv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform1fv)

        member this.Uniform1iv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform1iv)

        member this.Uniform2fv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform2fv)

        member this.Uniform2iv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform2iv)

        member this.Uniform3fv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform3fv)

        member this.Uniform3iv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform3iv)

        member this.Uniform4fv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform4fv)

        member this.Uniform4iv(location : int, cnt : int, ptr : nativeint) =
            s.BeginCall(3)
            s.PushArg(ptr)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.Uniform4iv)

        member this.UniformMatrix2fv(location : int, cnt : int, transpose : int, ptr : nativeint) =
            s.BeginCall(4)
            s.PushArg(ptr)
            s.PushArg(transpose)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.UniformMatrix2fv)

        member this.UniformMatrix3fv(location : int, cnt : int, transpose : int, ptr : nativeint) =
            s.BeginCall(4)
            s.PushArg(ptr)
            s.PushArg(transpose)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.UniformMatrix3fv)

        member this.UniformMatrix4fv(location : int, cnt : int, transpose : int, ptr : nativeint) =
            s.BeginCall(4)
            s.PushArg(ptr)
            s.PushArg(transpose)
            s.PushArg(cnt)
            s.PushArg(location)
            s.Call(OpenGl.Pointers.UniformMatrix4fv)

        member this.BindVertexAttributes(ctx : nativeptr<nativeint>, handle : VertexInputBindingHandle) =
            s.BeginCall(2)
            s.PushArg(NativePtr.toNativeInt handle.Pointer)
            s.PushArg(NativePtr.toNativeInt ctx)
            s.Call(OpenGl.Pointers.HBindVertexAttributes)


        member this.DrawArrays(stats : nativeptr<V2i>, isActive : nativeptr<int>, beginMode : nativeptr<GLBeginMode>, calls : nativeptr<DrawCallInfoList>) =
            s.BeginCall(4)
            s.PushArg(NativePtr.toNativeInt calls)
            s.PushArg(NativePtr.toNativeInt beginMode)
            s.PushArg(NativePtr.toNativeInt isActive)
            s.PushArg(NativePtr.toNativeInt stats)
            s.Call(OpenGl.Pointers.HDrawArrays)

        member this.DrawElements(stats : nativeptr<V2i>, isActive : nativeptr<int>, beginMode : nativeptr<GLBeginMode>, indexType : int, calls : nativeptr<DrawCallInfoList>) =
            s.BeginCall(5)
            s.PushArg(NativePtr.toNativeInt calls)
            s.PushArg(indexType)
            s.PushArg(NativePtr.toNativeInt beginMode)
            s.PushArg(NativePtr.toNativeInt isActive)
            s.PushArg(NativePtr.toNativeInt stats)
            s.Call(OpenGl.Pointers.HDrawElements)

        member this.DrawArraysIndirect(stats : nativeptr<V2i>, isActive : nativeptr<int>, beginMode : nativeptr<GLBeginMode>, indirect : nativeptr<IndirectDrawArgs>) =
            s.BeginCall(4)
            s.PushArg(NativePtr.toNativeInt indirect)
            s.PushArg(NativePtr.toNativeInt beginMode)
            s.PushArg(NativePtr.toNativeInt isActive)
            s.PushArg(NativePtr.toNativeInt stats)
            s.Call(OpenGl.Pointers.HDrawArraysIndirect)

        member this.DrawElementsIndirect(stats : nativeptr<V2i>, isActive : nativeptr<int>, beginMode : nativeptr<GLBeginMode>, indexType : int, indirect : nativeptr<IndirectDrawArgs>) =
            s.BeginCall(5)
            s.PushArg(NativePtr.toNativeInt indirect)
            s.PushArg(indexType)
            s.PushArg(NativePtr.toNativeInt beginMode)
            s.PushArg(NativePtr.toNativeInt isActive)
            s.PushArg(NativePtr.toNativeInt stats)
            s.Call(OpenGl.Pointers.HDrawElementsIndirect)

        member this.ClearColor(c : nativeptr<C4f>) =
            s.BeginCall(4)
            s.PushFloatArg(12n + NativePtr.toNativeInt c)
            s.PushFloatArg(8n + NativePtr.toNativeInt c)
            s.PushFloatArg(4n + NativePtr.toNativeInt c)
            s.PushFloatArg(0n + NativePtr.toNativeInt c)
            s.Call(OpenGl.Pointers.ClearColor)

        member this.ClearDepth(c : nativeptr<float>) =
            s.BeginCall(1)
            s.PushDoubleArg(NativePtr.toNativeInt c)
            s.Call(OpenGl.Pointers.ClearDepth)

        member this.ClearStencil(c : nativeptr<int>) =
            s.BeginCall(1)
            s.PushIntArg(NativePtr.toNativeInt c)
            s.Call(OpenGl.Pointers.ClearStencil)

        member this.Clear(mask : ClearBufferMask) =
            s.BeginCall(1)
            s.PushArg(mask |> int)
            s.Call(OpenGl.Pointers.Clear)

        member this.Copy(src : nativeptr<'a>, dst : nativeptr<'a>, cnt : int) =
            if cnt >= 0 then
                let sa = sizeof<'a>
                if cnt = 1 && sa &&& 7 = 0 then
                    let cnt = sa >>> 3
                    let mutable src = NativePtr.toNativeInt src
                    let mutable dst = NativePtr.toNativeInt dst

                    for i in 2 .. cnt do
                        s.Copy(src, dst, true)
                        src <- src + 8n
                        dst <- dst + 8n

                    s.Copy(src, dst, true)

                elif cnt = 1 && sa &&& 3 = 0 then
                    let cnt = sa >>> 2
                    let mutable src = NativePtr.toNativeInt src
                    let mutable dst = NativePtr.toNativeInt dst

                    for i in 2 .. cnt do
                        s.Copy(src, dst, false)
                        src <- src + 4n
                        dst <- dst + 4n

                    s.Copy(src, dst, false)

                else
                    let cpy = memcpy.Value

                    s.BeginCall(3)
                    s.PushArg(nativeint cnt * nativeint sa)
                    s.PushArg(NativePtr.toNativeInt src)
                    s.PushArg(NativePtr.toNativeInt dst)
                    s.Call(cpy.Handle)

        member this.Conditional(cond : nativeptr<int>, write : ICommandStream -> unit) =
            let skip = s.NewLabel()
            s.Cmp(NativePtr.toNativeInt cond, 0)
            s.Jump(JumpCondition.Equal, skip)

            write this

            s.Mark(skip)

        member this.Call(ptr : nativeint) =
            s.BeginCall(0)
            s.Call(ptr)

        member this.Call(ptr : nativeint, arg : int) =
            s.BeginCall(1)
            s.PushArg arg
            s.Call(ptr)

        member this.CallIndirect(ptr : nativeptr<nativeint>) =
            s.BeginCall(0)
            s.CallIndirect(ptr)

        member x.MemoryBarrier(flags : MemoryBarrierFlags) =
            s.BeginCall(1)
            s.PushArg(int flags)
            s.Call(OpenGl.Pointers.MemoryBarrier)


        interface ICommandStream with
            member this.BeginQuery(target: QueryTarget, query: nativeptr<int>) = this.BeginQuery(target, query)
            member this.BeginQuery(target: QueryTarget, query: int) = this.BeginQuery(target, query)
            member this.BindBuffer(target: int, buffer: int) = this.BindBuffer(target, buffer)
            member this.BindBuffer(target: int, buffer: nativeptr<int>) = this.BindBuffer(target, buffer)
            member this.BindBufferBase(target: BufferRangeTarget, slot: int, b: int) = this.BindBufferBase(target, slot, b)
            member this.BindBufferBase(target: BufferRangeTarget, slot: int, b: nativeptr<int>) = this.BindBufferBase(target, slot, b)
            member this.BindBufferRange(target: BufferRangeTarget, slot: int, b: int, offset: nativeint, size: nativeint) = this.BindBufferRange(target, slot, b, offset, size)
            member this.BindBufferRange(target: BufferRangeTarget, slot: int, b: nativeptr<int>, offset: nativeptr<nativeint>, size: nativeptr<nativeint>) = this.BindBufferRange(target, slot, b, offset, size)
            member this.BindBufferRangeFixed(target: BufferRangeTarget, slot: int, b: nativeptr<int>, offset: nativeint, size: nativeint) = this.BindBufferRangeFixed(target, slot, b, offset, size)
            member this.BindImageTexture(unit: int, texture: int, level: int, layered: bool, layer: int, access: TextureAccess, format: TextureFormat) = this.BindImageTexture(unit, texture, level, layered, layer, access, format)
            member this.BindSampler(slot: int, sampler: int) = this.BindSampler(slot, sampler)
            member this.BindSampler(slot: int, sampler: nativeptr<int>) = this.BindSampler(slot, sampler)
            member this.BindTexture(target: TextureTarget, t: int) = this.BindTexture(target, t)
            member this.BindTexture(target: nativeptr<TextureTarget>, t: nativeptr<int>) = this.BindTexture(target, t)
            member this.BindTexturesAndSamplers(textureBinding: TextureBinding) = this.BindTexturesAndSamplers(textureBinding)
            member this.BindVertexAttributes(ctx: nativeptr<nativeint>, handle: VertexInputBindingHandle) = this.BindVertexAttributes(ctx, handle)
            member this.Clear(mask: ClearBufferMask) = this.Clear(mask)
            member this.ClearColor(c: nativeptr<C4f>) = this.ClearColor(c)
            member this.ClearDepth(c: nativeptr<float>) = this.ClearDepth(c)
            member this.ClearStencil(c: nativeptr<int>) = this.ClearStencil(c)
            member this.DeleteQueries(count: int, queries: nativeptr<int>) = this.DeleteQueries(count, queries)
            member this.Disable(v: int) = this.Disable(v)
            member this.Disable(v: nativeptr<int>) = this.Disable(v)
            member this.DispatchCompute(gx: int, gy: int, gz: int) = this.DispatchCompute(gx, gy, gz)
            member this.DispatchCompute(groups: nativeptr<V3i>) = this.DispatchCompute(groups)
            member this.DrawArrays(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, calls: nativeptr<DrawCallInfoList>) = this.DrawArrays(stats, isActive, beginMode, calls)
            member this.DrawArraysIndirect(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, indirect: nativeptr<IndirectDrawArgs>) = this.DrawArraysIndirect(stats, isActive, beginMode, indirect)
            member this.DrawElements(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, indexType: int, calls: nativeptr<DrawCallInfoList>) = this.DrawElements(stats, isActive, beginMode, indexType, calls)
            member this.DrawElementsIndirect(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, indexType: int, indirect: nativeptr<IndirectDrawArgs>) = this.DrawElementsIndirect(stats, isActive, beginMode, indexType, indirect)
            member this.Enable(v: int) = this.Enable(v)
            member this.Enable(v: nativeptr<int>) = this.Enable(v)
            member this.EndQuery(target: QueryTarget) = this.EndQuery(target)
            member this.GenQueries(count: int, queries: nativeptr<int>) = this.GenQueries(count, queries)
            member this.Get(pname: GetPName, ptr: nativeptr<'a>) = this.Get(pname, ptr)
            member this.Get(pname: GetIndexedPName, index: int, ptr: nativeptr<'a>) = this.Get(pname, index, ptr)
            member this.GetQueryObject(query: nativeptr<int>, param: GetQueryObjectParam, ptr: nativeptr<'a>) = this.GetQueryObject(query, param, ptr)
            member this.GetQueryObject(query: int, param: GetQueryObjectParam, ptr: nativeptr<'a>) = this.GetQueryObject(query, param, ptr)
            member this.NamedBufferData(buffer: int, size: nativeint, data: nativeint, usage: OpenTK.Graphics.OpenGL4.BufferUsageHint) = this.NamedBufferData(buffer, size, data, usage)
            member this.NamedBufferSubData(buffer: int, offset: nativeint, size: nativeint, data: nativeint) = this.NamedBufferSubData(buffer, offset, size, data)
            member this.QueryCounter(target: QueryCounterTarget, id: int) = this.QueryCounter(target, id)
            member this.QueryCounter(target: QueryCounterTarget, id: nativeptr<int>) = this.QueryCounter(target, id)
            member this.SetActiveTexture(slot: int) = this.SetActiveTexture(slot)
            member this.SetBlendColor(c: nativeptr<C4f>) = this.SetBlendColor(c)
            member this.SetBlendModes(count : int, ptr: nativeint) = this.SetBlendModes(count, ptr)
            member this.SetColorMasks(count : int, ptr: nativeint) = this.SetColorMasks(count, ptr)
            member this.SetColorMask(r : bool, g : bool, b : bool, a : bool) = this.SetColorMask(r, g, b, a)
            member this.SetConservativeRaster(r: nativeptr<int>) = this.SetConservativeRaster(r)
            member this.SetCullMode(m: nativeptr<int>) = this.SetCullMode(m)
            member this.SetFrontFace(m: nativeptr<int>) = this.SetFrontFace(m)
            member this.SetDepthTest(m: nativeptr<int>) = this.SetDepthTest(m)
            member this.SetDepthBias(m: nativeptr<DepthBiasInfo>) = this.SetDepthBias(m)
            member this.SetDepthMask(m: bool) = this.SetDepthMask(m)
            member this.SetDepthMask(m: nativeptr<int>) = this.SetDepthMask(m)
            member this.SetDepthClamp(m: nativeptr<int>) = this.SetDepthClamp(m)
            member this.SetDrawBuffers(count: int, ptr: nativeint) = this.SetDrawBuffers(count, ptr)
            member this.SetMultisample(r: nativeptr<int>) = this.SetMultisample(r)
            member this.SetPolygonMode(m: nativeptr<int>) = this.SetPolygonMode(m)
            member this.SetStencilMask(m: bool) = this.SetStencilMask(m)
            member this.SetStencilMask(f : StencilFace, m: nativeptr<uint32>) = this.SetStencilMask(f, m)
            member this.SetStencilModes(f: nativeptr<GLStencilMode>, b: nativeptr<GLStencilMode>) = this.SetStencilModes(f, b)
            member this.TexParameterf(target: int, name: TextureParameterName, value: float32) = this.TexParameterf(target, name, value)
            member this.TexParameteri(target: int, name: TextureParameterName, value: int) = this.TexParameteri(target, name, value)
            member this.Uniform1fv(location: int, cnt: int, ptr: nativeint) = this.Uniform1fv(location, cnt, ptr)
            member this.Uniform1iv(location: int, cnt: int, ptr: nativeint) = this.Uniform1iv(location, cnt, ptr)
            member this.Uniform2fv(location: int, cnt: int, ptr: nativeint) = this.Uniform2fv(location, cnt, ptr)
            member this.Uniform2iv(location: int, cnt: int, ptr: nativeint) = this.Uniform2iv(location, cnt, ptr)
            member this.Uniform3fv(location: int, cnt: int, ptr: nativeint) = this.Uniform3fv(location, cnt, ptr)
            member this.Uniform3iv(location: int, cnt: int, ptr: nativeint) = this.Uniform3iv(location, cnt, ptr)
            member this.Uniform4fv(location: int, cnt: int, ptr: nativeint) = this.Uniform4fv(location, cnt, ptr)
            member this.Uniform4iv(location: int, cnt: int, ptr: nativeint) = this.Uniform4iv(location, cnt, ptr)
            member this.UniformMatrix2fv(location: int, cnt: int, transpose: int, ptr: nativeint) = this.UniformMatrix2fv(location, cnt, transpose, ptr)
            member this.UniformMatrix3fv(location: int, cnt: int, transpose: int, ptr: nativeint) = this.UniformMatrix3fv(location, cnt, transpose, ptr)
            member this.UniformMatrix4fv(location: int, cnt: int, transpose: int, ptr: nativeint) = this.UniformMatrix4fv(location, cnt, transpose, ptr)
            member this.UseProgram(p: int) = this.UseProgram(p)
            member this.UseProgram(m: nativeptr<int>) = this.UseProgram(m)
            member this.Conditional(cond: nativeptr<int>, write : ICommandStream -> unit) = this.Conditional(cond, write)
            member this.Copy(src: nativeptr<'a>, dst: nativeptr<'a>, cnt : int) = this.Copy(src, dst, cnt)
            member this.Call(ptr) = this.Call ptr
            member this.CallIndirect(ptr) = this.CallIndirect(ptr)
            member this.Call(ptr : nativeint, arg : int) = this.Call(ptr, arg)
            member this.MemoryBarrier(flags : MemoryBarrierFlags) = this.MemoryBarrier(flags)

    type private DebugCallbackDelegate = delegate of int -> unit
    type DebugCommandStream(inner : ICommandStream) =
        let commands = System.Collections.Generic.List<string * obj[]>()

        let after(i : int) =
            System.Console.WriteLine("{0}", fst commands.[i])

            let err = GL.GetError()
            if err <> ErrorCode.NoError then
                let sofar = Seq.take (i + 1) commands |> Seq.toArray
                System.Diagnostics.Debugger.Break()
                printfn "%A" sofar

        let afterDel =
            Marshal.PinDelegate(DebugCallbackDelegate(after))


        member x.Append(name : string, [<System.ParamArray>] args : obj[]) =
            let id = commands.Count
            commands.Add(name, args)
            inner.Call(afterDel.Pointer, id)

        interface ICommandStream with
            member x.BeginQuery(target: QueryTarget, query: nativeptr<int>) = inner.BeginQuery(target, query); x.Append("BeginQuery", target, query)
            member x.BeginQuery(target: QueryTarget, query: int) = inner.BeginQuery(target, query); x.Append("BeginQuery", target, query)
            member x.BindBuffer(target: int, buffer: int) = inner.BindBuffer(target, buffer); x.Append("BindBuffer", target, buffer)
            member x.BindBuffer(target: int, buffer: nativeptr<int>) = inner.BindBuffer(target, buffer); x.Append("BindBuffer", target, buffer)
            member x.BindBufferBase(target: BufferRangeTarget, slot: int, b: int) = inner.BindBufferBase(target, slot, b); x.Append("BindBufferBase", target, slot, b)
            member x.BindBufferBase(target: BufferRangeTarget, slot: int, b: nativeptr<int>) = inner.BindBufferBase(target, slot, b); x.Append("BindBufferBase", target, slot, b)
            member x.BindBufferRange(target: BufferRangeTarget, slot: int, b: int, offset: nativeint, size: nativeint) = inner.BindBufferRange(target, slot, b, offset, size); x.Append("BindBufferRange", target, slot, b, offset, size)
            member x.BindBufferRange(target: BufferRangeTarget, slot: int, b: nativeptr<int>, offset: nativeptr<nativeint>, size: nativeptr<nativeint>) = inner.BindBufferRange(target, slot, b, offset, size); x.Append("BindBufferRange", target, slot, b, offset, size)
            member x.BindBufferRangeFixed(target: BufferRangeTarget, slot: int, b: nativeptr<int>, offset: nativeint, size: nativeint) = inner.BindBufferRangeFixed(target, slot, b, offset, size); x.Append("BindBufferRangeFixed", target, slot, b, offset, size)
            member x.BindImageTexture(unit: int, texture: int, level: int, layered: bool, layer: int, access: TextureAccess, format: TextureFormat) = inner.BindImageTexture(unit, texture, level, layered, layer, access, format); x.Append("BindImageTexture", unit, texture, level, layered, layer, access, format)
            member x.BindSampler(slot: int, sampler: int) = inner.BindSampler(slot, sampler); x.Append("BindSampler", slot, sampler)
            member x.BindSampler(slot: int, sampler: nativeptr<int>) = inner.BindSampler(slot, sampler); x.Append("BindSampler", slot, sampler)
            member x.BindTexture(target: TextureTarget, t: int) = inner.BindTexture(target, t); x.Append("BindTexture", target, t)
            member x.BindTexture(target: nativeptr<TextureTarget>, t: nativeptr<int>) = inner.BindTexture(target, t); x.Append("BindTexture", target, t)
            member x.BindTexturesAndSamplers(textureBinding: TextureBinding) = inner.BindTexturesAndSamplers(textureBinding); x.Append("BindTexturesAndSamplers", textureBinding)
            member x.BindVertexAttributes(ctx: nativeptr<nativeint>, handle: VertexInputBindingHandle) = inner.BindVertexAttributes(ctx, handle); x.Append("BindVertexAttributes", ctx, handle)
            member x.Clear(mask: ClearBufferMask) = inner.Clear(mask); x.Append("Clear", mask)
            member x.ClearColor(c: nativeptr<C4f>) = inner.ClearColor(c); x.Append("ClearColor", c)
            member x.ClearDepth(c: nativeptr<float>) = inner.ClearDepth(c); x.Append("ClearDepth", c)
            member x.ClearStencil(c: nativeptr<int>) = inner.ClearStencil(c); x.Append("ClearStencil", c)
            member x.DeleteQueries(count: int, queries: nativeptr<int>) = inner.DeleteQueries(count, queries); x.Append("DeleteQueries", count, queries)
            member x.Disable(v: int) = inner.Disable(v); x.Append("Disable", v)
            member x.Disable(v: nativeptr<int>) = inner.Disable(v); x.Append("Disable", v)
            member x.DispatchCompute(gx: int, gy: int, gz: int) = inner.DispatchCompute(gx, gy, gz); x.Append("DispatchCompute", gx, gy, gz)
            member x.DispatchCompute(groups: nativeptr<V3i>) = inner.DispatchCompute(groups); x.Append("DispatchCompute", groups)
            member x.DrawArrays(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, calls: nativeptr<DrawCallInfoList>) = inner.DrawArrays(stats, isActive, beginMode, calls); x.Append("DrawArrays", stats, isActive, beginMode, calls)
            member x.DrawArraysIndirect(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, indirect: nativeptr<IndirectDrawArgs>) = inner.DrawArraysIndirect(stats, isActive, beginMode, indirect); x.Append("DrawArraysIndirect", stats, isActive, beginMode, indirect)
            member x.DrawElements(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, indexType: int, calls: nativeptr<DrawCallInfoList>) = inner.DrawElements(stats, isActive, beginMode, indexType, calls); x.Append("DrawElements", stats, isActive, beginMode, indexType, calls)
            member x.DrawElementsIndirect(stats: nativeptr<V2i>, isActive: nativeptr<int>, beginMode: nativeptr<GLBeginMode>, indexType: int, indirect: nativeptr<IndirectDrawArgs>) = inner.DrawElementsIndirect(stats, isActive, beginMode, indexType, indirect); x.Append("DrawElementsIndirect", stats, isActive, beginMode, indexType, indirect)
            member x.Enable(v: int) = inner.Enable(v); x.Append("Enable", v)
            member x.Enable(v: nativeptr<int>) = inner.Enable(v); x.Append("Enable", v)
            member x.EndQuery(target: QueryTarget) = inner.EndQuery(target); x.Append("EndQuery", target)
            member x.GenQueries(count: int, queries: nativeptr<int>) = inner.GenQueries(count, queries); x.Append("GenQueries", count, queries)
            member x.Get(pname: GetPName, ptr: nativeptr<'a>) = inner.Get(pname, ptr); x.Append("Get", pname, ptr)
            member x.Get(pname: GetIndexedPName, index: int, ptr: nativeptr<'a>) = inner.Get(pname, index, ptr); x.Append("Get", pname, index, ptr)
            member x.GetQueryObject(query: nativeptr<int>, param: GetQueryObjectParam, ptr: nativeptr<'a>) = inner.GetQueryObject(query, param, ptr); x.Append("GetQueryObject", query, param, ptr)
            member x.GetQueryObject(query: int, param: GetQueryObjectParam, ptr: nativeptr<'a>) = inner.GetQueryObject(query, param, ptr); x.Append("GetQueryObject", query, param, ptr)
            member x.NamedBufferData(buffer: int, size: nativeint, data: nativeint, usage: OpenTK.Graphics.OpenGL4.BufferUsageHint) = inner.NamedBufferData(buffer, size, data, usage); x.Append("NamedBufferData", buffer, size, data, usage)
            member x.NamedBufferSubData(buffer: int, offset: nativeint, size: nativeint, data: nativeint) = inner.NamedBufferSubData(buffer, offset, size, data); x.Append("NamedBufferSubData", buffer, offset, size, data)
            member x.QueryCounter(target: QueryCounterTarget, id: int) = inner.QueryCounter(target, id); x.Append("QueryCounter", target, id)
            member x.QueryCounter(target: QueryCounterTarget, id: nativeptr<int>) = inner.QueryCounter(target, id); x.Append("QueryCounter", target, id)
            member x.SetActiveTexture(slot: int) = inner.SetActiveTexture(slot); x.Append("SetActiveTexture", slot)
            member x.SetBlendColor(c: nativeptr<C4f>) = inner.SetBlendColor(c); x.Append("BlendColor", c)
            member x.SetBlendModes(count : int, ptr: nativeint) = inner.SetBlendModes(count, ptr); x.Append("SetBlendModes", ptr)
            member x.SetColorMasks(count : int, ptr: nativeint) = inner.SetColorMasks(count, ptr); x.Append("SetColorMasks", ptr)
            member x.SetColorMask(r : bool, g : bool, b : bool, a : bool) = inner.SetColorMask(r, g, b, a); x.Append("SetColorMask", r, g, b, a)
            member x.SetConservativeRaster(r: nativeptr<int>) = inner.SetConservativeRaster(r); x.Append("SetConservativeRaster", r)
            member x.SetCullMode(m: nativeptr<int>) = inner.SetCullMode(m); x.Append("SetCullMode", m)
            member x.SetFrontFace(m: nativeptr<int>) = inner.SetFrontFace(m); x.Append("SetFrontFace", m)
            member x.SetDepthTest(m: nativeptr<int>) = inner.SetDepthTest(m); x.Append("SetDepthTest", m)
            member x.SetDepthBias(m: nativeptr<DepthBiasInfo>) = inner.SetDepthBias(m); x.Append("SetDepthBias", m)
            member x.SetDepthMask(m: bool) = inner.SetDepthMask(m); x.Append("SetDepthMask", m)
            member x.SetDepthMask(m: nativeptr<int>) = inner.SetDepthMask(m); x.Append("SetDepthMask", m)
            member x.SetDepthClamp(m: nativeptr<int>) = inner.SetDepthTest(m); x.Append("SetDepthClamp", m)
            member x.SetDrawBuffers(count: int, ptr: nativeint) = inner.SetDrawBuffers(count, ptr); x.Append("SetDrawBuffers", count, ptr)
            member x.SetMultisample(r: nativeptr<int>) = inner.SetMultisample(r); x.Append("SetMultisample", r)
            member x.SetPolygonMode(m: nativeptr<int>) = inner.SetPolygonMode(m); x.Append("SetPolygonMode", m)
            member x.SetStencilMask(mask: bool) = inner.SetStencilMask(mask); x.Append("SetStencilMask", mask)
            member x.SetStencilMask(f: StencilFace, m: nativeptr<uint32>) = inner.SetStencilMask(f, m); x.Append("SetStencilMask", f, m)
            member x.SetStencilModes(f: nativeptr<GLStencilMode>, b: nativeptr<GLStencilMode>) = inner.SetStencilModes(f, b); x.Append("SetStencilMode", f, b)
            member x.TexParameterf(target: int, name: TextureParameterName, value: float32) = inner.TexParameterf(target, name, value); x.Append("TexParameterf", target, name, value)
            member x.TexParameteri(target: int, name: TextureParameterName, value: int) = inner.TexParameteri(target, name, value); x.Append("TexParameteri", target, name, value)
            member x.Uniform1fv(location: int, cnt: int, ptr: nativeint) = inner.Uniform1fv(location, cnt, ptr); x.Append("Uniform1fv", location, cnt, ptr)
            member x.Uniform1iv(location: int, cnt: int, ptr: nativeint) = inner.Uniform1iv(location, cnt, ptr); x.Append("Uniform1iv", location, cnt, ptr)
            member x.Uniform2fv(location: int, cnt: int, ptr: nativeint) = inner.Uniform2fv(location, cnt, ptr); x.Append("Uniform2fv", location, cnt, ptr)
            member x.Uniform2iv(location: int, cnt: int, ptr: nativeint) = inner.Uniform2iv(location, cnt, ptr); x.Append("Uniform2iv", location, cnt, ptr)
            member x.Uniform3fv(location: int, cnt: int, ptr: nativeint) = inner.Uniform3fv(location, cnt, ptr); x.Append("Uniform3fv", location, cnt, ptr)
            member x.Uniform3iv(location: int, cnt: int, ptr: nativeint) = inner.Uniform3iv(location, cnt, ptr); x.Append("Uniform3iv", location, cnt, ptr)
            member x.Uniform4fv(location: int, cnt: int, ptr: nativeint) = inner.Uniform4fv(location, cnt, ptr); x.Append("Uniform4fv", location, cnt, ptr)
            member x.Uniform4iv(location: int, cnt: int, ptr: nativeint) = inner.Uniform4iv(location, cnt, ptr); x.Append("Uniform4iv", location, cnt, ptr)
            member x.UniformMatrix2fv(location: int, cnt: int, transpose: int, ptr: nativeint) = inner.UniformMatrix2fv(location, cnt, transpose, ptr); x.Append("UniformMatrix2fv", location, cnt, transpose, ptr)
            member x.UniformMatrix3fv(location: int, cnt: int, transpose: int, ptr: nativeint) = inner.UniformMatrix3fv(location, cnt, transpose, ptr); x.Append("UniformMatrix3fv", location, cnt, transpose, ptr)
            member x.UniformMatrix4fv(location: int, cnt: int, transpose: int, ptr: nativeint) = inner.UniformMatrix4fv(location, cnt, transpose, ptr); x.Append("UniformMatrix4fv", location, cnt, transpose, ptr)
            member x.UseProgram(p: int) = inner.UseProgram(p); x.Append("UseProgram", p)
            member x.UseProgram(m: nativeptr<int>) = inner.UseProgram(m); x.Append("UseProgram", m)
            member x.Conditional(cond: nativeptr<int>, write : ICommandStream -> unit) = inner.Conditional(cond, fun _ -> write x)
            member x.Copy(src: nativeptr<'a>, dst: nativeptr<'a>, cnt : int) = inner.Copy(src, dst, cnt); x.Append("Copy", src, dst, cnt)
            member x.Call(ptr) = inner.Call(ptr); x.Append("Call", ptr)
            member x.CallIndirect(ptr) = inner.CallIndirect(ptr); x.Append("CallIndirect", ptr)
            member x.Call(ptr : nativeint, arg : int) = inner.Call(ptr, arg); x.Append("Call", ptr, arg)
            member x.MemoryBarrier(flags : MemoryBarrierFlags) = inner.MemoryBarrier(flags); x.Append("MemoryBarrier", flags)

    type ICommandStream with

        member x.GenQuery(query : nativeptr<int>) =
            x.GenQueries(1, query)

        member x.DeleteQuery(query : nativeptr<int>) =
            x.DeleteQueries(1, query)

        member inline x.SetDepthTest(m : IResource<'a, int>) = x.SetDepthTest(m.Pointer)
        member inline x.SetDepthBias(m : IResource<'a, DepthBiasInfo>) = x.SetDepthBias(m.Pointer)
        member inline x.SetDepthMask(m : IResource<'a, int>) = x.SetDepthMask(m.Pointer)
        member inline x.SetDepthClamp(m : IResource<'a, int>) = x.SetDepthClamp(m.Pointer)
        member inline x.SetPolygonMode(m : IResource<'a, int>) = x.SetPolygonMode(m.Pointer)
        member inline x.SetCullMode(m : IResource<'a, int>) = x.SetCullMode(m.Pointer)
        member inline x.SetFrontFace(m : IResource<'a, int>) = x.SetFrontFace(m.Pointer)
        member inline x.SetBlendColor(c : IResource<'a, C4f>) = x.SetBlendColor(c.Pointer)
        member inline x.SetBlendModes(count : int, m : IResource<'a, nativeint>) = x.SetBlendModes(count, NativePtr.toNativeInt m.Pointer)
        member inline x.SetColorMasks(count : int, m : IResource<'a, nativeint>) = x.SetColorMasks(count, NativePtr.toNativeInt m.Pointer)
        member inline x.SetStencilModes(f : IResource<'a, GLStencilMode>, b : IResource<'a, GLStencilMode>) = x.SetStencilModes(f.Pointer, b.Pointer)
        member inline x.SetStencilMask(f : StencilFace, m : IResource<'a, uint32>) = x.SetStencilMask(f, m.Pointer)
        member inline x.UseProgram(m : IResource<Program, int>) = x.UseProgram(m.Pointer)

        member inline x.BindUniformBufferView(slot : int, view : IResource<UniformBufferView, int>) =
            let v = view.Handle.GetValue()
            x.BindBufferRangeFixed(BufferRangeTarget.UniformBuffer, slot, view.Pointer, v.Offset, v.Size)

        member inline x.BindStorageBuffer(slot : int, view : IResource<Buffer, int>) =
            x.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, slot, view.Pointer)

        member inline x.BindTexture (texture : IResource<Texture, V2i>) =
            let texturePtr : nativeptr<int> = NativePtr.ofNativeInt (NativePtr.toNativeInt texture.Pointer)
            let targetPtr : nativeptr<TextureTarget> = NativePtr.ofNativeInt (NativePtr.toNativeInt texture.Pointer + 4n)
            x.BindTexture(targetPtr, texturePtr)

        member inline x.BindVertexAttributes(ctx : nativeptr<nativeint>, vao : IResource<VertexInputBindingHandle,_>) =
            let handle = vao.Handle.GetValue() // unchangeable
            x.BindVertexAttributes(ctx, handle)

        member inline x.SetConservativeRaster(r : IResource<_,int>) = x.SetConservativeRaster(r.Pointer)
        member inline x.SetMultisample(r : IResource<_,int>) = x.SetMultisample(r.Pointer)

        member inline x.DrawArrays(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, calls : IResource<_,DrawCallInfoList>) =
            x.DrawArrays(
                stats,
                isActive.Pointer,
                beginMode.Pointer,
                calls.Pointer
            )

        member inline x.DrawElements(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, indexType : int, calls : IResource<_,DrawCallInfoList>) =
            x.DrawElements(
                stats,
                isActive.Pointer,
                beginMode.Pointer,
                indexType,
                calls.Pointer
            )

        member inline x.DrawArraysIndirect(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, indirect : IResource<_, IndirectDrawArgs>) =
            x.DrawArraysIndirect(
                stats,
                isActive.Pointer,
                beginMode.Pointer,
                indirect.Pointer
            )

        member inline x.DrawElementsIndirect(stats : nativeptr<V2i>, isActive : IResource<_,int>, beginMode : IResource<_, GLBeginMode>, indexType : int, indirect : IResource<_, IndirectDrawArgs>) =
            x.DrawElementsIndirect(
                stats,
                isActive.Pointer,
                beginMode.Pointer,
                indexType,
                indirect.Pointer
            )

        member inline x.ClearColor(c : IResource<C4f, C4f>) = x.ClearColor(c.Pointer)
        member inline x.ClearDepth(c : IResource<float, float>) = x.ClearDepth(c.Pointer)
        member inline x.ClearStencil(c : IResource<int, int>) = x.ClearStencil(c.Pointer)

        member inline x.BindTexturesAndSamplers (textureBinding : IResource<TextureBinding, TextureBinding>) =
            let handle = textureBinding.Update(AdaptiveToken.Top, RenderToken.Empty); textureBinding.Handle.GetValue()
            if handle.count > 0 then
                x.BindTexturesAndSamplers(handle)

        member x.BindSampler (slot : int, sampler : IResource<Sampler, int>) =
            if ExecutionContext.samplersSupported then
                x.BindSampler(slot, sampler.Pointer)
            else
                let s = sampler.Handle.GetValue().Description
                let target = int OpenGl.Enums.TextureTarget.Texture2D
                let unit = int OpenGl.Enums.TextureUnit.Texture0 + slot
                x.TexParameteri(target, TextureParameterName.TextureWrapS, SamplerStateHelpers.wrapMode s.AddressU)
                x.TexParameteri(target, TextureParameterName.TextureWrapT, SamplerStateHelpers.wrapMode s.AddressV)
                x.TexParameteri(target, TextureParameterName.TextureWrapR, SamplerStateHelpers.wrapMode s.AddressW)
                x.TexParameteri(target, TextureParameterName.TextureMinFilter, SamplerStateHelpers.minFilter s.Filter)
                x.TexParameteri(target, TextureParameterName.TextureMagFilter, SamplerStateHelpers.magFilter s.Filter)
                x.TexParameterf(target, TextureParameterName.TextureMinLod, s.MinLod)
                x.TexParameterf(target, TextureParameterName.TextureMaxLod, s.MaxLod)

        member x.QueryTimestamp(temp : nativeptr<int>, result : nativeptr<'a>) =
            x.GenQueries(1, temp)
            x.QueryCounter(QueryCounterTarget.Timestamp, temp)
            x.GetQueryObject(temp, GetQueryObjectParam.QueryResult, result)
            x.DeleteQueries(1, temp)

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


        member this.Copy(src : nativeptr<'a>, dst : nativeptr<'a>) =
            this.Copy(src, dst, 1)

        member this.ConditionalCall(cond : nativeptr<int>, fptr : nativeint) =
            this.Conditional(cond, fun this ->
                this.Call fptr
            )

type RefRef<'a> =
    class
        val mutable public contents : 'a
        member x.Value
            with get() = x.contents
            and set v = x.contents <- v

        new(value : 'a) = { contents = value }
    end


type CompilerInfo =
    {
        resources               : ResourceInputSet
        contextHandle           : nativeptr<nativeint>
        runtimeStats            : nativeptr<V2i>
        currentContext          : aval<ContextHandle>
        drawBufferCount         : int

        structuralChange        : aval<unit>
        usedTextureSlots        : RefRef<CountingHashSet<int>>
        usedUniformBufferSlots  : RefRef<CountingHashSet<int>>

        task                    : IRenderTask
        tags                    : Map<string, obj>
    }



[<AutoOpen>]
module rec ChangeableProgram =

    open System.IO
    open System.Runtime.InteropServices
    open Aardvark.Rendering.Management

    module Memory =
        let executable : Memory<nativeint> =
            {
                malloc = ExecutableMemory.alloc
                mfree = ExecutableMemory.free
                mcopy = fun (src : nativeint) (srcOff : nativeint) (dst : nativeint) (dstOff : nativeint) (size : nativeint) -> Marshal.Copy(src + srcOff, dst + dstOff, size)
                mrealloc = fun (old : nativeint) (oldSize : nativeint) (newSize : nativeint) ->
                    let n = ExecutableMemory.alloc newSize
                    Marshal.Copy(old, n, min oldSize newSize)
                    ExecutableMemory.free old oldSize
                    n
            }

    [<AbstractClass>]
    type IAdaptivePinning() =
        abstract member Pin : object : IAdaptiveObject * evaluate : (AdaptiveToken -> 'a) * release : (unit -> unit) -> nativeptr<'a>
        abstract member Pin : action : (unit -> unit) -> nativeint

        member x.Pin(value : aval<'a>) =
            x.Pin(value, value.GetValue, id)

        member x.Pin(p : FragmentProgram) =
            p.Acquire()
            x.Pin(p, (fun t -> p.Update t; 0), p.Dispose) |> ignore
            p.EntryPointer


    type AdaptivePinning () =
        inherit AdaptiveObject()

        let dirty = Dict<IAdaptiveObject, AdaptiveToken -> unit>()
        let pointers = Dict<IAdaptiveObject, ref<int> * (unit -> unit) * nativeint>()
        let targets = Dict<nativeint, AdaptiveToken -> unit>()

        member x.Dispose() =
            for KeyValue(o, (r, release, ptr)) in pointers do
                o.Outputs.Remove x |> ignore
                release()
                Marshal.FreeHGlobal ptr
            dirty.Clear()
            pointers.Clear()
            targets.Clear()

        override x.InputChangedObject(t : obj, o : IAdaptiveObject) =
            lock pointers (fun () ->
                match pointers.TryGetValue o with
                | (true, (_, _, ptr)) ->
                    match targets.TryGetValue ptr with
                    | (true, write) -> dirty.[o] <- write
                    | _ -> ()
                | _ ->
                    ()
            )

        member x.Remove(value : IAdaptiveObject) =
            lock pointers (fun () ->
                match pointers.TryGetValue value with
                | (true, (ref, release, ptr)) ->
                    ref := !ref - 1
                    if !ref = 0 then
                        pointers.Remove value |> ignore
                        targets.Remove ptr |> ignore
                        value.Outputs.Remove x |> ignore
                        dirty.Remove value |> ignore
                        release()
                | _ ->
                    ()
            )

        member x.Add(value : IAdaptiveObject, evaluate : AdaptiveToken -> 'a, release : unit -> unit) =
            lock pointers (fun () ->
                let ref, _, ptr = pointers.GetOrCreate(value, fun _ -> ref 0, release, Marshal.AllocHGlobal sizeof<'a>)
                ref := !ref + 1
                if !ref = 1 then
                    let write(token : AdaptiveToken) =
                        let v = evaluate token
                        NativeInt.write ptr v
                    targets.[ptr] <- write
                    x.EvaluateAlways AdaptiveToken.Top (fun t -> write t)

                { new System.IDisposable with
                    member __.Dispose() =
                        x.Remove value
                }, NativePtr.ofNativeInt<'a> ptr
            )

        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token () (fun token ->
                for write in dirty.Values do write token
            )

        interface System.IDisposable with
            member x.Dispose() = x.Dispose()

    type ProgramFragment internal(parent : FragmentProgram, block : Block<nativeint>, pinned : System.Collections.Generic.List<System.IDisposable>) =
        let mutable block = block

        static let assemble (action : IAssemblerStream -> unit) =
            use ms = new MemoryStream()
            use ass = AssemblerStream.ofStream ms
            action ass
            ms.ToArray()

        static let jumpSize =
            assemble (fun s ->
                s.Jump 34325324
            ) |> Array.length

        static let jumpDistance (lOffset : nativeint) (lSize : nativeint) (rOffset : nativeint) =
            let jumpEnd = lOffset + lSize
            int (rOffset - jumpEnd)

        let mutable prev : option<ProgramFragment> = None
        let mutable next : option<ProgramFragment> = None

        member private x.WriteJumpTo(jumpTarget : nativeint) =
            parent.Use(block, fun ptr offset size ->
                let d = jumpDistance block.Offset block.Size jumpTarget
                use m = new UnmanagedMemoryStream(NativePtr.ofNativeInt (ptr + offset + size - nativeint jumpSize), int64 jumpSize, int64 jumpSize, FileAccess.ReadWrite)
                use ass = AssemblerStream.ofStream m
                ass.Jump d
            )

        member x.Mutate(action : IAssemblerStream -> IAdaptivePinning -> unit) =
            use ms = new MemoryStream()
            use ass = AssemblerStream.ofStream ms

            let o = pinned.ToArray()
            pinned.Clear()
            let p =
                { new IAdaptivePinning() with
                    member x.Pin(value : IAdaptiveObject, eval : AdaptiveToken -> 'a, release : unit -> unit) =
                        let d, ptr = parent.Pinning.Add(value, eval, release)
                        pinned.Add d
                        ptr
                    member x.Pin(action : unit -> unit) =
                        let pd = Marshal.PinDelegate(System.Action(action))
                        pinned.Add pd
                        pd.Pointer

                }

            action ass p
            for o in o do o.Dispose()

            let jumpTarget =
                match next with
                | Some n -> n.Offset
                | None -> parent.Epilog.Offset

            let newSize =
                nativeint ms.Length +
                nativeint jumpSize

            if newSize <> block.Size then
                parent.Free block
                let n = parent.Alloc(newSize)
                block <- n
                match prev with
                | Some p -> p.WriteJumpTo block.Offset
                | None -> ()

            ass.Jump(jumpDistance block.Offset block.Size jumpTarget)
            let arr = ms.ToArray()
            parent.Use(block, fun basePtr offset _size ->
                Marshal.Copy(arr, 0, basePtr + offset, arr.Length)
            )

        member x.IsDisposed =
            parent.IsDisposed || block.IsFree

        member x.Dispose() =
            if not x.IsDisposed then
                for p in pinned do p.Dispose()
                match prev, next with
                | Some p, _     -> p.Next <- next
                | None, Some n  -> n.Prev <- None
                | _ -> ()
                prev <- None
                next <- None
                parent.Free block

        member x.Offset =
            if x.IsDisposed then raise <| System.ObjectDisposedException "CodeFragment"
            block.Offset

        member x.Prev
            with get() =
                if x.IsDisposed then raise <| System.ObjectDisposedException "CodeFragment"
                prev
            and private set p =
                if x.IsDisposed then raise <| System.ObjectDisposedException "CodeFragment"
                prev <- p

        member x.Next
            with get() =
                if x.IsDisposed then raise <| System.ObjectDisposedException "CodeFragment"
                next
            and set n =
                if x.IsDisposed then raise <| System.ObjectDisposedException "CodeFragment"
                next <- n
                let jumpTarget =
                    match n with
                    | Some n ->
                        n.Prev <- Some x
                        n.Offset
                    | None ->
                        parent.Epilog.Offset

                x.WriteJumpTo jumpTarget

        interface System.IDisposable with
            member x.Dispose() = x.Dispose()

    type FragmentProgram() as this =
        inherit AdaptiveObject()

        let mutable refCount = 1

        let memory =
            new Management.MemoryManager<_>(Memory.executable, 16n)

        let pinning = new AdaptivePinning()

        static let assemble (action : IAssemblerStream -> unit) =
            use ms = new MemoryStream()
            use ass = AssemblerStream.ofStream ms
            action ass
            ms.ToArray()

        static let epilog =
            assemble (fun s ->
                s.EndFunction()
                s.Ret()
            )

        static let jumpSize =
            assemble (fun s ->
                s.Jump 34325324
            ) |> Array.length

        static let prologSize =
            assemble (fun s ->
                s.BeginFunction()
                s.Jump 34325324
            ) |> Array.length

        static let jumpDistance (lOffset : nativeint) (lSize : nativeint) (rOffset : nativeint) =
            let jumpEnd = lOffset + lSize
            int (rOffset - jumpEnd)

        let epilog =
            lazy (
                let block = memory.Alloc(nativeint epilog.Length)
                memory.Use(block, fun basePtr offset _size ->
                    Marshal.Copy(epilog, 0, basePtr + offset, epilog.Length)
                )
                block
            )



        let mutable run : option<unit -> unit> = None
        let prologBlock =

            let block = memory.Alloc (nativeint prologSize)
            memory.Use(block, fun basePtr offset _size ->
                use ms = new MemoryStream()
                use ass = AssemblerStream.ofStream ms

                ass.BeginFunction()
                ass.Jump (jumpDistance block.Offset block.Size epilog.Value.Offset)

                let arr = ms.ToArray()
                Marshal.Copy(arr, 0, basePtr + offset, arr.Length)

            )
            block

        let mutable entryPointer =
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr (memory.UnsafePointer + prologBlock.Offset)
            ptr

        let mutable prologFragment = None

        let getProlog() =
            match prologFragment with
            | Some f -> f
            | None ->
                let f = new ProgramFragment(this, prologBlock, System.Collections.Generic.List())
                prologFragment <- Some f
                f

            //new CodeFragment(this, block)

        let fixEntry() =
            let ptr = memory.UnsafePointer + prologBlock.Offset
            let o = NativePtr.read entryPointer
            if o <> ptr then
                NativePtr.write entryPointer ptr
                run <- None

        member x.IsDisposed =
            entryPointer = NativePtr.zero

        member x.Dispose() =
            if not x.IsDisposed && System.Threading.Interlocked.Decrement(&refCount) = 0 then
                pinning.Dispose()
                memory.Dispose()
                NativePtr.free entryPointer
                entryPointer <- NativePtr.zero
                run <- None

        member internal x.Epilog : Block<nativeint> = epilog.Value

        member internal x.Alloc(newSize : nativeint) =
            let b = memory.Alloc(newSize)
            fixEntry()
            b

        member internal x.Free(b : Block<nativeint>) =
            memory.Free(b)
            fixEntry()

        member internal x.Use<'a>(block : Block<nativeint>, action : nativeint -> nativeint -> nativeint -> 'a) : 'a =
            memory.Use<'a>(block, action)

        member internal x.Pinning : AdaptivePinning = pinning

        member internal x.Acquire() =
            System.Threading.Interlocked.Increment(&refCount) |> ignore


        member x.EntryPointer =
            entryPointer

        member internal x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token () (fun token ->
                pinning.Update token
            )

        member x.Run() =
            match run with
            | Some run -> run()
            | None ->
                if x.IsDisposed then raise <| System.ObjectDisposedException "FragmentProgram"
                let ptr = NativePtr.read entryPointer
                let r = UnmanagedFunctions.wrap ptr
                run <- Some r
                r()

        member x.Run(token : AdaptiveToken) =
            x.Update token
            x.Run()

        member x.First
            with get() =
                if x.IsDisposed then raise <| System.ObjectDisposedException("FragmentProgram")
                getProlog().Next
            and set f =
                if x.IsDisposed then raise <| System.ObjectDisposedException("FragmentProgram")
                getProlog().Next <- f

        member x.NewFragment(write : IAssemblerStream -> IAdaptivePinning -> unit) =
            if x.IsDisposed then raise <| System.ObjectDisposedException("FragmentProgram")
            use ms = new MemoryStream()
            use ass = AssemblerStream.ofStream ms

            let disposables = System.Collections.Generic.List<System.IDisposable>()
            let p =
                { new IAdaptivePinning() with
                    member x.Pin(value : IAdaptiveObject, eval : AdaptiveToken -> 'a, release : unit -> unit) =
                        let d, ptr = pinning.Add(value, eval, release)
                        disposables.Add d
                        ptr
                    member x.Pin(action : unit -> unit) =
                        let pd = Marshal.PinDelegate(System.Action(action))
                        disposables.Add pd
                        pd.Pointer
                }

            write ass p

            let blockSize =
                nativeint ms.Length +
                nativeint jumpSize

            let block = memory.Alloc blockSize
            fixEntry()
            ass.Jump (jumpDistance block.Offset block.Size epilog.Value.Offset)
            let arr = ms.ToArray()

            memory.Use(block, fun basePtr offset _size ->
                Marshal.Copy(arr, 0, basePtr + offset, arr.Length)
            )

            new ProgramFragment(x, block, disposables)

        interface System.IDisposable with
            member x.Dispose() = x.Dispose()

    type IAssemblerStream with
        member x.Print (pinning : IAdaptivePinning) fmt =
            fmt |> Printf.kprintf (fun str ->
                let ptr = pinning.Pin (fun () -> Log.line "%s" str)
                x.BeginCall(0)
                x.Call ptr
            )

        member x.Call(program : FragmentProgram, pinning : IAdaptivePinning) =
            let ptr = pinning.Pin(program)
            for r in x.CalleeSavedRegisters do x.Push r
            x.BeginCall 0
            x.CallIndirect ptr
            for r in x.CalleeSavedRegisters do x.Pop r

