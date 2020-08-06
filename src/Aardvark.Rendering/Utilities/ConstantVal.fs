namespace Aardvark.Base

open FSharp.Data.Adaptive

type ConstantVal<'T>(value : 'T) =
    inherit ConstantObject()

    interface IAdaptiveValue with
        member x.ContentType = typeof<'T>
        member x.GetValueUntyped _ = value :> obj

    interface aval<'T> with
        member x.GetValue _ = value