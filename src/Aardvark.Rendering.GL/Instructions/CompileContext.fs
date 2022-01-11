namespace Aardvark.Rendering.GL

open Aardvark.Base

/// <summary>
/// the ExecutionContext module provides functions for generating
/// instructions, translating them to CompiledInstructions and 
/// executing them using a simple interpreter.
/// Note that resources definitions extend this module with further
/// functions which generate instructions for higher-level operations 
/// (e.g. bindVertexArray)
/// </summary>
module ExecutionContext =

    // NOT USED !? is OpenGL 3.0
    ///// <summary>
    ///// determines whether the current OpenGL implementation supports VAOs
    ///// </summary>
    //let vertexArrayObjectsSupported = 
    //    Config.enableVertexArrayObjectsIfPossible && OpenGl.Pointers.BindVertexArray <> 0n

    // is OpenGL 3.2 -> do we want to support a fallback !?
    /// <summary>
    /// determines whether the current OpenGL implementation supports sampler objects
    /// </summary>
    let samplersSupported = 
        OpenGl.Pointers.BindSampler <> 0n
    
    // NOT USED !? is OpenGL 3.0
    ///// <summary>
    ///// determines whether the current OpenGL implementation supports uniform buffers
    ///// </summary>
    //let uniformBuffersSupported = 
    //    Config.enableUniformBuffersIfPossible && OpenGl.Pointers.BindBufferRange <> 0n

    // NOT USED !? is OpenGL 3.3
    ///// <summary>
    ///// determines whether the current OpenGL implementation supports hardware instancing
    ///// </summary>
    //let instancingSupported = OpenGl.Pointers.VertexAttribDivisor <> 0n

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

    // NOT USED !? is OpenGL 4.4
    ///// <summary>
    ///// determines whether the current OpenGL implementation supports buffer storage (persistently mappable)
    ///// </summary>
    //let bufferStorageSupported = OpenGl.getProcAddress "glBufferStorage" <> 0n



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