namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Rendering
open Microsoft.FSharp.NativeInterop
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
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
                BeginMode(int BeginMode.Patches, Translations.toPatchCount mode)
            else
                BeginMode(Translations.toGLMode mode, 0)

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
                let res = System.Collections.Generic.List<_>()

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
                                        let ptr = VertexAttribPointer(bt, normalized, stride, att.Offset + r * rowSize, buffer.Handle)
                                        res.Add (VertexAttribBinding.CreatePointer(uint32 index, c, divisor, ptr))

                                | Rgba ->
                                    let ptr = VertexAttribPointer(att.VertexAttributeType, 1, att.Stride, att.Offset, buffer.Handle)
                                    res.Add (VertexAttribBinding.CreatePointer(uint32 index, att.Dimension, divisor, ptr))

                                | _ -> 
                                    let ptr = VertexAttribPointer(att.VertexAttributeType, (if att.Normalized then 1 else 0), att.Stride, att.Offset, buffer.Handle)
                                    res.Add (VertexAttribBinding.CreatePointer(uint32 index, att.Dimension, divisor, ptr))

                        | Right value ->
                            res.Add (VertexAttribBinding.CreateValue(uint32 index, att.Dimension, -1, value))
                
                res.ToArray()

    type Context with

        member x.CreateIsActive(active : bool) =
            let value = if active then 1 else 0
            IsActiveHandle(ptr value)

        member x.Update(handle : IsActiveHandle, active : bool) =
            handle.Pointer := if active then 1 else 0

        member x.Delete(handle : IsActiveHandle) =
            NativePtr.free handle.Pointer


        member x.CreateBeginMode(mode : IndexedGeometryMode, hasTessellation : bool) =
            let value = toBeginMode hasTessellation mode
            BeginModeHandle(ptr value)

        member x.Update(handle : BeginModeHandle, mode : IndexedGeometryMode, hasTessellation : bool) =
            let value = toBeginMode hasTessellation mode
            handle.Pointer := value

        member x.Delete(handle : BeginModeHandle) =
            NativePtr.free handle.Pointer


        member x.CreateDrawCallInfoList(calls : DrawCallInfo[]) =
            let infos = NativePtr.alloc calls.Length
            for i in 0 .. calls.Length - 1 do infos.[i] <- calls.[i]
            DrawCallInfoListHandle(NativePtr.alloc 1, Count = calls.Length, Infos = infos)

        member x.Update(list : DrawCallInfoListHandle, calls : DrawCallInfo[]) =
            if list.Count = calls.Length then
                let ptr = list.Infos
                for i in 0 .. calls.Length-1 do
                    ptr.[i] <- calls.[i]

            else
                NativePtr.free list.Infos
                let ptr = NativePtr.alloc calls.Length
                for i in 0 .. calls.Length-1 do
                    ptr.[i] <- calls.[i]

                let mutable list = list // F# is very picky with property-setters
                list.Count <- calls.Length
                list.Infos <- ptr

        member x.Delete(list : DrawCallInfoListHandle) =
            NativePtr.free list.Infos
            NativePtr.free list.Pointer


        member x.CreateDepthTest(mode : DepthTestMode) =
            let value = Translations.toGLComparison mode.Comparison
            let clamp = if mode.Clamp then 1 else 0
            let value = DepthTestInfo(value, clamp)
            DepthTestModeHandle(ptr value)

        member x.Update(gl : DepthTestModeHandle, mode : DepthTestMode) =
            let value = Translations.toGLComparison mode.Comparison
            let clamp = if mode.Clamp then 1 else 0
            gl.Pointer := DepthTestInfo(value, clamp)

        member x.Delete(gl : DepthTestModeHandle) =
            NativePtr.free gl.Pointer


        member x.CreateCullMode(mode : CullMode) =
            let value = Translations.toGLFace mode
            CullModeHandle(ptr value)

        member x.Update(gl : CullModeHandle, mode : CullMode) =
            gl.Pointer := Translations.toGLFace mode

        member x.Delete(gl : CullModeHandle) =
            NativePtr.free gl.Pointer


        member x.CreatePolygonMode(mode : FillMode) =
            let value = Translations.toGLPolygonMode mode
            PolygonModeHandle(ptr value)

        member x.Update(handle : PolygonModeHandle, mode : FillMode) =
            handle.Pointer := Translations.toGLPolygonMode mode

        member x.Delete(handle : PolygonModeHandle) =
            NativePtr.free handle.Pointer


        member x.CreateBlendMode(mode : BlendMode) =
            let value = toGLBlendMode mode
            BlendModeHandle(ptr value)

        member x.Update(handle : BlendModeHandle, value : BlendMode) =
            handle.Pointer := toGLBlendMode value

        member x.Delete(handle : BlendModeHandle) =
            NativePtr.free handle.Pointer


        member x.CreateStencilMode(mode : StencilMode) =
            let value = toGLStencilMode mode
            StencilModeHandle(ptr value)

        member x.Update(handle : StencilModeHandle, mode : StencilMode) =
            handle.Pointer := toGLStencilMode mode

        member x.Delete(handle : StencilModeHandle) =
            NativePtr.free handle.Pointer


//
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
            let bindings = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0
            let ptr = NativePtr.alloc bindings.Length
            for i in 0 .. bindings.Length - 1 do NativePtr.set ptr i bindings.[i]
            let value = VertexInputBinding(index, bindings.Length, ptr, -1, 0n)
            let res = NativePtr.alloc 1
            NativePtr.write res value
            VertexInputBindingHandle res

        member x.Update(binding : VertexInputBindingHandle, index : Option<Buffer>, attributes : list<int * AttributeDescription>) =
            let bindings = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0

            let mutable value = NativePtr.read binding.Pointer
            if bindings.Length = value.Count then
                for i in 0 .. bindings.Length - 1 do  
                    NativePtr.set value.Bindings i bindings.[i]
            else
                NativePtr.free value.Bindings
                let ptr = NativePtr.alloc bindings.Length
                for i in 0 .. bindings.Length - 1 do NativePtr.set ptr i bindings.[i]
                value.Count <- bindings.Length
                value.Bindings <- ptr

            value.IndexBuffer <- index
            value.VAOContext <- 0n
            NativePtr.write binding.Pointer value

        member x.Delete(b : VertexInputBindingHandle) =
            let v = NativePtr.read b.Pointer
            if v.VAO > 0 then
                use t = x.ResourceLock
                GL.DeleteVertexArray(v.VAO)
            NativePtr.free v.Bindings
            NativePtr.free b.Pointer