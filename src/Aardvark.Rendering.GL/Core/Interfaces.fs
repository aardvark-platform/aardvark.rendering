namespace Aardvark.Rendering.GL

/// <summary>
/// IResource represents the base interface for all OpenGL resources
/// </summary>
type IContextChild =
    
    /// <summary>
    /// The context which the resource was
    /// created on.
    /// </summary>
    abstract member Context : Context

    /// <summary>
    /// The resource's handle
    /// </summary>
    abstract member Handle : int

