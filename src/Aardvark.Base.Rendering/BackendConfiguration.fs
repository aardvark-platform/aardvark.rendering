namespace Aardvark.Base

open System

/// Execution engine performing the corresponding graphics API calls for the render commands
type ExecutionEngine =
    /// Wraps a debug layer around the native execution engine providing possibility 
    /// to step through using the debugger and trace state changes
    | Debug = 0
    /// Performs graphics API calls using an executable memory with lowest possible overhead
    | Native = 1
   
/// Resource sharing configuration
[<Flags>]
type ResourceSharing =
    /// Each render task will build and manage its own resources
    | None      = 0x00
    /// Buffers are shared globally between individual render tasks
    | Buffers   = 0x01
    /// Textures are shared globally between individual render tasks
    | Textures  = 0x02
    /// Buffers and textures are shared globally between individual render tasks
    | Full      = 0x03

/// Configuration of execution engine, resources sharing and debug output
type BackendConfiguration = {
    /// Configuration of how graphics API calls are emitted
    execution : ExecutionEngine
    /// Configuration of resource sharing behavior
    sharing : ResourceSharing
    /// Enable additional debug output from the graphics API if possible (e.g. OpenGL Debug Output)
    useDebugOutput : bool
}

/// Predefined backend configurations
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BackendConfiguration =

    /// Configuration using Native execution engine and Full resources sharing
    let Native = 
        { 
            execution       = ExecutionEngine.Native
            sharing         = ResourceSharing.Textures &&& ResourceSharing.Buffers
            useDebugOutput  = false
        }
    
    /// Configuration with full debug functionality 
    let Debug = 
        { 
            execution       = ExecutionEngine.Debug
            sharing         = ResourceSharing.Textures  &&& ResourceSharing.Buffers
            useDebugOutput  = true
        }

    /// Recommendation of the Aardvark core team
    let Default = Native