namespace Aardvark.Rendering.Vulkan

open Aardvark.Base
open FSharp.NativeInterop
open System
open System.Runtime.InteropServices

open EXTLayerSettings
open TypeMeta

#nowarn "9"

[<Struct>]
type internal LayerSetting =
    { Layer  : string
      Name   : string
      Values : Array }

type internal LayerSettings(settings: LayerSetting[]) =
    let disposables = ResizeArray<IDisposable>()

    let addCompensation (comp: unit -> unit) =
        disposables.Add({ new IDisposable with member _.Dispose() = comp() })

    let getString (str: string) =
        let pStr = CStr.malloc str
        addCompensation (fun _ -> Marshal.FreeHGlobal(pStr.Address))
        pStr

    let getArray (arr: Array) =
        let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
        addCompensation (fun _ -> gc.Free() )
        gc.AddrOfPinnedObject()

    let allocateValues (values: Array) =
        let typ =
            match values.GetType().GetElementType() with
            | Bool    -> VkLayerSettingTypeEXT.Bool32
            | Int32   -> VkLayerSettingTypeEXT.Int32
            | Int64   -> VkLayerSettingTypeEXT.Int64
            | UInt32  -> VkLayerSettingTypeEXT.Int32
            | UInt64  -> VkLayerSettingTypeEXT.Int64
            | Float32 -> VkLayerSettingTypeEXT.Float32
            | Float64 -> VkLayerSettingTypeEXT.Float64
            | String  -> VkLayerSettingTypeEXT.String
            | typ     -> failwith $"Unsupported layer setting type: {typ}"

        let values : Array =
            match typ with
            | VkLayerSettingTypeEXT.Bool32 -> Array.map (fun value -> if value then VkTrue else VkFalse) (values :?> bool[])
            | VkLayerSettingTypeEXT.String -> Array.map getString (values :?> string[])
            | _ -> values

        typ, getArray values

    let pSettings =
        settings |> Array.map (fun s ->
            let pLayerName = getString s.Layer
            let pSettingName = getString s.Name
            let typ, pValues = allocateValues s.Values
            VkLayerSettingEXT(pLayerName, pSettingName, typ, uint32 s.Values.Length, pValues)
        )
        |> getArray
        |> NativePtr.ofNativeInt<VkLayerSettingEXT>

    new (settings: LayerSetting seq) =
        new LayerSettings (Seq.asArray settings)

    member _.Count = settings.Length

    member _.Pointer = pSettings

    member _.Dispose() =
        for d in disposables do d.Dispose()
        disposables.Clear()

    interface IDisposable with
        member this.Dispose() = this.Dispose()