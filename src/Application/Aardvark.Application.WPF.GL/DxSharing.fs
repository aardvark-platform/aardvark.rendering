namespace Aardvark.Application.WPF
//
//open System
//open System.Runtime.InteropServices
//open Aardvark.Base
//open Aardvark.Rendering.GL
//open SharpDX.Direct3D9
//open OpenTK.Graphics.OpenGL4
//
//module private WGL = 
//    [<DllImport("user32.dll", SetLastError = false)>]
//    extern nativeint GetDesktopWindow()
//
//    [<DllImport("opengl32.dll")>]
//    extern nativeint wglGetProcAddress(string name)
//
//type private Access =
//    | ReadOnly     = 0x0000
//    | ReadWrite    = 0x0001
//    | WriteDiscard = 0x0002
//
//[<AllowNullLiteral>]
//type private ISharedTexture =
//    inherit IDisposable
//    abstract member OpenGlTexture : int
//    abstract member DirectXSurface : Surface
//    abstract member Size : V2i
//    abstract member Lock : IDisposable
//
//type private WGLSharing(ctx : Context, d3d : Direct3DEx, device : DeviceEx) =
//    let wrap (name : string) =
//        using ctx.ResourceLock (fun _ -> UnmanagedFunctions.wrap <| WGL.wglGetProcAddress name)
//
//    let wglDXSetResourceShareHandleNV : nativeint -> nativeint -> unit = 
//        wrap "wglDXSetResourceShareHandleNV"
//
//    let wglDXOpenDeviceNV : nativeint -> nativeint = 
//        wrap "wglDXOpenDeviceNV"
//
//    let wglDXCloseDeviceNV : nativeint -> unit = 
//        wrap "wglDXCloseDeviceNV"
//
//    let wglDXRegisterObjectNV : nativeint -> nativeint -> int -> int -> Access -> nativeint = 
//        wrap "wglDXRegisterObjectNV"
//
//    let wglDXUnregisterObjectNV : nativeint -> bool =
//        wrap "wglDXUnregisterObjectNV"
//
//    let wglDXObjectAccessNV : nativeint -> Access -> bool =
//        wrap "wglDXObjectAccessNV"
//
//    let wglDXLockObjectsNV : nativeint -> int -> nativeint[] -> bool =
//        wrap "wglDXLockObjectsNV"
//
//    let wglDXUnlockObjectsNV : nativeint -> int -> nativeint[] -> bool =
//        wrap "wglDXUnlockObjectsNV"
//
//    let shareDevice = wglDXOpenDeviceNV(device.NativePointer)
//
//    member x.Dispose() =
//        using ctx.ResourceLock (fun _ ->
//            wglDXCloseDeviceNV shareDevice
//        )
//
//    member x.GetSharedTexture(dxSurface : Surface, wddmShareHandle : nativeint) =
// 
//        let glTexture, shareHandle =
//            using ctx.ResourceLock (fun _ ->
//                let glTexture = GL.GenTexture()
//                let share = wglDXRegisterObjectNV shareDevice dxSurface.NativePointer glTexture (int All.Texture2D) Access.WriteDiscard
//                
//                glTexture, share
//            )
//
//        let size = V2i(dxSurface.Description.Width, dxSurface.Description.Height)
//        
//        let lockT() =
//            using ctx.ResourceLock (fun _ ->
//                if not <| wglDXLockObjectsNV shareDevice 1 [|shareHandle|] then
//                    failwith "could not lock shared texture"
//            )
//
//        let unlockT() =
//            using ctx.ResourceLock (fun _ ->
//                if not <| wglDXUnlockObjectsNV shareDevice 1 [|shareHandle|] then
//                    failwith "could not lock shared texture"
//            )
//
//        { new ISharedTexture with
//            member x.Dispose() =
//                // wglDXUnregisterObjectNV shareHandle
//                dxSurface.Dispose()
//                using ctx.ResourceLock (fun _ ->
//                    GL.DeleteTexture glTexture
//                )
//
//            member x.OpenGlTexture = glTexture
//            member x.DirectXSurface = dxSurface
//            member x.Size = size
//            member x.Lock = 
//                lockT()
//                { new IDisposable with member x.Dispose() = unlockT() }
//        }
//
//    interface IDisposable with 
//        member x.Dispose() = x.Dispose()
