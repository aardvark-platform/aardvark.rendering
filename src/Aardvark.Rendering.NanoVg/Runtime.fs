namespace Aardvark.Rendering.NanoVg

open System
open System.Runtime.CompilerServices

open NanoVgSharp
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Monads.State
open Aardvark.Rendering.GL

module private Interpreter =

    let private toNvgCap (c : LineCap) =
        match c with
            | ButtCap -> NvgLineCap.Butt
            | RoundCap -> NvgLineCap.Round
            | SquareCap -> NvgLineCap.Square

    let private drawPath (ctx : NvgContext) (p : Path) =
        for i in p do
            match i with
                | MoveTo p -> NanoVg.nvgMoveTo(ctx, float32 p.X, float32 p.Y)
                | LineTo p -> NanoVg.nvgLineTo(ctx, float32 p.X, float32 p.Y)
                | BezierTo(c0, c1, p) -> NanoVg.nvgBezierTo(ctx, float32 c0.X, float32 c0.Y, float32 c1.X, float32 c1.Y, float32 p.X, float32 p.Y)
                | QuadraticTo(c,p) -> NanoVg.nvgQuadTo(ctx, float32 c.X, float32 c.Y, float32 p.X, float32 p.Y) 
                | ArcTo(p,t,r) -> NanoVg.nvgArcTo(ctx, float32 p.X, float32 p.Y, float32 t.X, float32 t.Y, float32 r)
                | ClosePath -> NanoVg.nvgClosePath(ctx)

    let private drawPrimitive (ctx : NvgContext) (p : Primitive) =
        NanoVg.nvgBeginPath(ctx)

        match p with
            | Path p -> drawPath ctx p
            | Arc(c,r,ar,d) -> NanoVg.nvgArc(ctx, float32 c.X, float32 c.Y, float32 r, float32 ar.Min, float32 ar.Max, d)
            | Rectangle box -> NanoVg.nvgRect(ctx, float32 box.Min.X, float32 box.Min.Y, float32 box.SizeX, float32 box.SizeY)
            | RoundedRectangle(box,r) -> NanoVg.nvgRoundedRect(ctx, float32 box.Min.X, float32 box.Min.Y, float32 box.SizeX, float32 box.SizeY, float32 r)
            | Ellipse(c,r) -> NanoVg.nvgEllipse(ctx, float32 c.X, float32 c.Y, float32 r.X, float32 r.Y)
            | Circle(c,r) -> NanoVg.nvgCircle(ctx, float32 c.X, float32 c.Y, float32 r)

    type private NvgState =
        {
            ctx : NvgContext
            transform : M33d
            scissor : Box2d
            fillColor : C4f
            pathStrokeWidth : float
            pathStrokeColor : C4f
            pathLineCap : LineCap
            pathLineJoin : LineJoin
            pathWinding : Winding
            font : Font
            fontSize : float
            fontLetterSpacing : float
            fontLineHeight : float
            fontBlur : float
            fontAlign : TextAlign
        }

    let private emptyState =
        {
            ctx = 0n
            transform = M33d.Identity
            scissor = Box2d.Infinite
            fillColor = C4f(Single.MaxValue, Single.MaxValue, Single.MaxValue, Single.MaxValue)
            pathStrokeWidth = -1.0
            pathStrokeColor = C4f(Single.MaxValue, Single.MaxValue, Single.MaxValue, Single.MaxValue)
            pathLineCap = ButtCap
            pathLineJoin = RoundJoin
            pathWinding = Winding.CW
            font = SystemFont("______", FontStyle.Regular)
            fontSize = -1.0
            fontLetterSpacing = -1000.0
            fontLineHeight = -1000.0
            fontBlur = 0.0
            fontAlign = TextAlign.BaseLine
        }

    module private Nvg =
        let setTransform (t : IMod<M33d>) = 
            { runState = fun s -> 
                if t <> null && s.transform <> !!t then 
                    let t = !!t
                    NanoVg.nvgResetTransform(s.ctx)
                    NanoVg.nvgTransform(s.ctx, float32 t.M00, float32 t.M10, float32 t.M01, float32 t.M11, float32 t.M02, float32 t.M12)
                    (), { s with transform = t }
                else
                    (), s
            }

        let setScissor (v : IMod<Box2d>) = 
            { runState = fun s -> 
                if v <> null && s.scissor <> !!v then 
                    let v = !!v
                    NanoVg.nvgResetScissor(s.ctx)
                    NanoVg.nvgScissor(s.ctx, float32 v.Min.X, float32 v.Min.Y, float32 v.SizeX, float32 v.SizeY)
                    (), { s with scissor = v }
                else
                    (), s
            }

        let setFillColor (v : IMod<C4f>) =
            { runState = fun s -> 
                if v <> null && s.fillColor <> !!v then 
                    let v = !!v
                    NanoVg.nvgFillColor(s.ctx, v)
                    (), { s with fillColor = v }
                else
                    (), s
            }  

        let setStrokeWidth (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.pathStrokeWidth <> !!v then 
                    let v = !!v
                    NanoVg.nvgStrokeWidth(s.ctx, float32 v)
                    (), { s with pathStrokeWidth = v }
                else
                    (), s
            }  

        let setStrokeColor (v : IMod<C4f>) =
            { runState = fun s -> 
                if v <> null && s.pathStrokeColor <> !!v then 
                    let v = !!v
                    NanoVg.nvgStrokeColor(s.ctx, v)
                    (), { s with pathStrokeColor = v }
                else
                    (), s
            }  

        let setLineCap (v : IMod<LineCap>) =
            { runState = fun s -> 
                if v <> null && s.pathLineCap <> !!v then 
                    let v = !!v
                    NanoVg.nvgLineCap(s.ctx, toNvgCap v)
                    (), { s with pathLineCap = v }
                else
                    (), s
            }  

        let setLineJoin (v : IMod<LineJoin>) =
            { runState = fun s -> 
                if v <> null && s.pathLineJoin <> !!v then 
                    let v = !!v
                    match v with
                        | MiterJoin cap ->
                            NanoVg.nvgMiterLimit(s.ctx,float32 cap)
                            NanoVg.nvgLineJoin(s.ctx, NvgLineCap.Miter)
                        | BevelJoin -> NanoVg.nvgLineJoin(s.ctx, NvgLineCap.Bevel)
                        | RoundJoin -> NanoVg.nvgLineJoin(s.ctx, NvgLineCap.Round)
                    (), { s with pathLineJoin = v }
                else
                    (), s
            }  

        let setWinding (v : IMod<Winding>) =
            { runState = fun s -> 
                if v <> null && s.pathWinding <> !!v then 
                    let v = !!v
                    let nvgWinding =
                        match v with
                            | Winding.CCW -> NvgWinding.CounterClockwise
                            | _ -> NvgWinding.Clockwise

                    NanoVg.nvgPathWinding(s.ctx, int nvgWinding)
                    (), { s with pathWinding = v }
                else
                    (), s
            }  

        let drawPrimitive (p : Primitive) =
            { runState = fun s -> 
                drawPrimitive s.ctx p
                ((), s)
            }

        let stroke =
            { runState = fun s -> 
                NanoVg.nvgStroke(s.ctx)
                ((), s)
            }

        let fill =
            { runState = fun s -> 
                NanoVg.nvgFill(s.ctx)
                ((), s)
            }

        let setFont (v : IMod<Font>) =
            { runState = fun s -> 
                if v <> null && s.font <> !!v then 
                    let v = !!v
                    let name = Context.getFontName v
                    NanoVg.nvgFontFace(s.ctx, name)
                    (), { s with font = v }
                else
                    (), s
            }  

        let setFontSize (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontSize <> !!v then 
                    let v = !!v
                    NanoVg.nvgFontSize(s.ctx, float32 v)
                    (), { s with fontSize = v }
                else
                    (), s
            }  

        let setLetterSpacing (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontLetterSpacing <> !!v then 
                    let v = !!v
                    NanoVg.nvgTextLetterSpacing(s.ctx, float32 v)
                    (), { s with fontLetterSpacing = v }
                else
                    (), s
            }  

        let setLineHeight (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontLineHeight <> !!v then 
                    let v = !!v
                    NanoVg.nvgTextLineHeight(s.ctx, float32 v)
                    (), { s with fontLineHeight = v }
                else
                    (), s
            }  

        let setBlur (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontBlur <> !!v then 
                    let v = !!v
                    NanoVg.nvgFontBlur(s.ctx, float32 v)
                    (), { s with fontBlur = v }
                else
                    (), s
            }  

        let drawText (align : TextAlign) (content : string) =
            { runState = fun s -> 
//                NanoVg.nvgTextAlign(s.ctx, unbox align)
//                NanoVg.nvgTextBox(s.ctx, 0.0f, 0.0f, 10000000.0f, content, 0n)
                NanoVgExt.nvgText(s.ctx, 0.0f, 0.0f, content, unbox align)
                ((),s)
            }  

    let private runRenderJob (rj : NvgRenderJob) =
        state {
            do! Nvg.setTransform rj.transform
            do! Nvg.setScissor rj.scissor
            do! Nvg.setFillColor rj.fillColor

            match rj.command with
                | Left p ->
                    do! Nvg.setStrokeWidth p.strokeWidth
                    do! Nvg.setStrokeColor p.strokeColor
                    do! Nvg.setLineCap p.lineCap
                    do! Nvg.setLineJoin p.lineJoin
                    do! Nvg.setWinding p.winding

                    do! Nvg.drawPrimitive !!p.primitive

                    match !!p.mode with
                        | FillPrimitive -> do! Nvg.fill
                        | StrokePrimitive -> do! Nvg.stroke

                | Right t ->
                    do! Nvg.setFont t.font
                    do! Nvg.setFontSize t.size
                    do! Nvg.setLetterSpacing t.letterSpacing
                    do! Nvg.setLineHeight t.lineHeight
                    do! Nvg.setBlur t.blur

                    do! Nvg.drawText !!t.align !!t.content 
        }

    let run (ctx : NvgContext) (s : seq<NvgRenderJob>) =
        let mutable state = { emptyState with ctx = ctx }

        for rj in s do
            let ((), n) = runRenderJob(rj).runState state
            state <- n
        ()

type RenderTask(glRuntime : Runtime, l : alist<NvgRenderJob>) as this =
    inherit AdaptiveObject()
    let glContext = glRuntime.Context
    let r = l.GetReader()
    let inputs = ReferenceCountingSet<IAdaptiveObject>()
    do r.AddOutput this

    let addInput (v : IAdaptiveObject) =
        if v <> null && inputs.Add v then
            v.AddOutput this

    let removeInput (v : IAdaptiveObject) =
        if v <> null && inputs.Remove v then
            v.RemoveOutput this

    let add (rj : NvgRenderJob) =
        addInput rj.transform
        addInput rj.scissor
        addInput rj.fillColor

        match rj.command with
            | Left p ->
                addInput p.lineCap
                addInput p.lineJoin
                addInput p.mode
                addInput p.primitive
                addInput p.strokeColor
                addInput p.strokeWidth
                addInput p.winding
            | Right t ->
                addInput t.align
                addInput t.blur
                addInput t.content
                addInput t.font
                addInput t.letterSpacing
                addInput t.lineHeight
                addInput t.size

    let remove (rj : NvgRenderJob) =
        removeInput rj.transform
        removeInput rj.scissor
        removeInput rj.fillColor

        match rj.command with
            | Left p ->
                removeInput p.lineCap
                removeInput p.lineJoin
                removeInput p.mode
                removeInput p.primitive
                removeInput p.strokeColor
                removeInput p.strokeWidth
                removeInput p.winding
            | Right t ->
                removeInput t.align
                removeInput t.blur
                removeInput t.content
                removeInput t.font
                removeInput t.letterSpacing
                removeInput t.lineHeight
                removeInput t.size

    member x.Dispose() =
        r.RemoveOutput x
        r.Dispose()
        let all = base.Inputs |> Seq.toArray
        for i in all do
            i.RemoveOutput x
        inputs.Clear()

    member x.Run(fbo : IFramebuffer) =
        base.EvaluateAlways (fun () ->
            let ctx = using glContext.ResourceLock (fun _ -> Context.current())

            for d in r.GetDelta() do
                match d with
                    | Add(_,rj) -> add rj
                    | Rem(_,rj) -> remove rj

            NanoVg.nvgBeginFrame(ctx, fbo.Size.X, fbo.Size.Y, 1.0f)
            r.Content |> Seq.map snd |> Interpreter.run ctx
            NanoVg.nvgEndFrame(ctx)
        )

    interface IRenderTask with
        member x.Runtime = glRuntime :> IRuntime |> Some
        member x.Run(fbo) = 
            x.Run(fbo)
            RenderingResult(fbo, FrameStatistics.Zero)

        member x.Dispose() =
            x.Dispose()

[<Extension; AbstractClass; Sealed>]
type RuntimeExtensions private() =
    [<Extension>]
    static member CompileRender(this : IRuntime, list : alist<NvgRenderJob>) =
        match this with
            | :? Runtime as this ->
                new RenderTask(this, list) :> IRenderTask
            | _ -> failwithf "unsupported NanoVg runtime: %A" this

    [<Extension>]
    static member CompileRender(this : IRuntime, list : list<NvgRenderJob>) =
        match this with
            | :? Runtime as this ->
                new RenderTask(this, AList.ofList list) :> IRenderTask
            | _ -> failwithf "unsupported NanoVg runtime: %A" this