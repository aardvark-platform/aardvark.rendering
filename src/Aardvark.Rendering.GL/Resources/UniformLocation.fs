#if INTERACTIVE
#I @"E:\Development\Aardvark-2015\build\Release\AMD64"
#r "Aardvark.Base.TypeProviders.dll"
#r "Aardvark.Base.dll"
#r "Aardvark.Base.FSharp.dll"
#r "OpenTK.dll"
#r "FSharp.PowerPack.dll"
#r "FSharp.PowerPack.Linq.dll"
#r "FSharp.PowerPack.Metadata.dll"
#r "FSharp.PowerPack.Parallel.Seq.dll"
#r "Aardvark.Rendering.GL.dll"
open Aardvark.Rendering.GL
#else
namespace Aardvark.Rendering.GL
#endif
open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Linq
open Aardvark.Base.Incremental
open Aardvark.Base.ShaderReflection
open Aardvark.Rendering.GL


type UniformLocation(ctx : Context, size : int, uniformType : ShaderParameterType) =
    let data = Marshal.AllocHGlobal(size)

    member x.Free() =
        Marshal.FreeHGlobal data

    member x.Context = ctx
    member x.Size = size
    member x.Data = data
    member x.Type = uniformType

[<AutoOpen>]
module UniformLocationExtensions =
    type Context with
        member x.CreateUniformLocation(size : int, uniformType : ShaderParameterType) =
            UniformLocation(x, size, uniformType)

        member x.Delete(loc : UniformLocation) =
            loc.Free()
