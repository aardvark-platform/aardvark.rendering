namespace Aardvark.Rendering.NanoVg

open System
open System.Runtime.CompilerServices

open NanoVgSharp
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Incremental.Operators
open Aardvark.Base.Monads.State
open Aardvark.Rendering.GL

module internal Interpreter =

    let internal toNvgCap (c : LineCap) =
        match c with
            | ButtCap -> NvgLineCap.Butt
            | RoundCap -> NvgLineCap.Round
            | SquareCap -> NvgLineCap.Square

    let internal drawPath (ctx : NvgContext) (p : Path) =
        for i in p do
            match i with
                | MoveTo p -> NanoVg.nvgMoveTo(ctx, float32 p.X, float32 p.Y)
                | LineTo p -> NanoVg.nvgLineTo(ctx, float32 p.X, float32 p.Y)
                | BezierTo(c0, c1, p) -> NanoVg.nvgBezierTo(ctx, float32 c0.X, float32 c0.Y, float32 c1.X, float32 c1.Y, float32 p.X, float32 p.Y)
                | QuadraticTo(c,p) -> NanoVg.nvgQuadTo(ctx, float32 c.X, float32 c.Y, float32 p.X, float32 p.Y) 
                | ArcTo(p,t,r) -> NanoVg.nvgArcTo(ctx, float32 p.X, float32 p.Y, float32 t.X, float32 t.Y, float32 r)
                | ClosePath -> NanoVg.nvgClosePath(ctx)

    let internal drawPrimitive (ctx : NvgContext) (p : Primitive) =
        NanoVg.nvgBeginPath(ctx)

        match p with
            | Path p -> drawPath ctx p
            | Arc(c,r,ar,d) -> NanoVg.nvgArc(ctx, float32 c.X, float32 c.Y, float32 r, float32 ar.Min, float32 ar.Max, d)
            | Rectangle box -> NanoVg.nvgRect(ctx, float32 box.Min.X, float32 box.Min.Y, float32 box.SizeX, float32 box.SizeY)
            | RoundedRectangle(box,r) -> NanoVg.nvgRoundedRect(ctx, float32 box.Min.X, float32 box.Min.Y, float32 box.SizeX, float32 box.SizeY, float32 r)
            | Ellipse(c,r) -> NanoVg.nvgEllipse(ctx, float32 c.X, float32 c.Y, float32 r.X, float32 r.Y)
            | Circle(c,r) -> NanoVg.nvgCircle(ctx, float32 c.X, float32 c.Y, float32 r)

    type internal NvgState =
        {
            caller : IAdaptiveObject
            ctx : Context.NanoVgContextHandle
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

    let internal emptyState =
        {
            caller = null
            ctx = null
            transform = M33d(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
            scissor = Box2d.Invalid
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
                if t <> null && s.transform <> t.GetValue(s.caller) then 
                    let t = t.GetValue(s.caller)
                    NanoVg.nvgResetTransform(s.ctx.Handle)
                    if not (t.IsIdentity(Constant.PositiveTinyValue)) then
                        NanoVg.nvgTransform(s.ctx.Handle, float32 t.M00, float32 t.M10, float32 t.M01, float32 t.M11, float32 t.M02, float32 t.M12)
                    (), { s with transform = t }
                else
                    (), s
            }
        let resetTransform  = 
            { runState = fun s ->
                if s.transform <> M33d.Identity then 
                    NanoVg.nvgResetTransform(s.ctx.Handle)
                    (), { s with transform = M33d.Identity }
                else
                    (), s
            }
        let setScissor (v : IMod<Box2d>) = 
            { runState = fun s -> 
                if v <> null && s.scissor <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgResetScissor(s.ctx.Handle)
                    if not v.IsInfinite then
                        NanoVg.nvgScissor(s.ctx.Handle, float32 v.Min.X, float32 v.Min.Y, float32 v.SizeX, float32 v.SizeY)
                    (), { s with scissor = v }
                else
                    (), s
            }

        let setFillColor (v : IMod<C4f>) =
            { runState = fun s -> 
                if v <> null && s.fillColor <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgFillColor(s.ctx.Handle, v)
                    (), { s with fillColor = v }
                else
                    (), s
            }  

        let setStrokeWidth (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.pathStrokeWidth <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgStrokeWidth(s.ctx.Handle, float32 v)
                    (), { s with pathStrokeWidth = v }
                else
                    (), s
            }  

        let setStrokeColor (v : IMod<C4f>) =
            { runState = fun s -> 
                if v <> null && s.pathStrokeColor <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgStrokeColor(s.ctx.Handle, v)
                    (), { s with pathStrokeColor = v }
                else
                    (), s
            }  

        let setLineCap (v : IMod<LineCap>) =
            { runState = fun s -> 
                if v <> null && s.pathLineCap <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgLineCap(s.ctx.Handle, toNvgCap v)
                    (), { s with pathLineCap = v }
                else
                    (), s
            }  

        let setLineJoin (v : IMod<LineJoin>) =
            { runState = fun s -> 
                if v <> null && s.pathLineJoin <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    match v with
                        | MiterJoin cap ->
                            NanoVg.nvgMiterLimit(s.ctx.Handle,float32 cap)
                            NanoVg.nvgLineJoin(s.ctx.Handle, NvgLineCap.Miter)
                        | BevelJoin -> NanoVg.nvgLineJoin(s.ctx.Handle, NvgLineCap.Bevel)
                        | RoundJoin -> NanoVg.nvgLineJoin(s.ctx.Handle, NvgLineCap.Round)
                    (), { s with pathLineJoin = v }
                else
                    (), s
            }  

        let setWinding (v : IMod<Winding>) =
            { runState = fun s -> 
                if v <> null && s.pathWinding <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    let nvgWinding =
                        match v with
                            | Winding.CCW -> NvgWinding.CounterClockwise
                            | _ -> NvgWinding.Clockwise

                    NanoVg.nvgPathWinding(s.ctx.Handle, int nvgWinding)
                    (), { s with pathWinding = v }
                else
                    (), s
            }  

        let drawPrimitive (p : Primitive) =
            { runState = fun s -> 
                drawPrimitive s.ctx.Handle p
                ((), s)
            }

        let stroke =
            { runState = fun s -> 
                NanoVg.nvgStroke(s.ctx.Handle)
                ((), s)
            }

        let fill =
            { runState = fun s -> 
                NanoVg.nvgFill(s.ctx.Handle)
                ((), s)
            }

        let setFont (v : IMod<Font>) =
            { runState = fun s -> 
                if v <> null && s.font <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    let id = s.ctx.GetFontId v
                    NanoVg.nvgFontFaceId(s.ctx.Handle, id)
                    //NanoVg.nvgFontFace(s.ctx.Handle, name)
                    (), { s with font = v }
                else
                    (), s
            }  

        let setFontSize (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontSize <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgFontSize(s.ctx.Handle, float32 v)
                    (), { s with fontSize = v }
                else
                    (), s
            }  

        let setLetterSpacing (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontLetterSpacing <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgTextLetterSpacing(s.ctx.Handle, float32 v)
                    (), { s with fontLetterSpacing = v }
                else
                    (), s
            }  

        let setLineHeight (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontLineHeight <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgTextLineHeight(s.ctx.Handle, float32 v)
                    (), { s with fontLineHeight = v }
                else
                    (), s
            }  

        let setBlur (v : IMod<float>) =
            { runState = fun s -> 
                if v <> null && s.fontBlur <> v.GetValue(s.caller) then 
                    let v = v.GetValue(s.caller)
                    NanoVg.nvgFontBlur(s.ctx.Handle, float32 v)
                    (), { s with fontBlur = v }
                else
                    (), s
            }  

        let drawText (pos : V2d) (align : TextAlign) (content : string) =
            { runState = fun s -> 
                //NanoVg.nvgTextAlign(s.ctx.Handle, unbox align)
                //NanoVg.nvgTextBox(s.ctx.Handle, 0.0f, 0.0f, 10000000.0f, content, 0n)
                NanoVgExt.nvgText(s.ctx.Handle, float32 pos.X, float32 pos.Y, content, unbox align)
                
                ((),s)
            }  

    let private runRenderObject (rj : NvgRenderObject) =
        state {
            let! state = getState
            if rj.isActive.GetValue(state.caller)
            then
                do! Nvg.setScissor rj.scissor
                do! Nvg.setFillColor rj.fillColor

                match rj.command with
                    | Left p ->
                        do! Nvg.setTransform rj.transform
                        do! Nvg.setStrokeWidth p.strokeWidth
                        do! Nvg.setStrokeColor p.strokeColor
                        do! Nvg.setLineCap p.lineCap
                        do! Nvg.setLineJoin p.lineJoin
                        do! Nvg.setWinding p.winding

                        do! Nvg.drawPrimitive (p.primitive.GetValue(state.caller))

                        match p.mode.GetValue(state.caller) with
                            | FillPrimitive -> do! Nvg.fill
                            | StrokePrimitive -> do! Nvg.stroke

                    | Right t ->
                        do! Nvg.setFont t.font
                    
                        do! Nvg.setLetterSpacing t.letterSpacing
                        do! Nvg.setLineHeight t.lineHeight
                        do! Nvg.setBlur t.blur
                        let trafo = rj.transform.GetValue(state.caller)

                        // important for clear text alignment (aa causing problems when using trafo)
                        let orientation = trafo.UpperLeftM22()
                        if orientation.IsIdentity(Constant.PositiveTinyValue) then
                            do! Nvg.resetTransform
                            do! Nvg.setFontSize t.size
                            do! Nvg.drawText trafo.C2.XY (t.align.GetValue(state.caller)) (t.content.GetValue(state.caller))
                        elif orientation.M01.IsTiny() && orientation.M10.IsTiny() && orientation.M00.ApproximateEquals(orientation.M11, Constant.PositiveTinyValue) then
                            do! Nvg.resetTransform
                            do! Nvg.setFontSize ~~(orientation.M11 * t.size.GetValue(state.caller))
                            do! Nvg.drawText trafo.C2.XY (t.align.GetValue(state.caller)) (t.content.GetValue(state.caller)) 
                        else
                            do! Nvg.setTransform rj.transform
                            do! Nvg.setFontSize t.size
                            do! Nvg.drawText V2d.Zero (t.align.GetValue(state.caller)) (t.content.GetValue(state.caller))

        }

    let run (caller : IAdaptiveObject) (ctx : Context.NanoVgContextHandle) (s : seq<NvgRenderObject>) =
        let mutable state = { emptyState with ctx = ctx; caller = caller }

        for rj in s do
            let ((), n) = runRenderObject(rj).runState state
            state <- n
        ()


type RenderTask(runtime : Runtime, ctx : Context.NanoVgContext, l : alist<NvgRenderObject>)  =
    
    inherit AdaptiveObject()
    let r = l.GetReader()
    let inputs = ReferenceCountingSet<IAdaptiveObject>()

    let mutable frameId = 0UL

//    let addInput (v : IAdaptiveObject) =
//        lock this (fun () -> 
//            if v <> null && inputs.Add v then
//                v.Outputs.Add this |> ignore
//        )
//
//    let removeInput (v : IAdaptiveObject) =
//        lock this (fun () -> 
//            if v <> null && inputs.Remove v then
//                v.Outputs.Remove this |> ignore
//        )
//
//    let add (rj : NvgRenderObject) =
//        addInput rj.transform
//        addInput rj.scissor
//        addInput rj.fillColor
//        addInput rj.isActive
//
//        match rj.command with
//            | Left p ->
//                addInput p.lineCap
//                addInput p.lineJoin
//                addInput p.mode
//                addInput p.primitive
//                addInput p.strokeColor
//                addInput p.strokeWidth
//                addInput p.winding
//            | Right t ->
//                addInput t.align
//                addInput t.blur
//                addInput t.content
//                addInput t.font
//                addInput t.letterSpacing
//                addInput t.lineHeight
//                addInput t.size
//
//    let remove (rj : NvgRenderObject) =
//        removeInput rj.transform
//        removeInput rj.scissor
//        removeInput rj.fillColor
//        removeInput rj.isActive
//
//        match rj.command with
//            | Left p ->
//                removeInput p.lineCap
//                removeInput p.lineJoin
//                removeInput p.mode
//                removeInput p.primitive
//                removeInput p.strokeColor
//                removeInput p.strokeWidth
//                removeInput p.winding
//            | Right t ->
//                removeInput t.align
//                removeInput t.blur
//                removeInput t.content
//                removeInput t.font
//                removeInput t.letterSpacing
//                removeInput t.lineHeight
//                removeInput t.size

    member x.Dispose() =
        r.RemoveOutput x
        r.Dispose()
        let all = inputs |> Seq.toArray
        for i in all do
            i.RemoveOutput x
        inputs.Clear()


    member x.Run(caller : IAdaptiveObject, fbo : OutputDescription) =
        let fbo = fbo.framebuffer
        x.EvaluateAlways caller (fun () ->
            r.Update x
//            for d in r.GetDelta(x) do
//                match d with
//                    | Add(_,rj) -> add rj
//                    | Rem(_,rj) -> remove rj

            ctx.Use (fun current -> 

                let wasEnabled = OpenTK.Graphics.OpenGL4.GL.IsEnabled OpenTK.Graphics.OpenGL4.EnableCap.DebugOutput
                OpenTK.Graphics.OpenGL4.GL.Disable OpenTK.Graphics.OpenGL4.EnableCap.DebugOutput

                let old = Array.create 4 0
                let mutable oldFbo = 0
                OpenTK.Graphics.OpenGL4.GL.GetInteger(OpenTK.Graphics.OpenGL4.GetPName.Viewport, old)
                OpenTK.Graphics.OpenGL4.GL.GetInteger(OpenTK.Graphics.OpenGL4.GetPName.FramebufferBinding, &oldFbo)

                //let ctx = Context.current()

                if oldFbo <> (fbo.GetHandle null :?> int) then
                    OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, fbo.GetHandle null :?> int)
                OpenTK.Graphics.OpenGL4.GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y)

                OpenTK.Graphics.OpenGL.GL.PushAttrib(OpenTK.Graphics.OpenGL.AttribMask.AllAttribBits)
                OpenTK.Graphics.OpenGL.GL.BindSampler(0,0)
                OpenTK.Graphics.OpenGL.GL.PolygonMode(OpenTK.Graphics.OpenGL.MaterialFace.FrontAndBack, OpenTK.Graphics.OpenGL.PolygonMode.Fill)

                NanoVg.nvgBeginFrame(current.Handle, fbo.Size.X, fbo.Size.Y, 1.0f)
             
                r.Content.All |> Seq.sortBy fst |> Seq.map snd |> Interpreter.run x current
                NanoVg.nvgEndFrame(current.Handle)

                frameId <- frameId + 1UL

                OpenTK.Graphics.OpenGL.GL.PopAttrib()

                if oldFbo <> (fbo.GetHandle null :?> int) then
                    OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(OpenTK.Graphics.OpenGL4.FramebufferTarget.Framebuffer, oldFbo)
                OpenTK.Graphics.OpenGL4.GL.Viewport(old.[0], old.[1], old.[2], old.[3])

                if wasEnabled then OpenTK.Graphics.OpenGL4.GL.Enable OpenTK.Graphics.OpenGL4.EnableCap.DebugOutput

            )
        )

    interface IRenderTask with
        member x.FramebufferSignature = None
        member x.Runtime = runtime :> IRuntime |> Some
        member x.Run(caller, fbo) = 
            x.Run(caller, fbo)
            FrameStatistics.Zero

        member x.Dispose() =
            x.Dispose()

        member x.FrameId = frameId

[<Extension; AbstractClass; Sealed>]
type LowLevelRuntimeExtensions private() =
    static let table = ConcurrentDict<Aardvark.Rendering.GL.Context, Context.NanoVgContext>(Dict())

    static let getOrCreateContext (ctx) =
        table.GetOrCreate(ctx, fun ctx -> Context.NanoVgContext(ctx))

    [<Extension>]
    static member GetNanoVgContext (this : IRuntime) =
        match this with
            | :? Runtime as this ->
                getOrCreateContext this.Context
            | _ -> failwithf "unsupported NanoVg runtime: %A" this

    [<Extension>]
    static member CompileRender(this : IRuntime, list : alist<NvgRenderObject>) =
        match this with
            | :? Runtime as this ->
                new RenderTask(this, getOrCreateContext this.Context, list) :> IRenderTask
            | _ -> failwithf "unsupported NanoVg runtime: %A" this

    [<Extension>]
    static member CompileRender(this : IRuntime, list : list<NvgRenderObject>) =
        match this with
            | :? Runtime as this ->
                new RenderTask(this, getOrCreateContext this.Context, AList.ofList list) :> IRenderTask
            | _ -> failwithf "unsupported NanoVg runtime: %A" this