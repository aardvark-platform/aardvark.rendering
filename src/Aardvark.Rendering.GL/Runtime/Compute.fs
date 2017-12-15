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
            failwith ""
        )

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
                            lock dirtyBuffers (fun () -> dirtyBuffers.Add buffer |> ignore)

                let name = ShaderPath.name f.Path
                let ref = ComputeShaderInputReference.Uniform(write)
                addReference name ref

            b.Index, buffer
        )

    member x.Bind() =
        for (l, b) in uniformBuffers do
            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, l, b.Handle, 0n, nativeint b.Size)
            GL.Check "could not bind uniform buffer"

        for (KeyValue(l, (tex, level, layered, layer))) in inputImages do
            GL.BindImageTexture(l, tex.Handle, level, layered, layer, TextureAccess.ReadWrite, unbox (int tex.Format))
            GL.Check "could not bind image texture"

        for (KeyValue(l, (target, tex, sampler))) in inputSamplers do
            GL.ActiveTexture(TextureUnit.Texture0 + unbox l)
            GL.BindTexture(target, tex.Handle)
            GL.Check "could not bind texture"
            GL.BindSampler(l, sampler.Handle)
            GL.Check "could not bind sampler"

    member private x.Write(name : string, value : obj) =
        match Map.tryFind name references with
            | Some refs ->
                for ref in refs do
                    match ref with
                        | Uniform write -> 
                            write value

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

    let uniformBlocks =
        iface.UniformBlocks |> List.choose (fun b ->
            if b.Name = "Arguments" then
                let res = 
                    { b with
                        Fields = b.Fields |> List.map (fun f ->
                            match f.Path with
                                | ShaderPath.Value name -> { f with Path = ShaderPath.Value (name.Substring 3) }
                                | _ -> f
                        )
                    }

                Some res
            else
                Some b
        )

    member x.Context : Context = ctx
    member x.Buffers : list<int * string * ShaderParameterType> = buffers
    member x.Images : list<int * int * string * ShaderParameterType * TextureDimension * bool * bool> = images
    member x.Samplers : list<int * int * string * ShaderParameterType * TextureDimension * bool * bool * bool * Sampler> = samplers
    member x.Uniforms : list<ShaderParameter>  = uniforms
    member x.UniformBlocks : list<ShaderBlock> = uniformBlocks

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

