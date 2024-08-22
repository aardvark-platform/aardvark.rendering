namespace Aardvark.Rendering.GL


open System
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.ShaderReflection
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module ProgramResourceExtensions = 

    type GraphicsContext with
        static member GetFunction<'a when 'a :> Delegate>(name : string) =
            let ctx = GraphicsContext.CurrentContext |> unbox<IGraphicsContextInternal>
            let ptr = ctx.GetAddress name
            if ptr = 0n then failwithf "[GL] could not get address for %s" name
            System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>
                    
    module private Impl = 
        type GetProgramResourceName = delegate of int * ProgramInterface * int * int * byref<int> * byte[] -> unit
        type GetProgramResource = delegate of int * ProgramInterface * int * int * byref<ProgramProperty> * int * byref<int> * array<int> -> unit

        let glGetProgramResourceName = lazy ( GraphicsContext.GetFunction<GetProgramResourceName> "glGetProgramResourceName" )
        let glGetProgramResourceiv = lazy ( GraphicsContext.GetFunction<GetProgramResource> "glGetProgramResourceiv" )

    type GL with
        static member GetProgramResource(program : int, iface : ProgramInterface, index : int, prop : ProgramProperty) =
            let mutable length = 0
            let mutable prop = prop
            let mutable res = [| 0 |]
            Impl.glGetProgramResourceiv.Value.Invoke(program, iface, index, 1, &prop, 1, &length, res)
            GL.Check "GetProgramResource[0]"
            res.[0]

        static member GetProgramResource(program : int, iface : ProgramInterface, index : int, prop : ProgramProperty, cnt : int) =
            let mutable length = 0
            let mutable prop = prop
            let mutable res = Array.zeroCreate cnt
            Impl.glGetProgramResourceiv.Value.Invoke(program, iface, index, 1, &prop, cnt, &length, res)
            GL.Check "GetProgramResource"
            res

        static member GetProgramResourceName(program : int, iface : ProgramInterface, index : int) =
            let len = GL.GetProgramResource(program, iface, index, ProgramProperty.NameLength)
            let buffer = Array.zeroCreate len
            let mutable realLength = 0
            Impl.glGetProgramResourceName.Value.Invoke(program, iface, index, len, &realLength, buffer)
            GL.Check "GetProgramResourceName"
            System.Text.Encoding.UTF8.GetString(buffer, 0, realLength)


        static member GetProgramResourceType(program : int, iface : ProgramInterface, index : int) =
            GL.GetProgramResource(program, iface, index, ProgramProperty.Type) |> unbox<ActiveUniformType>
                    
        static member GetProgramResourceLocation(program : int, iface : ProgramInterface, index : int) =
            GL.GetProgramResource(program, iface, index, ProgramProperty.Location)

    type ProgramProperty with
        static member inline ReferencedByComputeShader = 0x930B |> unbox<ProgramProperty>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ShaderParameterType =
    let ofActiveUniformType =
        LookupTable.lookup [
            ActiveUniformType.Bool, Bool
            ActiveUniformType.BoolVec2, Vector(Bool, 2)
            ActiveUniformType.BoolVec3, Vector(Bool, 3)
            ActiveUniformType.BoolVec4, Vector(Bool, 4)

            ActiveUniformType.Double, Double
            ActiveUniformType.DoubleVec2, Vector(Double, 2)
            ActiveUniformType.DoubleVec3, Vector(Double, 3)
            ActiveUniformType.DoubleVec4, Vector(Double, 4)

            ActiveUniformType.Float, Float
            ActiveUniformType.FloatVec2, Vector(Float, 2)
            ActiveUniformType.FloatVec3, Vector(Float, 3)
            ActiveUniformType.FloatVec4, Vector(Float, 4)
            ActiveUniformType.FloatMat2, Matrix(Float, 2, 2, false)
            ActiveUniformType.FloatMat2x3, Matrix(Float, 2, 3, false)
            ActiveUniformType.FloatMat2x4, Matrix(Float, 2, 4, false)
            ActiveUniformType.FloatMat3x2, Matrix(Float, 3, 2, false)
            ActiveUniformType.FloatMat3, Matrix(Float, 3, 3, false)
            ActiveUniformType.FloatMat3x4, Matrix(Float, 3, 4, false)
            ActiveUniformType.FloatMat4, Matrix(Float, 4, 4, false)
            ActiveUniformType.FloatMat4x2, Matrix(Float, 4, 2, false)
            ActiveUniformType.FloatMat4x3, Matrix(Float, 4, 3, false)

            ActiveUniformType.Int,                      Int
            ActiveUniformType.IntVec2,                  Vector(Int, 2)
            ActiveUniformType.IntVec3,                  Vector(Int, 3)
            ActiveUniformType.IntVec4,                  Vector(Int, 4)

            ActiveUniformType.UnsignedInt,              UnsignedInt
            ActiveUniformType.UnsignedIntVec2,          Vector(UnsignedInt, 2)
            ActiveUniformType.UnsignedIntVec3,          Vector(UnsignedInt, 3)
            ActiveUniformType.UnsignedIntVec4,          Vector(UnsignedInt, 4)


            ActiveUniformType.Image1D,                      Image(Float, TextureDimension.Texture1D, false, false)
            ActiveUniformType.Image1DArray,                 Image(Float, TextureDimension.Texture1D, false, true)
            ActiveUniformType.Image2D,                      Image(Float, TextureDimension.Texture2D, false, false)
            ActiveUniformType.Image2DArray,                 Image(Float, TextureDimension.Texture2D, false, true)
            ActiveUniformType.Image2DMultisample,           Image(Float, TextureDimension.Texture2D, true, false)
            ActiveUniformType.Image2DMultisampleArray,      Image(Float, TextureDimension.Texture2D, true, true)
            ActiveUniformType.Image2DRect,                  Image(Float, TextureDimension.Texture2D, false, false)
            ActiveUniformType.Image3D,                      Image(Float, TextureDimension.Texture3D, false, false)
            ActiveUniformType.ImageBuffer,                  Image(Float, TextureDimension.Texture1D, false, false)
            ActiveUniformType.ImageCube,                    Image(Float, TextureDimension.TextureCube, false, false)
            ActiveUniformType.ImageCubeMapArray,            Image(Float, TextureDimension.TextureCube, false, true)

            ActiveUniformType.IntImage1D,                   Image(Int, TextureDimension.Texture1D, false, false)
            ActiveUniformType.IntImage1DArray,              Image(Int, TextureDimension.Texture1D, false, true)
            ActiveUniformType.IntImage2D,                   Image(Int, TextureDimension.Texture2D, false, false)
            ActiveUniformType.IntImage2DArray,              Image(Int, TextureDimension.Texture2D, false, true)
            ActiveUniformType.IntImage2DMultisample,        Image(Int, TextureDimension.Texture2D, true, false)
            ActiveUniformType.IntImage2DMultisampleArray,   Image(Int, TextureDimension.Texture2D, true, true)
            ActiveUniformType.IntImage2DRect,               Image(Int, TextureDimension.Texture2D, false, false)
            ActiveUniformType.IntImage3D,                   Image(Int, TextureDimension.Texture3D, false, false)
            ActiveUniformType.IntImageBuffer,               Image(Int, TextureDimension.Texture1D, false, false)
            ActiveUniformType.IntImageCube,                 Image(Int, TextureDimension.TextureCube, false, false)
            ActiveUniformType.IntImageCubeMapArray,         Image(Int, TextureDimension.TextureCube, false, true)

            ActiveUniformType.IntSampler1D,                 ShaderParameterType.Sampler(Int, TextureDimension.Texture1D, false, false, false) 
            ActiveUniformType.IntSampler1DArray,            ShaderParameterType.Sampler(Int, TextureDimension.Texture1D, false, true, false)
            ActiveUniformType.IntSampler2D,                 ShaderParameterType.Sampler(Int, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.IntSampler2DArray,            ShaderParameterType.Sampler(Int, TextureDimension.Texture2D, false, true, false)
            ActiveUniformType.IntSampler2DMultisample,      ShaderParameterType.Sampler(Int, TextureDimension.Texture2D, true, false, false)
            ActiveUniformType.IntSampler2DMultisampleArray, ShaderParameterType.Sampler(Int, TextureDimension.Texture2D, true, true, false)
            ActiveUniformType.IntSampler2DRect,             ShaderParameterType.Sampler(Int, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.IntSampler3D,                 ShaderParameterType.Sampler(Int, TextureDimension.Texture3D, false, false, false)
            ActiveUniformType.IntSamplerBuffer,             ShaderParameterType.Sampler(Int, TextureDimension.Texture1D, false, false, false)
            ActiveUniformType.IntSamplerCube,               ShaderParameterType.Sampler(Int, TextureDimension.TextureCube, false, false, false)
            ActiveUniformType.IntSamplerCubeMapArray,       ShaderParameterType.Sampler(Int, TextureDimension.TextureCube, false, true, false)

            ActiveUniformType.Sampler1D,                    ShaderParameterType.Sampler(Float, TextureDimension.Texture1D, false, false, false) 
            ActiveUniformType.Sampler1DArray,               ShaderParameterType.Sampler(Float, TextureDimension.Texture1D, false, true, false)
            ActiveUniformType.Sampler2D,                    ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.Sampler2DArray,               ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, false, true, false)
            ActiveUniformType.Sampler2DMultisample,         ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, true, false, false)
            ActiveUniformType.Sampler2DMultisampleArray,    ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, true, true, false)
            ActiveUniformType.Sampler2DRect,                ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.Sampler3D,                    ShaderParameterType.Sampler(Float, TextureDimension.Texture3D, false, false, false)
            ActiveUniformType.SamplerBuffer,                ShaderParameterType.Sampler(Float, TextureDimension.Texture1D, false, false, false)
            ActiveUniformType.SamplerCube,                  ShaderParameterType.Sampler(Float, TextureDimension.TextureCube, false, false, false)
            ActiveUniformType.SamplerCubeMapArray,          ShaderParameterType.Sampler(Float, TextureDimension.TextureCube, false, true, false)
                    

            ActiveUniformType.Sampler1DShadow,                    ShaderParameterType.Sampler(Float, TextureDimension.Texture1D, false, false, true) 
            ActiveUniformType.Sampler1DArrayShadow,               ShaderParameterType.Sampler(Float, TextureDimension.Texture1D, false, true, true)
            ActiveUniformType.Sampler2DShadow,                    ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, false, false, true)
            ActiveUniformType.Sampler2DArrayShadow,               ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, false, true, true)
            ActiveUniformType.Sampler2DRectShadow,                ShaderParameterType.Sampler(Float, TextureDimension.Texture2D, false, false, true)
            ActiveUniformType.SamplerCubeShadow,                  ShaderParameterType.Sampler(Float, TextureDimension.TextureCube, false, false, true)
            ActiveUniformType.SamplerCubeMapArrayShadow,          ShaderParameterType.Sampler(Float, TextureDimension.TextureCube, false, true, true)
                    

            ActiveUniformType.UnsignedIntAtomicCounter,           AtomicCounter UnsignedInt

            ActiveUniformType.UnsignedIntImage1D,                   Image(UnsignedInt, TextureDimension.Texture1D, false, false)
            ActiveUniformType.UnsignedIntImage1DArray,              Image(UnsignedInt, TextureDimension.Texture1D, false, true)
            ActiveUniformType.UnsignedIntImage2D,                   Image(UnsignedInt, TextureDimension.Texture2D, false, false)
            ActiveUniformType.UnsignedIntImage2DArray,              Image(UnsignedInt, TextureDimension.Texture2D, false, true)
            ActiveUniformType.UnsignedIntImage2DMultisample,        Image(UnsignedInt, TextureDimension.Texture2D, true, false)
            ActiveUniformType.UnsignedIntImage2DMultisampleArray,   Image(UnsignedInt, TextureDimension.Texture2D, true, true)
            ActiveUniformType.UnsignedIntImage2DRect,               Image(UnsignedInt, TextureDimension.Texture2D, false, false)
            ActiveUniformType.UnsignedIntImage3D,                   Image(UnsignedInt, TextureDimension.Texture3D, false, false)
            ActiveUniformType.UnsignedIntImageBuffer,               Image(UnsignedInt, TextureDimension.Texture1D, false, false)
            ActiveUniformType.UnsignedIntImageCube,                 Image(UnsignedInt, TextureDimension.TextureCube, false, false)
            ActiveUniformType.UnsignedIntImageCubeMapArray,         Image(UnsignedInt, TextureDimension.TextureCube, false, true)

                    

            ActiveUniformType.UnsignedIntSampler1D,                 ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture1D, false, false, false) 
            ActiveUniformType.UnsignedIntSampler1DArray,            ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture1D, false, true, false)
            ActiveUniformType.UnsignedIntSampler2D,                 ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.UnsignedIntSampler2DArray,            ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture2D, false, true, false)
            ActiveUniformType.UnsignedIntSampler2DMultisample,      ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture2D, true, false, false)
            ActiveUniformType.UnsignedIntSampler2DMultisampleArray, ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture2D, true, true, false)
            ActiveUniformType.UnsignedIntSampler2DRect,             ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture2D, false, false, false)
            ActiveUniformType.UnsignedIntSampler3D,                 ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture3D, false, false, false)
            ActiveUniformType.UnsignedIntSamplerBuffer,             ShaderParameterType.Sampler(UnsignedInt, TextureDimension.Texture1D, false, false, false)
            ActiveUniformType.UnsignedIntSamplerCube,               ShaderParameterType.Sampler(UnsignedInt, TextureDimension.TextureCube, false, false, false)
            ActiveUniformType.UnsignedIntSamplerCubeMapArray,       ShaderParameterType.Sampler(UnsignedInt, TextureDimension.TextureCube, false, true, false)


        ]
    

    open FShade.GLSL

    let rec ofGLSLType (t : GLSLType) =
        match t with
            | GLSLType.Void -> failwithf "[GL] void cannot be a parameter type"
            | GLSLType.Bool -> ShaderParameterType.Bool
            | GLSLType.Int(true,(8|16|32)) -> ShaderParameterType.Int
            | GLSLType.Int(false,(8|16|32)) -> ShaderParameterType.UnsignedInt
            | GLSLType.Float((16|32|64)) -> ShaderParameterType.Float

            | GLSLType.Vec(d,b) -> ShaderParameterType.Vector(ofGLSLType b, d)
            | GLSLType.Mat(r,c,b) -> ShaderParameterType.Matrix(ofGLSLType b, r, c, true)
            | GLSLType.Array(l, b, s) -> ShaderParameterType.FixedArray(ofGLSLType b, s, l)
            | GLSLType.Struct(name, fields, size) ->
                ShaderParameterType.Struct(size, fields |> List.map (fun (name, typ, offset) -> { Name = name; Type = ofGLSLType typ; Offset = offset }))

        
            | _ -> failwithf "[GL] bad parameter type: %A" t

