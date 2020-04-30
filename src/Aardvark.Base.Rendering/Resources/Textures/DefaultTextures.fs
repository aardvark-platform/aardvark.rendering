namespace Aardvark.Base

open FSharp.Data.Adaptive

module DefaultTextures =

    let checkerboardPix =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II * 256)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 16L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                C4b.Gray
        ) |> ignore
        pi

    let checkerboard =
        PixTexture2d(PixImageMipMap [| checkerboardPix :> PixImage |], true) :> ITexture
        |> AVal.constant

    let blackPix =
        let pi = PixImage<byte>(Col.Format.RGBA, V2i.II)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) -> C4b.Black) |> ignore
        pi

    let blackTex =
        PixTexture2d(PixImageMipMap [| blackPix :> PixImage |], false) :> ITexture
        |> AVal.constant