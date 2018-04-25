namespace Aardvark.Rendering.GL

open Aardvark.Base

/// <summary>
/// a compiled instructions consists of a function-pointer, 
/// its code and all its arguments (as object array).
/// it can therefore be used by our Assembler to generate OpCode
/// calling the function with its arguments. Note that the OpCode
/// is only used for debugging purposes.
/// </summary>
type CompiledInstruction = { functionPointer : nativeint; operation : InstructionCode; args : obj[] }

/// <summary>
/// the ExecutionContext module provides functions for generating
/// instructions, translating them to CompiledInstructions and 
/// executing them using a simple interpreter.
/// Note that resources definitions extend this module with further
/// functions which generate instructions for higher-level operations 
/// (e.g. bindVertexArray)
/// </summary>
module ExecutionContext =

    /// <summary>
    /// determines whether the current OpenGL implementation supports VAOs
    /// </summary>
    let vertexArrayObjectsSupported = 
        Config.enableVertexArrayObjectsIfPossible && OpenGl.Pointers.BindVertexArray <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports sampler objects
    /// </summary>
    let samplersSupported = 
        Config.enableSamplersIfPossible && OpenGl.Pointers.BindSampler <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports uniform buffers
    /// </summary>
    let uniformBuffersSupported = 
        Config.enableUniformBuffersIfPossible && OpenGl.Pointers.BindBufferRange <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports hardware instancing
    /// </summary>
    let instancingSupported = OpenGl.Pointers.VertexAttribDivisor <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports framebuffer objects
    /// </summary>
    let framebuffersSupported = OpenGl.Pointers.BindVertexArray <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports synchronization via glFence
    /// </summary>
    let syncSupported = 
        let s = OpenGl.getProcAddress "glFenceSync" <> 0n
        s
    /// <summary>
    /// determines whether the current OpenGL implementation supports buffer storage (persistently mappable)
    /// </summary>
    let bufferStorageSupported = OpenGl.getProcAddress "glBufferStorage" <> 0n


    /// <summary>
    /// determines whether the given instruction-code can be performed on
    /// the current OpenGL implementation and raises an exeption if not.
    /// </summary>
    let inline private checkOperation (op : InstructionCode) =
        match op with
            | InstructionCode.BindVertexArray when not vertexArrayObjectsSupported ->
                failwith "VertexArrayObjects are not supported by this platform"
            | InstructionCode.BindSampler when not samplersSupported ->
                failwith "Samplers are not supported by this platform"
            | InstructionCode.BindBufferRange when not uniformBuffersSupported ->
                failwith "UniformBuffers are not supported by this platform"
            | InstructionCode.VertexAttribDivisor when not instancingSupported ->
                failwith "Instanced Attributes are not supported by this platform"
            | _ -> ()

    /// <summary>
    /// gets the native function-pointer for a specific InstructionCode
    /// </summary>
    let private getFunctionPointer(i : InstructionCode) =
        match i with
            | InstructionCode.BindVertexArray -> OpenGl.Pointers.BindVertexArray
            | InstructionCode.BindProgram -> OpenGl.Pointers.BindProgram
            | InstructionCode.ActiveTexture -> OpenGl.Pointers.ActiveTexture
            | InstructionCode.BindSampler -> OpenGl.Pointers.BindSampler
            | InstructionCode.BindTexture -> OpenGl.Pointers.BindTexture
            | InstructionCode.BindBuffer -> OpenGl.Pointers.BindBuffer
            | InstructionCode.BindBufferBase -> OpenGl.Pointers.BindBufferBase
            | InstructionCode.BindBufferRange -> OpenGl.Pointers.BindBufferRange
            | InstructionCode.BindFramebuffer -> OpenGl.Pointers.BindFramebuffer
            | InstructionCode.Viewport -> OpenGl.Pointers.Viewport
            | InstructionCode.Enable -> OpenGl.Pointers.Enable
            | InstructionCode.Disable -> OpenGl.Pointers.Disable
            | InstructionCode.DepthFunc -> OpenGl.Pointers.DepthFunc
            | InstructionCode.CullFace -> OpenGl.Pointers.CullFace
            | InstructionCode.BlendFuncSeparate -> OpenGl.Pointers.BlendFuncSeparate
            | InstructionCode.BlendEquationSeparate -> OpenGl.Pointers.BlendEquationSeparate
            | InstructionCode.BlendColor -> OpenGl.Pointers.BlendColor
            | InstructionCode.PolygonMode -> OpenGl.Pointers.PolygonMode
            | InstructionCode.StencilFuncSeparate -> OpenGl.Pointers.StencilFuncSeparate
            | InstructionCode.StencilOpSeparate -> OpenGl.Pointers.StencilOpSeparate
            | InstructionCode.PatchParameter -> OpenGl.Pointers.PatchParameter
            | InstructionCode.DrawElements -> OpenGl.Pointers.DrawElements
            | InstructionCode.DrawArrays -> OpenGl.Pointers.DrawArrays
            | InstructionCode.DrawElementsInstanced -> OpenGl.Pointers.DrawElementsInstanced
            | InstructionCode.DrawArraysInstanced -> OpenGl.Pointers.DrawArraysInstanced
            | InstructionCode.Clear -> OpenGl.Pointers.Clear
            | InstructionCode.BindImageTexture -> OpenGl.Pointers.BindImageTexture
            | InstructionCode.ClearColor -> OpenGl.Pointers.ClearColor
            | InstructionCode.ClearDepth -> OpenGl.Pointers.ClearDepth
            | InstructionCode.GetError -> OpenGl.Pointers.GetError
            | InstructionCode.VertexAttribPointer -> OpenGl.Pointers.VertexAttribPointer

            | InstructionCode.Uniform1fv                    -> OpenGl.Pointers.Uniform1fv
            | InstructionCode.Uniform1iv                    -> OpenGl.Pointers.Uniform1iv
            | InstructionCode.Uniform2fv                    -> OpenGl.Pointers.Uniform2fv
            | InstructionCode.Uniform2iv                    -> OpenGl.Pointers.Uniform2iv
            | InstructionCode.Uniform3fv                    -> OpenGl.Pointers.Uniform3fv
            | InstructionCode.Uniform3iv                    -> OpenGl.Pointers.Uniform3iv
            | InstructionCode.Uniform4fv                    -> OpenGl.Pointers.Uniform4fv
            | InstructionCode.Uniform4iv                    -> OpenGl.Pointers.Uniform4iv
            | InstructionCode.UniformMatrix2fv              -> OpenGl.Pointers.UniformMatrix2fv
            | InstructionCode.UniformMatrix3fv              -> OpenGl.Pointers.UniformMatrix3fv
            | InstructionCode.UniformMatrix4fv              -> OpenGl.Pointers.UniformMatrix4fv
            | InstructionCode.EnableVertexAttribArray       -> OpenGl.Pointers.EnableVertexAttribArray
            | InstructionCode.TexParameteri                 -> OpenGl.Pointers.TexParameteri
            | InstructionCode.TexParameterf                 -> OpenGl.Pointers.TexParameterf

            | InstructionCode.VertexAttrib1f                -> OpenGl.Pointers.VertexAttrib1f
            | InstructionCode.VertexAttrib2f                -> OpenGl.Pointers.VertexAttrib2f
            | InstructionCode.VertexAttrib3f                -> OpenGl.Pointers.VertexAttrib3f
            | InstructionCode.VertexAttrib4f                -> OpenGl.Pointers.VertexAttrib4f

            | InstructionCode.MultiDrawArraysIndirect       -> OpenGl.Pointers.MultiDrawArraysIndirect
            | InstructionCode.MultiDrawElementsIndirect     -> OpenGl.Pointers.MultiDrawElementsIndirect
            | InstructionCode.DepthMask                     -> OpenGl.Pointers.DepthMask
            | InstructionCode.StencilMask                   -> OpenGl.Pointers.StencilMask
            | InstructionCode.ColorMask                     -> OpenGl.Pointers.ColorMask
            | InstructionCode.DrawBuffers                   -> OpenGl.Pointers.DrawBuffers

            | InstructionCode.HDrawArrays                   -> OpenGl.Pointers.HDrawArrays
            | InstructionCode.HDrawElements                 -> OpenGl.Pointers.HDrawElements
            | InstructionCode.HDrawArraysIndirect           -> OpenGl.Pointers.HDrawArraysIndirect
            | InstructionCode.HDrawElementsIndirect         -> OpenGl.Pointers.HDrawElementsIndirect
            | InstructionCode.HSetDepthTest                 -> OpenGl.Pointers.HSetDepthTest
            | InstructionCode.HSetCullFace                  -> OpenGl.Pointers.HSetCullFace
            | InstructionCode.HSetPolygonMode               -> OpenGl.Pointers.HSetPolygonMode
            | InstructionCode.HSetBlendMode                 -> OpenGl.Pointers.HSetBlendMode
            | InstructionCode.HSetStencilMode               -> OpenGl.Pointers.HSetStencilMode
            | InstructionCode.HBindVertexAttributes         -> OpenGl.Pointers.HBindVertexAttributes
            | InstructionCode.HSetConservativeRaster        -> OpenGl.Pointers.HSetConservativeRaster
            | InstructionCode.HSetMultisample               -> OpenGl.Pointers.HSetMultisample

            | InstructionCode.HBindTextures                 -> OpenGl.Pointers.HBindTextures
            | InstructionCode.HBindSamplers                 -> OpenGl.Pointers.HBindSamplers


            | _ -> raise <| OpenGLException (OpenTK.Graphics.OpenGL4.ErrorCode.InvalidEnum, sprintf "cannot get function pointer for: %A" i)


    let private instructionCtors =
        Dict.ofList [
            OpenGl.Pointers.BindVertexArray, fun args -> Instruction(InstructionCode.BindVertexArray, args)
            OpenGl.Pointers.BindProgram, fun args -> Instruction(InstructionCode.BindProgram, args)
            OpenGl.Pointers.ActiveTexture, fun args -> Instruction(InstructionCode.ActiveTexture, args)
            OpenGl.Pointers.BindSampler, fun args -> Instruction(InstructionCode.BindSampler, args)
            OpenGl.Pointers.BindTexture, fun args -> Instruction(InstructionCode.BindTexture, args)
            OpenGl.Pointers.BindBuffer, fun args -> Instruction(InstructionCode.BindBuffer, args)
            OpenGl.Pointers.BindBufferBase, fun args -> Instruction(InstructionCode.BindBufferBase, args)
            OpenGl.Pointers.BindBufferRange, fun args -> Instruction(InstructionCode.BindBufferRange, args)
            OpenGl.Pointers.BindFramebuffer, fun args -> Instruction(InstructionCode.BindFramebuffer, args)
            OpenGl.Pointers.Viewport, fun args -> Instruction(InstructionCode.Viewport, args)
            OpenGl.Pointers.Enable, fun args -> Instruction(InstructionCode.Enable, args)
            OpenGl.Pointers.Disable, fun args -> Instruction(InstructionCode.Disable, args)
            OpenGl.Pointers.DepthFunc, fun args -> Instruction(InstructionCode.DepthFunc, args)
            OpenGl.Pointers.CullFace, fun args -> Instruction(InstructionCode.CullFace, args)
            OpenGl.Pointers.BlendFuncSeparate, fun args -> Instruction(InstructionCode.BlendFuncSeparate, args)
            OpenGl.Pointers.BlendEquationSeparate, fun args -> Instruction(InstructionCode.BlendEquationSeparate, args)
            OpenGl.Pointers.BlendColor, fun args -> Instruction(InstructionCode.BlendColor, args)
            OpenGl.Pointers.PolygonMode, fun args -> Instruction(InstructionCode.PolygonMode, args)
            OpenGl.Pointers.StencilFuncSeparate, fun args -> Instruction(InstructionCode.StencilFuncSeparate, args)
            OpenGl.Pointers.StencilOpSeparate, fun args -> Instruction(InstructionCode.StencilOpSeparate, args)
            OpenGl.Pointers.PatchParameter, fun args -> Instruction(InstructionCode.PatchParameter, args)
            OpenGl.Pointers.DrawElements, fun args -> Instruction(InstructionCode.DrawElements, args)
            OpenGl.Pointers.DrawArrays, fun args -> Instruction(InstructionCode.DrawArrays, args)
            OpenGl.Pointers.DrawElementsInstanced, fun args -> Instruction(InstructionCode.DrawElementsInstanced, args)
            OpenGl.Pointers.DrawArraysInstanced, fun args -> Instruction(InstructionCode.DrawArraysInstanced, args)
            OpenGl.Pointers.Clear, fun args -> Instruction(InstructionCode.Clear, args)
            OpenGl.Pointers.BindImageTexture, fun args -> Instruction(InstructionCode.BindImageTexture, args)
            OpenGl.Pointers.ClearColor, fun args -> Instruction(InstructionCode.ClearColor, args)
            OpenGl.Pointers.ClearDepth, fun args -> Instruction(InstructionCode.ClearDepth, args)
            OpenGl.Pointers.GetError, fun args -> Instruction(InstructionCode.GetError, args)
            OpenGl.Pointers.VertexAttribPointer, fun args -> Instruction(InstructionCode.VertexAttribPointer, args)

            OpenGl.Pointers.Uniform1fv, fun args -> Instruction(InstructionCode.Uniform1fv, args)
            OpenGl.Pointers.Uniform1iv, fun args -> Instruction(InstructionCode.Uniform1iv, args)
            OpenGl.Pointers.Uniform2fv, fun args -> Instruction(InstructionCode.Uniform2fv, args)
            OpenGl.Pointers.Uniform2iv, fun args -> Instruction(InstructionCode.Uniform2iv, args)
            OpenGl.Pointers.Uniform3fv, fun args -> Instruction(InstructionCode.Uniform3fv, args)
            OpenGl.Pointers.Uniform3iv, fun args -> Instruction(InstructionCode.Uniform3iv, args)
            OpenGl.Pointers.Uniform4fv, fun args -> Instruction(InstructionCode.Uniform4fv, args)
            OpenGl.Pointers.Uniform4iv, fun args -> Instruction(InstructionCode.Uniform4iv, args)
            OpenGl.Pointers.UniformMatrix2fv, fun args -> Instruction(InstructionCode.UniformMatrix2fv, args)
            OpenGl.Pointers.UniformMatrix3fv, fun args -> Instruction(InstructionCode.UniformMatrix3fv, args)
            OpenGl.Pointers.UniformMatrix4fv, fun args -> Instruction(InstructionCode.UniformMatrix4fv, args)
            OpenGl.Pointers.EnableVertexAttribArray, fun args -> Instruction(InstructionCode.EnableVertexAttribArray, args)
            OpenGl.Pointers.TexParameteri, fun args -> Instruction(InstructionCode.TexParameteri, args)
            OpenGl.Pointers.TexParameterf, fun args -> Instruction(InstructionCode.TexParameterf, args)

            OpenGl.Pointers.VertexAttrib1f, fun args -> Instruction(InstructionCode.VertexAttrib1f, args)
            OpenGl.Pointers.VertexAttrib2f, fun args -> Instruction(InstructionCode.VertexAttrib2f, args)
            OpenGl.Pointers.VertexAttrib3f, fun args -> Instruction(InstructionCode.VertexAttrib3f, args)
            OpenGl.Pointers.VertexAttrib4f, fun args -> Instruction(InstructionCode.VertexAttrib4f, args)

            OpenGl.Pointers.MultiDrawArraysIndirect, fun args -> Instruction(InstructionCode.MultiDrawArraysIndirect, args)
            OpenGl.Pointers.MultiDrawElementsIndirect, fun args -> Instruction(InstructionCode.MultiDrawElementsIndirect, args)
            OpenGl.Pointers.DepthMask, fun args -> Instruction(InstructionCode.DepthMask, args)
            OpenGl.Pointers.StencilMask, fun args -> Instruction(InstructionCode.StencilMask, args)
            OpenGl.Pointers.ColorMask, fun args -> Instruction(InstructionCode.ColorMask, args)
            OpenGl.Pointers.DrawBuffers, fun args -> Instruction(InstructionCode.DrawBuffers, args)


            OpenGl.Pointers.HDrawArrays, fun args -> Instruction(InstructionCode.HDrawArrays, args)
            OpenGl.Pointers.HDrawElements, fun args -> Instruction(InstructionCode.HDrawElements, args)
            OpenGl.Pointers.HDrawArraysIndirect, fun args -> Instruction(InstructionCode.HDrawArraysIndirect, args)
            OpenGl.Pointers.HDrawElementsIndirect, fun args -> Instruction(InstructionCode.HDrawElementsIndirect, args)
            OpenGl.Pointers.HSetDepthTest, fun args -> Instruction(InstructionCode.HSetDepthTest, args)
            OpenGl.Pointers.HSetCullFace, fun args -> Instruction(InstructionCode.HSetCullFace, args)
            OpenGl.Pointers.HSetPolygonMode, fun args -> Instruction(InstructionCode.HSetPolygonMode, args)
            OpenGl.Pointers.HSetBlendMode, fun args -> Instruction(InstructionCode.HSetBlendMode, args)
            OpenGl.Pointers.HSetStencilMode, fun args -> Instruction(InstructionCode.HSetStencilMode, args)
            OpenGl.Pointers.HBindVertexAttributes, fun args -> Instruction(InstructionCode.HBindVertexAttributes, args)
            OpenGl.Pointers.HSetConservativeRaster, fun args -> Instruction(InstructionCode.HSetConservativeRaster, args)
            OpenGl.Pointers.HSetMultisample, fun args -> Instruction(InstructionCode.HSetMultisample, args)

            
            OpenGl.Pointers.HBindTextures, fun args -> Instruction(InstructionCode.HBindTextures, args)
            OpenGl.Pointers.HBindSamplers, fun args -> Instruction(InstructionCode.HBindSamplers, args)

        ]

    let callToInstruction (ptr : nativeint, args : obj[]) =
        match instructionCtors.TryGetValue ptr with
            | (true, ctor) -> ctor args
            | _ -> failwithf "could not get instruction-code for pointer: %A" ptr
 
    /// <summary>
    /// translates an instruction to a compiled-instruction by
    /// resolving the needed native function.
    /// </summary>
    let compile(i : Instruction) =
        checkOperation i.Operation

        let ptr = getFunctionPointer i.Operation
        { functionPointer = ptr; operation = i.Operation; args = i.Arguments}

    /// <summary>
    /// executes a specific instruction using a simple interpreter
    /// Note that this might be relatively slow and should only be 
    /// used for debugging.
    /// </summary>
    let run(i : Instruction) =
        checkOperation i.Operation
        
        let inline int id = i.Arguments.[id] |> unbox<int>
        let inline float id = i.Arguments.[id] |> unbox<float32>
        let inline int64 id = i.Arguments.[id] |> unbox<int64>
        let inline ptr id = i.Arguments.[id] |> unbox<nativeint>

        let readInt (id : int) =
            match i.Arguments.[id] with
                | :? Aardvark.Base.PtrArgument as p -> 
                    match p with
                        | Ptr32 p -> System.Runtime.InteropServices.Marshal.ReadInt32 p
                        | Ptr64 p -> System.Runtime.InteropServices.Marshal.ReadInt64 p |> int32
                | :? int as v -> v
                | _ -> failwith "bad draw call count"

        match i.Operation with
            | InstructionCode.BindVertexArray          -> OpenGl.Unsafe.BindVertexArray(readInt 0)
            | InstructionCode.BindProgram              -> OpenGl.Unsafe.BindProgram(int 0)
            | InstructionCode.ActiveTexture            -> OpenGl.Unsafe.ActiveTexture(int 0)
            | InstructionCode.BindSampler              -> OpenGl.Unsafe.BindSampler (int 0) (int 1)
            | InstructionCode.BindTexture              -> OpenGl.Unsafe.BindTexture (int 0) (int 1)
            | InstructionCode.BindBuffer               -> OpenGl.Unsafe.BindBuffer (int 0) (int 1)
            | InstructionCode.BindBufferBase           -> OpenGl.Unsafe.BindBufferBase (int 0) (int 1) (int 2)
            | InstructionCode.BindBufferRange          -> OpenGl.Unsafe.BindBufferRange (int 0) (int 1) (int 2) (ptr 3) (ptr 4)
            | InstructionCode.BindFramebuffer          -> OpenGl.Unsafe.BindFramebuffer (int 0) (int 1)
            | InstructionCode.Viewport                 -> failwith "not implemented"
            | InstructionCode.Enable                   -> OpenGl.Unsafe.Enable (int 0)
            | InstructionCode.Disable                  -> OpenGl.Unsafe.Disable (int 0)
            | InstructionCode.DepthFunc                -> OpenGl.Unsafe.DepthFunc (int 0)
            | InstructionCode.CullFace                 -> OpenGl.Unsafe.CullFace (int 0)
            | InstructionCode.BlendFuncSeparate        -> OpenGl.Unsafe.BlendFuncSeparate (int 0) (int 1) (int 2) (int 3)
            | InstructionCode.BlendEquationSeparate    -> OpenGl.Unsafe.BlendEquationSeparate (int 0) (int 1)
            | InstructionCode.BlendColor               -> OpenGl.Unsafe.BlendColor (int 0) (int 1) (int 2) (int 3)
            | InstructionCode.PolygonMode              -> OpenGl.Unsafe.PolygonMode (int 0) (int 1)
            | InstructionCode.StencilFuncSeparate      -> OpenGl.Unsafe.StencilFuncSeparate (int 0) (int 1) (int 2) (int 3)
            | InstructionCode.StencilOpSeparate        -> OpenGl.Unsafe.StencilOpSeparate (int 0) (int 1) (int 2) (int 3)
            | InstructionCode.PatchParameter           -> OpenGl.Unsafe.PatchParameter (int 0) (int 1)
            | InstructionCode.DrawElements             -> OpenGl.Unsafe.DrawElements (int 0) (int 1) (int 2) (ptr 3)
            | InstructionCode.DrawArrays               -> OpenGl.Unsafe.DrawArrays (int 0) (int 1) (int 2)
            | InstructionCode.DrawElementsInstanced    -> OpenGl.Unsafe.DrawElementsInstanced (int 0) (int 1) (int 2) (ptr 3) (int 4)
            | InstructionCode.DrawArraysInstanced      -> OpenGl.Unsafe.DrawArraysInstanced (int 0) (int 1) (int 2) (int 3) 
            | InstructionCode.Clear                    -> OpenGl.Unsafe.Clear (int 0)
            | InstructionCode.BindImageTexture         -> OpenGl.Unsafe.BindImageTexture (int 0) (int 1) (int 2) (int 3)
            | InstructionCode.ClearColor               -> OpenGl.Unsafe.ClearColor (int 0) (int 1) (int 2) (int 3)
            | InstructionCode.ClearDepth               -> OpenGl.Unsafe.ClearDepth (int64 0)
            | InstructionCode.VertexAttribPointer      -> OpenGl.Unsafe.VertexAttribPointer (int 0) (int 1) (int 2) (int 3) (int 4) (ptr 5)

            | InstructionCode.Uniform1fv               -> OpenGl.Unsafe.Uniform1fv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform1iv               -> OpenGl.Unsafe.Uniform1iv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform2fv               -> OpenGl.Unsafe.Uniform2fv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform2iv               -> OpenGl.Unsafe.Uniform2iv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform3fv               -> OpenGl.Unsafe.Uniform3fv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform3iv               -> OpenGl.Unsafe.Uniform3iv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform4fv               -> OpenGl.Unsafe.Uniform4fv (int 0) (int 1) (ptr 2)
            | InstructionCode.Uniform4iv               -> OpenGl.Unsafe.Uniform4iv (int 0) (int 1) (ptr 2)
            | InstructionCode.UniformMatrix2fv         -> OpenGl.Unsafe.UniformMatrix2fv (int 0) (int 1) (int 2) (ptr 3)
            | InstructionCode.UniformMatrix3fv         -> OpenGl.Unsafe.UniformMatrix3fv (int 0) (int 1) (int 2) (ptr 3)
            | InstructionCode.UniformMatrix4fv         -> OpenGl.Unsafe.UniformMatrix4fv (int 0) (int 1) (int 2) (ptr 3)

            | InstructionCode.VertexAttrib1f           -> OpenGl.Unsafe.VertexAttrib1f (int 0) (float 1)
            | InstructionCode.VertexAttrib2f           -> OpenGl.Unsafe.VertexAttrib2f (int 0) (float 1) (float 2)
            | InstructionCode.VertexAttrib3f           -> OpenGl.Unsafe.VertexAttrib3f (int 0) (float 1) (float 2) (float 3)
            | InstructionCode.VertexAttrib4f           -> OpenGl.Unsafe.VertexAttrib4f (int 0) (float 1) (float 2) (float 3) (float 4)

            | InstructionCode.DepthMask                -> OpenGl.Unsafe.DepthMask (int 0)
            | InstructionCode.StencilMask              -> OpenGl.Unsafe.StencilMask (int 0)
            | InstructionCode.ColorMask                -> OpenGl.Unsafe.ColorMask (int 0) (int 1) (int 2) (int 3) (int 4)
            | InstructionCode.DrawBuffers              -> OpenGl.Unsafe.DrawBuffers (int 0) (ptr 1) 

            | InstructionCode.MultiDrawArraysIndirect  -> 
                OpenGl.Unsafe.MultiDrawArraysIndirect (int 0) (ptr 1) (readInt 2) (int 3)
            | InstructionCode.MultiDrawElementsIndirect  -> 
                OpenGl.Unsafe.MultiDrawElementsIndirect (int 0) (int 1) (ptr 2) (readInt 3) (int 4)

            | InstructionCode.HDrawArrays -> OpenGl.Unsafe.HDrawArrays (ptr 0) (ptr 1) (ptr 2) (ptr 3)
            | InstructionCode.HDrawElements -> OpenGl.Unsafe.HDrawElements (ptr 0) (ptr 1) (ptr 2) (int 3) (ptr 4)
            | InstructionCode.HDrawArraysIndirect -> OpenGl.Unsafe.HDrawArraysIndirect (ptr 0) (ptr 1) (ptr 2) (ptr 3) (int 4)
            | InstructionCode.HDrawElementsIndirect -> OpenGl.Unsafe.HDrawElementsIndirect (ptr 0) (ptr 1) (ptr 2) (int 3) (ptr 4)
            | InstructionCode.HSetDepthTest -> OpenGl.Unsafe.HSetDepthTest (ptr 0) 
            | InstructionCode.HSetCullFace -> OpenGl.Unsafe.HSetCullFace (ptr 0) 
            | InstructionCode.HSetPolygonMode -> OpenGl.Unsafe.HSetPolygonMode (ptr 0) 
            | InstructionCode.HSetBlendMode -> OpenGl.Unsafe.HSetBlendMode (ptr 0) 
            | InstructionCode.HSetStencilMode -> OpenGl.Unsafe.HSetStencilMode (ptr 0) 
            | InstructionCode.HBindVertexAttributes -> OpenGl.Unsafe.HBindVertexAttributes (ptr 0) (ptr 1)
            | InstructionCode.HSetConservativeRaster -> OpenGl.Unsafe.HSetConservativeRaster (ptr 0)
            | InstructionCode.HSetMultisample -> OpenGl.Unsafe.HSetMultisample (ptr 0)

            | InstructionCode.HBindTextures -> OpenGl.Unsafe.HBindTextures (int 0) (int 1) (ptr 2) (ptr 3)
            | InstructionCode.HBindSamplers -> OpenGl.Unsafe.HBindSamplers (int 0) (int 1) (ptr 2)

            | InstructionCode.GetError                 -> ()
            | _ -> failwithf "unknown instruction: %A" i

    /// <summary>
    /// executes a specific instruction using a simple interpreter
    /// Note that this might be relatively slow and should only be 
    /// used for debugging.
    /// </summary>
    let debug(i : Instruction) =
        run i
        OpenTK.Graphics.OpenGL4.GL.Flush()
        OpenTK.Graphics.OpenGL4.GL.Finish()
        let err = OpenGl.Unsafe.GetError() |> unbox<OpenTK.Graphics.OpenGL4.ErrorCode>
        if err <> OpenTK.Graphics.OpenGL4.ErrorCode.NoError then
            let str = sprintf "%A failed with code: %A" i err
            System.Diagnostics.Debugger.Break()
            Log.warn "%s" str



[<AutoOpen>]
module GLExtensionsPossiblyNotWorkingEverywhere =
    open OpenTK.Graphics.OpenGL4
    open System.Runtime.InteropServices
    open Aardvark.Rendering.GL

    type GL with
        static member inline Sync() =
            if ExecutionContext.syncSupported then
                let fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)
                GL.Flush()
                let status = GL.ClientWaitSync(fence, ClientWaitSyncFlags.None, ~~~0UL)
                GL.DeleteSync(fence)

                match status with
                    | WaitSyncStatus.TimeoutExpired ->
                        Log.warn "[GL] wait timeout"
                    | WaitSyncStatus.WaitFailed ->
                        Log.warn "[GL] wait failed"
                    | _ -> ()
            else
                Log.warn "[GL] flush/finish"
                GL.Flush()
                GL.Finish()