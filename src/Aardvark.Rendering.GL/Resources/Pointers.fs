namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Base.TypeMeta
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

            // Build a table for fast lookup instead of using active patterns multiple times.
            let private attributeTable =
                let primitives =
                    [
                        struct (typeof<float16>, VertexAttribPointerType.HalfFloat)
                        struct (typeof<float32>, VertexAttribPointerType.Float)
                        struct (typeof<float>,   VertexAttribPointerType.Double)
                        struct (typeof<int8>,    VertexAttribPointerType.Byte)
                        struct (typeof<int16>,   VertexAttribPointerType.Short)
                        struct (typeof<int32>,   VertexAttribPointerType.Int)
                        struct (typeof<uint8>,   VertexAttribPointerType.UnsignedByte)
                        struct (typeof<uint16>,  VertexAttribPointerType.UnsignedShort)
                        struct (typeof<uint32>,  VertexAttribPointerType.UnsignedInt)
                    ]
                    |> List.map (fun struct (t, a) ->
                        struct (t, struct (V2i.II, a, t.GetCLRSize()))
                    )

                let vectors =
                    let sizes = [2; 3; 4]

                    primitives
                    |> List.collect (fun struct (t, (_, a, typeSize)) ->
                        sizes |> List.choose (fun s ->
                            VectorType.tryGet t s
                            |> Option.map (fun vt ->
                                struct (vt, struct (V2i(s, 1), a, typeSize))
                            )
                        )
                    )

                let colors =
                    let sizes = [3; 4]

                    primitives
                    |> List.collect (fun struct (t, (_, a, typeSize)) ->
                        sizes |> List.choose (fun s ->
                            ColorType.tryGet t s
                            |> Option.map (fun vt ->
                                struct (vt, struct (V2i(s, 1), a, typeSize))
                            )
                        )
                    )

                let matrices =
                    let sizes = MatrixType.all |> List.map _.Dimension |> List.distinct

                    primitives
                    |> List.collect (fun struct (t, (_, a, typeSize)) ->
                        sizes |> List.choose (fun s ->
                            MatrixType.tryGet t s
                            |> Option.map (fun vt ->
                                struct (vt, struct (s, a, typeSize))
                            )
                        )
                    )

                Dictionary.ofListV (primitives @ vectors @ colors @ matrices)

            let bindings (attributes : struct (int * Attribute)[]) =
                let buffers = System.Collections.Generic.List<_>()
                let values = System.Collections.Generic.List<_>()

                for (index, att) in attributes do
                    match att with
                    | Attribute.Buffer att ->
                        let buffer = att.Buffer

                        let divisor =
                            match att.Frequency with
                            | PerVertex -> 0
                            | PerInstances i -> i

                        match Dictionary.tryFindV att.Type attributeTable with
                        | ValueSome (s, t, typeSize) ->
                            if s.Y > 1 then
                                let rowSize = typeSize * s.X

                                let stride =
                                    if att.Stride = 0 then rowSize * s.Y
                                    else att.Stride

                                for r in 0 .. s.Y - 1 do
                                    let ptr = VertexBufferBinding(uint32 (index + r), s.X, divisor, t, att.Format, stride, att.Offset + r * rowSize, buffer.Handle)
                                    buffers.Add ptr

                            else
                                match att.Type with
                                | ColorOf(d, UInt8) ->
                                    // C3b does not seem to work :/
                                    if d <> 4 then
                                        failf "cannot use %A for vertex or instance attribute buffers. Try any other color type instead." att.Type

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

                                | _ ->
                                    let ptr = VertexBufferBinding(uint32 index, s.X, divisor, t, att.Format, att.Stride, att.Offset, buffer.Handle)
                                    buffers.Add ptr

                        | _ ->
                            failf "cannot use %A buffer as vertex or instance attribute buffer" att.Type

                    | Attribute.Value (value, format) ->
                        let typ = value.GetType()

                        let struct (dim, attributeType, typeSize) =
                            match Dictionary.tryFindV typ attributeTable with
                            | ValueSome (d, a, s) -> struct (d, unbox<VertexAttribType> a, s)
                            | _ -> failf "cannot set value of %A as vertex or instance attribute" typ

                        let rowSize = uint64 <| typeSize * dim.X

                        // Adjust BGRA layout of C3b and C4b
                        let value : obj =
                            match value with
                            | :? C3b as c -> c.ToArray()
                            | :? C4b as c -> c.ToArray()
                            | _ -> value

                        value |> NativeInt.pin (fun pValue ->
                            let mutable pSrc = pValue

                            for r = 0 to dim.Y - 1 do
                                let pBinding = NativePtr.stackalloc<VertexValueBinding> 1
                                NativePtr.write pBinding <| VertexValueBinding(uint32 (index + r), attributeType, format)

                                Buffer.MemoryCopy(pSrc, NativePtr.toNativeInt pBinding, 32UL, rowSize)
                                pSrc <- pSrc + nativeint rowSize

                                values.Add (NativePtr.read pBinding)
                        )

                struct (buffers.ToArray(), values.ToArray())

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

        member x.CreateVertexInputBinding (index : Option<Buffer>, attributes : struct (int * Attribute)[]) =
            let struct (buffers, values) = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0

            let pBuffers = NativePtr.allocArray buffers
            let pValues = NativePtr.allocArray values

            let value = VertexInputBinding(index, buffers.Length, pBuffers, values.Length, pValues, -1, 0n)
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            VertexInputBindingHandle ptr

        member x.Update(ptr : VertexInputBindingHandle, index : Option<Buffer>, attributes : struct (int * Attribute)[]) =
            let mutable value = NativePtr.read ptr.Pointer
            let struct (buffers, values) = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0

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