namespace Aardvark.Base

open Aardvark.Base.Rendering

type SamplerDescription =
    {
        textureName : Symbol
        samplerState : SamplerStateDescription
    }
