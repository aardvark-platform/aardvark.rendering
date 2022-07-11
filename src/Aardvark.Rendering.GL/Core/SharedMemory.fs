namespace Aardvark.Rendering.GL

open System

[<AutoOpen>]
module SharedMemory =

    type internal SharedMemoryEntry =
        class 
            val mutable public SharedHandle : nativeint
            val mutable public Size : int64
            val mutable public GLHandle : int
            val mutable public RefCount : int

            new(sharedHandle : nativeint, size : int64, glHandle : int) =
                { 
                     SharedHandle = sharedHandle
                     Size = size
                     GLHandle = glHandle
                     RefCount = 1
                }
        end


    type ImportedMemoryHandle (opaqueHandle : nativeint, glHandle : int) =
        
        member x.GLHandle
            with get() = glHandle

        member x.OpaqueHandle
            with get() = opaqueHandle
