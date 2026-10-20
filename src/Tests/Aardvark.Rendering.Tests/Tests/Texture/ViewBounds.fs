namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open Expecto

module TextureViewBounds =

    module private Cases =

        let private withTextures (runtime : IRuntime) action =
            use array = runtime.CreateTexture2DArray(V2i(16, 8), TextureFormat.Rgba8, levels = 5, count = 8)
            use cube = runtime.CreateTextureCubeArray(16, TextureFormat.Rgba8, levels = 5, count = 2)
            for name, texture in ["array", array; "cube array", cube] do action name texture

        let private parent (texture : IBackendTexture) aspect =
            texture.GetSlice(aspect, Some 1, Some 3, Some 2, Some (texture.Slices - 3))

        let private ranges (p : ITextureRange) =
            [ "range", p
              "level as range", p.[1] :> ITextureRange
              "slice as range", p.GetSlice(None, None, 1) :> ITextureRange
              "subresource as range", p.[1, 1] :> ITextureRange
              "narrowed range", p.GetSlice(Some 0, Some 0, Some 0, Some 0) ]

        let private length (range : Range1i) = range.Max - range.Min + 1
        let private indices count = [-1; count; Int32.MinValue; Int32.MaxValue; Int32.MaxValue - 1]
        let private intervals count =
            [ Some -1, None; None, Some -1; Some count, None; None, Some count
              Some Int32.MinValue, None; None, Some Int32.MinValue
              Some Int32.MaxValue, None; None, Some Int32.MaxValue
              Some Int32.MaxValue, Some Int32.MinValue; Some Int32.MinValue, Some Int32.MaxValue
              Some 1, Some 0; Some (count - 1), Some 0 ]
            |> List.filter (fun (lo, hi) -> not (lo = Some 0 && hi = Some 0))

        let private reject context action =
            let error = try action(); None with e -> Some e
            match error with
            | Some (:? ArgumentException) -> ()
            | _ -> failtestf "%s: expected ArgumentException, got %A" context error

        let private physicalEscapes context (range : Range1i) physicalCount =
            for index in [-1; length range] do
                let absolute = int64 range.Min + int64 index
                Expect.isTrue (absolute >= 0L && absolute < int64 physicalCount)
                    (sprintf "%s, index=%d: escaping parent still lies in the physical texture" context index)

        let rangeEscapes (runtime : IRuntime) =
            withTextures runtime (fun shape texture ->
                for kind, p in ranges (parent texture TextureAspect.Color) do
                    let context = shape + ", " + kind
                    physicalEscapes (context + ", level") p.Levels texture.MipMapLevels
                    physicalEscapes (context + ", slice") p.Slices texture.Slices
                    for index in indices (length p.Levels) do
                        let context = sprintf "%s, level=%d" context index
                        reject (context + ": Item level") (fun () -> p.[index] |> ignore)
                        reject (context + ": Item level and slice") (fun () -> p.[index, 0] |> ignore)
                        reject (context + ": GetSlice level") (fun () -> p.GetSlice(index, None, None) |> ignore)
                    for index in indices (length p.Slices) do
                        let context = sprintf "%s, slice=%d" context index
                        reject (context + ": Item level and slice") (fun () -> p.[0, index] |> ignore)
                        reject (context + ": GetSlice slice") (fun () -> p.GetSlice(None, None, index) |> ignore)
                    for lo, hi in intervals (length p.Levels) do
                        let context = sprintf "%s, levels=%A..%A" context lo hi
                        reject (context + ": range") (fun () -> p.GetSlice(lo, hi, None, None) |> ignore)
                        reject (context + ": slice") (fun () -> p.GetSlice(lo, hi, 0) |> ignore)
                    for lo, hi in intervals (length p.Slices) do
                        let context = sprintf "%s, slices=%A..%A" context lo hi
                        reject (context + ": range") (fun () -> p.GetSlice(None, None, lo, hi) |> ignore)
                        reject (context + ": level") (fun () -> p.GetSlice(0, lo, hi) |> ignore))

        let levelEscapes (runtime : IRuntime) =
            withTextures runtime (fun shape texture ->
                let p = parent texture TextureAspect.Color
                for kind, level in ["level", p.[1]; "subresource as level", p.[1, 1] :> ITextureLevel] do
                    let context = shape + ", " + kind
                    physicalEscapes context level.Slices texture.Slices
                    for index in indices (length level.Slices) do
                        reject (sprintf "%s, slice=%d" context index) (fun () -> level.[index] |> ignore)
                    for lo, hi in intervals (length level.Slices) do
                        reject (sprintf "%s, slices=%A..%A" context lo hi) (fun () -> level.GetSlice(lo, hi) |> ignore))

        let sliceEscapes (runtime : IRuntime) =
            withTextures runtime (fun shape texture ->
                let p = parent texture TextureAspect.Color
                for kind, slice in ["slice", p.GetSlice(None, None, 1); "subresource as slice", p.[1, 1] :> ITextureSlice] do
                    let context = shape + ", " + kind
                    physicalEscapes context slice.Levels texture.MipMapLevels
                    for index in indices (length slice.Levels) do
                        reject (sprintf "%s, level=%d" context index) (fun () -> slice.[index] |> ignore)
                    for lo, hi in intervals (length slice.Levels) do
                        reject (sprintf "%s, levels=%A..%A" context lo hi) (fun () -> slice.GetSlice(lo, hi) |> ignore))

        let private check context (texture : IBackendTexture) aspect levels slices (value : ITextureRange) =
            Expect.isTrue (obj.ReferenceEquals(value.Texture, texture)) (context + ": texture identity")
            Expect.equal value.Aspect aspect (context + ": aspect identity")
            Expect.equal value.Levels levels (context + ": absolute levels")
            Expect.equal value.Slices slices (context + ": absolute slices")

        let private endpoints count =
            [None, None; Some 0, None; None, Some (count - 1); Some 0, Some (count - 1)
             Some (count - 1), None; None, Some 0; Some (count - 1), Some (count - 1)] |> List.distinct

        let private translate (range : Range1i) (lo, hi) =
            Range1i(range.Min + defaultArg lo 0, range.Min + defaultArg hi (length range - 1))

        let private valid context (texture : IBackendTexture) aspect =
            for kind, p in ranges (parent texture aspect) do
                let context = context + ", " + kind
                let verify suffix levels slices value = check (context + ": " + suffix) texture aspect levels slices value
                for level = 0 to length p.Levels - 1 do
                    let absoluteLevel = p.Levels.Min + level
                    let levelView : ITextureLevel = p.[level]
                    verify "Item level" (Range1i(absoluteLevel)) p.Slices levelView
                    Expect.equal levelView.Size (texture.GetSize absoluteLevel) (context + ": level extent")
                    Expect.isTrue (obj.ReferenceEquals(levelView.Runtime, texture.Runtime)) (context + ": runtime identity")
                    Expect.equal (levelView.Format, levelView.Samples) (texture.Format, texture.Samples) (context + ": output metadata")
                    for slice = 0 to length p.Slices - 1 do
                        let absoluteSlice = p.Slices.Min + slice
                        let selected : ITextureSubResource = p.[level, slice]
                        verify "Item level and slice" (Range1i(absoluteLevel)) (Range1i(absoluteSlice)) selected
                        Expect.equal (selected.Level, selected.Slice, selected.Size) (absoluteLevel, absoluteSlice, texture.GetSize absoluteLevel)
                            (context + ": subresource metadata")
                        verify "level Item" (Range1i(absoluteLevel)) (Range1i(absoluteSlice)) levelView.[slice]
                    for lo, hi in endpoints (length p.Slices) do
                        let expected = translate p.Slices (lo, hi)
                        verify "GetSlice level" (Range1i(absoluteLevel)) expected (p.GetSlice(level, lo, hi))
                        let result : ITextureRange = levelView.GetSlice(lo, hi)
                        verify "level GetSlice" (Range1i(absoluteLevel)) expected result
                for slice = 0 to length p.Slices - 1 do
                    let absoluteSlice = p.Slices.Min + slice
                    let sliceView : ITextureSlice = p.GetSlice(None, None, slice)
                    verify "GetSlice slice" p.Levels (Range1i(absoluteSlice)) sliceView
                    for level = 0 to length p.Levels - 1 do
                        verify "slice Item" (Range1i(p.Levels.Min + level)) (Range1i(absoluteSlice)) sliceView.[level]
                    for lo, hi in endpoints (length p.Levels) do
                        let expected = translate p.Levels (lo, hi)
                        verify "GetSlice level range and slice" expected (Range1i(absoluteSlice)) (p.GetSlice(lo, hi, slice))
                        let result : ITextureRange = sliceView.GetSlice(lo, hi)
                        verify "slice GetSlice" expected (Range1i(absoluteSlice)) result
                for levels in endpoints (length p.Levels) do
                    for slices in endpoints (length p.Slices) do
                        let l0, l1 = levels
                        let s0, s1 = slices
                        verify (sprintf "GetSlice ranges %A %A" levels slices) (translate p.Levels levels) (translate p.Slices slices)
                            (p.GetSlice(l0, l1, s0, s1))
                // Validate against the newly narrowed parent, not its ancestors.
                let narrowed = p.GetSlice(Some 0, Some 0, Some 0, Some 0)
                check (context + ": repeated defaults") texture aspect (Range1i(p.Levels.Min)) (Range1i(p.Slices.Min))
                    (narrowed.GetSlice(None, None, None, None))

        let boundariesAndDefaults (runtime : IRuntime) =
            withTextures runtime (fun shape texture ->
                valid shape texture TextureAspect.Color
                Expect.equal (texture.GetSize Int32.MinValue) (texture.GetSize 0) (shape + ": lower GetSize clamp unchanged")
                Expect.equal (texture.GetSize Int32.MaxValue) (texture.GetSize(texture.MipMapLevels - 1)) (shape + ": upper GetSize clamp unchanged"))
            use depth = runtime.CreateTexture2DArray(V2i(16, 8), TextureFormat.Depth24Stencil8, levels = 5, count = 8)
            for aspect in [TextureAspect.Depth; TextureAspect.Stencil; TextureAspect.DepthStencil] do
                valid (sprintf "depth-stencil aspect=%A" aspect) depth aspect

        let nestedTransfer (runtime : IRuntime) =
            use texture = runtime.CreateTexture2DArray(V2i(16, 8), TextureFormat.Rgba8, levels = 5, count = 8)
            let p = texture.GetSlice(TextureAspect.Color, Some 1, Some 3, Some 1, Some 6)
            let nested = p.GetSlice(Some 1, Some 2, Some 1, Some 3)
            let target = texture.[TextureAspect.Color, 3, 4]
            let neighbor = texture.[TextureAspect.Color, 3, 5]
            let size = target.Size.XY
            let sentinel = PixImage.solid size (C4b(211uy, 31uy, 79uy, 233uy))
            neighbor.Upload(sentinel)
            let routes =
                [ "range", nested.[1, 2]
                  "level", nested.GetSlice(1, None, None).[2]
                  "slice", nested.GetSlice(None, None, 2).[1] ]
            for iteration, (name, view) in routes |> List.indexed do
                check name texture TextureAspect.Color (Range1i(3)) (Range1i(4)) view
                let data = PixImage.solid size C4b.Black
                data.GetMatrix<C4b>().SetByCoord(fun (p : V2l) -> C4b(byte (17 + int p.X + 23 * iteration), byte (91 + int p.Y), 149uy, byte (37 + 11 * iteration))) |> ignore
                view.Upload(data)
                let actual = PixImage<byte>(Col.Format.RGBA, size)
                view.Download(actual)
                PixImage.compare V2i.Zero data actual
                target.Download(actual)
                PixImage.compare V2i.Zero data actual
                neighbor.Download(actual)
                PixImage.compare V2i.Zero sentinel actual

    let tests (target : TestTarget) =
        [
            "Range selections reject parent escapes",   Cases.rangeEscapes
            "Level selections reject parent escapes",   Cases.levelEscapes
            "Slice selections reject parent escapes",   Cases.sliceEscapes
            "Nested boundaries defaults and identity",  Cases.boundariesAndDefaults
            "Valid nested subresource transfer",        Cases.nestedTransfer
        ]
        |> prepareCasesGpu "ViewBounds" target
