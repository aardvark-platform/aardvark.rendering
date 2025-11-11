namespace Aardvark.Rendering.GL

open System
open System.Security
open System.Runtime.InteropServices
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering.GL

[<AutoOpen>]
module ARB_compute_variable_group_size =

    type GL private() =
        static let supported = ExtensionHelpers.isSupported (Version(666,666,666)) "GL_ARB_compute_variable_group_size"
        static member ARB_compute_variable_group_size = supported

    [<AutoOpen>]
    module private Delegates =

        type Marshal with
            static member GetDelegateForFunctionPointer<'T>(ptr : nativeint) : 'T =
                Marshal.GetDelegateForFunctionPointer(ptr, typeof<'T>) |> unbox<'T>

        [<SuppressUnmanagedCodeSecurity>]
        type private DispatchComputeGroupSizeDel = delegate of uint32 * uint32 * uint32 * uint32 * uint32 * uint32 -> unit

        type Functions() =
            static let dDispatchComputeGroupSize =
                if GL.ARB_compute_variable_group_size then
                    let ptr = ExtensionHelpers.getAddress "glDispatchComputeGroupSizeARB"
                    if ptr = 0n then
                        failwith "[GL] gc claimed to support GL_ARB_compute_variable_group_size but does not"

                    Marshal.GetDelegateForFunctionPointer<DispatchComputeGroupSizeDel>(ptr)
                else
                    DispatchComputeGroupSizeDel(fun _ _ _ _ _ _ -> failwith "[GL] does not support GL_ARB_compute_variable_group_size")

            static member DispatchComputeGroupSize(numGroupsX : uint32, numGroupsY : uint32, numGroupsZ : uint32,
                                                   groupSizeX : uint32, groupSizeY : uint32, groupSizeZ : uint32) =
                dDispatchComputeGroupSize.Invoke(numGroupsX, numGroupsY, numGroupsZ, groupSizeX, groupSizeY, groupSizeZ)

    type GL.Dispatch with
        static member DispatchComputeGroupSize(numGroupsX : uint32, numGroupsY : uint32, numGroupsZ : uint32,
                                               groupSizeX : uint32, groupSizeY : uint32, groupSizeZ : uint32) =
            Functions.DispatchComputeGroupSize(
                numGroupsX, numGroupsY, numGroupsZ, groupSizeX, groupSizeY, groupSizeZ
            )

        static member DispatchComputeGroupSize(numGroups : V3ui, groupSize : V3ui) =
            Functions.DispatchComputeGroupSize(
                numGroups.X, numGroups.Y, numGroups.Z,
                groupSize.X, groupSize.Y, groupSize.Z
            )

        static member inline DispatchComputeGroupSize(numGroups : V3i, groupSize : V3i) =
            GL.Dispatch.DispatchComputeGroupSize(V3ui numGroups, V3ui groupSize)

    let private MaxComputeVariableGroupSize = unbox<GetIndexedPName> 0x9345
    let private MaxComputeVariableGroupInvocations = unbox<GetPName> 0x9344

    type GetIndexedPName with
        static member MaxComputeVariableGroupSize = MaxComputeVariableGroupSize

    type GetPName with
        static member MaxComputeVariableGroupInvocations = MaxComputeVariableGroupInvocations