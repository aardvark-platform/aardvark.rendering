namespace Aardvark.Rendering.Vulkan

//#nowarn "1337"

//open System
//open System.Runtime.InteropServices
//open System.Runtime.CompilerServices
//open Microsoft.FSharp.NativeInterop
//open System.Security
//open Aardvark.Base

//#nowarn "9"
//#nowarn "51"

//type AttribMask = 
//    | GlCurrentBit = 1
//    | GlPointBit = 2
//    | GlLineBit = 4
//    | GlPolygonBit = 8
//    | GlPolygonStippleBit = 16
//    | GlPixelModeBit = 32
//    | GlLightingBit = 64
//    | GlFogBit = 128
//    | GlDepthBufferBit = 256
//    | GlAccumBufferBit = 512
//    | GlStencilBufferBit = 1024
//    | GlViewportBit = 2048
//    | GlTransformBit = 4096
//    | GlEnableBit = 8192
//    | GlColorBufferBit = 16384
//    | GlHintBit = 32768
//    | GlEvalBit = 65536
//    | GlListBit = 131072
//    | GlTextureBit = 262144
//    | GlScissorBit = 524288
//    | GlMultisampleBit = 536870912
//    | GlMultisampleBitArb = 536870912
//    | GlMultisampleBitExt = 536870912
//    | GlMultisampleBit3dfx = 536870912
//    | GlAllAttribBits = -1

//type ClearBufferMask = 
//    | GlCoverageBufferBitNv = 32768

//type ClientAttribMask = 
//    | GlClientPixelStoreBit = 1
//    | GlClientVertexArrayBit = 2
//    | GlClientAllAttribBits = -1

//type ContextFlagMask = 
//    | GlContextFlagForwardCompatibleBit = 1
//    | GlContextFlagDebugBit = 2
//    | GlContextFlagDebugBitKhr = 2
//    | GlContextFlagRobustAccessBit = 4
//    | GlContextFlagRobustAccessBitArb = 4
//    | GlContextFlagNoErrorBit = 8
//    | GlContextFlagNoErrorBitKhr = 8
//    | GlContextFlagProtectedContentBitExt = 16

//type ContextProfileMask = 
//    | GlContextCoreProfileBit = 1
//    | GlContextCompatibilityProfileBit = 2

//type MapBufferUsageMask = 
//    | GlMapReadBit = 1
//    | GlMapReadBitExt = 1
//    | GlMapWriteBit = 2
//    | GlMapWriteBitExt = 2
//    | GlMapInvalidateRangeBit = 4
//    | GlMapInvalidateRangeBitExt = 4
//    | GlMapInvalidateBufferBit = 8
//    | GlMapInvalidateBufferBitExt = 8
//    | GlMapFlushExplicitBit = 16
//    | GlMapFlushExplicitBitExt = 16
//    | GlMapUnsynchronizedBit = 32
//    | GlMapUnsynchronizedBitExt = 32
//    | GlMapPersistentBit = 64
//    | GlMapPersistentBitExt = 64
//    | GlMapCoherentBit = 128
//    | GlMapCoherentBitExt = 128
//    | GlDynamicStorageBit = 256
//    | GlDynamicStorageBitExt = 256
//    | GlClientStorageBit = 512
//    | GlClientStorageBitExt = 512
//    | GlSparseStorageBitArb = 1024
//    | GlLgpuSeparateStorageBitNvx = 2048
//    | GlPerGpuStorageBitNv = 2048
//    | GlExternalStorageBitNvx = 8192

//type MemoryBarrierMask = 
//    | GlVertexAttribArrayBarrierBit = 1
//    | GlVertexAttribArrayBarrierBitExt = 1
//    | GlElementArrayBarrierBit = 2
//    | GlElementArrayBarrierBitExt = 2
//    | GlUniformBarrierBit = 4
//    | GlUniformBarrierBitExt = 4
//    | GlTextureFetchBarrierBit = 8
//    | GlTextureFetchBarrierBitExt = 8
//    | GlShaderGlobalAccessBarrierBitNv = 16
//    | GlShaderImageAccessBarrierBit = 32
//    | GlShaderImageAccessBarrierBitExt = 32
//    | GlCommandBarrierBit = 64
//    | GlCommandBarrierBitExt = 64
//    | GlPixelBufferBarrierBit = 128
//    | GlPixelBufferBarrierBitExt = 128
//    | GlTextureUpdateBarrierBit = 256
//    | GlTextureUpdateBarrierBitExt = 256
//    | GlBufferUpdateBarrierBit = 512
//    | GlBufferUpdateBarrierBitExt = 512
//    | GlFramebufferBarrierBit = 1024
//    | GlFramebufferBarrierBitExt = 1024
//    | GlTransformFeedbackBarrierBit = 2048
//    | GlTransformFeedbackBarrierBitExt = 2048
//    | GlAtomicCounterBarrierBit = 4096
//    | GlAtomicCounterBarrierBitExt = 4096
//    | GlShaderStorageBarrierBit = 8192
//    | GlClientMappedBufferBarrierBit = 16384
//    | GlClientMappedBufferBarrierBitExt = 16384
//    | GlQueryBufferBarrierBit = 32768
//    | GlAllBarrierBits = -1
//    | GlAllBarrierBitsExt = -1

//type OcclusionQueryEventMaskAMD = 
//    | GlQueryDepthPassEventBitAmd = 1
//    | GlQueryDepthFailEventBitAmd = 2
//    | GlQueryStencilFailEventBitAmd = 4
//    | GlQueryDepthBoundsFailEventBitAmd = 8
//    | GlQueryAllEventBitsAmd = -1

//type SyncObjectMask = 
//    | GlSyncFlushCommandsBit = 1
//    | GlSyncFlushCommandsBitApple = 1

//type UseProgramStageMask = 
//    | GlVertexShaderBit = 1
//    | GlVertexShaderBitExt = 1
//    | GlFragmentShaderBit = 2
//    | GlFragmentShaderBitExt = 2
//    | GlGeometryShaderBit = 4
//    | GlGeometryShaderBitExt = 4
//    | GlGeometryShaderBitOes = 4
//    | GlTessControlShaderBit = 8
//    | GlTessControlShaderBitExt = 8
//    | GlTessControlShaderBitOes = 8
//    | GlTessEvaluationShaderBit = 16
//    | GlTessEvaluationShaderBitExt = 16
//    | GlTessEvaluationShaderBitOes = 16
//    | GlComputeShaderBit = 32
//    | GlMeshShaderBitNv = 64
//    | GlTaskShaderBitNv = 128
//    | GlAllShaderBits = -1
//    | GlAllShaderBitsExt = -1

//type TextureStorageMaskAMD = 
//    | GlTextureStorageSparseBitAmd = 1

//type FragmentShaderDestMaskATI = 
//    | GlRedBitAti = 1
//    | GlGreenBitAti = 2
//    | GlBlueBitAti = 4

//type FragmentShaderDestModMaskATI = 
//    | Gl2xBitAti = 1
//    | Gl4xBitAti = 2
//    | Gl8xBitAti = 4
//    | GlHalfBitAti = 8
//    | GlQuarterBitAti = 16
//    | GlEighthBitAti = 32
//    | GlSaturateBitAti = 64

//type FragmentShaderColorModMaskATI = 
//    | GlCompBitAti = 2
//    | GlNegateBitAti = 4
//    | GlBiasBitAti = 8

//type TraceMaskMESA = 
//    | GlTraceOperationsBitMesa = 1
//    | GlTracePrimitivesBitMesa = 2
//    | GlTraceArraysBitMesa = 4
//    | GlTraceTexturesBitMesa = 8
//    | GlTracePixelsBitMesa = 16
//    | GlTraceErrorsBitMesa = 32
//    | GlTraceAllBitsMesa = 65535

//type PathRenderingMaskNV = 
//    | GlBoldBitNv = 1
//    | GlItalicBitNv = 2
//    | GlGlyphWidthBitNv = 1
//    | GlGlyphHeightBitNv = 2
//    | GlGlyphHorizontalBearingXBitNv = 4
//    | GlGlyphHorizontalBearingYBitNv = 8
//    | GlGlyphHorizontalBearingAdvanceBitNv = 16
//    | GlGlyphVerticalBearingXBitNv = 32
//    | GlGlyphVerticalBearingYBitNv = 64
//    | GlGlyphVerticalBearingAdvanceBitNv = 128
//    | GlGlyphHasKerningBitNv = 256
//    | GlFontXMinBoundsBitNv = 65536
//    | GlFontYMinBoundsBitNv = 131072
//    | GlFontXMaxBoundsBitNv = 262144
//    | GlFontYMaxBoundsBitNv = 524288
//    | GlFontUnitsPerEmBitNv = 1048576
//    | GlFontAscenderBitNv = 2097152
//    | GlFontDescenderBitNv = 4194304
//    | GlFontHeightBitNv = 8388608
//    | GlFontMaxAdvanceWidthBitNv = 16777216
//    | GlFontMaxAdvanceHeightBitNv = 33554432
//    | GlFontUnderlinePositionBitNv = 67108864
//    | GlFontUnderlineThicknessBitNv = 134217728
//    | GlFontHasKerningBitNv = 268435456
//    | GlFontNumGlyphIndicesBitNv = 536870912

//type PerformanceQueryCapsMaskINTEL = 
//    | GlPerfquerySingleContextIntel = 0
//    | GlPerfqueryGlobalContextIntel = 1

//type VertexHintsMaskPGI = 
//    | GlVertex23BitPgi = 4
//    | GlVertex4BitPgi = 8
//    | GlColor3BitPgi = 65536
//    | GlColor4BitPgi = 131072
//    | GlEdgeflagBitPgi = 262144
//    | GlIndexBitPgi = 524288
//    | GlMatAmbientBitPgi = 1048576
//    | GlMatAmbientAndDiffuseBitPgi = 2097152
//    | GlMatDiffuseBitPgi = 4194304
//    | GlMatEmissionBitPgi = 8388608
//    | GlMatColorIndexesBitPgi = 16777216
//    | GlMatShininessBitPgi = 33554432
//    | GlMatSpecularBitPgi = 67108864
//    | GlNormalBitPgi = 134217728
//    | GlTexcoord1BitPgi = 268435456
//    | GlTexcoord2BitPgi = 536870912
//    | GlTexcoord3BitPgi = 1073741824
//    | GlTexcoord4BitPgi = -2147483648

//type BufferBitQCOM = 
//    | GlColorBufferBit0Qcom = 1
//    | GlColorBufferBit1Qcom = 2
//    | GlColorBufferBit2Qcom = 4
//    | GlColorBufferBit3Qcom = 8
//    | GlColorBufferBit4Qcom = 16
//    | GlColorBufferBit5Qcom = 32
//    | GlColorBufferBit6Qcom = 64
//    | GlColorBufferBit7Qcom = 128
//    | GlDepthBufferBit0Qcom = 256
//    | GlDepthBufferBit1Qcom = 512
//    | GlDepthBufferBit2Qcom = 1024
//    | GlDepthBufferBit3Qcom = 2048
//    | GlDepthBufferBit4Qcom = 4096
//    | GlDepthBufferBit5Qcom = 8192
//    | GlDepthBufferBit6Qcom = 16384
//    | GlDepthBufferBit7Qcom = 32768
//    | GlStencilBufferBit0Qcom = 65536
//    | GlStencilBufferBit1Qcom = 131072
//    | GlStencilBufferBit2Qcom = 262144
//    | GlStencilBufferBit3Qcom = 524288
//    | GlStencilBufferBit4Qcom = 1048576
//    | GlStencilBufferBit5Qcom = 2097152
//    | GlStencilBufferBit6Qcom = 4194304
//    | GlStencilBufferBit7Qcom = 8388608
//    | GlMultisampleBufferBit0Qcom = 16777216
//    | GlMultisampleBufferBit1Qcom = 33554432
//    | GlMultisampleBufferBit2Qcom = 67108864
//    | GlMultisampleBufferBit3Qcom = 134217728
//    | GlMultisampleBufferBit4Qcom = 268435456
//    | GlMultisampleBufferBit5Qcom = 536870912
//    | GlMultisampleBufferBit6Qcom = 1073741824
//    | GlMultisampleBufferBit7Qcom = -2147483648

//type FoveationConfigBitQCOM = 
//    | GlFoveationEnableBitQcom = 1
//    | GlFoveationScaledBinMethodBitQcom = 2
//    | GlFoveationSubsampledLayoutMethodBitQcom = 4

//type FfdMaskSGIX = 
//    | GlTextureDeformationBitSgix = 1
//    | GlGeometryDeformationBitSgix = 2

//type CommandOpcodesNV = 
//    | GlTerminateSequenceCommandNv = 0
//    | GlNopCommandNv = 1
//    | GlDrawElementsCommandNv = 2
//    | GlDrawArraysCommandNv = 3
//    | GlDrawElementsStripCommandNv = 4
//    | GlDrawArraysStripCommandNv = 5
//    | GlDrawElementsInstancedCommandNv = 6
//    | GlDrawArraysInstancedCommandNv = 7
//    | GlElementAddressCommandNv = 8
//    | GlAttributeAddressCommandNv = 9
//    | GlUniformAddressCommandNv = 10
//    | GlBlendColorCommandNv = 11
//    | GlStencilRefCommandNv = 12
//    | GlLineWidthCommandNv = 13
//    | GlPolygonOffsetCommandNv = 14
//    | GlAlphaRefCommandNv = 15
//    | GlViewportCommandNv = 16
//    | GlScissorCommandNv = 17
//    | GlFrontFaceCommandNv = 18

//type MapTextureFormatINTEL = 
//    | GlLayoutDefaultIntel = 0
//    | GlLayoutLinearIntel = 1
//    | GlLayoutLinearCpuCachedIntel = 2

//type PathRenderingTokenNV = 
//    | GlClosePathNv = 0
//    | GlMoveToNv = 2
//    | GlRelativeMoveToNv = 3
//    | GlLineToNv = 4
//    | GlRelativeLineToNv = 5
//    | GlHorizontalLineToNv = 6
//    | GlRelativeHorizontalLineToNv = 7
//    | GlVerticalLineToNv = 8
//    | GlRelativeVerticalLineToNv = 9
//    | GlQuadraticCurveToNv = 10
//    | GlRelativeQuadraticCurveToNv = 11
//    | GlCubicCurveToNv = 12
//    | GlRelativeCubicCurveToNv = 13
//    | GlSmoothQuadraticCurveToNv = 14
//    | GlRelativeSmoothQuadraticCurveToNv = 15
//    | GlSmoothCubicCurveToNv = 16
//    | GlRelativeSmoothCubicCurveToNv = 17
//    | GlSmallCcwArcToNv = 18
//    | GlRelativeSmallCcwArcToNv = 19
//    | GlSmallCwArcToNv = 20
//    | GlRelativeSmallCwArcToNv = 21
//    | GlLargeCcwArcToNv = 22
//    | GlRelativeLargeCcwArcToNv = 23
//    | GlLargeCwArcToNv = 24
//    | GlRelativeLargeCwArcToNv = 25
//    | GlConicCurveToNv = 26
//    | GlRelativeConicCurveToNv = 27
//    | GlSharedEdgeNv = 192
//    | GlRoundedRectNv = 232
//    | GlRelativeRoundedRectNv = 233
//    | GlRoundedRect2Nv = 234
//    | GlRelativeRoundedRect2Nv = 235
//    | GlRoundedRect4Nv = 236
//    | GlRelativeRoundedRect4Nv = 237
//    | GlRoundedRect8Nv = 238
//    | GlRelativeRoundedRect8Nv = 239
//    | GlRestartPathNv = 240
//    | GlDupFirstCubicCurveToNv = 242
//    | GlDupLastCubicCurveToNv = 244
//    | GlRectNv = 246
//    | GlRelativeRectNv = 247
//    | GlCircularCcwArcToNv = 248
//    | GlCircularCwArcToNv = 250
//    | GlCircularTangentArcToNv = 252
//    | GlArcToNv = 254
//    | GlRelativeArcToNv = 255

//type TransformFeedbackTokenNV = 
//    | GlNextBufferNv = -2
//    | GlSkipComponents4Nv = -3
//    | GlSkipComponents3Nv = -4
//    | GlSkipComponents2Nv = -5
//    | GlSkipComponents1Nv = -6

//type TriangleListSUN = 
//    | GlRestartSun = 1
//    | GlReplaceMiddleSun = 2
//    | GlReplaceOldestSun = 3

//type SpecialNumbers = 
//    | GlFalse = 0
//    | GlNoError = 0
//    | GlZero = 0
//    | GlNone = 0
//    | GlNoneOes = 0
//    | GlTrue = 1
//    | GlOne = 1
//    | GlInvalidIndex = -1
//    | GlAllPixelsAmd = -1
//    | GlTimeoutIgnored = -1
//    | GlTimeoutIgnoredApple = -1
//    | GlVersionEsCl10 = 1
//    | GlVersionEsCm11 = 1
//    | GlVersionEsCl11 = 1
//    | GlUuidSizeExt = 16
//    | GlLuidSizeExt = 8

//type RegisterCombinerPname = 
//    | GlCombine = 34160
//    | GlCombineArb = 34160
//    | GlCombineExt = 34160
//    | GlCombineRgb = 34161
//    | GlCombineRgbArb = 34161
//    | GlCombineRgbExt = 34161
//    | GlCombineAlpha = 34162
//    | GlCombineAlphaArb = 34162
//    | GlCombineAlphaExt = 34162
//    | GlRgbScale = 34163
//    | GlRgbScaleArb = 34163
//    | GlRgbScaleExt = 34163
//    | GlAddSigned = 34164
//    | GlAddSignedArb = 34164
//    | GlAddSignedExt = 34164
//    | GlInterpolate = 34165
//    | GlInterpolateArb = 34165
//    | GlInterpolateExt = 34165
//    | GlConstant = 34166
//    | GlConstantArb = 34166
//    | GlConstantExt = 34166
//    | GlConstantNv = 34166
//    | GlPrimaryColor = 34167
//    | GlPrimaryColorArb = 34167
//    | GlPrimaryColorExt = 34167
//    | GlPrevious = 34168
//    | GlPreviousArb = 34168
//    | GlPreviousExt = 34168
//    | GlSource0Rgb = 34176
//    | GlSource0RgbArb = 34176
//    | GlSource0RgbExt = 34176
//    | GlSrc0Rgb = 34176
//    | GlSource1Rgb = 34177
//    | GlSource1RgbArb = 34177
//    | GlSource1RgbExt = 34177
//    | GlSrc1Rgb = 34177
//    | GlSource2Rgb = 34178
//    | GlSource2RgbArb = 34178
//    | GlSource2RgbExt = 34178
//    | GlSrc2Rgb = 34178
//    | GlSource3RgbNv = 34179
//    | GlSource0Alpha = 34184
//    | GlSource0AlphaArb = 34184
//    | GlSource0AlphaExt = 34184
//    | GlSrc0Alpha = 34184
//    | GlSource1Alpha = 34185
//    | GlSource1AlphaArb = 34185
//    | GlSource1AlphaExt = 34185
//    | GlSrc1Alpha = 34185
//    | GlSrc1AlphaExt = 34185
//    | GlSource2Alpha = 34186
//    | GlSource2AlphaArb = 34186
//    | GlSource2AlphaExt = 34186
//    | GlSrc2Alpha = 34186
//    | GlSource3AlphaNv = 34187
//    | GlOperand0Rgb = 34192
//    | GlOperand0RgbArb = 34192
//    | GlOperand0RgbExt = 34192
//    | GlOperand1Rgb = 34193
//    | GlOperand1RgbArb = 34193
//    | GlOperand1RgbExt = 34193
//    | GlOperand2Rgb = 34194
//    | GlOperand2RgbArb = 34194
//    | GlOperand2RgbExt = 34194
//    | GlOperand3RgbNv = 34195
//    | GlOperand0Alpha = 34200
//    | GlOperand0AlphaArb = 34200
//    | GlOperand0AlphaExt = 34200
//    | GlOperand1Alpha = 34201
//    | GlOperand1AlphaArb = 34201
//    | GlOperand1AlphaExt = 34201
//    | GlOperand2Alpha = 34202
//    | GlOperand2AlphaArb = 34202
//    | GlOperand2AlphaExt = 34202
//    | GlOperand3AlphaNv = 34203

//type ShaderType = 
//    | GlFragmentShader = 35632
//    | GlFragmentShaderArb = 35632
//    | GlVertexShader = 35633
//    | GlVertexShaderArb = 35633

//type ContainerType = 
//    | GlProgramObjectArb = 35648
//    | GlProgramObjectExt = 35648

//type AttributeType = 
//    | GlFloatVec2 = 35664
//    | GlFloatVec2Arb = 35664
//    | GlFloatVec3 = 35665
//    | GlFloatVec3Arb = 35665
//    | GlFloatVec4 = 35666
//    | GlFloatVec4Arb = 35666
//    | GlIntVec2 = 35667
//    | GlIntVec2Arb = 35667
//    | GlIntVec3 = 35668
//    | GlIntVec3Arb = 35668
//    | GlIntVec4 = 35669
//    | GlIntVec4Arb = 35669
//    | GlBool = 35670
//    | GlBoolArb = 35670
//    | GlBoolVec2 = 35671
//    | GlBoolVec2Arb = 35671
//    | GlBoolVec3 = 35672
//    | GlBoolVec3Arb = 35672
//    | GlBoolVec4 = 35673
//    | GlBoolVec4Arb = 35673
//    | GlFloatMat2 = 35674
//    | GlFloatMat2Arb = 35674
//    | GlFloatMat3 = 35675
//    | GlFloatMat3Arb = 35675
//    | GlFloatMat4 = 35676
//    | GlFloatMat4Arb = 35676
//    | GlSampler1d = 35677
//    | GlSampler1dArb = 35677
//    | GlSampler2d = 35678
//    | GlSampler2dArb = 35678
//    | GlSampler3d = 35679
//    | GlSampler3dArb = 35679
//    | GlSampler3dOes = 35679
//    | GlSamplerCube = 35680
//    | GlSamplerCubeArb = 35680
//    | GlSampler1dShadow = 35681
//    | GlSampler1dShadowArb = 35681
//    | GlSampler2dShadow = 35682
//    | GlSampler2dShadowArb = 35682
//    | GlSampler2dShadowExt = 35682
//    | GlSampler2dRect = 35683
//    | GlSampler2dRectArb = 35683
//    | GlSampler2dRectShadow = 35684
//    | GlSampler2dRectShadowArb = 35684
//    | GlFloatMat23 = 35685
//    | GlFloatMat23Nv = 35685
//    | GlFloatMat24 = 35686
//    | GlFloatMat24Nv = 35686
//    | GlFloatMat32 = 35687
//    | GlFloatMat32Nv = 35687
//    | GlFloatMat34 = 35688
//    | GlFloatMat34Nv = 35688
//    | GlFloatMat42 = 35689
//    | GlFloatMat42Nv = 35689
//    | GlFloatMat43 = 35690
//    | GlFloatMat43Nv = 35690

//module GLRaw = 
//    [<Literal>]
//    let lib = "opengl"

//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAccum(nativeint op, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAccumxOES(nativeint op, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveProgramEXT(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveShaderProgram(nativeint pipeline, nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveShaderProgramEXT(nativeint pipeline, nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveStencilFaceEXT(nativeint face)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveTexture(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveTextureARB(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glActiveVaryingNV(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFragmentOp1ATI(nativeint op, nativeint dst, nativeint dstMod, nativeint arg1, nativeint arg1Rep, nativeint arg1Mod)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFragmentOp2ATI(nativeint op, nativeint dst, nativeint dstMod, nativeint arg1, nativeint arg1Rep, nativeint arg1Mod, nativeint arg2, nativeint arg2Rep, nativeint arg2Mod)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFragmentOp3ATI(nativeint op, nativeint dst, nativeint dstMod, nativeint arg1, nativeint arg1Rep, nativeint arg1Mod, nativeint arg2, nativeint arg2Rep, nativeint arg2Mod, nativeint arg3, nativeint arg3Rep, nativeint arg3Mod)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFunc(nativeint func, nativeint ref)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFuncQCOM(nativeint func, nativeint ref)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFuncx(nativeint func, nativeint ref)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaFuncxOES(nativeint func, nativeint ref)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAlphaToCoverageDitherControlNV(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glApplyFramebufferAttachmentCMAAINTEL()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glApplyTextureEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glAcquireKeyedMutexWin32EXT(nativeint memory, nativeint key, nativeint timeout)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glAreProgramsResidentNV(nativeint n, nativeint* programs, nativeint* residences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glAreTexturesResident(nativeint n, nativeint* textures, nativeint* residences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glAreTexturesResidentEXT(nativeint n, nativeint* textures, nativeint* residences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glArrayElement(nativeint i)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glArrayElementEXT(nativeint i)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glArrayObjectATI(nativeint array, nativeint size, nativeint _type, nativeint stride, nativeint buffer, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAsyncMarkerSGIX(nativeint marker)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAttachObjectARB(nativeint containerObj, nativeint obj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glAttachShader(nativeint program, nativeint shader)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBegin(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginConditionalRender(nativeint id, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginConditionalRenderNV(nativeint id, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginConditionalRenderNVX(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginFragmentShaderATI()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginOcclusionQueryNV(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginPerfMonitorAMD(nativeint monitor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginPerfQueryINTEL(nativeint queryHandle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginQuery(nativeint target, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginQueryARB(nativeint target, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginQueryEXT(nativeint target, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginQueryIndexed(nativeint target, nativeint index, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginTransformFeedback(nativeint primitiveMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginTransformFeedbackEXT(nativeint primitiveMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginTransformFeedbackNV(nativeint primitiveMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginVertexShaderEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBeginVideoCaptureNV(nativeint video_capture_slot)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindAttribLocation(nativeint program, nativeint index, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindAttribLocationARB(nativeint programObj, nativeint index, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBuffer(nativeint target, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferARB(nativeint target, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferBase(nativeint target, nativeint index, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferBaseEXT(nativeint target, nativeint index, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferBaseNV(nativeint target, nativeint index, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferOffsetEXT(nativeint target, nativeint index, nativeint buffer, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferOffsetNV(nativeint target, nativeint index, nativeint buffer, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferRange(nativeint target, nativeint index, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferRangeEXT(nativeint target, nativeint index, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBufferRangeNV(nativeint target, nativeint index, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBuffersBase(nativeint target, nativeint first, nativeint count, nativeint* buffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindBuffersRange(nativeint target, nativeint first, nativeint count, nativeint* buffers, nativeint* offsets, nativeint* sizes)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFragDataLocation(nativeint program, nativeint color, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFragDataLocationEXT(nativeint program, nativeint color, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFragDataLocationIndexed(nativeint program, nativeint colorNumber, nativeint index, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFragDataLocationIndexedEXT(nativeint program, nativeint colorNumber, nativeint index, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFragmentShaderATI(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFramebuffer(nativeint target, nativeint framebuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFramebufferEXT(nativeint target, nativeint framebuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindFramebufferOES(nativeint target, nativeint framebuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindImageTexture(nativeint unit, nativeint texture, nativeint level, nativeint layered, nativeint layer, nativeint access, nativeint format)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindImageTextureEXT(nativeint index, nativeint texture, nativeint level, nativeint layered, nativeint layer, nativeint access, nativeint format)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindImageTextures(nativeint first, nativeint count, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glBindLightParameterEXT(nativeint light, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glBindMaterialParameterEXT(nativeint face, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindMultiTextureEXT(nativeint texunit, nativeint target, nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glBindParameterEXT(nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindProgramARB(nativeint target, nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindProgramNV(nativeint target, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindProgramPipeline(nativeint pipeline)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindProgramPipelineEXT(nativeint pipeline)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindRenderbuffer(nativeint target, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindRenderbufferEXT(nativeint target, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindRenderbufferOES(nativeint target, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindSampler(nativeint unit, nativeint sampler)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindSamplers(nativeint first, nativeint count, nativeint* samplers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindShadingRateImageNV(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glBindTexGenParameterEXT(nativeint unit, nativeint coord, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindTexture(nativeint target, nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindTextureEXT(nativeint target, nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindTextureUnit(nativeint unit, nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glBindTextureUnitParameterEXT(nativeint unit, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindTextures(nativeint first, nativeint count, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindTransformFeedback(nativeint target, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindTransformFeedbackNV(nativeint target, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVertexArray(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVertexArrayAPPLE(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVertexArrayOES(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVertexBuffer(nativeint bindingindex, nativeint buffer, nativeint offset, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVertexBuffers(nativeint first, nativeint count, nativeint* buffers, nativeint* offsets, nativeint* strides)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVertexShaderEXT(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVideoCaptureStreamBufferNV(nativeint video_capture_slot, nativeint stream, nativeint frame_region, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBindVideoCaptureStreamTextureNV(nativeint video_capture_slot, nativeint stream, nativeint frame_region, nativeint target, nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3bEXT(nativeint bx, nativeint by, nativeint bz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3bvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3dEXT(nativeint bx, nativeint by, nativeint bz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3dvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3fEXT(nativeint bx, nativeint by, nativeint bz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3fvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3iEXT(nativeint bx, nativeint by, nativeint bz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3ivEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3sEXT(nativeint bx, nativeint by, nativeint bz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormal3svEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBinormalPointerEXT(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBitmap(nativeint width, nativeint height, nativeint xorig, nativeint yorig, nativeint xmove, nativeint ymove, nativeint* bitmap)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBitmapxOES(nativeint width, nativeint height, nativeint xorig, nativeint yorig, nativeint xmove, nativeint ymove, nativeint* bitmap)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendBarrier()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendBarrierKHR()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendBarrierNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendColor(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendColorEXT(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendColorxOES(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquation(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationIndexedAMD(nativeint buf, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationOES(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparate(nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparateEXT(nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparateIndexedAMD(nativeint buf, nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparateOES(nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparatei(nativeint buf, nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparateiARB(nativeint buf, nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparateiEXT(nativeint buf, nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationSeparateiOES(nativeint buf, nativeint modeRGB, nativeint modeAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationi(nativeint buf, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationiARB(nativeint buf, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationiEXT(nativeint buf, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendEquationiOES(nativeint buf, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFunc(nativeint sfactor, nativeint dfactor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncIndexedAMD(nativeint buf, nativeint src, nativeint dst)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparate(nativeint sfactorRGB, nativeint dfactorRGB, nativeint sfactorAlpha, nativeint dfactorAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateEXT(nativeint sfactorRGB, nativeint dfactorRGB, nativeint sfactorAlpha, nativeint dfactorAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateINGR(nativeint sfactorRGB, nativeint dfactorRGB, nativeint sfactorAlpha, nativeint dfactorAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateIndexedAMD(nativeint buf, nativeint srcRGB, nativeint dstRGB, nativeint srcAlpha, nativeint dstAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateOES(nativeint srcRGB, nativeint dstRGB, nativeint srcAlpha, nativeint dstAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparatei(nativeint buf, nativeint srcRGB, nativeint dstRGB, nativeint srcAlpha, nativeint dstAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateiARB(nativeint buf, nativeint srcRGB, nativeint dstRGB, nativeint srcAlpha, nativeint dstAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateiEXT(nativeint buf, nativeint srcRGB, nativeint dstRGB, nativeint srcAlpha, nativeint dstAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFuncSeparateiOES(nativeint buf, nativeint srcRGB, nativeint dstRGB, nativeint srcAlpha, nativeint dstAlpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFunci(nativeint buf, nativeint src, nativeint dst)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFunciARB(nativeint buf, nativeint src, nativeint dst)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFunciEXT(nativeint buf, nativeint src, nativeint dst)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendFunciOES(nativeint buf, nativeint src, nativeint dst)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlendParameteriNV(nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlitFramebuffer(nativeint srcX0, nativeint srcY0, nativeint srcX1, nativeint srcY1, nativeint dstX0, nativeint dstY0, nativeint dstX1, nativeint dstY1, nativeint mask, nativeint filter)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlitFramebufferANGLE(nativeint srcX0, nativeint srcY0, nativeint srcX1, nativeint srcY1, nativeint dstX0, nativeint dstY0, nativeint dstX1, nativeint dstY1, nativeint mask, nativeint filter)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlitFramebufferEXT(nativeint srcX0, nativeint srcY0, nativeint srcX1, nativeint srcY1, nativeint dstX0, nativeint dstY0, nativeint dstX1, nativeint dstY1, nativeint mask, nativeint filter)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlitFramebufferNV(nativeint srcX0, nativeint srcY0, nativeint srcX1, nativeint srcY1, nativeint dstX0, nativeint dstY0, nativeint dstX1, nativeint dstY1, nativeint mask, nativeint filter)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBlitNamedFramebuffer(nativeint readFramebuffer, nativeint drawFramebuffer, nativeint srcX0, nativeint srcY0, nativeint srcX1, nativeint srcY1, nativeint dstX0, nativeint dstY0, nativeint dstX1, nativeint dstY1, nativeint mask, nativeint filter)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferAddressRangeNV(nativeint pname, nativeint index, nativeint address, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferAttachMemoryNV(nativeint target, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferData(nativeint target, nativeint size, nativeint data, nativeint usage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferDataARB(nativeint target, nativeint size, nativeint data, nativeint usage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferPageCommitmentARB(nativeint target, nativeint offset, nativeint size, nativeint commit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferParameteriAPPLE(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferStorage(nativeint target, nativeint size, nativeint data, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferStorageEXT(nativeint target, nativeint size, nativeint data, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferStorageExternalEXT(nativeint target, nativeint offset, nativeint size, nativeint clientBuffer, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferStorageMemEXT(nativeint target, nativeint size, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferSubData(nativeint target, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glBufferSubDataARB(nativeint target, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCallCommandListNV(nativeint list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCallList(nativeint list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCallLists(nativeint n, nativeint _type, nativeint lists)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCheckFramebufferStatus(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCheckFramebufferStatusEXT(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCheckFramebufferStatusOES(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCheckNamedFramebufferStatus(nativeint framebuffer, nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCheckNamedFramebufferStatusEXT(nativeint framebuffer, nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClampColor(nativeint target, nativeint clamp)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClampColorARB(nativeint target, nativeint clamp)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClear(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearAccum(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearAccumxOES(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearBufferData(nativeint target, nativeint internalformat, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearBufferSubData(nativeint target, nativeint internalformat, nativeint offset, nativeint size, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearBufferfi(nativeint buffer, nativeint drawbuffer, nativeint depth, nativeint stencil)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearBufferfv(nativeint buffer, nativeint drawbuffer, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearBufferiv(nativeint buffer, nativeint drawbuffer, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearBufferuiv(nativeint buffer, nativeint drawbuffer, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearColor(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearColorIiEXT(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearColorIuiEXT(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearColorx(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearColorxOES(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearDepth(nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearDepthdNV(nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearDepthf(nativeint d)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearDepthfOES(nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearDepthx(nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearDepthxOES(nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearIndex(nativeint c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedBufferData(nativeint buffer, nativeint internalformat, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedBufferDataEXT(nativeint buffer, nativeint internalformat, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedBufferSubData(nativeint buffer, nativeint internalformat, nativeint offset, nativeint size, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedBufferSubDataEXT(nativeint buffer, nativeint internalformat, nativeint offset, nativeint size, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedFramebufferfi(nativeint framebuffer, nativeint buffer, nativeint drawbuffer, nativeint depth, nativeint stencil)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedFramebufferfv(nativeint framebuffer, nativeint buffer, nativeint drawbuffer, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedFramebufferiv(nativeint framebuffer, nativeint buffer, nativeint drawbuffer, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearNamedFramebufferuiv(nativeint framebuffer, nativeint buffer, nativeint drawbuffer, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearPixelLocalStorageuiEXT(nativeint offset, nativeint n, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearStencil(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearTexImage(nativeint texture, nativeint level, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearTexImageEXT(nativeint texture, nativeint level, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearTexSubImage(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClearTexSubImageEXT(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClientActiveTexture(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClientActiveTextureARB(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClientActiveVertexStreamATI(nativeint stream)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClientAttribDefaultEXT(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glClientWaitSync(nativeint sync, nativeint flags, nativeint timeout)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glClientWaitSyncAPPLE(nativeint sync, nativeint flags, nativeint timeout)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipControl(nativeint origin, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipControlEXT(nativeint origin, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlane(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlanef(nativeint p, nativeint* eqn)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlanefIMG(nativeint p, nativeint* eqn)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlanefOES(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlanex(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlanexIMG(nativeint p, nativeint* eqn)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glClipPlanexOES(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3b(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3bv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3d(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3f(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3fVertex3fSUN(nativeint r, nativeint g, nativeint b, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3fVertex3fvSUN(nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3hNV(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3i(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3s(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3ub(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3ubv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3ui(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3uiv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3us(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3usv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3xOES(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor3xvOES(nativeint* components)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4b(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4bv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4d(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4f(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4fNormal3fVertex3fSUN(nativeint r, nativeint g, nativeint b, nativeint a, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4fNormal3fVertex3fvSUN(nativeint* c, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4hNV(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4i(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4s(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ub(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ubVertex2fSUN(nativeint r, nativeint g, nativeint b, nativeint a, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ubVertex2fvSUN(nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ubVertex3fSUN(nativeint r, nativeint g, nativeint b, nativeint a, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ubVertex3fvSUN(nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ubv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4ui(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4uiv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4us(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4usv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4x(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4xOES(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColor4xvOES(nativeint* components)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorFormatNV(nativeint size, nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorFragmentOp1ATI(nativeint op, nativeint dst, nativeint dstMask, nativeint dstMod, nativeint arg1, nativeint arg1Rep, nativeint arg1Mod)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorFragmentOp2ATI(nativeint op, nativeint dst, nativeint dstMask, nativeint dstMod, nativeint arg1, nativeint arg1Rep, nativeint arg1Mod, nativeint arg2, nativeint arg2Rep, nativeint arg2Mod)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorFragmentOp3ATI(nativeint op, nativeint dst, nativeint dstMask, nativeint dstMod, nativeint arg1, nativeint arg1Rep, nativeint arg1Mod, nativeint arg2, nativeint arg2Rep, nativeint arg2Mod, nativeint arg3, nativeint arg3Rep, nativeint arg3Mod)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorMask(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorMaskIndexedEXT(nativeint index, nativeint r, nativeint g, nativeint b, nativeint a)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorMaski(nativeint index, nativeint r, nativeint g, nativeint b, nativeint a)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorMaskiEXT(nativeint index, nativeint r, nativeint g, nativeint b, nativeint a)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorMaskiOES(nativeint index, nativeint r, nativeint g, nativeint b, nativeint a)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorMaterial(nativeint face, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorP3ui(nativeint _type, nativeint color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorP3uiv(nativeint _type, nativeint* color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorP4ui(nativeint _type, nativeint color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorP4uiv(nativeint _type, nativeint* color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorPointer(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorPointerEXT(nativeint size, nativeint _type, nativeint stride, nativeint count, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorPointerListIBM(nativeint size, nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorPointervINTEL(nativeint size, nativeint _type, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorSubTable(nativeint target, nativeint start, nativeint count, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorSubTableEXT(nativeint target, nativeint start, nativeint count, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTable(nativeint target, nativeint internalformat, nativeint width, nativeint format, nativeint _type, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTableEXT(nativeint target, nativeint internalFormat, nativeint width, nativeint format, nativeint _type, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTableParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTableParameterfvSGI(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTableParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTableParameterivSGI(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glColorTableSGI(nativeint target, nativeint internalformat, nativeint width, nativeint format, nativeint _type, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerInputNV(nativeint stage, nativeint portion, nativeint variable, nativeint input, nativeint mapping, nativeint componentUsage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerOutputNV(nativeint stage, nativeint portion, nativeint abOutput, nativeint cdOutput, nativeint sumOutput, nativeint scale, nativeint bias, nativeint abDotProduct, nativeint cdDotProduct, nativeint muxSum)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerParameterfNV(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerParameterfvNV(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerParameteriNV(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerParameterivNV(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCombinerStageParameterfvNV(nativeint stage, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCommandListSegmentsNV(nativeint list, nativeint segments)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompileCommandListNV(nativeint list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompileShader(nativeint shader)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompileShaderARB(nativeint shaderObj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompileShaderIncludeARB(nativeint shader, nativeint count, nativeint* path, nativeint* length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedMultiTexImage1DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedMultiTexImage2DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedMultiTexImage3DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedMultiTexSubImage1DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedMultiTexSubImage2DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedMultiTexSubImage3DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage1D(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage1DARB(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage2D(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage2DARB(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage3D(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage3DARB(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexImage3DOES(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage1D(nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage1DARB(nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage2D(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage2DARB(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage3D(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage3DARB(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTexSubImage3DOES(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureImage1DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureImage2DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureImage3DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureSubImage1D(nativeint texture, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureSubImage1DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureSubImage2D(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureSubImage2DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureSubImage3D(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint imageSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCompressedTextureSubImage3DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint imageSize, nativeint bits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConservativeRasterParameterfNV(nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConservativeRasterParameteriNV(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionFilter1D(nativeint target, nativeint internalformat, nativeint width, nativeint format, nativeint _type, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionFilter1DEXT(nativeint target, nativeint internalformat, nativeint width, nativeint format, nativeint _type, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionFilter2D(nativeint target, nativeint internalformat, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionFilter2DEXT(nativeint target, nativeint internalformat, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterf(nativeint target, nativeint pname, nativeint _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterfEXT(nativeint target, nativeint pname, nativeint _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameteri(nativeint target, nativeint pname, nativeint _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameteriEXT(nativeint target, nativeint pname, nativeint _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterxOES(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glConvolutionParameterxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyBufferSubData(nativeint readTarget, nativeint writeTarget, nativeint readOffset, nativeint writeOffset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyBufferSubDataNV(nativeint readTarget, nativeint writeTarget, nativeint readOffset, nativeint writeOffset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyColorSubTable(nativeint target, nativeint start, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyColorSubTableEXT(nativeint target, nativeint start, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyColorTable(nativeint target, nativeint internalformat, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyColorTableSGI(nativeint target, nativeint internalformat, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyConvolutionFilter1D(nativeint target, nativeint internalformat, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyConvolutionFilter1DEXT(nativeint target, nativeint internalformat, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyConvolutionFilter2D(nativeint target, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyConvolutionFilter2DEXT(nativeint target, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyImageSubData(nativeint srcName, nativeint srcTarget, nativeint srcLevel, nativeint srcX, nativeint srcY, nativeint srcZ, nativeint dstName, nativeint dstTarget, nativeint dstLevel, nativeint dstX, nativeint dstY, nativeint dstZ, nativeint srcWidth, nativeint srcHeight, nativeint srcDepth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyImageSubDataEXT(nativeint srcName, nativeint srcTarget, nativeint srcLevel, nativeint srcX, nativeint srcY, nativeint srcZ, nativeint dstName, nativeint dstTarget, nativeint dstLevel, nativeint dstX, nativeint dstY, nativeint dstZ, nativeint srcWidth, nativeint srcHeight, nativeint srcDepth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyImageSubDataNV(nativeint srcName, nativeint srcTarget, nativeint srcLevel, nativeint srcX, nativeint srcY, nativeint srcZ, nativeint dstName, nativeint dstTarget, nativeint dstLevel, nativeint dstX, nativeint dstY, nativeint dstZ, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyImageSubDataOES(nativeint srcName, nativeint srcTarget, nativeint srcLevel, nativeint srcX, nativeint srcY, nativeint srcZ, nativeint dstName, nativeint dstTarget, nativeint dstLevel, nativeint dstX, nativeint dstY, nativeint dstZ, nativeint srcWidth, nativeint srcHeight, nativeint srcDepth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyMultiTexImage1DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyMultiTexImage2DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint height, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyMultiTexSubImage1DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyMultiTexSubImage2DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyMultiTexSubImage3DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyNamedBufferSubData(nativeint readBuffer, nativeint writeBuffer, nativeint readOffset, nativeint writeOffset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyPathNV(nativeint resultPath, nativeint srcPath)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyPixels(nativeint x, nativeint y, nativeint width, nativeint height, nativeint _type)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexImage1D(nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexImage1DEXT(nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexImage2D(nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint height, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexImage2DEXT(nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint height, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage1D(nativeint target, nativeint level, nativeint xoffset, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage1DEXT(nativeint target, nativeint level, nativeint xoffset, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage2D(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage2DEXT(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage3D(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage3DEXT(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTexSubImage3DOES(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureImage1DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureImage2DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint x, nativeint y, nativeint width, nativeint height, nativeint border)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureLevelsAPPLE(nativeint destinationTexture, nativeint sourceTexture, nativeint sourceBaseLevel, nativeint sourceLevelCount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureSubImage1D(nativeint texture, nativeint level, nativeint xoffset, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureSubImage1DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint x, nativeint y, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureSubImage2D(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureSubImage2DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureSubImage3D(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCopyTextureSubImage3DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverFillPathInstancedNV(nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint coverMode, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverFillPathNV(nativeint path, nativeint coverMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverStrokePathInstancedNV(nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint coverMode, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverStrokePathNV(nativeint path, nativeint coverMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverageMaskNV(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverageModulationNV(nativeint components)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverageModulationTableNV(nativeint n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCoverageOperationNV(nativeint operation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateBuffers(nativeint n, nativeint* buffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateCommandListsNV(nativeint n, nativeint* lists)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateFramebuffers(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateMemoryObjectsEXT(nativeint n, nativeint* memoryObjects)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreatePerfQueryINTEL(nativeint queryId, nativeint* queryHandle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateProgram()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateProgramObjectARB()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateProgramPipelines(nativeint n, nativeint* pipelines)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateQueries(nativeint target, nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateRenderbuffers(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateSamplers(nativeint n, nativeint* samplers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateShader(nativeint _type)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateShaderObjectARB(nativeint shaderType)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateShaderProgramEXT(nativeint _type, nativeint* string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateShaderProgramv(nativeint _type, nativeint count, nativeint* strings)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateShaderProgramvEXT(nativeint _type, nativeint count, nativeint* strings)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateStatesNV(nativeint n, nativeint* states)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glCreateSyncFromCLeventARB(nativeint* context, nativeint* event, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateTextures(nativeint target, nativeint n, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateTransformFeedbacks(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCreateVertexArrays(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCullFace(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCullParameterdvEXT(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCullParameterfvEXT(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCurrentPaletteMatrixARB(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glCurrentPaletteMatrixOES(nativeint matrixpaletteindex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageCallback(nativeint callback, nativeint userParam)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageCallbackAMD(nativeint callback, nativeint userParam)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageCallbackARB(nativeint callback, nativeint userParam)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageCallbackKHR(nativeint callback, nativeint userParam)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageControl(nativeint source, nativeint _type, nativeint severity, nativeint count, nativeint* ids, nativeint enabled)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageControlARB(nativeint source, nativeint _type, nativeint severity, nativeint count, nativeint* ids, nativeint enabled)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageControlKHR(nativeint source, nativeint _type, nativeint severity, nativeint count, nativeint* ids, nativeint enabled)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageEnableAMD(nativeint category, nativeint severity, nativeint count, nativeint* ids, nativeint enabled)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageInsert(nativeint source, nativeint _type, nativeint id, nativeint severity, nativeint length, nativeint* buf)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageInsertAMD(nativeint category, nativeint severity, nativeint id, nativeint length, nativeint* buf)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageInsertARB(nativeint source, nativeint _type, nativeint id, nativeint severity, nativeint length, nativeint* buf)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDebugMessageInsertKHR(nativeint source, nativeint _type, nativeint id, nativeint severity, nativeint length, nativeint* buf)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeformSGIX(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeformationMap3dSGIX(nativeint target, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint w1, nativeint w2, nativeint wstride, nativeint worder, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeformationMap3fSGIX(nativeint target, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint w1, nativeint w2, nativeint wstride, nativeint worder, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteAsyncMarkersSGIX(nativeint marker, nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteBuffers(nativeint n, nativeint* buffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteBuffersARB(nativeint n, nativeint* buffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteCommandListsNV(nativeint n, nativeint* lists)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteFencesAPPLE(nativeint n, nativeint* fences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteFencesNV(nativeint n, nativeint* fences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteFragmentShaderATI(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteFramebuffers(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteFramebuffersEXT(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteFramebuffersOES(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteLists(nativeint list, nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteMemoryObjectsEXT(nativeint n, nativeint* memoryObjects)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteNamedStringARB(nativeint namelen, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteNamesAMD(nativeint identifier, nativeint num, nativeint* names)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteObjectARB(nativeint obj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteOcclusionQueriesNV(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeletePathsNV(nativeint path, nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeletePerfMonitorsAMD(nativeint n, nativeint* monitors)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeletePerfQueryINTEL(nativeint queryHandle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteProgram(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteProgramPipelines(nativeint n, nativeint* pipelines)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteProgramPipelinesEXT(nativeint n, nativeint* pipelines)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteProgramsARB(nativeint n, nativeint* programs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteProgramsNV(nativeint n, nativeint* programs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteQueries(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteQueriesARB(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteQueriesEXT(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteQueryResourceTagNV(nativeint n, nativeint* tagIds)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteRenderbuffers(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteRenderbuffersEXT(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteRenderbuffersOES(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteSamplers(nativeint count, nativeint* samplers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteSemaphoresEXT(nativeint n, nativeint* semaphores)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteShader(nativeint shader)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteStatesNV(nativeint n, nativeint* states)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteSync(nativeint sync)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteSyncAPPLE(nativeint sync)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteTextures(nativeint n, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteTexturesEXT(nativeint n, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteTransformFeedbacks(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteTransformFeedbacksNV(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteVertexArrays(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteVertexArraysAPPLE(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteVertexArraysOES(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDeleteVertexShaderEXT(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthBoundsEXT(nativeint zmin, nativeint zmax)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthBoundsdNV(nativeint zmin, nativeint zmax)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthFunc(nativeint func)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthMask(nativeint flag)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRange(nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangeArrayfvNV(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangeArrayfvOES(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangeArrayv(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangeIndexed(nativeint index, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangeIndexedfNV(nativeint index, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangeIndexedfOES(nativeint index, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangedNV(nativeint zNear, nativeint zFar)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangef(nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangefOES(nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangex(nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDepthRangexOES(nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDetachObjectARB(nativeint containerObj, nativeint attachedObj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDetachShader(nativeint program, nativeint shader)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDetailTexFuncSGIS(nativeint target, nativeint n, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisable(nativeint cap)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableClientState(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableClientStateIndexedEXT(nativeint array, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableClientStateiEXT(nativeint array, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableDriverControlQCOM(nativeint driverControl)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableIndexedEXT(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVariantClientStateEXT(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVertexArrayAttrib(nativeint vaobj, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVertexArrayAttribEXT(nativeint vaobj, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVertexArrayEXT(nativeint vaobj, nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVertexAttribAPPLE(nativeint index, nativeint pname)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVertexAttribArray(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableVertexAttribArrayARB(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisablei(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableiEXT(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableiNV(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDisableiOES(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDiscardFramebufferEXT(nativeint target, nativeint numAttachments, nativeint* attachments)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDispatchCompute(nativeint num_groups_x, nativeint num_groups_y, nativeint num_groups_z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDispatchComputeGroupSizeARB(nativeint num_groups_x, nativeint num_groups_y, nativeint num_groups_z, nativeint group_size_x, nativeint group_size_y, nativeint group_size_z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDispatchComputeIndirect(nativeint indirect)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArrays(nativeint mode, nativeint first, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysEXT(nativeint mode, nativeint first, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysIndirect(nativeint mode, nativeint indirect)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstanced(nativeint mode, nativeint first, nativeint count, nativeint instancecount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstancedANGLE(nativeint mode, nativeint first, nativeint count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstancedARB(nativeint mode, nativeint first, nativeint count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstancedBaseInstance(nativeint mode, nativeint first, nativeint count, nativeint instancecount, nativeint baseinstance)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstancedBaseInstanceEXT(nativeint mode, nativeint first, nativeint count, nativeint instancecount, nativeint baseinstance)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstancedEXT(nativeint mode, nativeint start, nativeint count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawArraysInstancedNV(nativeint mode, nativeint first, nativeint count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffer(nativeint buf)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffers(nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffersARB(nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffersATI(nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffersEXT(nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffersIndexedEXT(nativeint n, nativeint* location, nativeint* indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawBuffersNV(nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawCommandsAddressNV(nativeint primitiveMode, nativeint* indirects, nativeint* sizes, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawCommandsNV(nativeint primitiveMode, nativeint buffer, nativeint* indirects, nativeint* sizes, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawCommandsStatesAddressNV(nativeint* indirects, nativeint* sizes, nativeint* states, nativeint* fbos, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawCommandsStatesNV(nativeint buffer, nativeint* indirects, nativeint* sizes, nativeint* states, nativeint* fbos, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementArrayAPPLE(nativeint mode, nativeint first, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementArrayATI(nativeint mode, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElements(nativeint mode, nativeint count, nativeint _type, nativeint indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsBaseVertex(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsBaseVertexEXT(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsBaseVertexOES(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsIndirect(nativeint mode, nativeint _type, nativeint indirect)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstanced(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedANGLE(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedARB(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseInstance(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint baseinstance)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseInstanceEXT(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint baseinstance)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseVertex(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseVertexBaseInstance(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint basevertex, nativeint baseinstance)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseVertexBaseInstanceEXT(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint basevertex, nativeint baseinstance)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseVertexEXT(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedBaseVertexOES(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint instancecount, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedEXT(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawElementsInstancedNV(nativeint mode, nativeint count, nativeint _type, nativeint indices, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawMeshArraysSUN(nativeint mode, nativeint first, nativeint count, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawMeshTasksNV(nativeint first, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawMeshTasksIndirectNV(nativeint indirect)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawPixels(nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElementArrayAPPLE(nativeint mode, nativeint start, nativeint _end, nativeint first, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElementArrayATI(nativeint mode, nativeint start, nativeint _end, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElements(nativeint mode, nativeint start, nativeint _end, nativeint count, nativeint _type, nativeint indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElementsBaseVertex(nativeint mode, nativeint start, nativeint _end, nativeint count, nativeint _type, nativeint indices, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElementsBaseVertexEXT(nativeint mode, nativeint start, nativeint _end, nativeint count, nativeint _type, nativeint indices, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElementsBaseVertexOES(nativeint mode, nativeint start, nativeint _end, nativeint count, nativeint _type, nativeint indices, nativeint basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawRangeElementsEXT(nativeint mode, nativeint start, nativeint _end, nativeint count, nativeint _type, nativeint indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexfOES(nativeint x, nativeint y, nativeint z, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexfvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexiOES(nativeint x, nativeint y, nativeint z, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexivOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexsOES(nativeint x, nativeint y, nativeint z, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexsvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTextureNV(nativeint texture, nativeint sampler, nativeint x0, nativeint y0, nativeint x1, nativeint y1, nativeint z, nativeint s0, nativeint t0, nativeint s1, nativeint t1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexxOES(nativeint x, nativeint y, nativeint z, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTexxvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedback(nativeint mode, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedbackEXT(nativeint mode, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedbackInstanced(nativeint mode, nativeint id, nativeint instancecount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedbackInstancedEXT(nativeint mode, nativeint id, nativeint instancecount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedbackNV(nativeint mode, nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedbackStream(nativeint mode, nativeint id, nativeint stream)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawTransformFeedbackStreamInstanced(nativeint mode, nativeint id, nativeint stream, nativeint instancecount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEGLImageTargetRenderbufferStorageOES(nativeint target, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEGLImageTargetTexStorageEXT(nativeint target, nativeint image, nativeint* attrib_list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEGLImageTargetTexture2DOES(nativeint target, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEGLImageTargetTextureStorageEXT(nativeint texture, nativeint image, nativeint* attrib_list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEdgeFlag(nativeint flag)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEdgeFlagFormatNV(nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEdgeFlagPointer(nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEdgeFlagPointerEXT(nativeint stride, nativeint count, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEdgeFlagPointerListIBM(nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEdgeFlagv(nativeint* flag)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glElementPointerAPPLE(nativeint _type, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glElementPointerATI(nativeint _type, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnable(nativeint cap)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableClientState(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableClientStateIndexedEXT(nativeint array, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableClientStateiEXT(nativeint array, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableDriverControlQCOM(nativeint driverControl)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableIndexedEXT(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVariantClientStateEXT(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVertexArrayAttrib(nativeint vaobj, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVertexArrayAttribEXT(nativeint vaobj, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVertexArrayEXT(nativeint vaobj, nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVertexAttribAPPLE(nativeint index, nativeint pname)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVertexAttribArray(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableVertexAttribArrayARB(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnablei(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableiEXT(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableiNV(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnableiOES(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEnd()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndConditionalRender()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndConditionalRenderNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndConditionalRenderNVX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndFragmentShaderATI()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndList()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndOcclusionQueryNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndPerfMonitorAMD(nativeint monitor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndPerfQueryINTEL(nativeint queryHandle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndQuery(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndQueryARB(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndQueryEXT(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndQueryIndexed(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndTilingQCOM(nativeint preserveMask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndTransformFeedback()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndTransformFeedbackEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndTransformFeedbackNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndVertexShaderEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEndVideoCaptureNV(nativeint video_capture_slot)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord1d(nativeint u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord1dv(nativeint* u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord1f(nativeint u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord1fv(nativeint* u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord1xOES(nativeint u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord1xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord2d(nativeint u, nativeint v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord2dv(nativeint* u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord2f(nativeint u, nativeint v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord2fv(nativeint* u)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord2xOES(nativeint u, nativeint v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalCoord2xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalMapsNV(nativeint target, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalMesh1(nativeint mode, nativeint i1, nativeint i2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalMesh2(nativeint mode, nativeint i1, nativeint i2, nativeint j1, nativeint j2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalPoint1(nativeint i)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvalPoint2(nativeint i, nativeint j)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glEvaluateDepthValuesARB()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExecuteProgramNV(nativeint target, nativeint id, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetBufferPointervQCOM(nativeint target, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetBuffersQCOM(nativeint* buffers, nativeint maxBuffers, nativeint* numBuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetFramebuffersQCOM(nativeint* framebuffers, nativeint maxFramebuffers, nativeint* numFramebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetProgramBinarySourceQCOM(nativeint program, nativeint shadertype, nativeint* source, nativeint* length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetProgramsQCOM(nativeint* programs, nativeint maxPrograms, nativeint* numPrograms)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetRenderbuffersQCOM(nativeint* renderbuffers, nativeint maxRenderbuffers, nativeint* numRenderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetShadersQCOM(nativeint* shaders, nativeint maxShaders, nativeint* numShaders)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetTexLevelParameterivQCOM(nativeint texture, nativeint face, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetTexSubImageQCOM(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint texels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtGetTexturesQCOM(nativeint* textures, nativeint maxTextures, nativeint* numTextures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glExtIsProgramBinaryQCOM(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtTexObjectStateOverrideiQCOM(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glExtractComponentEXT(nativeint res, nativeint src, nativeint num)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFeedbackBuffer(nativeint size, nativeint _type, nativeint* buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFeedbackBufferxOES(nativeint n, nativeint _type, nativeint* buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glFenceSync(nativeint condition, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glFenceSyncAPPLE(nativeint condition, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFinalCombinerInputNV(nativeint variable, nativeint input, nativeint mapping, nativeint componentUsage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFinish()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glFinishAsyncSGIX(nativeint* markerp)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFinishFenceAPPLE(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFinishFenceNV(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFinishObjectAPPLE(nativeint _object, nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFinishTextureSUNX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlush()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushMappedBufferRange(nativeint target, nativeint offset, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushMappedBufferRangeAPPLE(nativeint target, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushMappedBufferRangeEXT(nativeint target, nativeint offset, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushMappedNamedBufferRange(nativeint buffer, nativeint offset, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushMappedNamedBufferRangeEXT(nativeint buffer, nativeint offset, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushPixelDataRangeNV(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushRasterSGIX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushStaticDataIBM(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushVertexArrayRangeAPPLE(nativeint length, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFlushVertexArrayRangeNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordFormatNV(nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordPointer(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordPointerEXT(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordPointerListIBM(nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordd(nativeint coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoorddEXT(nativeint coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoorddv(nativeint* coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoorddvEXT(nativeint* coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordf(nativeint coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordfEXT(nativeint coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordfv(nativeint* coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordfvEXT(nativeint* coord)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordhNV(nativeint fog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogCoordhvNV(nativeint* fog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogFuncSGIS(nativeint n, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogf(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogfv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogi(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogiv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogx(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogxOES(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogxv(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFogxvOES(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentColorMaterialSGIX(nativeint face, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentCoverageColorNV(nativeint color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightModelfSGIX(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightModelfvSGIX(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightModeliSGIX(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightModelivSGIX(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightfSGIX(nativeint light, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightfvSGIX(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightiSGIX(nativeint light, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentLightivSGIX(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentMaterialfSGIX(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentMaterialfvSGIX(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentMaterialiSGIX(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFragmentMaterialivSGIX(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrameTerminatorGREMEDY()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrameZoomSGIX(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferDrawBufferEXT(nativeint framebuffer, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferDrawBuffersEXT(nativeint framebuffer, nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferFetchBarrierEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferFetchBarrierQCOM()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferFoveationConfigQCOM(nativeint framebuffer, nativeint numLayers, nativeint focalPointsPerLayer, nativeint requestedFeatures, nativeint* providedFeatures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferFoveationParametersQCOM(nativeint framebuffer, nativeint layer, nativeint focalPoint, nativeint focalX, nativeint focalY, nativeint gainX, nativeint gainY, nativeint foveaArea)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferParameteri(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferPixelLocalStorageSizeEXT(nativeint target, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferReadBufferEXT(nativeint framebuffer, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferRenderbuffer(nativeint target, nativeint attachment, nativeint renderbuffertarget, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferRenderbufferEXT(nativeint target, nativeint attachment, nativeint renderbuffertarget, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferRenderbufferOES(nativeint target, nativeint attachment, nativeint renderbuffertarget, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferSampleLocationsfvARB(nativeint target, nativeint start, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferSampleLocationsfvNV(nativeint target, nativeint start, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferSamplePositionsfvAMD(nativeint target, nativeint numsamples, nativeint pixelindex, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture(nativeint target, nativeint attachment, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture1D(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture1DEXT(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture2D(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture2DEXT(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture2DDownsampleIMG(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint xscale, nativeint yscale)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture2DMultisampleEXT(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint samples)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture2DMultisampleIMG(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint samples)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture2DOES(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture3D(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint zoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture3DEXT(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint zoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTexture3DOES(nativeint target, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint zoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureARB(nativeint target, nativeint attachment, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureEXT(nativeint target, nativeint attachment, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureFaceARB(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint face)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureFaceEXT(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint face)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureLayer(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint layer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureLayerARB(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint layer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureLayerEXT(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint layer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureLayerDownsampleIMG(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint layer, nativeint xscale, nativeint yscale)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureMultisampleMultiviewOVR(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint samples, nativeint baseViewIndex, nativeint numViews)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureMultiviewOVR(nativeint target, nativeint attachment, nativeint texture, nativeint level, nativeint baseViewIndex, nativeint numViews)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFramebufferTextureOES(nativeint target, nativeint attachment, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFreeObjectBufferATI(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrontFace(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrustum(nativeint left, nativeint right, nativeint bottom, nativeint top, nativeint zNear, nativeint zFar)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrustumf(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrustumfOES(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrustumx(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glFrustumxOES(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGenAsyncMarkersSGIX(nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenBuffers(nativeint n, nativeint* buffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenBuffersARB(nativeint n, nativeint* buffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenFencesAPPLE(nativeint n, nativeint* fences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenFencesNV(nativeint n, nativeint* fences)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGenFragmentShadersATI(nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenFramebuffers(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenFramebuffersEXT(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenFramebuffersOES(nativeint n, nativeint* framebuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGenLists(nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenNamesAMD(nativeint identifier, nativeint num, nativeint* names)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenOcclusionQueriesNV(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGenPathsNV(nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenPerfMonitorsAMD(nativeint n, nativeint* monitors)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenProgramPipelines(nativeint n, nativeint* pipelines)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenProgramPipelinesEXT(nativeint n, nativeint* pipelines)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenProgramsARB(nativeint n, nativeint* programs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenProgramsNV(nativeint n, nativeint* programs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenQueries(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenQueriesARB(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenQueriesEXT(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenQueryResourceTagNV(nativeint n, nativeint* tagIds)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenRenderbuffers(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenRenderbuffersEXT(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenRenderbuffersOES(nativeint n, nativeint* renderbuffers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenSamplers(nativeint count, nativeint* samplers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenSemaphoresEXT(nativeint n, nativeint* semaphores)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGenSymbolsEXT(nativeint datatype, nativeint storagetype, nativeint range, nativeint components)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenTextures(nativeint n, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenTexturesEXT(nativeint n, nativeint* textures)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenTransformFeedbacks(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenTransformFeedbacksNV(nativeint n, nativeint* ids)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenVertexArrays(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenVertexArraysAPPLE(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenVertexArraysOES(nativeint n, nativeint* arrays)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGenVertexShadersEXT(nativeint range)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenerateMipmap(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenerateMipmapEXT(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenerateMipmapOES(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenerateMultiTexMipmapEXT(nativeint texunit, nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenerateTextureMipmap(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGenerateTextureMipmapEXT(nativeint texture, nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveAtomicCounterBufferiv(nativeint program, nativeint bufferIndex, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveAttrib(nativeint program, nativeint index, nativeint bufSize, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveAttribARB(nativeint programObj, nativeint index, nativeint maxLength, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveSubroutineName(nativeint program, nativeint shadertype, nativeint index, nativeint bufsize, nativeint* length, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveSubroutineUniformName(nativeint program, nativeint shadertype, nativeint index, nativeint bufsize, nativeint* length, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveSubroutineUniformiv(nativeint program, nativeint shadertype, nativeint index, nativeint pname, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveUniform(nativeint program, nativeint index, nativeint bufSize, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveUniformARB(nativeint programObj, nativeint index, nativeint maxLength, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveUniformBlockName(nativeint program, nativeint uniformBlockIndex, nativeint bufSize, nativeint* length, nativeint* uniformBlockName)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveUniformBlockiv(nativeint program, nativeint uniformBlockIndex, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveUniformName(nativeint program, nativeint uniformIndex, nativeint bufSize, nativeint* length, nativeint* uniformName)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveUniformsiv(nativeint program, nativeint uniformCount, nativeint* uniformIndices, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetActiveVaryingNV(nativeint program, nativeint index, nativeint bufSize, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetArrayObjectfvATI(nativeint array, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetArrayObjectivATI(nativeint array, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetAttachedObjectsARB(nativeint containerObj, nativeint maxCount, nativeint* count, nativeint* obj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetAttachedShaders(nativeint program, nativeint maxCount, nativeint* count, nativeint* shaders)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetAttribLocation(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetAttribLocationARB(nativeint programObj, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBooleanIndexedvEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBooleani_v(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBooleanv(nativeint pname, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferParameteri64v(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferParameterivARB(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferParameterui64vNV(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferPointerv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferPointervARB(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferPointervOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferSubData(nativeint target, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetBufferSubDataARB(nativeint target, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetClipPlane(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetClipPlanef(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetClipPlanefOES(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetClipPlanex(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetClipPlanexOES(nativeint plane, nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTable(nativeint target, nativeint format, nativeint _type, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableEXT(nativeint target, nativeint format, nativeint _type, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableParameterfvSGI(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableParameterivSGI(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetColorTableSGI(nativeint target, nativeint format, nativeint _type, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCombinerInputParameterfvNV(nativeint stage, nativeint portion, nativeint variable, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCombinerInputParameterivNV(nativeint stage, nativeint portion, nativeint variable, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCombinerOutputParameterfvNV(nativeint stage, nativeint portion, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCombinerOutputParameterivNV(nativeint stage, nativeint portion, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCombinerStageParameterfvNV(nativeint stage, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetCommandHeaderNV(nativeint tokenID, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCompressedMultiTexImageEXT(nativeint texunit, nativeint target, nativeint lod, nativeint img)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCompressedTexImage(nativeint target, nativeint level, nativeint img)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCompressedTexImageARB(nativeint target, nativeint level, nativeint img)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCompressedTextureImage(nativeint texture, nativeint level, nativeint bufSize, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCompressedTextureImageEXT(nativeint texture, nativeint target, nativeint lod, nativeint img)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCompressedTextureSubImage(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint bufSize, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionFilter(nativeint target, nativeint format, nativeint _type, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionFilterEXT(nativeint target, nativeint format, nativeint _type, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetConvolutionParameterxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetCoverageModulationTableNV(nativeint bufsize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetDebugMessageLog(nativeint count, nativeint bufSize, nativeint* sources, nativeint* types, nativeint* ids, nativeint* severities, nativeint* lengths, nativeint* messageLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetDebugMessageLogAMD(nativeint count, nativeint bufsize, nativeint* categories, nativeint* severities, nativeint* ids, nativeint* lengths, nativeint* message)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetDebugMessageLogARB(nativeint count, nativeint bufSize, nativeint* sources, nativeint* types, nativeint* ids, nativeint* severities, nativeint* lengths, nativeint* messageLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetDebugMessageLogKHR(nativeint count, nativeint bufSize, nativeint* sources, nativeint* types, nativeint* ids, nativeint* severities, nativeint* lengths, nativeint* messageLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDetailTexFuncSGIS(nativeint target, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDoubleIndexedvEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDoublei_v(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDoublei_vEXT(nativeint pname, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDoublev(nativeint pname, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDriverControlStringQCOM(nativeint driverControl, nativeint bufSize, nativeint* length, nativeint* driverControlString)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetDriverControlsQCOM(nativeint* num, nativeint size, nativeint* driverControls)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetError()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFenceivNV(nativeint fence, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFinalCombinerInputParameterfvNV(nativeint variable, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFinalCombinerInputParameterivNV(nativeint variable, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFirstPerfQueryIdINTEL(nativeint* queryId)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFixedv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFixedvOES(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFloatIndexedvEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFloati_v(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFloati_vEXT(nativeint pname, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFloati_vNV(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFloati_vOES(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFloatv(nativeint pname, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFogFuncSGIS(nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetFragDataIndex(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetFragDataIndexEXT(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetFragDataLocation(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetFragDataLocationEXT(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFragmentLightfvSGIX(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFragmentLightivSGIX(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFragmentMaterialfvSGIX(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFragmentMaterialivSGIX(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFramebufferAttachmentParameteriv(nativeint target, nativeint attachment, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFramebufferAttachmentParameterivEXT(nativeint target, nativeint attachment, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFramebufferAttachmentParameterivOES(nativeint target, nativeint attachment, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFramebufferParameterfvAMD(nativeint target, nativeint pname, nativeint numsamples, nativeint pixelindex, nativeint size, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFramebufferParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetFramebufferParameterivEXT(nativeint framebuffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetFramebufferPixelLocalStorageSizeEXT(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetGraphicsResetStatus()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetGraphicsResetStatusARB()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetGraphicsResetStatusEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetGraphicsResetStatusKHR()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetHandleARB(nativeint pname)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogram(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogramEXT(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogramParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogramParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogramParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogramParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetHistogramParameterxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetImageHandleARB(nativeint texture, nativeint level, nativeint layered, nativeint layer, nativeint format)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetImageHandleNV(nativeint texture, nativeint level, nativeint layered, nativeint layer, nativeint format)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetImageTransformParameterfvHP(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetImageTransformParameterivHP(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInfoLogARB(nativeint obj, nativeint maxLength, nativeint* length, nativeint* infoLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetInstrumentsSGIX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInteger64i_v(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInteger64v(nativeint pname, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInteger64vAPPLE(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetIntegerIndexedvEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetIntegeri_v(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetIntegeri_vEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetIntegerui64i_vNV(nativeint value, nativeint index, nativeint* result)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetIntegerui64vNV(nativeint value, nativeint* result)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetIntegerv(nativeint pname, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInternalformatSampleivNV(nativeint target, nativeint internalformat, nativeint samples, nativeint pname, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInternalformati64v(nativeint target, nativeint internalformat, nativeint pname, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInternalformativ(nativeint target, nativeint internalformat, nativeint pname, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInvariantBooleanvEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInvariantFloatvEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetInvariantIntegervEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLightfv(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLightiv(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLightxOES(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLightxv(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLightxvOES(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetListParameterfvSGIX(nativeint list, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetListParameterivSGIX(nativeint list, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLocalConstantBooleanvEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLocalConstantFloatvEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetLocalConstantIntegervEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapAttribParameterfvNV(nativeint target, nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapAttribParameterivNV(nativeint target, nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapControlPointsNV(nativeint target, nativeint index, nativeint _type, nativeint ustride, nativeint vstride, nativeint packed, nativeint points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapParameterfvNV(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapParameterivNV(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapdv(nativeint target, nativeint query, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapfv(nativeint target, nativeint query, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapiv(nativeint target, nativeint query, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMapxvOES(nativeint target, nativeint query, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMaterialfv(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMaterialiv(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMaterialxOES(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMaterialxv(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMaterialxvOES(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMemoryObjectDetachedResourcesuivNV(nativeint memory, nativeint pname, nativeint first, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMemoryObjectParameterivEXT(nativeint memoryObject, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMinmax(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMinmaxEXT(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMinmaxParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMinmaxParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMinmaxParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMinmaxParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexEnvfvEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexEnvivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexGendvEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexGenfvEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexGenivEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexImageEXT(nativeint texunit, nativeint target, nativeint level, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexLevelParameterfvEXT(nativeint texunit, nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexLevelParameterivEXT(nativeint texunit, nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexParameterIivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexParameterIuivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexParameterfvEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultiTexParameterivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultisamplefv(nativeint pname, nativeint index, nativeint* _val)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetMultisamplefvNV(nativeint pname, nativeint index, nativeint* _val)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferParameteri64v(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferParameteriv(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferParameterivEXT(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferParameterui64vNV(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferPointerv(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferPointervEXT(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferSubData(nativeint buffer, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedBufferSubDataEXT(nativeint buffer, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedFramebufferParameterfvAMD(nativeint framebuffer, nativeint pname, nativeint numsamples, nativeint pixelindex, nativeint size, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedFramebufferAttachmentParameteriv(nativeint framebuffer, nativeint attachment, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedFramebufferAttachmentParameterivEXT(nativeint framebuffer, nativeint attachment, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedFramebufferParameteriv(nativeint framebuffer, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedFramebufferParameterivEXT(nativeint framebuffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedProgramLocalParameterIivEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedProgramLocalParameterIuivEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedProgramLocalParameterdvEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedProgramLocalParameterfvEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedProgramStringEXT(nativeint program, nativeint target, nativeint pname, nativeint string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedProgramivEXT(nativeint program, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedRenderbufferParameteriv(nativeint renderbuffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedRenderbufferParameterivEXT(nativeint renderbuffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedStringARB(nativeint namelen, nativeint* name, nativeint bufSize, nativeint* stringlen, nativeint* string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNamedStringivARB(nativeint namelen, nativeint* name, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetNextPerfQueryIdINTEL(nativeint queryId, nativeint* nextQueryId)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectBufferfvATI(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectBufferivATI(nativeint buffer, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectLabel(nativeint identifier, nativeint name, nativeint bufSize, nativeint* length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectLabelEXT(nativeint _type, nativeint _object, nativeint bufSize, nativeint* length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectLabelKHR(nativeint identifier, nativeint name, nativeint bufSize, nativeint* length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectParameterfvARB(nativeint obj, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectParameterivAPPLE(nativeint objectType, nativeint name, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectParameterivARB(nativeint obj, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectPtrLabel(nativeint ptr, nativeint bufSize, nativeint* length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetObjectPtrLabelKHR(nativeint ptr, nativeint bufSize, nativeint* length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetOcclusionQueryivNV(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetOcclusionQueryuivNV(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathColorGenfvNV(nativeint color, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathColorGenivNV(nativeint color, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathCommandsNV(nativeint path, nativeint* commands)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathCoordsNV(nativeint path, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathDashArrayNV(nativeint path, nativeint* dashArray)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetPathLengthNV(nativeint path, nativeint startSegment, nativeint numSegments)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathMetricRangeNV(nativeint metricQueryMask, nativeint firstPathName, nativeint numPaths, nativeint stride, nativeint* metrics)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathMetricsNV(nativeint metricQueryMask, nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint stride, nativeint* metrics)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathParameterfvNV(nativeint path, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathParameterivNV(nativeint path, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathSpacingNV(nativeint pathListMode, nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint advanceScale, nativeint kerningScale, nativeint transformType, nativeint* returnedSpacing)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathTexGenfvNV(nativeint texCoordSet, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPathTexGenivNV(nativeint texCoordSet, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfCounterInfoINTEL(nativeint queryId, nativeint counterId, nativeint counterNameLength, nativeint* counterName, nativeint counterDescLength, nativeint* counterDesc, nativeint* counterOffset, nativeint* counterDataSize, nativeint* counterTypeEnum, nativeint* counterDataTypeEnum, nativeint* rawCounterMaxValue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfMonitorCounterDataAMD(nativeint monitor, nativeint pname, nativeint dataSize, nativeint* data, nativeint* bytesWritten)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfMonitorCounterInfoAMD(nativeint group, nativeint counter, nativeint pname, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfMonitorCounterStringAMD(nativeint group, nativeint counter, nativeint bufSize, nativeint* length, nativeint* counterString)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfMonitorCountersAMD(nativeint group, nativeint* numCounters, nativeint* maxActiveCounters, nativeint counterSize, nativeint* counters)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfMonitorGroupStringAMD(nativeint group, nativeint bufSize, nativeint* length, nativeint* groupString)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfMonitorGroupsAMD(nativeint* numGroups, nativeint groupsSize, nativeint* groups)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfQueryDataINTEL(nativeint queryHandle, nativeint flags, nativeint dataSize, nativeint data, nativeint* bytesWritten)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfQueryIdByNameINTEL(nativeint* queryName, nativeint* queryId)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPerfQueryInfoINTEL(nativeint queryId, nativeint queryNameLength, nativeint* queryName, nativeint* dataSize, nativeint* noCounters, nativeint* noInstances, nativeint* capsMask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelMapfv(nativeint map, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelMapuiv(nativeint map, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelMapusv(nativeint map, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelMapxv(nativeint map, nativeint size, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelTexGenParameterfvSGIS(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelTexGenParameterivSGIS(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelTransformParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPixelTransformParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPointerIndexedvEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPointeri_vEXT(nativeint pname, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPointerv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPointervEXT(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPointervKHR(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetPolygonStipple(nativeint* mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramBinary(nativeint program, nativeint bufSize, nativeint* length, nativeint* binaryFormat, nativeint binary)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramBinaryOES(nativeint program, nativeint bufSize, nativeint* length, nativeint* binaryFormat, nativeint binary)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramEnvParameterIivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramEnvParameterIuivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramEnvParameterdvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramEnvParameterfvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramInfoLog(nativeint program, nativeint bufSize, nativeint* length, nativeint* infoLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramInterfaceiv(nativeint program, nativeint programInterface, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramLocalParameterIivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramLocalParameterIuivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramLocalParameterdvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramLocalParameterfvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramNamedParameterdvNV(nativeint id, nativeint len, nativeint* name, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramNamedParameterfvNV(nativeint id, nativeint len, nativeint* name, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramParameterdvNV(nativeint target, nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramParameterfvNV(nativeint target, nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramPipelineInfoLog(nativeint pipeline, nativeint bufSize, nativeint* length, nativeint* infoLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramPipelineInfoLogEXT(nativeint pipeline, nativeint bufSize, nativeint* length, nativeint* infoLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramPipelineiv(nativeint pipeline, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramPipelineivEXT(nativeint pipeline, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetProgramResourceIndex(nativeint program, nativeint programInterface, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetProgramResourceLocation(nativeint program, nativeint programInterface, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetProgramResourceLocationIndex(nativeint program, nativeint programInterface, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetProgramResourceLocationIndexEXT(nativeint program, nativeint programInterface, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramResourceName(nativeint program, nativeint programInterface, nativeint index, nativeint bufSize, nativeint* length, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramResourcefvNV(nativeint program, nativeint programInterface, nativeint index, nativeint propCount, nativeint* props, nativeint bufSize, nativeint* length, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramResourceiv(nativeint program, nativeint programInterface, nativeint index, nativeint propCount, nativeint* props, nativeint bufSize, nativeint* length, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramStageiv(nativeint program, nativeint shadertype, nativeint pname, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramStringARB(nativeint target, nativeint pname, nativeint string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramStringNV(nativeint id, nativeint pname, nativeint* program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramSubroutineParameteruivNV(nativeint target, nativeint index, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramiv(nativeint program, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramivARB(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetProgramivNV(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryBufferObjecti64v(nativeint id, nativeint buffer, nativeint pname, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryBufferObjectiv(nativeint id, nativeint buffer, nativeint pname, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryBufferObjectui64v(nativeint id, nativeint buffer, nativeint pname, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryBufferObjectuiv(nativeint id, nativeint buffer, nativeint pname, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryIndexediv(nativeint target, nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjecti64v(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjecti64vEXT(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectiv(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectivARB(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectivEXT(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectui64v(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectui64vEXT(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectuiv(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectuivARB(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryObjectuivEXT(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryiv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryivARB(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetQueryivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetRenderbufferParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetRenderbufferParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetRenderbufferParameterivOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterIiv(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterIivEXT(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterIivOES(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterIuiv(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterIuivEXT(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterIuivOES(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameterfv(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSamplerParameteriv(nativeint sampler, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSemaphoreParameterui64vEXT(nativeint semaphore, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSeparableFilter(nativeint target, nativeint format, nativeint _type, nativeint row, nativeint column, nativeint span)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSeparableFilterEXT(nativeint target, nativeint format, nativeint _type, nativeint row, nativeint column, nativeint span)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShaderInfoLog(nativeint shader, nativeint bufSize, nativeint* length, nativeint* infoLog)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShaderPrecisionFormat(nativeint shadertype, nativeint precisiontype, nativeint* range, nativeint* precision)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShaderSource(nativeint shader, nativeint bufSize, nativeint* length, nativeint* source)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShaderSourceARB(nativeint obj, nativeint maxLength, nativeint* length, nativeint* source)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShaderiv(nativeint shader, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShadingRateImagePaletteNV(nativeint viewport, nativeint entry, nativeint* rate)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetShadingRateSampleLocationivNV(nativeint rate, nativeint samples, nativeint index, nativeint* location)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSharpenTexFuncSGIS(nativeint target, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetStageIndexNV(nativeint shadertype)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint* glGetString(nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint* glGetStringi(nativeint name, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetSubroutineIndex(nativeint program, nativeint shadertype, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetSubroutineUniformLocation(nativeint program, nativeint shadertype, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSynciv(nativeint sync, nativeint pname, nativeint bufSize, nativeint* length, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetSyncivAPPLE(nativeint sync, nativeint pname, nativeint bufSize, nativeint* length, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexBumpParameterfvATI(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexBumpParameterivATI(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexEnvfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexEnviv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexEnvxv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexEnvxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexFilterFuncSGIS(nativeint target, nativeint filter, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexGendv(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexGenfv(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexGenfvOES(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexGeniv(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexGenivOES(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexGenxvOES(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexImage(nativeint target, nativeint level, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexLevelParameterfv(nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexLevelParameteriv(nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexLevelParameterxvOES(nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterIiv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterIivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterIivOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterIuiv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterIuivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterIuivOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterPointervAPPLE(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterxv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTexParameterxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetTextureHandleARB(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetTextureHandleIMG(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetTextureHandleNV(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureImage(nativeint texture, nativeint level, nativeint format, nativeint _type, nativeint bufSize, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureImageEXT(nativeint texture, nativeint target, nativeint level, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureLevelParameterfv(nativeint texture, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureLevelParameterfvEXT(nativeint texture, nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureLevelParameteriv(nativeint texture, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureLevelParameterivEXT(nativeint texture, nativeint target, nativeint level, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterIiv(nativeint texture, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterIivEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterIuiv(nativeint texture, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterIuivEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterfv(nativeint texture, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterfvEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameteriv(nativeint texture, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureParameterivEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetTextureSamplerHandleARB(nativeint texture, nativeint sampler)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetTextureSamplerHandleIMG(nativeint texture, nativeint sampler)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetTextureSamplerHandleNV(nativeint texture, nativeint sampler)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTextureSubImage(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint bufSize, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTrackMatrixivNV(nativeint target, nativeint address, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTransformFeedbackVarying(nativeint program, nativeint index, nativeint bufSize, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTransformFeedbackVaryingEXT(nativeint program, nativeint index, nativeint bufSize, nativeint* length, nativeint* size, nativeint* _type, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTransformFeedbackVaryingNV(nativeint program, nativeint index, nativeint* location)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTransformFeedbacki64_v(nativeint xfb, nativeint pname, nativeint index, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTransformFeedbacki_v(nativeint xfb, nativeint pname, nativeint index, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTransformFeedbackiv(nativeint xfb, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetTranslatedShaderSourceANGLE(nativeint shader, nativeint bufsize, nativeint* length, nativeint* source)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetUniformBlockIndex(nativeint program, nativeint* uniformBlockName)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetUniformBufferSizeEXT(nativeint program, nativeint location)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformIndices(nativeint program, nativeint uniformCount, nativeint* uniformNames, nativeint* uniformIndices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetUniformLocation(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetUniformLocationARB(nativeint programObj, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetUniformOffsetEXT(nativeint program, nativeint location)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformSubroutineuiv(nativeint shadertype, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformdv(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformfv(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformfvARB(nativeint programObj, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformi64vARB(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformi64vNV(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformiv(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformivARB(nativeint programObj, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformui64vARB(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformui64vNV(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformuiv(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUniformuivEXT(nativeint program, nativeint location, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUnsignedBytevEXT(nativeint pname, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetUnsignedBytei_vEXT(nativeint target, nativeint index, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVariantArrayObjectfvATI(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVariantArrayObjectivATI(nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVariantBooleanvEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVariantFloatvEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVariantIntegervEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVariantPointervEXT(nativeint id, nativeint value, nativeint* data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetVaryingLocationNV(nativeint program, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayIndexed64iv(nativeint vaobj, nativeint index, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayIndexediv(nativeint vaobj, nativeint index, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayIntegeri_vEXT(nativeint vaobj, nativeint index, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayIntegervEXT(nativeint vaobj, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayPointeri_vEXT(nativeint vaobj, nativeint index, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayPointervEXT(nativeint vaobj, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexArrayiv(nativeint vaobj, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribArrayObjectfvATI(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribArrayObjectivATI(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribIiv(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribIivEXT(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribIuiv(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribIuivEXT(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribLdv(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribLdvEXT(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribLi64vNV(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribLui64vARB(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribLui64vNV(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribPointerv(nativeint index, nativeint pname, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribPointervARB(nativeint index, nativeint pname, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribPointervNV(nativeint index, nativeint pname, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribdv(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribdvARB(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribdvNV(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribfv(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribfvARB(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribfvNV(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribiv(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribivARB(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVertexAttribivNV(nativeint index, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoCaptureStreamdvNV(nativeint video_capture_slot, nativeint stream, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoCaptureStreamfvNV(nativeint video_capture_slot, nativeint stream, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoCaptureStreamivNV(nativeint video_capture_slot, nativeint stream, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoCaptureivNV(nativeint video_capture_slot, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoi64vNV(nativeint video_slot, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoivNV(nativeint video_slot, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideoui64vNV(nativeint video_slot, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetVideouivNV(nativeint video_slot, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnColorTable(nativeint target, nativeint format, nativeint _type, nativeint bufSize, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnColorTableARB(nativeint target, nativeint format, nativeint _type, nativeint bufSize, nativeint table)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnCompressedTexImage(nativeint target, nativeint lod, nativeint bufSize, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnCompressedTexImageARB(nativeint target, nativeint lod, nativeint bufSize, nativeint img)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnConvolutionFilter(nativeint target, nativeint format, nativeint _type, nativeint bufSize, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnConvolutionFilterARB(nativeint target, nativeint format, nativeint _type, nativeint bufSize, nativeint image)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnHistogram(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint bufSize, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnHistogramARB(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint bufSize, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMapdv(nativeint target, nativeint query, nativeint bufSize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMapdvARB(nativeint target, nativeint query, nativeint bufSize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMapfv(nativeint target, nativeint query, nativeint bufSize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMapfvARB(nativeint target, nativeint query, nativeint bufSize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMapiv(nativeint target, nativeint query, nativeint bufSize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMapivARB(nativeint target, nativeint query, nativeint bufSize, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMinmax(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint bufSize, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnMinmaxARB(nativeint target, nativeint reset, nativeint format, nativeint _type, nativeint bufSize, nativeint values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPixelMapfv(nativeint map, nativeint bufSize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPixelMapfvARB(nativeint map, nativeint bufSize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPixelMapuiv(nativeint map, nativeint bufSize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPixelMapuivARB(nativeint map, nativeint bufSize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPixelMapusv(nativeint map, nativeint bufSize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPixelMapusvARB(nativeint map, nativeint bufSize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPolygonStipple(nativeint bufSize, nativeint* pattern)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnPolygonStippleARB(nativeint bufSize, nativeint* pattern)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnSeparableFilter(nativeint target, nativeint format, nativeint _type, nativeint rowBufSize, nativeint row, nativeint columnBufSize, nativeint column, nativeint span)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnSeparableFilterARB(nativeint target, nativeint format, nativeint _type, nativeint rowBufSize, nativeint row, nativeint columnBufSize, nativeint column, nativeint span)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnTexImage(nativeint target, nativeint level, nativeint format, nativeint _type, nativeint bufSize, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnTexImageARB(nativeint target, nativeint level, nativeint format, nativeint _type, nativeint bufSize, nativeint img)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformdv(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformdvARB(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformfv(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformfvARB(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformfvEXT(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformfvKHR(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformi64vARB(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformiv(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformivARB(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformivEXT(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformivKHR(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformui64vARB(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformuiv(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformuivARB(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGetnUniformuivKHR(nativeint program, nativeint location, nativeint bufSize, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactorbSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactordSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactorfSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactoriSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactorsSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactorubSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactoruiSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glGlobalAlphaFactorusSUN(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glHint(nativeint target, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glHintPGI(nativeint target, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glHistogram(nativeint target, nativeint width, nativeint internalformat, nativeint sink)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glHistogramEXT(nativeint target, nativeint width, nativeint internalformat, nativeint sink)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIglooInterfaceSGIX(nativeint pname, nativeint _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImageTransformParameterfHP(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImageTransformParameterfvHP(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImageTransformParameteriHP(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImageTransformParameterivHP(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImportMemoryFdEXT(nativeint memory, nativeint size, nativeint handleType, nativeint fd)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImportMemoryWin32HandleEXT(nativeint memory, nativeint size, nativeint handleType, nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImportMemoryWin32NameEXT(nativeint memory, nativeint size, nativeint handleType, nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImportSemaphoreFdEXT(nativeint semaphore, nativeint handleType, nativeint fd)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImportSemaphoreWin32HandleEXT(nativeint semaphore, nativeint handleType, nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glImportSemaphoreWin32NameEXT(nativeint semaphore, nativeint handleType, nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glImportSyncEXT(nativeint external_sync_type, nativeint external_sync, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexFormatNV(nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexFuncEXT(nativeint func, nativeint ref)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexMask(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexMaterialEXT(nativeint face, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexPointer(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexPointerEXT(nativeint _type, nativeint stride, nativeint count, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexPointerListIBM(nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexd(nativeint c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexdv(nativeint* c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexf(nativeint c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexfv(nativeint* c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexi(nativeint c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexiv(nativeint* c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexs(nativeint c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexsv(nativeint* c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexub(nativeint c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexubv(nativeint* c)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexxOES(nativeint component)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glIndexxvOES(nativeint* component)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInitNames()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInsertComponentEXT(nativeint res, nativeint src, nativeint num)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInsertEventMarkerEXT(nativeint length, nativeint* marker)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInstrumentsBufferSGIX(nativeint size, nativeint* buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInterleavedArrays(nativeint format, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInterpolatePathsNV(nativeint resultPath, nativeint pathA, nativeint pathB, nativeint weight)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateBufferData(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateBufferSubData(nativeint buffer, nativeint offset, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateFramebuffer(nativeint target, nativeint numAttachments, nativeint* attachments)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateNamedFramebufferData(nativeint framebuffer, nativeint numAttachments, nativeint* attachments)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateNamedFramebufferSubData(nativeint framebuffer, nativeint numAttachments, nativeint* attachments, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateSubFramebuffer(nativeint target, nativeint numAttachments, nativeint* attachments, nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateTexImage(nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glInvalidateTexSubImage(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsAsyncMarkerSGIX(nativeint marker)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsBuffer(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsBufferARB(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsBufferResidentNV(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsCommandListNV(nativeint list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsEnabled(nativeint cap)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsEnabledIndexedEXT(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsEnabledi(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsEnablediEXT(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsEnablediNV(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsEnablediOES(nativeint target, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsFenceAPPLE(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsFenceNV(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsFramebuffer(nativeint framebuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsFramebufferEXT(nativeint framebuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsFramebufferOES(nativeint framebuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsImageHandleResidentARB(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsImageHandleResidentNV(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsList(nativeint list)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsMemoryObjectEXT(nativeint memoryObject)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsNameAMD(nativeint identifier, nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsNamedBufferResidentNV(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsNamedStringARB(nativeint namelen, nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsObjectBufferATI(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsOcclusionQueryNV(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsPathNV(nativeint path)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsPointInFillPathNV(nativeint path, nativeint mask, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsPointInStrokePathNV(nativeint path, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsProgram(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsProgramARB(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsProgramNV(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsProgramPipeline(nativeint pipeline)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsProgramPipelineEXT(nativeint pipeline)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsQuery(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsQueryARB(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsQueryEXT(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsRenderbuffer(nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsRenderbufferEXT(nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsRenderbufferOES(nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsSemaphoreEXT(nativeint semaphore)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsSampler(nativeint sampler)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsShader(nativeint shader)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsStateNV(nativeint state)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsSync(nativeint sync)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsSyncAPPLE(nativeint sync)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsTexture(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsTextureEXT(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsTextureHandleResidentARB(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsTextureHandleResidentNV(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsTransformFeedback(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsTransformFeedbackNV(nativeint id)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsVariantEnabledEXT(nativeint id, nativeint cap)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsVertexArray(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsVertexArrayAPPLE(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsVertexArrayOES(nativeint array)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glIsVertexAttribEnabledAPPLE(nativeint index, nativeint pname)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLGPUCopyImageSubDataNVX(nativeint sourceGpu, nativeint destinationGpuMask, nativeint srcName, nativeint srcTarget, nativeint srcLevel, nativeint srcX, nativeint srxY, nativeint srcZ, nativeint dstName, nativeint dstTarget, nativeint dstLevel, nativeint dstX, nativeint dstY, nativeint dstZ, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLGPUInterlockNVX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLGPUNamedBufferSubDataNVX(nativeint gpuMask, nativeint buffer, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLabelObjectEXT(nativeint _type, nativeint _object, nativeint length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightEnviSGIX(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModelf(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModelfv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModeli(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModeliv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModelx(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModelxOES(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModelxv(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightModelxvOES(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightf(nativeint light, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightfv(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLighti(nativeint light, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightiv(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightx(nativeint light, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightxOES(nativeint light, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightxv(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLightxvOES(nativeint light, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLineStipple(nativeint factor, nativeint pattern)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLineWidth(nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLineWidthx(nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLineWidthxOES(nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLinkProgram(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLinkProgramARB(nativeint programObj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glListBase(nativeint _base)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glListDrawCommandsStatesClientNV(nativeint list, nativeint segment, nativeint* indirects, nativeint* sizes, nativeint* states, nativeint* fbos, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glListParameterfSGIX(nativeint list, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glListParameterfvSGIX(nativeint list, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glListParameteriSGIX(nativeint list, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glListParameterivSGIX(nativeint list, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadIdentity()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadIdentityDeformationMapSGIX(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadMatrixd(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadMatrixf(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadMatrixx(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadMatrixxOES(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadName(nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadPaletteFromModelViewMatrixOES()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadProgramNV(nativeint target, nativeint id, nativeint len, nativeint* program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadTransposeMatrixd(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadTransposeMatrixdARB(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadTransposeMatrixf(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadTransposeMatrixfARB(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLoadTransposeMatrixxOES(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLockArraysEXT(nativeint first, nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glLogicOp(nativeint opcode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeBufferNonResidentNV(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeBufferResidentNV(nativeint target, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeImageHandleNonResidentARB(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeImageHandleNonResidentNV(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeImageHandleResidentARB(nativeint handle, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeImageHandleResidentNV(nativeint handle, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeNamedBufferNonResidentNV(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeNamedBufferResidentNV(nativeint buffer, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeTextureHandleNonResidentARB(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeTextureHandleNonResidentNV(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeTextureHandleResidentARB(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMakeTextureHandleResidentNV(nativeint handle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMap1d(nativeint target, nativeint u1, nativeint u2, nativeint stride, nativeint order, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMap1f(nativeint target, nativeint u1, nativeint u2, nativeint stride, nativeint order, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMap1xOES(nativeint target, nativeint u1, nativeint u2, nativeint stride, nativeint order, nativeint points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMap2d(nativeint target, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMap2f(nativeint target, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMap2xOES(nativeint target, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapBuffer(nativeint target, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapBufferARB(nativeint target, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapBufferOES(nativeint target, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapBufferRange(nativeint target, nativeint offset, nativeint length, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapBufferRangeEXT(nativeint target, nativeint offset, nativeint length, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapControlPointsNV(nativeint target, nativeint index, nativeint _type, nativeint ustride, nativeint vstride, nativeint uorder, nativeint vorder, nativeint packed, nativeint points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapGrid1d(nativeint un, nativeint u1, nativeint u2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapGrid1f(nativeint un, nativeint u1, nativeint u2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapGrid1xOES(nativeint n, nativeint u1, nativeint u2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapGrid2d(nativeint un, nativeint u1, nativeint u2, nativeint vn, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapGrid2f(nativeint un, nativeint u1, nativeint u2, nativeint vn, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapGrid2xOES(nativeint n, nativeint u1, nativeint u2, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapNamedBuffer(nativeint buffer, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapNamedBufferEXT(nativeint buffer, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapNamedBufferRange(nativeint buffer, nativeint offset, nativeint length, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapNamedBufferRangeEXT(nativeint buffer, nativeint offset, nativeint length, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapObjectBufferATI(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapParameterfvNV(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapParameterivNV(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glMapTexture2DINTEL(nativeint texture, nativeint level, nativeint access, nativeint* stride, nativeint* layout)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapVertexAttrib1dAPPLE(nativeint index, nativeint size, nativeint u1, nativeint u2, nativeint stride, nativeint order, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapVertexAttrib1fAPPLE(nativeint index, nativeint size, nativeint u1, nativeint u2, nativeint stride, nativeint order, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapVertexAttrib2dAPPLE(nativeint index, nativeint size, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMapVertexAttrib2fAPPLE(nativeint index, nativeint size, nativeint u1, nativeint u2, nativeint ustride, nativeint uorder, nativeint v1, nativeint v2, nativeint vstride, nativeint vorder, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialf(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialfv(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMateriali(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialiv(nativeint face, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialx(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialxOES(nativeint face, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialxv(nativeint face, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaterialxvOES(nativeint face, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixFrustumEXT(nativeint mode, nativeint left, nativeint right, nativeint bottom, nativeint top, nativeint zNear, nativeint zFar)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixIndexPointerARB(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixIndexPointerOES(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixIndexubvARB(nativeint size, nativeint* indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixIndexuivARB(nativeint size, nativeint* indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixIndexusvARB(nativeint size, nativeint* indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoad3x2fNV(nativeint matrixMode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoad3x3fNV(nativeint matrixMode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoadIdentityEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoadTranspose3x3fNV(nativeint matrixMode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoadTransposedEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoadTransposefEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoaddEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixLoadfEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMode(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMult3x2fNV(nativeint matrixMode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMult3x3fNV(nativeint matrixMode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMultTranspose3x3fNV(nativeint matrixMode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMultTransposedEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMultTransposefEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMultdEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixMultfEXT(nativeint mode, nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixOrthoEXT(nativeint mode, nativeint left, nativeint right, nativeint bottom, nativeint top, nativeint zNear, nativeint zFar)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixPopEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixPushEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixRotatedEXT(nativeint mode, nativeint angle, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixRotatefEXT(nativeint mode, nativeint angle, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixScaledEXT(nativeint mode, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixScalefEXT(nativeint mode, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixTranslatedEXT(nativeint mode, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMatrixTranslatefEXT(nativeint mode, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaxShaderCompilerThreadsKHR(nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMaxShaderCompilerThreadsARB(nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMemoryBarrier(nativeint barriers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMemoryBarrierByRegion(nativeint barriers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMemoryBarrierEXT(nativeint barriers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMemoryObjectParameterivEXT(nativeint memoryObject, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMinSampleShading(nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMinSampleShadingARB(nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMinSampleShadingOES(nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMinmax(nativeint target, nativeint internalformat, nativeint sink)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMinmaxEXT(nativeint target, nativeint internalformat, nativeint sink)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultMatrixd(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultMatrixf(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultMatrixx(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultMatrixxOES(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultTransposeMatrixd(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultTransposeMatrixdARB(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultTransposeMatrixf(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultTransposeMatrixfARB(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultTransposeMatrixxOES(nativeint* m)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArrays(nativeint mode, nativeint* first, nativeint* count, nativeint drawcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysEXT(nativeint mode, nativeint* first, nativeint* count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirect(nativeint mode, nativeint indirect, nativeint drawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirectAMD(nativeint mode, nativeint indirect, nativeint primcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirectBindlessCountNV(nativeint mode, nativeint indirect, nativeint drawCount, nativeint maxDrawCount, nativeint stride, nativeint vertexBufferCount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirectBindlessNV(nativeint mode, nativeint indirect, nativeint drawCount, nativeint stride, nativeint vertexBufferCount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirectCount(nativeint mode, nativeint indirect, nativeint drawcount, nativeint maxdrawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirectCountARB(nativeint mode, nativeint indirect, nativeint drawcount, nativeint maxdrawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawArraysIndirectEXT(nativeint mode, nativeint indirect, nativeint drawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementArrayAPPLE(nativeint mode, nativeint* first, nativeint* count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElements(nativeint mode, nativeint* count, nativeint _type, nativeint* indices, nativeint drawcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsBaseVertex(nativeint mode, nativeint* count, nativeint _type, nativeint* indices, nativeint drawcount, nativeint* basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsBaseVertexEXT(nativeint mode, nativeint* count, nativeint _type, nativeint* indices, nativeint primcount, nativeint* basevertex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsEXT(nativeint mode, nativeint* count, nativeint _type, nativeint* indices, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirect(nativeint mode, nativeint _type, nativeint indirect, nativeint drawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirectAMD(nativeint mode, nativeint _type, nativeint indirect, nativeint primcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirectBindlessCountNV(nativeint mode, nativeint _type, nativeint indirect, nativeint drawCount, nativeint maxDrawCount, nativeint stride, nativeint vertexBufferCount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirectBindlessNV(nativeint mode, nativeint _type, nativeint indirect, nativeint drawCount, nativeint stride, nativeint vertexBufferCount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirectCount(nativeint mode, nativeint _type, nativeint indirect, nativeint drawcount, nativeint maxdrawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirectCountARB(nativeint mode, nativeint _type, nativeint indirect, nativeint drawcount, nativeint maxdrawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawElementsIndirectEXT(nativeint mode, nativeint _type, nativeint indirect, nativeint drawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawMeshTasksIndirectNV(nativeint indirect, nativeint drawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawMeshTasksIndirectCountNV(nativeint indirect, nativeint drawcount, nativeint maxdrawcount, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiDrawRangeElementArrayAPPLE(nativeint mode, nativeint start, nativeint _end, nativeint* first, nativeint* count, nativeint primcount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiModeDrawArraysIBM(nativeint* mode, nativeint* first, nativeint* count, nativeint primcount, nativeint modestride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiModeDrawElementsIBM(nativeint* mode, nativeint* count, nativeint _type, nativeint* indices, nativeint primcount, nativeint modestride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexBufferEXT(nativeint texunit, nativeint target, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1bOES(nativeint texture, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1bvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1d(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1dARB(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1dv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1dvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1f(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1fARB(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1fv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1fvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1hNV(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1hvNV(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1i(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1iARB(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1iv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1ivARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1s(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1sARB(nativeint target, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1sv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1svARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1xOES(nativeint texture, nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord1xvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2bOES(nativeint texture, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2bvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2d(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2dARB(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2dv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2dvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2f(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2fARB(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2fv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2fvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2hNV(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2hvNV(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2i(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2iARB(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2iv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2ivARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2s(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2sARB(nativeint target, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2sv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2svARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2xOES(nativeint texture, nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord2xvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3bOES(nativeint texture, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3bvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3d(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3dARB(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3dv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3dvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3f(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3fARB(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3fv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3fvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3hNV(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3hvNV(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3i(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3iARB(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3iv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3ivARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3s(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3sARB(nativeint target, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3sv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3svARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3xOES(nativeint texture, nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord3xvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4bOES(nativeint texture, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4bvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4d(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4dARB(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4dv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4dvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4f(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4fARB(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4fv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4fvARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4hNV(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4hvNV(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4i(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4iARB(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4iv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4ivARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4s(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4sARB(nativeint target, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4sv(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4svARB(nativeint target, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4x(nativeint texture, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4xOES(nativeint texture, nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoord4xvOES(nativeint texture, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP1ui(nativeint texture, nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP1uiv(nativeint texture, nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP2ui(nativeint texture, nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP2uiv(nativeint texture, nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP3ui(nativeint texture, nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP3uiv(nativeint texture, nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP4ui(nativeint texture, nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordP4uiv(nativeint texture, nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexCoordPointerEXT(nativeint texunit, nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexEnvfEXT(nativeint texunit, nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexEnvfvEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexEnviEXT(nativeint texunit, nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexEnvivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexGendEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexGendvEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexGenfEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexGenfvEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexGeniEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexGenivEXT(nativeint texunit, nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexImage1DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexImage2DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexImage3DEXT(nativeint texunit, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexParameterIivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexParameterIuivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexParameterfEXT(nativeint texunit, nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexParameterfvEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexParameteriEXT(nativeint texunit, nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexParameterivEXT(nativeint texunit, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexRenderbufferEXT(nativeint texunit, nativeint target, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexSubImage1DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexSubImage2DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMultiTexSubImage3DEXT(nativeint texunit, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastBarrierNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastBlitFramebufferNV(nativeint srcGpu, nativeint dstGpu, nativeint srcX0, nativeint srcY0, nativeint srcX1, nativeint srcY1, nativeint dstX0, nativeint dstY0, nativeint dstX1, nativeint dstY1, nativeint mask, nativeint filter)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastBufferSubDataNV(nativeint gpuMask, nativeint buffer, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastCopyBufferSubDataNV(nativeint readGpu, nativeint writeGpuMask, nativeint readBuffer, nativeint writeBuffer, nativeint readOffset, nativeint writeOffset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastCopyImageSubDataNV(nativeint srcGpu, nativeint dstGpuMask, nativeint srcName, nativeint srcTarget, nativeint srcLevel, nativeint srcX, nativeint srcY, nativeint srcZ, nativeint dstName, nativeint dstTarget, nativeint dstLevel, nativeint dstX, nativeint dstY, nativeint dstZ, nativeint srcWidth, nativeint srcHeight, nativeint srcDepth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastFramebufferSampleLocationsfvNV(nativeint gpu, nativeint framebuffer, nativeint start, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastGetQueryObjecti64vNV(nativeint gpu, nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastGetQueryObjectivNV(nativeint gpu, nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastGetQueryObjectui64vNV(nativeint gpu, nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastGetQueryObjectuivNV(nativeint gpu, nativeint id, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glMulticastWaitSyncNV(nativeint signalGpu, nativeint waitGpuMask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferAttachMemoryNV(nativeint buffer, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferData(nativeint buffer, nativeint size, nativeint data, nativeint usage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferDataEXT(nativeint buffer, nativeint size, nativeint data, nativeint usage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferPageCommitmentARB(nativeint buffer, nativeint offset, nativeint size, nativeint commit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferPageCommitmentEXT(nativeint buffer, nativeint offset, nativeint size, nativeint commit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferStorage(nativeint buffer, nativeint size, nativeint data, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferStorageExternalEXT(nativeint buffer, nativeint offset, nativeint size, nativeint clientBuffer, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferStorageEXT(nativeint buffer, nativeint size, nativeint data, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferStorageMemEXT(nativeint buffer, nativeint size, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferSubData(nativeint buffer, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedBufferSubDataEXT(nativeint buffer, nativeint offset, nativeint size, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedCopyBufferSubDataEXT(nativeint readBuffer, nativeint writeBuffer, nativeint readOffset, nativeint writeOffset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferDrawBuffer(nativeint framebuffer, nativeint buf)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferDrawBuffers(nativeint framebuffer, nativeint n, nativeint* bufs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferParameteri(nativeint framebuffer, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferParameteriEXT(nativeint framebuffer, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferReadBuffer(nativeint framebuffer, nativeint src)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferRenderbuffer(nativeint framebuffer, nativeint attachment, nativeint renderbuffertarget, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferRenderbufferEXT(nativeint framebuffer, nativeint attachment, nativeint renderbuffertarget, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferSampleLocationsfvARB(nativeint framebuffer, nativeint start, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferSampleLocationsfvNV(nativeint framebuffer, nativeint start, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTexture(nativeint framebuffer, nativeint attachment, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferSamplePositionsfvAMD(nativeint framebuffer, nativeint numsamples, nativeint pixelindex, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTexture1DEXT(nativeint framebuffer, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTexture2DEXT(nativeint framebuffer, nativeint attachment, nativeint textarget, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTexture3DEXT(nativeint framebuffer, nativeint attachment, nativeint textarget, nativeint texture, nativeint level, nativeint zoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTextureEXT(nativeint framebuffer, nativeint attachment, nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTextureFaceEXT(nativeint framebuffer, nativeint attachment, nativeint texture, nativeint level, nativeint face)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTextureLayer(nativeint framebuffer, nativeint attachment, nativeint texture, nativeint level, nativeint layer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedFramebufferTextureLayerEXT(nativeint framebuffer, nativeint attachment, nativeint texture, nativeint level, nativeint layer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameter4dEXT(nativeint program, nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameter4dvEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameter4fEXT(nativeint program, nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameter4fvEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameterI4iEXT(nativeint program, nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameterI4ivEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameterI4uiEXT(nativeint program, nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameterI4uivEXT(nativeint program, nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParameters4fvEXT(nativeint program, nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParametersI4ivEXT(nativeint program, nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramLocalParametersI4uivEXT(nativeint program, nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedProgramStringEXT(nativeint program, nativeint target, nativeint format, nativeint len, nativeint string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedRenderbufferStorage(nativeint renderbuffer, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedRenderbufferStorageEXT(nativeint renderbuffer, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedRenderbufferStorageMultisample(nativeint renderbuffer, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedRenderbufferStorageMultisampleAdvancedAMD(nativeint renderbuffer, nativeint samples, nativeint storageSamples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedRenderbufferStorageMultisampleCoverageEXT(nativeint renderbuffer, nativeint coverageSamples, nativeint colorSamples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedRenderbufferStorageMultisampleEXT(nativeint renderbuffer, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNamedStringARB(nativeint _type, nativeint namelen, nativeint* name, nativeint stringlen, nativeint* string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNewList(nativeint list, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glNewObjectBufferATI(nativeint size, nativeint pointer, nativeint usage)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3b(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3bv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3d(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3f(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3fVertex3fSUN(nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3fVertex3fvSUN(nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3hNV(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3i(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3s(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3x(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3xOES(nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormal3xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalFormatNV(nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalP3ui(nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalP3uiv(nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalPointer(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalPointerEXT(nativeint _type, nativeint stride, nativeint count, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalPointerListIBM(nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalPointervINTEL(nativeint _type, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3bATI(nativeint stream, nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3bvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3dATI(nativeint stream, nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3dvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3fATI(nativeint stream, nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3fvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3iATI(nativeint stream, nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3ivATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3sATI(nativeint stream, nativeint nx, nativeint ny, nativeint nz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glNormalStream3svATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glObjectLabel(nativeint identifier, nativeint name, nativeint length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glObjectLabelKHR(nativeint identifier, nativeint name, nativeint length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glObjectPtrLabel(nativeint ptr, nativeint length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glObjectPtrLabelKHR(nativeint ptr, nativeint length, nativeint* label)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glObjectPurgeableAPPLE(nativeint objectType, nativeint name, nativeint option)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glObjectUnpurgeableAPPLE(nativeint objectType, nativeint name, nativeint option)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glOrtho(nativeint left, nativeint right, nativeint bottom, nativeint top, nativeint zNear, nativeint zFar)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glOrthof(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glOrthofOES(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glOrthox(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glOrthoxOES(nativeint l, nativeint r, nativeint b, nativeint t, nativeint n, nativeint f)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPNTrianglesfATI(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPNTrianglesiATI(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPassTexCoordATI(nativeint dst, nativeint coord, nativeint swizzle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPassThrough(nativeint token)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPassThroughxOES(nativeint token)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPatchParameterfv(nativeint pname, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPatchParameteri(nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPatchParameteriEXT(nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPatchParameteriOES(nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathColorGenNV(nativeint color, nativeint genMode, nativeint colorFormat, nativeint* coeffs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathCommandsNV(nativeint path, nativeint numCommands, nativeint* commands, nativeint numCoords, nativeint coordType, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathCoordsNV(nativeint path, nativeint numCoords, nativeint coordType, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathCoverDepthFuncNV(nativeint func)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathDashArrayNV(nativeint path, nativeint dashCount, nativeint* dashArray)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathFogGenNV(nativeint genMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glPathGlyphIndexArrayNV(nativeint firstPathName, nativeint fontTarget, nativeint fontName, nativeint fontStyle, nativeint firstGlyphIndex, nativeint numGlyphs, nativeint pathParameterTemplate, nativeint emScale)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glPathGlyphIndexRangeNV(nativeint fontTarget, nativeint fontName, nativeint fontStyle, nativeint pathParameterTemplate, nativeint emScale, nativeint_2 baseAndCount)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathGlyphRangeNV(nativeint firstPathName, nativeint fontTarget, nativeint fontName, nativeint fontStyle, nativeint firstGlyph, nativeint numGlyphs, nativeint handleMissingGlyphs, nativeint pathParameterTemplate, nativeint emScale)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathGlyphsNV(nativeint firstPathName, nativeint fontTarget, nativeint fontName, nativeint fontStyle, nativeint numGlyphs, nativeint _type, nativeint charcodes, nativeint handleMissingGlyphs, nativeint pathParameterTemplate, nativeint emScale)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glPathMemoryGlyphIndexArrayNV(nativeint firstPathName, nativeint fontTarget, nativeint fontSize, nativeint fontData, nativeint faceIndex, nativeint firstGlyphIndex, nativeint numGlyphs, nativeint pathParameterTemplate, nativeint emScale)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathParameterfNV(nativeint path, nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathParameterfvNV(nativeint path, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathParameteriNV(nativeint path, nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathParameterivNV(nativeint path, nativeint pname, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathStencilDepthOffsetNV(nativeint factor, nativeint units)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathStencilFuncNV(nativeint func, nativeint ref, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathStringNV(nativeint path, nativeint format, nativeint length, nativeint pathString)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathSubCommandsNV(nativeint path, nativeint commandStart, nativeint commandsToDelete, nativeint numCommands, nativeint* commands, nativeint numCoords, nativeint coordType, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathSubCoordsNV(nativeint path, nativeint coordStart, nativeint numCoords, nativeint coordType, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPathTexGenNV(nativeint texCoordSet, nativeint genMode, nativeint components, nativeint* coeffs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPauseTransformFeedback()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPauseTransformFeedbackNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelDataRangeNV(nativeint target, nativeint length, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelMapfv(nativeint map, nativeint mapsize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelMapuiv(nativeint map, nativeint mapsize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelMapusv(nativeint map, nativeint mapsize, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelMapx(nativeint map, nativeint size, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelStoref(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelStorei(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelStorex(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTexGenParameterfSGIS(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTexGenParameterfvSGIS(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTexGenParameteriSGIS(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTexGenParameterivSGIS(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTexGenSGIX(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransferf(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransferi(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransferxOES(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransformParameterfEXT(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransformParameterfvEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransformParameteriEXT(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelTransformParameterivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelZoom(nativeint xfactor, nativeint yfactor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPixelZoomxOES(nativeint xfactor, nativeint yfactor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glPointAlongPathNV(nativeint path, nativeint startSegment, nativeint numSegments, nativeint distance, nativeint* x, nativeint* y, nativeint* tangentX, nativeint* tangentY)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterf(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfARB(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfEXT(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfSGIS(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfvARB(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfvEXT(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterfvSGIS(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameteri(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameteriNV(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameteriv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterivNV(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterx(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterxOES(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterxv(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointParameterxvOES(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointSize(nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointSizePointerOES(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointSizex(nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPointSizexOES(nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glPollAsyncSGIX(nativeint* markerp)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glPollInstrumentsSGIX(nativeint* marker_p)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonMode(nativeint face, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonModeNV(nativeint face, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonOffset(nativeint factor, nativeint units)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonOffsetClamp(nativeint factor, nativeint units, nativeint clamp)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonOffsetClampEXT(nativeint factor, nativeint units, nativeint clamp)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonOffsetEXT(nativeint factor, nativeint bias)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonOffsetx(nativeint factor, nativeint units)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonOffsetxOES(nativeint factor, nativeint units)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPolygonStipple(nativeint* mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopAttrib()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopClientAttrib()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopDebugGroup()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopDebugGroupKHR()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopGroupMarkerEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopMatrix()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPopName()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPresentFrameDualFillNV(nativeint video_slot, nativeint minPresentTime, nativeint beginPresentTimeId, nativeint presentDurationId, nativeint _type, nativeint target0, nativeint fill0, nativeint target1, nativeint fill1, nativeint target2, nativeint fill2, nativeint target3, nativeint fill3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPresentFrameKeyedNV(nativeint video_slot, nativeint minPresentTime, nativeint beginPresentTimeId, nativeint presentDurationId, nativeint _type, nativeint target0, nativeint fill0, nativeint key0, nativeint target1, nativeint fill1, nativeint key1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveBoundingBox(nativeint minX, nativeint minY, nativeint minZ, nativeint minW, nativeint maxX, nativeint maxY, nativeint maxZ, nativeint maxW)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveBoundingBoxARB(nativeint minX, nativeint minY, nativeint minZ, nativeint minW, nativeint maxX, nativeint maxY, nativeint maxZ, nativeint maxW)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveBoundingBoxEXT(nativeint minX, nativeint minY, nativeint minZ, nativeint minW, nativeint maxX, nativeint maxY, nativeint maxZ, nativeint maxW)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveBoundingBoxOES(nativeint minX, nativeint minY, nativeint minZ, nativeint minW, nativeint maxX, nativeint maxY, nativeint maxZ, nativeint maxW)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveRestartIndex(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveRestartIndexNV(nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrimitiveRestartNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrioritizeTextures(nativeint n, nativeint* textures, nativeint* priorities)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrioritizeTexturesEXT(nativeint n, nativeint* textures, nativeint* priorities)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPrioritizeTexturesxOES(nativeint n, nativeint* textures, nativeint* priorities)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramBinary(nativeint program, nativeint binaryFormat, nativeint binary, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramBinaryOES(nativeint program, nativeint binaryFormat, nativeint binary, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramBufferParametersIivNV(nativeint target, nativeint bindingIndex, nativeint wordIndex, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramBufferParametersIuivNV(nativeint target, nativeint bindingIndex, nativeint wordIndex, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramBufferParametersfvNV(nativeint target, nativeint bindingIndex, nativeint wordIndex, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameter4dARB(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameter4dvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameter4fARB(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameter4fvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameterI4iNV(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameterI4ivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameterI4uiNV(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameterI4uivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParameters4fvEXT(nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParametersI4ivNV(nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramEnvParametersI4uivNV(nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameter4dARB(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameter4dvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameter4fARB(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameter4fvARB(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameterI4iNV(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameterI4ivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameterI4uiNV(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameterI4uivNV(nativeint target, nativeint index, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParameters4fvEXT(nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParametersI4ivNV(nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramLocalParametersI4uivNV(nativeint target, nativeint index, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramNamedParameter4dNV(nativeint id, nativeint len, nativeint* name, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramNamedParameter4dvNV(nativeint id, nativeint len, nativeint* name, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramNamedParameter4fNV(nativeint id, nativeint len, nativeint* name, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramNamedParameter4fvNV(nativeint id, nativeint len, nativeint* name, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameter4dNV(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameter4dvNV(nativeint target, nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameter4fNV(nativeint target, nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameter4fvNV(nativeint target, nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameteri(nativeint program, nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameteriARB(nativeint program, nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameteriEXT(nativeint program, nativeint pname, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameters4dvNV(nativeint target, nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramParameters4fvNV(nativeint target, nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramPathFragmentInputGenNV(nativeint program, nativeint location, nativeint genMode, nativeint components, nativeint* coeffs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramStringARB(nativeint target, nativeint format, nativeint len, nativeint string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramSubroutineParametersuivNV(nativeint target, nativeint count, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1d(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1dEXT(nativeint program, nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1dv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1dvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1f(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1fEXT(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1fv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1fvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1i(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1i64ARB(nativeint program, nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1i64NV(nativeint program, nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1i64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1i64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1iEXT(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1iv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1ivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1ui(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1ui64ARB(nativeint program, nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1ui64NV(nativeint program, nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1ui64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1ui64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1uiEXT(nativeint program, nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1uiv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform1uivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2d(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2dEXT(nativeint program, nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2dv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2dvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2f(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2fEXT(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2fv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2fvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2i(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2i64ARB(nativeint program, nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2i64NV(nativeint program, nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2i64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2i64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2iEXT(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2iv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2ivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2ui(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2ui64ARB(nativeint program, nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2ui64NV(nativeint program, nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2ui64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2ui64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2uiEXT(nativeint program, nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2uiv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform2uivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3d(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3dEXT(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3dv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3dvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3f(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3fEXT(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3fv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3fvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3i(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3i64ARB(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3i64NV(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3i64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3i64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3iEXT(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3iv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3ivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3ui(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3ui64ARB(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3ui64NV(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3ui64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3ui64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3uiEXT(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3uiv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform3uivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4d(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4dEXT(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4dv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4dvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4f(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4fEXT(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4fv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4fvEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4i(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4i64ARB(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4i64NV(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4i64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4i64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4iEXT(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4iv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4ivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4ui(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4ui64ARB(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4ui64NV(nativeint program, nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4ui64vARB(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4ui64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4uiEXT(nativeint program, nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4uiv(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniform4uivEXT(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformHandleui64ARB(nativeint program, nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformHandleui64IMG(nativeint program, nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformHandleui64NV(nativeint program, nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformHandleui64vARB(nativeint program, nativeint location, nativeint count, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformHandleui64vIMG(nativeint program, nativeint location, nativeint count, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformHandleui64vNV(nativeint program, nativeint location, nativeint count, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x3dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x3dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x3fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x3fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x4dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x4dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x4fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix2x4fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x2dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x2dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x2fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x2fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x4dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x4dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x4fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix3x4fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x2dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x2dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x2fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x2fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x3dv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x3dvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x3fv(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformMatrix4x3fvEXT(nativeint program, nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformui64NV(nativeint program, nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramUniformui64vNV(nativeint program, nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProgramVertexLimitNV(nativeint target, nativeint limit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProvokingVertex(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glProvokingVertexEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushAttrib(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushClientAttrib(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushClientAttribDefaultEXT(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushDebugGroup(nativeint source, nativeint id, nativeint length, nativeint* message)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushDebugGroupKHR(nativeint source, nativeint id, nativeint length, nativeint* message)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushGroupMarkerEXT(nativeint length, nativeint* marker)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushMatrix()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glPushName(nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glQueryCounter(nativeint id, nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glQueryCounterEXT(nativeint id, nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glQueryMatrixxOES(nativeint* mantissa, nativeint* exponent)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glQueryObjectParameteruiAMD(nativeint target, nativeint id, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glQueryResourceNV(nativeint queryType, nativeint tagId, nativeint bufSize, nativeint* buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glQueryResourceTagNV(nativeint tagId, nativeint* tagString)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2d(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2f(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2i(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2s(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2xOES(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos2xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3d(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3f(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3i(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3s(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3xOES(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos3xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4d(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4f(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4i(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4s(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4xOES(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterPos4xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRasterSamplesEXT(nativeint samples, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadBuffer(nativeint src)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadBufferIndexedEXT(nativeint src, nativeint index)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadBufferNV(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadInstrumentsSGIX(nativeint marker)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadPixels(nativeint x, nativeint y, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadnPixels(nativeint x, nativeint y, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint bufSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadnPixelsARB(nativeint x, nativeint y, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint bufSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadnPixelsEXT(nativeint x, nativeint y, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint bufSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReadnPixelsKHR(nativeint x, nativeint y, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint bufSize, nativeint data)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glReleaseKeyedMutexWin32EXT(nativeint memory, nativeint key)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectd(nativeint x1, nativeint y1, nativeint x2, nativeint y2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectdv(nativeint* v1, nativeint* v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectf(nativeint x1, nativeint y1, nativeint x2, nativeint y2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectfv(nativeint* v1, nativeint* v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRecti(nativeint x1, nativeint y1, nativeint x2, nativeint y2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectiv(nativeint* v1, nativeint* v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRects(nativeint x1, nativeint y1, nativeint x2, nativeint y2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectsv(nativeint* v1, nativeint* v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectxOES(nativeint x1, nativeint y1, nativeint x2, nativeint y2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRectxvOES(nativeint* v1, nativeint* v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReferencePlaneSGIX(nativeint* equation)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReleaseShaderCompiler()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderGpuMaskNV(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glRenderMode(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorage(nativeint target, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageEXT(nativeint target, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisample(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleANGLE(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleAPPLE(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleAdvancedAMD(nativeint target, nativeint samples, nativeint storageSamples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleCoverageNV(nativeint target, nativeint coverageSamples, nativeint colorSamples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleEXT(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleIMG(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageMultisampleNV(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRenderbufferStorageOES(nativeint target, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodePointerSUN(nativeint _type, nativeint stride, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeubSUN(nativeint code)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeubvSUN(nativeint* code)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiColor3fVertex3fSUN(nativeint rc, nativeint r, nativeint g, nativeint b, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiColor3fVertex3fvSUN(nativeint* rc, nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiColor4fNormal3fVertex3fSUN(nativeint rc, nativeint r, nativeint g, nativeint b, nativeint a, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiColor4fNormal3fVertex3fvSUN(nativeint* rc, nativeint* c, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiColor4ubVertex3fSUN(nativeint rc, nativeint r, nativeint g, nativeint b, nativeint a, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiColor4ubVertex3fvSUN(nativeint* rc, nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiNormal3fVertex3fSUN(nativeint rc, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiNormal3fVertex3fvSUN(nativeint* rc, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiSUN(nativeint code)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiTexCoord2fColor4fNormal3fVertex3fSUN(nativeint rc, nativeint s, nativeint t, nativeint r, nativeint g, nativeint b, nativeint a, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiTexCoord2fColor4fNormal3fVertex3fvSUN(nativeint* rc, nativeint* tc, nativeint* c, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiTexCoord2fNormal3fVertex3fSUN(nativeint rc, nativeint s, nativeint t, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiTexCoord2fNormal3fVertex3fvSUN(nativeint* rc, nativeint* tc, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiTexCoord2fVertex3fSUN(nativeint rc, nativeint s, nativeint t, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiTexCoord2fVertex3fvSUN(nativeint* rc, nativeint* tc, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiVertex3fSUN(nativeint rc, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuiVertex3fvSUN(nativeint* rc, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeuivSUN(nativeint* code)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeusSUN(nativeint code)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glReplacementCodeusvSUN(nativeint* code)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRequestResidentProgramsNV(nativeint n, nativeint* programs)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResetHistogram(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResetHistogramEXT(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResetMemoryObjectParameterNV(nativeint memory, nativeint pname)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResetMinmax(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResetMinmaxEXT(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResizeBuffersMESA()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResolveDepthValuesNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResolveMultisampleFramebufferAPPLE()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResumeTransformFeedback()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glResumeTransformFeedbackNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRotated(nativeint angle, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRotatef(nativeint angle, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRotatex(nativeint angle, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glRotatexOES(nativeint angle, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleCoverage(nativeint value, nativeint invert)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleCoverageARB(nativeint value, nativeint invert)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleCoveragex(nativeint value, nativeint invert)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleCoveragexOES(nativeint value, nativeint invert)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleMapATI(nativeint dst, nativeint interp, nativeint swizzle)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleMaskEXT(nativeint value, nativeint invert)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleMaskIndexedNV(nativeint index, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleMaskSGIS(nativeint value, nativeint invert)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSampleMaski(nativeint maskNumber, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplePatternEXT(nativeint pattern)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplePatternSGIS(nativeint pattern)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterIiv(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterIivEXT(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterIivOES(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterIuiv(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterIuivEXT(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterIuivOES(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterf(nativeint sampler, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameterfv(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameteri(nativeint sampler, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSamplerParameteriv(nativeint sampler, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScaled(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScalef(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScalex(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScalexOES(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissor(nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorArrayv(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorArrayvNV(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorArrayvOES(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorExclusiveArrayvNV(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorExclusiveNV(nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorIndexed(nativeint index, nativeint left, nativeint bottom, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorIndexedNV(nativeint index, nativeint left, nativeint bottom, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorIndexedOES(nativeint index, nativeint left, nativeint bottom, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorIndexedv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorIndexedvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glScissorIndexedvOES(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3b(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3bEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3bv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3bvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3d(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3dEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3dvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3f(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3fEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3fvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3hNV(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3i(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3iEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3ivEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3s(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3sEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3svEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3ub(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3ubEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3ubv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3ubvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3ui(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3uiEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3uiv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3uivEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3us(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3usEXT(nativeint red, nativeint green, nativeint blue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3usv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColor3usvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColorFormatNV(nativeint size, nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColorP3ui(nativeint _type, nativeint color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColorP3uiv(nativeint _type, nativeint* color)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColorPointer(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColorPointerEXT(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSecondaryColorPointerListIBM(nativeint size, nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSelectBuffer(nativeint size, nativeint* buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSelectPerfMonitorCountersAMD(nativeint monitor, nativeint enable, nativeint group, nativeint numCounters, nativeint* counterList)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSemaphoreParameterui64vEXT(nativeint semaphore, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSeparableFilter2D(nativeint target, nativeint internalformat, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint row, nativeint column)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSeparableFilter2DEXT(nativeint target, nativeint internalformat, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint row, nativeint column)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSetFenceAPPLE(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSetFenceNV(nativeint fence, nativeint condition)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSetFragmentShaderConstantATI(nativeint dst, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSetInvariantEXT(nativeint id, nativeint _type, nativeint addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSetLocalConstantEXT(nativeint id, nativeint _type, nativeint addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSetMultisamplefvAMD(nativeint pname, nativeint index, nativeint* _val)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShadeModel(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderBinary(nativeint count, nativeint* shaders, nativeint binaryformat, nativeint binary, nativeint length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderOp1EXT(nativeint op, nativeint res, nativeint arg1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderOp2EXT(nativeint op, nativeint res, nativeint arg1, nativeint arg2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderOp3EXT(nativeint op, nativeint res, nativeint arg1, nativeint arg2, nativeint arg3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderSource(nativeint shader, nativeint count, nativeint* string, nativeint* length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderSourceARB(nativeint shaderObj, nativeint count, nativeint* string, nativeint* length)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShaderStorageBlockBinding(nativeint program, nativeint storageBlockIndex, nativeint storageBlockBinding)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShadingRateImageBarrierNV(nativeint synchronize)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShadingRateImagePaletteNV(nativeint viewport, nativeint first, nativeint count, nativeint* rates)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShadingRateSampleOrderNV(nativeint order)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glShadingRateSampleOrderCustomNV(nativeint rate, nativeint samples, nativeint* locations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSharpenTexFuncSGIS(nativeint target, nativeint n, nativeint* points)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSignalSemaphoreEXT(nativeint semaphore, nativeint numBufferBarriers, nativeint* buffers, nativeint numTextureBarriers, nativeint* textures, nativeint* dstLayouts)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSpecializeShader(nativeint shader, nativeint* pEntryPoint, nativeint numSpecializationConstants, nativeint* pConstantIndex, nativeint* pConstantValue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSpecializeShaderARB(nativeint shader, nativeint* pEntryPoint, nativeint numSpecializationConstants, nativeint* pConstantIndex, nativeint* pConstantValue)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSpriteParameterfSGIX(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSpriteParameterfvSGIX(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSpriteParameteriSGIX(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSpriteParameterivSGIX(nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStartInstrumentsSGIX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStartTilingQCOM(nativeint x, nativeint y, nativeint width, nativeint height, nativeint preserveMask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStateCaptureNV(nativeint state, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilClearTagEXT(nativeint stencilTagBits, nativeint stencilClearTag)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilFillPathInstancedNV(nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint fillMode, nativeint mask, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilFillPathNV(nativeint path, nativeint fillMode, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilFunc(nativeint func, nativeint ref, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilFuncSeparate(nativeint face, nativeint func, nativeint ref, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilFuncSeparateATI(nativeint frontfunc, nativeint backfunc, nativeint ref, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilMask(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilMaskSeparate(nativeint face, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilOp(nativeint fail, nativeint zfail, nativeint zpass)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilOpSeparate(nativeint face, nativeint sfail, nativeint dpfail, nativeint dppass)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilOpSeparateATI(nativeint face, nativeint sfail, nativeint dpfail, nativeint dppass)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilOpValueAMD(nativeint face, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilStrokePathInstancedNV(nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint reference, nativeint mask, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilStrokePathNV(nativeint path, nativeint reference, nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilThenCoverFillPathInstancedNV(nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint fillMode, nativeint mask, nativeint coverMode, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilThenCoverFillPathNV(nativeint path, nativeint fillMode, nativeint mask, nativeint coverMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilThenCoverStrokePathInstancedNV(nativeint numPaths, nativeint pathNameType, nativeint paths, nativeint pathBase, nativeint reference, nativeint mask, nativeint coverMode, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStencilThenCoverStrokePathNV(nativeint path, nativeint reference, nativeint mask, nativeint coverMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStopInstrumentsSGIX(nativeint marker)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glStringMarkerGREMEDY(nativeint len, nativeint string)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSubpixelPrecisionBiasNV(nativeint xbits, nativeint ybits)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSwizzleEXT(nativeint res, nativeint _in, nativeint outX, nativeint outY, nativeint outZ, nativeint outW)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSyncTextureINTEL(nativeint texture)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTagSampleBufferSGIX()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3bEXT(nativeint tx, nativeint ty, nativeint tz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3bvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3dEXT(nativeint tx, nativeint ty, nativeint tz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3dvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3fEXT(nativeint tx, nativeint ty, nativeint tz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3fvEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3iEXT(nativeint tx, nativeint ty, nativeint tz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3ivEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3sEXT(nativeint tx, nativeint ty, nativeint tz)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangent3svEXT(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTangentPointerEXT(nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTbufferMask3DFX(nativeint mask)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTessellationFactorAMD(nativeint factor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTessellationModeAMD(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glTestFenceAPPLE(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glTestFenceNV(nativeint fence)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glTestObjectAPPLE(nativeint _object, nativeint name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexAttachMemoryNV(nativeint target, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBuffer(nativeint target, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBufferARB(nativeint target, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBufferEXT(nativeint target, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBufferOES(nativeint target, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBufferRange(nativeint target, nativeint internalformat, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBufferRangeEXT(nativeint target, nativeint internalformat, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBufferRangeOES(nativeint target, nativeint internalformat, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBumpParameterfvATI(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexBumpParameterivATI(nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1bOES(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1d(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1f(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1hNV(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1i(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1s(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1xOES(nativeint s)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord1xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2bOES(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2d(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2f(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fColor3fVertex3fSUN(nativeint s, nativeint t, nativeint r, nativeint g, nativeint b, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fColor3fVertex3fvSUN(nativeint* tc, nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fColor4fNormal3fVertex3fSUN(nativeint s, nativeint t, nativeint r, nativeint g, nativeint b, nativeint a, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fColor4fNormal3fVertex3fvSUN(nativeint* tc, nativeint* c, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fColor4ubVertex3fSUN(nativeint s, nativeint t, nativeint r, nativeint g, nativeint b, nativeint a, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fColor4ubVertex3fvSUN(nativeint* tc, nativeint* c, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fNormal3fVertex3fSUN(nativeint s, nativeint t, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fNormal3fVertex3fvSUN(nativeint* tc, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fVertex3fSUN(nativeint s, nativeint t, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fVertex3fvSUN(nativeint* tc, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2hNV(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2i(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2s(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2xOES(nativeint s, nativeint t)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord2xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3bOES(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3d(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3f(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3hNV(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3i(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3s(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3xOES(nativeint s, nativeint t, nativeint r)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord3xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4bOES(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4d(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4f(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4fColor4fNormal3fVertex4fSUN(nativeint s, nativeint t, nativeint p, nativeint q, nativeint r, nativeint g, nativeint b, nativeint a, nativeint nx, nativeint ny, nativeint nz, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4fColor4fNormal3fVertex4fvSUN(nativeint* tc, nativeint* c, nativeint* n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4fVertex4fSUN(nativeint s, nativeint t, nativeint p, nativeint q, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4fVertex4fvSUN(nativeint* tc, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4hNV(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4i(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4s(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4xOES(nativeint s, nativeint t, nativeint r, nativeint q)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoord4xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordFormatNV(nativeint size, nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP1ui(nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP1uiv(nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP2ui(nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP2uiv(nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP3ui(nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP3uiv(nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP4ui(nativeint _type, nativeint coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordP4uiv(nativeint _type, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordPointer(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordPointerEXT(nativeint size, nativeint _type, nativeint stride, nativeint count, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordPointerListIBM(nativeint size, nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexCoordPointervINTEL(nativeint size, nativeint _type, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvf(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvi(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnviv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvx(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvxOES(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvxv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexEnvxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexFilterFuncSGIS(nativeint target, nativeint filter, nativeint n, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGend(nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGendv(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenf(nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenfOES(nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenfv(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenfvOES(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGeni(nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGeniOES(nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGeniv(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenivOES(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenxOES(nativeint coord, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexGenxvOES(nativeint coord, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage1D(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage2D(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage2DMultisample(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage2DMultisampleCoverageNV(nativeint target, nativeint coverageSamples, nativeint colorSamples, nativeint internalFormat, nativeint width, nativeint height, nativeint fixedSampleLocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage3D(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage3DEXT(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage3DMultisample(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage3DMultisampleCoverageNV(nativeint target, nativeint coverageSamples, nativeint colorSamples, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint fixedSampleLocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage3DOES(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexImage4DSGIS(nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint size4d, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexPageCommitmentARB(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint commit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexPageCommitmentEXT(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint commit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterIiv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterIivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterIivOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterIuiv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterIuivEXT(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterIuivOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterf(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterfv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameteri(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameteriv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterx(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterxOES(nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterxv(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexParameterxvOES(nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexRenderbufferNV(nativeint target, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage1D(nativeint target, nativeint levels, nativeint internalformat, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage1DEXT(nativeint target, nativeint levels, nativeint internalformat, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage2D(nativeint target, nativeint levels, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage2DEXT(nativeint target, nativeint levels, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage2DMultisample(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage3D(nativeint target, nativeint levels, nativeint internalformat, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage3DEXT(nativeint target, nativeint levels, nativeint internalformat, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage3DMultisample(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorage3DMultisampleOES(nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorageMem1DEXT(nativeint target, nativeint levels, nativeint internalFormat, nativeint width, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorageMem2DEXT(nativeint target, nativeint levels, nativeint internalFormat, nativeint width, nativeint height, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorageMem2DMultisampleEXT(nativeint target, nativeint samples, nativeint internalFormat, nativeint width, nativeint height, nativeint fixedSampleLocations, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorageMem3DEXT(nativeint target, nativeint levels, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorageMem3DMultisampleEXT(nativeint target, nativeint samples, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint fixedSampleLocations, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexStorageSparseAMD(nativeint target, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint layers, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage1D(nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage1DEXT(nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage2D(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage2DEXT(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage3D(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage3DEXT(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage3DOES(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexSubImage4DSGIS(nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint woffset, nativeint width, nativeint height, nativeint depth, nativeint size4d, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureAttachMemoryNV(nativeint texture, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureBarrier()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureBarrierNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureBuffer(nativeint texture, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureBufferEXT(nativeint texture, nativeint target, nativeint internalformat, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureBufferRange(nativeint texture, nativeint internalformat, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureBufferRangeEXT(nativeint texture, nativeint target, nativeint internalformat, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureColorMaskSGIS(nativeint red, nativeint green, nativeint blue, nativeint alpha)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureFoveationParametersQCOM(nativeint texture, nativeint layer, nativeint focalPoint, nativeint focalX, nativeint focalY, nativeint gainX, nativeint gainY, nativeint foveaArea)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage1DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage2DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage2DMultisampleCoverageNV(nativeint texture, nativeint target, nativeint coverageSamples, nativeint colorSamples, nativeint internalFormat, nativeint width, nativeint height, nativeint fixedSampleLocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage2DMultisampleNV(nativeint texture, nativeint target, nativeint samples, nativeint internalFormat, nativeint width, nativeint height, nativeint fixedSampleLocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage3DEXT(nativeint texture, nativeint target, nativeint level, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint border, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage3DMultisampleCoverageNV(nativeint texture, nativeint target, nativeint coverageSamples, nativeint colorSamples, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint fixedSampleLocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureImage3DMultisampleNV(nativeint texture, nativeint target, nativeint samples, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint fixedSampleLocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureLightEXT(nativeint pname)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureMaterialEXT(nativeint face, nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureNormalEXT(nativeint mode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTexturePageCommitmentEXT(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint commit)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterIiv(nativeint texture, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterIivEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterIuiv(nativeint texture, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterIuivEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterf(nativeint texture, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterfEXT(nativeint texture, nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterfv(nativeint texture, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterfvEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameteri(nativeint texture, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameteriEXT(nativeint texture, nativeint target, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameteriv(nativeint texture, nativeint pname, nativeint* param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureParameterivEXT(nativeint texture, nativeint target, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureRangeAPPLE(nativeint target, nativeint length, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureRenderbufferEXT(nativeint texture, nativeint target, nativeint renderbuffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage1D(nativeint texture, nativeint levels, nativeint internalformat, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage1DEXT(nativeint texture, nativeint target, nativeint levels, nativeint internalformat, nativeint width)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage2D(nativeint texture, nativeint levels, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage2DEXT(nativeint texture, nativeint target, nativeint levels, nativeint internalformat, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage2DMultisample(nativeint texture, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage2DMultisampleEXT(nativeint texture, nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage3D(nativeint texture, nativeint levels, nativeint internalformat, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage3DEXT(nativeint texture, nativeint target, nativeint levels, nativeint internalformat, nativeint width, nativeint height, nativeint depth)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage3DMultisample(nativeint texture, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorage3DMultisampleEXT(nativeint texture, nativeint target, nativeint samples, nativeint internalformat, nativeint width, nativeint height, nativeint depth, nativeint fixedsamplelocations)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorageMem1DEXT(nativeint texture, nativeint levels, nativeint internalFormat, nativeint width, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorageMem2DEXT(nativeint texture, nativeint levels, nativeint internalFormat, nativeint width, nativeint height, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorageMem2DMultisampleEXT(nativeint texture, nativeint samples, nativeint internalFormat, nativeint width, nativeint height, nativeint fixedSampleLocations, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorageMem3DEXT(nativeint texture, nativeint levels, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorageMem3DMultisampleEXT(nativeint texture, nativeint samples, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint fixedSampleLocations, nativeint memory, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureStorageSparseAMD(nativeint texture, nativeint target, nativeint internalFormat, nativeint width, nativeint height, nativeint depth, nativeint layers, nativeint flags)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureSubImage1D(nativeint texture, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureSubImage1DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint width, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureSubImage2D(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureSubImage2DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint width, nativeint height, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureSubImage3D(nativeint texture, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureSubImage3DEXT(nativeint texture, nativeint target, nativeint level, nativeint xoffset, nativeint yoffset, nativeint zoffset, nativeint width, nativeint height, nativeint depth, nativeint format, nativeint _type, nativeint pixels)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureView(nativeint texture, nativeint target, nativeint origtexture, nativeint internalformat, nativeint minlevel, nativeint numlevels, nativeint minlayer, nativeint numlayers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureViewEXT(nativeint texture, nativeint target, nativeint origtexture, nativeint internalformat, nativeint minlevel, nativeint numlevels, nativeint minlayer, nativeint numlayers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTextureViewOES(nativeint texture, nativeint target, nativeint origtexture, nativeint internalformat, nativeint minlevel, nativeint numlevels, nativeint minlayer, nativeint numlayers)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTrackMatrixNV(nativeint target, nativeint address, nativeint matrix, nativeint transform)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackAttribsNV(nativeint count, nativeint* attribs, nativeint bufferMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackBufferBase(nativeint xfb, nativeint index, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackBufferRange(nativeint xfb, nativeint index, nativeint buffer, nativeint offset, nativeint size)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackStreamAttribsNV(nativeint count, nativeint* attribs, nativeint nbuffers, nativeint* bufstreams, nativeint bufferMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackVaryings(nativeint program, nativeint count, nativeint* varyings, nativeint bufferMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackVaryingsEXT(nativeint program, nativeint count, nativeint* varyings, nativeint bufferMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformFeedbackVaryingsNV(nativeint program, nativeint count, nativeint* locations, nativeint bufferMode)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTransformPathNV(nativeint resultPath, nativeint srcPath, nativeint transformType, nativeint* transformValues)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTranslated(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTranslatef(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTranslatex(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glTranslatexOES(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1d(nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1dv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1f(nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1fARB(nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1fv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1fvARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1i(nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1i64ARB(nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1i64NV(nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1i64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1i64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1iARB(nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1iv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1ivARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1ui(nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1ui64ARB(nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1ui64NV(nativeint location, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1ui64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1ui64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1uiEXT(nativeint location, nativeint v0)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1uiv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform1uivEXT(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2d(nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2dv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2f(nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2fARB(nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2fv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2fvARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2i(nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2i64ARB(nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2i64NV(nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2i64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2i64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2iARB(nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2iv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2ivARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2ui(nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2ui64ARB(nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2ui64NV(nativeint location, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2ui64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2ui64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2uiEXT(nativeint location, nativeint v0, nativeint v1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2uiv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform2uivEXT(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3d(nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3dv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3f(nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3fARB(nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3fv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3fvARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3i(nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3i64ARB(nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3i64NV(nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3i64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3i64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3iARB(nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3iv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3ivARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3ui(nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3ui64ARB(nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3ui64NV(nativeint location, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3ui64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3ui64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3uiEXT(nativeint location, nativeint v0, nativeint v1, nativeint v2)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3uiv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform3uivEXT(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4d(nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4dv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4f(nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4fARB(nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4fv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4fvARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4i(nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4i64ARB(nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4i64NV(nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4i64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4i64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4iARB(nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4iv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4ivARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4ui(nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4ui64ARB(nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4ui64NV(nativeint location, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4ui64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4ui64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4uiEXT(nativeint location, nativeint v0, nativeint v1, nativeint v2, nativeint v3)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4uiv(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniform4uivEXT(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformBlockBinding(nativeint program, nativeint uniformBlockIndex, nativeint uniformBlockBinding)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformBufferEXT(nativeint program, nativeint location, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformHandleui64ARB(nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformHandleui64IMG(nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformHandleui64NV(nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformHandleui64vARB(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformHandleui64vIMG(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformHandleui64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2fvARB(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2x3dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2x3fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2x3fvNV(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2x4dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2x4fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix2x4fvNV(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3fvARB(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3x2dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3x2fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3x2fvNV(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3x4dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3x4fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix3x4fvNV(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4fvARB(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4x2dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4x2fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4x2fvNV(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4x3dv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4x3fv(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformMatrix4x3fvNV(nativeint location, nativeint count, nativeint transpose, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformSubroutinesuiv(nativeint shadertype, nativeint count, nativeint* indices)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformui64NV(nativeint location, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUniformui64vNV(nativeint location, nativeint count, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUnlockArraysEXT()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glUnmapBuffer(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glUnmapBufferARB(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glUnmapBufferOES(nativeint target)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glUnmapNamedBuffer(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glUnmapNamedBufferEXT(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUnmapObjectBufferATI(nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUnmapTexture2DINTEL(nativeint texture, nativeint level)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUpdateObjectBufferATI(nativeint buffer, nativeint offset, nativeint size, nativeint pointer, nativeint preserve)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUseProgram(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUseProgramObjectARB(nativeint programObj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUseProgramStages(nativeint pipeline, nativeint stages, nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUseProgramStagesEXT(nativeint pipeline, nativeint stages, nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glUseShaderProgramEXT(nativeint _type, nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUFiniNV()
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUGetSurfaceivNV(nativeint surface, nativeint pname, nativeint bufSize, nativeint* length, nativeint* values)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUInitNV(nativeint vdpDevice, nativeint getProcAddress)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glVDPAUIsSurfaceNV(nativeint surface)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUMapSurfacesNV(nativeint numSurfaces, nativeint* surfaces)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glVDPAURegisterOutputSurfaceNV(nativeint vdpSurface, nativeint target, nativeint numTextureNames, nativeint* textureNames)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glVDPAURegisterVideoSurfaceNV(nativeint vdpSurface, nativeint target, nativeint numTextureNames, nativeint* textureNames)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glVDPAURegisterVideoSurfaceWithPictureStructureNV(nativeint vdpSurface, nativeint target, nativeint numTextureNames, nativeint* textureNames, nativeint isFrameStructure)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUSurfaceAccessNV(nativeint surface, nativeint access)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUUnmapSurfacesNV(nativeint numSurface, nativeint* surfaces)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVDPAUUnregisterSurfaceNV(nativeint surface)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glValidateProgram(nativeint program)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glValidateProgramARB(nativeint programObj)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glValidateProgramPipeline(nativeint pipeline)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glValidateProgramPipelineEXT(nativeint pipeline)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantArrayObjectATI(nativeint id, nativeint _type, nativeint stride, nativeint buffer, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantPointerEXT(nativeint id, nativeint _type, nativeint stride, nativeint addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantbvEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantdvEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantfvEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantivEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantsvEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantubvEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantuivEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVariantusvEXT(nativeint id, nativeint* addr)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2bOES(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2d(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2f(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2hNV(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2i(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2s(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2xOES(nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex2xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3bOES(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3d(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3f(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3hNV(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3i(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3s(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3xOES(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex3xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4bOES(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4bvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4d(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4f(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4hNV(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4hvNV(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4i(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4s(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4xOES(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertex4xvOES(nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayAttribBinding(nativeint vaobj, nativeint attribindex, nativeint bindingindex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayAttribFormat(nativeint vaobj, nativeint attribindex, nativeint size, nativeint _type, nativeint normalized, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayAttribIFormat(nativeint vaobj, nativeint attribindex, nativeint size, nativeint _type, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayAttribLFormat(nativeint vaobj, nativeint attribindex, nativeint size, nativeint _type, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayBindVertexBufferEXT(nativeint vaobj, nativeint bindingindex, nativeint buffer, nativeint offset, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayBindingDivisor(nativeint vaobj, nativeint bindingindex, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayColorOffsetEXT(nativeint vaobj, nativeint buffer, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayEdgeFlagOffsetEXT(nativeint vaobj, nativeint buffer, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayElementBuffer(nativeint vaobj, nativeint buffer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayFogCoordOffsetEXT(nativeint vaobj, nativeint buffer, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayIndexOffsetEXT(nativeint vaobj, nativeint buffer, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayMultiTexCoordOffsetEXT(nativeint vaobj, nativeint buffer, nativeint texunit, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayNormalOffsetEXT(nativeint vaobj, nativeint buffer, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayParameteriAPPLE(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayRangeAPPLE(nativeint length, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayRangeNV(nativeint length, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArraySecondaryColorOffsetEXT(nativeint vaobj, nativeint buffer, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayTexCoordOffsetEXT(nativeint vaobj, nativeint buffer, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribBindingEXT(nativeint vaobj, nativeint attribindex, nativeint bindingindex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribDivisorEXT(nativeint vaobj, nativeint index, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribFormatEXT(nativeint vaobj, nativeint attribindex, nativeint size, nativeint _type, nativeint normalized, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribIFormatEXT(nativeint vaobj, nativeint attribindex, nativeint size, nativeint _type, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribIOffsetEXT(nativeint vaobj, nativeint buffer, nativeint index, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribLFormatEXT(nativeint vaobj, nativeint attribindex, nativeint size, nativeint _type, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribLOffsetEXT(nativeint vaobj, nativeint buffer, nativeint index, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexAttribOffsetEXT(nativeint vaobj, nativeint buffer, nativeint index, nativeint size, nativeint _type, nativeint normalized, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexBindingDivisorEXT(nativeint vaobj, nativeint bindingindex, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexBuffer(nativeint vaobj, nativeint bindingindex, nativeint buffer, nativeint offset, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexBuffers(nativeint vaobj, nativeint first, nativeint count, nativeint* buffers, nativeint* offsets, nativeint* strides)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexArrayVertexOffsetEXT(nativeint vaobj, nativeint buffer, nativeint size, nativeint _type, nativeint stride, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1d(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1dARB(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1dNV(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1dvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1dvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1f(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1fARB(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1fNV(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1fv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1fvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1fvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1hNV(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1hvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1s(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1sARB(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1sNV(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1sv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1svARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib1svNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2d(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2dARB(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2dNV(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2dvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2dvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2f(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2fARB(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2fNV(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2fv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2fvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2fvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2hNV(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2hvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2s(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2sARB(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2sNV(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2sv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2svARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib2svNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3d(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3dARB(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3dNV(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3dvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3dvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3f(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3fARB(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3fNV(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3fv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3fvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3fvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3hNV(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3hvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3s(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3sARB(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3sNV(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3sv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3svARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib3svNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Nbv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NbvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Niv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NivARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Nsv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NsvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Nub(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NubARB(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Nubv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NubvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Nuiv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NuivARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4Nusv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4NusvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4bv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4bvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4d(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4dARB(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4dNV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4dvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4dvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4f(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4fARB(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4fNV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4fv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4fvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4fvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4hNV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4hvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4iv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4ivARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4s(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4sARB(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4sNV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4sv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4svARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4svNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4ubNV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4ubv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4ubvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4ubvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4uiv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4uivARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4usv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttrib4usvARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribArrayObjectATI(nativeint index, nativeint size, nativeint _type, nativeint normalized, nativeint stride, nativeint buffer, nativeint offset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribBinding(nativeint attribindex, nativeint bindingindex)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribDivisor(nativeint index, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribDivisorANGLE(nativeint index, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribDivisorARB(nativeint index, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribDivisorEXT(nativeint index, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribDivisorNV(nativeint index, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribFormat(nativeint attribindex, nativeint size, nativeint _type, nativeint normalized, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribFormatNV(nativeint index, nativeint size, nativeint _type, nativeint normalized, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1i(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1iEXT(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1iv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1ivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1ui(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1uiEXT(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1uiv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI1uivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2i(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2iEXT(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2iv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2ivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2ui(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2uiEXT(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2uiv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI2uivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3i(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3iEXT(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3iv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3ivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3ui(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3uiEXT(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3uiv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI3uivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4bv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4bvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4i(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4iEXT(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4iv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4ivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4sv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4svEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4ubv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4ubvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4ui(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4uiEXT(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4uiv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4uivEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4usv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribI4usvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribIFormat(nativeint attribindex, nativeint size, nativeint _type, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribIFormatNV(nativeint index, nativeint size, nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribIPointer(nativeint index, nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribIPointerEXT(nativeint index, nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1d(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1dEXT(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1dvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1i64NV(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1i64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1ui64ARB(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1ui64NV(nativeint index, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1ui64vARB(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL1ui64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2d(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2dEXT(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2dvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2i64NV(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2i64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2ui64NV(nativeint index, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL2ui64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3d(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3dEXT(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3dvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3i64NV(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3i64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3ui64NV(nativeint index, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL3ui64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4d(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4dEXT(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4dv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4dvEXT(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4i64NV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4i64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4ui64NV(nativeint index, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribL4ui64vNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribLFormat(nativeint attribindex, nativeint size, nativeint _type, nativeint relativeoffset)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribLFormatNV(nativeint index, nativeint size, nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribLPointer(nativeint index, nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribLPointerEXT(nativeint index, nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP1ui(nativeint index, nativeint _type, nativeint normalized, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP1uiv(nativeint index, nativeint _type, nativeint normalized, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP2ui(nativeint index, nativeint _type, nativeint normalized, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP2uiv(nativeint index, nativeint _type, nativeint normalized, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP3ui(nativeint index, nativeint _type, nativeint normalized, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP3uiv(nativeint index, nativeint _type, nativeint normalized, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP4ui(nativeint index, nativeint _type, nativeint normalized, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribP4uiv(nativeint index, nativeint _type, nativeint normalized, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribParameteriAMD(nativeint index, nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribPointer(nativeint index, nativeint size, nativeint _type, nativeint normalized, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribPointerARB(nativeint index, nativeint size, nativeint _type, nativeint normalized, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribPointerNV(nativeint index, nativeint fsize, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs1dvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs1fvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs1hvNV(nativeint index, nativeint n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs1svNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs2dvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs2fvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs2hvNV(nativeint index, nativeint n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs2svNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs3dvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs3fvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs3hvNV(nativeint index, nativeint n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs3svNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs4dvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs4fvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs4hvNV(nativeint index, nativeint n, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs4svNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexAttribs4ubvNV(nativeint index, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexBindingDivisor(nativeint bindingindex, nativeint divisor)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexBlendARB(nativeint count)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexBlendEnvfATI(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexBlendEnviATI(nativeint pname, nativeint param)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexFormatNV(nativeint size, nativeint _type, nativeint stride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexP2ui(nativeint _type, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexP2uiv(nativeint _type, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexP3ui(nativeint _type, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexP3uiv(nativeint _type, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexP4ui(nativeint _type, nativeint value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexP4uiv(nativeint _type, nativeint* value)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexPointer(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexPointerEXT(nativeint size, nativeint _type, nativeint stride, nativeint count, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexPointerListIBM(nativeint size, nativeint _type, nativeint stride, nativeint* pointer, nativeint ptrstride)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexPointervINTEL(nativeint size, nativeint _type, nativeint* pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1dATI(nativeint stream, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1dvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1fATI(nativeint stream, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1fvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1iATI(nativeint stream, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1ivATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1sATI(nativeint stream, nativeint x)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream1svATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2dATI(nativeint stream, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2dvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2fATI(nativeint stream, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2fvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2iATI(nativeint stream, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2ivATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2sATI(nativeint stream, nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream2svATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3dATI(nativeint stream, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3dvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3fATI(nativeint stream, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3fvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3iATI(nativeint stream, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3ivATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3sATI(nativeint stream, nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream3svATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4dATI(nativeint stream, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4dvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4fATI(nativeint stream, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4fvATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4iATI(nativeint stream, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4ivATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4sATI(nativeint stream, nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexStream4svATI(nativeint stream, nativeint* coords)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexWeightPointerEXT(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexWeightfEXT(nativeint weight)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexWeightfvEXT(nativeint* weight)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexWeighthNV(nativeint weight)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVertexWeighthvNV(nativeint* weight)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glVideoCaptureNV(nativeint video_capture_slot, nativeint* sequence_num, nativeint* capture_time)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVideoCaptureStreamParameterdvNV(nativeint video_capture_slot, nativeint stream, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVideoCaptureStreamParameterfvNV(nativeint video_capture_slot, nativeint stream, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glVideoCaptureStreamParameterivNV(nativeint video_capture_slot, nativeint stream, nativeint pname, nativeint* _params)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewport(nativeint x, nativeint y, nativeint width, nativeint height)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportArrayv(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportArrayvNV(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportArrayvOES(nativeint first, nativeint count, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportIndexedf(nativeint index, nativeint x, nativeint y, nativeint w, nativeint h)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportIndexedfOES(nativeint index, nativeint x, nativeint y, nativeint w, nativeint h)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportIndexedfNV(nativeint index, nativeint x, nativeint y, nativeint w, nativeint h)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportIndexedfv(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportIndexedfvOES(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportIndexedfvNV(nativeint index, nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportPositionWScaleNV(nativeint index, nativeint xcoeff, nativeint ycoeff)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glViewportSwizzleNV(nativeint index, nativeint swizzlex, nativeint swizzley, nativeint swizzlez, nativeint swizzlew)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWaitSemaphoreEXT(nativeint semaphore, nativeint numBufferBarriers, nativeint* buffers, nativeint numTextureBarriers, nativeint* textures, nativeint* srcLayouts)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWaitSync(nativeint sync, nativeint flags, nativeint timeout)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWaitSyncAPPLE(nativeint sync, nativeint flags, nativeint timeout)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightPathsNV(nativeint resultPath, nativeint numPaths, nativeint* paths, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightPointerARB(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightPointerOES(nativeint size, nativeint _type, nativeint stride, nativeint pointer)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightbvARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightdvARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightfvARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightivARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightsvARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightubvARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightuivARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWeightusvARB(nativeint size, nativeint* weights)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2d(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2dARB(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2dMESA(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2dvARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2dvMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2f(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2fARB(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2fMESA(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2fvARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2fvMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2i(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2iARB(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2iMESA(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2ivARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2ivMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2s(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2sARB(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2sMESA(nativeint x, nativeint y)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2svARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos2svMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3d(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3dARB(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3dMESA(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3dv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3dvARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3dvMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3f(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3fARB(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3fMESA(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3fv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3fvARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3fvMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3i(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3iARB(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3iMESA(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3iv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3ivARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3ivMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3s(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3sARB(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3sMESA(nativeint x, nativeint y, nativeint z)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3sv(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3svARB(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos3svMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4dMESA(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4dvMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4fMESA(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4fvMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4iMESA(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4ivMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4sMESA(nativeint x, nativeint y, nativeint z, nativeint w)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowPos4svMESA(nativeint* v)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWindowRectanglesEXT(nativeint mode, nativeint count, nativeint* box)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWriteMaskEXT(nativeint res, nativeint _in, nativeint outX, nativeint outY, nativeint outZ, nativeint outW)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glDrawVkImageNV(nativeint vkImage, nativeint sampler, nativeint x0, nativeint y0, nativeint x1, nativeint y1, nativeint z, nativeint s0, nativeint t0, nativeint s1, nativeint t1)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern nativeint glGetVkProcAddrNV(nativeint* name)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glWaitVkSemaphoreNV(nativeint vkSemaphore)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSignalVkSemaphoreNV(nativeint vkSemaphore)
//    [<DllImport(lib);SuppressUnmanagedCodeSecurity>]
//    extern void glSignalVkFenceNV(nativeint vkFence)
