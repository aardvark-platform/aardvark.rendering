namespace Aardvark.Rendering.NanoVg

open Aardvark.Base
open Aardvark.Base.Incremental

type PathRenderJob =
    {
        strokeWidth : IMod<float>
        strokeColor : IMod<C4f>
        lineCap : IMod<LineCap>
        lineJoin : IMod<LineJoin>
        winding : IMod<Winding>
        primitive : IMod<Primitive>
        mode : IMod<PrimitiveMode>
    }     

type TextRenderJob =
    {
        font : IMod<Font>
        size : IMod<float>
        letterSpacing : IMod<float>
        lineHeight : IMod<float>
        blur : IMod<float>
        align : IMod<TextAlign>
        content : IMod<string>
    }     

type NvgRenderJob =
    {
        transform : IMod<M33d>
        scissor : IMod<Box2d>
        fillColor : IMod<C4f>
        isActive : IMod<bool>

        command : Either<PathRenderJob, TextRenderJob>
    }