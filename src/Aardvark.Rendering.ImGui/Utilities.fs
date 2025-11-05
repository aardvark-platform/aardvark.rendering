namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open System
open System.Text
open FSharp.NativeInterop
open Hexa.NET.ImGui

#nowarn "9"

[<AutoOpen>]
module internal Utilities =

    type TextBuffer() =
        let mutable handle = NativePtr.zero<uint8>
        let mutable currentSize = 0
        static let shared = TextBuffer()

        static member Shared = shared
        member _.Handle = handle
        member _.Size = currentSize

        member this.Text
            with get() =
                let data = Span<uint8>(NativePtr.toVoidPtr handle, currentSize)
                let length = max 0 (data.IndexOf 0uy)
                Encoding.UTF8.GetString(handle, length)

            and set (text: string) =
                use pText = fixed text
                let size = Encoding.UTF8.GetMaxByteCount (text.Length + 1)
                this.Resize size
                let length = Encoding.UTF8.GetBytes(pText, text.Length, handle, size)
                handle.[length] <- 0uy

        member _.Resize(size: int) =
            if currentSize <> size then
                NativePtr.free handle
                handle <- NativePtr.alloc<uint8> size
                currentSize <- size

        member this.InputTextResizeCallback(data: nativeptr<ImGuiInputTextCallbackData>) =
            let mutable data = NativePtr.toByRef data
            if data.EventFlag = ImGuiInputTextFlags.CallbackResize then
                this.Resize data.BufSize
                data.Buf <- this.Handle
            0

    module String =

        let ofPtrUtf8 (ptr: nativeptr<uint8>) =
            if NativePtr.isNullPtr ptr then null
            else
                let mutable length = 0
                while ptr.[length] <> 0uy do inc &length
                Encoding.UTF8.GetString(ptr, length)

        let toPtrUtf8 (str: string) =
            if isNull str then NativePtr.zero
            else
                let data = Encoding.UTF8.GetBytes str
                let ptr = NativePtr.alloc<uint8> (data.Length + 1)
                for i = 0 to data.Length - 1 do ptr.[i] <- data.[i]
                ptr.[data.Length] <- 0uy
                ptr

    type ImTextureRect with
        member inline this.Size = V2i(int this.W, int this.H)
        member inline this.Offset = V2i(int this.X, int this.Y)

    type ImTextureDataPtr with
        member inline this.Size = V2i(this.Width, this.Height)

        member this.GetNativeTensor() =
            let pData =
                let ptr = this.GetPixels()
                NativePtr.ofVoidPtr<uint8> ptr

            let srcInfo =
                let size = V4l(this.Width, this.Height, 1, 4)
                Tensor4Info(size, V4l(size.W, size.W * size.X, size.W * size.X * size.Y, 1L))

            NativeTensor4<uint8>(pData, srcInfo)

        member inline this.GetNativeTensor(window: ImTextureRect) =
            this.GetNativeTensor().SubTensor4(window.Offset.XYOO, V4i(window.Size, 1, 4))

    type ImDrawDataPtr with
        member inline this.Display =
            Box2d.FromMinAndSize(
                float this.DisplayPos.X, float this.DisplayPos.Y,
                float this.DisplaySize.X, float this.DisplaySize.Y
            )