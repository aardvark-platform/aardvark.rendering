namespace Aardvark.Rendering

open FSharp.Data.Adaptive

type ConstantVal<'T>(value : 'T) =
    inherit ConstantObject()

    interface IAdaptiveValue with
        member x.ContentType = typeof<'T>
        member x.GetValueUntyped _ = value :> obj
        member x.Accept (v : IAdaptiveValueVisitor<'R>) = v.Visit x

    interface aval<'T> with
        member x.GetValue _ = value