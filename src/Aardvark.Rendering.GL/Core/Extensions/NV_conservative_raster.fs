namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4

[<AutoOpen>]
module NV_conservative_raster =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(999,999)) "GL_NV_conservative_raster"
        static member NV_conservative_raster = supported

    type EnableCap with
        static member ConservativeRasterization = NvConservativeRaster.ConservativeRasterizationNv