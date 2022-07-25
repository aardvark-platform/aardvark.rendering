namespace Aardvark.Rendering.GL

open Aardvark.Rendering

[<AutoOpen>]
module SharedMemory =

    type internal SharedMemoryEntry =
        class 
            val mutable public SharedHandle : IExternalMemoryHandle
            val mutable public Size : int64
            val mutable public GLHandle : int
            val mutable public RefCount : int

            new(sharedHandle : IExternalMemoryHandle, size : int64, glHandle : int) =
                { 
                     SharedHandle = sharedHandle
                     Size = size
                     GLHandle = glHandle
                     RefCount = 1
                }
        end


    type ImportedMemoryHandle (opaqueHandle : IExternalMemoryHandle, glHandle : int) =
        
        member x.GLHandle
            with get() = glHandle

        member x.OpaqueHandle
            with get() = opaqueHandle
