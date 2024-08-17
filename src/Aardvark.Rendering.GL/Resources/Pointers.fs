namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Base.TypeInfo
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module PointerContextExtensions =

    [<AutoOpen>]
    module private Helpers =

        let inline toBeginMode (hasTessellation : bool) (mode : IndexedGeometryMode) =
            if hasTessellation then
                GLBeginMode(int BeginMode.Patches, Translations.toPatchCount mode)
            else
                GLBeginMode(Translations.toGLMode mode, 0)

        let inline toGLBlendMode (mode : BlendMode) =
            GLBlendMode(
                Enabled = (if mode.Enabled then 1 else 0),
                SourceFactor = Translations.toGLFactor mode.SourceColorFactor,
                DestFactor = Translations.toGLFactor mode.DestinationColorFactor,
                Operation = Translations.toGLOperation mode.ColorOperation,
                SourceFactorAlpha = Translations.toGLFactor mode.SourceAlphaFactor,
                DestFactorAlpha = Translations.toGLFactor mode.DestinationAlphaFactor,
                OperationAlpha = Translations.toGLOperation mode.AlphaOperation
            )

        let inline toGLColorMask (mask : ColorMask) =
            GLColorMask(
                R = (if mask &&& ColorMask.Red   <> ColorMask.None then 1 else 0),
                G = (if mask &&& ColorMask.Green <> ColorMask.None then 1 else 0),
                B = (if mask &&& ColorMask.Blue  <> ColorMask.None then 1 else 0),
                A = (if mask &&& ColorMask.Alpha <> ColorMask.None then 1 else 0)
            )

        let inline toGLStencilMode (mode : StencilMode) =
            GLStencilMode(
                Enabled = (if mode.Enabled then 1 else 0),
                Cmp = Translations.toGLCompareFunction mode.Comparison,
                Mask = uint32 mode.CompareMask,
                Reference = mode.Reference,
                OpStencilFail = Translations.toGLStencilOperation mode.Fail,
                OpDepthFail = Translations.toGLStencilOperation mode.DepthFail,
                OpPass = Translations.toGLStencilOperation mode.Pass
            )

        module Attribute =

            let private attributeTypeTable = // (dimension, VertexAttribPointerType, elementSize, isByteColor)
                Dictionary.ofArrayV [|
                    typeof<M22i>, struct(V2i(2,2), VertexAttribPointerType.Int, sizeof<int32>, false)
                    typeof<M23i>, struct(V2i(3,2), VertexAttribPointerType.Int, sizeof<int32>, false)
                    typeof<M33i>, struct(V2i(3,3), VertexAttribPointerType.Int, sizeof<int32>, false)
                    typeof<M34i>, struct(V2i(4,3), VertexAttribPointerType.Int, sizeof<int32>, false)
                    typeof<M44i>, struct(V2i(4,4), VertexAttribPointerType.Int, sizeof<int32>, false)

                    typeof<M22f>, struct(V2i(2,2), VertexAttribPointerType.Float, sizeof<float32>, false)
                    typeof<M23f>, struct(V2i(3,2), VertexAttribPointerType.Float, sizeof<float32>, false)
                    typeof<M33f>, struct(V2i(3,3), VertexAttribPointerType.Float, sizeof<float32>, false)
                    typeof<M34f>, struct(V2i(4,3), VertexAttribPointerType.Float, sizeof<float32>, false)
                    typeof<M44f>, struct(V2i(4,4), VertexAttribPointerType.Float, sizeof<float32>, false)

                    typeof<M22d>, struct(V2i(2,2), VertexAttribPointerType.Double, sizeof<double>, false)
                    typeof<M23d>, struct(V2i(3,2), VertexAttribPointerType.Double, sizeof<double>, false)
                    typeof<M33d>, struct(V2i(3,3), VertexAttribPointerType.Double, sizeof<double>, false)
                    typeof<M34d>, struct(V2i(4,3), VertexAttribPointerType.Double, sizeof<double>, false)
                    typeof<M44d>, struct(V2i(4,4), VertexAttribPointerType.Double, sizeof<double>, false)

                    typeof<C3b>,  struct(V2i(3, 1), VertexAttribPointerType.UnsignedShort, sizeof<uint8>,   true)
                    typeof<C3us>, struct(V2i(3, 1), VertexAttribPointerType.UnsignedShort, sizeof<uint16>,  false)
                    typeof<C3ui>, struct(V2i(3, 1), VertexAttribPointerType.UnsignedInt,   sizeof<uint32>,  false)
                    typeof<C3f>,  struct(V2i(3, 1), VertexAttribPointerType.Float,         sizeof<float32>, false)
                    typeof<C3d>,  struct(V2i(3, 1), VertexAttribPointerType.Double,        sizeof<double>,  false)

                    typeof<C4b>,  struct(V2i(4, 1), VertexAttribPointerType.UnsignedByte,  sizeof<uint8>,   true)
                    typeof<C4us>, struct(V2i(4, 1), VertexAttribPointerType.UnsignedShort, sizeof<uint16>,  false)
                    typeof<C4ui>, struct(V2i(4, 1), VertexAttribPointerType.UnsignedInt,   sizeof<uint32>,  false)
                    typeof<C4f>,  struct(V2i(4, 1), VertexAttribPointerType.Float,         sizeof<float32>, false)
                    typeof<C4d>,  struct(V2i(4, 1), VertexAttribPointerType.Double,        sizeof<double>,  false)

                    typeof<V2i>,  struct(V2i(2, 1), VertexAttribPointerType.Int,           sizeof<int32>,  false)
                    typeof<V2ui>, struct(V2i(2, 1), VertexAttribPointerType.UnsignedInt,   sizeof<uint32>, false)
                    typeof<V2f>,  struct(V2i(2, 1), VertexAttribPointerType.Float,         sizeof<float32>, false)
                    typeof<V2d>,  struct(V2i(2, 1), VertexAttribPointerType.Double,        sizeof<double>, false)

                    typeof<V3i>,  struct(V2i(3, 1), VertexAttribPointerType.Int,           sizeof<int32>, false)
                    typeof<V3ui>, struct(V2i(3, 1), VertexAttribPointerType.UnsignedInt,   sizeof<uint32>, false)
                    typeof<V3f>,  struct(V2i(3, 1), VertexAttribPointerType.Float,         sizeof<float32>, false)
                    typeof<V3d>,  struct(V2i(3, 1), VertexAttribPointerType.Double,        sizeof<double>, false)

                    typeof<V4i>,  struct(V2i(4, 1), VertexAttribPointerType.Int,           sizeof<int32>, false)
                    typeof<V4ui>, struct(V2i(4, 1), VertexAttribPointerType.UnsignedInt,   sizeof<uint32>, false)
                    typeof<V4f>,  struct(V2i(4, 1), VertexAttribPointerType.Float,         sizeof<float32>, false)
                    typeof<V4d>,  struct(V2i(4, 1), VertexAttribPointerType.Double,        sizeof<double>, false)

                    typeof<int8>,    struct(V2i(1, 1), VertexAttribPointerType.Byte,          sizeof<int8>, false)
                    typeof<int16>,   struct(V2i(1, 1), VertexAttribPointerType.Short,         sizeof<int16>, false)
                    typeof<int32>,   struct(V2i(1, 1), VertexAttribPointerType.Int,           sizeof<int32>, false)
                    typeof<uint8>,   struct(V2i(1, 1), VertexAttribPointerType.UnsignedByte,  sizeof<uint8>, false)
                    typeof<uint16>,  struct(V2i(1, 1), VertexAttribPointerType.UnsignedShort, sizeof<uint16>, false)
                    typeof<uint32>,  struct(V2i(1, 1), VertexAttribPointerType.UnsignedInt,   sizeof<uint32>, false)
                    typeof<float16>, struct(V2i(1, 1), VertexAttribPointerType.HalfFloat,     sizeof<float16>, false)
                    typeof<float32>, struct(V2i(1, 1), VertexAttribPointerType.Float,         sizeof<float32>, false)
                    typeof<double>,  struct(V2i(1, 1), VertexAttribPointerType.Double,        sizeof<double>, false)
                |]


            let bindings (attributes : struct(int * Attribute)[]) =
                let buffers = System.Collections.Generic.List<_>()
                let values = System.Collections.Generic.List<_>()

                for (index, att) in attributes do
                    match att with
                    | Attribute.Buffer att ->

                        match Dictionary.tryFindV att.Type attributeTypeTable with
                        | ValueSome struct(d, t, s, isByteColor) ->
                            
                            let buffer = att.Buffer

                            let divisor = 
                                match att.Frequency with
                                | PerVertex -> 0
                                | PerInstances i -> i

                            if d.Y = 1 then

                                if isByteColor then

                                    if d.X = 3 then
                                        failwith "cannot use C3b for vertex or instance attribute buffers. Try any other color type instead."

                                    // Only works if normalized = true, i.e. only can be used for floating point attributes
                                    if att.Format <> VertexAttributeFormat.Normalized then
                                        failf "%A vertex or instance attribute buffers can only be used for normalized floating point attributes." att.Type

                                    // See: https://registry.khronos.org/OpenGL-Refpages/gl4/html/glVertexAttribPointer.xhtml
                                    let ptr =
                                        VertexBufferBinding(
                                            uint32 index, int All.Bgra, divisor,
                                            VertexAttribPointerType.UnsignedByte, VertexAttributeFormat.Normalized,
                                            att.Stride, att.Offset, buffer.Handle
                                        )

                                    buffers.Add ptr
                                else

                                    let ptr = VertexBufferBinding(uint32 index, d.X, divisor, t, att.Format, att.Stride, att.Offset, buffer.Handle)
                                    buffers.Add ptr

                            else
                                let rowSize = s * d.X

                                let stride =
                                    if att.Stride = 0 then rowSize * d.Y
                                    else att.Stride

                                for r in 0 .. d.Y - 1 do
                                    let ptr = VertexBufferBinding(uint32 (index + r), d.X, divisor, t, att.Format, stride, att.Offset + r * rowSize, buffer.Handle)
                                    buffers.Add ptr

                        | _ ->
                                
                            failf "cannot use %A buffer as vertex or instance attribute buffer" att.Type

                    | Attribute.Value (value, format) ->
                        let typ = value.GetType()

                        let dim, attributeType, rowSize =
                            match Dictionary.tryFindV typ attributeTypeTable with
                            | ValueSome struct(d, t, s, _) -> d, t, d.X * s
                            | _ -> failf "cannot set value of %A as vertex or instance attribute" typ

                        // Adjust BGRA layout of C3b and C4b
                        let value : obj =
                            match value with
                            | :? C3b as c -> c.ToArray()
                            | :? C4b as c -> c.ToArray()
                            | _ -> value

                        pinned value (fun pValue ->
                            let mutable pSrc = pValue

                            for r = 0 to dim.Y - 1 do
                                let pBinding = NativePtr.stackalloc<VertexValueBinding> 1
                                NativePtr.write pBinding <| VertexValueBinding(uint32 (index + r), attributeType |> unbox<VertexAttribType>, format)

                                Buffer.MemoryCopy(pSrc, NativePtr.toNativeInt pBinding, 32UL, uint64 rowSize)
                                pSrc <- pSrc + nativeint rowSize

                                values.Add (NativePtr.read pBinding)
                        )


                buffers.ToArray(), values.ToArray()

    module NativePtr =
        let setArray (a : 'a[]) (ptr : nativeptr<'a>) =
            for i in 0 .. a.Length-1 do NativePtr.set ptr i a.[i]

        let allocArray (a :'a[]) =
            let ptr = NativePtr.alloc a.Length
            setArray a ptr
            ptr

    type Context with

        member x.ToIsActive(active : bool) =
            if active then 1 else 0

        member x.ToBeginMode(mode : IndexedGeometryMode, hasTessellation : bool) =
            toBeginMode hasTessellation mode

        member x.CreateDrawCallInfoList(calls : DrawCallInfo[]) =
            let infos = NativePtr.alloc calls.Length
            for i in 0 .. calls.Length - 1 do infos.[i] <- calls.[i]
            DrawCallInfoList(calls.Length, infos)

        member x.Update(list : DrawCallInfoList, calls : DrawCallInfo[]) =
            if list.Count = int64 calls.Length then
                let ptr = list.Infos
                for i in 0 .. calls.Length-1 do
                    ptr.[i] <- calls.[i]

                list
            else
                NativePtr.free list.Infos
                let ptr = NativePtr.alloc calls.Length
                for i in 0 .. calls.Length-1 do
                    ptr.[i] <- calls.[i]

                let mutable list = list // F# is very picky with property-setters
                list.Count <- int64 calls.Length
                list.Infos <- ptr
                list

        member x.Delete(list : DrawCallInfoList) =
            NativePtr.free list.Infos


        member x.ToDepthTest(test : DepthTest) =
            Translations.toGLDepthTest test

        member x.ToDepthBias(state : DepthBias) =
            DepthBiasInfo(float32 state.Constant, float32 state.SlopeScale, float32 state.Clamp)

        member x.ToCullMode(mode : CullMode) =
            Translations.toGLCullMode mode

        member x.ToFrontFace(frontFace : Aardvark.Rendering.WindingOrder) =
            Translations.toGLFrontFace frontFace

        member x.ToPolygonMode(mode : FillMode) =
            Translations.toGLPolygonMode mode

        member x.ToBlendMode(mode : BlendMode) =
            toGLBlendMode mode

        member x.ToColorMask(mask : ColorMask) =
            toGLColorMask mask

        member x.ToStencilMode(mode : StencilMode) =
            toGLStencilMode mode

        member x.CreateVertexInputBinding (index : ValueOption<Buffer>, attributes : struct(int * Attribute)[]) =
            let buffers, values = Attribute.bindings attributes
            let index = match index with | ValueSome i -> i.Handle | _ -> 0

            let pBuffers = NativePtr.allocArray buffers
            let pValues = NativePtr.allocArray values

            let value = VertexInputBinding(index, buffers.Length, pBuffers, values.Length, pValues, -1, 0n)
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            VertexInputBindingHandle ptr

        member x.Update(ptr : VertexInputBindingHandle, index : ValueOption<Buffer>, attributes : struct(int * Attribute)[]) =
            let mutable value = NativePtr.read ptr.Pointer
            let buffers, values = Attribute.bindings attributes
            let index = match index with | ValueSome i -> i.Handle | _ -> 0

            let mutable value = value

            if buffers.Length = value.BufferBindingCount then
                for i in 0 .. buffers.Length - 1 do  
                    NativePtr.set value.BufferBindings i buffers.[i]
            else
                NativePtr.free value.BufferBindings
                let pBuffers = NativePtr.allocArray buffers
                value.BufferBindingCount <- buffers.Length
                value.BufferBindings <- pBuffers

            if values.Length = value.ValueBindingCount then
                for i in 0 .. values.Length - 1 do  
                    NativePtr.set value.ValueBindings i values.[i]
            else
                NativePtr.free value.ValueBindings
                let pValues = NativePtr.allocArray values
                value.ValueBindingCount <- values.Length
                value.ValueBindings <- pValues

            if value.VAO > 0 then
                if value.VAOContext <> IntPtr.Zero then
                    GLVM.hglDeleteVAO(value.VAOContext, value.VAO)
                    value.VAOContext <- 0n
                    value.VAO <- 0
                else
                    Log.warn "[GL] VertexInputBindingHandle.Update: Invalid VAOContext"

            value.IndexBuffer <- index
            NativePtr.write ptr.Pointer value

        member x.Delete(ptr : VertexInputBindingHandle) =
            let v = NativePtr.read ptr.Pointer
            if v.VAO > 0 then
                if v.VAOContext <> IntPtr.Zero then
                    GLVM.hglDeleteVAO(v.VAOContext, v.VAO)
                else
                     Log.warn "[GL] VertexInputBindingHandle.Delete: Invalid VAOContext"
            NativePtr.free v.BufferBindings
            NativePtr.free v.ValueBindings
            NativePtr.free ptr.Pointer