namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_compute_shader =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(4, 3)) "ARB_compute_shader"
        static member ARB_compute_shader = supported

    let private MaxComputeWorkGroupSize = unbox<GetIndexedPName> All.MaxComputeWorkGroupSize
    let private MaxComputeWorkGroupInvocations = unbox<GetPName> All.MaxComputeWorkGroupInvocations

    type GetIndexedPName with
        static member MaxComputeWorkGroupSize = MaxComputeWorkGroupSize

    type GetPName with
        static member MaxComputeWorkGroupInvocations = MaxComputeWorkGroupInvocations