namespace Aardvark.Rendering.NanoVg

#nowarn "9"

open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Base.Incremental
open NanoVgSharp

type INvg =
    interface end

type INvgApplicator =
    inherit INvg
    abstract member Child : IMod<INvg>

module Nvg =
    
    [<AbstractClass>]
    type AbstractApplicator(child : IMod<INvg>) =
        member x.Child = child
        interface INvgApplicator with
            member x.Child = child

        new(child : INvg) = AbstractApplicator(Mod.constant child)

    type ContextApplicator(context : Context.NanoVgContext, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Context = context

    type TrafoApplicator(trafo : IMod<M33d>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Trafo = trafo

    type ScissorApplicator(bounds : IMod<Box2d>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Bounds = bounds

    type FillColorApplicator(color : IMod<C4f>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Color = color


    type PrimitiveLeaf(mode : IMod<PrimitiveMode>, primitive : IMod<Primitive>) =
        interface INvg
        member x.Mode = mode
        member x.Primitive = primitive
        
    type StrokeWidthApplicator(width : IMod<float>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Width = width

    type StrokeColorApplicator(color : IMod<C4f>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Color = color

    type LineCapApplicator(cap : IMod<LineCap>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.LineCap = cap

    type LineJoinApplicator(lineJoin : IMod<LineJoin>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.LineJoin = lineJoin

    type WindingApplicator(winding : IMod<Winding>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Winding = winding

    type EmptyLeaf() =
        interface INvg

    type TextLeaf(content : IMod<string>) =
        interface INvg
        member x.Content = content

    type FontApplicator(font : IMod<Font>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Font = font

    type FontSizeApplicator(fontSize : IMod<float>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.FontSize = fontSize

    type LetterSpacingApplicator(letterSpacing : IMod<float>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.LetterSpacing = letterSpacing

    type LineHeightApplicator(lineHeight : IMod<float>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.LineHeight = lineHeight

    type BlurApplicator(blur : IMod<float>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.Blur = blur

    type TextAlignApplicator(align : IMod<TextAlign>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.TextAlign = align


    type Group (l : alist<INvg>) =
        interface INvg
        member x.List = l

    type OnOffNode(isActive : IMod<bool>, child : IMod<INvg>) =
        inherit AbstractApplicator(child)
        member x.IsActive = isActive
        member x.Child = child

[<AutoOpen>]
module ``Semantic Extensions`` =
    open Aardvark.Base.Ag

    type INvg with
        member x.Context : Context.NanoVgContext = x?Context
        member x.StrokeWidth : IMod<float> = x?StrokeWidth
        member x.StrokeColor : IMod<C4f> = x?StrokeColor
        member x.LineCap : IMod<LineCap> = x?LineCap
        member x.LineJoin : IMod<LineJoin> = x?LineJoin
        member x.Winding : IMod<Winding> = x?Winding
        member x.Font : IMod<Font> = x?Font
        member x.FontSize : IMod<float> = x?FontSize
        member x.LetterSpacing : IMod<float> = x?LetterSpacing
        member x.LineHeight : IMod<float> = x?LineHeight
        member x.FontBlur : IMod<float> = x?FontBlur
        member x.TextAlign : IMod<TextAlign> = x?TextAlign
        member x.Trafo : IMod<M33d> = x?Trafo
        member x.Scissor : IMod<Box2d> = x?Scissor
        member x.FillColor : IMod<C4f> = x?FillColor

        member x.RenderObjects() : alist<NvgRenderObject> = x?RenderObjects()
        member x.LocalBoundingBox() : IMod<Box2d> = x?LocalBoundingBox()
        member x.GlobalBoundingBox() : IMod<Box2d> = x?GlobalBoundingBox()
        member x.IsActive : IMod<bool> = x?IsActive

    module Nvg =
        let empty =
            Nvg.EmptyLeaf() :> INvg

        let text (content : IMod<string>) = 
            Nvg.TextLeaf(content) :> INvg

        let fill (content : IMod<Primitive>) =
            Nvg.PrimitiveLeaf(Mod.constant PrimitiveMode.FillPrimitive, content) :> INvg

        let stroke (content : IMod<Primitive>) =
            Nvg.PrimitiveLeaf(Mod.constant PrimitiveMode.StrokePrimitive, content) :> INvg

        let trafo (t : IMod<M33d>) (c : INvg) =
            Nvg.TrafoApplicator(t, Mod.constant c) :> INvg

        let scissor (t : IMod<Box2d>) (c : INvg) =
            Nvg.ScissorApplicator(t, Mod.constant c) :> INvg

        let fillColor (t : IMod<C4f>) (c : INvg) =
            Nvg.FillColorApplicator(t, Mod.constant c) :> INvg

        let strokeColor (t : IMod<C4f>) (c : INvg) =
            Nvg.StrokeColorApplicator(t, Mod.constant c) :> INvg

        let strokeWidth (t : IMod<float>) (c : INvg) =
            Nvg.StrokeWidthApplicator(t, Mod.constant c) :> INvg

        let lineCap (t : IMod<LineCap>) (c : INvg) =
            Nvg.LineCapApplicator(t, Mod.constant c) :> INvg

        let lineJoin (t : IMod<LineJoin>) (c : INvg) =
            Nvg.LineJoinApplicator(t, Mod.constant c) :> INvg

        let font (t : IMod<Font>) (c : INvg) =
            Nvg.FontApplicator(t, Mod.constant c) :> INvg

        let systemFont (name : string) (style : FontStyle) (c : INvg) =
            Nvg.FontApplicator(Mod.constant (SystemFont(name, style)), Mod.constant c) :> INvg


        let fontSize (t : IMod<float>) (c : INvg) =
            Nvg.FontSizeApplicator(t, Mod.constant c) :> INvg

        let letterSpacing (t : IMod<float>) (c : INvg) =
            Nvg.LetterSpacingApplicator(t, Mod.constant c) :> INvg

        let lineHeight (t : IMod<float>) (c : INvg) =
            Nvg.LineHeightApplicator(t, Mod.constant c) :> INvg

        let blur (t : IMod<float>) (c : INvg) =
            Nvg.BlurApplicator(t, Mod.constant c) :> INvg

        let align (t : IMod<TextAlign>) (c : INvg) =
            Nvg.TextAlignApplicator(t, Mod.constant c) :> INvg


        let group (l : alist<INvg>) =
            Nvg.Group(l) :> INvg

        let onOff (m : IMod<bool>) (c : INvg) =
            Nvg.OnOffNode(m, Mod.constant c) :> INvg

        let ofList (l : list<INvg>) =
            Nvg.Group(AList.ofList l) :> INvg

[<AbstractClass; Sealed; Extension>]
type RuntimeExtensions private() =
    [<Extension>]
    static member CompileRender(this : IRuntime, sg : INvg) =
        let ctx = this.GetNanoVgContext()
        let app = Nvg.ContextApplicator(ctx, Mod.constant sg)
        let renderJobs = app.RenderObjects()
        this.CompileRender(renderJobs)

module Semantics =
    open Aardvark.Base.Ag
    open Aardvark.Base.AgHelpers
    open Microsoft.FSharp.NativeInterop

    

    module NanoVgExt =
    
        let nvgText(ctx : NvgContext, x : float32, y : float32, str : string, align : NvgAlign) =


            // copy the UTF8 string to a stack-pointer
            let strbytes = System.Text.UTF8Encoding.UTF8.GetBytes str
            let input = NativePtr.stackalloc (strbytes.Length + 1)
            for i in 0..strbytes.Length-1 do NativePtr.set input i strbytes.[i]
            NativePtr.set input strbytes.Length 0uy

            // get the lines using nanovg
            let lineBreaks = str.ToCharArray() |> Array.filter (fun c -> c = '\r' || c = '\n') |> Array.length

            if lineBreaks = 0 then
                NanoVg.nvgSave(ctx)
                NanoVg.nvgTextAlign(ctx, align)
                NanoVg.nvgText(ctx, x, y, str, 0n) |> ignore
                NanoVg.nvgRestore(ctx)
            else

                let maxLineCount = 1 + lineBreaks
                let lines = NativePtr.stackalloc maxLineCount
                let total = NanoVg.nvgTextBreakLinesPtr(ctx, input, 0n, 10000000.0f, lines, maxLineCount)

                let mutable asc = 0.0f
                let mutable desc = 0.0f
                let mutable lineh = 0.0f
                NanoVg.nvgTextMetrics(ctx, &asc, &desc, &lineh)

                let horizontalAlign = align &&& ~~~NvgAlign.Top &&& ~~~NvgAlign.Middle &&& ~~~NvgAlign.BaseLine &&& ~~~NvgAlign.Bottom

                let offset,align =
                    if (align &&& NvgAlign.Bottom) <> unbox 0 then 
                        let initial = -(float32 (total - 1) * lineh)
                        let align = horizontalAlign ||| NvgAlign.Bottom
                        initial, align

                    elif (align &&& NvgAlign.Middle) <> unbox 0 then 
                        let initial = -(float32 (total - 1) * lineh) * 0.5f
                        let align = horizontalAlign ||| NvgAlign.Middle
                        initial, align

                    elif (align &&& NvgAlign.BaseLine) <> unbox 0 then 
                        let initial = -(float32 (total - 1) * lineh) - desc
                        let align = horizontalAlign ||| NvgAlign.Bottom
                        initial, align

                    else
                        (0.0f, horizontalAlign ||| NvgAlign.Top)


        

                NanoVg.nvgTextAlign(ctx, align)
        
                let isBlock = (align &&& NvgAlign.Block <> unbox 0)
                let mutable offset = offset

                if isBlock then
                    // determine the total width
                    let mutable totalWidth = 0.0f
                    for i in 0..total-1 do
                        let r = NativePtr.get lines i
                        totalWidth <- max r.width totalWidth

                    for i in 0..total-1 do
                        let r = NativePtr.get lines i

                        let spaces = r.endPtr - r.startPtr - 1n |> float32

                        let spacing = (totalWidth - r.width) / spaces
                        NanoVg.nvgTextLetterSpacing(ctx, spacing)
                        NanoVg.nvgTextPtr(ctx, x, y + offset, NativePtr.ofNativeInt r.startPtr, NativePtr.ofNativeInt r.endPtr) |> ignore
                        offset <- offset + lineh
                else
                    for i in 0..total-1 do
                        let r = NativePtr.get lines i
                        NanoVg.nvgTextPtr(ctx, x, y + offset, NativePtr.ofNativeInt r.startPtr, NativePtr.ofNativeInt r.endPtr) |> ignore
                        offset <- offset + lineh

                NanoVg.nvgRestore(ctx)

        let nvgTextBounds(ctx : NvgContext, x : float32, y : float32, str : string, align : NvgAlign) =


            // copy the UTF8 string to a stack-pointer
            let strbytes = System.Text.UTF8Encoding.UTF8.GetBytes str
            let input = NativePtr.stackalloc (strbytes.Length + 1)
            for i in 0..strbytes.Length-1 do NativePtr.set input i strbytes.[i]
            NativePtr.set input strbytes.Length 0uy

            // get the lines using nanovg
            let lineBreaks = str.ToCharArray() |> Array.filter (fun c -> c = '\r' || c = '\n') |> Array.length

            if lineBreaks = 0 then
                NanoVg.nvgSave(ctx)
                NanoVg.nvgTextAlign(ctx, align)
                let mutable b = Box2f.Invalid
                NanoVg.nvgTextBounds(ctx, x, y, str, 0n, &b) |> ignore
                NanoVg.nvgRestore(ctx)
                b
            else

                let maxLineCount = 1 + lineBreaks
                let lines = NativePtr.stackalloc maxLineCount
                let total = NanoVg.nvgTextBreakLinesPtr(ctx, input, 0n, 10000000.0f, lines, maxLineCount)

                let mutable asc = 0.0f
                let mutable desc = 0.0f
                let mutable lineh = 0.0f
                NanoVg.nvgTextMetrics(ctx, &asc, &desc, &lineh)

                let horizontalAlign = align &&& ~~~NvgAlign.Top &&& ~~~NvgAlign.Middle &&& ~~~NvgAlign.BaseLine &&& ~~~NvgAlign.Bottom

                let offset,align =
                    if (align &&& NvgAlign.Bottom) <> unbox 0 then 
                        let initial = -(float32 (total - 1) * lineh)
                        let align = horizontalAlign ||| NvgAlign.Bottom
                        initial, align

                    elif (align &&& NvgAlign.Middle) <> unbox 0 then 
                        let initial = -(float32 (total - 1) * lineh) * 0.5f
                        let align = horizontalAlign ||| NvgAlign.Middle
                        initial, align

                    elif (align &&& NvgAlign.BaseLine) <> unbox 0 then 
                        let initial = -(float32 (total - 1) * lineh) - desc
                        let align = horizontalAlign ||| NvgAlign.Bottom
                        initial, align

                    else
                        (0.0f, horizontalAlign ||| NvgAlign.Top)


        

                NanoVg.nvgTextAlign(ctx, align)
        
                let isBlock = (align &&& NvgAlign.Block <> unbox 0)
                let mutable offset = offset
                let mutable bounds = Box2f.Invalid
                if isBlock then
                    // determine the total width
                    let mutable totalWidth = 0.0f
                    for i in 0..total-1 do
                        let r = NativePtr.get lines i
                        totalWidth <- max r.width totalWidth

                    for i in 0..total-1 do
                        let r = NativePtr.get lines i

                        let spaces = r.endPtr - r.startPtr - 1n |> float32

                        let spacing = (totalWidth - r.width) / spaces
                        NanoVg.nvgTextLetterSpacing(ctx, spacing)
                        let mutable b = Box2f.Invalid
                        NanoVg.nvgTextBoundsPtr(ctx, x, y + offset, NativePtr.ofNativeInt r.startPtr, NativePtr.ofNativeInt r.endPtr, &b) |> ignore
                   
                        bounds <- bounds.Union(b)
                        offset <- offset + lineh
                else
                    for i in 0..total-1 do
                        let r = NativePtr.get lines i
                        let mutable b = Box2f.Invalid
                        NanoVg.nvgTextBoundsPtr(ctx, x, y + offset, NativePtr.ofNativeInt r.startPtr, NativePtr.ofNativeInt r.endPtr, &b) |> ignore
                        bounds <- bounds.Union(b)
                        offset <- offset + lineh

                NanoVg.nvgRestore(ctx)
                bounds


    [<Semantic>]
    type InheritedSem() =
        
        member x.Context(r : Root<INvg>) =
            r.Child?Context <- Unchecked.defaultof<Context.NanoVgContext>

        member x.Context(app : Nvg.ContextApplicator) =
            app.Child?Context <- app.Context

        member x.StrokeWidth(r : Root<INvg>) =
            r.Child?StrokeWidth <- Mod.constant 1.0

        member x.StrokeWidth(app : Nvg.StrokeWidthApplicator) =
            app.Child?StrokeWidth <- app.Width


        member x.StrokeColor(r : Root<INvg>) =
            r.Child?StrokeColor <- Mod.constant C4f.White

        member x.StrokeColor(app : Nvg.StrokeColorApplicator) =
            app.Child?StrokeColor <- app.Color


        member x.LineCap(r : Root<INvg>) =
            r.Child?LineCap <- Mod.constant LineCap.ButtCap

        member x.LineCap(app : Nvg.LineCapApplicator) =
            app.Child?LineCap <- app.LineCap


        member x.LineJoin(r : Root<INvg>) =
            r.Child?LineJoin <- Mod.constant LineJoin.RoundJoin

        member x.LineJoin(app : Nvg.LineJoinApplicator) =
            app.Child?LineJoin <- app.LineJoin


        member x.Winding(r : Root<INvg>) =
            r.Child?Winding <- Mod.constant Winding.CCW

        member x.Winding(app : Nvg.WindingApplicator) =
            app.Child?Winding <- app.Winding


        member x.Font(r : Root<INvg>) =
            r.Child?Font <- Mod.constant (SystemFont("Consolas", FontStyle.Regular))

        member x.Font(app : Nvg.FontApplicator) =
            app.Child?Font <- app.Font


        member x.FontSize(r : Root<INvg>) =
            r.Child?FontSize <- Mod.constant 12.0

        member x.FontSize(app : Nvg.FontSizeApplicator) =
            app.Child?FontSize <- app.FontSize


        member x.LetterSpacing(r : Root<INvg>) =
            r.Child?LetterSpacing <- Mod.constant 0.0

        member x.LetterSpacing(app : Nvg.LetterSpacingApplicator) =
            app.Child?LetterSpacing <- app.LetterSpacing


        member x.LineHeight(r : Root<INvg>) =
            r.Child?LineHeight <- Mod.constant 1.0

        member x.LineHeight(app : Nvg.LineHeightApplicator) =
            app.Child?LineHeight <- app.LineHeight


        member x.FontBlur(r : Root<INvg>) =
            r.Child?FontBlur <- Mod.constant 0.0

        member x.FontBlur(app : Nvg.BlurApplicator) =
            app.Child?FontBlur <- app.Blur


        member x.TextAlign(r : Root<INvg>) =
            r.Child?TextAlign <- Mod.constant (TextAlign.Left ||| TextAlign.Top)

        member x.TextAlign(app : Nvg.TextAlignApplicator) =
            app.Child?TextAlign <- app.TextAlign


        member x.Trafo(r : Root<INvg>) =
            r.Child?Trafo <- Mod.constant M33d.Identity

        member x.Trafo(app : Nvg.TrafoApplicator) =
            let parent : IMod<M33d> = app?Trafo
            app.Child?Trafo <- Mod.map2 (*) parent app.Trafo


        member x.Scissor(r : Root<INvg>) =
            r.Child?Scissor <- Mod.constant Box2d.Infinite

        member x.Scissor(app : Nvg.ScissorApplicator) =
            app.Child?Scissor <- app.Bounds


        member x.FillColor(r : Root<INvg>) =
            r.Child?FillColor <- Mod.constant C4f.White

        member x.FillColor(app : Nvg.FillColorApplicator) =
            app.Child?FillColor <- app.Color

        member x.IsActive(app : INvgApplicator) =
            app.Child?IsActive <- app?IsActive

        member x.IsActive(t : Nvg.OnOffNode) =
            t.Child?IsActive <- t.IsActive


    [<Semantic>]
    type RenderObjectSem() =

        member x.RenderObjects(app : INvgApplicator) =
            alist {
                let! c = app.Child
                yield! c.RenderObjects()
            }


        member x.RenderObjects(t : Nvg.TextLeaf) =
            AList.single {
                transform = t.Trafo
                scissor = t.Scissor
                fillColor = t.FillColor
                command = 
                    Right {
                        font = t.Font
                        size = t.FontSize
                        letterSpacing = t.LetterSpacing
                        lineHeight = t.LineHeight
                        blur = t.FontBlur
                        align = t.TextAlign
                        content = t.Content
                    }
                isActive = t.IsActive
            }

        member x.RenderObjects(t : Nvg.PrimitiveLeaf) =
            AList.single {
                transform = t.Trafo
                scissor = t.Scissor
                fillColor = t.FillColor
                command = 
                    Left {
                        strokeWidth = t.StrokeWidth
                        strokeColor = t.StrokeColor
                        lineCap = t.LineCap
                        lineJoin = t.LineJoin
                        winding = t.Winding
                        primitive = t.Primitive
                        mode = t.Mode
                    }
                isActive = t.IsActive
            }

        member x.RenderObjects(t : Nvg.Group) =
            alist {
                for c in t.List do
                    yield! c.RenderObjects()
            }

        member x.RenderObjects(e : Nvg.EmptyLeaf) : alist<NvgRenderObject> =
            AList.empty

    [<Semantic>]
    type BBSem() =
        
        let textBounds (ctx : Context.NanoVgContext) (text : string) (align : TextAlign) (font : Font) (size : float) (spacing : float) (lineh : float) : Box2d =
            ctx.Use (fun ctx ->
                NanoVg.nvgSave(ctx.Handle)

                NanoVg.nvgTextAlign(ctx.Handle, unbox align)
                NanoVg.nvgFontFaceId(ctx.Handle, ctx.GetFontId font)
                NanoVg.nvgFontSize(ctx.Handle, float32 size)
                NanoVg.nvgTextLetterSpacing(ctx.Handle, float32 spacing)
                NanoVg.nvgTextLineHeight(ctx.Handle, float32 lineh)

                let bounds = NanoVgExt.nvgTextBounds(ctx.Handle, 0.0f, 0.0f, text, unbox align)


                NanoVg.nvgRestore(ctx.Handle)

                Box2d(bounds)
            )

        member x.LocalBoundingBox(app : INvgApplicator) =
            adaptive {
                let! c = app.Child
                return! c.LocalBoundingBox()
            }

        member x.LocalBoundingBox(trafo : Nvg.TrafoApplicator) =
            adaptive {
                let! c = trafo.Child
                return! Mod.map2 (fun (t : M33d) (b : Box2d) -> Box2d(t.TransformPos(b.Min), t.TransformPos(b.Max))) trafo.Trafo (c.LocalBoundingBox())
            }

        member x.LocalBoundingBox(app : Nvg.TextLeaf) =
            adaptive {
                let! align = app.TextAlign
                let! (letterSpacing, lineHeight) = app.LetterSpacing, app.LineHeight
                let! (font,size) = app.Font, app.FontSize
                let! content = app.Content

                return textBounds app.Context content align font size letterSpacing lineHeight
            }

        member x.LocalBoundingBox(app : Nvg.PrimitiveLeaf) =
            Mod.constant (Box2d())

        member x.LocalBoundingBox(l : Nvg.EmptyLeaf) : IMod<Box2d> =
            Mod.constant Box2d.Invalid
