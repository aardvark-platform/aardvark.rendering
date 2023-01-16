namespace Aardvark.Rendering.GL

open System
open System.Security
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK.Graphics.OpenGL4
open FSharp.Data.Adaptive
open FShade
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.GL

#nowarn "9"

open Aardvark.Base.Runtime

[<SuppressUnmanagedCodeSecurity>]
type private DispatchComputeGroupSizeARBDelegate = delegate of uint32 * uint32 * uint32 * uint32 * uint32 * uint32 -> unit

type internal Bound =
    | Texture of slot : int * target : TextureTarget
    | Buffer  of slot : int * target : BufferTarget
    | Image of slot : int

type ComputeShaderInputBinding(shader : ComputeShader) =
    let ctx = shader.Context
    let mutable dirtyBuffers = System.Collections.Generic.HashSet<UniformBuffer>()
    let mutable references : Map<string, list<ComputeShaderInputReference>>  = Map.empty

    let addReference (name : string) (r : ComputeShaderInputReference) =
        let names =
            if name.StartsWith "cs_" then [name; name.Substring 3]
            else [name]
        for name in names do
            let o = Map.tryFind name references |> Option.defaultValue []
            references <- Map.add name (r :: o) references


    //let uniformLocations : list<int * UniformLocation> = 
    //    shader.Uniforms |> List.map (fun l ->
    //        failwith "[GL] not implemented"
    //    )

    let inputImages =
        shader.Images |> List.map (fun (b, name, typ) ->
            addReference name (Image b)
            b, (Texture.empty, 0, false, 0, typ)
        ) |> Dictionary.ofList

    let inputBuffers =
        shader.Buffers |> List.map (fun (l,name,t) ->
            addReference name (ComputeShaderInputReference.StorageBuffer(l, ShaderParameterType.ofGLSLType t))
            let empty = new GL.Buffer(shader.Context, 0n, 0)
            l, (empty, 0n, 0n)
        )
        |> Dictionary.ofList

    let inputSamplers =
        shader.Samplers |> List.map (fun (b, name, sampler) ->
            let target = TextureTarget.Texture2D //TODO: wrong here //TextureTarget.ofParameters dim isArray isMS
            addReference name (Texture b)
            b, (target, Texture.empty, sampler)
        ) |> Dictionary.ofList

    let uniformBuffers = 
        shader.UniformBlocks |> List.map (fun b ->
            let buffer = ctx.CreateUniformBuffer(nativeint b.ubSize)

            for f in b.ubFields do
                let write (o : obj) =
                    match o with
                        | null -> ()
                        | o ->
                            let t = o.GetType()
                            let sem = ShaderParameterWriter.get t (ShaderParameterType.ofGLSLType f.ufType)
                            sem.WriteUnsafe(buffer.Data + nativeint f.ufOffset, o)
                            buffer.Dirty <- true
                            lock dirtyBuffers (fun () -> dirtyBuffers.Add buffer |> ignore)

                let name = f.ufName

                let nameFixed =
                    if name.StartsWith "cs_" then name.Substring(3) else name

                let ref = ComputeShaderInputReference.Uniform(write)
                addReference nameFixed ref

            b.ubBinding, buffer
        )

    member x.CompileBind(stream : ICommandStream) =
        for (l, b) in uniformBuffers do
            ctx.Upload b
            stream.BindBufferRange(BufferRangeTarget.UniformBuffer, l, b.Handle, 0n, nativeint b.Size)
        
        for (KeyValue(slot, (b, o, s))) in inputBuffers do
            stream.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, slot, b.Handle, o, s)
            
        for (KeyValue(l, (tex, level, layered, layer, _))) in inputImages do
            stream.BindImageTexture(l, tex.Handle, level, layered, layer, TextureAccess.ReadWrite, tex.Format)
            
        for (KeyValue(l, (target, tex, sampler))) in inputSamplers do
            stream.SetActiveTexture(int (TextureUnit.Texture0 + unbox l))
            stream.BindTexture(target, tex.Handle)
            stream.BindSampler(l, sampler.Handle)

    member internal x.Bind(boundThings : System.Collections.Generic.HashSet<Bound>) =
        for (l, b) in uniformBuffers do
            ctx.Upload b
            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, l, b.Handle, 0n, nativeint b.Size)
            boundThings.Add(Buffer(l,BufferTarget.UniformBuffer)) |> ignore
            GL.Check "could not bind uniform buffer"

        for (KeyValue(slot, (b, o, s))) in inputBuffers do
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, slot, b.Handle, o, s)
            boundThings.Add(Buffer(slot,BufferTarget.ShaderStorageBuffer)) |> ignore
            GL.Check "could not bind storage buffer"

        for (KeyValue(l, (tex, level, layered, layer, _))) in inputImages do
            GL.Dispatch.BindImageTexture(l, tex.Handle, level, layered, layer, TextureAccess.ReadWrite, TextureFormat.toSizedInternalFormat tex.Format)
            boundThings.Add(Bound.Image l) |> ignore
            GL.Check "could not bind image texture"

        for (KeyValue(l, (target, tex, sampler))) in inputSamplers do
            GL.ActiveTexture(TextureUnit.Texture0 + unbox l)
            GL.BindTexture(target, tex.Handle)
            GL.Check "could not bind texture"
            GL.BindSampler(l, sampler.Handle)
            GL.Check "could not bind sampler"
            boundThings.Add(Bound.Texture(l,target)) |> ignore

    member private x.Write(name : string, value : obj) =
        match Map.tryFind name references with
        | Some refs ->
            for ref in refs do
                match ref with
                | Uniform write -> 
                    write value

                | StorageBuffer(slot, _) ->
                    match value with
                    | null ->
                        inputBuffers.[slot] <- (new Aardvark.Rendering.GL.Buffer(ctx, 0n, 0), 0n, 0n)
                    | :? IBufferRange as range ->
                        let buffer = unbox<GL.Buffer> range.Buffer
                        inputBuffers.[slot] <- (buffer, range.Offset, range.Size)

                    | :? GL.Buffer as b ->
                        inputBuffers.[slot] <- (b, 0n, b.SizeInBytes)

                    | _ ->
                        failwithf "[GL] bad buffer: %A" value


                | Image slot ->
                    let (_, _, _, _, typ) = inputImages.[slot]
                    let expectedFormat = typ.format |> Option.map unbox<TextureFormat>

                    match value with
                    | :? ITextureLevel as l ->
                        let t = l.Texture |> unbox<Texture>

                        let isValidFormat =
                            expectedFormat |> Option.map ((=) t.Format) |> Option.defaultValue true

                        if not isValidFormat then
                            failwithf "[GL] Expected image '%s' with format %A but got %A" name expectedFormat.Value t.Format

                        let isLayered = l.Slices.Min <> l.Slices.Max
                        inputImages.[slot] <- (t, l.Level, isLayered, l.Slices.Min, typ)
                    | _ ->
                        failwithf "[GL] bad image texture: %A" value
                | Texture slot ->
                    let (target, t, s) = inputSamplers.[slot]
                    match value with
                    | :? Texture as t ->
                        inputSamplers.[slot] <- (target, t, s)
                    | _ ->
                        failwithf "[GL] bad texture: %A" value
            | None ->
                ()
    
    member x.Dispose() =
        use __ = ctx.ResourceLock
        for (_,b) in uniformBuffers do
            ctx.Delete b

    member x.Flush() =
        use __ = ctx.ResourceLock
        let dirty = 
            lock dirtyBuffers (fun () -> 
                let arr = Aardvark.Base.HashSet.toArray dirtyBuffers
                dirtyBuffers.Clear()
                arr
            )
        for d in dirty do ctx.Upload d

    member x.Item
        with set (name : string) (value : obj) = x.Write(name, value)

    interface IComputeShaderInputBinding with
        member x.Dispose() = x.Dispose()
        member x.Flush() = x.Flush()
        member x.Shader = shader :> IComputeShader
        member x.Item
            with set (name : string) (value : obj) = x.Write(name, value)

and private ComputeShaderInputReference =
    | Uniform of write : (obj -> unit)
    | Image of slot : int
    | Texture of slot : int
    | StorageBuffer of slot : int * storageType : ShaderParameterType

and ComputeShader(prog : Program, localSize : V3i) =
    let mutable isDisposed = 0

    let ctx = prog.Context
    let iface = prog.Interface

    let bufferTypes =
        iface.storageBuffers |> MapExt.map (fun _ b -> 
            b.ssbType

        )

    let buffers =
        iface.storageBuffers |> MapExt.toList |> List.map (fun (_,b) ->
            b.ssbBinding, b.ssbName, b.ssbType
            //match b.Fields with
            //    | [f] -> 
            //        match f.Path with
            //            | ShaderPath.Value name -> b.Index, name, f.Type
            //            | _ -> failwith "[FShade] found structured storage buffer (not supported atm.)"
            //    | _ -> 
            //        failwith "[FShade] found structured storage buffer (not supported atm.)"
        )

    let images =
        iface.images |> MapExt.toList |> List.map (fun (_,u) ->
            u.imageBinding, u.imageName, u.imageType
            //match u.Type with
            //    | ShaderParameterType.Image(valueType,dim,isMS,isArray) ->
            //        match u.Path with
            //            | ShaderPath.Value name ->
            //                if name.StartsWith "cs_" then
                                                    
            //                    Some (u.Location, u.Binding, name.Substring 3, valueType, dim, isMS, isArray)
            //                else
            //                    failwithf "[FShade] found non-primitive image uniform %A" u
            //            | _ ->
            //                failwithf "[FShade] found non-primitive image uniform %A" u
                                            
            //    | _ ->
            //        None
        )

    let samplers =
        iface.samplers |> MapExt.toList |> List.map (fun (_,u) ->
            match u.samplerTextures with
                | [(name, state)] ->
                    u.samplerBinding, name, ctx.CreateSampler state.SamplerState
                | _ ->
                    failwith "not implemented"
            //match u.Type with
            //    | ShaderParameterType.FixedArray(ShaderParameterType.Sampler(valueType,dim,isMS,isArray,isShadow),_,l) ->
            //        failwith "not implemented"

            //    | ShaderParameterType.Sampler(valueType,dim,isMS,isArray,isShadow) ->
            //        match u.Path with
            //            | ShaderPath.Value name ->
            //                match Map.tryFind (name,0) prog.TextureInfo with
            //                    | Some info ->
            //                        let sampler = ctx.CreateSampler info.samplerState
            //                        Some (u.Location, u.Binding, string info.textureName, valueType, dim, isMS, isArray, isShadow, sampler)

            //                    | _ ->
            //                        failwithf "[FShade] found non-primitive image uniform %A" u
                        
            //            | _ ->
            //                failwithf "[FShade] found non-primitive image uniform %A" u
                                            
            //    | _ ->
            //        None
        )

    //let uniforms =
    //    iface.Uniforms |> List.choose (fun u ->
    //        match u.Type with
    //            | ShaderParameterType.Image _ -> None
    //            | ShaderParameterType.Sampler _ -> None
    //            | _ -> Some u
    //    )

    let uniformBlocks = iface.uniformBuffers

    member x.Context : Context = ctx
    member x.Buffers : list<int * string * GLSL.GLSLType>  = buffers
    member x.Images : list<int * string * GLSL.GLSLImageType> = images
    member x.Samplers : list<int * string * Sampler> = samplers
    member x.UniformBlocks : list<GLSL.GLSLUniformBuffer> = MapExt.toList uniformBlocks |> List.map snd
    member x.Handle = prog.Handle

    member x.Dispose() =
        use __ = ctx.ResourceLock
        for (_,_,s) in samplers do
            ctx.Delete s

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IComputeShader with
        member x.LocalSize = localSize
        member x.Runtime = x.Context.Runtime :> IComputeRuntime
    
type private GLCompute(ctx : Context) =
    let mutable workGroupSize = V3i.Zero
    let mutable workGroupInvocations = 0
    let mutable hasDynamicCompute = false
    let mutable glDispatchCompute = Unchecked.defaultof<DispatchComputeGroupSizeARBDelegate>
    do 
        use __ = ctx.ResourceLock
        GL.GetInteger(unbox (int All.MaxComputeWorkGroupSize), 0, &workGroupSize.X)
        GL.GetInteger(unbox (int All.MaxComputeWorkGroupSize), 1, &workGroupSize.Y)
        GL.GetInteger(unbox (int All.MaxComputeWorkGroupSize), 2, &workGroupSize.Z)
        GL.GetInteger(unbox (int All.MaxComputeWorkGroupInvocations), &workGroupInvocations)


        let c = ctx.CurrentContextHandle.Value.Handle |> unbox<OpenTK.Graphics.IGraphicsContextInternal>
        let ptr = c.GetAddress "glDispatchComputeGroupSizeARB"
        if ptr <> 0n then
            hasDynamicCompute <- true
            glDispatchCompute <-System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof<DispatchComputeGroupSizeARBDelegate>) |> unbox<DispatchComputeGroupSizeARBDelegate>

    member x.WorkGroupSize = workGroupSize
    member x.WorkGroupInvocations = workGroupInvocations

    member x.DispatchCompute(groups : V3i, local : V3i) =
        if not hasDynamicCompute then failwith "[GL] cannot invoke kernel with variable local-size (GL does not support it)"
        glDispatchCompute.Invoke(uint32 groups.X, uint32 groups.Y, uint32 groups.Z, uint32 local.X, uint32 local.Y, uint32 local.Z)
            
    member x.DispatchCompute(groups : V3i) =
        GL.DispatchCompute(groups.X, groups.Y, groups.Z)



    member private x.Run(i : ComputeCommand, boundThings : System.Collections.Generic.HashSet<Bound>, queries : IQuery) =
        match i with
            | ComputeCommand.BindCmd shader ->
                let shader = unbox<ComputeShader> shader
                GL.UseProgram(shader.Handle)
            | ComputeCommand.SetInputCmd(input) ->
                let input = unbox<ComputeShaderInputBinding> input
                input.Bind(boundThings)
                GL.Sync()
                GL.Check()
            | ComputeCommand.DispatchCmd groups ->
                GL.DispatchCompute(groups.X, groups.Y, groups.Z)
                GL.Sync()
                GL.Check()

            | ComputeCommand.SyncBufferCmd _ -> 
                GL.MemoryBarrier(MemoryBarrierFlags.BufferUpdateBarrierBit 
                                 ||| MemoryBarrierFlags.ClientMappedBufferBarrierBit 
                                 ||| MemoryBarrierFlags.ShaderStorageBarrierBit
                                 ||| MemoryBarrierFlags.AllBarrierBits)
                GL.Sync()
                GL.Check()

             | ComputeCommand.SyncImageCmd _ -> 
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit ||| MemoryBarrierFlags.TextureFetchBarrierBit ||| MemoryBarrierFlags.TextureUpdateBarrierBit ||| MemoryBarrierFlags.AllBarrierBits)
                GL.Sync()
                GL.Check()
            | ComputeCommand.TransformLayoutCmd _ | ComputeCommand.TransformSubLayoutCmd _ ->
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit ||| MemoryBarrierFlags.TextureFetchBarrierBit ||| MemoryBarrierFlags.TextureUpdateBarrierBit ||| MemoryBarrierFlags.BufferUpdateBarrierBit ||| MemoryBarrierFlags.ClientMappedBufferBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit ||| MemoryBarrierFlags.AllBarrierBits)
                GL.Sync()
                GL.Check()

            | ComputeCommand.CopyBufferCmd(src, dst) ->
                GL.Sync()
                GL.Check()
                let srcBuffer = unbox<GL.Buffer> src.Buffer
                let dstBuffer = unbox<GL.Buffer> dst.Buffer
                ctx.Copy(srcBuffer, src.Offset, dstBuffer, dst.Offset, src.Size)
                GL.Sync()
                GL.Check()
                ()

            | ComputeCommand.UploadBufferCmd(src, dst) ->
                match src with
                | HostMemory.Managed(arr, index) ->
                    let elementSize = arr.GetType().GetElementType() |> Marshal.SizeOf |> nativeint
                    let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                    try
                        let ptr = gc.AddrOfPinnedObject() + (nativeint index * elementSize)
                        ctx.Runtime.Upload(ptr, dst.Buffer, dst.Offset, dst.Size)
                    finally
                        gc.Free()

                | HostMemory.Unmanaged ptr ->
                    ctx.Runtime.Upload(ptr, dst.Buffer, dst.Offset, dst.Size)

            | ComputeCommand.CopyImageCmd(src, srcOffset, dst, dstOffset, size) ->
                ctx.Runtime.Copy(src, srcOffset, dst, dstOffset, size)

            | ComputeCommand.SetBufferCmd(dst, value) ->
                let dstBuffer = unbox<GL.Buffer> dst.Buffer
                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                GL.Dispatch.ClearNamedBufferSubData(dstBuffer.Handle, PixelInternalFormat.R32ui, dst.Offset, dst.Size, PixelFormat.Red, PixelType.UnsignedInt, gc.AddrOfPinnedObject())
                gc.Free()
                GL.Sync()
                GL.Check()

            | ComputeCommand.DownloadBufferCmd(src, dst) ->
                GL.Sync()
                GL.Check()
                let srcBuffer = unbox<GL.Buffer> src.Buffer
                match dst with
                    | HostMemory.Managed(arr,index) ->
                        let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                        let es = Marshal.SizeOf (arr.GetType().GetElementType()) |> nativeint
                        GL.Dispatch.GetNamedBufferSubData(srcBuffer.Handle, src.Offset, src.Size, gc.AddrOfPinnedObject() + es * nativeint index)
                        gc.Free()

                    | HostMemory.Unmanaged ptr ->   
                        GL.Dispatch.GetNamedBufferSubData(srcBuffer.Handle, src.Offset, src.Size, ptr)
            | ComputeCommand.ExecuteCmd other ->
                other.Run(queries)
    
    member x.Run(i : list<ComputeCommand>, queries : IQuery) =
        use __ = ctx.ResourceLock

        queries.Begin()

        let boundThings = System.Collections.Generic.HashSet<_>()
        for i in i do x.Run(i, boundThings, queries)
        for b in boundThings do
            match b with
                | Bound.Buffer(slot,target) -> 
                    GL.BindBufferBase(unbox (int target),slot,0)
                | Bound.Image(slot) -> 
                    GL.Dispatch.BindImageTexture(slot,0,0,false,0,TextureAccess.ReadWrite,SizedInternalFormat.Rgba8)
                | Bound.Texture(slot,target) -> 
                    GL.ActiveTexture(TextureUnit.Texture0 + unbox slot)
                    GL.BindTexture(target,0)
                    GL.BindSampler(slot,0)

        queries.End()

        GL.Sync()

[<AutoOpen>]
module GLComputeExtensions =
    let private computeInstanceCache = System.Collections.Concurrent.ConcurrentDictionary<Context, GLCompute>()

    let private getGLCompute (ctx : Context) =
        computeInstanceCache.GetOrAdd(ctx, fun ctx -> GLCompute(ctx))

    type Context with

        member x.TryCompileKernel (id : string, code : string, iface : FShade.GLSL.GLSLProgramInterface, localSize : V3i) =
            use __ = x.ResourceLock

            match x.TryCompileComputeProgram(id, code, iface) with
            | Success program ->
                let kernel = new ComputeShader(program, localSize)
                Success kernel

            | Error err ->
                Error err

        [<Obsolete>]
        member x.TryCompileKernel (code : string, iface : FShade.GLSL.GLSLProgramInterface, localSize : V3i) =
            x.TryCompileKernel(code, code, iface, localSize)

        member x.TryCompileKernel (shader : FShade.ComputeShader) =
            let gl = getGLCompute x
            let glsl = 
                if x.Driver.glsl >= Version(4,3,0) then
                    shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL430
                elif 
                    x.Driver.extensions |> Set.contains "GL_ARB_compute_shader" && 
                    x.Driver.extensions |> Set.contains "GL_ARB_shading_language_420pack" && 
                    x.Driver.glsl >= Version(4,1,0) then 
                    let be = 
                        FShade.GLSL.Backend.Create 
                            { glsl410.Config with 
                                enabledExtensions = 
                                    glsl410.Config.enabledExtensions
                                    |> Set.add "GL_ARB_compute_shader"  
                                    |> Set.add "GL_ARB_shading_language_420pack" 
                            }
                    shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL be
                else 
                    failwithf "[GL] Compute shader not supported: GLSL version = %A" x.Driver.glsl

            let localSize = 
                if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
                else failwith "[GL] compute shader has no local size"

            let adjust (s : GLSL.GLSLSampler) =
                let textures =
                    List.init s.samplerCount (fun i -> 
                        let texName = 
                            match Map.tryFind (s.samplerName, i) shader.csTextureNames with
                                | Some ti -> ti
                                | _ -> s.samplerName
                        let samplerState =
                            match Map.tryFind (s.samplerName, i) shader.csSamplerStates with
                                | Some sam -> sam
                                | _ -> SamplerState.empty
                        texName, samplerState
                    )
                { s with samplerTextures = textures }

            let iface = { glsl.iface with samplers = glsl.iface.samplers |> MapExt.map (constF adjust) }
            //glsl.iface.samplers
            x.TryCompileKernel(shader.csId, glsl.code, iface, localSize)

        member x.CompileKernel (shader : FShade.ComputeShader) =
            match x.TryCompileKernel shader with
                | Success kernel -> 
                    kernel
                | Error err ->
                    Log.error "%s" err
                    failwith err
    
        member x.Delete(k : ComputeShader) =
            k.Dispose()

        member x.Run(i : list<ComputeCommand>, queries : IQuery) =
            let c = getGLCompute x
            c.Run(i, queries)
