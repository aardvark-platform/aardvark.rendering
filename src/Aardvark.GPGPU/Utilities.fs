namespace Aardvark.Base


[<AutoOpen>]
module GPGPUHelpers =
            
    let inline ceilDiv (v : ^a) (a : ^a) : ^a =
        if v % a = LanguagePrimitives.GenericZero then v / a
        else LanguagePrimitives.GenericOne + v / a


    let ceilDiv2 (v : V2i) (a : V2i) =
        V2i(ceilDiv v.X a.X, ceilDiv v.Y a.Y)

    let ceilDiv3 (v : V3i) (a : V3i) =
        V3i(ceilDiv v.X a.X, ceilDiv v.Y a.Y, ceilDiv v.Z a.Z)