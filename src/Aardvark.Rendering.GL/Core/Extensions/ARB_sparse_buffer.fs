﻿namespace Aardvark.Rendering.GL

#nowarn "9"
#nowarn "51"

open System
open System.Runtime.InteropServices
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open OpenTK.Graphics.OpenGL4
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

open ExtensionHelpers


[<AutoOpen>]
module ARB_sparse_buffer =

    type Marshal with
        static member GetDelegateForFunctionPointer<'a>(ptr : nativeint) : 'a =
            Marshal.GetDelegateForFunctionPointer(ptr, typeof<'a>) |> unbox<'a>

    type private BufferPageCommitmentDel = delegate of BufferTarget * nativeint * nativeint * bool -> unit
    type private NamedBufferPageCommitmentDel = delegate of int * nativeint * nativeint * bool -> unit

    type GL private() =
        static let supported = 
            let vendor = GL.GetString(StringName.Vendor).ToLower()
            let reallySupportsSparse = vendor.Contains("nvidia")  
            reallySupportsSparse && ExtensionHelpers.isSupported (Version(666,666,666)) "GL_ARB_sparse_buffer" 

        static let dBufferPageCommitment = 
            if supported then 
                let ptr = ExtensionHelpers.getAddress "glBufferPageCommitment"
                if ptr = 0n then
                    failwith "[GL] gc claimed to support GL_ARB_sparse_buffer but does not"

                Marshal.GetDelegateForFunctionPointer<BufferPageCommitmentDel>(ptr)
            else
                BufferPageCommitmentDel(fun _ _ _ _ -> failwith "[GL] does not support GL_ARB_sparse_buffer")

        static let dNamedBufferPageCommitment = 
            if supported then 
                let ptr = ExtensionHelpers.getAddress "glNamedBufferPageCommitment"
                if ptr <> 0n then
                    Marshal.GetDelegateForFunctionPointer<NamedBufferPageCommitmentDel>(ptr)
                else
                    NamedBufferPageCommitmentDel(fun b o s c ->
                        ExtensionHelpers.bindBuffer b (fun t ->
                            dBufferPageCommitment.Invoke(t, o, s, c)
                        )
                    )
            else
                NamedBufferPageCommitmentDel(fun _ _ _ _ -> failwith "[GL] does not support GL_ARB_sparse_buffer")

        static member ARB_sparse_buffer = supported

        static member BufferPageCommitment(target : BufferTarget, offset : nativeint, size : nativeint, commit : bool) =
            dBufferPageCommitment.Invoke(target, offset, size, commit)

        static member NamedBufferPageCommitment(buffer : int, offset : nativeint, size : nativeint, commit : bool) =
            dNamedBufferPageCommitment.Invoke(buffer, offset, size, commit)

    let private SparseStorageBit = unbox<BufferStorageFlags> 0x0400
    let private BufferPageSize = unbox<GetPName> 0x82F8

    type BufferStorageFlags with
        static member SparseStorageBit = SparseStorageBit

    type GetPName with
        static member BufferPageSize = BufferPageSize
        