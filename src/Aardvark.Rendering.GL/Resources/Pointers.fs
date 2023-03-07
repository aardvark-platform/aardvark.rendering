namespace Aardvark.Rendering.GL

open System
open System.Runtime.InteropServices
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

        type nativeptr<'a when 'a : unmanaged> with
            member x.Item
                with inline get (i : int) = NativePtr.get x i
                and inline set (i : int) (v : 'a) = NativePtr.set x i v

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

            let private (|AttributeType|_|) (t : Type) =
                match t with
                | Float32 -> Some VertexAttribType.Float
                | Float64 -> Some VertexAttribType.Double
                | SByte   -> Some VertexAttribType.Byte
                | Int16   -> Some VertexAttribType.Short
                | Int32   -> Some VertexAttribType.Int
                | Byte    -> Some VertexAttribType.UnsignedByte
                | UInt16  -> Some VertexAttribType.UnsignedShort
                | UInt32  -> Some VertexAttribType.UnsignedInt
                | _ -> None

            let private vertexAttribPointerTypeSize =
                LookupTable.lookupTable [
                    VertexAttribPointerType.Byte, sizeof<int8>
                    VertexAttribPointerType.UnsignedByte, sizeof<uint8>
                    VertexAttribPointerType.Short, sizeof<int16>
                    VertexAttribPointerType.UnsignedShort, sizeof<uint16>
                    VertexAttribPointerType.Int, sizeof<int32>
                    VertexAttribPointerType.UnsignedInt, sizeof<uint32>
                    VertexAttribPointerType.HalfFloat, sizeof<float16>
                    VertexAttribPointerType.Float, sizeof<float32>
                    VertexAttribPointerType.Double, sizeof<double>
                ]

            let private matrixTypes =
                Dictionary.ofList [
                    typeof<M22f>, (VertexAttribPointerType.Float, 2, 2)
                    typeof<M23f>, (VertexAttribPointerType.Float, 2, 3)
                    typeof<M33f>, (VertexAttribPointerType.Float, 3, 3)
                    typeof<M34f>, (VertexAttribPointerType.Float, 3, 4)
                    typeof<M44f>, (VertexAttribPointerType.Float, 4, 4)

                    typeof<M22d>, (VertexAttribPointerType.Double, 2, 2)
                    typeof<M23d>, (VertexAttribPointerType.Double, 2, 3)
                    typeof<M33d>, (VertexAttribPointerType.Double, 3, 3)
                    typeof<M34d>, (VertexAttribPointerType.Double, 3, 4)
                    typeof<M44d>, (VertexAttribPointerType.Double, 4, 4)

                    typeof<M22i>, (VertexAttribPointerType.Int, 2, 2)
                    typeof<M23i>, (VertexAttribPointerType.Int, 2, 3)
                    typeof<M33i>, (VertexAttribPointerType.Int, 3, 3)
                    typeof<M34i>, (VertexAttribPointerType.Int, 3, 4)
                    typeof<M44i>, (VertexAttribPointerType.Int, 4, 4)

                ]

            let private (|MatrixOf|_|) (t : Type) =
                match matrixTypes.TryGetValue t with
                | (true, (bt, r, c)) -> Some (bt, r, c)
                | _ -> None

            let private (|Rgba|_|) (t : Type) =
                if t = typeof<C4b> then Some ()
                else None

            let bindings (attributes : (int * AttributeDescription)[]) =
                let buffers = System.Collections.Generic.List<_>()
                let values = System.Collections.Generic.List<_>()

                for (index, att) in attributes do
                    match att with
                    | AttributeDescription.Buffer att ->
                        let buffer = att.Buffer

                        let divisor =
                            match att.Frequency with
                            | PerVertex -> 0
                            | PerInstances i -> i

                        match att.Type with
                        | MatrixOf(bt, r, c) ->
                            let normalized = if att.Normalized then 1 else 0
                            let rowSize = vertexAttribPointerTypeSize bt * c

                            let stride =
                                if att.Stride = 0 then rowSize * r
                                else att.Stride

                            for r in 0 .. r - 1 do
                                let ptr = VertexBufferBinding(uint32 (index + r), c, divisor, bt, normalized, stride, att.Offset + r * rowSize, buffer.Handle)
                                buffers.Add ptr

                        | Rgba ->
                            let ptr = VertexBufferBinding(uint32 index, att.Dimension, divisor, att.VertexAttributeType, 1, att.Stride, att.Offset, buffer.Handle)
                            buffers.Add ptr

                        | _ ->
                            let normalized = if att.Normalized then 1 else 0
                            let ptr = VertexBufferBinding(uint32 index, att.Dimension, divisor, att.VertexAttributeType, normalized, att.Stride, att.Offset, buffer.Handle)
                            buffers.Add ptr

                    | AttributeDescription.Value value ->
                        let typ = value.GetType()

                        let attributeType =
                            match typ with
                            | AttributeType t -> t
                            | VectorOf(_, AttributeType t) -> t
                            | _ -> failf "cannot set value of type '%A' as vertex or instance attribute" typ

                        let pBinding = NativePtr.stackalloc<VertexValueBinding> 1
                        let binding = NativePtr.toByRef pBinding
                        binding <- VertexValueBinding(uint32 index, attributeType)

                        // TODO: Matrix & BGRA layout for C3b etc.
                        pinned value (fun pValue ->
                            let size = uint64 <| Marshal.SizeOf typ
                            let pDst = NativePtr.toNativeInt pBinding
                            Buffer.MemoryCopy(pValue.ToPointer(), pDst.ToPointer(), 32UL, size)
                        )

                        values.Add binding

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

        member x.CreateVertexInputBinding (index : Option<Buffer>, attributes : (int * AttributeDescription)[]) =
            let buffers, values = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0

            let pBuffers = NativePtr.allocArray buffers
            let pValues = NativePtr.allocArray values

            let value = VertexInputBinding(index, buffers.Length, pBuffers, values.Length, pValues, -1, 0n)
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            VertexInputBindingHandle ptr

        member x.Update(ptr : VertexInputBindingHandle, index : Option<Buffer>, attributes : (int * AttributeDescription)[]) =
            let mutable value = NativePtr.read ptr.Pointer
            let buffers, values = Attribute.bindings attributes
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