namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering
open Microsoft.FSharp.NativeInterop
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

#nowarn "9"

[<AutoOpen>]
module PointerContextExtensions =
    [<AutoOpen>]
    module private Helpers =
        let inline (!) (p : nativeptr<'a>) = NativePtr.read p
        let inline (:=) (p : nativeptr<'a>) (v : 'a) = NativePtr.write p v

        let inline ptr (v : 'a) =
            let p = NativePtr.alloc 1
            p := v
            p

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
                SourceFactor = Translations.toGLFactor mode.SourceFactor,
                DestFactor = Translations.toGLFactor mode.DestinationFactor,
                Operation = Translations.toGLOperation mode.Operation,
                SourceFactorAlpha = Translations.toGLFactor mode.SourceAlphaFactor,
                DestFactorAlpha = Translations.toGLFactor mode.DestinationAlphaFactor,
                OperationAlpha = Translations.toGLOperation mode.AlphaOperation
            )

        let inline toGLStencilMode (mode : StencilMode) =
            GLStencilMode(
                Enabled = (if mode.IsEnabled then 1 else 0),
                CmpFront = Translations.toGLFunction mode.CompareFront.Function,
                MaskFront = mode.CompareFront.Mask,
                ReferenceFront = mode.CompareFront.Reference,
                CmpBack = Translations.toGLFunction mode.CompareBack.Function,
                MaskBack = mode.CompareBack.Mask,
                ReferenceBack = mode.CompareBack.Reference,

                OpFrontSF = Translations.toGLStencilOperation mode.OperationFront.StencilFail,
                OpFrontDF = Translations.toGLStencilOperation mode.OperationFront.DepthFail,
                OpFrontPass = Translations.toGLStencilOperation mode.OperationFront.DepthPass,

                OpBackSF = Translations.toGLStencilOperation mode.OperationBack.StencilFail,
                OpBackDF = Translations.toGLStencilOperation mode.OperationBack.DepthFail,
                OpBackPass = Translations.toGLStencilOperation mode.OperationBack.DepthPass
            )

        module Attribute =

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
                    | (true, (bt,r,c)) -> Some (bt,r,c)
                    | _ -> None

            let private (|Rgba|_|) (t : Type) =
                if t = typeof<C4b> then Some ()
                else None

            let bindings (attributes : list<int * AttributeDescription>) =
                let buffers = System.Collections.Generic.List<_>()
                let values = System.Collections.Generic.List<_>()

                for (index, att) in attributes do
                    match att.Content with
                        | Left buffer ->
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
                                    let ptr = VertexBufferBinding(uint32 index, att.Dimension, divisor, att.VertexAttributeType, (if att.Normalized then 1 else 0), att.Stride, att.Offset, buffer.Handle)
                                    buffers.Add ptr

                        | Right value ->
                            values.Add(VertexValueBinding(uint32 index, value.X, value.Y, value.Z, value.W))
                
                buffers.ToArray(), values.ToArray()

    module NativePtr =
        let allocArray (a :'a[]) =
            let ptr = NativePtr.alloc a.Length
            for i in 0 .. a.Length-1 do NativePtr.set ptr i a.[i]
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


        member x.ToDepthTest(mode : DepthTestMode) =
            let value = Translations.toGLComparison mode.Comparison
            let clamp = if mode.Clamp then 1 else 0
            DepthTestInfo(value, clamp)

        member x.ToDepthBias(state : DepthBiasState) =
            DepthBiasInfo(float32 state.Constant, float32 state.SlopeScale, float32 state.Clamp)

        member x.ToCullMode(mode : CullMode) =
            Translations.toGLCullMode mode

        member x.ToFrontFace(frontFace : Aardvark.Base.Rendering.WindingOrder) =
            Translations.toGLFrontFace frontFace

        member x.ToPolygonMode(mode : FillMode) =
            Translations.toGLPolygonMode mode

        member x.ToBlendMode(mode : BlendMode) =
            toGLBlendMode mode

        member x.ToStencilMode(mode : StencilMode) =
            toGLStencilMode mode

//        member private x.GetVertexBindings(index : Option<Buffer>, attributes : list<int * AttributeDescription>) =
//            let res = System.Collections.Generic.List<_>()
//
//            for (index, att) in attributes do
//                match att.Content with
//                    | Left buffer ->
//                        let divisor =
//                            match att.Frequency with
//                                | PerVertex -> 0
//                                | PerInstances i -> i
//
//                       
//
//                        let ptr = VertexAttribPointer(att.VertexAttributeType, (if att.Normalized then 1 else 0), att.Stride, buffer.Handle)
//                        res.Add (VertexAttribBinding.CreatePointer(uint32 index, att.Dimension, divisor, ptr))
//
//                    | Right value ->
//                        res.Add (VertexAttribBinding.CreateValue(uint32 index, att.Dimension, -1, value))
//            
//            let ibo =
//                match index with
//                    | Some i -> i.Handle
//                    | _ -> 0
//
//            ibo, res.ToArray()

        member x.CreateVertexInputBinding (index : Option<Buffer>, attributes : list<int * AttributeDescription>) =
            let buffers, values = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0

            let pBuffers = NativePtr.allocArray buffers
            let pValues = NativePtr.allocArray values

            let value = VertexInputBinding(index, buffers.Length, pBuffers, values.Length, pValues, -1, 0n)
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            VertexInputBindingHandle ptr

        member x.Update(ptr : VertexInputBindingHandle, index : Option<Buffer>, attributes : list<int * AttributeDescription>) =
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