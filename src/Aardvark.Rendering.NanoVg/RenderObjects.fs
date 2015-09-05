namespace Aardvark.Rendering.NanoVg

open Aardvark.Base
open Aardvark.Base.Incremental

type PathRenderObject =
    {
        strokeWidth : IMod<float>
        strokeColor : IMod<C4f>
        lineCap : IMod<LineCap>
        lineJoin : IMod<LineJoin>
        winding : IMod<Winding>
        primitive : IMod<Primitive>
        mode : IMod<PrimitiveMode>
    }     

type TextRenderObject =
    {
        font : IMod<Font>
        size : IMod<float>
        letterSpacing : IMod<float>
        lineHeight : IMod<float>
        blur : IMod<float>
        align : IMod<TextAlign>
        content : IMod<string>
    }     

type NvgRenderObject =
    {
        transform : IMod<M33d>
        scissor : IMod<Box2d>
        fillColor : IMod<C4f>
        isActive : IMod<bool>

        command : Either<PathRenderObject, TextRenderObject>
    }