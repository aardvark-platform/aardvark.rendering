namespace Aardvark.Rendering

open System
open FShade
open FSharp.Data.Adaptive

type IBackendSurface =
    inherit IDisposable
    abstract member Handle : obj

type DynamicSurface = EffectInputLayout * aval<Imperative.Module>

[<RequireQualifiedAccess>]
type Surface =
    | Effect of effect: Effect
    | Dynamic of compile: Func<IFramebufferSignature, IndexedGeometryMode, DynamicSurface>
    | Backend of surface: IBackendSurface
    | None

    [<Obsolete("Use Surface.Effect instead.")>]
    static member FShadeSimple(effect: Effect) = Surface.Effect effect

    [<Obsolete("Use Surface.Dynamic instead.")>]
    static member FShade(compile: IFramebufferSignature -> IndexedGeometryMode -> DynamicSurface) = Surface.Dynamic compile

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Surface =

    [<Sealed; AbstractClass>]
    type Converter() =
        static member inline ToSurface(surface: Surface)         = surface
        static member inline ToSurface(effect: Effect)           = Surface.Effect effect
        static member inline ToSurface(effects: #seq<Effect>)    = Surface.Effect <| FShade.Effect.compose effects
        static member inline ToSurface(compile: _ -> _ -> _)     = Surface.Dynamic compile
        static member inline ToSurface(compile: Func<_, _, _>)   = Surface.Dynamic compile
        static member inline ToSurface(surface: IBackendSurface) = Surface.Backend surface

    let inline private toSurface (_ : ^Z) (data: ^T) =
        ((^Z or ^T) : (static member ToSurface : ^T -> Surface)(data))

    /// Creates a surface from the given data.
    /// May be an effect, sequence of effects, backend surface, or dynamic surface function.
    let inline create (data: ^T) =
        toSurface Unchecked.defaultof<Converter> data