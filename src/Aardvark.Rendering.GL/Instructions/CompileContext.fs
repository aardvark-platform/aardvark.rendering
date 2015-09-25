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
    let vertexArrayObjectsSupported = OpenGl.Pointers.BindVertexArray <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports sampler objects
    /// </summary>
    let samplersSupported = OpenGl.Pointers.BindSampler <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports uniform buffers
    /// </summary>
    let uniformBuffersSupported = OpenGl.Pointers.BindBufferRange <> 0n

    /// <summary>
    /// determines whether the current OpenGL implementation supports hardware instancing
    /// </summary>
    let instancingSupported = OpenGl.Pointers.VertexAttribDivisor <> 0n


    let framebuffersSupported = OpenGl.Pointers.BindVertexArray <> 0n

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

            | InstructionCode.Uniform1fv               -> OpenGl.Pointers.Uniform1fv
            | InstructionCode.Uniform1iv               -> OpenGl.Pointers.Uniform1iv
            | InstructionCode.Uniform2fv               -> OpenGl.Pointers.Uniform2fv
            | InstructionCode.Uniform2iv               -> OpenGl.Pointers.Uniform2iv
            | InstructionCode.Uniform3fv               -> OpenGl.Pointers.Uniform3fv
            | InstructionCode.Uniform3iv               -> OpenGl.Pointers.Uniform3iv
            | InstructionCode.Uniform4fv               -> OpenGl.Pointers.Uniform4fv
            | InstructionCode.Uniform4iv               -> OpenGl.Pointers.Uniform4iv
            | InstructionCode.UniformMatrix2fv         -> OpenGl.Pointers.UniformMatrix2fv
            | InstructionCode.UniformMatrix3fv         -> OpenGl.Pointers.UniformMatrix3fv
            | InstructionCode.UniformMatrix4fv         -> OpenGl.Pointers.UniformMatrix4fv
            | InstructionCode.EnableVertexAttribArray  -> OpenGl.Pointers.EnableVertexAttribArray

            | _ -> raise <| OpenGLException (OpenTK.Graphics.OpenGL4.ErrorCode.InvalidEnum, sprintf "cannot get function pointer for: %A" i)

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
        let inline int64 id = i.Arguments.[id] |> unbox<int64>
        let inline ptr id = i.Arguments.[id] |> unbox<nativeint>

        match i.Operation with
            | InstructionCode.BindVertexArray          -> OpenGl.Unsafe.BindVertexArray(int 0)
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

            | InstructionCode.GetError                 -> ()
            | _ -> failwithf "unknown instruction: %A" i

    /// <summary>
    /// executes a specific instruction using a simple interpreter
    /// Note that this might be relatively slow and should only be 
    /// used for debugging.
    /// </summary>
    let debug(i : Instruction) =
        run i
        let err = OpenGl.Unsafe.GetError() |> unbox<OpenTK.Graphics.OpenGL4.ErrorCode>
        if err <> OpenTK.Graphics.OpenGL4.ErrorCode.NoError then
            let str = sprintf "%A failed with code: %A" i err
            System.Diagnostics.Debugger.Break()
            Log.warn "%s" str
