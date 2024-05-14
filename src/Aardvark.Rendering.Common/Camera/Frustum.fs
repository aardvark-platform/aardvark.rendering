namespace Aardvark.Rendering

open Aardvark.Base
open System.Runtime.CompilerServices

type Frustum =
    {
        left    : float
        right   : float
        bottom  : float
        top     : float
        near    : float
        far     : float
        isOrtho : bool
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Frustum =
    let perspective (horizontalFieldOfViewInDegrees : float) (near : float) (far : float) (aspect : float) =
        let d = tan (0.5 * Conversion.RadiansFromDegrees horizontalFieldOfViewInDegrees) * near
        { left = -d; right = +d; bottom = -d / aspect; top = +d / aspect; near = near; far = far; isOrtho = false }

    let ortho (b : Box3d) =
        {
            left = b.Min.X
            right = b.Max.X
            bottom = b.Min.Y
            top = b.Max.Y
            near = b.Min.Z
            far = b.Max.Z
            isOrtho = true
        }

    let projTrafo {left = l; right = r; top = t; bottom = b; near = n; far = f; isOrtho = isOrtho } : Trafo3d =
        if isOrtho then
            Trafo3d.OrthoProjectionGL(l, r, b, t, n, f)
        else
            Trafo3d.PerspectiveProjectionGL(l, r, b, t, n, f)

    let private isTrafoOrtho (t : Trafo3d) =
        t.Forward.M30.IsTiny() && t.Forward.M31.IsTiny() && t.Forward.M32.IsTiny()

    let ofTrafo (t : Trafo3d) =
        let isOrtho = isTrafoOrtho t
        let m = t.Forward
        if not isOrtho then
            let r = (1.0 + m.M22) / (m.M22 - 1.0)
            let far     = (r - 1.0) * m.M23 / (2.0 * r)
            let near    = r * far
            let top     = (1.0 + m.M12) * near / m.M11
            let bottom  = (m.M12 - 1.0) * near / m.M11
            let left    = (m.M02 - 1.0) * near / m.M00
            let right   = (1.0 + m.M02) * near / m.M00

            {
                isOrtho = false
                left = left
                right = right
                top = top
                bottom = bottom
                near = near
                far = far
            }
        else
            let left        = -(1.0 + m.M03) / m.M00
            let right       = (1.0 - m.M03) / m.M00
            let bottom      = -(1.0 + m.M13) / m.M11
            let top         = (1.0 - m.M13) / m.M11
            let far         = -(1.0 + m.M23) / m.M22
            let near        = (1.0 - m.M23) / m.M22
            {
                isOrtho = true
                left = left
                right = right
                top = top
                bottom = bottom
                near = near
                far = far
            }

    let withNear (near : float) (f : Frustum) =
        if f.isOrtho then
            { f with near = near }
        else
            let factor = near / f.near
            {
                isOrtho = false
                near = near
                far = f.far
                left = factor * f.left
                right = factor * f.right
                top = factor * f.top
                bottom = factor * f.bottom
            }

    let withFar (far : float) (f : Frustum) =
        { f with far = far }

    let aspect { left = l; right = r; top = t; bottom = b } =
        (r - l) / (t - b)

    let withAspect (newAspect : float) ( { left = l; right = r; top = t; bottom = b } as f )  =
        let factor = aspect f / newAspect
        { f with top = factor * t; bottom = factor * b }

    let withHorizontalFieldOfViewInDegrees (angleInDegrees : float) (frustum : Frustum) =
        if frustum.isOrtho then
            frustum
        else
            let aspect = aspect frustum
            perspective angleInDegrees frustum.near frustum.far aspect

    let horizontalFieldOfViewInDegrees { left = l; right = r; near = near } =
        let l,r = atan2 l near, atan2 r near
        Conversion.DegreesFromRadians(-l + r)

    let inline near   (f : Frustum) = f.near
    let inline far    (f : Frustum) = f.far
    let inline left   (f : Frustum) = f.left
    let inline right  (f : Frustum) = f.right
    let inline bottom (f : Frustum) = f.bottom
    let inline top    (f : Frustum) = f.top

[<Extension;AutoOpen>]
type CameraCSharpExtensions() =
    [<Extension>]
    static member ProjTrafo(f : Frustum) = Frustum.projTrafo f