namespace Examples


open System
open Aardvark.Base
open Aardvark.Base.Incremental

open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.ShaderReflection

module ComputeTest =
    open OpenTK.Graphics.OpenGL4
    open Aardvark.Rendering.GL
    open Microsoft.FSharp.NativeInterop
    open FShade
    open FShade.GLSL

    type DispatchComputeGroupSizeARBDelegate = delegate of uint32 * uint32 * uint32 * uint32 * uint32 * uint32 -> unit

    let private computeFunCache = System.Collections.Concurrent.ConcurrentDictionary<Context, DispatchComputeGroupSizeARBDelegate>()

    type Context with

        member x.TryCompileComputeFunction (code : string) =
            using x.ResourceLock (fun token ->
                let glDispatchComputeGroupSize = 
                    computeFunCache.GetOrAdd(x, fun x ->
                        let c = x.CurrentContextHandle.Value.Handle |> unbox<OpenTK.Graphics.IGraphicsContextInternal>
                        let ptr = c.GetAddress "glDispatchComputeGroupSizeARB"
                        System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof<DispatchComputeGroupSizeARBDelegate>) |> unbox<DispatchComputeGroupSizeARBDelegate>
                    )

                match x.TryCompileCompute(true, code) with
                    | Success prog ->
                        let iface = prog.Interface
                        let str = ShaderInterface.toString prog.Interface

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
                        
                        let mutable argumentBlock = None

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
                                    argumentBlock <- Some res

                                    None
                                else
                                    Some b
                            )

                        let createArguments =
                            match argumentBlock with
                                | None ->
                                    fun _ -> (-1, Unchecked.defaultof<UniformBuffer>)
                                | Some b ->
                                
                                    let intWriter : ShaderParameterWriter.Writer<int> = ShaderParameterWriter.writer ShaderParameterType.Int
                                    let init (get : string -> Option<obj>) =
                                        let res = x.CreateUniformBuffer b

                                        for f in b.Fields do
                                            match f.Path with
                                                | ShaderPath.Value name ->
                                                    if name.EndsWith "_length" then
                                                        let name = name.Substring(0, name.Length - 7)
                                                        match Map.tryFind name bufferTypes, get name with
                                                            | Some (DynamicArray(contentType,_)), Some (:? Buffer as b) ->
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
                                        x.Upload res
                                        b.Index, res

                                    init
                                    
                        let invoke (groups : V3i) (local : V3i) (get : string -> Option<obj>) =
                            use __ = x.ResourceLock

                            GL.UseProgram(prog.Handle)
                            GL.Check "use"

                            let (argIndex, argBuffer) = createArguments get
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
                                            
                                for ub in uniformBlocks do
                                    failwith "not implemented"

                                if argIndex >= 0 then
                                    GL.BindBufferRange(BufferRangeTarget.UniformBuffer, argIndex, argBuffer.Handle, 0n, nativeint argBuffer.Size)
                                    GL.Check "args"

                                glDispatchComputeGroupSize.Invoke(uint32 groups.X, uint32 groups.Y, uint32 groups.Z, uint32 local.X, uint32 local.Y, uint32 local.Z)
                                GL.Check "dispatch"

                            finally
                                GL.UseProgram(0)

                                for (index, name, t) in buffers do GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, index, 0, 0n, 0n)

                                if argIndex >= 0 then
                                    GL.BindBufferRange(BufferRangeTarget.UniformBuffer, argIndex, 0, 0n, 0n)
                                    x.Delete argBuffer

                        Success invoke

                    | Error err ->
                        Error err
            )

        member x.TryCompileComputeFunction (f : 'a) =
            let shader = FShade.ComputeShader.ofFunction f
            let glsl = shader |> FShade.ComputeShader.toModule |> ModuleCompiler.compileGLSL410

            printfn "%s" glsl

            x.TryCompileComputeFunction glsl

    let computer (a : float[]) (b : float[]) (c : Image2d<Formats.r32f>) =
        compute {
            let id = getGlobalId()

            let i = id.X


            b.[i] <- a.[i] * 3.0 + c.[V2i(i,0)].X

        }

    let run() =
        use app = new Aardvark.Application.WinForms.OpenGlApplication()
        let runtime = app.Runtime
        let ctx = runtime.Context
        use __ = ctx.ResourceLock

        let code = 
            """#version 440

            #extension GL_ARB_compute_variable_group_size : enable

            layout(r32f)
            uniform image2D cs_c;

            uniform Arguments
            {
                int cs_a_length;
                float cs_f;
            };




            layout( local_size_variable ) in;
            layout(std430) buffer a_ssb  { float a[]; };
            layout(std430) buffer b_ssb  { float b[]; };
            void main()
            {
                int i = ivec3(gl_GlobalInvocationID).x;
                if((i < cs_a_length))
                {
                    b[i] = ((cs_f * a[i]) + imageLoad(cs_c, ivec2(i, 0)).x);
                }
            }
            """

        match ctx.TryCompileComputeFunction computer with
            | Success invoke ->
                let f a = float32 a + 1.0f
                let ba = ctx.CreateBuffer(Array.init 1024 f, BufferUsage.Dynamic)
                let bb = ctx.CreateBuffer(Array.zeroCreate<float32> 1024, BufferUsage.Dynamic)

                let img = PixImage<float32>(Col.Format.Gray, 1024L, 1024L)
                img.GetChannel(0L).SetByCoord (fun (x : int64) (y : int64) -> float32 x) |> ignore
                let myImg =
                    ctx.CreateTexture(
                        PixTexture2d(PixImageMipMap [| img :> PixImage |], false)
                    )
                GL.Check "could not upload texture"

                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.Check "could not bind texture"
                GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits)
                GL.Check "could not enqueue barrier"

                let args =
                    Map.ofList [
                        "a", ba :> obj
                        "b", bb :> obj
                        "f", 3.0 :> obj
                        "c", { texture = myImg; slice = 0; level = 0 } :> obj
                    ]


                invoke (V3i(32,1,1)) (V3i(32,1,1)) (fun n -> Map.tryFind n args)
                
                GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits)
                GL.Check "could not enqueue barrier"

                let ra : float32[] = ctx.Download(ba)
                let rb : float32[] = ctx.Download(bb)

                ctx.Delete ba
                ctx.Delete bb
                ctx.Delete myImg

                for i in 0 .. rb.Length-1 do
                    printfn "%d: %A" i rb.[i]



            | Error err ->
                Log.error "%A" err


