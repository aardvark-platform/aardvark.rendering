namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection

[<AutoOpen>]
module ComputeTest =
    open OpenTK.Graphics.OpenGL4
    open Aardvark.Rendering.GL
    open Microsoft.FSharp.NativeInterop
    open FShade
    open FShade.GLSL
    open Microsoft.FSharp.Quotations

    type DispatchComputeGroupSizeARBDelegate = delegate of uint32 * uint32 * uint32 * uint32 * uint32 * uint32 -> unit

    
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

    let private computeInstanceCache = System.Collections.Concurrent.ConcurrentDictionary<Context, GLCompute>()

    let private getGLCompute (ctx : Context) =
        computeInstanceCache.GetOrAdd(ctx, fun ctx -> GLCompute(ctx))

    type Kernel(prog : Program, localSize : Option<V3i>) =   
        let GLCompute = getGLCompute prog.Context
        
        

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

        let uniforms =
            iface.Uniforms |> List.choose (fun u ->
                match u.Type with
                    | ShaderParameterType.Image _ -> None
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

        let intWriter : ShaderParameterWriter.Writer<int> = ShaderParameterWriter.writer ShaderParameterType.Int
        let createArguments (get : string -> Option<obj>) =
            uniformBlocks |> List.map (fun b ->
                let res = ctx.CreateUniformBuffer b

                for f in b.Fields do
                    match f.Path with
                        | ShaderPath.Value name ->
                            if name.EndsWith "_length" then
                                let name = name.Substring(0, name.Length - 7)
                                match Map.tryFind name bufferTypes, get name with
                                    | Some (ShaderParameterType.DynamicArray(contentType,_)), Some (:? Buffer as b) ->
                                        let es = ShaderParameterType.sizeof contentType
                                        let cnt = b.SizeInBytes / nativeint es |> int
                                        intWriter.Write(res.Data + nativeint f.Offset, cnt)
                                    | _ ->
                                        ()
                            else
                                match get name with
                                    | Some v ->
                                        let inputType = v.GetType()
                                        let writer = ShaderParameterWriter.get inputType f.Type
                                        writer.WriteUnsafe(res.Data + nativeint f.Offset, v)
                                        ()
                                    | None ->
                                        ()
                        | _ ->
                            failwith "sadasdsad"


                res.Dirty <- true
                ctx.Upload res
                b.Index, res
            )

        let invoke (groups : V3i) (local : V3i) (get : string -> Option<obj>) =
            use __ = ctx.ResourceLock

            GL.UseProgram(prog.Handle)
            GL.Check "use"

            let ubs = createArguments get
            try
                for (index, name, t) in buffers do
                    match get name with
                        | Some (:? Buffer as b) ->
                            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, index, b.Handle, 0n, b.SizeInBytes)
                            GL.Check "buffer"
                        | _ ->
                            failwith "bad buffer"
                                    
                for (loc, binding, name, valueType, dim, isMS, isArray) in images do
                    match get name with
                        | Some (:? BackendTextureOutputView as view) ->
                            match view.texture with 
                                | :? Texture as t ->
                                    let fmt = t.Format |> int |> unbox<SizedInternalFormat>
//                                                    GL.ActiveTexture(TextureUnit.Texture0 + unbox loc)
//                                                    GL.Check "active"
                                    //GL.Enable(EnableCap.Texture
                                    //GL.BindImageTextures(loc, 1, [|t.Handle|])
                                    GL.BindImageTexture(binding, t.Handle, view.level, false, view.slice, TextureAccess.ReadWrite, fmt)
                                    GL.Check "image"
                                | _ ->
                                    failwithf "[GL] incompatible compute shader image: %A" view.texture
                        | Some t ->
                            failwithf "[GL] incompatible compute shader image: %A" t
                        | None ->
                            failwithf "[GL] no compute shader image for: %A" name
                                    
                for (index, ub) in ubs do
                    GL.BindBufferRange(BufferRangeTarget.UniformBuffer, index, ub.Handle, 0n, nativeint ub.Size)
                    GL.Check "args"

                if Option.isSome localSize then 
                    GLCompute.DispatchCompute(groups)
                else 
                    if local.AnyGreater GLCompute.WorkGroupSize then
                        failwithf "[GL] cannot use local-size: %A (greater than %A)" local GLCompute.WorkGroupSize

                    let total = local.X * local.Y * local.Z
                    if total > GLCompute.WorkGroupInvocations then
                        failwithf "[GL] cannot use local-size: %A (more than %d in total)" local GLCompute.WorkGroupInvocations
                        
                    GLCompute.DispatchCompute(groups, local)
                GL.Check "dispatch"

            finally
                GL.UseProgram(0)

                for (index, name, t) in buffers do GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, index, 0, 0n, 0n)
                
                for (index, ub) in ubs do
                    GL.BindBufferRange(BufferRangeTarget.UniformBuffer, index, 0, 0n, 0n)
                    ctx.Delete ub

        static let groups (globalSize : int) (localSize : int) =
            if globalSize % localSize = 0 then globalSize / localSize
            else 1 + globalSize / localSize

        member x.Dispose() =
            if System.Threading.Interlocked.Exchange(&isDisposed, 1) = 0 then
                ctx.Delete(prog)

        member x.Program = prog

        member x.Invoke(globalSize : V3i, local : V3i, get : string -> Option<obj>) =
            if isDisposed = 1 then raise <| ObjectDisposedException("Kernel")

            let localSize = localSize |> Option.defaultValue local
            let groups =
                V3i(groups globalSize.X localSize.X, groups globalSize.Y localSize.Y, groups globalSize.Z localSize.Z)

            invoke groups localSize get

        member x.Invoke(globalSize : V3i, local : V3i, args : list<string * obj>) =
            let m = Map.ofList args
            x.Invoke(globalSize,local, fun s -> Map.tryFind s m)

        member x.Invoke(globalSize : V3i, get : string -> Option<obj>) =
            x.Invoke(globalSize, V3i.III, get)
            
        member x.Invoke(globalSize : V3i, args : list<string * obj>) =
            let m = Map.ofList args
            x.Invoke(globalSize, fun s -> Map.tryFind s m)

        member x.Invoke(globalSize : V2i, local : V2i, get : string -> Option<obj>) =
            x.Invoke(V3i(globalSize.X, globalSize.Y, 1), V3i(local.X, local.Y, 1), get)
            
        member x.Invoke(globalSize : V2i, local : V2i, args : list<string * obj>) =
            let m = Map.ofList args
            x.Invoke(globalSize,local, fun s -> Map.tryFind s m)

        member x.Invoke(globalSize : V2i, get : string -> Option<obj>) =
            x.Invoke(V3i(globalSize.X, globalSize.Y, 1), V3i.III, get)

        member x.Invoke(globalSize : V2i, args : list<string * obj>) =
            let m = Map.ofList args
            x.Invoke(globalSize, fun s -> Map.tryFind s m)

        member x.Invoke(globalSize : int, local : int, get : string -> Option<obj>) =
            x.Invoke(V3i(globalSize, 1, 1), V3i(local, 1, 1), get)

        member x.Invoke(globalSize : int, local : int, args : list<string * obj>) =
            let m = Map.ofList args
            x.Invoke(globalSize,local, fun s -> Map.tryFind s m)
            
        member x.Invoke(globalSize : int, get : string -> Option<obj>) =
            x.Invoke(V3i(globalSize, 1, 1), V3i.III, get)

        member x.Invoke(globalSize : int, args : list<string * obj>) =
            let m = Map.ofList args
            x.Invoke(globalSize, fun s -> Map.tryFind s m)

    type Context with

        member x.TryCompileKernel (code : string, localSize : Option<V3i>) =
            using x.ResourceLock (fun token ->
                match x.TryCompileCompute(true, code) with
                    | Success prog ->
                        let kernel = Kernel(prog, localSize)
                        Success kernel

                    | Error err ->
                        Error err
            )

        member x.TryCompileKernel (f : 'a -> 'b) =
            let gl = getGLCompute x
            let shader = FShade.ComputeShader.ofFunction gl.WorkGroupSize f
            let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL410
            let localSize = 
                if shader.csLocalSize.AllGreater 0 then Some shader.csLocalSize
                else None

            printfn "%s" glsl.code

            x.TryCompileKernel(glsl.code, localSize)

        member x.CompileKernel (f : 'a -> 'b) =
            match x.TryCompileKernel f with
                | Success kernel -> 
                    kernel
                | Error err ->
                    Log.error "%s" err
                    failwith err
    
        member x.Delete(k : Kernel) =
            k.Dispose()

