namespace Aardvark.Rendering.GL

open System
open System.Security
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base.Incremental
open FShade
open Microsoft.FSharp.NativeInterop
open Aardvark.Base.ShaderReflection

#nowarn "9"

[<SuppressUnmanagedCodeSecurity>]
type private DispatchComputeGroupSizeARBDelegate = delegate of uint32 * uint32 * uint32 * uint32 * uint32 * uint32 -> unit

type internal Bound =
    | Texture of slot : int * target : TextureTarget
    | Buffer  of slot : int * target : BufferTarget
    | Image of slot : int

type ComputeShaderInputBinding(shader : ComputeShader) =
    let ctx = shader.Context
    let mutable dirtyBuffers = HashSet<UniformBuffer>()
    let mutable references : Map<string, list<ComputeShaderInputReference>>  = Map.empty

    let addReference (name : string) (r : ComputeShaderInputReference) =
        let o = Map.tryFind name references |> Option.defaultValue []
        references <- Map.add name (r :: o) references


    let uniformLocations : list<int * UniformLocation> = 
        shader.Uniforms |> List.map (fun l ->
            failwith "[GL] not implemented"
        )

    let inputImages =
        shader.Images |> List.map (fun (l, b, name, valueType, dim, isMS, isArray) ->
            addReference name (Image b)
            b, (Texture.empty, 0, false, 0)
        ) |> Dictionary.ofList

    let inputBuffers =
        shader.Buffers |> List.map (fun (l,name,t) ->
            addReference name (ComputeShaderInputReference.StorageBuffer(l, t))
            let empty = new GL.Buffer(shader.Context, 0n, 0)
            l, (empty, 0n, 0n)
        )
        |> Dictionary.ofList

    let inputSamplers =
        shader.Samplers |> List.map (fun (l, b, name, valueType, dim, isMS, isArray, isShadow, sampler) ->
            let target = TextureTarget.ofParameters dim isArray isMS
            addReference name (Texture b)
            b, (target, Texture.empty, sampler)
        ) |> Dictionary.ofList

    let uniformBuffers = 
        shader.UniformBlocks |> List.map (fun b ->
            let buffer = ctx.CreateUniformBuffer(b)

            for f in b.Fields do
                let write (o : obj) =
                    match o with
                        | null -> ()
                        | o ->
                            let t = o.GetType()
                            let sem = ShaderParameterWriter.get t f.Type 
                            sem.WriteUnsafe(buffer.Data + nativeint f.Offset, o)
                            buffer.Dirty <- true
                            lock dirtyBuffers (fun () -> dirtyBuffers.Add buffer |> ignore)

                let name = ShaderPath.name f.Path

                let nameFixed =
                    if name.StartsWith "cs_" then name.Substring(3) else name

                let ref = ComputeShaderInputReference.Uniform(write)
                addReference nameFixed ref

            b.Index, buffer
        )

    member internal x.Bind(boundThings : HashSet<Bound>) =
        for (l, b) in uniformBuffers do
            ctx.Upload b
            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, l, b.Handle, 0n, nativeint b.Size)
            boundThings.Add(Buffer(l,BufferTarget.UniformBuffer)) |> ignore
            GL.Check "could not bind uniform buffer"

        for (KeyValue(slot, (b, o, s))) in inputBuffers do
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, slot, b.Handle, o, s)
            boundThings.Add(Buffer(slot,BufferTarget.ShaderStorageBuffer)) |> ignore
            GL.Check "could not bind storage buffer"

        for (KeyValue(l, (tex, level, layered, layer))) in inputImages do
            GL.BindImageTexture(l, tex.Handle, level, layered, layer, TextureAccess.ReadWrite, unbox (int tex.Format))
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
                                | :? IBufferRange as range ->
                                    let buffer = unbox<GL.Buffer> range.Buffer
                                    inputBuffers.[slot] <- (buffer, range.Offset, range.Size)

                                | :? GL.Buffer as b ->
                                    inputBuffers.[slot] <- (b, 0n, b.SizeInBytes)

                                | _ ->
                                    failwithf "[GL] bad buffer: %A" value
                                    

                        | Image slot ->
                            let (t, level, layered, layer) = inputImages.[slot]
                            match value with
                                | :? ITextureLevel as l ->
                                    let t = l.Texture |> unbox<Texture>
                                    let isLayered = l.Slices.Min <> l.Slices.Max
                                    inputImages.[slot] <- (t, l.Level, isLayered, l.Slices.Min)
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
                let arr = HashSet.toArray dirtyBuffers
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
        iface.StorageBlocks |> List.map (fun b -> 
            match b.Fields with
                | [f] -> 
                    match f.Path with
                        | ShaderPath.Value name -> name, f.Type
                        | _ -> failwith "[FShade] found structured storage buffer (not supported atm.)"
                | _ -> 
                    failwith "[FShade] found structured storage buffer (not supported atm.)"
        ) |> Map.ofList

    let buffers =
        iface.StorageBlocks |> List.map (fun b ->
            match b.Fields with
                | [f] -> 
                    match f.Path with
                        | ShaderPath.Value name -> b.Index, name, f.Type
                        | _ -> failwith "[FShade] found structured storage buffer (not supported atm.)"
                | _ -> 
                    failwith "[FShade] found structured storage buffer (not supported atm.)"
        )

    let images =
        iface.Uniforms |> List.choose (fun u ->
            match u.Type with
                | ShaderParameterType.Image(valueType,dim,isMS,isArray) ->
                    match u.Path with
                        | ShaderPath.Value name ->
                            if name.StartsWith "cs_" then
                                                    
                                Some (u.Location, u.Binding, name.Substring 3, valueType, dim, isMS, isArray)
                            else
                                failwithf "[FShade] found non-primitive image uniform %A" u
                        | _ ->
                            failwithf "[FShade] found non-primitive image uniform %A" u
                                            
                | _ ->
                    None
        )

    let samplers =
        iface.Uniforms |> List.choose (fun u ->
            match u.Type with
                | ShaderParameterType.FixedArray(ShaderParameterType.Sampler(valueType,dim,isMS,isArray,isShadow),_,l) ->
                    failwith "not implemented"

                | ShaderParameterType.Sampler(valueType,dim,isMS,isArray,isShadow) ->
                    match u.Path with
                        | ShaderPath.Value name ->
                            match Map.tryFind (name,0) prog.TextureInfo with
                                | Some info ->
                                    let sampler = ctx.CreateSampler info.samplerState
                                    Some (u.Location, u.Binding, string info.textureName, valueType, dim, isMS, isArray, isShadow, sampler)

                                | _ ->
                                    failwithf "[FShade] found non-primitive image uniform %A" u
                        
                        | _ ->
                            failwithf "[FShade] found non-primitive image uniform %A" u
                                            
                | _ ->
                    None
        )

    let uniforms =
        iface.Uniforms |> List.choose (fun u ->
            match u.Type with
                | ShaderParameterType.Image _ -> None
                | ShaderParameterType.Sampler _ -> None
                | _ -> Some u
        )

    let uniformBlocks = iface.UniformBlocks

    member x.Context : Context = ctx
    member x.Buffers : list<int * string * ShaderParameterType> = buffers
    member x.Images : list<int * int * string * ShaderParameterType * TextureDimension * bool * bool> = images
    member x.Samplers : list<int * int * string * ShaderParameterType * TextureDimension * bool * bool * bool * Sampler> = samplers
    member x.Uniforms : list<ShaderParameter>  = uniforms
    member x.UniformBlocks : list<ShaderBlock> = uniformBlocks
    member x.Handle = prog.Handle

    member x.Dispose() =
        use __ = ctx.ResourceLock
        for (_,_,_,_,_,_,_,_,s) in samplers do
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



    member private x.Run(i : ComputeCommand, boundThings : HashSet<Bound>) =
        match i with
            | ComputeCommand.BindCmd shader ->
                let shader = unbox<ComputeShader> shader
                GL.UseProgram(shader.Handle)
            | ComputeCommand.SetInputCmd(input) ->
                let input = unbox<ComputeShaderInputBinding> input
                input.Bind(boundThings)
            | ComputeCommand.DispatchCmd groups ->
                GL.DispatchCompute(groups.X, groups.Y, groups.Z)

            | ComputeCommand.SyncBufferCmd _ -> 
                GL.MemoryBarrier(MemoryBarrierFlags.BufferUpdateBarrierBit ||| MemoryBarrierFlags.ClientMappedBufferBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit)

             | ComputeCommand.SyncImageCmd _ -> 
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit ||| MemoryBarrierFlags.TextureFetchBarrierBit ||| MemoryBarrierFlags.TextureUpdateBarrierBit)

            | ComputeCommand.TransformLayoutCmd _ ->
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit ||| MemoryBarrierFlags.TextureFetchBarrierBit ||| MemoryBarrierFlags.TextureUpdateBarrierBit ||| MemoryBarrierFlags.BufferUpdateBarrierBit ||| MemoryBarrierFlags.ClientMappedBufferBarrierBit ||| MemoryBarrierFlags.ShaderStorageBarrierBit)

            | ComputeCommand.CopyBufferCmd(src, dst) ->
                let srcBuffer = unbox<GL.Buffer> src.Buffer
                let dstBuffer = unbox<GL.Buffer> dst.Buffer
                ctx.Copy(srcBuffer, src.Offset, dstBuffer, dst.Offset, src.Size)
                ()
                
            | ComputeCommand.UploadBufferCmd _ 
            | ComputeCommand.CopyImageCmd _ ->
                failwith "please implement"

            | ComputeCommand.SetBufferCmd(dst, value) ->
                let dstBuffer = unbox<GL.Buffer> dst.Buffer
                let gc = GCHandle.Alloc(value, GCHandleType.Pinned)
                GL.NamedClearBufferSubData(dstBuffer.Handle, PixelInternalFormat.R32ui, dst.Offset, dst.Size, PixelFormat.Red, PixelType.UnsignedInt, gc.AddrOfPinnedObject())
                gc.Free()

            | ComputeCommand.DownloadBufferCmd(src, dst) ->
                let srcBuffer = unbox<GL.Buffer> src.Buffer
                match dst with
                    | HostMemory.Managed(arr,index) ->
                        let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                        let es = Marshal.SizeOf (arr.GetType().GetElementType()) |> nativeint
                        GL.GetNamedBufferSubData(srcBuffer.Handle, src.Offset, src.Size, gc.AddrOfPinnedObject() + es * nativeint index)
                        gc.Free()

                    | HostMemory.Unmanaged ptr ->   
                        GL.GetNamedBufferSubData(srcBuffer.Handle, src.Offset, src.Size, ptr)
            | ComputeCommand.ExecuteCmd other ->
                other.Run()
    
    member x.Run(i : list<ComputeCommand>) =
        use __ = ctx.ResourceLock
        let boundThings = HashSet<_>()
        for i in i do x.Run(i, boundThings)
        for b in boundThings do
            match b with
                | Bound.Buffer(slot,target) -> 
                    GL.BindBufferBase(unbox (int target),slot,0)
                | Bound.Image(slot) -> 
                    GL.BindImageTexture(slot,0,0,false,0,TextureAccess.ReadWrite,SizedInternalFormat.Rgba8)
                | Bound.Texture(slot,target) -> 
                    GL.ActiveTexture(TextureUnit.Texture0 + unbox slot)
                    GL.BindTexture(target,0)
                    GL.BindSampler(slot,0)
        GL.Sync()

[<AutoOpen>]
module GLComputeExtensions =
    let private computeInstanceCache = System.Collections.Concurrent.ConcurrentDictionary<Context, GLCompute>()

    let private getGLCompute (ctx : Context) =
        computeInstanceCache.GetOrAdd(ctx, fun ctx -> GLCompute(ctx))

    type Context with

        member x.TryCompileKernel (code : string, textureInfo : Map<_,_>, localSize : V3i) =
            using x.ResourceLock (fun token ->
                match x.TryCompileCompute(true, code) with
                    | Success prog ->
                        let kernel = new ComputeShader({ prog with TextureInfo = textureInfo }, localSize)
                        Success kernel

                    | Error err ->
                        Error err
            )

        member x.TryCompileKernel (shader : FShade.ComputeShader) =
            let gl = getGLCompute x
            let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL410
            let localSize = 
                if shader.csLocalSize.AllGreater 0 then shader.csLocalSize
                else failwith "[GL] compute shader has no local size"

            printfn "%s" glsl.code

            let samplerDescriptions =
                shader.csTextureNames |> Map.map (fun (name, index) texName ->
                    let samplerState =
                        match Map.tryFind (name, index) shader.csSamplerStates with
                            | Some sam -> sam
                            | _ -> SamplerState.empty
                    { textureName = Symbol.Create texName; samplerState = samplerState.SamplerStateDescription }
                )

            x.TryCompileKernel(glsl.code, samplerDescriptions, localSize)

        member x.CompileKernel (shader : FShade.ComputeShader) =
            match x.TryCompileKernel shader with
                | Success kernel -> 
                    kernel
                | Error err ->
                    Log.error "%s" err
                    failwith err
    
        member x.Delete(k : ComputeShader) =
            k.Dispose()

        member x.Run(i : list<ComputeCommand>) =
            let c = getGLCompute x
            c.Run i
