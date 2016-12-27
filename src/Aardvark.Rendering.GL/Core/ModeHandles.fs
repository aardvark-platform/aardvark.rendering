namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4

#nowarn "9"

[<StructLayout(LayoutKind.Sequential)>]
type BeginMode =
    struct
        val mutable public Mode : int
        val mutable public PatchVertices : int

        new(m,v) = { Mode = m; PatchVertices = v }
    end

[<StructLayout(LayoutKind.Sequential)>]
type DrawCallInfoList =
    struct
        val mutable public Count : int64
        val mutable public Infos : nativeptr<DrawCallInfo>

        new(c : int, i) = { Count = int64 c; Infos = i }
    end

[<StructLayout(LayoutKind.Sequential)>]
type GLBlendMode =
    struct
        val mutable public Enabled : int
        val mutable public SourceFactor : int
        val mutable public DestFactor : int
        val mutable public Operation : int
        val mutable public SourceFactorAlpha : int
        val mutable public DestFactorAlpha : int
        val mutable public OperationAlpha : int
    end

[<StructLayout(LayoutKind.Sequential)>]
type GLStencilMode =
    struct
        val mutable public Enabled : int
        val mutable public CmpFront : int
        val mutable public MaskFront : uint32
        val mutable public ReferenceFront : int
        val mutable public CmpBack : int
        val mutable public MaskBack : uint32
        val mutable public ReferenceBack : int
        val mutable public OpFrontSF : int
        val mutable public OpFrontDF : int
        val mutable public OpFrontPass : int
        val mutable public OpBackSF : int
        val mutable public OpBackDF : int
        val mutable public OpBackPass : int
    end

[<StructLayout(LayoutKind.Sequential)>]
type DrawCallInfoListHandle =
    struct
        val mutable public Pointer : nativeptr<DrawCallInfoList>

        member x.Count
            with get() = NativePtr.read<int64> (NativePtr.cast x.Pointer) |> int
            and set (v : int) = NativePtr.write (NativePtr.cast x.Pointer) (int64 v)

        member x.Infos
            with get() : nativeptr<DrawCallInfo> = NativeInt.read (NativePtr.toNativeInt x.Pointer + 8n)
            and set (v : nativeptr<DrawCallInfo>) = NativeInt.write (NativePtr.toNativeInt x.Pointer + 8n) v
    
        new(ptr) = { Pointer = ptr }
    end

type DepthTestInfo =
    struct
        val mutable public Comparison : int
        val mutable public Clamp : int

        new(comparison, clamp) = { Comparison = comparison; Clamp = clamp }
    end

[<StructLayout(LayoutKind.Sequential)>]
type DepthTestModeHandle =
    struct
        val mutable public Pointer : nativeptr<DepthTestInfo>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type CullModeHandle =
    struct
        val mutable public Pointer : nativeptr<int>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type PolygonModeHandle =
    struct
        val mutable public Pointer : nativeptr<int>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type BeginModeHandle =
    struct
        val mutable public Pointer : nativeptr<BeginMode>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type IsActiveHandle =
    struct
        val mutable public Pointer : nativeptr<int>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type BlendModeHandle =
    struct
        val mutable public Pointer : nativeptr<GLBlendMode>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type StencilModeHandle =
    struct
        val mutable public Pointer : nativeptr<GLStencilMode>
        new(p) = { Pointer = p }
    end

[<StructLayout(LayoutKind.Sequential)>]
type VertexAttribPointer =
    struct
        val mutable public Type         : VertexAttribPointerType
        val mutable public Normalized   : int
        val mutable public Stride       : int
        val mutable public Offset       : int
        val mutable public Buffer       : int
        
        new(t,n,s,o,b) = { Type = t; Normalized = n; Stride = s; Offset = o; Buffer = b }
    end
[<StructLayout(LayoutKind.Sequential)>]
type VertexAttribValue =
    struct
        val mutable public X        : float32
        val mutable public Y        : float32
        val mutable public Z        : float32
        val mutable public W        : float32
        val mutable public Dummy    : int
        
        new(x,y,z,w) = { X = x; Y = y; Z = z; W = w; Dummy = 0 }
    end
[<StructLayout(LayoutKind.Explicit, Size = 32)>]
type VertexAttribBinding =
    struct
        [<FieldOffset(0)>]
        val mutable public Index    : uint32
        [<FieldOffset(4)>]
        val mutable public Size     : int
        [<FieldOffset(8)>]
        val mutable public Divisor  : int
        [<FieldOffset(12)>]
        val mutable Value : VertexAttribValue
        [<FieldOffset(12)>]
        val mutable Pointer : VertexAttribPointer

        private new(index, size, divisor) = { Index = index; Size = size; Divisor = divisor; Value = VertexAttribValue(0.0f,0.0f,0.0f,0.0f); Pointer = VertexAttribPointer(VertexAttribPointerType.Byte, 0, 0, 0, 0) }

        static member CreateValue(index : uint32, size : int, divisor : int, v : V4f) =
            let mutable res = VertexAttribBinding(index, size, divisor)
            res.Value <- VertexAttribValue(v.X, v.Y, v.Z, v.W)
            res

        static member CreatePointer(index : uint32, size : int, divisor : int, v : VertexAttribPointer) =
            let mutable res = VertexAttribBinding(index, size, divisor)
            res.Pointer <- v
            res

    end

[<StructLayout(LayoutKind.Sequential)>]
type VertexInputBinding =
    struct
    
        val mutable public IndexBuffer  : int
        val mutable public Count        : int
        val mutable public Bindings     : nativeptr<VertexAttribBinding>
        val mutable public VAO          : int
        val mutable public VAOContext   : nativeint
        new(i,c,b,v,vc) = { IndexBuffer = i; Count = c; Bindings = b; VAO = v; VAOContext = vc }
    end
    
[<StructLayout(LayoutKind.Sequential)>]
type VertexInputBindingHandle =
    struct
        val mutable public Pointer : nativeptr<VertexInputBinding>
        new(p) = { Pointer = p }
    end