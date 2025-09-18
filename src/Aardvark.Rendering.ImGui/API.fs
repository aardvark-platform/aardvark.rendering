namespace Aardvark.Rendering.ImGui

open Aardvark.Base
open FSharp.Data.Adaptive
open FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

type INativeContext = HexaGen.Runtime.INativeContext
type ImColor = Hexa.NET.ImGui.ImColor
type ImColorPtr = Hexa.NET.ImGui.ImColorPtr
type ImDrawCallback = Hexa.NET.ImGui.ImDrawCallback
type ImDrawCmd = Hexa.NET.ImGui.ImDrawCmd
type ImDrawCmdPtr = Hexa.NET.ImGui.ImDrawCmdPtr
type ImDrawDataPtr = Hexa.NET.ImGui.ImDrawDataPtr
type ImDrawFlags = Hexa.NET.ImGui.ImDrawFlags
type ImDrawList = Hexa.NET.ImGui.ImDrawList
type ImDrawListPtr = Hexa.NET.ImGui.ImDrawListPtr
type ImDrawListSharedDataPtr = Hexa.NET.ImGui.ImDrawListSharedDataPtr
type ImDrawListSplitterPtr = Hexa.NET.ImGui.ImDrawListSplitterPtr
type ImFontAtlasPtr = Hexa.NET.ImGui.ImFontAtlasPtr
type ImFontAtlasRectPtr = Hexa.NET.ImGui.ImFontAtlasRectPtr
type ImFontBakedPtr = Hexa.NET.ImGui.ImFontBakedPtr
type ImFontConfigPtr = Hexa.NET.ImGui.ImFontConfigPtr
type ImFontGlyphPtr = Hexa.NET.ImGui.ImFontGlyphPtr
type ImFontGlyphRangesBuilderPtr = Hexa.NET.ImGui.ImFontGlyphRangesBuilderPtr
type ImFontLoaderPtr = Hexa.NET.ImGui.ImFontLoaderPtr
type ImFontPtr = Hexa.NET.ImGui.ImFontPtr
type ImGuiButtonFlags = Hexa.NET.ImGui.ImGuiButtonFlags
type ImGuiChildFlags = Hexa.NET.ImGui.ImGuiChildFlags
type ImGuiCol = Hexa.NET.ImGui.ImGuiCol
type ImGuiColorEditFlags = Hexa.NET.ImGui.ImGuiColorEditFlags
type ImGuiComboFlags = Hexa.NET.ImGui.ImGuiComboFlags
type ImGuiCond = Hexa.NET.ImGui.ImGuiCond
type ImGuiContextPtr = Hexa.NET.ImGui.ImGuiContextPtr
type ImGuiDataType = Hexa.NET.ImGui.ImGuiDataType
type ImGuiDir = Hexa.NET.ImGui.ImGuiDir
type ImGuiDockNodeFlags = Hexa.NET.ImGui.ImGuiDockNodeFlags
type ImGuiDragDropFlags = Hexa.NET.ImGui.ImGuiDragDropFlags
type ImGuiFocusedFlags = Hexa.NET.ImGui.ImGuiFocusedFlags
type ImGuiFreeTypeLoaderFlags = Hexa.NET.ImGui.ImGuiFreeTypeLoaderFlags
type ImGuiHoveredFlags = Hexa.NET.ImGui.ImGuiHoveredFlags
type ImGuiIOPtr = Hexa.NET.ImGui.ImGuiIOPtr
type ImGuiInputFlags = Hexa.NET.ImGui.ImGuiInputFlags
type ImGuiInputTextCallback = Hexa.NET.ImGui.ImGuiInputTextCallback
type ImGuiInputTextCallbackData = Hexa.NET.ImGui.ImGuiInputTextCallbackData
type ImGuiInputTextCallbackDataPtr = Hexa.NET.ImGui.ImGuiInputTextCallbackDataPtr
type ImGuiInputTextFlags = Hexa.NET.ImGui.ImGuiInputTextFlags
type ImGuiItemFlags = Hexa.NET.ImGui.ImGuiItemFlags
type ImGuiKey = Hexa.NET.ImGui.ImGuiKey
type ImGuiListClipperPtr = Hexa.NET.ImGui.ImGuiListClipperPtr
type ImGuiMemAllocFunc = Hexa.NET.ImGui.ImGuiMemAllocFunc
type ImGuiMemFreeFunc = Hexa.NET.ImGui.ImGuiMemFreeFunc
type ImGuiMouseButton = Hexa.NET.ImGui.ImGuiMouseButton
type ImGuiMouseCursor = Hexa.NET.ImGui.ImGuiMouseCursor
type ImGuiMouseSource = Hexa.NET.ImGui.ImGuiMouseSource
type ImGuiMultiSelectFlags = Hexa.NET.ImGui.ImGuiMultiSelectFlags
type ImGuiMultiSelectIOPtr = Hexa.NET.ImGui.ImGuiMultiSelectIOPtr
type ImGuiOnceUponAFramePtr = Hexa.NET.ImGui.ImGuiOnceUponAFramePtr
type ImGuiPayloadPtr = Hexa.NET.ImGui.ImGuiPayloadPtr
type ImGuiPlatformIO = Hexa.NET.ImGui.ImGuiPlatformIO
type ImGuiPlatformIOPtr = Hexa.NET.ImGui.ImGuiPlatformIOPtr
type ImGuiPlatformImeDataPtr = Hexa.NET.ImGui.ImGuiPlatformImeDataPtr
type ImGuiPlatformMonitorPtr = Hexa.NET.ImGui.ImGuiPlatformMonitorPtr
type ImGuiPopupFlags = Hexa.NET.ImGui.ImGuiPopupFlags
type ImGuiSelectableFlags = Hexa.NET.ImGui.ImGuiSelectableFlags
type ImGuiSelectionBasicStoragePtr = Hexa.NET.ImGui.ImGuiSelectionBasicStoragePtr
type ImGuiSelectionExternalStoragePtr = Hexa.NET.ImGui.ImGuiSelectionExternalStoragePtr
type ImGuiSizeCallback = Hexa.NET.ImGui.ImGuiSizeCallback
type ImGuiSizeCallbackData = Hexa.NET.ImGui.ImGuiSizeCallbackData
type ImGuiSliderFlags = Hexa.NET.ImGui.ImGuiSliderFlags
type ImGuiStoragePairPtr = Hexa.NET.ImGui.ImGuiStoragePairPtr
type ImGuiStoragePtr = Hexa.NET.ImGui.ImGuiStoragePtr
type ImGuiStylePtr = Hexa.NET.ImGui.ImGuiStylePtr
type ImGuiStyleVar = Hexa.NET.ImGui.ImGuiStyleVar
type ImGuiTabBarFlags = Hexa.NET.ImGui.ImGuiTabBarFlags
type ImGuiTabItemFlags = Hexa.NET.ImGui.ImGuiTabItemFlags
type ImGuiTableBgTarget = Hexa.NET.ImGui.ImGuiTableBgTarget
type ImGuiTableColumnFlags = Hexa.NET.ImGui.ImGuiTableColumnFlags
type ImGuiTableColumnSortSpecsPtr = Hexa.NET.ImGui.ImGuiTableColumnSortSpecsPtr
type ImGuiTableFlags = Hexa.NET.ImGui.ImGuiTableFlags
type ImGuiTableRowFlags = Hexa.NET.ImGui.ImGuiTableRowFlags
type ImGuiTableSortSpecsPtr = Hexa.NET.ImGui.ImGuiTableSortSpecsPtr
type ImGuiTextBufferPtr = Hexa.NET.ImGui.ImGuiTextBufferPtr
type ImGuiTextFilterPtr = Hexa.NET.ImGui.ImGuiTextFilterPtr
type ImGuiTextRangePtr = Hexa.NET.ImGui.ImGuiTextRangePtr
type ImGuiTreeNodeFlags = Hexa.NET.ImGui.ImGuiTreeNodeFlags
type ImGuiViewport = Hexa.NET.ImGui.ImGuiViewport
type ImGuiViewportPtr = Hexa.NET.ImGui.ImGuiViewportPtr
type ImGuiWindowClassPtr = Hexa.NET.ImGui.ImGuiWindowClassPtr
type ImGuiWindowFlags = Hexa.NET.ImGui.ImGuiWindowFlags
type ImRect = Hexa.NET.ImGui.ImRect
type ImTextureDataPtr = Hexa.NET.ImGui.ImTextureDataPtr
type ImTextureFormat = Hexa.NET.ImGui.ImTextureFormat
type ImTextureID = Hexa.NET.ImGui.ImTextureID
type ImTextureRef = Hexa.NET.ImGui.ImTextureRef
type ImTextureRefPtr = Hexa.NET.ImGui.ImTextureRefPtr
type ImTextureStatus = Hexa.NET.ImGui.ImTextureStatus
type ImVector<'T when 'T : unmanaged and 'T : (new: unit -> 'T) and 'T : struct and 'T :> System.ValueType> = Hexa.NET.ImGui.ImVector<'T>

[<AutoOpen>]
module internal ConversionExtensions =
    open System.Numerics

    type Vector2 with
        static member FromV2f(v: V2f) = Vector2(v.X, v.Y)
        member v.ToV2f() = V2f(v.X, v.Y)

    type Vector3 with
        static member FromV3f(v: V3f) = Vector3(v.X, v.Y, v.Z)
        member v.ToV3f() = V3f(v.X, v.Y, v.Z)

        static member FromC3b(v: C3b) = Vector3.FromV3f(v.ToC3f().ToV3f())
        member v.ToC3b() = v.ToV3f().ToC3f().ToC3b()

        static member FromC3us(v: C3us) = Vector3.FromV3f(v.ToC3f().ToV3f())
        member v.ToC3us() = v.ToV3f().ToC3f().ToC3us()

        static member FromC3ui(v: C3ui) = Vector3.FromV3f(v.ToC3f().ToV3f())
        member v.ToC3ui() = v.ToV3f().ToC3f().ToC3ui()

        static member FromC3f(v: C3f) = Vector3.FromV3f(v.ToV3f())
        member v.ToC3f() = v.ToV3f().ToC3f()

        static member FromC3d(v: C3d) = Vector3.FromV3f(v.ToC3f().ToV3f())
        member v.ToC3d() = v.ToV3f().ToC3f().ToC3d()

    type Vector4 with
        static member FromV4f(v: V4f) = Vector4(v.X, v.Y, v.Z, v.W)
        member v.ToV4f() = V4f(v.X, v.Y, v.Z, v.W)

        static member FromC4b(v: C4b) = Vector4.FromV4f(v.ToC4f().ToV4f())
        member v.ToC4b() = v.ToV4f().ToC4f().ToC4b()

        static member FromC4us(v: C4us) = Vector4.FromV4f(v.ToC4f().ToV4f())
        member v.ToC4us() = v.ToV4f().ToC4f().ToC4us()

        static member FromC4ui(v: C4ui) = Vector4.FromV4f(v.ToC4f().ToV4f())
        member v.ToC4ui() = v.ToV4f().ToC4f().ToC4ui()

        static member FromC4f(v: C4f) = Vector4.FromV4f(v.ToV4f())
        member v.ToC4f() = v.ToV4f().ToC4f()

        static member FromC4d(v: C4d) = Vector4.FromV4f(v.ToC4f().ToV4f())
        member v.ToC4d() = v.ToV4f().ToC4f().ToC4d()

[<AbstractClass; Sealed>]
type ImGui =


    static member AcceptDragDropPayload(typ: nativeptr<uint8>, flags: ImGuiDragDropFlags) : ImGuiPayloadPtr =
        Hexa.NET.ImGui.ImGui.AcceptDragDropPayload(typ, flags)

    static member AcceptDragDropPayload(typ: nativeptr<uint8>) : ImGuiPayloadPtr =
        Hexa.NET.ImGui.ImGui.AcceptDragDropPayload(typ)


    static member AddBezierCubic(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, p4: V2f, col: uint32, thickness: float32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddBezierCubic(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), col, thickness, numSegments)

    static member AddBezierCubic(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, p4: V2f, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddBezierCubic(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), col, thickness)


    static member AddBezierQuadratic(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, col: uint32, thickness: float32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddBezierQuadratic(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), col, thickness, numSegments)

    static member AddBezierQuadratic(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddBezierQuadratic(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), col, thickness)


    static member AddCallback(self: ImDrawListPtr, callback: ImDrawCallback, userdata: voidptr, userdataSize: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.AddCallback(self, callback, userdata, userdataSize)

    static member AddCallback(self: ImDrawListPtr, callback: ImDrawCallback, userdata: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.AddCallback(self, callback, userdata)


    static member AddChar(self: ImFontGlyphRangesBuilderPtr, c: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddChar(self, c)


    static member AddCircle(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, numSegments: int32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddCircle(self, System.Numerics.Vector2.FromV2f(center), radius, col, numSegments, thickness)

    static member AddCircle(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddCircle(self, System.Numerics.Vector2.FromV2f(center), radius, col, numSegments)

    static member AddCircle(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddCircle(self, System.Numerics.Vector2.FromV2f(center), radius, col)

    static member AddCircle(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddCircle(self, System.Numerics.Vector2.FromV2f(center), radius, col, thickness)


    static member AddCircleFilled(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddCircleFilled(self, System.Numerics.Vector2.FromV2f(center), radius, col, numSegments)

    static member AddCircleFilled(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddCircleFilled(self, System.Numerics.Vector2.FromV2f(center), radius, col)


    static member AddConcavePolyFilled(self: ImDrawListPtr, points: V2f[], col: uint32) : unit =
        use pointsPinned = fixed points
        Hexa.NET.ImGui.ImGui.AddConcavePolyFilled(self, NativePtr.cast<_, System.Numerics.Vector2> pointsPinned, points.Length, col)


    static member AddConvexPolyFilled(self: ImDrawListPtr, points: V2f[], col: uint32) : unit =
        use pointsPinned = fixed points
        Hexa.NET.ImGui.ImGui.AddConvexPolyFilled(self, NativePtr.cast<_, System.Numerics.Vector2> pointsPinned, points.Length, col)


    static member AddCustomRect(self: ImFontAtlasPtr, width: int32, height: int32, outR: ImFontAtlasRectPtr) : int32 =
        Hexa.NET.ImGui.ImGui.AddCustomRect(self, width, height, outR)

    static member AddCustomRect(self: ImFontAtlasPtr, width: int32, height: int32) : int32 =
        Hexa.NET.ImGui.ImGui.AddCustomRect(self, width, height)


    static member AddDrawCmd(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.AddDrawCmd(self)


    static member AddDrawList(self: ImDrawDataPtr, drawList: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.AddDrawList(self, drawList)


    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, rot: float32, numSegments: int32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, rot, numSegments, thickness)

    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, rot: float32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, rot, numSegments)

    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, rot: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, rot)

    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col)

    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, numSegments)

    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, rot: float32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, rot, thickness)

    static member AddEllipse(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, numSegments: int32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipse(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, numSegments, thickness)


    static member AddEllipseFilled(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, rot: float32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipseFilled(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, rot, numSegments)

    static member AddEllipseFilled(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, rot: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipseFilled(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, rot)

    static member AddEllipseFilled(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipseFilled(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col)

    static member AddEllipseFilled(self: ImDrawListPtr, center: V2f, radius: V2f, col: uint32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddEllipseFilled(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), col, numSegments)


    static member AddFocusEvent(self: ImGuiIOPtr, focused: bool) : unit =
        Hexa.NET.ImGui.ImGui.AddFocusEvent(self, focused)


    static member AddFont(self: ImFontAtlasPtr, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFont(self, fontCfg)


    static member AddFontDefault(self: ImFontAtlasPtr, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontDefault(self, fontCfg)

    static member AddFontDefault(self: ImFontAtlasPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontDefault(self)


    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, sizePixels: float32, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, sizePixels, fontCfg, glyphRanges)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, sizePixels: float32, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, sizePixels, fontCfg)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, sizePixels: float32) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, sizePixels)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, fontCfg)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, sizePixels: float32, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, sizePixels, glyphRanges)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, glyphRanges)

    static member AddFontFromFileTTF(self: ImFontAtlasPtr, filename: string, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromFileTTF(self, filename, fontCfg, glyphRanges)


    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, sizePixels: float32, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, sizePixels, fontCfg, glyphRanges)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, sizePixels: float32, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, sizePixels, fontCfg)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, sizePixels: float32) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, sizePixels)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, fontCfg)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, sizePixels: float32, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, sizePixels, glyphRanges)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, glyphRanges)

    static member AddFontFromMemoryCompressedBase85TTF(self: ImFontAtlasPtr, compressedFontDatabase85: string, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedBase85TTF(self, compressedFontDatabase85, fontCfg, glyphRanges)


    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, sizePixels: float32, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, sizePixels, fontCfg, glyphRanges)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, sizePixels: float32, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, sizePixels, fontCfg)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, sizePixels: float32) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, sizePixels)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, fontCfg)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, sizePixels: float32, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, sizePixels, glyphRanges)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, glyphRanges)

    static member AddFontFromMemoryCompressedTTF(self: ImFontAtlasPtr, compressedFontData: voidptr, compressedFontDataSize: int32, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryCompressedTTF(self, compressedFontData, compressedFontDataSize, fontCfg, glyphRanges)


    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, sizePixels: float32, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, sizePixels, fontCfg, glyphRanges)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, sizePixels: float32, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, sizePixels, fontCfg)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, sizePixels: float32) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, sizePixels)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, fontCfg: ImFontConfigPtr) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, fontCfg)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, sizePixels: float32, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, sizePixels, glyphRanges)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, glyphRanges)

    static member AddFontFromMemoryTTF(self: ImFontAtlasPtr, fontData: voidptr, fontDataSize: int32, fontCfg: ImFontConfigPtr, glyphRanges: nativeptr<uint32>) : ImFontPtr =
        Hexa.NET.ImGui.ImGui.AddFontFromMemoryTTF(self, fontData, fontDataSize, fontCfg, glyphRanges)


    static member AddImage(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, uvMin: V2f, uvMax: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImage(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), System.Numerics.Vector2.FromV2f(uvMin), System.Numerics.Vector2.FromV2f(uvMax), col)

    static member AddImage(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, uvMin: V2f, uvMax: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImage(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), System.Numerics.Vector2.FromV2f(uvMin), System.Numerics.Vector2.FromV2f(uvMax))

    static member AddImage(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, uvMin: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImage(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), System.Numerics.Vector2.FromV2f(uvMin))

    static member AddImage(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImage(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax))

    static member AddImage(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, uvMin: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImage(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), System.Numerics.Vector2.FromV2f(uvMin), col)

    static member AddImage(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImage(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col)


    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, uv2: V2f, uv3: V2f, uv4: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector2.FromV2f(uv2), System.Numerics.Vector2.FromV2f(uv3), System.Numerics.Vector2.FromV2f(uv4), col)

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, uv2: V2f, uv3: V2f, uv4: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector2.FromV2f(uv2), System.Numerics.Vector2.FromV2f(uv3), System.Numerics.Vector2.FromV2f(uv4))

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, uv2: V2f, uv3: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector2.FromV2f(uv2), System.Numerics.Vector2.FromV2f(uv3))

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, uv2: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector2.FromV2f(uv2))

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1))

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4))

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, uv2: V2f, uv3: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector2.FromV2f(uv2), System.Numerics.Vector2.FromV2f(uv3), col)

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, uv2: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector2.FromV2f(uv2), col)

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, uv1: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), System.Numerics.Vector2.FromV2f(uv1), col)

    static member AddImageQuad(self: ImDrawListPtr, texRef: ImTextureRef, p1: V2f, p2: V2f, p3: V2f, p4: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddImageQuad(self, texRef, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), col)


    static member AddImageRounded(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, uvMin: V2f, uvMax: V2f, col: uint32, rounding: float32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.AddImageRounded(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), System.Numerics.Vector2.FromV2f(uvMin), System.Numerics.Vector2.FromV2f(uvMax), col, rounding, flags)

    static member AddImageRounded(self: ImDrawListPtr, texRef: ImTextureRef, pMin: V2f, pMax: V2f, uvMin: V2f, uvMax: V2f, col: uint32, rounding: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddImageRounded(self, texRef, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), System.Numerics.Vector2.FromV2f(uvMin), System.Numerics.Vector2.FromV2f(uvMax), col, rounding)


    static member AddInputCharacter(self: ImGuiIOPtr, c: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddInputCharacter(self, c)


    static member AddInputCharacterUTF16(self: ImGuiIOPtr, c: uint16) : unit =
        Hexa.NET.ImGui.ImGui.AddInputCharacterUTF16(self, c)


    static member AddInputCharactersUTF8(self: ImGuiIOPtr, str: string) : unit =
        Hexa.NET.ImGui.ImGui.AddInputCharactersUTF8(self, str)


    static member AddKeyAnalogEvent(self: ImGuiIOPtr, key: ImGuiKey, down: bool, v: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddKeyAnalogEvent(self, key, down, v)


    static member AddKeyEvent(self: ImGuiIOPtr, key: ImGuiKey, down: bool) : unit =
        Hexa.NET.ImGui.ImGui.AddKeyEvent(self, key, down)


    static member AddLine(self: ImDrawListPtr, p1: V2f, p2: V2f, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddLine(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), col, thickness)

    static member AddLine(self: ImDrawListPtr, p1: V2f, p2: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddLine(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), col)


    static member AddMouseButtonEvent(self: ImGuiIOPtr, button: int32, down: bool) : unit =
        Hexa.NET.ImGui.ImGui.AddMouseButtonEvent(self, button, down)


    static member AddMousePosEvent(self: ImGuiIOPtr, x: float32, y: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddMousePosEvent(self, x, y)


    static member AddMouseSourceEvent(self: ImGuiIOPtr, source: ImGuiMouseSource) : unit =
        Hexa.NET.ImGui.ImGui.AddMouseSourceEvent(self, source)


    static member AddMouseViewportEvent(self: ImGuiIOPtr, id: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddMouseViewportEvent(self, id)


    static member AddMouseWheelEvent(self: ImGuiIOPtr, wheelX: float32, wheelY: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddMouseWheelEvent(self, wheelX, wheelY)


    static member AddNgon(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, numSegments: int32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddNgon(self, System.Numerics.Vector2.FromV2f(center), radius, col, numSegments, thickness)

    static member AddNgon(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddNgon(self, System.Numerics.Vector2.FromV2f(center), radius, col, numSegments)


    static member AddNgonFilled(self: ImDrawListPtr, center: V2f, radius: float32, col: uint32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.AddNgonFilled(self, System.Numerics.Vector2.FromV2f(center), radius, col, numSegments)


    static member AddPolyline(self: ImDrawListPtr, points: V2f[], col: uint32, flags: ImDrawFlags, thickness: float32) : unit =
        use pointsPinned = fixed points
        Hexa.NET.ImGui.ImGui.AddPolyline(self, NativePtr.cast<_, System.Numerics.Vector2> pointsPinned, points.Length, col, flags, thickness)


    static member AddQuad(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, p4: V2f, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddQuad(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), col, thickness)

    static member AddQuad(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, p4: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddQuad(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), col)


    static member AddQuadFilled(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, p4: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddQuadFilled(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), col)


    static member AddRanges(self: ImFontGlyphRangesBuilderPtr, ranges: nativeptr<uint32>) : unit =
        Hexa.NET.ImGui.ImGui.AddRanges(self, ranges)


    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, rounding: float32, flags: ImDrawFlags, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, rounding, flags, thickness)

    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, rounding: float32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, rounding, flags)

    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, rounding: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, rounding)

    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col)

    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, flags)

    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, rounding: float32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, rounding, thickness)

    static member AddRect(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, flags: ImDrawFlags, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddRect(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, flags, thickness)


    static member AddRectFilled(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, rounding: float32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.AddRectFilled(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, rounding, flags)

    static member AddRectFilled(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, rounding: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddRectFilled(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, rounding)

    static member AddRectFilled(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddRectFilled(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col)

    static member AddRectFilled(self: ImDrawListPtr, pMin: V2f, pMax: V2f, col: uint32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.AddRectFilled(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), col, flags)


    static member AddRectFilledMultiColor(self: ImDrawListPtr, pMin: V2f, pMax: V2f, colUprLeft: uint32, colUprRight: uint32, colBotRight: uint32, colBotLeft: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddRectFilledMultiColor(self, System.Numerics.Vector2.FromV2f(pMin), System.Numerics.Vector2.FromV2f(pMax), colUprLeft, colUprRight, colBotRight, colBotLeft)


    static member AddRemapChar(self: ImFontPtr, fromCodepoint: uint32, toCodepoint: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddRemapChar(self, fromCodepoint, toCodepoint)


    static member AddText(self: ImDrawListPtr, pos: V2f, col: uint32, textBegin: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, System.Numerics.Vector2.FromV2f(pos), col, textBegin, textEnd)

    static member AddText(self: ImDrawListPtr, pos: V2f, col: uint32, textBegin: string) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, System.Numerics.Vector2.FromV2f(pos), col, textBegin)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, textEnd: string, wrapWidth: float32, cpuFineClipRect: byref<V4f>) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, textEnd, wrapWidth, NativePtr.cast<_, System.Numerics.Vector4> &&cpuFineClipRect)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, textEnd: string, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, textEnd, wrapWidth)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, textEnd)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, wrapWidth)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, textEnd: string, cpuFineClipRect: byref<V4f>) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, textEnd, NativePtr.cast<_, System.Numerics.Vector4> &&cpuFineClipRect)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, cpuFineClipRect: byref<V4f>) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, NativePtr.cast<_, System.Numerics.Vector4> &&cpuFineClipRect)

    static member AddText(self: ImDrawListPtr, font: ImFontPtr, fontSize: float32, pos: V2f, col: uint32, textBegin: string, wrapWidth: float32, cpuFineClipRect: byref<V4f>) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, font, fontSize, System.Numerics.Vector2.FromV2f(pos), col, textBegin, wrapWidth, NativePtr.cast<_, System.Numerics.Vector4> &&cpuFineClipRect)

    static member AddText(self: ImFontGlyphRangesBuilderPtr, text: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, text, textEnd)

    static member AddText(self: ImFontGlyphRangesBuilderPtr, text: string) : unit =
        Hexa.NET.ImGui.ImGui.AddText(self, text)


    static member AddTriangle(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.AddTriangle(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), col, thickness)

    static member AddTriangle(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddTriangle(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), col)


    static member AddTriangleFilled(self: ImDrawListPtr, p1: V2f, p2: V2f, p3: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.AddTriangleFilled(self, System.Numerics.Vector2.FromV2f(p1), System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), col)


    static member AlignTextToFramePadding() : unit =
        Hexa.NET.ImGui.ImGui.AlignTextToFramePadding()


    static member ApplyRequests(self: ImGuiSelectionBasicStoragePtr, msIo: ImGuiMultiSelectIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.ApplyRequests(self, msIo)

    static member ApplyRequests(self: ImGuiSelectionExternalStoragePtr, msIo: ImGuiMultiSelectIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.ApplyRequests(self, msIo)


    static member ArrowButton(strId: string, dir: ImGuiDir) : bool =
        Hexa.NET.ImGui.ImGui.ArrowButton(strId, dir)


    static member Begin(name: string, pOpen: byref<bool>, flags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.Begin(name, &&pOpen, flags)

    static member Begin(name: string, pOpen: cval<bool>, flags: ImGuiWindowFlags) : unit =
        let mutable pOpenState = pOpen.Value
        let result = Hexa.NET.ImGui.ImGui.Begin(name, &&pOpenState, flags)
        if result then
            pOpen.Value <- pOpenState

    static member Begin(name: string, pOpen: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.Begin(name, &&pOpen)

    static member Begin(name: string, pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        let result = Hexa.NET.ImGui.ImGui.Begin(name, &&pOpenState)
        if result then
            pOpen.Value <- pOpenState

    static member Begin(name: string) : bool =
        Hexa.NET.ImGui.ImGui.Begin(name)

    static member Begin(name: string, flags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.Begin(name, flags)

    static member Begin(self: ImGuiListClipperPtr, itemsCount: int32, itemsHeight: float32) : unit =
        Hexa.NET.ImGui.ImGui.Begin(self, itemsCount, itemsHeight)

    static member Begin(self: ImGuiListClipperPtr, itemsCount: int32) : unit =
        Hexa.NET.ImGui.ImGui.Begin(self, itemsCount)


    static member BeginChild(strId: string, size: V2f, childFlags: ImGuiChildFlags, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, System.Numerics.Vector2.FromV2f(size), childFlags, windowFlags)

    static member BeginChild(strId: string, size: V2f, childFlags: ImGuiChildFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, System.Numerics.Vector2.FromV2f(size), childFlags)

    static member BeginChild(strId: string, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, System.Numerics.Vector2.FromV2f(size))

    static member BeginChild(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId)

    static member BeginChild(strId: string, childFlags: ImGuiChildFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, childFlags)

    static member BeginChild(strId: string, size: V2f, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, System.Numerics.Vector2.FromV2f(size), windowFlags)

    static member BeginChild(strId: string, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, windowFlags)

    static member BeginChild(strId: string, childFlags: ImGuiChildFlags, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(strId, childFlags, windowFlags)

    static member BeginChild(id: uint32, size: V2f, childFlags: ImGuiChildFlags, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, System.Numerics.Vector2.FromV2f(size), childFlags, windowFlags)

    static member BeginChild(id: uint32, size: V2f, childFlags: ImGuiChildFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, System.Numerics.Vector2.FromV2f(size), childFlags)

    static member BeginChild(id: uint32, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, System.Numerics.Vector2.FromV2f(size))

    static member BeginChild(id: uint32) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id)

    static member BeginChild(id: uint32, childFlags: ImGuiChildFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, childFlags)

    static member BeginChild(id: uint32, size: V2f, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, System.Numerics.Vector2.FromV2f(size), windowFlags)

    static member BeginChild(id: uint32, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, windowFlags)

    static member BeginChild(id: uint32, childFlags: ImGuiChildFlags, windowFlags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginChild(id, childFlags, windowFlags)


    static member BeginCombo(label: string, previewValue: string, flags: ImGuiComboFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginCombo(label, previewValue, flags)

    static member BeginCombo(label: string, previewValue: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginCombo(label, previewValue)


    static member BeginDisabled(disabled: bool) : unit =
        Hexa.NET.ImGui.ImGui.BeginDisabled(disabled)

    static member BeginDisabled() : unit =
        Hexa.NET.ImGui.ImGui.BeginDisabled()


    static member BeginDragDropSource(flags: ImGuiDragDropFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginDragDropSource(flags)

    static member BeginDragDropSource() : bool =
        Hexa.NET.ImGui.ImGui.BeginDragDropSource()


    static member BeginDragDropTarget() : bool =
        Hexa.NET.ImGui.ImGui.BeginDragDropTarget()


    static member BeginGroup() : unit =
        Hexa.NET.ImGui.ImGui.BeginGroup()


    static member BeginItemTooltip() : bool =
        Hexa.NET.ImGui.ImGui.BeginItemTooltip()


    static member BeginListBox(label: string, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.BeginListBox(label, System.Numerics.Vector2.FromV2f(size))

    static member BeginListBox(label: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginListBox(label)


    static member BeginMainMenuBar() : bool =
        Hexa.NET.ImGui.ImGui.BeginMainMenuBar()


    static member BeginMenu(label: string, enabled: bool) : bool =
        Hexa.NET.ImGui.ImGui.BeginMenu(label, enabled)

    static member BeginMenu(label: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginMenu(label)


    static member BeginMenuBar() : bool =
        Hexa.NET.ImGui.ImGui.BeginMenuBar()


    static member BeginMultiSelect(flags: ImGuiMultiSelectFlags, selectionSize: int32, itemsCount: int32) : ImGuiMultiSelectIOPtr =
        Hexa.NET.ImGui.ImGui.BeginMultiSelect(flags, selectionSize, itemsCount)

    static member BeginMultiSelect(flags: ImGuiMultiSelectFlags, selectionSize: int32) : ImGuiMultiSelectIOPtr =
        Hexa.NET.ImGui.ImGui.BeginMultiSelect(flags, selectionSize)

    static member BeginMultiSelect(flags: ImGuiMultiSelectFlags) : ImGuiMultiSelectIOPtr =
        Hexa.NET.ImGui.ImGui.BeginMultiSelect(flags)


    static member BeginPopup(strId: string, flags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopup(strId, flags)

    static member BeginPopup(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopup(strId)


    static member BeginPopupContextItem(strId: string, popupFlags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextItem(strId, popupFlags)

    static member BeginPopupContextItem(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextItem(strId)

    static member BeginPopupContextItem() : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextItem()

    static member BeginPopupContextItem(popupFlags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextItem(popupFlags)


    static member BeginPopupContextVoid(strId: string, popupFlags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextVoid(strId, popupFlags)

    static member BeginPopupContextVoid(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextVoid(strId)

    static member BeginPopupContextVoid() : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextVoid()

    static member BeginPopupContextVoid(popupFlags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextVoid(popupFlags)


    static member BeginPopupContextWindow(strId: string, popupFlags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextWindow(strId, popupFlags)

    static member BeginPopupContextWindow(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextWindow(strId)

    static member BeginPopupContextWindow() : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextWindow()

    static member BeginPopupContextWindow(popupFlags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupContextWindow(popupFlags)


    static member BeginPopupModal(name: string, pOpen: byref<bool>, flags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupModal(name, &&pOpen, flags)

    static member BeginPopupModal(name: string, pOpen: cval<bool>, flags: ImGuiWindowFlags) : unit =
        let mutable pOpenState = pOpen.Value
        let result = Hexa.NET.ImGui.ImGui.BeginPopupModal(name, &&pOpenState, flags)
        if result then
            pOpen.Value <- pOpenState

    static member BeginPopupModal(name: string, pOpen: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupModal(name, &&pOpen)

    static member BeginPopupModal(name: string, pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        let result = Hexa.NET.ImGui.ImGui.BeginPopupModal(name, &&pOpenState)
        if result then
            pOpen.Value <- pOpenState

    static member BeginPopupModal(name: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupModal(name)

    static member BeginPopupModal(name: string, flags: ImGuiWindowFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginPopupModal(name, flags)


    static member BeginTabBar(strId: string, flags: ImGuiTabBarFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginTabBar(strId, flags)

    static member BeginTabBar(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginTabBar(strId)


    static member BeginTabItem(label: string, pOpen: byref<bool>, flags: ImGuiTabItemFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginTabItem(label, &&pOpen, flags)

    static member BeginTabItem(label: string, pOpen: cval<bool>, flags: ImGuiTabItemFlags) : unit =
        let mutable pOpenState = pOpen.Value
        let result = Hexa.NET.ImGui.ImGui.BeginTabItem(label, &&pOpenState, flags)
        if result then
            pOpen.Value <- pOpenState

    static member BeginTabItem(label: string, pOpen: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.BeginTabItem(label, &&pOpen)

    static member BeginTabItem(label: string, pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        let result = Hexa.NET.ImGui.ImGui.BeginTabItem(label, &&pOpenState)
        if result then
            pOpen.Value <- pOpenState

    static member BeginTabItem(label: string) : bool =
        Hexa.NET.ImGui.ImGui.BeginTabItem(label)

    static member BeginTabItem(label: string, flags: ImGuiTabItemFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginTabItem(label, flags)


    static member BeginTable(strId: string, columns: int32, flags: ImGuiTableFlags, outerSize: V2f, innerWidth: float32) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, flags, System.Numerics.Vector2.FromV2f(outerSize), innerWidth)

    static member BeginTable(strId: string, columns: int32, flags: ImGuiTableFlags, outerSize: V2f) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, flags, System.Numerics.Vector2.FromV2f(outerSize))

    static member BeginTable(strId: string, columns: int32, flags: ImGuiTableFlags) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, flags)

    static member BeginTable(strId: string, columns: int32) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns)

    static member BeginTable(strId: string, columns: int32, outerSize: V2f) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, System.Numerics.Vector2.FromV2f(outerSize))

    static member BeginTable(strId: string, columns: int32, flags: ImGuiTableFlags, innerWidth: float32) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, flags, innerWidth)

    static member BeginTable(strId: string, columns: int32, innerWidth: float32) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, innerWidth)

    static member BeginTable(strId: string, columns: int32, outerSize: V2f, innerWidth: float32) : bool =
        Hexa.NET.ImGui.ImGui.BeginTable(strId, columns, System.Numerics.Vector2.FromV2f(outerSize), innerWidth)


    static member BeginTooltip() : bool =
        Hexa.NET.ImGui.ImGui.BeginTooltip()


    static member Build(self: ImGuiTextFilterPtr) : unit =
        Hexa.NET.ImGui.ImGui.Build(self)


    static member BuildRanges(self: ImFontGlyphRangesBuilderPtr, outRanges: byref<ImVector<uint32>>) : unit =
        Hexa.NET.ImGui.ImGui.BuildRanges(self, &&outRanges)


    static member BuildSortByKey(self: ImGuiStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.BuildSortByKey(self)


    static member Bullet() : unit =
        Hexa.NET.ImGui.ImGui.Bullet()


    static member BulletText(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.BulletText(fmt)


    static member BulletTextV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.BulletTextV(fmt, args)


    static member Button(label: string, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Button(label, System.Numerics.Vector2.FromV2f(size))

    static member Button(label: string) : bool =
        Hexa.NET.ImGui.ImGui.Button(label)


    static member CalcItemWidth() : float32 =
        Hexa.NET.ImGui.ImGui.CalcItemWidth()


    static member CalcTextSize(text: string) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text)
        result.ToV2f()

    static member CalcTextSize(text: string, textEnd: string) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, textEnd)
        result.ToV2f()

    static member CalcTextSize(pOut: byref<V2f>, text: string) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text)

    static member CalcTextSize(text: string, hideTextAfterDoubleHash: bool) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, hideTextAfterDoubleHash)
        result.ToV2f()

    static member CalcTextSize(text: string, textEnd: string, hideTextAfterDoubleHash: bool) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, textEnd, hideTextAfterDoubleHash)
        result.ToV2f()

    static member CalcTextSize(pOut: byref<V2f>, text: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, textEnd)

    static member CalcTextSize(text: string, wrapWidth: float32) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, wrapWidth)
        result.ToV2f()

    static member CalcTextSize(text: string, textEnd: string, wrapWidth: float32) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, textEnd, wrapWidth)
        result.ToV2f()

    static member CalcTextSize(pOut: byref<V2f>, text: string, hideTextAfterDoubleHash: bool) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, hideTextAfterDoubleHash)

    static member CalcTextSize(pOut: byref<V2f>, text: string, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, wrapWidth)

    static member CalcTextSize(text: string, hideTextAfterDoubleHash: bool, wrapWidth: float32) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, hideTextAfterDoubleHash, wrapWidth)
        result.ToV2f()

    static member CalcTextSize(text: string, textEnd: string, hideTextAfterDoubleHash: bool, wrapWidth: float32) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSize(text, textEnd, hideTextAfterDoubleHash, wrapWidth)
        result.ToV2f()

    static member CalcTextSize(pOut: byref<V2f>, text: string, textEnd: string, hideTextAfterDoubleHash: bool, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, textEnd, hideTextAfterDoubleHash, wrapWidth)

    static member CalcTextSize(pOut: byref<V2f>, text: string, textEnd: string, hideTextAfterDoubleHash: bool) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, textEnd, hideTextAfterDoubleHash)

    static member CalcTextSize(pOut: byref<V2f>, text: string, textEnd: string, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, textEnd, wrapWidth)

    static member CalcTextSize(pOut: byref<V2f>, text: string, hideTextAfterDoubleHash: bool, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, text, hideTextAfterDoubleHash, wrapWidth)


    static member CalcTextSizeA(self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSizeA(self, size, maxWidth, wrapWidth, textBegin)
        result.ToV2f()

    static member CalcTextSizeA(self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string, textEnd: string) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSizeA(self, size, maxWidth, wrapWidth, textBegin, textEnd)
        result.ToV2f()

    static member CalcTextSizeA(pOut: byref<V2f>, self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSizeA(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self, size, maxWidth, wrapWidth, textBegin)

    static member CalcTextSizeA(self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string, remaining: byref<nativeptr<uint8>>) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSizeA(self, size, maxWidth, wrapWidth, textBegin, &&remaining)
        result.ToV2f()

    static member CalcTextSizeA(self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string, textEnd: string, remaining: byref<nativeptr<uint8>>) : V2f =
        let result = Hexa.NET.ImGui.ImGui.CalcTextSizeA(self, size, maxWidth, wrapWidth, textBegin, textEnd, &&remaining)
        result.ToV2f()

    static member CalcTextSizeA(pOut: byref<V2f>, self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string, textEnd: string, remaining: byref<nativeptr<uint8>>) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSizeA(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self, size, maxWidth, wrapWidth, textBegin, textEnd, &&remaining)

    static member CalcTextSizeA(pOut: byref<V2f>, self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSizeA(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self, size, maxWidth, wrapWidth, textBegin, textEnd)

    static member CalcTextSizeA(pOut: byref<V2f>, self: ImFontPtr, size: float32, maxWidth: float32, wrapWidth: float32, textBegin: string, remaining: byref<nativeptr<uint8>>) : unit =
        Hexa.NET.ImGui.ImGui.CalcTextSizeA(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self, size, maxWidth, wrapWidth, textBegin, &&remaining)


    static member CalcWordWrapPosition(self: ImFontPtr, size: float32, text: string, textEnd: string, wrapWidth: float32) : string =
        Hexa.NET.ImGui.ImGui.CalcWordWrapPositionS(self, size, text, textEnd, wrapWidth)


    static member ChannelsMerge(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.ChannelsMerge(self)


    static member ChannelsSetCurrent(self: ImDrawListPtr, n: int32) : unit =
        Hexa.NET.ImGui.ImGui.ChannelsSetCurrent(self, n)


    static member ChannelsSplit(self: ImDrawListPtr, count: int32) : unit =
        Hexa.NET.ImGui.ImGui.ChannelsSplit(self, count)


    static member Checkbox(label: string, v: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.Checkbox(label, &&v)

    static member Checkbox(label: string, v: cval<bool>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.Checkbox(label, &&vState)
        if result then
            v.Value <- vState


    static member CheckboxFlags(label: string, flags: byref<int32>, flagsValue: int32) : bool =
        Hexa.NET.ImGui.ImGui.CheckboxFlags(label, &&flags, flagsValue)

    static member CheckboxFlags(label: string, flags: cval<int32>, flagsValue: int32) : unit =
        let mutable flagsState = flags.Value
        let result = Hexa.NET.ImGui.ImGui.CheckboxFlags(label, &&flagsState, flagsValue)
        if result then
            flags.Value <- flagsState

    static member CheckboxFlags(label: string, flags: byref<uint32>, flagsValue: uint32) : bool =
        Hexa.NET.ImGui.ImGui.CheckboxFlags(label, &&flags, flagsValue)

    static member CheckboxFlags(label: string, flags: cval<uint32>, flagsValue: uint32) : unit =
        let mutable flagsState = flags.Value
        let result = Hexa.NET.ImGui.ImGui.CheckboxFlags(label, &&flagsState, flagsValue)
        if result then
            flags.Value <- flagsState


    static member Clear(self: ImGuiPayloadPtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImGuiTextFilterPtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImGuiStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImGuiSelectionBasicStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImDrawListSplitterPtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImDrawDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImFontGlyphRangesBuilderPtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)

    static member Clear(self: ImFontAtlasPtr) : unit =
        Hexa.NET.ImGui.ImGui.Clear(self)


    static member ClearEventsQueue(self: ImGuiIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearEventsQueue(self)


    static member ClearFonts(self: ImFontAtlasPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearFonts(self)


    static member ClearFreeMemory(self: ImDrawListSplitterPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearFreeMemory(self)


    static member ClearInputData(self: ImFontAtlasPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearInputData(self)


    static member ClearInputKeys(self: ImGuiIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearInputKeys(self)


    static member ClearInputMouse(self: ImGuiIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearInputMouse(self)


    static member ClearOutputData(self: ImFontBakedPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearOutputData(self)

    static member ClearOutputData(self: ImFontPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearOutputData(self)


    static member ClearSelection(self: ImGuiInputTextCallbackDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearSelection(self)


    static member ClearTexData(self: ImFontAtlasPtr) : unit =
        Hexa.NET.ImGui.ImGui.ClearTexData(self)


    static member CloneOutput(self: ImDrawListPtr) : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.CloneOutput(self)


    static member CloseCurrentPopup() : unit =
        Hexa.NET.ImGui.ImGui.CloseCurrentPopup()


    static member CollapsingHeader(label: string, flags: ImGuiTreeNodeFlags) : bool =
        Hexa.NET.ImGui.ImGui.CollapsingHeader(label, flags)

    static member CollapsingHeader(label: string) : bool =
        Hexa.NET.ImGui.ImGui.CollapsingHeader(label)

    static member CollapsingHeader(label: string, pVisible: byref<bool>, flags: ImGuiTreeNodeFlags) : bool =
        Hexa.NET.ImGui.ImGui.CollapsingHeader(label, &&pVisible, flags)

    static member CollapsingHeader(label: string, pVisible: cval<bool>, flags: ImGuiTreeNodeFlags) : unit =
        let mutable pVisibleState = pVisible.Value
        let result = Hexa.NET.ImGui.ImGui.CollapsingHeader(label, &&pVisibleState, flags)
        if result then
            pVisible.Value <- pVisibleState

    static member CollapsingHeader(label: string, pVisible: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.CollapsingHeader(label, &&pVisible)

    static member CollapsingHeader(label: string, pVisible: cval<bool>) : unit =
        let mutable pVisibleState = pVisible.Value
        let result = Hexa.NET.ImGui.ImGui.CollapsingHeader(label, &&pVisibleState)
        if result then
            pVisible.Value <- pVisibleState


    static member ColorButton(descId: string, col: V4f, flags: ImGuiColorEditFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromV4f(col), flags, System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4b, flags: ImGuiColorEditFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4b(col), flags, System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4us, flags: ImGuiColorEditFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4us(col), flags, System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4ui, flags: ImGuiColorEditFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4ui(col), flags, System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4f, flags: ImGuiColorEditFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4f(col), flags, System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4d, flags: ImGuiColorEditFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4d(col), flags, System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: V4f, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromV4f(col), flags)

    static member ColorButton(descId: string, col: C4b, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4b(col), flags)

    static member ColorButton(descId: string, col: C4us, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4us(col), flags)

    static member ColorButton(descId: string, col: C4ui, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4ui(col), flags)

    static member ColorButton(descId: string, col: C4f, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4f(col), flags)

    static member ColorButton(descId: string, col: C4d, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4d(col), flags)

    static member ColorButton(descId: string, col: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromV4f(col))

    static member ColorButton(descId: string, col: C4b) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4b(col))

    static member ColorButton(descId: string, col: C4us) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4us(col))

    static member ColorButton(descId: string, col: C4ui) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4ui(col))

    static member ColorButton(descId: string, col: C4f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4f(col))

    static member ColorButton(descId: string, col: C4d) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4d(col))

    static member ColorButton(descId: string, col: V4f, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromV4f(col), System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4b, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4b(col), System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4us, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4us(col), System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4ui, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4ui(col), System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4f, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4f(col), System.Numerics.Vector2.FromV2f(size))

    static member ColorButton(descId: string, col: C4d, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ColorButton(descId, System.Numerics.Vector4.FromC4d(col), System.Numerics.Vector2.FromV2f(size))


    static member ColorConvertFloat4ToU32(input: V4f) : uint32 =
        Hexa.NET.ImGui.ImGui.ColorConvertFloat4ToU32(System.Numerics.Vector4.FromV4f(input))


    static member ColorConvertHSVtoRGB(h: float32, s: float32, v: float32, outR: byref<float32>, outG: byref<float32>, outB: byref<float32>) : unit =
        Hexa.NET.ImGui.ImGui.ColorConvertHSVtoRGB(h, s, v, &&outR, &&outG, &&outB)


    static member ColorConvertRGBtoHSV(r: float32, g: float32, b: float32, outH: byref<float32>, outS: byref<float32>, outV: byref<float32>) : unit =
        Hexa.NET.ImGui.ImGui.ColorConvertRGBtoHSV(r, g, b, &&outH, &&outS, &&outV)


    static member ColorConvertU32ToFloat4(input: uint32) : V4f =
        let result = Hexa.NET.ImGui.ImGui.ColorConvertU32ToFloat4(input)
        result.ToV4f()

    static member ColorConvertU32ToFloat4(pOut: byref<V4f>, input: uint32) : unit =
        Hexa.NET.ImGui.ImGui.ColorConvertU32ToFloat4(NativePtr.cast<_, System.Numerics.Vector4> &&pOut, input)


    static member ColorEdit3(label: string, col: byref<V3f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorEdit3(label: string, col: cval<V3f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorEdit3(label: string, col: byref<C3b>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3b()
        result

    static member ColorEdit3(label: string, col: cval<C3b>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3b()

    static member ColorEdit3(label: string, col: byref<C3us>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3us()
        result

    static member ColorEdit3(label: string, col: cval<C3us>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3us()

    static member ColorEdit3(label: string, col: byref<C3ui>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3ui()
        result

    static member ColorEdit3(label: string, col: cval<C3ui>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3ui()

    static member ColorEdit3(label: string, col: byref<C3f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorEdit3(label: string, col: cval<C3f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorEdit3(label: string, col: byref<C3d>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3d()
        result

    static member ColorEdit3(label: string, col: cval<C3d>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3d()

    static member ColorEdit3(label: string, col: byref<V3f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col)

    static member ColorEdit3(label: string, col: cval<V3f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorEdit3(label: string, col: byref<C3b>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3b()
        result

    static member ColorEdit3(label: string, col: cval<C3b>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3b()

    static member ColorEdit3(label: string, col: byref<C3us>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3us()
        result

    static member ColorEdit3(label: string, col: cval<C3us>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3us()

    static member ColorEdit3(label: string, col: byref<C3ui>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3ui()
        result

    static member ColorEdit3(label: string, col: cval<C3ui>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3ui()

    static member ColorEdit3(label: string, col: byref<C3f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col)

    static member ColorEdit3(label: string, col: cval<C3f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorEdit3(label: string, col: byref<C3d>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3d()
        result

    static member ColorEdit3(label: string, col: cval<C3d>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3d()


    static member ColorEdit4(label: string, col: byref<V4f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorEdit4(label: string, col: cval<V4f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorEdit4(label: string, col: byref<C4b>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4b()
        result

    static member ColorEdit4(label: string, col: cval<C4b>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4b()

    static member ColorEdit4(label: string, col: byref<C4us>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4us()
        result

    static member ColorEdit4(label: string, col: cval<C4us>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4us()

    static member ColorEdit4(label: string, col: byref<C4ui>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4ui()
        result

    static member ColorEdit4(label: string, col: cval<C4ui>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4ui()

    static member ColorEdit4(label: string, col: byref<C4f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorEdit4(label: string, col: cval<C4f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorEdit4(label: string, col: byref<C4d>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4d()
        result

    static member ColorEdit4(label: string, col: cval<C4d>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4d()

    static member ColorEdit4(label: string, col: byref<V4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col)

    static member ColorEdit4(label: string, col: cval<V4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorEdit4(label: string, col: byref<C4b>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4b()
        result

    static member ColorEdit4(label: string, col: cval<C4b>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4b()

    static member ColorEdit4(label: string, col: byref<C4us>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4us()
        result

    static member ColorEdit4(label: string, col: cval<C4us>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4us()

    static member ColorEdit4(label: string, col: byref<C4ui>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4ui()
        result

    static member ColorEdit4(label: string, col: cval<C4ui>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4ui()

    static member ColorEdit4(label: string, col: byref<C4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col)

    static member ColorEdit4(label: string, col: cval<C4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorEdit4(label: string, col: byref<C4d>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4d()
        result

    static member ColorEdit4(label: string, col: cval<C4d>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorEdit4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4d()


    static member ColorPicker3(label: string, col: byref<V3f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorPicker3(label: string, col: cval<V3f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorPicker3(label: string, col: byref<C3b>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3b()
        result

    static member ColorPicker3(label: string, col: cval<C3b>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3b()

    static member ColorPicker3(label: string, col: byref<C3us>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3us()
        result

    static member ColorPicker3(label: string, col: cval<C3us>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3us()

    static member ColorPicker3(label: string, col: byref<C3ui>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3ui()
        result

    static member ColorPicker3(label: string, col: cval<C3ui>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3ui()

    static member ColorPicker3(label: string, col: byref<C3f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorPicker3(label: string, col: cval<C3f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorPicker3(label: string, col: byref<C3d>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC3d()
        result

    static member ColorPicker3(label: string, col: cval<C3d>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC3d()

    static member ColorPicker3(label: string, col: byref<V3f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col)

    static member ColorPicker3(label: string, col: cval<V3f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorPicker3(label: string, col: byref<C3b>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3b()
        result

    static member ColorPicker3(label: string, col: cval<C3b>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3b()

    static member ColorPicker3(label: string, col: byref<C3us>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3us()
        result

    static member ColorPicker3(label: string, col: cval<C3us>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3us()

    static member ColorPicker3(label: string, col: byref<C3ui>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3ui()
        result

    static member ColorPicker3(label: string, col: cval<C3ui>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3ui()

    static member ColorPicker3(label: string, col: byref<C3f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col)

    static member ColorPicker3(label: string, col: cval<C3f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorPicker3(label: string, col: byref<C3d>) : bool =
        let mutable colState = col.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC3d()
        result

    static member ColorPicker3(label: string, col: cval<C3d>) : unit =
        let mutable colState = col.Value.ToC3f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker3(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC3d()


    static member ColorPicker4(label: string, col: byref<V4f>, flags: ImGuiColorEditFlags, refCol: byref<V4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags, NativePtr.cast<_, float32> &&refCol)

    static member ColorPicker4(label: string, col: cval<V4f>, flags: ImGuiColorEditFlags, refCol: byref<V4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4b>, flags: ImGuiColorEditFlags, refCol: byref<C4b>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4b()
            refCol <- refColState.ToC4b()
        result

    static member ColorPicker4(label: string, col: cval<C4b>, flags: ImGuiColorEditFlags, refCol: byref<C4b>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4b()
            refCol <- refColState.ToC4b()

    static member ColorPicker4(label: string, col: byref<C4us>, flags: ImGuiColorEditFlags, refCol: byref<C4us>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4us()
            refCol <- refColState.ToC4us()
        result

    static member ColorPicker4(label: string, col: cval<C4us>, flags: ImGuiColorEditFlags, refCol: byref<C4us>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4us()
            refCol <- refColState.ToC4us()

    static member ColorPicker4(label: string, col: byref<C4ui>, flags: ImGuiColorEditFlags, refCol: byref<C4ui>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4ui()
            refCol <- refColState.ToC4ui()
        result

    static member ColorPicker4(label: string, col: cval<C4ui>, flags: ImGuiColorEditFlags, refCol: byref<C4ui>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4ui()
            refCol <- refColState.ToC4ui()

    static member ColorPicker4(label: string, col: byref<C4f>, flags: ImGuiColorEditFlags, refCol: byref<C4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags, NativePtr.cast<_, float32> &&refCol)

    static member ColorPicker4(label: string, col: cval<C4f>, flags: ImGuiColorEditFlags, refCol: byref<C4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4d>, flags: ImGuiColorEditFlags, refCol: byref<C4d>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4d()
            refCol <- refColState.ToC4d()
        result

    static member ColorPicker4(label: string, col: cval<C4d>, flags: ImGuiColorEditFlags, refCol: byref<C4d>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4d()
            refCol <- refColState.ToC4d()

    static member ColorPicker4(label: string, col: byref<V4f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorPicker4(label: string, col: cval<V4f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4b>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4b()
        result

    static member ColorPicker4(label: string, col: cval<C4b>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4b()

    static member ColorPicker4(label: string, col: byref<C4us>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4us()
        result

    static member ColorPicker4(label: string, col: cval<C4us>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4us()

    static member ColorPicker4(label: string, col: byref<C4ui>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4ui()
        result

    static member ColorPicker4(label: string, col: cval<C4ui>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4ui()

    static member ColorPicker4(label: string, col: byref<C4f>, flags: ImGuiColorEditFlags) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags)

    static member ColorPicker4(label: string, col: cval<C4f>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4d>, flags: ImGuiColorEditFlags) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, flags)
        if result then
            col <- colState.ToC4d()
        result

    static member ColorPicker4(label: string, col: cval<C4d>, flags: ImGuiColorEditFlags) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, flags)
        if result then
            col.Value <- colState.ToC4d()

    static member ColorPicker4(label: string, col: byref<V4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col)

    static member ColorPicker4(label: string, col: cval<V4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4b>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4b()
        result

    static member ColorPicker4(label: string, col: cval<C4b>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4b()

    static member ColorPicker4(label: string, col: byref<C4us>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4us()
        result

    static member ColorPicker4(label: string, col: cval<C4us>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4us()

    static member ColorPicker4(label: string, col: byref<C4ui>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4ui()
        result

    static member ColorPicker4(label: string, col: cval<C4ui>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4ui()

    static member ColorPicker4(label: string, col: byref<C4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col)

    static member ColorPicker4(label: string, col: cval<C4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4d>) : bool =
        let mutable colState = col.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col)
        if result then
            col <- colState.ToC4d()
        result

    static member ColorPicker4(label: string, col: cval<C4d>) : unit =
        let mutable colState = col.Value.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState)
        if result then
            col.Value <- colState.ToC4d()

    static member ColorPicker4(label: string, col: byref<V4f>, refCol: byref<V4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, NativePtr.cast<_, float32> &&refCol)

    static member ColorPicker4(label: string, col: cval<V4f>, refCol: byref<V4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4b>, refCol: byref<C4b>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4b()
            refCol <- refColState.ToC4b()
        result

    static member ColorPicker4(label: string, col: cval<C4b>, refCol: byref<C4b>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4b()
            refCol <- refColState.ToC4b()

    static member ColorPicker4(label: string, col: byref<C4us>, refCol: byref<C4us>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4us()
            refCol <- refColState.ToC4us()
        result

    static member ColorPicker4(label: string, col: cval<C4us>, refCol: byref<C4us>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4us()
            refCol <- refColState.ToC4us()

    static member ColorPicker4(label: string, col: byref<C4ui>, refCol: byref<C4ui>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4ui()
            refCol <- refColState.ToC4ui()
        result

    static member ColorPicker4(label: string, col: cval<C4ui>, refCol: byref<C4ui>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4ui()
            refCol <- refColState.ToC4ui()

    static member ColorPicker4(label: string, col: byref<C4f>, refCol: byref<C4f>) : bool =
        Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, NativePtr.cast<_, float32> &&refCol)

    static member ColorPicker4(label: string, col: cval<C4f>, refCol: byref<C4f>) : unit =
        let mutable colState = col.Value
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState

    static member ColorPicker4(label: string, col: byref<C4d>, refCol: byref<C4d>) : bool =
        let mutable colState = col.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&col, NativePtr.cast<_, float32> &&refCol)
        if result then
            col <- colState.ToC4d()
            refCol <- refColState.ToC4d()
        result

    static member ColorPicker4(label: string, col: cval<C4d>, refCol: byref<C4d>) : unit =
        let mutable colState = col.Value.ToC4f()
        let mutable refColState = refCol.ToC4f()
        let result = Hexa.NET.ImGui.ImGui.ColorPicker4(label, NativePtr.cast<_, float32> &&colState, NativePtr.cast<_, float32> &&refCol)
        if result then
            col.Value <- colState.ToC4d()
            refCol <- refColState.ToC4d()


    static member Columns(count: int32, id: string, borders: bool) : unit =
        Hexa.NET.ImGui.ImGui.Columns(count, id, borders)

    static member Columns(count: int32, id: string) : unit =
        Hexa.NET.ImGui.ImGui.Columns(count, id)

    static member Columns(count: int32) : unit =
        Hexa.NET.ImGui.ImGui.Columns(count)

    static member Columns() : unit =
        Hexa.NET.ImGui.ImGui.Columns()

    static member Columns(id: string) : unit =
        Hexa.NET.ImGui.ImGui.Columns(id)

    static member Columns(count: int32, borders: bool) : unit =
        Hexa.NET.ImGui.ImGui.Columns(count, borders)

    static member Columns(borders: bool) : unit =
        Hexa.NET.ImGui.ImGui.Columns(borders)

    static member Columns(id: string, borders: bool) : unit =
        Hexa.NET.ImGui.ImGui.Columns(id, borders)


    static member Combo(label: string, currentItem: byref<int32>, items: string[], popupMaxHeightInItems: int32) : bool =
        Hexa.NET.ImGui.ImGui.Combo(label, &&currentItem, items, items.Length, popupMaxHeightInItems)

    static member Combo(label: string, currentItem: cval<int32>, items: string[], popupMaxHeightInItems: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.Combo(label, &&currentItemState, items, items.Length, popupMaxHeightInItems)
        if result then
            currentItem.Value <- currentItemState

    static member Combo(label: string, currentItem: byref<int32>, items: string[]) : bool =
        Hexa.NET.ImGui.ImGui.Combo(label, &&currentItem, items, items.Length)

    static member Combo(label: string, currentItem: cval<int32>, items: string[]) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.Combo(label, &&currentItemState, items, items.Length)
        if result then
            currentItem.Value <- currentItemState

    static member Combo(label: string, currentItem: byref<int32>, itemsSeparatedByZeros: string, popupMaxHeightInItems: int32) : bool =
        Hexa.NET.ImGui.ImGui.Combo(label, &&currentItem, itemsSeparatedByZeros, popupMaxHeightInItems)

    static member Combo(label: string, currentItem: cval<int32>, itemsSeparatedByZeros: string, popupMaxHeightInItems: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.Combo(label, &&currentItemState, itemsSeparatedByZeros, popupMaxHeightInItems)
        if result then
            currentItem.Value <- currentItemState

    static member Combo(label: string, currentItem: byref<int32>, itemsSeparatedByZeros: string) : bool =
        Hexa.NET.ImGui.ImGui.Combo(label, &&currentItem, itemsSeparatedByZeros)

    static member Combo(label: string, currentItem: cval<int32>, itemsSeparatedByZeros: string) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.Combo(label, &&currentItemState, itemsSeparatedByZeros)
        if result then
            currentItem.Value <- currentItemState

    static member Combo(label: string, currentItem: byref<int32>, getter: nativeint, userData: voidptr, itemsCount: int32, popupMaxHeightInItems: int32) : bool =
        Hexa.NET.ImGui.ImGui.Combo(label, &&currentItem, getter, userData, itemsCount, popupMaxHeightInItems)

    static member Combo(label: string, currentItem: cval<int32>, getter: nativeint, userData: voidptr, itemsCount: int32, popupMaxHeightInItems: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.Combo(label, &&currentItemState, getter, userData, itemsCount, popupMaxHeightInItems)
        if result then
            currentItem.Value <- currentItemState

    static member Combo(label: string, currentItem: byref<int32>, getter: nativeint, userData: voidptr, itemsCount: int32) : bool =
        Hexa.NET.ImGui.ImGui.Combo(label, &&currentItem, getter, userData, itemsCount)

    static member Combo(label: string, currentItem: cval<int32>, getter: nativeint, userData: voidptr, itemsCount: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.Combo(label, &&currentItemState, getter, userData, itemsCount)
        if result then
            currentItem.Value <- currentItemState


    static member CompactCache(self: ImFontAtlasPtr) : unit =
        Hexa.NET.ImGui.ImGui.CompactCache(self)


    static member Contains(self: ImGuiSelectionBasicStoragePtr, id: uint32) : bool =
        Hexa.NET.ImGui.ImGui.Contains(self, id)


    static member Create(self: ImTextureDataPtr, format: ImTextureFormat, w: int32, h: int32) : unit =
        Hexa.NET.ImGui.ImGui.Create(self, format, w, h)


    static member CreateContext(sharedFontAtlas: ImFontAtlasPtr) : ImGuiContextPtr =
        Hexa.NET.ImGui.ImGui.CreateContext(sharedFontAtlas)

    static member CreateContext() : ImGuiContextPtr =
        Hexa.NET.ImGui.ImGui.CreateContext()


    static member DataTypeFormatString(buf: nativeptr<uint8>, bufSize: int32, dataType: ImGuiDataType, pData: voidptr, format: string) : int32 =
        Hexa.NET.ImGui.ImGui.DataTypeFormatString(buf, bufSize, dataType, pData, format)


    static member DeIndexAllBuffers(self: ImDrawDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.DeIndexAllBuffers(self)


    static member DebugCheckVersionAndDataLayout(versionStr: string, szIo: unativeint, szStyle: unativeint, szvec2: unativeint, szvec4: unativeint, szDrawvert: unativeint, szDrawidx: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.DebugCheckVersionAndDataLayout(versionStr, szIo, szStyle, szvec2, szvec4, szDrawvert, szDrawidx)


    static member DebugEditFontLoaderFlags(pFontLoaderFlags: nativeptr<ImGuiFreeTypeLoaderFlags>) : bool =
        Hexa.NET.ImGui.ImGui.DebugEditFontLoaderFlags(pFontLoaderFlags)


    static member DebugFlashStyleColor(idx: ImGuiCol) : unit =
        Hexa.NET.ImGui.ImGui.DebugFlashStyleColor(idx)


    static member DebugLog(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.DebugLog(fmt)


    static member DebugLogV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.DebugLogV(fmt, args)


    static member DebugStartItemPicker() : unit =
        Hexa.NET.ImGui.ImGui.DebugStartItemPicker()


    static member DebugTextEncoding(text: string) : unit =
        Hexa.NET.ImGui.ImGui.DebugTextEncoding(text)


    static member DeleteChars(self: ImGuiInputTextCallbackDataPtr, pos: int32, bytesCount: int32) : unit =
        Hexa.NET.ImGui.ImGui.DeleteChars(self, pos, bytesCount)


    static member Destroy(self: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(NativePtr.cast<_, System.Numerics.Vector2> &&self)

    static member Destroy(self: ImTextureRefPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiTableSortSpecsPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiTableColumnSortSpecsPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiStylePtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiInputTextCallbackDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiWindowClassPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiPayloadPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiOnceUponAFramePtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiTextFilterPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiTextRangePtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiTextBufferPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiStoragePairPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiListClipperPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImColorPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiSelectionBasicStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiSelectionExternalStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImDrawCmdPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImDrawListSplitterPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImDrawDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImTextureDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontConfigPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontGlyphPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontGlyphRangesBuilderPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontAtlasRectPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontAtlasPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontBakedPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImFontPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiViewportPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiPlatformIOPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiPlatformMonitorPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)

    static member Destroy(self: ImGuiPlatformImeDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.Destroy(self)


    static member DestroyContext(ctx: ImGuiContextPtr) : unit =
        Hexa.NET.ImGui.ImGui.DestroyContext(ctx)

    static member DestroyContext() : unit =
        Hexa.NET.ImGui.ImGui.DestroyContext()


    static member DestroyPixels(self: ImTextureDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.DestroyPixels(self)


    static member DestroyPlatformWindows() : unit =
        Hexa.NET.ImGui.ImGui.DestroyPlatformWindows()


    static member DockSpace(dockspaceId: uint32, size: V2f, flags: ImGuiDockNodeFlags, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.FromV2f(size), flags, windowClass)

    static member DockSpace(dockspaceId: uint32, size: V2f, flags: ImGuiDockNodeFlags) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.FromV2f(size), flags)

    static member DockSpace(dockspaceId: uint32, size: V2f) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.FromV2f(size))

    static member DockSpace(dockspaceId: uint32) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId)

    static member DockSpace(dockspaceId: uint32, flags: ImGuiDockNodeFlags) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, flags)

    static member DockSpace(dockspaceId: uint32, size: V2f, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.FromV2f(size), windowClass)

    static member DockSpace(dockspaceId: uint32, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, windowClass)

    static member DockSpace(dockspaceId: uint32, flags: ImGuiDockNodeFlags, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpace(dockspaceId, flags, windowClass)


    static member DockSpaceOverViewport(dockspaceId: uint32, viewport: ImGuiViewportPtr, flags: ImGuiDockNodeFlags, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, viewport, flags, windowClass)

    static member DockSpaceOverViewport(dockspaceId: uint32, viewport: ImGuiViewportPtr, flags: ImGuiDockNodeFlags) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, viewport, flags)

    static member DockSpaceOverViewport(dockspaceId: uint32, viewport: ImGuiViewportPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, viewport)

    static member DockSpaceOverViewport(dockspaceId: uint32) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId)

    static member DockSpaceOverViewport() : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport()

    static member DockSpaceOverViewport(viewport: ImGuiViewportPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(viewport)

    static member DockSpaceOverViewport(dockspaceId: uint32, flags: ImGuiDockNodeFlags) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, flags)

    static member DockSpaceOverViewport(flags: ImGuiDockNodeFlags) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(flags)

    static member DockSpaceOverViewport(viewport: ImGuiViewportPtr, flags: ImGuiDockNodeFlags) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(viewport, flags)

    static member DockSpaceOverViewport(dockspaceId: uint32, viewport: ImGuiViewportPtr, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, viewport, windowClass)

    static member DockSpaceOverViewport(dockspaceId: uint32, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, windowClass)

    static member DockSpaceOverViewport(windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(windowClass)

    static member DockSpaceOverViewport(viewport: ImGuiViewportPtr, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(viewport, windowClass)

    static member DockSpaceOverViewport(dockspaceId: uint32, flags: ImGuiDockNodeFlags, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(dockspaceId, flags, windowClass)

    static member DockSpaceOverViewport(flags: ImGuiDockNodeFlags, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(flags, windowClass)

    static member DockSpaceOverViewport(viewport: ImGuiViewportPtr, flags: ImGuiDockNodeFlags, windowClass: ImGuiWindowClassPtr) : uint32 =
        Hexa.NET.ImGui.ImGui.DockSpaceOverViewport(viewport, flags, windowClass)


    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, vMax, format, flags)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, vMax, format)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, vMax)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v)

    static member DragFloat(label: string, v: cval<float32>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, format)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, format)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, format)

    static member DragFloat(label: string, v: cval<float32>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, format)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, vMax, flags)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, flags)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, flags)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, flags)

    static member DragFloat(label: string, v: cval<float32>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, vMin, format, flags)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, vSpeed, format, flags)

    static member DragFloat(label: string, v: cval<float32>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat(label: string, v: byref<float32>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat(label, &&v, format, flags)

    static member DragFloat(label: string, v: cval<float32>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat(label, &&vState, format, flags)
        if result then
            v.Value <- vState


    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, format, flags)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, format)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v)

    static member DragFloat2(label: string, v: cval<V2f>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, format)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, format)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, format)

    static member DragFloat2(label: string, v: cval<V2f>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, format)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, flags)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, flags)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, flags)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, flags)

    static member DragFloat2(label: string, v: cval<V2f>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, format, flags)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, vSpeed, format, flags)

    static member DragFloat2(label: string, v: cval<V2f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat2(label: string, v: byref<V2f>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&v, format, flags)

    static member DragFloat2(label: string, v: cval<V2f>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat2(label, NativePtr.cast<_, float32> &&vState, format, flags)
        if result then
            v.Value <- vState


    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, format, flags)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, format)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v)

    static member DragFloat3(label: string, v: cval<V3f>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, format)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, format)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, format)

    static member DragFloat3(label: string, v: cval<V3f>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, format)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, flags)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, flags)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, flags)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, flags)

    static member DragFloat3(label: string, v: cval<V3f>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, format, flags)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, vSpeed, format, flags)

    static member DragFloat3(label: string, v: cval<V3f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat3(label: string, v: byref<V3f>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&v, format, flags)

    static member DragFloat3(label: string, v: cval<V3f>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat3(label, NativePtr.cast<_, float32> &&vState, format, flags)
        if result then
            v.Value <- vState


    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, format, flags)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, format)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v)

    static member DragFloat4(label: string, v: cval<V4f>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, format)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, format)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, format)

    static member DragFloat4(label: string, v: cval<V4f>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, format)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, vMax, flags)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, flags)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, flags)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, flags)

    static member DragFloat4(label: string, v: cval<V4f>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, vMin, format, flags)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, vSpeed, format, flags)

    static member DragFloat4(label: string, v: cval<V4f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragFloat4(label: string, v: byref<V4f>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&v, format, flags)

    static member DragFloat4(label: string, v: cval<V4f>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloat4(label, NativePtr.cast<_, float32> &&vState, format, flags)
        if result then
            v.Value <- vState


    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, vMax, format, formatMax, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, vMax, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, vMax, format, formatMax)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, vMax, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, vMax, format)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, vMax, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, vMax)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, vMax: float32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, vMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent))

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState))
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, format)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, format)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), format)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, format, formatMax)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, format, formatMax)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), format, formatMax)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, vMax, format, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, vMax, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, vMax, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, vMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, format, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, format, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), format, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, vMin: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, vMin, format, formatMax, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, vMin: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, vMin, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, vSpeed: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), vSpeed, format, formatMax, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, vSpeed: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), vSpeed, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragFloatRange2(label: string, vCurrent: byref<Range1f>, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrent), format, formatMax, flags)

    static member DragFloatRange2(label: string, vCurrent: cval<Range1f>, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragFloatRange2(label, NativePtr.cast<_, float32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, float32> &&vCurrentState), format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState


    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, vMax, format, flags)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, vMax, format)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, vMax)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v)

    static member DragInt(label: string, v: cval<int32>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin)

    static member DragInt(label: string, v: cval<int32>, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, vMax)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, format)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, format)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, format)

    static member DragInt(label: string, v: cval<int32>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, format)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, format)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, vMax, format)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, vMax, flags)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, flags)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, flags)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, flags)

    static member DragInt(label: string, v: cval<int32>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, flags)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, vMax, flags)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, vMin, format, flags)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vSpeed, format, flags)

    static member DragInt(label: string, v: cval<int32>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, format, flags)

    static member DragInt(label: string, v: cval<int32>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, format, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, format, flags)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt(label: string, v: byref<int32>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt(label, &&v, vMin, vMax, format, flags)

    static member DragInt(label: string, v: cval<int32>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt(label, &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState


    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, format, flags)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, format)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v)

    static member DragInt2(label: string, v: cval<V2i>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, format)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, format)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, format)

    static member DragInt2(label: string, v: cval<V2i>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, format)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, format)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, flags)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, flags)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, flags)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, flags)

    static member DragInt2(label: string, v: cval<V2i>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, flags)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax, flags)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, format, flags)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vSpeed, format, flags)

    static member DragInt2(label: string, v: cval<V2i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, format, flags)

    static member DragInt2(label: string, v: cval<V2i>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, format, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, format, flags)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format, flags)

    static member DragInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState


    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, format, flags)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, format)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v)

    static member DragInt3(label: string, v: cval<V3i>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, format)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, format)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, format)

    static member DragInt3(label: string, v: cval<V3i>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, format)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, format)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, flags)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, flags)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, flags)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, flags)

    static member DragInt3(label: string, v: cval<V3i>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, flags)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax, flags)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, format, flags)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vSpeed, format, flags)

    static member DragInt3(label: string, v: cval<V3i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, format, flags)

    static member DragInt3(label: string, v: cval<V3i>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, format, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, format, flags)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format, flags)

    static member DragInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState


    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, format, flags)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, format)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v)

    static member DragInt4(label: string, v: cval<V4i>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, format)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, format)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, format)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, format)

    static member DragInt4(label: string, v: cval<V4i>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, format)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, format)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, format)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, vMax, flags)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, flags)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, flags)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, flags)

    static member DragInt4(label: string, v: cval<V4i>, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, flags)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax, flags)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, vMin, format, flags)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vSpeed, format, flags)

    static member DragInt4(label: string, v: cval<V4i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vSpeed, format, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, format, flags)

    static member DragInt4(label: string, v: cval<V4i>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, format, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, format, flags)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, format, flags)
        if result then
            v.Value <- vState

    static member DragInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format, flags)

    static member DragInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.DragInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState


    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, vMax, format, formatMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, vMax, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, vMax, format, formatMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, vMax, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, vMax, format)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, vMax, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, vMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, vMax: int32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, vMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent))

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState))
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, vMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, vMax: int32) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, vMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, format)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, format)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), format)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, format)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, vMax, format)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, vMax, format)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, format, formatMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, format, formatMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), format, formatMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, format, formatMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, vMax: int32, format: string, formatMax: string) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, vMax, format, formatMax)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, vMax: int32, format: string, formatMax: string) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, vMax, format, formatMax)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, vMax, format, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, vMax, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, vMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, vMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, vMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, vMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, format, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, format, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), format, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, format, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, vMax, format, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, vMax, format, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, vMin: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, vMin, format, formatMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, vMin: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, vMin, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vSpeed: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vSpeed, format, formatMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vSpeed: float32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vSpeed, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), format, formatMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, format, formatMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState

    static member DragIntRange2(label: string, vCurrent: byref<Range1i>, vMin: int32, vMax: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrent, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrent), vMin, vMax, format, formatMax, flags)

    static member DragIntRange2(label: string, vCurrent: cval<Range1i>, vMin: int32, vMax: int32, format: string, formatMax: string, flags: ImGuiSliderFlags) : unit =
        let mutable vCurrentState = vCurrent.Value
        let result = Hexa.NET.ImGui.ImGui.DragIntRange2(label, NativePtr.cast<_, int32> &&vCurrentState, NativePtr.step 1 (NativePtr.cast<_, int32> &&vCurrentState), vMin, vMax, format, formatMax, flags)
        if result then
            vCurrent.Value <- vCurrentState


    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, pMax, format, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, pMax, format)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, pMax)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, pMax)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, format)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, format)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, format)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, format)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, pMax, format)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, pMax, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, pMax, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, pMin: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, pMin, format, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, vSpeed, format, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, format, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, format, flags)

    static member DragScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalar(label, dataType, pData, pMin, pMax, format, flags)


    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, pMax, format, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, pMax, format)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, pMax)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, pMax)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, format)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, format)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, format)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, format)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, pMax, format)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, pMax, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, pMax, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, pMin: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, pMin, format, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, vSpeed: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, vSpeed, format, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, format, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, format, flags)

    static member DragScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.DragScalarN(label, dataType, pData, components, pMin, pMax, format, flags)


    static member Draw(self: ImGuiTextFilterPtr, label: string, width: float32) : bool =
        Hexa.NET.ImGui.ImGui.Draw(self, label, width)

    static member Draw(self: ImGuiTextFilterPtr, label: string) : bool =
        Hexa.NET.ImGui.ImGui.Draw(self, label)

    static member Draw(self: ImGuiTextFilterPtr) : bool =
        Hexa.NET.ImGui.ImGui.Draw(self)

    static member Draw(self: ImGuiTextFilterPtr, width: float32) : bool =
        Hexa.NET.ImGui.ImGui.Draw(self, width)


    static member Dummy(size: V2f) : unit =
        Hexa.NET.ImGui.ImGui.Dummy(System.Numerics.Vector2.FromV2f(size))


    static member End() : unit =
        Hexa.NET.ImGui.ImGui.End()

    static member End(self: ImGuiListClipperPtr) : unit =
        Hexa.NET.ImGui.ImGui.End(self)


    static member EndChild() : unit =
        Hexa.NET.ImGui.ImGui.EndChild()


    static member EndCombo() : unit =
        Hexa.NET.ImGui.ImGui.EndCombo()


    static member EndDisabled() : unit =
        Hexa.NET.ImGui.ImGui.EndDisabled()


    static member EndDragDropSource() : unit =
        Hexa.NET.ImGui.ImGui.EndDragDropSource()


    static member EndDragDropTarget() : unit =
        Hexa.NET.ImGui.ImGui.EndDragDropTarget()


    static member EndFrame() : unit =
        Hexa.NET.ImGui.ImGui.EndFrame()


    static member EndGroup() : unit =
        Hexa.NET.ImGui.ImGui.EndGroup()


    static member EndListBox() : unit =
        Hexa.NET.ImGui.ImGui.EndListBox()


    static member EndMainMenuBar() : unit =
        Hexa.NET.ImGui.ImGui.EndMainMenuBar()


    static member EndMenu() : unit =
        Hexa.NET.ImGui.ImGui.EndMenu()


    static member EndMenuBar() : unit =
        Hexa.NET.ImGui.ImGui.EndMenuBar()


    static member EndMultiSelect() : ImGuiMultiSelectIOPtr =
        Hexa.NET.ImGui.ImGui.EndMultiSelect()


    static member EndPopup() : unit =
        Hexa.NET.ImGui.ImGui.EndPopup()


    static member EndTabBar() : unit =
        Hexa.NET.ImGui.ImGui.EndTabBar()


    static member EndTabItem() : unit =
        Hexa.NET.ImGui.ImGui.EndTabItem()


    static member EndTable() : unit =
        Hexa.NET.ImGui.ImGui.EndTable()


    static member EndTooltip() : unit =
        Hexa.NET.ImGui.ImGui.EndTooltip()


    static member FindGlyph(self: ImFontBakedPtr, c: uint32) : ImFontGlyphPtr =
        Hexa.NET.ImGui.ImGui.FindGlyph(self, c)


    static member FindGlyphNoFallback(self: ImFontBakedPtr, c: uint32) : ImFontGlyphPtr =
        Hexa.NET.ImGui.ImGui.FindGlyphNoFallback(self, c)


    static member FindViewportByID(id: uint32) : ImGuiViewportPtr =
        Hexa.NET.ImGui.ImGui.FindViewportByID(id)


    static member FindViewportByPlatformHandle(platformHandle: voidptr) : ImGuiViewportPtr =
        Hexa.NET.ImGui.ImGui.FindViewportByPlatformHandle(platformHandle)


    static member FreeApi() : unit =
        Hexa.NET.ImGui.ImGui.FreeApi()


    static member GETFLTMAX() : float32 =
        Hexa.NET.ImGui.ImGui.GETFLTMAX()


    static member GETFLTMIN() : float32 =
        Hexa.NET.ImGui.ImGui.GETFLTMIN()


    static member GetAllocatorFunctions(pAllocFunc: nativeptr<nativeint>, pFreeFunc: nativeptr<nativeint>, pUserData: nativeptr<voidptr>) : unit =
        Hexa.NET.ImGui.ImGui.GetAllocatorFunctions(pAllocFunc, pFreeFunc, pUserData)


    static member GetBackgroundDrawList(viewport: ImGuiViewportPtr) : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.GetBackgroundDrawList(viewport)

    static member GetBackgroundDrawList() : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.GetBackgroundDrawList()


    static member GetBit(self: ImFontGlyphRangesBuilderPtr, n: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.GetBit(self, n)


    static member GetBool(self: ImGuiStoragePtr, key: uint32, defaultVal: bool) : bool =
        Hexa.NET.ImGui.ImGui.GetBool(self, key, defaultVal)

    static member GetBool(self: ImGuiStoragePtr, key: uint32) : bool =
        Hexa.NET.ImGui.ImGui.GetBool(self, key)


    static member GetBoolRef(self: ImGuiStoragePtr, key: uint32, defaultVal: bool) : nativeptr<bool> =
        Hexa.NET.ImGui.ImGui.GetBoolRef(self, key, defaultVal)

    static member GetBoolRef(self: ImGuiStoragePtr, key: uint32) : nativeptr<bool> =
        Hexa.NET.ImGui.ImGui.GetBoolRef(self, key)


    static member GetCenter(self: ImGuiViewportPtr) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetCenter(self)
        result.ToV2f()

    static member GetCenter(pOut: byref<V2f>, self: ImGuiViewportPtr) : unit =
        Hexa.NET.ImGui.ImGui.GetCenter(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self)


    static member GetCharAdvance(self: ImFontBakedPtr, c: uint32) : float32 =
        Hexa.NET.ImGui.ImGui.GetCharAdvance(self, c)


    static member GetClipRectMax(self: ImDrawListPtr) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetClipRectMax(self)
        result.ToV2f()

    static member GetClipRectMax(pOut: byref<V2f>, self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.GetClipRectMax(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self)


    static member GetClipRectMin(self: ImDrawListPtr) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetClipRectMin(self)
        result.ToV2f()

    static member GetClipRectMin(pOut: byref<V2f>, self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.GetClipRectMin(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self)


    static member GetClipboardText() : string =
        Hexa.NET.ImGui.ImGui.GetClipboardTextS()


    static member GetColorU32(idx: ImGuiCol, alphaMul: float32) : uint32 =
        Hexa.NET.ImGui.ImGui.GetColorU32(idx, alphaMul)

    static member GetColorU32(idx: ImGuiCol) : uint32 =
        Hexa.NET.ImGui.ImGui.GetColorU32(idx)

    static member GetColorU32(col: V4f) : uint32 =
        Hexa.NET.ImGui.ImGui.GetColorU32(System.Numerics.Vector4.FromV4f(col))

    static member GetColorU32(col: uint32, alphaMul: float32) : uint32 =
        Hexa.NET.ImGui.ImGui.GetColorU32(col, alphaMul)

    static member GetColorU32(col: uint32) : uint32 =
        Hexa.NET.ImGui.ImGui.GetColorU32(col)


    static member GetColumnIndex() : int32 =
        Hexa.NET.ImGui.ImGui.GetColumnIndex()


    static member GetColumnOffset(columnIndex: int32) : float32 =
        Hexa.NET.ImGui.ImGui.GetColumnOffset(columnIndex)

    static member GetColumnOffset() : float32 =
        Hexa.NET.ImGui.ImGui.GetColumnOffset()


    static member GetColumnWidth(columnIndex: int32) : float32 =
        Hexa.NET.ImGui.ImGui.GetColumnWidth(columnIndex)

    static member GetColumnWidth() : float32 =
        Hexa.NET.ImGui.ImGui.GetColumnWidth()


    static member GetColumnsCount() : int32 =
        Hexa.NET.ImGui.ImGui.GetColumnsCount()


    static member GetContentRegionAvail() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetContentRegionAvail()
        result.ToV2f()

    static member GetContentRegionAvail(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetContentRegionAvail(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetCurrentContext() : ImGuiContextPtr =
        Hexa.NET.ImGui.ImGui.GetCurrentContext()


    static member GetCursorPos() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetCursorPos()
        result.ToV2f()

    static member GetCursorPos(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetCursorPos(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetCursorPosX() : float32 =
        Hexa.NET.ImGui.ImGui.GetCursorPosX()


    static member GetCursorPosY() : float32 =
        Hexa.NET.ImGui.ImGui.GetCursorPosY()


    static member GetCursorScreenPos() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetCursorScreenPos()
        result.ToV2f()

    static member GetCursorScreenPos(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetCursorScreenPos(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetCursorStartPos() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetCursorStartPos()
        result.ToV2f()

    static member GetCursorStartPos(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetCursorStartPos(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetCustomRect(self: ImFontAtlasPtr, id: int32, outR: ImFontAtlasRectPtr) : bool =
        Hexa.NET.ImGui.ImGui.GetCustomRect(self, id, outR)


    static member GetDebugName(self: ImFontPtr) : string =
        Hexa.NET.ImGui.ImGui.GetDebugNameS(self)


    static member GetDragDropPayload() : ImGuiPayloadPtr =
        Hexa.NET.ImGui.ImGui.GetDragDropPayload()


    static member GetDrawData() : ImDrawDataPtr =
        Hexa.NET.ImGui.ImGui.GetDrawData()


    static member GetDrawListSharedData() : ImDrawListSharedDataPtr =
        Hexa.NET.ImGui.ImGui.GetDrawListSharedData()


    static member GetFloat(self: ImGuiStoragePtr, key: uint32, defaultVal: float32) : float32 =
        Hexa.NET.ImGui.ImGui.GetFloat(self, key, defaultVal)

    static member GetFloat(self: ImGuiStoragePtr, key: uint32) : float32 =
        Hexa.NET.ImGui.ImGui.GetFloat(self, key)


    static member GetFloatRef(self: ImGuiStoragePtr, key: uint32, defaultVal: float32) : nativeptr<float32> =
        Hexa.NET.ImGui.ImGui.GetFloatRef(self, key, defaultVal)

    static member GetFloatRef(self: ImGuiStoragePtr, key: uint32) : nativeptr<float32> =
        Hexa.NET.ImGui.ImGui.GetFloatRef(self, key)


    static member GetFont() : ImFontPtr =
        Hexa.NET.ImGui.ImGui.GetFont()


    static member GetFontBaked() : ImFontBakedPtr =
        Hexa.NET.ImGui.ImGui.GetFontBaked()

    static member GetFontBaked(self: ImFontPtr, fontSize: float32, density: float32) : ImFontBakedPtr =
        Hexa.NET.ImGui.ImGui.GetFontBaked(self, fontSize, density)

    static member GetFontBaked(self: ImFontPtr, fontSize: float32) : ImFontBakedPtr =
        Hexa.NET.ImGui.ImGui.GetFontBaked(self, fontSize)


    static member GetFontLoader() : ImFontLoaderPtr =
        Hexa.NET.ImGui.ImGui.GetFontLoader()


    static member GetFontSize() : float32 =
        Hexa.NET.ImGui.ImGui.GetFontSize()


    static member GetFontTexUvWhitePixel() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetFontTexUvWhitePixel()
        result.ToV2f()

    static member GetFontTexUvWhitePixel(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetFontTexUvWhitePixel(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetForegroundDrawList(viewport: ImGuiViewportPtr) : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.GetForegroundDrawList(viewport)

    static member GetForegroundDrawList() : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.GetForegroundDrawList()


    static member GetFrameCount() : int32 =
        Hexa.NET.ImGui.ImGui.GetFrameCount()


    static member GetFrameHeight() : float32 =
        Hexa.NET.ImGui.ImGui.GetFrameHeight()


    static member GetFrameHeightWithSpacing() : float32 =
        Hexa.NET.ImGui.ImGui.GetFrameHeightWithSpacing()


    static member GetGlyphRangesDefault(self: ImFontAtlasPtr) : nativeptr<uint32> =
        Hexa.NET.ImGui.ImGui.GetGlyphRangesDefault(self)


    static member GetID(strId: string) : uint32 =
        Hexa.NET.ImGui.ImGui.GetID(strId)

    static member GetID(strIdBegin: string, strIdEnd: string) : uint32 =
        Hexa.NET.ImGui.ImGui.GetID(strIdBegin, strIdEnd)

    static member GetID(ptrId: voidptr) : uint32 =
        Hexa.NET.ImGui.ImGui.GetID(ptrId)

    static member GetID(intId: int32) : uint32 =
        Hexa.NET.ImGui.ImGui.GetID(intId)


    static member GetIO() : ImGuiIOPtr =
        Hexa.NET.ImGui.ImGui.GetIO()


    static member GetInt(self: ImGuiStoragePtr, key: uint32, defaultVal: int32) : int32 =
        Hexa.NET.ImGui.ImGui.GetInt(self, key, defaultVal)

    static member GetInt(self: ImGuiStoragePtr, key: uint32) : int32 =
        Hexa.NET.ImGui.ImGui.GetInt(self, key)


    static member GetIntRef(self: ImGuiStoragePtr, key: uint32, defaultVal: int32) : nativeptr<int32> =
        Hexa.NET.ImGui.ImGui.GetIntRef(self, key, defaultVal)

    static member GetIntRef(self: ImGuiStoragePtr, key: uint32) : nativeptr<int32> =
        Hexa.NET.ImGui.ImGui.GetIntRef(self, key)


    static member GetItemID() : uint32 =
        Hexa.NET.ImGui.ImGui.GetItemID()


    static member GetItemRectMax() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetItemRectMax()
        result.ToV2f()

    static member GetItemRectMax(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetItemRectMax(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetItemRectMin() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetItemRectMin()
        result.ToV2f()

    static member GetItemRectMin(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetItemRectMin(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetItemRectSize() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetItemRectSize()
        result.ToV2f()

    static member GetItemRectSize(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetItemRectSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetKeyChordName(keyChord: int32) : string =
        Hexa.NET.ImGui.ImGui.GetKeyChordNameS(keyChord)


    static member GetKeyName(key: ImGuiKey) : string =
        Hexa.NET.ImGui.ImGui.GetKeyNameS(key)


    static member GetKeyPressedAmount(key: ImGuiKey, repeatDelay: float32, rate: float32) : int32 =
        Hexa.NET.ImGui.ImGui.GetKeyPressedAmount(key, repeatDelay, rate)


    static member GetLibraryName() : string =
        Hexa.NET.ImGui.ImGui.GetLibraryName()


    static member GetMainViewport() : ImGuiViewportPtr =
        Hexa.NET.ImGui.ImGui.GetMainViewport()


    static member GetMouseClickedCount(button: ImGuiMouseButton) : int32 =
        Hexa.NET.ImGui.ImGui.GetMouseClickedCount(button)


    static member GetMouseCursor() : ImGuiMouseCursor =
        Hexa.NET.ImGui.ImGui.GetMouseCursor()


    static member GetMouseDragDelta() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetMouseDragDelta()
        result.ToV2f()

    static member GetMouseDragDelta(button: ImGuiMouseButton) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetMouseDragDelta(button)
        result.ToV2f()

    static member GetMouseDragDelta(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetMouseDragDelta(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)

    static member GetMouseDragDelta(lockThreshold: float32) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetMouseDragDelta(lockThreshold)
        result.ToV2f()

    static member GetMouseDragDelta(button: ImGuiMouseButton, lockThreshold: float32) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetMouseDragDelta(button, lockThreshold)
        result.ToV2f()

    static member GetMouseDragDelta(pOut: byref<V2f>, button: ImGuiMouseButton, lockThreshold: float32) : unit =
        Hexa.NET.ImGui.ImGui.GetMouseDragDelta(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, button, lockThreshold)

    static member GetMouseDragDelta(pOut: byref<V2f>, button: ImGuiMouseButton) : unit =
        Hexa.NET.ImGui.ImGui.GetMouseDragDelta(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, button)

    static member GetMouseDragDelta(pOut: byref<V2f>, lockThreshold: float32) : unit =
        Hexa.NET.ImGui.ImGui.GetMouseDragDelta(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, lockThreshold)


    static member GetMousePos() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetMousePos()
        result.ToV2f()

    static member GetMousePos(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetMousePos(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetMousePosOnOpeningCurrentPopup() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetMousePosOnOpeningCurrentPopup()
        result.ToV2f()

    static member GetMousePosOnOpeningCurrentPopup(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetMousePosOnOpeningCurrentPopup(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetNextSelectedItem(self: ImGuiSelectionBasicStoragePtr, opaqueIt: nativeptr<voidptr>, outId: nativeptr<uint32>) : bool =
        Hexa.NET.ImGui.ImGui.GetNextSelectedItem(self, opaqueIt, outId)


    static member GetPitch(self: ImTextureDataPtr) : int32 =
        Hexa.NET.ImGui.ImGui.GetPitch(self)


    static member GetPixels(self: ImTextureDataPtr) : voidptr =
        Hexa.NET.ImGui.ImGui.GetPixels(self)


    static member GetPixelsAt(self: ImTextureDataPtr, x: int32, y: int32) : voidptr =
        Hexa.NET.ImGui.ImGui.GetPixelsAt(self, x, y)


    static member GetPlatformIO() : ImGuiPlatformIOPtr =
        Hexa.NET.ImGui.ImGui.GetPlatformIO()


    static member GetScrollMaxX() : float32 =
        Hexa.NET.ImGui.ImGui.GetScrollMaxX()


    static member GetScrollMaxY() : float32 =
        Hexa.NET.ImGui.ImGui.GetScrollMaxY()


    static member GetScrollX() : float32 =
        Hexa.NET.ImGui.ImGui.GetScrollX()


    static member GetScrollY() : float32 =
        Hexa.NET.ImGui.ImGui.GetScrollY()


    static member GetSizeInBytes(self: ImTextureDataPtr) : int32 =
        Hexa.NET.ImGui.ImGui.GetSizeInBytes(self)


    static member GetStateStorage() : ImGuiStoragePtr =
        Hexa.NET.ImGui.ImGui.GetStateStorage()


    static member GetStorageIdFromIndex(self: ImGuiSelectionBasicStoragePtr, idx: int32) : uint32 =
        Hexa.NET.ImGui.ImGui.GetStorageIdFromIndex(self, idx)


    static member GetStyle() : ImGuiStylePtr =
        Hexa.NET.ImGui.ImGui.GetStyle()


    static member GetStyleColorName(idx: ImGuiCol) : string =
        Hexa.NET.ImGui.ImGui.GetStyleColorNameS(idx)


    static member GetStyleColorVec4(idx: ImGuiCol) : nativeptr<V4f> =
        let result = Hexa.NET.ImGui.ImGui.GetStyleColorVec4(idx)
        NativePtr.cast result


    static member GetTexID(self: ImTextureRefPtr) : ImTextureID =
        Hexa.NET.ImGui.ImGui.GetTexID(self)

    static member GetTexID(self: ImDrawCmdPtr) : ImTextureID =
        Hexa.NET.ImGui.ImGui.GetTexID(self)

    static member GetTexID(self: ImTextureDataPtr) : ImTextureID =
        Hexa.NET.ImGui.ImGui.GetTexID(self)


    static member GetTexRef(self: ImTextureDataPtr) : ImTextureRef =
        Hexa.NET.ImGui.ImGui.GetTexRef(self)

    static member GetTexRef(pOut: ImTextureRefPtr, self: ImTextureDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.GetTexRef(pOut, self)


    static member GetTextLineHeight() : float32 =
        Hexa.NET.ImGui.ImGui.GetTextLineHeight()


    static member GetTextLineHeightWithSpacing() : float32 =
        Hexa.NET.ImGui.ImGui.GetTextLineHeightWithSpacing()


    static member GetTime() : float =
        Hexa.NET.ImGui.ImGui.GetTime()


    static member GetTreeNodeToLabelSpacing() : float32 =
        Hexa.NET.ImGui.ImGui.GetTreeNodeToLabelSpacing()


    static member GetVersion() : string =
        Hexa.NET.ImGui.ImGui.GetVersionS()


    static member GetVoidPtr(self: ImGuiStoragePtr, key: uint32) : voidptr =
        Hexa.NET.ImGui.ImGui.GetVoidPtr(self, key)


    static member GetVoidPtrRef(self: ImGuiStoragePtr, key: uint32, defaultVal: voidptr) : nativeptr<voidptr> =
        Hexa.NET.ImGui.ImGui.GetVoidPtrRef(self, key, defaultVal)

    static member GetVoidPtrRef(self: ImGuiStoragePtr, key: uint32) : nativeptr<voidptr> =
        Hexa.NET.ImGui.ImGui.GetVoidPtrRef(self, key)


    static member GetWindowDockID() : uint32 =
        Hexa.NET.ImGui.ImGui.GetWindowDockID()


    static member GetWindowDpiScale() : float32 =
        Hexa.NET.ImGui.ImGui.GetWindowDpiScale()


    static member GetWindowDrawList() : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.GetWindowDrawList()


    static member GetWindowHeight() : float32 =
        Hexa.NET.ImGui.ImGui.GetWindowHeight()


    static member GetWindowPos() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetWindowPos()
        result.ToV2f()

    static member GetWindowPos(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetWindowPos(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetWindowSize() : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetWindowSize()
        result.ToV2f()

    static member GetWindowSize(pOut: byref<V2f>) : unit =
        Hexa.NET.ImGui.ImGui.GetWindowSize(NativePtr.cast<_, System.Numerics.Vector2> &&pOut)


    static member GetWindowViewport() : ImGuiViewportPtr =
        Hexa.NET.ImGui.ImGui.GetWindowViewport()


    static member GetWindowWidth() : float32 =
        Hexa.NET.ImGui.ImGui.GetWindowWidth()


    static member GetWorkCenter(self: ImGuiViewportPtr) : V2f =
        let result = Hexa.NET.ImGui.ImGui.GetWorkCenter(self)
        result.ToV2f()

    static member GetWorkCenter(pOut: byref<V2f>, self: ImGuiViewportPtr) : unit =
        Hexa.NET.ImGui.ImGui.GetWorkCenter(NativePtr.cast<_, System.Numerics.Vector2> &&pOut, self)


    static member HSV(h: float32, s: float32, v: float32) : ImColor =
        Hexa.NET.ImGui.ImGui.HSV(h, s, v)

    static member HSV(h: float32, s: float32, v: float32, a: float32) : ImColor =
        Hexa.NET.ImGui.ImGui.HSV(h, s, v, a)

    static member HSV(pOut: ImColorPtr, h: float32, s: float32, v: float32, a: float32) : unit =
        Hexa.NET.ImGui.ImGui.HSV(pOut, h, s, v, a)

    static member HSV(pOut: ImColorPtr, h: float32, s: float32, v: float32) : unit =
        Hexa.NET.ImGui.ImGui.HSV(pOut, h, s, v)


    static member HasSelection(self: ImGuiInputTextCallbackDataPtr) : bool =
        Hexa.NET.ImGui.ImGui.HasSelection(self)


    static member ImColor() : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor()

    static member ImColor(r: float32, g: float32, b: float32, a: float32) : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor(r, g, b, a)

    static member ImColor(r: float32, g: float32, b: float32) : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor(r, g, b)

    static member ImColor(col: V4f) : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor(System.Numerics.Vector4.FromV4f(col))

    static member ImColor(r: int32, g: int32, b: int32, a: int32) : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor(r, g, b, a)

    static member ImColor(r: int32, g: int32, b: int32) : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor(r, g, b)

    static member ImColor(rgba: uint32) : ImColorPtr =
        Hexa.NET.ImGui.ImGui.ImColor(rgba)


    static member ImDrawCmd() : ImDrawCmdPtr =
        Hexa.NET.ImGui.ImGui.ImDrawCmd()


    static member ImDrawData() : ImDrawDataPtr =
        Hexa.NET.ImGui.ImGui.ImDrawData()


    static member ImDrawList(sharedData: ImDrawListSharedDataPtr) : ImDrawListPtr =
        Hexa.NET.ImGui.ImGui.ImDrawList(sharedData)


    static member ImDrawListSplitter() : ImDrawListSplitterPtr =
        Hexa.NET.ImGui.ImGui.ImDrawListSplitter()


    static member ImFont() : ImFontPtr =
        Hexa.NET.ImGui.ImGui.ImFont()


    static member ImFontAtlas() : ImFontAtlasPtr =
        Hexa.NET.ImGui.ImGui.ImFontAtlas()


    static member ImFontAtlasBakedAddFontGlyphAdvancedX(atlas: ImFontAtlasPtr, baked: ImFontBakedPtr, src: ImFontConfigPtr, codepoint: uint32, advanceX: float32) : unit =
        Hexa.NET.ImGui.ImGui.ImFontAtlasBakedAddFontGlyphAdvancedX(atlas, baked, src, codepoint, advanceX)


    static member ImFontAtlasRect() : ImFontAtlasRectPtr =
        Hexa.NET.ImGui.ImGui.ImFontAtlasRect()


    static member ImFontAtlasSetFontLoader(self: ImFontAtlasPtr, fontLoader: ImFontLoaderPtr) : unit =
        Hexa.NET.ImGui.ImGui.ImFontAtlasSetFontLoader(self, fontLoader)


    static member ImFontBaked() : ImFontBakedPtr =
        Hexa.NET.ImGui.ImGui.ImFontBaked()


    static member ImFontConfig() : ImFontConfigPtr =
        Hexa.NET.ImGui.ImGui.ImFontConfig()


    static member ImFontGlyph() : ImFontGlyphPtr =
        Hexa.NET.ImGui.ImGui.ImFontGlyph()


    static member ImFontGlyphRangesBuilder() : ImFontGlyphRangesBuilderPtr =
        Hexa.NET.ImGui.ImGui.ImFontGlyphRangesBuilder()


    static member ImFormatString(buf: nativeptr<uint8>, bufSize: unativeint, fmt: string) : int32 =
        Hexa.NET.ImGui.ImGui.ImFormatString(buf, bufSize, fmt)


    static member ImFormatStringToTempBuffer(outBuf: byref<nativeptr<uint8>>, outBufEnd: byref<nativeptr<uint8>>, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.ImFormatStringToTempBuffer(&&outBuf, &&outBufEnd, fmt)


    static member ImFormatStringV(buf: nativeptr<uint8>, bufSize: unativeint, fmt: string, args: unativeint) : int32 =
        Hexa.NET.ImGui.ImGui.ImFormatStringV(buf, bufSize, fmt, args)


    static member ImGuiIO() : ImGuiIOPtr =
        Hexa.NET.ImGui.ImGui.ImGuiIO()


    static member ImGuiInputTextCallbackData() : ImGuiInputTextCallbackDataPtr =
        Hexa.NET.ImGui.ImGui.ImGuiInputTextCallbackData()


    static member ImGuiListClipper() : ImGuiListClipperPtr =
        Hexa.NET.ImGui.ImGui.ImGuiListClipper()


    static member ImGuiOnceUponAFrame() : ImGuiOnceUponAFramePtr =
        Hexa.NET.ImGui.ImGui.ImGuiOnceUponAFrame()


    static member ImGuiPayload() : ImGuiPayloadPtr =
        Hexa.NET.ImGui.ImGui.ImGuiPayload()


    static member ImGuiPlatformIO() : ImGuiPlatformIOPtr =
        Hexa.NET.ImGui.ImGui.ImGuiPlatformIO()


    static member ImGuiPlatformImeData() : ImGuiPlatformImeDataPtr =
        Hexa.NET.ImGui.ImGui.ImGuiPlatformImeData()


    static member ImGuiPlatformMonitor() : ImGuiPlatformMonitorPtr =
        Hexa.NET.ImGui.ImGui.ImGuiPlatformMonitor()


    static member ImGuiSelectionBasicStorage() : ImGuiSelectionBasicStoragePtr =
        Hexa.NET.ImGui.ImGui.ImGuiSelectionBasicStorage()


    static member ImGuiSelectionExternalStorage() : ImGuiSelectionExternalStoragePtr =
        Hexa.NET.ImGui.ImGui.ImGuiSelectionExternalStorage()


    static member ImGuiStoragePair(key: uint32, value: int32) : ImGuiStoragePairPtr =
        Hexa.NET.ImGui.ImGui.ImGuiStoragePair(key, value)

    static member ImGuiStoragePair(key: uint32, value: float32) : ImGuiStoragePairPtr =
        Hexa.NET.ImGui.ImGui.ImGuiStoragePair(key, value)

    static member ImGuiStoragePair(key: uint32, value: voidptr) : ImGuiStoragePairPtr =
        Hexa.NET.ImGui.ImGui.ImGuiStoragePair(key, value)


    static member ImGuiStyle() : ImGuiStylePtr =
        Hexa.NET.ImGui.ImGui.ImGuiStyle()


    static member ImGuiTableColumnSortSpecs() : ImGuiTableColumnSortSpecsPtr =
        Hexa.NET.ImGui.ImGui.ImGuiTableColumnSortSpecs()


    static member ImGuiTableSortSpecs() : ImGuiTableSortSpecsPtr =
        Hexa.NET.ImGui.ImGui.ImGuiTableSortSpecs()


    static member ImGuiTextBuffer() : ImGuiTextBufferPtr =
        Hexa.NET.ImGui.ImGui.ImGuiTextBuffer()


    static member ImGuiTextFilter(defaultFilter: string) : ImGuiTextFilterPtr =
        Hexa.NET.ImGui.ImGui.ImGuiTextFilter(defaultFilter)

    static member ImGuiTextFilter() : ImGuiTextFilterPtr =
        Hexa.NET.ImGui.ImGui.ImGuiTextFilter()


    static member ImGuiTextRange() : ImGuiTextRangePtr =
        Hexa.NET.ImGui.ImGui.ImGuiTextRange()

    static member ImGuiTextRange(b: string, e: string) : ImGuiTextRangePtr =
        Hexa.NET.ImGui.ImGui.ImGuiTextRange(b, e)


    static member ImGuiViewport() : ImGuiViewportPtr =
        Hexa.NET.ImGui.ImGui.ImGuiViewport()


    static member ImGuiWindowClass() : ImGuiWindowClassPtr =
        Hexa.NET.ImGui.ImGui.ImGuiWindowClass()


    static member ImParseFormatTrimDecorations(format: string, buf: nativeptr<uint8>, bufSize: unativeint) : string =
        Hexa.NET.ImGui.ImGui.ImParseFormatTrimDecorationsS(format, buf, bufSize)


    static member ImTextStrFromUtf8(outBuf: nativeptr<uint32>, outBufSize: int32, inText: string, inTextEnd: string, inRemaining: byref<nativeptr<uint8>>) : int32 =
        Hexa.NET.ImGui.ImGui.ImTextStrFromUtf8(outBuf, outBufSize, inText, inTextEnd, &&inRemaining)

    static member ImTextStrFromUtf8(outBuf: nativeptr<uint32>, outBufSize: int32, inText: string, inTextEnd: string) : int32 =
        Hexa.NET.ImGui.ImGui.ImTextStrFromUtf8(outBuf, outBufSize, inText, inTextEnd)


    static member ImTextStrToUtf8(outBuf: byref<uint8>, outBufSize: int32, inText: nativeptr<uint32>, inTextEnd: nativeptr<uint32>) : int32 =
        Hexa.NET.ImGui.ImGui.ImTextStrToUtf8(&&outBuf, outBufSize, inText, inTextEnd)


    static member ImTextureData() : ImTextureDataPtr =
        Hexa.NET.ImGui.ImGui.ImTextureData()


    static member ImTextureRef() : ImTextureRefPtr =
        Hexa.NET.ImGui.ImGui.ImTextureRef()

    static member ImTextureRef(texId: ImTextureID) : ImTextureRefPtr =
        Hexa.NET.ImGui.ImGui.ImTextureRef(texId)


    static member ImVec2() : nativeptr<V2f> =
        let result = Hexa.NET.ImGui.ImGui.ImVec2()
        NativePtr.cast result

    static member ImVec2(x: float32, y: float32) : nativeptr<V2f> =
        let result = Hexa.NET.ImGui.ImGui.ImVec2(x, y)
        NativePtr.cast result


    static member ImVec4() : nativeptr<V4f> =
        let result = Hexa.NET.ImGui.ImGui.ImVec4()
        NativePtr.cast result

    static member ImVec4(x: float32, y: float32, z: float32, w: float32) : nativeptr<V4f> =
        let result = Hexa.NET.ImGui.ImGui.ImVec4(x, y, z, w)
        NativePtr.cast result


    static member ImVectorImWcharCreate() : nativeptr<ImVector<uint32>> =
        Hexa.NET.ImGui.ImGui.ImVectorImWcharCreate()


    static member ImVectorImWcharDestroy(self: byref<ImVector<uint32>>) : unit =
        Hexa.NET.ImGui.ImGui.ImVectorImWcharDestroy(&&self)


    static member ImVectorImWcharInit(p: byref<ImVector<uint32>>) : unit =
        Hexa.NET.ImGui.ImGui.ImVectorImWcharInit(&&p)


    static member ImVectorImWcharUnInit(p: byref<ImVector<uint32>>) : unit =
        Hexa.NET.ImGui.ImGui.ImVectorImWcharUnInit(&&p)


    static member Image(texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f) : unit =
        Hexa.NET.ImGui.ImGui.Image(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1))

    static member Image(texRef: ImTextureRef, imageSize: V2f, uv0: V2f) : unit =
        Hexa.NET.ImGui.ImGui.Image(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0))

    static member Image(texRef: ImTextureRef, imageSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.Image(texRef, System.Numerics.Vector2.FromV2f(imageSize))


    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f, bgCol: V4f, tintCol: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector4.FromV4f(bgCol), System.Numerics.Vector4.FromV4f(tintCol))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f, bgCol: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector4.FromV4f(bgCol))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, uv0: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, uv0: V2f, bgCol: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector4.FromV4f(bgCol))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, bgCol: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector4.FromV4f(bgCol))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, uv0: V2f, bgCol: V4f, tintCol: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector4.FromV4f(bgCol), System.Numerics.Vector4.FromV4f(tintCol))

    static member ImageButton(strId: string, texRef: ImTextureRef, imageSize: V2f, bgCol: V4f, tintCol: V4f) : bool =
        Hexa.NET.ImGui.ImGui.ImageButton(strId, texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector4.FromV4f(bgCol), System.Numerics.Vector4.FromV4f(tintCol))


    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f, bgCol: V4f, tintCol: V4f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector4.FromV4f(bgCol), System.Numerics.Vector4.FromV4f(tintCol))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f, bgCol: V4f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1), System.Numerics.Vector4.FromV4f(bgCol))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, uv0: V2f, uv1: V2f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector2.FromV2f(uv1))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, uv0: V2f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, uv0: V2f, bgCol: V4f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector4.FromV4f(bgCol))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, bgCol: V4f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector4.FromV4f(bgCol))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, uv0: V2f, bgCol: V4f, tintCol: V4f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector2.FromV2f(uv0), System.Numerics.Vector4.FromV4f(bgCol), System.Numerics.Vector4.FromV4f(tintCol))

    static member ImageWithBg(texRef: ImTextureRef, imageSize: V2f, bgCol: V4f, tintCol: V4f) : unit =
        Hexa.NET.ImGui.ImGui.ImageWithBg(texRef, System.Numerics.Vector2.FromV2f(imageSize), System.Numerics.Vector4.FromV4f(bgCol), System.Numerics.Vector4.FromV4f(tintCol))


    static member IncludeItemByIndex(self: ImGuiListClipperPtr, itemIndex: int32) : unit =
        Hexa.NET.ImGui.ImGui.IncludeItemByIndex(self, itemIndex)


    static member IncludeItemsByIndex(self: ImGuiListClipperPtr, itemBegin: int32, itemEnd: int32) : unit =
        Hexa.NET.ImGui.ImGui.IncludeItemsByIndex(self, itemBegin, itemEnd)


    static member Indent(indentW: float32) : unit =
        Hexa.NET.ImGui.ImGui.Indent(indentW)

    static member Indent() : unit =
        Hexa.NET.ImGui.ImGui.Indent()


    static member InitApi(context: INativeContext) : unit =
        Hexa.NET.ImGui.ImGui.InitApi(context)


    static member InputDouble(label: string, v: byref<float>, step: float, stepFast: float, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, stepFast, format, flags)

    static member InputDouble(label: string, v: cval<float>, step: float, stepFast: float, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, stepFast, format, flags)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float, stepFast: float, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, stepFast, format)

    static member InputDouble(label: string, v: cval<float>, step: float, stepFast: float, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, stepFast, format)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float, stepFast: float) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, stepFast)

    static member InputDouble(label: string, v: cval<float>, step: float, stepFast: float) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, stepFast)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step)

    static member InputDouble(label: string, v: cval<float>, step: float) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v)

    static member InputDouble(label: string, v: cval<float>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, format)

    static member InputDouble(label: string, v: cval<float>, step: float, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, format)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, format)

    static member InputDouble(label: string, v: cval<float>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, format)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float, stepFast: float, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, stepFast, flags)

    static member InputDouble(label: string, v: cval<float>, step: float, stepFast: float, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, stepFast, flags)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, flags)

    static member InputDouble(label: string, v: cval<float>, step: float, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, flags)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, flags)

    static member InputDouble(label: string, v: cval<float>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, flags)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, step: float, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, step, format, flags)

    static member InputDouble(label: string, v: cval<float>, step: float, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, step, format, flags)
        if result then
            v.Value <- vState

    static member InputDouble(label: string, v: byref<float>, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputDouble(label, &&v, format, flags)

    static member InputDouble(label: string, v: cval<float>, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputDouble(label, &&vState, format, flags)
        if result then
            v.Value <- vState


    static member InputFloat(label: string, v: byref<float32>, step: float32, stepFast: float32, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, stepFast, format, flags)

    static member InputFloat(label: string, v: cval<float32>, step: float32, stepFast: float32, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, stepFast, format, flags)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32, stepFast: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, stepFast, format)

    static member InputFloat(label: string, v: cval<float32>, step: float32, stepFast: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, stepFast, format)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32, stepFast: float32) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, stepFast)

    static member InputFloat(label: string, v: cval<float32>, step: float32, stepFast: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, stepFast)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step)

    static member InputFloat(label: string, v: cval<float32>, step: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v)

    static member InputFloat(label: string, v: cval<float32>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, format)

    static member InputFloat(label: string, v: cval<float32>, step: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, format)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, format)

    static member InputFloat(label: string, v: cval<float32>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, format)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32, stepFast: float32, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, stepFast, flags)

    static member InputFloat(label: string, v: cval<float32>, step: float32, stepFast: float32, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, stepFast, flags)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, flags)

    static member InputFloat(label: string, v: cval<float32>, step: float32, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, flags)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, flags)

    static member InputFloat(label: string, v: cval<float32>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, flags)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, step: float32, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, step, format, flags)

    static member InputFloat(label: string, v: cval<float32>, step: float32, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, step, format, flags)
        if result then
            v.Value <- vState

    static member InputFloat(label: string, v: byref<float32>, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat(label, &&v, format, flags)

    static member InputFloat(label: string, v: cval<float32>, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat(label, &&vState, format, flags)
        if result then
            v.Value <- vState


    static member InputFloat2(label: string, v: byref<V2f>, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&v, format, flags)

    static member InputFloat2(label: string, v: cval<V2f>, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&vState, format, flags)
        if result then
            v.Value <- vState

    static member InputFloat2(label: string, v: byref<V2f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&v, format)

    static member InputFloat2(label: string, v: cval<V2f>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&vState, format)
        if result then
            v.Value <- vState

    static member InputFloat2(label: string, v: byref<V2f>) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&v)

    static member InputFloat2(label: string, v: cval<V2f>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&vState)
        if result then
            v.Value <- vState

    static member InputFloat2(label: string, v: byref<V2f>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&v, flags)

    static member InputFloat2(label: string, v: cval<V2f>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat2(label, NativePtr.cast<_, float32> &&vState, flags)
        if result then
            v.Value <- vState


    static member InputFloat3(label: string, v: byref<V3f>, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&v, format, flags)

    static member InputFloat3(label: string, v: cval<V3f>, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&vState, format, flags)
        if result then
            v.Value <- vState

    static member InputFloat3(label: string, v: byref<V3f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&v, format)

    static member InputFloat3(label: string, v: cval<V3f>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&vState, format)
        if result then
            v.Value <- vState

    static member InputFloat3(label: string, v: byref<V3f>) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&v)

    static member InputFloat3(label: string, v: cval<V3f>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&vState)
        if result then
            v.Value <- vState

    static member InputFloat3(label: string, v: byref<V3f>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&v, flags)

    static member InputFloat3(label: string, v: cval<V3f>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat3(label, NativePtr.cast<_, float32> &&vState, flags)
        if result then
            v.Value <- vState


    static member InputFloat4(label: string, v: byref<V4f>, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&v, format, flags)

    static member InputFloat4(label: string, v: cval<V4f>, format: string, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&vState, format, flags)
        if result then
            v.Value <- vState

    static member InputFloat4(label: string, v: byref<V4f>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&v, format)

    static member InputFloat4(label: string, v: cval<V4f>, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&vState, format)
        if result then
            v.Value <- vState

    static member InputFloat4(label: string, v: byref<V4f>) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&v)

    static member InputFloat4(label: string, v: cval<V4f>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&vState)
        if result then
            v.Value <- vState

    static member InputFloat4(label: string, v: byref<V4f>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&v, flags)

    static member InputFloat4(label: string, v: cval<V4f>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputFloat4(label, NativePtr.cast<_, float32> &&vState, flags)
        if result then
            v.Value <- vState


    static member InputInt(label: string, v: byref<int32>, step: int32, stepFast: int32, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputInt(label, &&v, step, stepFast, flags)

    static member InputInt(label: string, v: cval<int32>, step: int32, stepFast: int32, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt(label, &&vState, step, stepFast, flags)
        if result then
            v.Value <- vState

    static member InputInt(label: string, v: byref<int32>, step: int32, stepFast: int32) : bool =
        Hexa.NET.ImGui.ImGui.InputInt(label, &&v, step, stepFast)

    static member InputInt(label: string, v: cval<int32>, step: int32, stepFast: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt(label, &&vState, step, stepFast)
        if result then
            v.Value <- vState

    static member InputInt(label: string, v: byref<int32>, step: int32) : bool =
        Hexa.NET.ImGui.ImGui.InputInt(label, &&v, step)

    static member InputInt(label: string, v: cval<int32>, step: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt(label, &&vState, step)
        if result then
            v.Value <- vState

    static member InputInt(label: string, v: byref<int32>) : bool =
        Hexa.NET.ImGui.ImGui.InputInt(label, &&v)

    static member InputInt(label: string, v: cval<int32>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt(label, &&vState)
        if result then
            v.Value <- vState

    static member InputInt(label: string, v: byref<int32>, step: int32, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputInt(label, &&v, step, flags)

    static member InputInt(label: string, v: cval<int32>, step: int32, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt(label, &&vState, step, flags)
        if result then
            v.Value <- vState

    static member InputInt(label: string, v: byref<int32>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputInt(label, &&v, flags)

    static member InputInt(label: string, v: cval<int32>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt(label, &&vState, flags)
        if result then
            v.Value <- vState


    static member InputInt2(label: string, v: byref<V2i>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputInt2(label, NativePtr.cast<_, int32> &&v, flags)

    static member InputInt2(label: string, v: cval<V2i>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt2(label, NativePtr.cast<_, int32> &&vState, flags)
        if result then
            v.Value <- vState

    static member InputInt2(label: string, v: byref<V2i>) : bool =
        Hexa.NET.ImGui.ImGui.InputInt2(label, NativePtr.cast<_, int32> &&v)

    static member InputInt2(label: string, v: cval<V2i>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt2(label, NativePtr.cast<_, int32> &&vState)
        if result then
            v.Value <- vState


    static member InputInt3(label: string, v: byref<V3i>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputInt3(label, NativePtr.cast<_, int32> &&v, flags)

    static member InputInt3(label: string, v: cval<V3i>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt3(label, NativePtr.cast<_, int32> &&vState, flags)
        if result then
            v.Value <- vState

    static member InputInt3(label: string, v: byref<V3i>) : bool =
        Hexa.NET.ImGui.ImGui.InputInt3(label, NativePtr.cast<_, int32> &&v)

    static member InputInt3(label: string, v: cval<V3i>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt3(label, NativePtr.cast<_, int32> &&vState)
        if result then
            v.Value <- vState


    static member InputInt4(label: string, v: byref<V4i>, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputInt4(label, NativePtr.cast<_, int32> &&v, flags)

    static member InputInt4(label: string, v: cval<V4i>, flags: ImGuiInputTextFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt4(label, NativePtr.cast<_, int32> &&vState, flags)
        if result then
            v.Value <- vState

    static member InputInt4(label: string, v: byref<V4i>) : bool =
        Hexa.NET.ImGui.ImGui.InputInt4(label, NativePtr.cast<_, int32> &&v)

    static member InputInt4(label: string, v: cval<V4i>) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.InputInt4(label, NativePtr.cast<_, int32> &&vState)
        if result then
            v.Value <- vState


    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, pStepFast: voidptr, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, pStepFast, format, flags)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, pStepFast: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, pStepFast, format)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, pStepFast: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, pStepFast)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, format)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, format)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, pStepFast: voidptr, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, pStepFast, flags)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, flags)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, flags)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pStep: voidptr, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, pStep, format, flags)

    static member InputScalar(label: string, dataType: ImGuiDataType, pData: voidptr, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalar(label, dataType, pData, format, flags)


    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, pStepFast: voidptr, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, pStepFast, format, flags)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, pStepFast: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, pStepFast, format)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, pStepFast: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, pStepFast)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, format)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, format)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, pStepFast: voidptr, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, pStepFast, flags)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, flags)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, flags)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pStep: voidptr, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, pStep, format, flags)

    static member InputScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, format: string, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.InputScalarN(label, dataType, pData, components, format, flags)


    static member InputText(label: string, text: byref<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>, flags: ImGuiInputTextFlags) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, flags: ImGuiInputTextFlags) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>, flags: ImGuiInputTextFlags, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, flags: ImGuiInputTextFlags, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputText(label: string, text: byref<string>, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputText(label: string, text: cval<string>, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputText(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text


    static member InputTextEx(label: string, hint: string, text: byref<string>, sizeArg: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextEx(label: string, hint: string, text: cval<string>, sizeArg: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextEx(label: string, hint: string, text: byref<string>, sizeArg: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextEx(label: string, hint: string, text: cval<string>, sizeArg: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextEx(label: string, hint: string, text: byref<string>, sizeArg: V2f, flags: ImGuiInputTextFlags) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextEx(label: string, hint: string, text: cval<string>, sizeArg: V2f, flags: ImGuiInputTextFlags) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextEx(label: string, hint: string, text: byref<string>, sizeArg: V2f, flags: ImGuiInputTextFlags, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextEx(label: string, hint: string, text: cval<string>, sizeArg: V2f, flags: ImGuiInputTextFlags, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextEx(label, hint, TextBuffer.Shared.Handle, TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(sizeArg), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text


    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, flags: ImGuiInputTextFlags) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, flags: ImGuiInputTextFlags) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size))
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size))
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, flags: ImGuiInputTextFlags) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, flags: ImGuiInputTextFlags) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, flags: ImGuiInputTextFlags, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, flags: ImGuiInputTextFlags, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, flags: ImGuiInputTextFlags, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, flags: ImGuiInputTextFlags, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, size: V2f, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, size: V2f, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, System.Numerics.Vector2.FromV2f(size), callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextMultiline(label: string, text: byref<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextMultiline(label: string, text: cval<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextMultiline(label, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text


    static member InputTextWithHint(label: string, hint: string, text: byref<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, flags: ImGuiInputTextFlags, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>, flags: ImGuiInputTextFlags) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, flags: ImGuiInputTextFlags) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>, callback: ImGuiInputTextCallback) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, callback: ImGuiInputTextCallback) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>, flags: ImGuiInputTextFlags, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, flags: ImGuiInputTextFlags, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, flags ||| ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>, userData: voidptr) : bool =
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, userData: voidptr) : unit =
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, TextBuffer.Shared.InputTextResizeCallback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text

    static member InputTextWithHint(label: string, hint: string, text: byref<string>, callback: ImGuiInputTextCallback, userData: voidptr) : bool =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text <- TextBuffer.Shared.Text
        result

    static member InputTextWithHint(label: string, hint: string, text: cval<string>, callback: ImGuiInputTextCallback, userData: voidptr) : unit =
        let callback data =
            TextBuffer.Shared.InputTextResizeCallback data |> ignore
            callback.Invoke data
        TextBuffer.Shared.Text <- text.Value
        let result = Hexa.NET.ImGui.ImGui.InputTextWithHint(label, hint, TextBuffer.Shared.Handle, unativeint TextBuffer.Shared.Size, ImGuiInputTextFlags.CallbackResize, callback, userData)
        if result then
            text.Value <- TextBuffer.Shared.Text


    static member InsertChars(self: ImGuiInputTextCallbackDataPtr, pos: int32, text: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.InsertChars(self, pos, text, textEnd)

    static member InsertChars(self: ImGuiInputTextCallbackDataPtr, pos: int32, text: string) : unit =
        Hexa.NET.ImGui.ImGui.InsertChars(self, pos, text)


    static member InvisibleButton(strId: string, size: V2f, flags: ImGuiButtonFlags) : bool =
        Hexa.NET.ImGui.ImGui.InvisibleButton(strId, System.Numerics.Vector2.FromV2f(size), flags)

    static member InvisibleButton(strId: string, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.InvisibleButton(strId, System.Numerics.Vector2.FromV2f(size))


    static member IsActive(self: ImGuiTextFilterPtr) : bool =
        Hexa.NET.ImGui.ImGui.IsActive(self)


    static member IsAnyItemActive() : bool =
        Hexa.NET.ImGui.ImGui.IsAnyItemActive()


    static member IsAnyItemFocused() : bool =
        Hexa.NET.ImGui.ImGui.IsAnyItemFocused()


    static member IsAnyItemHovered() : bool =
        Hexa.NET.ImGui.ImGui.IsAnyItemHovered()


    static member IsAnyMouseDown() : bool =
        Hexa.NET.ImGui.ImGui.IsAnyMouseDown()


    static member IsDataType(self: ImGuiPayloadPtr, typ: nativeptr<uint8>) : bool =
        Hexa.NET.ImGui.ImGui.IsDataType(self, typ)


    static member IsDelivery(self: ImGuiPayloadPtr) : bool =
        Hexa.NET.ImGui.ImGui.IsDelivery(self)


    static member IsGlyphInFont(self: ImFontPtr, c: uint32) : bool =
        Hexa.NET.ImGui.ImGui.IsGlyphInFont(self, c)


    static member IsGlyphLoaded(self: ImFontBakedPtr, c: uint32) : bool =
        Hexa.NET.ImGui.ImGui.IsGlyphLoaded(self, c)


    static member IsGlyphRangeUnused(self: ImFontPtr, cBegin: uint32, cLast: uint32) : bool =
        Hexa.NET.ImGui.ImGui.IsGlyphRangeUnused(self, cBegin, cLast)


    static member IsItemActivated() : bool =
        Hexa.NET.ImGui.ImGui.IsItemActivated()


    static member IsItemActive() : bool =
        Hexa.NET.ImGui.ImGui.IsItemActive()


    static member IsItemClicked(mouseButton: ImGuiMouseButton) : bool =
        Hexa.NET.ImGui.ImGui.IsItemClicked(mouseButton)

    static member IsItemClicked() : bool =
        Hexa.NET.ImGui.ImGui.IsItemClicked()


    static member IsItemDeactivated() : bool =
        Hexa.NET.ImGui.ImGui.IsItemDeactivated()


    static member IsItemDeactivatedAfterEdit() : bool =
        Hexa.NET.ImGui.ImGui.IsItemDeactivatedAfterEdit()


    static member IsItemEdited() : bool =
        Hexa.NET.ImGui.ImGui.IsItemEdited()


    static member IsItemFocused() : bool =
        Hexa.NET.ImGui.ImGui.IsItemFocused()


    static member IsItemHovered(flags: ImGuiHoveredFlags) : bool =
        Hexa.NET.ImGui.ImGui.IsItemHovered(flags)

    static member IsItemHovered() : bool =
        Hexa.NET.ImGui.ImGui.IsItemHovered()


    static member IsItemToggledOpen() : bool =
        Hexa.NET.ImGui.ImGui.IsItemToggledOpen()


    static member IsItemToggledSelection() : bool =
        Hexa.NET.ImGui.ImGui.IsItemToggledSelection()


    static member IsItemVisible() : bool =
        Hexa.NET.ImGui.ImGui.IsItemVisible()


    static member IsKeyChordPressed(keyChord: int32) : bool =
        Hexa.NET.ImGui.ImGui.IsKeyChordPressed(keyChord)


    static member IsKeyDown(key: ImGuiKey) : bool =
        Hexa.NET.ImGui.ImGui.IsKeyDown(key)


    static member IsKeyPressed(key: ImGuiKey, repeat: bool) : bool =
        Hexa.NET.ImGui.ImGui.IsKeyPressed(key, repeat)

    static member IsKeyPressed(key: ImGuiKey) : bool =
        Hexa.NET.ImGui.ImGui.IsKeyPressed(key)


    static member IsKeyReleased(key: ImGuiKey) : bool =
        Hexa.NET.ImGui.ImGui.IsKeyReleased(key)


    static member IsLoaded(self: ImFontPtr) : bool =
        Hexa.NET.ImGui.ImGui.IsLoaded(self)


    static member IsMouseClicked(button: ImGuiMouseButton, repeat: bool) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseClicked(button, repeat)

    static member IsMouseClicked(button: ImGuiMouseButton) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseClicked(button)


    static member IsMouseDoubleClicked(button: ImGuiMouseButton) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseDoubleClicked(button)


    static member IsMouseDown(button: ImGuiMouseButton) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseDown(button)


    static member IsMouseDragging(button: ImGuiMouseButton, lockThreshold: float32) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseDragging(button, lockThreshold)

    static member IsMouseDragging(button: ImGuiMouseButton) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseDragging(button)


    static member IsMouseHoveringRect(rMin: V2f, rMax: V2f, clip: bool) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseHoveringRect(System.Numerics.Vector2.FromV2f(rMin), System.Numerics.Vector2.FromV2f(rMax), clip)

    static member IsMouseHoveringRect(rMin: V2f, rMax: V2f) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseHoveringRect(System.Numerics.Vector2.FromV2f(rMin), System.Numerics.Vector2.FromV2f(rMax))


    static member IsMousePosValid(mousePos: byref<V2f>) : bool =
        Hexa.NET.ImGui.ImGui.IsMousePosValid(NativePtr.cast<_, System.Numerics.Vector2> &&mousePos)

    static member IsMousePosValid() : bool =
        Hexa.NET.ImGui.ImGui.IsMousePosValid()


    static member IsMouseReleased(button: ImGuiMouseButton) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseReleased(button)


    static member IsMouseReleasedWithDelay(button: ImGuiMouseButton, delay: float32) : bool =
        Hexa.NET.ImGui.ImGui.IsMouseReleasedWithDelay(button, delay)


    static member IsPopupOpen(strId: string, flags: ImGuiPopupFlags) : bool =
        Hexa.NET.ImGui.ImGui.IsPopupOpen(strId, flags)

    static member IsPopupOpen(strId: string) : bool =
        Hexa.NET.ImGui.ImGui.IsPopupOpen(strId)


    static member IsPreview(self: ImGuiPayloadPtr) : bool =
        Hexa.NET.ImGui.ImGui.IsPreview(self)


    static member IsRectVisible(size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.IsRectVisible(System.Numerics.Vector2.FromV2f(size))

    static member IsRectVisible(rectMin: V2f, rectMax: V2f) : bool =
        Hexa.NET.ImGui.ImGui.IsRectVisible(System.Numerics.Vector2.FromV2f(rectMin), System.Numerics.Vector2.FromV2f(rectMax))


    static member IsWindowAppearing() : bool =
        Hexa.NET.ImGui.ImGui.IsWindowAppearing()


    static member IsWindowCollapsed() : bool =
        Hexa.NET.ImGui.ImGui.IsWindowCollapsed()


    static member IsWindowDocked() : bool =
        Hexa.NET.ImGui.ImGui.IsWindowDocked()


    static member IsWindowFocused(flags: ImGuiFocusedFlags) : bool =
        Hexa.NET.ImGui.ImGui.IsWindowFocused(flags)

    static member IsWindowFocused() : bool =
        Hexa.NET.ImGui.ImGui.IsWindowFocused()


    static member IsWindowHovered(flags: ImGuiHoveredFlags) : bool =
        Hexa.NET.ImGui.ImGui.IsWindowHovered(flags)

    static member IsWindowHovered() : bool =
        Hexa.NET.ImGui.ImGui.IsWindowHovered()


    static member LabelText(label: string, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.LabelText(label, fmt)


    static member LabelTextV(label: string, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.LabelTextV(label, fmt, args)


    static member ListBox(label: string, currentItem: byref<int32>, items: string[], heightInItems: int32) : bool =
        Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItem, items, items.Length, heightInItems)

    static member ListBox(label: string, currentItem: cval<int32>, items: string[], heightInItems: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItemState, items, items.Length, heightInItems)
        if result then
            currentItem.Value <- currentItemState

    static member ListBox(label: string, currentItem: byref<int32>, items: string[]) : bool =
        Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItem, items, items.Length)

    static member ListBox(label: string, currentItem: cval<int32>, items: string[]) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItemState, items, items.Length)
        if result then
            currentItem.Value <- currentItemState

    static member ListBox(label: string, currentItem: byref<int32>, getter: nativeint, userData: voidptr, itemsCount: int32, heightInItems: int32) : bool =
        Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItem, getter, userData, itemsCount, heightInItems)

    static member ListBox(label: string, currentItem: cval<int32>, getter: nativeint, userData: voidptr, itemsCount: int32, heightInItems: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItemState, getter, userData, itemsCount, heightInItems)
        if result then
            currentItem.Value <- currentItemState

    static member ListBox(label: string, currentItem: byref<int32>, getter: nativeint, userData: voidptr, itemsCount: int32) : bool =
        Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItem, getter, userData, itemsCount)

    static member ListBox(label: string, currentItem: cval<int32>, getter: nativeint, userData: voidptr, itemsCount: int32) : unit =
        let mutable currentItemState = currentItem.Value
        let result = Hexa.NET.ImGui.ImGui.ListBox(label, &&currentItemState, getter, userData, itemsCount)
        if result then
            currentItem.Value <- currentItemState


    static member LoadIniSettingsFromDisk(iniFilename: string) : unit =
        Hexa.NET.ImGui.ImGui.LoadIniSettingsFromDisk(iniFilename)


    static member LoadIniSettingsFromMemory(iniData: string, iniSize: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.LoadIniSettingsFromMemory(iniData, iniSize)

    static member LoadIniSettingsFromMemory(iniData: string) : unit =
        Hexa.NET.ImGui.ImGui.LoadIniSettingsFromMemory(iniData)


    static member LogButtons() : unit =
        Hexa.NET.ImGui.ImGui.LogButtons()


    static member LogFinish() : unit =
        Hexa.NET.ImGui.ImGui.LogFinish()


    static member LogText(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.LogText(fmt)


    static member LogTextV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.LogTextV(fmt, args)


    static member LogToClipboard(autoOpenDepth: int32) : unit =
        Hexa.NET.ImGui.ImGui.LogToClipboard(autoOpenDepth)

    static member LogToClipboard() : unit =
        Hexa.NET.ImGui.ImGui.LogToClipboard()


    static member LogToFile(autoOpenDepth: int32, filename: string) : unit =
        Hexa.NET.ImGui.ImGui.LogToFile(autoOpenDepth, filename)

    static member LogToFile(autoOpenDepth: int32) : unit =
        Hexa.NET.ImGui.ImGui.LogToFile(autoOpenDepth)

    static member LogToFile() : unit =
        Hexa.NET.ImGui.ImGui.LogToFile()

    static member LogToFile(filename: string) : unit =
        Hexa.NET.ImGui.ImGui.LogToFile(filename)


    static member LogToTTY(autoOpenDepth: int32) : unit =
        Hexa.NET.ImGui.ImGui.LogToTTY(autoOpenDepth)

    static member LogToTTY() : unit =
        Hexa.NET.ImGui.ImGui.LogToTTY()


    static member MemAlloc(size: unativeint) : voidptr =
        Hexa.NET.ImGui.ImGui.MemAlloc(size)


    static member MemFree(ptr: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.MemFree(ptr)


    static member MenuItem(label: string, shortcut: string, selected: bool, enabled: bool) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut, selected, enabled)

    static member MenuItem(label: string, shortcut: string, selected: bool) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut, selected)

    static member MenuItem(label: string, shortcut: string) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut)

    static member MenuItem(label: string) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label)

    static member MenuItem(label: string, selected: bool) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, selected)

    static member MenuItem(label: string, selected: bool, enabled: bool) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, selected, enabled)

    static member MenuItem(label: string, shortcut: string, pSelected: byref<bool>, enabled: bool) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut, &&pSelected, enabled)

    static member MenuItem(label: string, shortcut: string, pSelected: cval<bool>, enabled: bool) : unit =
        let mutable pSelectedState = pSelected.Value
        let result = Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut, &&pSelectedState, enabled)
        if result then
            pSelected.Value <- pSelectedState

    static member MenuItem(label: string, shortcut: string, pSelected: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut, &&pSelected)

    static member MenuItem(label: string, shortcut: string, pSelected: cval<bool>) : unit =
        let mutable pSelectedState = pSelected.Value
        let result = Hexa.NET.ImGui.ImGui.MenuItem(label, shortcut, &&pSelectedState)
        if result then
            pSelected.Value <- pSelectedState


    static member Merge(self: ImDrawListSplitterPtr, drawList: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.Merge(self, drawList)


    static member NewFrame() : unit =
        Hexa.NET.ImGui.ImGui.NewFrame()


    static member NewLine() : unit =
        Hexa.NET.ImGui.ImGui.NewLine()


    static member NextColumn() : unit =
        Hexa.NET.ImGui.ImGui.NextColumn()


    static member OpenPopup(strId: string, popupFlags: ImGuiPopupFlags) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopup(strId, popupFlags)

    static member OpenPopup(strId: string) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopup(strId)

    static member OpenPopup(id: uint32, popupFlags: ImGuiPopupFlags) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopup(id, popupFlags)

    static member OpenPopup(id: uint32) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopup(id)


    static member OpenPopupOnItemClick(strId: string, popupFlags: ImGuiPopupFlags) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopupOnItemClick(strId, popupFlags)

    static member OpenPopupOnItemClick(strId: string) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopupOnItemClick(strId)

    static member OpenPopupOnItemClick() : unit =
        Hexa.NET.ImGui.ImGui.OpenPopupOnItemClick()

    static member OpenPopupOnItemClick(popupFlags: ImGuiPopupFlags) : unit =
        Hexa.NET.ImGui.ImGui.OpenPopupOnItemClick(popupFlags)


    static member PassFilter(self: ImGuiTextFilterPtr, text: string, textEnd: string) : bool =
        Hexa.NET.ImGui.ImGui.PassFilter(self, text, textEnd)

    static member PassFilter(self: ImGuiTextFilterPtr, text: string) : bool =
        Hexa.NET.ImGui.ImGui.PassFilter(self, text)


    static member PathArcTo(self: ImDrawListPtr, center: V2f, radius: float32, aMin: float32, aMax: float32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.PathArcTo(self, System.Numerics.Vector2.FromV2f(center), radius, aMin, aMax, numSegments)

    static member PathArcTo(self: ImDrawListPtr, center: V2f, radius: float32, aMin: float32, aMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PathArcTo(self, System.Numerics.Vector2.FromV2f(center), radius, aMin, aMax)


    static member PathArcToFast(self: ImDrawListPtr, center: V2f, radius: float32, aMinOf12: int32, aMaxOf12: int32) : unit =
        Hexa.NET.ImGui.ImGui.PathArcToFast(self, System.Numerics.Vector2.FromV2f(center), radius, aMinOf12, aMaxOf12)


    static member PathBezierCubicCurveTo(self: ImDrawListPtr, p2: V2f, p3: V2f, p4: V2f, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.PathBezierCubicCurveTo(self, System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4), numSegments)

    static member PathBezierCubicCurveTo(self: ImDrawListPtr, p2: V2f, p3: V2f, p4: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PathBezierCubicCurveTo(self, System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), System.Numerics.Vector2.FromV2f(p4))


    static member PathBezierQuadraticCurveTo(self: ImDrawListPtr, p2: V2f, p3: V2f, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.PathBezierQuadraticCurveTo(self, System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3), numSegments)

    static member PathBezierQuadraticCurveTo(self: ImDrawListPtr, p2: V2f, p3: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PathBezierQuadraticCurveTo(self, System.Numerics.Vector2.FromV2f(p2), System.Numerics.Vector2.FromV2f(p3))


    static member PathClear(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.PathClear(self)


    static member PathEllipticalArcTo(self: ImDrawListPtr, center: V2f, radius: V2f, rot: float32, aMin: float32, aMax: float32, numSegments: int32) : unit =
        Hexa.NET.ImGui.ImGui.PathEllipticalArcTo(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), rot, aMin, aMax, numSegments)

    static member PathEllipticalArcTo(self: ImDrawListPtr, center: V2f, radius: V2f, rot: float32, aMin: float32, aMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PathEllipticalArcTo(self, System.Numerics.Vector2.FromV2f(center), System.Numerics.Vector2.FromV2f(radius), rot, aMin, aMax)


    static member PathFillConcave(self: ImDrawListPtr, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PathFillConcave(self, col)


    static member PathFillConvex(self: ImDrawListPtr, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PathFillConvex(self, col)


    static member PathLineTo(self: ImDrawListPtr, pos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PathLineTo(self, System.Numerics.Vector2.FromV2f(pos))


    static member PathLineToMergeDuplicate(self: ImDrawListPtr, pos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PathLineToMergeDuplicate(self, System.Numerics.Vector2.FromV2f(pos))


    static member PathRect(self: ImDrawListPtr, rectMin: V2f, rectMax: V2f, rounding: float32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.PathRect(self, System.Numerics.Vector2.FromV2f(rectMin), System.Numerics.Vector2.FromV2f(rectMax), rounding, flags)

    static member PathRect(self: ImDrawListPtr, rectMin: V2f, rectMax: V2f, rounding: float32) : unit =
        Hexa.NET.ImGui.ImGui.PathRect(self, System.Numerics.Vector2.FromV2f(rectMin), System.Numerics.Vector2.FromV2f(rectMax), rounding)

    static member PathRect(self: ImDrawListPtr, rectMin: V2f, rectMax: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PathRect(self, System.Numerics.Vector2.FromV2f(rectMin), System.Numerics.Vector2.FromV2f(rectMax))

    static member PathRect(self: ImDrawListPtr, rectMin: V2f, rectMax: V2f, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.PathRect(self, System.Numerics.Vector2.FromV2f(rectMin), System.Numerics.Vector2.FromV2f(rectMax), flags)


    static member PathStroke(self: ImDrawListPtr, col: uint32, flags: ImDrawFlags, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.PathStroke(self, col, flags, thickness)

    static member PathStroke(self: ImDrawListPtr, col: uint32, flags: ImDrawFlags) : unit =
        Hexa.NET.ImGui.ImGui.PathStroke(self, col, flags)

    static member PathStroke(self: ImDrawListPtr, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PathStroke(self, col)

    static member PathStroke(self: ImDrawListPtr, col: uint32, thickness: float32) : unit =
        Hexa.NET.ImGui.ImGui.PathStroke(self, col, thickness)


    static member PlatformIOSetPlatformGetWindowPos(platformIo: ImGuiPlatformIOPtr, userCallback: nativeint) : unit =
        Hexa.NET.ImGui.ImGui.PlatformIOSetPlatformGetWindowPos(platformIo, userCallback)


    static member PlatformIOSetPlatformGetWindowSize(platformIo: ImGuiPlatformIOPtr, userCallback: nativeint) : unit =
        Hexa.NET.ImGui.ImGui.PlatformIOSetPlatformGetWindowSize(platformIo, userCallback)


    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset)

    static member PlotHistogram(label: string, values: float32[]) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length)

    static member PlotHistogram(label: string, values: float32[], overlayText: string) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin)

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax)

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, scaleMax)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], overlayText: string, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax, stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, stride)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, stride)

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, stride)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax, stride)

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, scaleMax, stride)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax, stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, overlayText: string, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, overlayText, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, overlayText)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, overlayText)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, scaleMin)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, overlayText, scaleMin)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin, scaleMax)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, scaleMin, scaleMax)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, overlayText, scaleMin, scaleMax)

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotHistogram(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotHistogram(label, valuesGetter, data, valuesCount, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))


    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset)

    static member PlotLines(label: string, values: float32[]) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length)

    static member PlotLines(label: string, values: float32[], overlayText: string) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin)

    static member PlotLines(label: string, values: float32[], scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin)

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax)

    static member PlotLines(label: string, values: float32[], scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, scaleMax)

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], overlayText: string, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, scaleMax, stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, stride)

    static member PlotLines(label: string, values: float32[], overlayText: string, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, stride)

    static member PlotLines(label: string, values: float32[], scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, stride)

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax, stride)

    static member PlotLines(label: string, values: float32[], scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, scaleMax, stride)

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax, stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, overlayText: string, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, overlayText, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], overlayText: string, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], valuesOffset: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, valuesOffset, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, values: float32[], overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f, stride: int32) : unit =
        use valuesPinned = fixed values
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesPinned, values.Length, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize), stride)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin, scaleMax)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, overlayText)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, overlayText)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, scaleMin)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, overlayText, scaleMin)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin, scaleMax)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, scaleMin, scaleMax)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32, scaleMax: float32) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, overlayText, scaleMin, scaleMax)

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, overlayText: string, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, overlayText, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, overlayText, scaleMin, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, valuesOffset: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, valuesOffset, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))

    static member PlotLines(label: string, valuesGetter: nativeint, data: voidptr, valuesCount: int32, overlayText: string, scaleMin: float32, scaleMax: float32, graphSize: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PlotLines(label, valuesGetter, data, valuesCount, overlayText, scaleMin, scaleMax, System.Numerics.Vector2.FromV2f(graphSize))


    static member PopClipRect() : unit =
        Hexa.NET.ImGui.ImGui.PopClipRect()

    static member PopClipRect(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.PopClipRect(self)


    static member PopFont() : unit =
        Hexa.NET.ImGui.ImGui.PopFont()


    static member PopID() : unit =
        Hexa.NET.ImGui.ImGui.PopID()


    static member PopItemFlag() : unit =
        Hexa.NET.ImGui.ImGui.PopItemFlag()


    static member PopItemWidth() : unit =
        Hexa.NET.ImGui.ImGui.PopItemWidth()


    static member PopStyleColor(count: int32) : unit =
        Hexa.NET.ImGui.ImGui.PopStyleColor(count)

    static member PopStyleColor() : unit =
        Hexa.NET.ImGui.ImGui.PopStyleColor()


    static member PopStyleVar(count: int32) : unit =
        Hexa.NET.ImGui.ImGui.PopStyleVar(count)

    static member PopStyleVar() : unit =
        Hexa.NET.ImGui.ImGui.PopStyleVar()


    static member PopTextWrapPos() : unit =
        Hexa.NET.ImGui.ImGui.PopTextWrapPos()


    static member PopTexture(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.PopTexture(self)


    static member PrimQuadUV(self: ImDrawListPtr, a: V2f, b: V2f, c: V2f, d: V2f, uvA: V2f, uvB: V2f, uvC: V2f, uvD: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PrimQuadUV(self, System.Numerics.Vector2.FromV2f(a), System.Numerics.Vector2.FromV2f(b), System.Numerics.Vector2.FromV2f(c), System.Numerics.Vector2.FromV2f(d), System.Numerics.Vector2.FromV2f(uvA), System.Numerics.Vector2.FromV2f(uvB), System.Numerics.Vector2.FromV2f(uvC), System.Numerics.Vector2.FromV2f(uvD), col)


    static member PrimRect(self: ImDrawListPtr, a: V2f, b: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PrimRect(self, System.Numerics.Vector2.FromV2f(a), System.Numerics.Vector2.FromV2f(b), col)


    static member PrimRectUV(self: ImDrawListPtr, a: V2f, b: V2f, uvA: V2f, uvB: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PrimRectUV(self, System.Numerics.Vector2.FromV2f(a), System.Numerics.Vector2.FromV2f(b), System.Numerics.Vector2.FromV2f(uvA), System.Numerics.Vector2.FromV2f(uvB), col)


    static member PrimReserve(self: ImDrawListPtr, idxCount: int32, vtxCount: int32) : unit =
        Hexa.NET.ImGui.ImGui.PrimReserve(self, idxCount, vtxCount)


    static member PrimUnreserve(self: ImDrawListPtr, idxCount: int32, vtxCount: int32) : unit =
        Hexa.NET.ImGui.ImGui.PrimUnreserve(self, idxCount, vtxCount)


    static member PrimVtx(self: ImDrawListPtr, pos: V2f, uv: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PrimVtx(self, System.Numerics.Vector2.FromV2f(pos), System.Numerics.Vector2.FromV2f(uv), col)


    static member PrimWriteIdx(self: ImDrawListPtr, idx: uint16) : unit =
        Hexa.NET.ImGui.ImGui.PrimWriteIdx(self, idx)


    static member PrimWriteVtx(self: ImDrawListPtr, pos: V2f, uv: V2f, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PrimWriteVtx(self, System.Numerics.Vector2.FromV2f(pos), System.Numerics.Vector2.FromV2f(uv), col)


    static member ProgressBar(fraction: float32, sizeArg: V2f, overlay: string) : unit =
        Hexa.NET.ImGui.ImGui.ProgressBar(fraction, System.Numerics.Vector2.FromV2f(sizeArg), overlay)

    static member ProgressBar(fraction: float32, sizeArg: V2f) : unit =
        Hexa.NET.ImGui.ImGui.ProgressBar(fraction, System.Numerics.Vector2.FromV2f(sizeArg))

    static member ProgressBar(fraction: float32) : unit =
        Hexa.NET.ImGui.ImGui.ProgressBar(fraction)

    static member ProgressBar(fraction: float32, overlay: string) : unit =
        Hexa.NET.ImGui.ImGui.ProgressBar(fraction, overlay)


    static member PushClipRect(clipRectMin: V2f, clipRectMax: V2f, intersectWithCurrentClipRect: bool) : unit =
        Hexa.NET.ImGui.ImGui.PushClipRect(System.Numerics.Vector2.FromV2f(clipRectMin), System.Numerics.Vector2.FromV2f(clipRectMax), intersectWithCurrentClipRect)

    static member PushClipRect(self: ImDrawListPtr, clipRectMin: V2f, clipRectMax: V2f, intersectWithCurrentClipRect: bool) : unit =
        Hexa.NET.ImGui.ImGui.PushClipRect(self, System.Numerics.Vector2.FromV2f(clipRectMin), System.Numerics.Vector2.FromV2f(clipRectMax), intersectWithCurrentClipRect)

    static member PushClipRect(self: ImDrawListPtr, clipRectMin: V2f, clipRectMax: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PushClipRect(self, System.Numerics.Vector2.FromV2f(clipRectMin), System.Numerics.Vector2.FromV2f(clipRectMax))


    static member PushClipRectFullScreen(self: ImDrawListPtr) : unit =
        Hexa.NET.ImGui.ImGui.PushClipRectFullScreen(self)


    static member PushFont(font: ImFontPtr, fontSizeBaseUnscaled: float32) : unit =
        Hexa.NET.ImGui.ImGui.PushFont(font, fontSizeBaseUnscaled)


    static member PushID(strId: string) : unit =
        Hexa.NET.ImGui.ImGui.PushID(strId)

    static member PushID(strIdBegin: string, strIdEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.PushID(strIdBegin, strIdEnd)

    static member PushID(ptrId: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.PushID(ptrId)

    static member PushID(intId: int32) : unit =
        Hexa.NET.ImGui.ImGui.PushID(intId)


    static member PushItemFlag(option: ImGuiItemFlags, enabled: bool) : unit =
        Hexa.NET.ImGui.ImGui.PushItemFlag(option, enabled)


    static member PushItemWidth(itemWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.PushItemWidth(itemWidth)


    static member PushStyleColor(idx: ImGuiCol, col: uint32) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, col)

    static member PushStyleColor(idx: ImGuiCol, col: V4f) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, System.Numerics.Vector4.FromV4f(col))

    static member PushStyleColor(idx: ImGuiCol, col: C4b) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, System.Numerics.Vector4.FromC4b(col))

    static member PushStyleColor(idx: ImGuiCol, col: C4us) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, System.Numerics.Vector4.FromC4us(col))

    static member PushStyleColor(idx: ImGuiCol, col: C4ui) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, System.Numerics.Vector4.FromC4ui(col))

    static member PushStyleColor(idx: ImGuiCol, col: C4f) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, System.Numerics.Vector4.FromC4f(col))

    static member PushStyleColor(idx: ImGuiCol, col: C4d) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleColor(idx, System.Numerics.Vector4.FromC4d(col))


    static member PushStyleVar(idx: ImGuiStyleVar, value: float32) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleVar(idx, value)

    static member PushStyleVar(idx: ImGuiStyleVar, value: V2f) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleVar(idx, System.Numerics.Vector2.FromV2f(value))


    static member PushStyleVarX(idx: ImGuiStyleVar, valX: float32) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleVarX(idx, valX)


    static member PushStyleVarY(idx: ImGuiStyleVar, valY: float32) : unit =
        Hexa.NET.ImGui.ImGui.PushStyleVarY(idx, valY)


    static member PushTextWrapPos(wrapLocalPosX: float32) : unit =
        Hexa.NET.ImGui.ImGui.PushTextWrapPos(wrapLocalPosX)

    static member PushTextWrapPos() : unit =
        Hexa.NET.ImGui.ImGui.PushTextWrapPos()


    static member PushTexture(self: ImDrawListPtr, texRef: ImTextureRef) : unit =
        Hexa.NET.ImGui.ImGui.PushTexture(self, texRef)


    static member RadioButton(label: string, active: bool) : bool =
        Hexa.NET.ImGui.ImGui.RadioButton(label, active)

    static member RadioButton(label: string, v: byref<int32>, vButton: int32) : bool =
        Hexa.NET.ImGui.ImGui.RadioButton(label, &&v, vButton)

    static member RadioButton(label: string, v: cval<int32>, vButton: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.RadioButton(label, &&vState, vButton)
        if result then
            v.Value <- vState


    static member RemoveCustomRect(self: ImFontAtlasPtr, id: int32) : unit =
        Hexa.NET.ImGui.ImGui.RemoveCustomRect(self, id)


    static member RemoveFont(self: ImFontAtlasPtr, font: ImFontPtr) : unit =
        Hexa.NET.ImGui.ImGui.RemoveFont(self, font)


    static member Render() : unit =
        Hexa.NET.ImGui.ImGui.Render()


    static member RenderChar(self: ImFontPtr, drawList: ImDrawListPtr, size: float32, pos: V2f, col: uint32, c: uint32, cpuFineClip: byref<V4f>) : unit =
        Hexa.NET.ImGui.ImGui.RenderChar(self, drawList, size, System.Numerics.Vector2.FromV2f(pos), col, c, NativePtr.cast<_, System.Numerics.Vector4> &&cpuFineClip)

    static member RenderChar(self: ImFontPtr, drawList: ImDrawListPtr, size: float32, pos: V2f, col: uint32, c: uint32) : unit =
        Hexa.NET.ImGui.ImGui.RenderChar(self, drawList, size, System.Numerics.Vector2.FromV2f(pos), col, c)


    static member RenderPlatformWindowsDefault(platformRenderArg: voidptr, rendererRenderArg: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.RenderPlatformWindowsDefault(platformRenderArg, rendererRenderArg)

    static member RenderPlatformWindowsDefault(platformRenderArg: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.RenderPlatformWindowsDefault(platformRenderArg)

    static member RenderPlatformWindowsDefault() : unit =
        Hexa.NET.ImGui.ImGui.RenderPlatformWindowsDefault()


    static member RenderText(self: ImFontPtr, drawList: ImDrawListPtr, size: float32, pos: V2f, col: uint32, clipRect: V4f, textBegin: string, textEnd: string, wrapWidth: float32, cpuFineClip: bool) : unit =
        Hexa.NET.ImGui.ImGui.RenderText(self, drawList, size, System.Numerics.Vector2.FromV2f(pos), col, System.Numerics.Vector4.FromV4f(clipRect), textBegin, textEnd, wrapWidth, cpuFineClip)

    static member RenderText(self: ImFontPtr, drawList: ImDrawListPtr, size: float32, pos: V2f, col: uint32, clipRect: V4f, textBegin: string, textEnd: string, wrapWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.RenderText(self, drawList, size, System.Numerics.Vector2.FromV2f(pos), col, System.Numerics.Vector4.FromV4f(clipRect), textBegin, textEnd, wrapWidth)

    static member RenderText(self: ImFontPtr, drawList: ImDrawListPtr, size: float32, pos: V2f, col: uint32, clipRect: V4f, textBegin: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.RenderText(self, drawList, size, System.Numerics.Vector2.FromV2f(pos), col, System.Numerics.Vector4.FromV4f(clipRect), textBegin, textEnd)

    static member RenderText(self: ImFontPtr, drawList: ImDrawListPtr, size: float32, pos: V2f, col: uint32, clipRect: V4f, textBegin: string, textEnd: string, cpuFineClip: bool) : unit =
        Hexa.NET.ImGui.ImGui.RenderText(self, drawList, size, System.Numerics.Vector2.FromV2f(pos), col, System.Numerics.Vector4.FromV4f(clipRect), textBegin, textEnd, cpuFineClip)


    static member ResetMouseDragDelta(button: ImGuiMouseButton) : unit =
        Hexa.NET.ImGui.ImGui.ResetMouseDragDelta(button)

    static member ResetMouseDragDelta() : unit =
        Hexa.NET.ImGui.ImGui.ResetMouseDragDelta()


    static member SameLine(offsetFromStartX: float32, spacing: float32) : unit =
        Hexa.NET.ImGui.ImGui.SameLine(offsetFromStartX, spacing)

    static member SameLine(offsetFromStartX: float32) : unit =
        Hexa.NET.ImGui.ImGui.SameLine(offsetFromStartX)

    static member SameLine() : unit =
        Hexa.NET.ImGui.ImGui.SameLine()


    static member SaveIniSettingsToDisk(iniFilename: string) : unit =
        Hexa.NET.ImGui.ImGui.SaveIniSettingsToDisk(iniFilename)


    static member SaveIniSettingsToMemory(outIniSize: nativeptr<unativeint>) : string =
        Hexa.NET.ImGui.ImGui.SaveIniSettingsToMemoryS(outIniSize)

    static member SaveIniSettingsToMemory() : string =
        Hexa.NET.ImGui.ImGui.SaveIniSettingsToMemoryS()


    static member ScaleAllSizes(self: ImGuiStylePtr, scaleFactor: float32) : unit =
        Hexa.NET.ImGui.ImGui.ScaleAllSizes(self, scaleFactor)


    static member ScaleClipRects(self: ImDrawDataPtr, fbScale: V2f) : unit =
        Hexa.NET.ImGui.ImGui.ScaleClipRects(self, System.Numerics.Vector2.FromV2f(fbScale))


    static member SeekCursorForItem(self: ImGuiListClipperPtr, itemIndex: int32) : unit =
        Hexa.NET.ImGui.ImGui.SeekCursorForItem(self, itemIndex)


    static member SelectAll(self: ImGuiInputTextCallbackDataPtr) : unit =
        Hexa.NET.ImGui.ImGui.SelectAll(self)


    static member Selectable(label: string, selected: bool, flags: ImGuiSelectableFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, selected, flags, System.Numerics.Vector2.FromV2f(size))

    static member Selectable(label: string, selected: bool, flags: ImGuiSelectableFlags) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, selected, flags)

    static member Selectable(label: string, selected: bool) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, selected)

    static member Selectable(label: string) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label)

    static member Selectable(label: string, flags: ImGuiSelectableFlags) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, flags)

    static member Selectable(label: string, selected: bool, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, selected, System.Numerics.Vector2.FromV2f(size))

    static member Selectable(label: string, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, System.Numerics.Vector2.FromV2f(size))

    static member Selectable(label: string, flags: ImGuiSelectableFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, flags, System.Numerics.Vector2.FromV2f(size))

    static member Selectable(label: string, pSelected: byref<bool>, flags: ImGuiSelectableFlags, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelected, flags, System.Numerics.Vector2.FromV2f(size))

    static member Selectable(label: string, pSelected: cval<bool>, flags: ImGuiSelectableFlags, size: V2f) : unit =
        let mutable pSelectedState = pSelected.Value
        let result = Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelectedState, flags, System.Numerics.Vector2.FromV2f(size))
        if result then
            pSelected.Value <- pSelectedState

    static member Selectable(label: string, pSelected: byref<bool>, flags: ImGuiSelectableFlags) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelected, flags)

    static member Selectable(label: string, pSelected: cval<bool>, flags: ImGuiSelectableFlags) : unit =
        let mutable pSelectedState = pSelected.Value
        let result = Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelectedState, flags)
        if result then
            pSelected.Value <- pSelectedState

    static member Selectable(label: string, pSelected: byref<bool>) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelected)

    static member Selectable(label: string, pSelected: cval<bool>) : unit =
        let mutable pSelectedState = pSelected.Value
        let result = Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelectedState)
        if result then
            pSelected.Value <- pSelectedState

    static member Selectable(label: string, pSelected: byref<bool>, size: V2f) : bool =
        Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelected, System.Numerics.Vector2.FromV2f(size))

    static member Selectable(label: string, pSelected: cval<bool>, size: V2f) : unit =
        let mutable pSelectedState = pSelected.Value
        let result = Hexa.NET.ImGui.ImGui.Selectable(label, &&pSelectedState, System.Numerics.Vector2.FromV2f(size))
        if result then
            pSelected.Value <- pSelectedState


    static member Separator() : unit =
        Hexa.NET.ImGui.ImGui.Separator()


    static member SeparatorText(label: string) : unit =
        Hexa.NET.ImGui.ImGui.SeparatorText(label)


    static member SetAllInt(self: ImGuiStoragePtr, value: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetAllInt(self, value)


    static member SetAllocatorFunctions(allocFunc: ImGuiMemAllocFunc, freeFunc: ImGuiMemFreeFunc, userData: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.SetAllocatorFunctions(allocFunc, freeFunc, userData)

    static member SetAllocatorFunctions(allocFunc: ImGuiMemAllocFunc, freeFunc: ImGuiMemFreeFunc) : unit =
        Hexa.NET.ImGui.ImGui.SetAllocatorFunctions(allocFunc, freeFunc)

    static member SetAllocatorFunctions(allocFunc: nativeint, freeFunc: nativeint, userData: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.SetAllocatorFunctions(allocFunc, freeFunc, userData)

    static member SetAllocatorFunctions(allocFunc: nativeint, freeFunc: nativeint) : unit =
        Hexa.NET.ImGui.ImGui.SetAllocatorFunctions(allocFunc, freeFunc)


    static member SetAppAcceptingEvents(self: ImGuiIOPtr, acceptingEvents: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetAppAcceptingEvents(self, acceptingEvents)


    static member SetBit(self: ImFontGlyphRangesBuilderPtr, n: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.SetBit(self, n)


    static member SetBool(self: ImGuiStoragePtr, key: uint32, value: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetBool(self, key, value)


    static member SetClipboardText(text: string) : unit =
        Hexa.NET.ImGui.ImGui.SetClipboardText(text)


    static member SetColorEditOptions(flags: ImGuiColorEditFlags) : unit =
        Hexa.NET.ImGui.ImGui.SetColorEditOptions(flags)


    static member SetColumnOffset(columnIndex: int32, offsetX: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetColumnOffset(columnIndex, offsetX)


    static member SetColumnWidth(columnIndex: int32, width: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetColumnWidth(columnIndex, width)


    static member SetCurrentChannel(self: ImDrawListSplitterPtr, drawList: ImDrawListPtr, channelIdx: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetCurrentChannel(self, drawList, channelIdx)


    static member SetCurrentContext(ctx: ImGuiContextPtr) : unit =
        Hexa.NET.ImGui.ImGui.SetCurrentContext(ctx)


    static member SetCursorPos(localPos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetCursorPos(System.Numerics.Vector2.FromV2f(localPos))


    static member SetCursorPosX(localX: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetCursorPosX(localX)


    static member SetCursorPosY(localY: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetCursorPosY(localY)


    static member SetCursorScreenPos(pos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetCursorScreenPos(System.Numerics.Vector2.FromV2f(pos))


    static member SetDragDropPayload(typ: nativeptr<uint8>, data: voidptr, sz: unativeint, cond: ImGuiCond) : bool =
        Hexa.NET.ImGui.ImGui.SetDragDropPayload(typ, data, sz, cond)

    static member SetDragDropPayload(typ: nativeptr<uint8>, data: voidptr, sz: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.SetDragDropPayload(typ, data, sz)


    static member SetFloat(self: ImGuiStoragePtr, key: uint32, value: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetFloat(self, key, value)


    static member SetHSV(self: ImColorPtr, h: float32, s: float32, v: float32, a: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetHSV(self, h, s, v, a)

    static member SetHSV(self: ImColorPtr, h: float32, s: float32, v: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetHSV(self, h, s, v)


    static member SetInt(self: ImGuiStoragePtr, key: uint32, value: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetInt(self, key, value)


    static member SetItemDefaultFocus() : unit =
        Hexa.NET.ImGui.ImGui.SetItemDefaultFocus()


    static member SetItemKeyOwner(key: ImGuiKey) : unit =
        Hexa.NET.ImGui.ImGui.SetItemKeyOwner(key)


    static member SetItemSelected(self: ImGuiSelectionBasicStoragePtr, id: uint32, selected: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetItemSelected(self, id, selected)


    static member SetItemTooltip(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.SetItemTooltip(fmt)


    static member SetItemTooltipV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.SetItemTooltipV(fmt, args)


    static member SetKeyEventNativeData(self: ImGuiIOPtr, key: ImGuiKey, nativeKeycode: int32, nativeScancode: int32, nativeLegacyIndex: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetKeyEventNativeData(self, key, nativeKeycode, nativeScancode, nativeLegacyIndex)

    static member SetKeyEventNativeData(self: ImGuiIOPtr, key: ImGuiKey, nativeKeycode: int32, nativeScancode: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetKeyEventNativeData(self, key, nativeKeycode, nativeScancode)


    static member SetKeyboardFocusHere(offset: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetKeyboardFocusHere(offset)

    static member SetKeyboardFocusHere() : unit =
        Hexa.NET.ImGui.ImGui.SetKeyboardFocusHere()


    static member SetMouseCursor(cursorType: ImGuiMouseCursor) : unit =
        Hexa.NET.ImGui.ImGui.SetMouseCursor(cursorType)


    static member SetNavCursorVisible(visible: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetNavCursorVisible(visible)


    static member SetNextFrameWantCaptureKeyboard(wantCaptureKeyboard: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetNextFrameWantCaptureKeyboard(wantCaptureKeyboard)


    static member SetNextFrameWantCaptureMouse(wantCaptureMouse: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetNextFrameWantCaptureMouse(wantCaptureMouse)


    static member SetNextItemAllowOverlap() : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemAllowOverlap()


    static member SetNextItemOpen(isOpen: bool, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemOpen(isOpen, cond)

    static member SetNextItemOpen(isOpen: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemOpen(isOpen)


    static member SetNextItemSelectionUserData(selectionUserData: int64) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemSelectionUserData(selectionUserData)


    static member SetNextItemShortcut(keyChord: int32, flags: ImGuiInputFlags) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemShortcut(keyChord, flags)

    static member SetNextItemShortcut(keyChord: int32) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemShortcut(keyChord)


    static member SetNextItemStorageID(storageId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemStorageID(storageId)


    static member SetNextItemWidth(itemWidth: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetNextItemWidth(itemWidth)


    static member SetNextWindowBgAlpha(alpha: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowBgAlpha(alpha)


    static member SetNextWindowClass(windowClass: ImGuiWindowClassPtr) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowClass(windowClass)


    static member SetNextWindowCollapsed(collapsed: bool, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowCollapsed(collapsed, cond)

    static member SetNextWindowCollapsed(collapsed: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowCollapsed(collapsed)


    static member SetNextWindowContentSize(size: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowContentSize(System.Numerics.Vector2.FromV2f(size))


    static member SetNextWindowDockID(dockId: uint32, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowDockID(dockId, cond)

    static member SetNextWindowDockID(dockId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowDockID(dockId)


    static member SetNextWindowFocus() : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowFocus()


    static member SetNextWindowPos(pos: V2f, cond: ImGuiCond, pivot: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowPos(System.Numerics.Vector2.FromV2f(pos), cond, System.Numerics.Vector2.FromV2f(pivot))

    static member SetNextWindowPos(pos: V2f, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowPos(System.Numerics.Vector2.FromV2f(pos), cond)

    static member SetNextWindowPos(pos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowPos(System.Numerics.Vector2.FromV2f(pos))

    static member SetNextWindowPos(pos: V2f, pivot: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowPos(System.Numerics.Vector2.FromV2f(pos), System.Numerics.Vector2.FromV2f(pivot))


    static member SetNextWindowScroll(scroll: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowScroll(System.Numerics.Vector2.FromV2f(scroll))


    static member SetNextWindowSize(size: V2f, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowSize(System.Numerics.Vector2.FromV2f(size), cond)

    static member SetNextWindowSize(size: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowSize(System.Numerics.Vector2.FromV2f(size))


    static member SetNextWindowSizeConstraints(sizeMin: V2f, sizeMax: V2f, customCallback: ImGuiSizeCallback, customCallbackData: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowSizeConstraints(System.Numerics.Vector2.FromV2f(sizeMin), System.Numerics.Vector2.FromV2f(sizeMax), customCallback, customCallbackData)

    static member SetNextWindowSizeConstraints(sizeMin: V2f, sizeMax: V2f, customCallback: ImGuiSizeCallback) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowSizeConstraints(System.Numerics.Vector2.FromV2f(sizeMin), System.Numerics.Vector2.FromV2f(sizeMax), customCallback)

    static member SetNextWindowSizeConstraints(sizeMin: V2f, sizeMax: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowSizeConstraints(System.Numerics.Vector2.FromV2f(sizeMin), System.Numerics.Vector2.FromV2f(sizeMax))

    static member SetNextWindowSizeConstraints(sizeMin: V2f, sizeMax: V2f, customCallbackData: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowSizeConstraints(System.Numerics.Vector2.FromV2f(sizeMin), System.Numerics.Vector2.FromV2f(sizeMax), customCallbackData)


    static member SetNextWindowViewport(viewportId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.SetNextWindowViewport(viewportId)


    static member SetScrollFromPosX(localX: float32, centerXRatio: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollFromPosX(localX, centerXRatio)

    static member SetScrollFromPosX(localX: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollFromPosX(localX)


    static member SetScrollFromPosY(localY: float32, centerYRatio: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollFromPosY(localY, centerYRatio)

    static member SetScrollFromPosY(localY: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollFromPosY(localY)


    static member SetScrollHereX(centerXRatio: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollHereX(centerXRatio)

    static member SetScrollHereX() : unit =
        Hexa.NET.ImGui.ImGui.SetScrollHereX()


    static member SetScrollHereY(centerYRatio: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollHereY(centerYRatio)

    static member SetScrollHereY() : unit =
        Hexa.NET.ImGui.ImGui.SetScrollHereY()


    static member SetScrollX(scrollX: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollX(scrollX)


    static member SetScrollY(scrollY: float32) : unit =
        Hexa.NET.ImGui.ImGui.SetScrollY(scrollY)


    static member SetStateStorage(storage: ImGuiStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.SetStateStorage(storage)


    static member SetStatus(self: ImTextureDataPtr, status: ImTextureStatus) : unit =
        Hexa.NET.ImGui.ImGui.SetStatus(self, status)


    static member SetTabItemClosed(tabOrDockedWindowLabel: string) : unit =
        Hexa.NET.ImGui.ImGui.SetTabItemClosed(tabOrDockedWindowLabel)


    static member SetTexID(self: ImTextureDataPtr, texId: ImTextureID) : unit =
        Hexa.NET.ImGui.ImGui.SetTexID(self, texId)


    static member SetTooltip(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.SetTooltip(fmt)


    static member SetTooltipV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.SetTooltipV(fmt, args)


    static member SetVoidPtr(self: ImGuiStoragePtr, key: uint32, value: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.SetVoidPtr(self, key, value)


    static member SetWindowCollapsed(collapsed: bool, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowCollapsed(collapsed, cond)

    static member SetWindowCollapsed(collapsed: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowCollapsed(collapsed)

    static member SetWindowCollapsed(name: string, collapsed: bool, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowCollapsed(name, collapsed, cond)

    static member SetWindowCollapsed(name: string, collapsed: bool) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowCollapsed(name, collapsed)


    static member SetWindowFocus() : unit =
        Hexa.NET.ImGui.ImGui.SetWindowFocus()

    static member SetWindowFocus(name: string) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowFocus(name)


    static member SetWindowPos(pos: V2f, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowPos(System.Numerics.Vector2.FromV2f(pos), cond)

    static member SetWindowPos(pos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowPos(System.Numerics.Vector2.FromV2f(pos))

    static member SetWindowPos(name: string, pos: V2f, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowPos(name, System.Numerics.Vector2.FromV2f(pos), cond)

    static member SetWindowPos(name: string, pos: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowPos(name, System.Numerics.Vector2.FromV2f(pos))


    static member SetWindowSize(size: V2f, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowSize(System.Numerics.Vector2.FromV2f(size), cond)

    static member SetWindowSize(size: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowSize(System.Numerics.Vector2.FromV2f(size))

    static member SetWindowSize(name: string, size: V2f, cond: ImGuiCond) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowSize(name, System.Numerics.Vector2.FromV2f(size), cond)

    static member SetWindowSize(name: string, size: V2f) : unit =
        Hexa.NET.ImGui.ImGui.SetWindowSize(name, System.Numerics.Vector2.FromV2f(size))


    static member Shortcut(keyChord: int32, flags: ImGuiInputFlags) : bool =
        Hexa.NET.ImGui.ImGui.Shortcut(keyChord, flags)

    static member Shortcut(keyChord: int32) : bool =
        Hexa.NET.ImGui.ImGui.Shortcut(keyChord)


    static member ShowAboutWindow(pOpen: byref<bool>) : unit =
        Hexa.NET.ImGui.ImGui.ShowAboutWindow(&&pOpen)

    static member ShowAboutWindow(pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        Hexa.NET.ImGui.ImGui.ShowAboutWindow(&&pOpenState)
        pOpen.Value <- pOpenState

    static member ShowAboutWindow() : unit =
        Hexa.NET.ImGui.ImGui.ShowAboutWindow()


    static member ShowDebugLogWindow(pOpen: byref<bool>) : unit =
        Hexa.NET.ImGui.ImGui.ShowDebugLogWindow(&&pOpen)

    static member ShowDebugLogWindow(pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        Hexa.NET.ImGui.ImGui.ShowDebugLogWindow(&&pOpenState)
        pOpen.Value <- pOpenState

    static member ShowDebugLogWindow() : unit =
        Hexa.NET.ImGui.ImGui.ShowDebugLogWindow()


    static member ShowDemoWindow(pOpen: byref<bool>) : unit =
        Hexa.NET.ImGui.ImGui.ShowDemoWindow(&&pOpen)

    static member ShowDemoWindow(pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        Hexa.NET.ImGui.ImGui.ShowDemoWindow(&&pOpenState)
        pOpen.Value <- pOpenState

    static member ShowDemoWindow() : unit =
        Hexa.NET.ImGui.ImGui.ShowDemoWindow()


    static member ShowFontSelector(label: string) : unit =
        Hexa.NET.ImGui.ImGui.ShowFontSelector(label)


    static member ShowIDStackToolWindow(pOpen: byref<bool>) : unit =
        Hexa.NET.ImGui.ImGui.ShowIDStackToolWindow(&&pOpen)

    static member ShowIDStackToolWindow(pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        Hexa.NET.ImGui.ImGui.ShowIDStackToolWindow(&&pOpenState)
        pOpen.Value <- pOpenState

    static member ShowIDStackToolWindow() : unit =
        Hexa.NET.ImGui.ImGui.ShowIDStackToolWindow()


    static member ShowMetricsWindow(pOpen: byref<bool>) : unit =
        Hexa.NET.ImGui.ImGui.ShowMetricsWindow(&&pOpen)

    static member ShowMetricsWindow(pOpen: cval<bool>) : unit =
        let mutable pOpenState = pOpen.Value
        Hexa.NET.ImGui.ImGui.ShowMetricsWindow(&&pOpenState)
        pOpen.Value <- pOpenState

    static member ShowMetricsWindow() : unit =
        Hexa.NET.ImGui.ImGui.ShowMetricsWindow()


    static member ShowStyleEditor(reference: ImGuiStylePtr) : unit =
        Hexa.NET.ImGui.ImGui.ShowStyleEditor(reference)

    static member ShowStyleEditor() : unit =
        Hexa.NET.ImGui.ImGui.ShowStyleEditor()


    static member ShowStyleSelector(label: string) : bool =
        Hexa.NET.ImGui.ImGui.ShowStyleSelector(label)


    static member ShowUserGuide() : unit =
        Hexa.NET.ImGui.ImGui.ShowUserGuide()


    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, vDegreesMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, vDegreesMax, format, flags)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, vDegreesMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, vDegreesMax, format, flags)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, vDegreesMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, vDegreesMax, format)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, vDegreesMax: float32, format: string) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, vDegreesMax, format)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, vDegreesMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, vDegreesMax)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, vDegreesMax: float32) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, vDegreesMax)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad)

    static member SliderAngle(label: string, vRad: cval<float32>) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, format)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, format: string) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, format)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, format)

    static member SliderAngle(label: string, vRad: cval<float32>, format: string) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, format)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, vDegreesMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, vDegreesMax, flags)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, vDegreesMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, vDegreesMax, flags)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, flags)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, flags)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, flags)

    static member SliderAngle(label: string, vRad: cval<float32>, flags: ImGuiSliderFlags) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, flags)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, vDegreesMin: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, vDegreesMin, format, flags)

    static member SliderAngle(label: string, vRad: cval<float32>, vDegreesMin: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, vDegreesMin, format, flags)
        if result then
            vRad.Value <- vRadState

    static member SliderAngle(label: string, vRad: byref<float32>, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRad, format, flags)

    static member SliderAngle(label: string, vRad: cval<float32>, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vRadState = vRad.Value
        let result = Hexa.NET.ImGui.ImGui.SliderAngle(label, &&vRadState, format, flags)
        if result then
            vRad.Value <- vRadState


    static member SliderFloat(label: string, v: byref<float32>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat(label, &&v, vMin, vMax, format, flags)

    static member SliderFloat(label: string, v: cval<float32>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat(label, &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderFloat(label: string, v: byref<float32>, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat(label, &&v, vMin, vMax, format)

    static member SliderFloat(label: string, v: cval<float32>, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat(label, &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderFloat(label: string, v: byref<float32>, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat(label, &&v, vMin, vMax)

    static member SliderFloat(label: string, v: cval<float32>, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat(label, &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderFloat(label: string, v: byref<float32>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat(label, &&v, vMin, vMax, flags)

    static member SliderFloat(label: string, v: cval<float32>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat(label, &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderFloat2(label: string, v: byref<V2f>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&v, vMin, vMax, format, flags)

    static member SliderFloat2(label: string, v: cval<V2f>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderFloat2(label: string, v: byref<V2f>, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&v, vMin, vMax, format)

    static member SliderFloat2(label: string, v: cval<V2f>, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderFloat2(label: string, v: byref<V2f>, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&v, vMin, vMax)

    static member SliderFloat2(label: string, v: cval<V2f>, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderFloat2(label: string, v: byref<V2f>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&v, vMin, vMax, flags)

    static member SliderFloat2(label: string, v: cval<V2f>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat2(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderFloat3(label: string, v: byref<V3f>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&v, vMin, vMax, format, flags)

    static member SliderFloat3(label: string, v: cval<V3f>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderFloat3(label: string, v: byref<V3f>, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&v, vMin, vMax, format)

    static member SliderFloat3(label: string, v: cval<V3f>, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderFloat3(label: string, v: byref<V3f>, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&v, vMin, vMax)

    static member SliderFloat3(label: string, v: cval<V3f>, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderFloat3(label: string, v: byref<V3f>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&v, vMin, vMax, flags)

    static member SliderFloat3(label: string, v: cval<V3f>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat3(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderFloat4(label: string, v: byref<V4f>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&v, vMin, vMax, format, flags)

    static member SliderFloat4(label: string, v: cval<V4f>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderFloat4(label: string, v: byref<V4f>, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&v, vMin, vMax, format)

    static member SliderFloat4(label: string, v: cval<V4f>, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderFloat4(label: string, v: byref<V4f>, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&v, vMin, vMax)

    static member SliderFloat4(label: string, v: cval<V4f>, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderFloat4(label: string, v: byref<V4f>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&v, vMin, vMax, flags)

    static member SliderFloat4(label: string, v: cval<V4f>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderFloat4(label, NativePtr.cast<_, float32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderInt(label: string, v: byref<int32>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt(label, &&v, vMin, vMax, format, flags)

    static member SliderInt(label: string, v: cval<int32>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt(label, &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderInt(label: string, v: byref<int32>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt(label, &&v, vMin, vMax, format)

    static member SliderInt(label: string, v: cval<int32>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt(label, &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderInt(label: string, v: byref<int32>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt(label, &&v, vMin, vMax)

    static member SliderInt(label: string, v: cval<int32>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt(label, &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderInt(label: string, v: byref<int32>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt(label, &&v, vMin, vMax, flags)

    static member SliderInt(label: string, v: cval<int32>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt(label, &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format, flags)

    static member SliderInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format)

    static member SliderInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax)

    static member SliderInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderInt2(label: string, v: byref<V2i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&v, vMin, vMax, flags)

    static member SliderInt2(label: string, v: cval<V2i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt2(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format, flags)

    static member SliderInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format)

    static member SliderInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax)

    static member SliderInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderInt3(label: string, v: byref<V3i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&v, vMin, vMax, flags)

    static member SliderInt3(label: string, v: cval<V3i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt3(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format, flags)

    static member SliderInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member SliderInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax, format)

    static member SliderInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member SliderInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax)

    static member SliderInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member SliderInt4(label: string, v: byref<V4i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&v, vMin, vMax, flags)

    static member SliderInt4(label: string, v: cval<V4i>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.SliderInt4(label, NativePtr.cast<_, int32> &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member SliderScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalar(label, dataType, pData, pMin, pMax, format, flags)

    static member SliderScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalar(label, dataType, pData, pMin, pMax, format)

    static member SliderScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalar(label, dataType, pData, pMin, pMax)

    static member SliderScalar(label: string, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalar(label, dataType, pData, pMin, pMax, flags)


    static member SliderScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalarN(label, dataType, pData, components, pMin, pMax, format, flags)

    static member SliderScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalarN(label, dataType, pData, components, pMin, pMax, format)

    static member SliderScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalarN(label, dataType, pData, components, pMin, pMax)

    static member SliderScalarN(label: string, dataType: ImGuiDataType, pData: voidptr, components: int32, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.SliderScalarN(label, dataType, pData, components, pMin, pMax, flags)


    static member SmallButton(label: string) : bool =
        Hexa.NET.ImGui.ImGui.SmallButton(label)


    static member Spacing() : unit =
        Hexa.NET.ImGui.ImGui.Spacing()


    static member Split(self: ImDrawListSplitterPtr, drawList: ImDrawListPtr, count: int32) : unit =
        Hexa.NET.ImGui.ImGui.Split(self, drawList, count)


    static member Step(self: ImGuiListClipperPtr) : bool =
        Hexa.NET.ImGui.ImGui.Step(self)


    static member StyleColorsClassic(dst: ImGuiStylePtr) : unit =
        Hexa.NET.ImGui.ImGui.StyleColorsClassic(dst)

    static member StyleColorsClassic() : unit =
        Hexa.NET.ImGui.ImGui.StyleColorsClassic()


    static member StyleColorsDark(dst: ImGuiStylePtr) : unit =
        Hexa.NET.ImGui.ImGui.StyleColorsDark(dst)

    static member StyleColorsDark() : unit =
        Hexa.NET.ImGui.ImGui.StyleColorsDark()


    static member StyleColorsLight(dst: ImGuiStylePtr) : unit =
        Hexa.NET.ImGui.ImGui.StyleColorsLight(dst)

    static member StyleColorsLight() : unit =
        Hexa.NET.ImGui.ImGui.StyleColorsLight()


    static member Swap(self: ImGuiSelectionBasicStoragePtr, r: ImGuiSelectionBasicStoragePtr) : unit =
        Hexa.NET.ImGui.ImGui.Swap(self, r)


    static member TabItemButton(label: string, flags: ImGuiTabItemFlags) : bool =
        Hexa.NET.ImGui.ImGui.TabItemButton(label, flags)

    static member TabItemButton(label: string) : bool =
        Hexa.NET.ImGui.ImGui.TabItemButton(label)


    static member TableAngledHeadersRow() : unit =
        Hexa.NET.ImGui.ImGui.TableAngledHeadersRow()


    static member TableGetColumnCount() : int32 =
        Hexa.NET.ImGui.ImGui.TableGetColumnCount()


    static member TableGetColumnFlags(columnN: int32) : ImGuiTableColumnFlags =
        Hexa.NET.ImGui.ImGui.TableGetColumnFlags(columnN)

    static member TableGetColumnFlags() : ImGuiTableColumnFlags =
        Hexa.NET.ImGui.ImGui.TableGetColumnFlags()


    static member TableGetColumnIndex() : int32 =
        Hexa.NET.ImGui.ImGui.TableGetColumnIndex()


    static member TableGetColumnName(columnN: int32) : string =
        Hexa.NET.ImGui.ImGui.TableGetColumnNameS(columnN)

    static member TableGetColumnName() : string =
        Hexa.NET.ImGui.ImGui.TableGetColumnNameS()


    static member TableGetHoveredColumn() : int32 =
        Hexa.NET.ImGui.ImGui.TableGetHoveredColumn()


    static member TableGetRowIndex() : int32 =
        Hexa.NET.ImGui.ImGui.TableGetRowIndex()


    static member TableGetSortSpecs() : ImGuiTableSortSpecsPtr =
        Hexa.NET.ImGui.ImGui.TableGetSortSpecs()


    static member TableHeader(label: string) : unit =
        Hexa.NET.ImGui.ImGui.TableHeader(label)


    static member TableHeadersRow() : unit =
        Hexa.NET.ImGui.ImGui.TableHeadersRow()


    static member TableNextColumn() : bool =
        Hexa.NET.ImGui.ImGui.TableNextColumn()


    static member TableNextRow(rowFlags: ImGuiTableRowFlags, minRowHeight: float32) : unit =
        Hexa.NET.ImGui.ImGui.TableNextRow(rowFlags, minRowHeight)

    static member TableNextRow(rowFlags: ImGuiTableRowFlags) : unit =
        Hexa.NET.ImGui.ImGui.TableNextRow(rowFlags)

    static member TableNextRow() : unit =
        Hexa.NET.ImGui.ImGui.TableNextRow()

    static member TableNextRow(minRowHeight: float32) : unit =
        Hexa.NET.ImGui.ImGui.TableNextRow(minRowHeight)


    static member TableSetBgColor(target: ImGuiTableBgTarget, color: uint32, columnN: int32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetBgColor(target, color, columnN)

    static member TableSetBgColor(target: ImGuiTableBgTarget, color: uint32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetBgColor(target, color)


    static member TableSetColumnEnabled(columnN: int32, v: bool) : unit =
        Hexa.NET.ImGui.ImGui.TableSetColumnEnabled(columnN, v)


    static member TableSetColumnIndex(columnN: int32) : bool =
        Hexa.NET.ImGui.ImGui.TableSetColumnIndex(columnN)


    static member TableSetupColumn(label: string, flags: ImGuiTableColumnFlags, initWidthOrWeight: float32, userId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, flags, initWidthOrWeight, userId)

    static member TableSetupColumn(label: string, flags: ImGuiTableColumnFlags, initWidthOrWeight: float32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, flags, initWidthOrWeight)

    static member TableSetupColumn(label: string, flags: ImGuiTableColumnFlags) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, flags)

    static member TableSetupColumn(label: string) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label)

    static member TableSetupColumn(label: string, initWidthOrWeight: float32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, initWidthOrWeight)

    static member TableSetupColumn(label: string, flags: ImGuiTableColumnFlags, userId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, flags, userId)

    static member TableSetupColumn(label: string, userId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, userId)

    static member TableSetupColumn(label: string, initWidthOrWeight: float32, userId: uint32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupColumn(label, initWidthOrWeight, userId)


    static member TableSetupScrollFreeze(cols: int32, rows: int32) : unit =
        Hexa.NET.ImGui.ImGui.TableSetupScrollFreeze(cols, rows)


    static member TempInputText(bb: ImRect, id: uint32, label: string, buf: nativeptr<uint8>, bufSize: int32, flags: ImGuiInputTextFlags) : bool =
        Hexa.NET.ImGui.ImGui.TempInputText(bb, id, label, buf, bufSize, flags)


    static member Text(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.Text(fmt)


    static member TextAligned(alignX: float32, sizeX: float32, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextAligned(alignX, sizeX, fmt)


    static member TextColored(col: V4f, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextColored(System.Numerics.Vector4.FromV4f(col), fmt)

    static member TextColored(col: C4b, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextColored(System.Numerics.Vector4.FromC4b(col), fmt)

    static member TextColored(col: C4us, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextColored(System.Numerics.Vector4.FromC4us(col), fmt)

    static member TextColored(col: C4ui, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextColored(System.Numerics.Vector4.FromC4ui(col), fmt)

    static member TextColored(col: C4f, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextColored(System.Numerics.Vector4.FromC4f(col), fmt)

    static member TextColored(col: C4d, fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextColored(System.Numerics.Vector4.FromC4d(col), fmt)


    static member TextColoredV(col: V4f, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextColoredV(System.Numerics.Vector4.FromV4f(col), fmt, args)

    static member TextColoredV(col: C4b, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextColoredV(System.Numerics.Vector4.FromC4b(col), fmt, args)

    static member TextColoredV(col: C4us, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextColoredV(System.Numerics.Vector4.FromC4us(col), fmt, args)

    static member TextColoredV(col: C4ui, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextColoredV(System.Numerics.Vector4.FromC4ui(col), fmt, args)

    static member TextColoredV(col: C4f, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextColoredV(System.Numerics.Vector4.FromC4f(col), fmt, args)

    static member TextColoredV(col: C4d, fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextColoredV(System.Numerics.Vector4.FromC4d(col), fmt, args)


    static member TextDisabled(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextDisabled(fmt)


    static member TextDisabledV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextDisabledV(fmt, args)


    static member TextLink(label: string) : bool =
        Hexa.NET.ImGui.ImGui.TextLink(label)


    static member TextLinkOpenURL(label: string, url: string) : bool =
        Hexa.NET.ImGui.ImGui.TextLinkOpenURL(label, url)

    static member TextLinkOpenURL(label: string) : bool =
        Hexa.NET.ImGui.ImGui.TextLinkOpenURL(label)


    static member TextUnformatted(text: string, textEnd: string) : unit =
        Hexa.NET.ImGui.ImGui.TextUnformatted(text, textEnd)

    static member TextUnformatted(text: string) : unit =
        Hexa.NET.ImGui.ImGui.TextUnformatted(text)


    static member TextV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextV(fmt, args)


    static member TextWrapped(fmt: string) : unit =
        Hexa.NET.ImGui.ImGui.TextWrapped(fmt)


    static member TextWrappedV(fmt: string, args: unativeint) : unit =
        Hexa.NET.ImGui.ImGui.TextWrappedV(fmt, args)


    static member TreeNode(label: string) : bool =
        Hexa.NET.ImGui.ImGui.TreeNode(label)

    static member TreeNode(strId: string, fmt: string) : bool =
        Hexa.NET.ImGui.ImGui.TreeNode(strId, fmt)

    static member TreeNode(ptrId: voidptr, fmt: string) : bool =
        Hexa.NET.ImGui.ImGui.TreeNode(ptrId, fmt)


    static member TreeNodeEx(label: string, flags: ImGuiTreeNodeFlags) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeEx(label, flags)

    static member TreeNodeEx(label: string) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeEx(label)

    static member TreeNodeEx(strId: string, flags: ImGuiTreeNodeFlags, fmt: string) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeEx(strId, flags, fmt)

    static member TreeNodeEx(ptrId: voidptr, flags: ImGuiTreeNodeFlags, fmt: string) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeEx(ptrId, flags, fmt)


    static member TreeNodeExV(strId: string, flags: ImGuiTreeNodeFlags, fmt: string, args: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeExV(strId, flags, fmt, args)

    static member TreeNodeExV(ptrId: voidptr, flags: ImGuiTreeNodeFlags, fmt: string, args: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeExV(ptrId, flags, fmt, args)


    static member TreeNodeV(strId: string, fmt: string, args: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeV(strId, fmt, args)

    static member TreeNodeV(ptrId: voidptr, fmt: string, args: unativeint) : bool =
        Hexa.NET.ImGui.ImGui.TreeNodeV(ptrId, fmt, args)


    static member TreePop() : unit =
        Hexa.NET.ImGui.ImGui.TreePop()


    static member TreePush(strId: string) : unit =
        Hexa.NET.ImGui.ImGui.TreePush(strId)

    static member TreePush(ptrId: voidptr) : unit =
        Hexa.NET.ImGui.ImGui.TreePush(ptrId)


    static member Unindent(indentW: float32) : unit =
        Hexa.NET.ImGui.ImGui.Unindent(indentW)

    static member Unindent() : unit =
        Hexa.NET.ImGui.ImGui.Unindent()


    static member UpdatePlatformWindows() : unit =
        Hexa.NET.ImGui.ImGui.UpdatePlatformWindows()


    static member VSliderFloat(label: string, size: V2f, v: byref<float32>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax, format, flags)

    static member VSliderFloat(label: string, size: V2f, v: cval<float32>, vMin: float32, vMax: float32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member VSliderFloat(label: string, size: V2f, v: byref<float32>, vMin: float32, vMax: float32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax, format)

    static member VSliderFloat(label: string, size: V2f, v: cval<float32>, vMin: float32, vMax: float32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member VSliderFloat(label: string, size: V2f, v: byref<float32>, vMin: float32, vMax: float32) : bool =
        Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax)

    static member VSliderFloat(label: string, size: V2f, v: cval<float32>, vMin: float32, vMax: float32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member VSliderFloat(label: string, size: V2f, v: byref<float32>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax, flags)

    static member VSliderFloat(label: string, size: V2f, v: cval<float32>, vMin: float32, vMax: float32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderFloat(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member VSliderInt(label: string, size: V2f, v: byref<int32>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax, format, flags)

    static member VSliderInt(label: string, size: V2f, v: cval<int32>, vMin: int32, vMax: int32, format: string, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax, format, flags)
        if result then
            v.Value <- vState

    static member VSliderInt(label: string, size: V2f, v: byref<int32>, vMin: int32, vMax: int32, format: string) : bool =
        Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax, format)

    static member VSliderInt(label: string, size: V2f, v: cval<int32>, vMin: int32, vMax: int32, format: string) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax, format)
        if result then
            v.Value <- vState

    static member VSliderInt(label: string, size: V2f, v: byref<int32>, vMin: int32, vMax: int32) : bool =
        Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax)

    static member VSliderInt(label: string, size: V2f, v: cval<int32>, vMin: int32, vMax: int32) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax)
        if result then
            v.Value <- vState

    static member VSliderInt(label: string, size: V2f, v: byref<int32>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&v, vMin, vMax, flags)

    static member VSliderInt(label: string, size: V2f, v: cval<int32>, vMin: int32, vMax: int32, flags: ImGuiSliderFlags) : unit =
        let mutable vState = v.Value
        let result = Hexa.NET.ImGui.ImGui.VSliderInt(label, System.Numerics.Vector2.FromV2f(size), &&vState, vMin, vMax, flags)
        if result then
            v.Value <- vState


    static member VSliderScalar(label: string, size: V2f, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, format: string, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.VSliderScalar(label, System.Numerics.Vector2.FromV2f(size), dataType, pData, pMin, pMax, format, flags)

    static member VSliderScalar(label: string, size: V2f, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, format: string) : bool =
        Hexa.NET.ImGui.ImGui.VSliderScalar(label, System.Numerics.Vector2.FromV2f(size), dataType, pData, pMin, pMax, format)

    static member VSliderScalar(label: string, size: V2f, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr) : bool =
        Hexa.NET.ImGui.ImGui.VSliderScalar(label, System.Numerics.Vector2.FromV2f(size), dataType, pData, pMin, pMax)

    static member VSliderScalar(label: string, size: V2f, dataType: ImGuiDataType, pData: voidptr, pMin: voidptr, pMax: voidptr, flags: ImGuiSliderFlags) : bool =
        Hexa.NET.ImGui.ImGui.VSliderScalar(label, System.Numerics.Vector2.FromV2f(size), dataType, pData, pMin, pMax, flags)


    static member Value(prefix: string, b: bool) : unit =
        Hexa.NET.ImGui.ImGui.Value(prefix, b)

    static member Value(prefix: string, v: int32) : unit =
        Hexa.NET.ImGui.ImGui.Value(prefix, v)

    static member Value(prefix: string, v: uint32) : unit =
        Hexa.NET.ImGui.ImGui.Value(prefix, v)

    static member Value(prefix: string, v: float32, floatFormat: string) : unit =
        Hexa.NET.ImGui.ImGui.Value(prefix, v, floatFormat)

    static member Value(prefix: string, v: float32) : unit =
        Hexa.NET.ImGui.ImGui.Value(prefix, v)
