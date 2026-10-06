namespace Aardvark.Rendering.Tests.Texture

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Tests
open FSharp.Data.Adaptive
open Expecto

module AdaptiveAttachment =

    module private Cases =

        type private Texture(dimension : TextureDimension, size : V3i, levels : int, count : int,
                             format : TextureFormat, samples : int) =
            let mutable disposals = 0
            let mutable name = null
            let mutable size = size
            let mutable levels = levels
            member _.Disposals = disposals
            member _.Resize(newSize, newLevels) = size <- newSize; levels <- newLevels
            interface IBackendTexture with
                member _.Runtime = Unchecked.defaultof<ITextureRuntime>
                member _.Dimension = dimension
                member _.Size = size
                member _.MipMapLevels = levels
                member _.Count = count
                member _.Format = format
                member _.Samples = samples
                member _.Handle = 0UL
                member _.WantMipMaps = levels > 1
                member _.Name with get() = name and set value = name <- value
                member _.Dispose() = disposals <- disposals + 1

        type private Source(input : aval<IBackendTexture>) =
            inherit AdaptiveResource<IBackendTexture>()
            member val Creates = 0 with get, set
            member val Destroys = 0 with get, set
            member val Computes = 0 with get, set
            override x.Create() = x.Creates <- x.Creates + 1
            override x.Destroy() = x.Destroys <- x.Destroys + 1
            override x.Compute(token, _) =
                x.Computes <- x.Computes + 1
                input.GetValue token

        let private textures() =
            [
                TextureDimension.Texture2D,   V3i(64, 32, 1), 7, 1
                TextureDimension.Texture2D,   V3i(31, 9, 1),  5, 1
                TextureDimension.Texture2D,   V3i(17, 7, 1),  5, 3
                TextureDimension.TextureCube, V3i(32, 32, 1), 6, 1
                TextureDimension.TextureCube, V3i(7, 7, 1),   3, 2
                TextureDimension.Texture1D,   V3i(33, 1, 1),  6, 2
                TextureDimension.Texture3D,   V3i(17, 9, 5),  5, 1
                TextureDimension.Texture2D,   V3i(1, 3, 1),   2, 1
                TextureDimension.Texture2D,   V3i(1, 1, 1),   1, 1
            ]
            |> List.map (fun (dimension, size, levels, count) ->
                new Texture(dimension, size, levels, count, TextureFormat.Rgba8, 1))

        let private output overload (texture : aval<IBackendTexture>) (aspect : aval<TextureAspect>)
                           (level : aval<int>) (slice : aval<int>) =
            match overload with
            | 0 -> texture.GetOutputView(aspect, level, slice)
            | 1 -> texture.GetOutputView(AVal.force aspect, AVal.force level, AVal.force slice)
            | 2 -> texture.GetOutputView(level, slice)
            | 3 -> texture.GetOutputView(AVal.force level, AVal.force slice)
            | _ -> failwith "Invalid overload"

        let private extent (texture : IBackendTexture) level =
            let shift = max 0 (min (texture.MipMapLevels - 1) level)
            V2i(max 1 (texture.Size.X >>> shift), max 1 (texture.Size.Y >>> shift))

        let private context (texture : IBackendTexture) overload level slice =
            $"{texture.Dimension}, size={texture.Size}, levels={texture.MipMapLevels}, count={texture.Count}, overload={overload}, level={level}, slice={slice}"

        let private check context (texture : IBackendTexture) aspect level slice (attachment : IAdaptiveFramebufferOutput) =
            let expected = extent texture level
            let metadata = AVal.force attachment.Size
            Expect.equal metadata expected $"{context}: selected mip metadata"
            Expect.equal (AVal.force attachment.Format) texture.Format $"{context}: format metadata"
            Expect.equal (AVal.force attachment.Samples) texture.Samples $"{context}: samples metadata"
            let evaluated = attachment.GetValue(AdaptiveToken.Top, RenderToken.Empty)
            Expect.equal metadata evaluated.Size $"{context}: metadata disagrees with evaluated output"
            Expect.equal evaluated.Size expected $"{context}: evaluated mip extent"
            Expect.equal evaluated.Format texture.Format $"{context}: evaluated format"
            Expect.equal evaluated.Samples texture.Samples $"{context}: evaluated samples"
            let range = evaluated :?> ITextureRange
            Expect.isTrue (obj.ReferenceEquals(range.Texture, texture)) $"{context}: selected texture"
            Expect.equal range.Aspect aspect $"{context}: selected aspect"
            Expect.equal range.Levels (Range1i(level, level)) $"{context}: selected level was altered"
            Expect.equal range.Slices (if slice < 0 then Range1i(0, texture.Slices - 1) else Range1i(slice, slice))
                $"{context}: selected slices"
            Expect.isTrue (obj.ReferenceEquals(evaluated, attachment.GetValue(AdaptiveToken.Top, RenderToken.Empty)))
                $"{context}: unchanged output was not cached"

        let staticLevels() =
            for texture in textures() do
                let tex = texture :> IBackendTexture
                for overload in 0 .. 3 do
                    for level in [-5; 0; 1; 2; tex.MipMapLevels - 1; tex.MipMapLevels; Int32.MaxValue] |> List.distinct do
                        for slice in [-1; 0; tex.Slices - 1] |> List.distinct do
                            let attachment = output overload (AVal.constant tex) (AVal.constant tex.Format.Aspect)
                                                    (AVal.constant level) (AVal.constant slice)
                            attachment.Acquire()
                            try check (context tex overload level slice) tex tex.Format.Aspect level slice attachment
                            finally attachment.ReleaseAll()
                Expect.equal texture.Disposals 0 "Attachments must not dispose caller-owned textures"

        let levelChanges() =
            for texture in textures() do
                let tex = texture :> IBackendTexture
                for overload in [0; 2] do
                    for slice in [-1; 0; tex.Slices - 1] |> List.distinct do
                        let level = cval 0
                        let attachment = output overload (AVal.constant tex) (AVal.constant tex.Format.Aspect) level (AVal.constant slice)
                        attachment.Acquire()
                        try
                            for selected in [0; 2; 1; tex.MipMapLevels - 1; Int32.MaxValue; -1; 0] do
                                transact (fun () -> level.Value <- selected)
                                check (context tex overload selected slice) tex tex.Format.Aspect selected slice attachment
                        finally attachment.ReleaseAll()
                Expect.equal texture.Disposals 0 "Level changes must not dispose caller-owned textures"

        let textureChanges() =
            for original in textures() do
                let first = original :> IBackendTexture
                let resizedSize =
                    match first.Dimension with
                    | TextureDimension.Texture1D -> V3i(first.Size.X * 2, 1, 1)
                    | TextureDimension.Texture3D -> first.Size * 2
                    | _ -> V3i(first.Size.XY * 2, 1)
                use resized = new Texture(first.Dimension, resizedSize, first.MipMapLevels + 1, first.Count, TextureFormat.R32f, 1)
                use replaced = new Texture(first.Dimension, first.Size, 1, first.Count, TextureFormat.Depth24Stencil8, 4)
                for overload in 0 .. 3 do
                    for selected in [0; 2] do
                        for slice in [-1; 0; first.Slices - 1] |> List.distinct do
                            for isResource in [false; true] do
                                let texture = cval first
                                let source = Source(texture)
                                let input = if isResource then source :> aval<_> else texture :> aval<_>
                                let level = cval selected
                                let aspect = input |> AVal.map (fun t -> t.Format.Aspect)
                                let attachment = output overload input aspect level (AVal.constant slice)
                                attachment.Acquire()
                                try
                                    for tex in [first; resized :> IBackendTexture; replaced :> IBackendTexture; first] do
                                        transact (fun () -> texture.Value <- tex)
                                        let selectedAspect = if overload = 1 then first.Format.Aspect else tex.Format.Aspect
                                        check $"{context tex overload selected slice}, resource={isResource}"
                                            tex selectedAspect selected slice attachment
                                    if overload = 0 || overload = 2 then
                                        transact (fun () -> level.Value <- 0)
                                        check $"{context first overload 0 slice}, resource={isResource}"
                                            first first.Format.Aspect 0 slice attachment
                                finally attachment.ReleaseAll()
                                if isResource then
                                    Expect.equal source.Creates 1 "Texture changes reacquired the source"
                                    Expect.equal source.Destroys 1 "Texture changes lost source ownership"
                Expect.equal original.Disposals 0 "Texture replacement transferred caller ownership"
                Expect.equal resized.Disposals 0 "Attachment disposed the resized texture"
                Expect.equal replaced.Disposals 0 "Attachment disposed the replacement texture"

        let inPlaceResize() =
            for texture in textures() do
                let tex = texture :> IBackendTexture
                let initialSize, initialLevels = tex.Size, tex.MipMapLevels
                for overload in 0 .. 3 do
                    for slice in [-1; 0; tex.Slices - 1] |> List.distinct do
                        let source = Source(AVal.constant tex)
                        let attachment = output overload source (AVal.constant tex.Format.Aspect)
                                                (AVal.constant 0) (AVal.constant slice)
                        attachment.Acquire()
                        try
                            check (context tex overload 0 slice) tex tex.Format.Aspect 0 slice attachment
                            for size, levels in [V3i.One, 1; initialSize, initialLevels] do
                                transact (fun () -> texture.Resize(size, levels); source.MarkOutdated())
                                check (context tex overload 0 slice) tex tex.Format.Aspect 0 slice attachment
                        finally
                            texture.Resize(initialSize, initialLevels)
                            attachment.ReleaseAll()
                        Expect.equal source.Creates 1 "Same-handle resizing reacquired the source"
                        Expect.equal source.Destroys 1 "Same-handle resizing lost source ownership"
                Expect.equal texture.Disposals 0 "Same-handle resizing transferred caller ownership"

        let aspectAndSliceChanges() =
            for texture in textures() do
                let original = texture :> IBackendTexture
                use depth = new Texture(original.Dimension, original.Size, original.MipMapLevels, original.Count,
                                        TextureFormat.Depth24Stencil8, 1)
                let tex = depth :> IBackendTexture
                let aspect = cval TextureAspect.Depth
                let slice = cval -1
                let attachment = output 0 (AVal.constant tex) aspect (AVal.constant 0) slice
                attachment.Acquire()
                try
                    let size = attachment.Size
                    for selectedAspect in [TextureAspect.Depth; TextureAspect.Stencil; tex.Format.Aspect] do
                        for selectedSlice in [0; tex.Slices - 1; -1] do
                            transact (fun () -> aspect.Value <- selectedAspect; slice.Value <- selectedSlice)
                            Expect.isTrue (obj.ReferenceEquals(size, attachment.Size)) "Size adaptive identity changed"
                            check (context tex 0 0 selectedSlice) tex selectedAspect 0 selectedSlice attachment
                finally attachment.ReleaseAll()
                Expect.equal depth.Disposals 0 "Aspect/slice changes transferred caller ownership"

        let resourceOwnership() =
            for texture in textures() do
                let tex = texture :> IBackendTexture
                for overload in 0 .. 3 do
                    let source = Source(AVal.constant tex)
                    let attachment = output overload source (AVal.constant tex.Format.Aspect) (AVal.constant 0) (AVal.constant -1)
                    let ctx = context tex overload 0 -1
                    attachment.Acquire()
                    attachment.Acquire()
                    try
                        Expect.equal source.Creates 1 $"{ctx}: acquisition forwarding"
                        Expect.equal source.Computes 0 $"{ctx}: acquisition evaluated texture"
                        for _ in 1 .. 8 do
                            Expect.equal (AVal.force attachment.Size) tex.Size.XY $"{ctx}: cached size"
                            Expect.equal (AVal.force attachment.Format) tex.Format $"{ctx}: cached format"
                            Expect.equal (AVal.force attachment.Samples) tex.Samples $"{ctx}: cached samples"
                        Expect.equal source.Computes 1 $"{ctx}: metadata recomputed the texture resource"
                        Expect.isTrue attachment.OutOfDate $"{ctx}: metadata evaluated the attachment"
                        check ctx tex tex.Format.Aspect 0 -1 attachment
                        attachment.Release()
                        Expect.equal source.Destroys 0 $"{ctx}: release ignored remaining owner"
                        check ctx tex tex.Format.Aspect 0 -1 attachment
                        attachment.Release()
                        Expect.equal source.Destroys 1 $"{ctx}: final release forwarding"
                        attachment.Acquire()
                        check ctx tex tex.Format.Aspect 0 -1 attachment
                        Expect.equal source.Creates 2 $"{ctx}: reacquisition forwarding"
                        Expect.equal source.Computes 2 $"{ctx}: reacquisition did not refresh texture"
                    finally attachment.ReleaseAll()
                    Expect.equal source.Destroys 2 $"{ctx}: reacquisition cleanup"
                Expect.equal texture.Disposals 0 "Attachments disposed a resource-owned texture directly"

    let tests (target : TestTarget) =
        [
            "Static selected mip extents",            Cases.staticLevels
            "Level changes and return to base level", Cases.levelChanges
            "Texture replacement and resizing",       Cases.textureChanges
            "In-place resource resize controls",      Cases.inPlaceResize
            "Aspect and slice selection controls",    Cases.aspectAndSliceChanges
            "Resource ownership and cached metadata", Cases.resourceOwnership
        ]
        |> prepareCasesCpu "AdaptiveAttachment" target
