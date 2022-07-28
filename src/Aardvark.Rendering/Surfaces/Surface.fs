namespace Aardvark.Rendering

open System
open FShade
open FSharp.Data.Adaptive

[<AllowNullLiteral>]
type ISurface = interface end

type IBackendSurface =
    inherit ISurface
    inherit IDisposable
    abstract member Handle : obj

[<RequireQualifiedAccess>]
type Surface =
    | FShadeSimple of Effect
    | FShade of (EffectConfig -> EffectInputLayout * aval<Imperative.Module>)
    | Backend of ISurface
    | None