namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4

module ExtensionHelpers =
    let private suffixes = [""; "ARB"; "EXT"; "NV"; "AMD"]

    let vendor() =
        let str = GL.GetString(StringName.Vendor).ToLower()
        if str.Contains "nvidia" then GPUVendor.nVidia
        elif str.Contains "amd" || str.Contains "ati" then GPUVendor.AMD
        elif str.Contains "intel" then GPUVendor.Intel
        else GPUVendor.Unknown

    let rec private hasExtension (i : int) (name : string) =
        if i >= 0 then
            let ext = GL.GetString(StringNameIndexed.Extensions, uint32 i)
            if ext = name then true
            else hasExtension (i - 1) name
        else 
            false

    let isSupported (v : Version) (e : string) =
        let ctx = GraphicsContext.CurrentContext
        if isNull ctx then failwithf "[GL] cannot initialize %s without a context" e

        let version = Version(GL.GetInteger(GetPName.MajorVersion), GL.GetInteger(GetPName.MinorVersion), 0)
        if version >= v then
            true
        else
            let count = GL.GetInteger(GetPName.NumExtensions)
            let res = hasExtension (count - 1) e
            res

    let getAddress (name : string) =
        let ctx = GraphicsContext.CurrentContext
        if isNull ctx then failwithf "[GL] cannot get proc address for %s without a context" name
        let ctx = unbox<IGraphicsContextInternal> ctx

        suffixes |> List.tryPick (fun s ->
            let ptr = ctx.GetAddress(name + s)
            if ptr = 0n then None
            else Some ptr
        )
        |> Option.defaultValue 0n

    let inline bindBuffer (b : int) (action : BufferTarget -> 'a) =
        let old = GL.GetInteger(unbox 0x90EF)
        try
            GL.BindBuffer(BufferTarget.DispatchIndirectBuffer, b)
            action BufferTarget.DispatchIndirectBuffer
        finally
            GL.BindBuffer(BufferTarget.DispatchIndirectBuffer, old)
        
    let inline bindBuffers (b0 : int) (b1 : int) (action : BufferTarget -> BufferTarget -> 'a) =
        let oRead = GL.GetInteger(unbox 0x8F36)
        let oWrite = GL.GetInteger(unbox 0x8F37)
        try
            GL.BindBuffer(BufferTarget.CopyReadBuffer, b0)
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, b1)
            action BufferTarget.CopyReadBuffer BufferTarget.CopyWriteBuffer
        finally
            GL.BindBuffer(BufferTarget.CopyReadBuffer, oRead)
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, oWrite)
 