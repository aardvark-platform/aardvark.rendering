namespace Aardvark.Rendering

open System
open FShade
open FSharp.Data.Adaptive

type IBackendSurface =
    inherit IDisposable
    abstract member Handle : obj

type DynamicSurface = EffectInputLayout * aval<Imperative.Module>

[<RequireQualifiedAccess; ReferenceEquality>]
type Surface =
    | FShadeSimple of Effect
    | FShade of (EffectConfig -> DynamicSurface)
    | Backend of IBackendSurface
    | None

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Surface =

    [<Sealed; AbstractClass>]
    type Converter() =
        static member inline ToSurface(surface: Surface) = surface
        static member inline ToSurface(effect: Effect) = Surface.FShadeSimple effect
        static member inline ToSurface(effects: #seq<Effect>) = Surface.FShadeSimple <| FShade.Effect.compose effects
        static member inline ToSurface(create: EffectConfig -> DynamicSurface) = Surface.FShade create
        static member inline ToSurface(surface: IBackendSurface) = Surface.Backend surface

    let inline private toSurface (_ : ^Z) (data: ^T) =
        ((^Z or ^T) : (static member ToSurface : ^T -> Surface)(data))

    /// Creates a surface from the given data.
    /// May be an effect, sequence of effects, backend surface, or dynamic surface function.
    let inline create (data: ^T) =
        toSurface Unchecked.defaultof<Converter> data