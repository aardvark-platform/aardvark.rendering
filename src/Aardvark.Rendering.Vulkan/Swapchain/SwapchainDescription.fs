namespace Aardvark.Rendering.Vulkan

open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Vulkan
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AbstractClass>]
type AbstractGraphicsMode() =
    abstract member ImageTrafo                      : ImageTrafo
    abstract member ChooseColorFormat               : Set<VkFormat> -> VkFormat
    abstract member ChooseColorSpace                : Set<VkColorSpaceKHR> -> VkColorSpaceKHR
    abstract member ChooseDepthSteniclFormat        : Set<VkFormat> -> Option<VkFormat>
    abstract member ChoosePresentMode               : Set<VkPresentModeKHR> -> VkPresentModeKHR
    abstract member ChooseBufferCount               : int * int -> int
    abstract member Samples                         : int

type GraphicsMode(format : Col.Format, bits : int, depthBits : int, stencilBits : int, buffers : int, samples : int, imageTrafo : ImageTrafo) =
    inherit AbstractGraphicsMode()
    let bitType =
        match bits with
            | 8 -> typeof<uint8>
            | 16 -> typeof<uint16>
            | 32 -> typeof<uint32>
            | _ -> failf "unsupported color-bits: %A" bits

    let pixFormat = PixFormat(bitType, format)

    let rec probeColorExtendChannels (available : Map<Col.Format, Set<VkFormat>>) (format : Col.Format) (wantedBits : int) =
        match Map.tryFind format available with
            | Some fmts ->
                fmts |> Seq.tryFind (fun fmt ->
                    let channels = VkFormat.channels fmt
                    let totalSize = 8 * VkFormat.sizeInBytes fmt
                    totalSize / channels = wantedBits 
                )
            | None ->
                let next =
                    match format with
                        | Col.Format.Gray -> Col.Format.GrayAlpha
                        | Col.Format.GrayAlpha -> Col.Format.RGBA
                        | Col.Format.NormalUV -> Col.Format.RGB
                        | Col.Format.BGR -> Col.Format.BGRA
                        | Col.Format.RGB -> Col.Format.RGBA
                        | Col.Format.RGBA -> Col.Format.BGRA
                        | f -> f
                if next <> format then
                    probeColorExtendChannels available next bits
                else
                    None

    let rec probeColor (available : Map<Col.Format, Set<VkFormat>>) (format : Col.Format) (wantedBits : int) =
        match probeColorExtendChannels available format wantedBits with
            | Some fmt -> Some fmt
            | None ->
                let nextBits = Fun.NextPowerOfTwo (wantedBits + 1)
                if nextBits > 32 then None
                else probeColor available format nextBits

    let rec probeDepth (available : Map<int * int, VkFormat>) (depthBits : int) (stencilBits : int) =
        match Map.tryFind (depthBits, stencilBits) available with
            | Some fmt -> Some fmt
            | None ->
                if stencilBits = 0 then
                    match probeDepth available depthBits 8 with
                        | Some fmt -> Some fmt
                        | None ->
                            match depthBits with
                                | 16 -> probeDepth available 24 stencilBits
                                | 24 -> probeDepth available 32 stencilBits
                                | _ -> None
                else
                    match depthBits with
                        | 16 -> probeDepth available 24 stencilBits
                        | 24 -> probeDepth available 32 stencilBits
                        | _ -> None  

    let presentModeScore (mode : VkPresentModeKHR) =
        match mode with
            | VkPresentModeKHR.VkPresentModeMailboxKhr -> 16
            | VkPresentModeKHR.VkPresentModeFifoKhr -> 8
            | VkPresentModeKHR.VkPresentModeImmediateKhr -> 4
            | VkPresentModeKHR.VkPresentModeFifoRelaxedKhr -> 2
            | _ -> 0

    member x.Format = format
    member x.Bits = bits
    member x.DepthBits = depthBits
    member x.StencilBits = stencilBits
    member x.Buffers = buffers

    override x.ImageTrafo = imageTrafo
    override x.Samples = samples
    override x.ChooseColorFormat(available : Set<VkFormat>) =
        let map = available |> Seq.map (fun fmt -> VkFormat.toColFormat fmt, fmt) |> Map.ofSeqDupl
        match probeColor map format bits with
            | Some fmt -> fmt
            | _ ->
                failf "could not find compatible format for { format = %A; bits = %A } in %A" format bits available

    override x.ChooseColorSpace(available : Set<VkColorSpaceKHR>) =
        available |> Seq.head

    override x.ChooseDepthSteniclFormat(available : Set<VkFormat>) =
        if depthBits = 0 && stencilBits = 0 then
            None
        else
            let map = 
                available 
                    |> Seq.choose (fun fmt ->
                        match fmt with
                            | VkFormat.D16Unorm -> Some ((16, 0), fmt)
                            | VkFormat.D16UnormS8Uint -> Some ((16, 8), fmt)
                            | VkFormat.D24UnormS8Uint -> Some ((24, 8), fmt)
                            | VkFormat.D32Sfloat -> Some ((32, 0), fmt)
                            | VkFormat.D32SfloatS8Uint -> Some ((32, 8), fmt)
                            | VkFormat.X8D24UnormPack32 -> Some ((24, 0), fmt)
                            | _ -> None
                       )
                    |> Map.ofSeq

            match probeDepth map depthBits stencilBits with
                | Some fmt -> Some fmt
                | None -> failf "could not find compatible depth-format for { depthBits = %A; stencilBits = %A } in %A" depthBits stencilBits available

    override x.ChoosePresentMode(modes : Set<VkPresentModeKHR>) =
        modes |> Seq.maxBy presentModeScore

    override x.ChooseBufferCount(min, max) =
        clamp min max buffers

type SwapchainDescription =
    {
        renderPass      : RenderPass
        surface         : Surface
        colorFormat     : VkFormat
        colorSpace      : VkColorSpaceKHR
        depthFormat     : Option<VkFormat>
        presentMode     : VkPresentModeKHR
        presentTrafo    : ImageTrafo
        blitTrafo       : ImageTrafo
        buffers         : int
        samples         : int
    }



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module SwapchainDescription =
    module private Chooser = 
        let transformProbes =
            [
                ImageTrafo.MirrorY
                ImageTrafo.MirrorX
                ImageTrafo.Rot0
                ImageTrafo.Rot180
            ]

        let chooseTransform (desired : ImageTrafo) (available : Set<ImageTrafo>) =
            if Set.contains desired available then
                desired
            else
                match transformProbes |> List.tryFind (fun v -> Set.contains v available) with
                    | Some transform -> transform
                    | None -> failf "could not get valid Surface transformation from available: %A" available

    let create (surface : Surface) (mode : AbstractGraphicsMode) (device : Device) =
        if not surface.IsSupported then
            failf "could not create SwapchainDescription for Surface"

        let colorFormat         = mode.ChooseColorFormat surface.ColorFormats
        let colorSpace          = mode.ChooseColorSpace surface.ColorSpaces.[colorFormat]
        let depthFormat         = mode.ChooseDepthSteniclFormat surface.DepthFormats
        let presentMode         = mode.ChoosePresentMode surface.PresentModes
        let buffers             = mode.ChooseBufferCount(surface.MinImageCount, surface.MaxImageCount)
        let presentTrafo        = Chooser.chooseTransform mode.ImageTrafo surface.Transforms
        let blitTrafo           = ImageTrafo.compose mode.ImageTrafo (ImageTrafo.inverse presentTrafo)
        let samples             = mode.Samples

        let renderPass = 
            match depthFormat with
                | Some depthFormat ->
                    device.CreateRenderPass(
                        Map.ofList [
                            DefaultSemantic.Colors, { format = VkFormat.toRenderbufferFormat colorFormat; samples = samples }
                            DefaultSemantic.Depth, { format = VkFormat.toRenderbufferFormat depthFormat; samples = samples }
                        ],
                        1, Set.empty
                    )
                | None ->
                    device.CreateRenderPass(
                        Map.ofList [
                            DefaultSemantic.Colors, { format = VkFormat.toRenderbufferFormat colorFormat; samples = samples }
                        ],
                        1, Set.empty
                    )

        {
            renderPass = renderPass
            surface = surface
            colorFormat = colorFormat
            colorSpace = colorSpace
            depthFormat = depthFormat
            presentMode = presentMode
            presentTrafo = presentTrafo
            blitTrafo = blitTrafo
            buffers = buffers
            samples = samples
        }

    let delete (desc : SwapchainDescription) (device : Device) =
        device.Delete(desc.renderPass)

[<AbstractClass; Sealed; Extension>]
type DeviceSwapchainDescriptionExtensions private() =

    [<Extension>]
    static member CreateSwapchainDescription(this : Device, surface : Surface, mode : AbstractGraphicsMode) =
        this |> SwapchainDescription.create surface mode

    [<Extension>]
    static member Delete(this : Device, desc : SwapchainDescription) =
        this |> SwapchainDescription.delete desc
