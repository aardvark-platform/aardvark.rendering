namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Base.TypeInfo
open Aardvark.Rendering
open Aardvark.Rendering.TypeInfoExtensions.TypeInfo
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

            let private (|AttributePointerType|_|) (t : Type) =
                match t with
                | Float32 -> Some VertexAttribPointerType.Float
                | Float64 -> Some VertexAttribPointerType.Double
                | SByte   -> Some VertexAttribPointerType.Byte
                | Int16   -> Some VertexAttribPointerType.Short
                | Int32   -> Some VertexAttribPointerType.Int
                | Byte    -> Some VertexAttribPointerType.UnsignedByte
                | UInt16  -> Some VertexAttribPointerType.UnsignedShort
                | UInt32  -> Some VertexAttribPointerType.UnsignedInt
                | _ -> None

            let private (|AttributePointer|_|) (t : Type) =
                match t with
                | AttributePointerType t -> Some (V2i.II, t)
                | ColorOf (d, AttributePointerType t)
                | VectorOf (d, AttributePointerType t) -> Some (V2i(d, 1), t)
                | MatrixOf (d, AttributePointerType t) -> Some (d, t)
                | _ -> None

            let private (|Attribute|_|) =
                (|AttributePointer|_|) >> Option.map (fun (d, t) -> d, unbox<VertexAttribType> t)

            let private vertexAttribTypeSize =
                LookupTable.lookupTable [
                    VertexAttribType.Byte,          sizeof<int8>
                    VertexAttribType.UnsignedByte,  sizeof<uint8>
                    VertexAttribType.Short,         sizeof<int16>
                    VertexAttribType.UnsignedShort, sizeof<uint16>
                    VertexAttribType.Int,           sizeof<int32>
                    VertexAttribType.UnsignedInt,   sizeof<uint32>
                    VertexAttribType.HalfFloat,     sizeof<float16>
                    VertexAttribType.Float,         sizeof<float32>
                    VertexAttribType.Double,        sizeof<double>
                ]

            let inline private vertexAttribPointerTypeSize (t : VertexAttribPointerType) =
                t |> unbox<VertexAttribType> |> vertexAttribTypeSize

            let bindings (attributes : (int * Attribute)[]) =
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

                        match att.Type with
                        | MatrixOf(s, AttributePointerType t) ->
                            let normalized = if att.Normalized then 1 else 0
                            let rowSize = vertexAttribPointerTypeSize t * s.X

                            let stride =
                                if att.Stride = 0 then rowSize * s.Y
                                else att.Stride

                            for r in 0 .. s.Y - 1 do
                                let ptr = VertexBufferBinding(uint32 (index + r), s.X, divisor, t, normalized, stride, att.Offset + r * rowSize, buffer.Handle)
                                buffers.Add ptr

                        | ColorOf(d, Byte) ->
                            // C3b does not seem to work :/
                            if d <> 4 then
                                failf "cannot use %A for vertex or instance attribute buffers. Try any other color type instead." att.Type

                            // Only works if normalized = true, i.e. only can be used for floating point attributes
                            if not att.Normalized then
                                failf "%A vertex or instance attribute buffers can only be used for floating point attributes." att.Type

                            // See: https://registry.khronos.org/OpenGL-Refpages/gl4/html/glVertexAttribPointer.xhtml
                            let ptr = VertexBufferBinding(uint32 index, int All.Bgra, divisor, VertexAttribPointerType.UnsignedByte, 1, att.Stride, att.Offset, buffer.Handle)
                            buffers.Add ptr

                        | AttributePointer (d, t) when d.Y = 1 ->
                            let normalized = if att.Normalized then 1 else 0
                            let ptr = VertexBufferBinding(uint32 index, d.X, divisor, t, normalized, att.Stride, att.Offset, buffer.Handle)
                            buffers.Add ptr

                        | _ ->
                            failf "cannot use %A buffer as vertex or instance attribute buffer" att.Type

                    | Attribute.Value (value, norm) ->
                        let typ = value.GetType()

                        let dim, attributeType =
                            match typ with
                            | Attribute (d, t) -> d, t
                            | _ -> failf "cannot set value of %A as vertex or instance attribute" typ

                        let rowSize = uint64 <| vertexAttribTypeSize attributeType * dim.X

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
                                NativePtr.write pBinding <| VertexValueBinding(uint32 (index + r), attributeType, norm)

                                Buffer.MemoryCopy(pSrc, NativePtr.toNativeInt pBinding, 32UL, rowSize)
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

        member x.CreateVertexInputBinding (index : Option<Buffer>, attributes : (int * Attribute)[]) =
            let buffers, values = Attribute.bindings attributes
            let index = match index with | Some i -> i.Handle | _ -> 0

            let pBuffers = NativePtr.allocArray buffers
            let pValues = NativePtr.allocArray values

            let value = VertexInputBinding(index, buffers.Length, pBuffers, values.Length, pValues, -1, 0n)
            let ptr = NativePtr.alloc 1
            NativePtr.write ptr value
            VertexInputBindingHandle ptr

        member x.Update(ptr : VertexInputBindingHandle, index : Option<Buffer>, attributes : (int * Attribute)[]) =
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