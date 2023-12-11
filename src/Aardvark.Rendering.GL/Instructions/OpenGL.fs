namespace Aardvark.Rendering.GL
open System
open System.IO
open System.Runtime.InteropServices
open Aardvark.Base

/// <summary>
/// This module contains enumerations provided by OpenGL and
/// searches for OpenGL entry-points exposing them as wrapped
/// functions and as pointers
/// </summary>
module OpenGl =

    let mutable DefaultContext = Unchecked.defaultof<OpenTK.Graphics.IGraphicsContextInternal>

    type Handle = int

    [<AutoOpen>]
    module Enums =
        type TextureUnit = Texture0 = 0x84C0
                         | Texture1 = 0x84C1
                         | Texture2 = 0x84C2
                         | Texture3 = 0x84C3
                         | Texture4 = 0x84C4
                         | Texture5 = 0x84C5
                         | Texture6 = 0x84C6
                         | Texture7 = 0x84C7
                         | Texture8 = 0x84C8
                         | Texture9 = 0x84C9
                         | Texture10 = 0x84CA
                         | Texture11 = 0x84CB
                         | Texture12 = 0x84CC
                         | Texture13 = 0x84CD
                         | Texture14 = 0x84CE
                         | Texture15 = 0x84CF
                         | Texture16 = 0x84D0
                         | Texture17 = 0x84D1
                         | Texture18 = 0x84D2
                         | Texture19 = 0x84D3
                         | Texture20 = 0x84D4
                         | Texture21 = 0x84D5
                         | Texture22 = 0x84D6
                         | Texture23 = 0x84D7
                         | Texture24 = 0x84D8
                         | Texture25 = 0x84D9
                         | Texture26 = 0x84DA
                         | Texture27 = 0x84DB
                         | Texture28 = 0x84DC
                         | Texture29 = 0x84DD
                         | Texture30 = 0x84DE
                         | Texture31 = 0x84DF

        type TextureTarget = Texture1D = 0x0DE0
                           | Texture2D = 0x0DE1
                           | Texture3D = 0x806F
                           | TextureCubeMap = 0x8513
                           | TextureBuffer = 0x8C2A
                           | Texture2DMultisample = 0x9100
                           | TextureRectangle = 0x84F5
                           | Texture1DArray = 0x8C18
                           | Texture2DArray = 0x8C1A
                           | TextureCubeMapArray = 0x9009
                           | Texture2DMultisampleArray = 0x9102

        type BufferTarget = AtomicCounterBuffer = 0x92C0
                          | TransformFeedbackBuffer = 0x8C8E
                          | UniformBuffer = 0x8A11
                          | ShaderStorageBuffer = 0x90D2

        type FramebufferTarget = Framebuffer = 0x8D40
                               | DrawFramebuffer = 0x8CA9
                               | ReadFramebuffer = 0x8CA8

        type State = Blend = 0x0BE2
                   | ClipDistance0 = 0x3000
                   | ClipDistance1 = 0x3001
                   | ClipDistance2 = 0x3002
                   | ClipDistance3 = 0x3003
                   | ClipDistance4 = 0x3004
                   | ClipDistance5 = 0x3005
                   | ClipDistance6 = 0x3006
                   | ClipDistance7 = 0x3007
                   | ColorLogicOperation = 0x0BF2
                   | CullFace = 0x0B44
                   | DebugOutput = 0x92E0
                   | DebugOutputSynchronous = 0x8242
                   | DepthClamp = 0x864F
                   | DepthTest = 0x0B71
                   | Dither = 0x0BD0
                   | LineSmooth = 0x0B20
                   | Multisample = 0x809D
                   | PolygonOffsetFill = 0x8037
                   | PolygonOffsetLine = 0x2A02
                   | PolygonOffsetPoint = 0x2A01
                   | PolygonSmooth = 0x0B41
                   | PrimitiveRestart = 0x8F9D
                   | PrimitiveRestartFixedIndex = 0x8D69
                   | RasterizerDiscard = 0x8C89
                   | SampleAlphaToCoverage = 0x809E
                   | SampleAlphaToOne = 0x809F
                   | SampleCoverage = 0x80A0
                   | SampleShading = 0x8C36
                   | SampleMask = 0x8E51
                   | ScissorTest = 0x0C11
                   | StencilTest = 0x0B90
                   | TextureCubeMapSeamless = 0x884F
                   | ProgramPointSize = 0x8642

        type PolygonMode = Point = 0x1B00
                         | Line = 0x1B01
                         | Fill = 0x1B02

        type CompareFunction = Never = 0x0200
                             | Less = 0x0201
                             | Equal = 0x0202
                             | LessEqual = 0x0203
                             | Greater = 0x0204
                             | NotEqual = 0x0205
                             | GreaterEqual = 0x0206
                             | Always = 0x0207

        type Face = Front = 0x0404
                  | Back = 0x0405
                  | FrontAndBack = 0x0408

        type WindingOrder = CW = 0x0900
                          | CCW = 0x0901

        type BlendFactor = Zero = 0
                         | One = 1
                         | SrcColor = 0x0300
                         | InvSrcColor = 0x0301
                         | SrcAlpha = 0x0302
                         | InvSrcAlpha = 0x0303
                         | DstAlpha = 0x0304
                         | InvDstAlpha = 0x0305
                         | DstColor = 0x0306
                         | InvDstColor = 0x0307
                         | SrcAlphaSat = 0x0308
                         | ConstantColor = 0x8001
                         | InvConstantColor = 0x8002
                         | ConstantAlpha = 0x8003
                         | InvConstantAlpha = 0x8004
                         | Src1Alpha = 0x8589
                         | Src1Color = 0x88F9
                         | InvSrc1Color = 0x88FA
                         | InvSrc1Alpha = 0x88FB

        type BlendOperation = Add = 0x8006
                            | Subtract = 0x800A
                            | ReverseSubtract = 0x800B
                            | Minimum = 0x8007
                            | Maximum = 0x8008

        type StencilOperation = Keep = 0x1E00
                              | Zero = 0
                              | Replace = 0x1E01
                              | Increment = 0x1E02
                              | IncrementWrap = 0x8507
                              | Decrement = 0x1E03
                              | DecrementWrap = 0x8508
                              | Invert = 0x150A

        type DrawMode = Points = 0x0000
                      | LineStrip = 0x0003
                      | LineLoop = 0x0002
                      | Lines = 0x0001
                      | LineStripAdjacency = 0x000B
                      | LinesAdjacency = 0x000A
                      | TriangleStrip = 0x0005
                      | TriangleFan = 0x0006
                      | Triangles = 0x0004
                      | TriangleStripAdjacency = 0x000D
                      | TrianglesAdjacency = 0x000C
                      | Patches = 0x000E
                      | QuadList = 0x0007

        type IndexType = UnsignedByte = 0x1401
                       | UnsignedShort = 0x1403
                       | UnsignedInt = 0x1405

        type ClearMask = ColorBuffer = 0x00004000
                       | DepthBuffer = 0x00000100
                       | AccumBuffer = 0x00000200
                       | StencilBuffer = 0x00000400

        type Access = ReadOnly = 35000
                    | WriteOnly = 35001
                    | ReadWrite = 35002

        type All =
              Texture0 = 0x84C0
            | Texture1 = 0x84C1
            | Texture2 = 0x84C2
            | Texture3 = 0x84C3
            | Texture4 = 0x84C4
            | Texture5 = 0x84C5
            | Texture6 = 0x84C6
            | Texture7 = 0x84C7
            | Texture8 = 0x84C8
            | Texture9 = 0x84C9
            | Texture10 = 0x84CA
            | Texture11 = 0x84CB
            | Texture12 = 0x84CC
            | Texture13 = 0x84CD
            | Texture14 = 0x84CE
            | Texture15 = 0x84CF
            | Texture16 = 0x84D0
            | Texture17 = 0x84D1
            | Texture18 = 0x84D2
            | Texture19 = 0x84D3
            | Texture20 = 0x84D4
            | Texture21 = 0x84D5
            | Texture22 = 0x84D6
            | Texture23 = 0x84D7
            | Texture24 = 0x84D8
            | Texture25 = 0x84D9
            | Texture26 = 0x84DA
            | Texture27 = 0x84DB
            | Texture28 = 0x84DC
            | Texture29 = 0x84DD
            | Texture30 = 0x84DE
            | Texture31 = 0x84DF
            | Texture1D = 0x0DE0
            | Texture2D = 0x0DE1
            | Texture3D = 0x806F
            | TextureCubeMap = 0x8513
            | TextureBuffer = 0x8C2A
            | Texture2DMultisample = 0x9100
            | TextureRectangle = 0x84F5
            | Texture1DArray = 0x8C18
            | Texture2DArray = 0x8C1A
            | TextureCubeMapArray = 0x9009
            | Texture2DMultisampleArray = 0x9102
            | Framebuffer = 0x8D40
            | DrawFramebuffer = 0x8CA9
            | ReadFramebuffer = 0x8CA8
            | Blend = 0x0BE2
            | ClipDistance0 = 0x3000
            | ClipDistance1 = 0x3001
            | ClipDistance2 = 0x3002
            | ClipDistance3 = 0x3003
            | ClipDistance4 = 0x3004
            | ClipDistance5 = 0x3005
            | ClipDistance6 = 0x3006
            | ClipDistance7 = 0x3007
            | ColorLogicOperation = 0x0BF2
            | CullFace = 0x0B44
            | DebugOutput = 0x92E0
            | DebugOutputSynchronous = 0x8242
            | DepthClamp = 0x864F
            | DepthTest = 0x0B71
            | Dither = 0x0BD0
            | LineSmooth = 0x0B20
            | Multisample = 0x809D
            | PolygonOffsetFill = 0x8037
            | PolygonOffsetLine = 0x2A02
            | PolygonOffsetPoint = 0x2A01
            | PolygonSmooth = 0x0B41
            | PrimitiveRestart = 0x8F9D
            | PrimitiveRestartFixedIndex = 0x8D69
            | RasterizerDiscard = 0x8C89
            | SampleAlphaToCoverage = 0x809E
            | SampleAlphaToOne = 0x809F
            | SampleCoverage = 0x80A0
            | SampleShading = 0x8C36
            | SampleMask = 0x8E51
            | ScissorTest = 0x0C11
            | StencilTest = 0x0B90
            | TextureCubeMapSeamless = 0x884F
            | ProgramPointSize = 0x8642
            | Point = 0x1B00
            | Line = 0x1B01
            | Fill = 0x1B02
            | Never = 0x0200
            | Less = 0x0201
            | Equal = 0x0202
            | LessEqual = 0x0203
            | Greater = 0x0204
            | NotEqual = 0x0205
            | GreaterEqual = 0x0206
            | Always = 0x0207
            | Front = 0x0404
            | Back = 0x0405
            | FrontAndBack = 0x0408
            | Zero = 0
            | One = 1
            | SrcColor = 0x0300
            | InvSrcColor = 0x0301
            | SrcAlpha = 0x0302
            | InvSrcAlpha = 0x0303
            | DstAlpha = 0x0304
            | InvDstAlpha = 0x0305
            | DstColor = 0x0306
            | InvDstColor = 0x0307
            | SrcAlphaSat = 0x0308
            | ConstantColor = 0x8001
            | InvConstantColor = 0x8002
            | ConstantAlpha = 0x8003
            | InvConstantAlpha = 0x8004
            | Add = 0x8006
            | Subtract = 0x800A
            | ReverseSubtract = 0x800B
            | Keep = 0x1E00
            | Replace = 0x1E01
            | Increment = 0x1E02
            | IncrementWrap = 0x8507
            | Decrement = 0x1E03
            | DecrementWrap = 0x8508
            | Invert = 0x150A
            | Points = 0x0000
            | LineStrip = 0x0003
            | LineLoop = 0x0002
            | Lines = 0x0001
            | LineStripAdjacency = 0x000B
            | LinesAdjacency = 0x000A
            | TriangleStrip = 0x0005
            | TriangleFan = 0x0006
            | Triangles = 0x0004
            | TriangleStripAdjacency = 0x000D
            | TrianglesAdjacency = 0x000C
            | Patches = 0x000E
            | UnsignedByte = 0x1401
            | UnsignedShort = 0x1403
            | UnsignedInt = 0x1405
            | ColorBuffer = 0x00004000
            | DepthBuffer = 0x00000100
            | AccumBuffer = 0x00000200
            | StencilBuffer = 0x00000400
            | ReadOnly = 35000
            | WriteOnly = 35001
            | ReadWrite = 35002

    let mutable opengl32Lib = Unchecked.defaultof<_>
    let mutable private opengl32 = 0n

    /// <summary>
    /// imports some WGL functions. By using a separate module
    /// we ensure that those imports are not loaded until their first use
    /// which allows the system to work on other platforms dynamically.
    /// </summary>
    module private Wgl =

        [<Literal>]
        let gl = "opengl32.dll"

        [<System.Security.SuppressUnmanagedCodeSecurity()>]
        [<System.Runtime.InteropServices.DllImport(gl, EntryPoint = "wglGetDefaultProcAddress", ExactSpelling = true, SetLastError = true)>]
        extern IntPtr GetDefaultProcAddress(String lpszProc);

        [<System.Security.SuppressUnmanagedCodeSecurity()>]
        [<System.Runtime.InteropServices.DllImport(gl, EntryPoint = "wglGetProcAddress", ExactSpelling = true, SetLastError = true)>]
        extern IntPtr GLGetProcAddress(String lpszProc);

        [<System.Runtime.InteropServices.DllImport("kernel32.dll")>]
        extern nativeint LoadLibrary (string path)

        [<System.Runtime.InteropServices.DllImport("kernel32.dll")>]
        extern nativeint GetProcAddress(nativeint library, string name)

    /// <summary>
    /// imports some GLX functions. By using a separate module
    /// we ensure that those imports are not loaded until their first use
    /// which allows the system to work on other platforms dynamically.
    /// </summary>
    module private GLX =

        [<Literal>]
        let gl = "libGL.so.1"

        [<System.Security.SuppressUnmanagedCodeSecurity()>]
        [<System.Runtime.InteropServices.DllImport(gl, EntryPoint = "glXGetProcAddress", ExactSpelling = true, SetLastError = true)>]
        extern IntPtr GetProcAddress(String lpszProc);



    /// <summary>
    /// searches for a given name in the OpenGL-implementation
    /// by successively checking "GetDefaultProcAddress", "glGetProcAddress"
    /// </summary>
    let getProcAddressInternal (name : string) =
        match System.Environment.OSVersion with
        | Linux ->
            if opengl32 = 0n then
                opengl32Lib <- DynamicLinker.loadLibrary "libGL.so.1"
                opengl32 <- opengl32Lib.Handle

            match GLX.GetProcAddress name with
            | 0n -> 0n
            | ptr -> ptr

        | Mac ->
            let ctx = OpenTK.Graphics.GraphicsContext.CurrentContext :?> OpenTK.Graphics.IGraphicsContextInternal
            ctx.GetAddress name

            (*if opengl32 = 0n then
                let ctx : OpenTK.Graphics.IGraphicsContextInternal = failwith ""

                ctx.GetAddress(name)

                opengl32Lib <- DynamicLinker.loadLibrary "/System/Library/Frameworks/OpenGL.framework/Versions/A/Libraries/libGL.dylib"
                opengl32 <- opengl32Lib.Handle

            let handle = opengl32Lib.GetFunction(name).Handle
            if handle <> 0n then Log.warn "could not get function ptr: %A" name
            handle*)

        | Windows ->
            if opengl32 = 0n then
                opengl32Lib <- DynamicLinker.loadLibrary "Opengl32.dll"
                opengl32 <- opengl32Lib.Handle

            match Wgl.GetDefaultProcAddress name with
            | 0n -> match Wgl.GLGetProcAddress name with
                    | 0n -> match Wgl.GetProcAddress(opengl32, name) with
                            | 0n -> 0n
                            | ptr -> ptr
                    | ptr -> ptr
            | ptr -> ptr


    let rec getProcAddressProbing (suffixes : list<string>) (name : string) =
        match suffixes with
            | s::rest ->
                let ptr = getProcAddressInternal (name + s)
                if ptr <> 0n then ptr
                else getProcAddressProbing rest name
            | [] -> 0n

    let getProcAddress (name : string) =
         let address = getProcAddressProbing [""; "ARB"; "EXT"] name
         if address = 0n then
              Aardvark.Base.Report.Line(2, "[GL] Could not get \"{0}\" procedure address", name)
              0n
         else
              address

    let getGLVMProcAddress =
        let lib = DynamicLinker.loadLibrary "glvm"
        fun (name : string) ->
            let ptr = lib.GetFunction(name).Handle
            ptr

    /// <summary>
    /// wraps the given ptr as function efficiently
    ///
    /// </summary>
    let private wrap (ptr : nativeint) =
        UnmanagedFunctions.wrap ptr

    /// <summary>
    /// contains function-pointers for all needed OpenGL entry-points.
    /// </summary>
    module Pointers =
        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindVertexArray.xml
        /// </summary>
        let BindVertexArray  = getProcAddress "glBindVertexArray"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glUseProgram.xml
        /// </summary>
        let BindProgram  = getProcAddress "glUseProgram"

        let DispatchCompute = getProcAddress "glDispatchCompute"
        let GetInteger = getProcAddress "glGetIntegerv"
        let GetFloat = getProcAddress "glGetFloatv"
        let GetDouble = getProcAddress "glGetDoublev"
        let GetBoolean = getProcAddress "glGetBooleanv"

        let GetIndexedInteger = getProcAddress "glGetIntegeri_v"
        let GetIndexedInteger64 = getProcAddress "glGetInteger64i_v"

        let NamedBufferData = getProcAddress "glNamedBufferData"

        let BufferSubData = getProcAddress "glBufferSubData"
        let NamedBufferSubData = getProcAddress "glNamedBufferSubData"

        let MemoryBarrier = getProcAddress "glMemoryBarrier"

        let ClearBufferSubData = getProcAddress "glClearBufferSubData"
        let ClearNamedBufferSubData = getProcAddress "glClearNamedBufferSubData"

        let CopyBufferSubData = getProcAddress "glCopyBufferSubData"
        let CopyNamedBufferSubData = getProcAddress "glCopyNamedBufferSubData"

        let GetBufferSubData = getProcAddress "glGetBufferSubData"
        let GetNamedBufferSubData = getProcAddress "glGetNamedBufferSubData"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glActiveTexture.xml
        /// </summary>
        let ActiveTexture  = getProcAddress "glActiveTexture"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindSampler.xml
        /// </summary>
        let BindSampler  = getProcAddress "glBindSampler"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindTexture.xml
        /// </summary>
        let BindTexture  = getProcAddress "glBindTexture"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindBuffer.xml
        /// </summary>
        let BindBuffer  = getProcAddress "glBindBuffer"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindBufferBase.xml
        /// </summary>
        let BindBufferBase  = getProcAddress "glBindBufferBase"
        

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindBufferRange.xml
        /// </summary>
        let BindBufferRange = getProcAddress "glBindBufferRange"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindFramebuffer.xml
        /// </summary>
        let BindFramebuffer  = getProcAddress "glBindFramebuffer"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glViewport.xml
        /// </summary>
        let Viewport  = getProcAddress "glViewport"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glEnable.xml
        /// </summary>
        let Enable  = getProcAddress "glEnable"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDisable.xml
        /// </summary>
        let Disable  = getProcAddress "glDisable"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDepthFunc.xml
        /// </summary>
        let DepthFunc  = getProcAddress "glDepthFunc"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glCullFace.xml
        /// </summary>
        let CullFace  = getProcAddress "glCullFace"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glFrontFace.xml
        /// </summary>
        let FrontFace  = getProcAddress "glFrontFace"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBlendFuncSeparate.xml
        /// </summary>
        let BlendFuncSeparate  = getProcAddress "glBlendFuncSeparate"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBlendEquationSeparate.xml
        /// </summary>
        let BlendEquationSeparate  = getProcAddress "glBlendEquationSeparate"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBlendColor.xml
        /// </summary>
        let BlendColor  = getProcAddress "glBlendColor"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glPolygonMode.xml
        /// </summary>
        let PolygonMode  = getProcAddress "glPolygonMode"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glStencilFuncSeparate.xml
        /// </summary>
        let StencilFuncSeparate  = getProcAddress "glStencilFuncSeparate"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glStencilOpSeparate.xml
        /// </summary>
        let StencilOpSeparate  = getProcAddress "glStencilOpSeparate"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glPatchParameteri.xml
        /// </summary>
        let PatchParameter  = getProcAddress "glPatchParameteri"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDrawElements.xml
        /// </summary>
        let DrawElements  = getProcAddress "glDrawElements"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDrawArrays.xml
        /// </summary>
        let DrawArrays  = getProcAddress "glDrawArrays"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDrawElementsInstanced.xml
        /// </summary>
        let DrawElementsInstanced  = getProcAddress "glDrawElementsInstanced"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDrawArraysInstanced.xml
        /// </summary>
        let DrawArraysInstanced  = getProcAddress "glDrawArraysInstanced"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glClear.xml
        /// </summary>
        let Clear  = getProcAddress "glClear"
        let ClearBufferiv  = getProcAddress "glClearBufferiv"
        let ClearBufferfv  = getProcAddress "glClearBufferfv"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glBindImageTexture.xml
        /// </summary>
        let BindImageTexture = getProcAddress "glBindImageTexture"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glClearColor.xml
        /// </summary>
        let ClearColor  = getProcAddress "glClearColor"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glClearDepth.xml
        /// </summary>
        let ClearDepth  = getProcAddress "glClearDepth"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glClearStencil.xml
        /// </summary>
        let ClearStencil  = getProcAddress "glClearStencil"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glGetError.xml
        /// </summary>
        let GetError  = getProcAddress "glGetError"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glVertexAttribPointer.xml
        /// </summary>
        let VertexAttribPointer = getProcAddress "glVertexAttribPointer"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glVertexAttribDivisor.xml
        /// </summary>
        let VertexAttribDivisor = getProcAddress "glVertexAttribDivisor"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glEnableVertexAttribArray.xml
        /// </summary>
        let EnableVertexAttribArray = getProcAddress "glEnableVertexAttribArray"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/docbook4/xhtml/glDisableVertexAttribArray.xml
        /// </summary>
        let DisableVertexAttribArray = getProcAddress "glDisableVertexAttribArray"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glMultiDrawArraysIndirect.xhtml
        /// </summary>
        let MultiDrawArraysIndirect = getProcAddress "glMultiDrawArraysIndirect"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glMultiDrawElementsIndirect.xhtml
        /// </summary>
        let MultiDrawElementsIndirect = getProcAddress "glMultiDrawElementsIndirect"


        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glDepthMask.xhtml
        /// </summary>
        let DepthMask = getProcAddress "glDepthMask"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glStencilMask.xhtml
        /// </summary>
        let StencilMask = getProcAddress "glStencilMask"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glStencilMaskSeparate.xhtml
        /// </summary>
        let StencilMaskSeparate = getProcAddress "glStencilMaskSeparate"


        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glColorMask.xhtml
        /// </summary>
        let ColorMask = getProcAddress "glColorMask"

        /// <summary>
        /// https://www.opengl.org/sdk/docs/man/html/glDrawBuffers.xhtml
        /// </summary>
        let DrawBuffers = getProcAddress "glDrawBuffers"

        
        /// <summary>
        /// https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawBuffer.xhtml
        /// </summary>
        let DrawBuffer = getProcAddress "glDrawBuffer"

        
        let GenQueries = getProcAddress "glGenQueries"
        let DeleteQueries = getProcAddress "glDeleteQueries"
        let BeginQuery = getProcAddress "glBeginQuery"
        let EndQuery = getProcAddress "glEndQuery"
        let QueryCounter = getProcAddress "glQueryCounter"
        let GetQueryObjectInt32 = getProcAddress "glGetQueryObjectiv"
        let GetQueryObjectInt64 = getProcAddress "glGetQueryObjecti64v"
        let GetQueryObjectUInt32 = getProcAddress "glGetQueryObjectuiv"
        let GetQueryObjectUInt64 = getProcAddress "glGetQueryObjectui64v"


        let Uniform1iv = getProcAddress "glUniform1iv"
        let Uniform1fv = getProcAddress "glUniform1fv"
        let Uniform2iv = getProcAddress "glUniform2iv"
        let Uniform2fv = getProcAddress "glUniform2fv"
        let Uniform3iv = getProcAddress "glUniform3iv"
        let Uniform3fv = getProcAddress "glUniform3fv"
        let Uniform4iv = getProcAddress "glUniform4iv"
        let Uniform4fv = getProcAddress "glUniform4fv"
        let UniformMatrix2fv = getProcAddress "glUniformMatrix2fv"
        let UniformMatrix3fv = getProcAddress "glUniformMatrix3fv"
        let UniformMatrix4fv = getProcAddress "glUniformMatrix4fv"
        let TexParameteri = getProcAddress "glTexParameteri"
        let TexParameterf = getProcAddress "glTexParameterf"
        let VertexAttrib1f = getProcAddress "glVertexAttrib1f"
        let VertexAttrib2f = getProcAddress "glVertexAttrib2f"
        let VertexAttrib3f = getProcAddress "glVertexAttrib3f"
        let VertexAttrib4f = getProcAddress "glVertexAttrib4f"


        let HDrawArrays = getGLVMProcAddress "hglDrawArrays"
        let HDrawElements = getGLVMProcAddress "hglDrawElements"
        let HDrawArraysIndirect = getGLVMProcAddress "hglDrawArraysIndirect"
        let HDrawElementsIndirect = getGLVMProcAddress "hglDrawElementsIndirect"
        let HSetDepthBias = getGLVMProcAddress "hglSetDepthBias"
        let HSetBlendModes = getGLVMProcAddress "hglSetBlendModes"
        let HSetColorMasks = getGLVMProcAddress "hglSetColorMasks"
        let HSetStencilMode = getGLVMProcAddress "hglSetStencilMode"
        let HBindVertexAttributes = getGLVMProcAddress "hglBindVertexAttributes"

        let HBindTextures = getGLVMProcAddress "hglBindTextures"
        let HBindSamplers = getGLVMProcAddress "hglBindSamplers"



        let private pointerNames =
            [ BindVertexArray, "glBindVertexArray"
              BindProgram, "glUseProgram"
              ActiveTexture, "glActiveTexture"
              BindSampler, "glBindSampler"
              BindTexture, "glBindTexture"
              BindBuffer, "glBindBuffer"
              BindBufferBase, "glBindBufferBase"
              BindBufferRange, "glBindBufferRange"
              BindFramebuffer, "glBindFramebuffer"
              Viewport, "glViewport"
              Enable, "glEnable"
              Disable, "glDisable"
              DepthFunc, "glDepthFunc"
              CullFace, "glCullFace"
              BlendFuncSeparate, "glBlendFunc"
              BlendEquationSeparate, "glBlendEquationSeparate"
              BlendColor, "glBlendColor"
              PolygonMode, "glPolygonMode"
              StencilFuncSeparate, "glStencilFuncSeparate"
              StencilOpSeparate, "glStencilOpSeparate"
              PatchParameter, "glPatchParameteri"
              DrawElements, "glDrawElements"
              DrawArrays, "glDrawArrays"
              DrawElementsInstanced, "glDrawElementsInstanced"
              DrawArraysInstanced, "glDrawArraysInstanced"
              Clear, "glClear"
              ClearBufferiv, "glClearBufferiv"
              ClearBufferfv, "glClearBufferfv"
              BindImageTexture, "glBindImageTexture"
              ClearColor, "glClearColor"
              ClearDepth, "glClearDepth"
              GetError, "glGetError"
              VertexAttribPointer, "glVertexAttribPointer"
              VertexAttribDivisor, "glVertexAttribDivisor"
              EnableVertexAttribArray, "glEnableVertexAttribArray"
              DisableVertexAttribArray, "glDisableVertexAttribArray"

              Uniform1iv, "glUniform1iv"
              Uniform1fv, "glUniform1fv"
              Uniform2iv, "glUniform2iv"
              Uniform2fv, "glUniform2fv"
              Uniform3iv, "glUniform3iv"
              Uniform3fv, "glUniform3fv"
              Uniform4iv, "glUniform4iv"
              Uniform4fv, "glUniform4fv"
              UniformMatrix2fv, "glUniformMatrix2fv"
              UniformMatrix3fv, "glUniformMatrix3fv"
              UniformMatrix4fv, "glUniformMatrix4fv"

              TexParameterf, "glTexParameterf"
              TexParameteri, "glTexParameteri"

              VertexAttrib1f, "glVertexAttrib1f"
              VertexAttrib2f, "glVertexAttrib2f"
              VertexAttrib3f, "glVertexAttrib3f"
              VertexAttrib4f, "glVertexAttrib4f"


              MultiDrawArraysIndirect, "glMultiDrawArraysIndirect"
              MultiDrawElementsIndirect, "glMultiDrawElementsIndirect"
              DepthMask, "glDepthMask"
              ColorMask, "glColorMaski"
              DrawBuffers, "glDrawBuffers"

              HDrawArrays, "hglDrawArrays"
              HDrawElements, "hglDrawElements"
              HDrawArraysIndirect, "hglDrawArraysIndirect"
              HDrawElementsIndirect, "hglDrawElementsIndirect"
              HSetBlendModes, "hglSetBlendModes"
              HSetStencilMode, "hglSetStencilMode"
              HBindVertexAttributes, "hglBindVertexAttributes"
              HBindTextures, "hglBindTextures"
              HBindSamplers, "hglBindSamplers"

            ] |> Map.ofList

        let getPointerName (ptr : nativeint) =
            match Map.tryFind ptr pointerNames with
                | Some name -> name
                | _ -> ptr.ToString()
