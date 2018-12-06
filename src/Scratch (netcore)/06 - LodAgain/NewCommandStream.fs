namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open OpenTK
open OpenTK.Graphics.OpenGL4

#nowarn "9"
#nowarn "44"


type InstructionCode = 
    | ActiveProgram = 0
    | ActiveShaderProgram = 1
    | ActiveTexture = 2
    | AttachShader = 3
    | BeginConditionalRender = 4
    | BeginQuery = 5
    | BeginQueryIndexed = 6
    | BeginTransformFeedback = 7
    | BindBuffer = 8
    | BindBufferBase = 9
    | BindBufferRange = 10
    | BindBuffersBase = 11
    | BindBuffersRange = 12
    | BindFramebuffer = 13
    | BindImageTexture = 14
    | BindImageTextures = 15
    | BindMultiTexture = 16
    | BindProgramPipeline = 17
    | BindRenderbuffer = 18
    | BindSampler = 19
    | BindSamplers = 20
    | BindTexture = 21
    | BindTextureUnit = 22
    | BindTextures = 23
    | BindTransformFeedback = 24
    | BindVertexArray = 25
    | BindVertexBuffer = 26
    | BindVertexBuffers = 27
    | BlendColor = 28
    | BlendEquation = 29
    | BlendEquationSeparate = 30
    | BlendFunc = 31
    | BlendFuncSeparate = 32
    | BlitFramebuffer = 33
    | BlitNamedFramebuffer = 34
    | BufferData = 35
    | BufferPageCommitment = 36
    | BufferStorage = 37
    | BufferSubData = 38
    | ClampColor = 39
    | Clear = 40
    | ClearBuffer = 41
    | ClearBufferData = 42
    | ClearBufferSubData = 43
    | ClearColor = 44
    | ClearDepth = 45
    | ClearNamedBufferData = 46
    | ClearNamedBufferSubData = 47
    | ClearNamedFramebuffer = 48
    | ClearStencil = 49
    | ClearTexImage = 50
    | ClearTexSubImage = 51
    | ClientAttribDefault = 52
    | ClipControl = 53
    | ColorMask = 54
    | ColorP3 = 55
    | ColorP4 = 56
    | ColorSubTable = 57
    | ColorTable = 58
    | ColorTableParameter = 59
    | CompileShader = 60
    | CompressedMultiTexImage1D = 61
    | CompressedMultiTexImage2D = 62
    | CompressedMultiTexImage3D = 63
    | CompressedMultiTexSubImage1D = 64
    | CompressedMultiTexSubImage2D = 65
    | CompressedMultiTexSubImage3D = 66
    | CompressedTexImage1D = 67
    | CompressedTexImage2D = 68
    | CompressedTexImage3D = 69
    | CompressedTexSubImage1D = 70
    | CompressedTexSubImage2D = 71
    | CompressedTexSubImage3D = 72
    | CompressedTextureImage1D = 73
    | CompressedTextureImage2D = 74
    | CompressedTextureImage3D = 75
    | CompressedTextureSubImage1D = 76
    | CompressedTextureSubImage2D = 77
    | CompressedTextureSubImage3D = 78
    | ConvolutionFilter1D = 79
    | ConvolutionFilter2D = 80
    | ConvolutionParameter = 81
    | CopyBufferSubData = 82
    | CopyColorSubTable = 83
    | CopyColorTable = 84
    | CopyConvolutionFilter1D = 85
    | CopyConvolutionFilter2D = 86
    | CopyImageSubData = 87
    | CopyMultiTexImage1D = 88
    | CopyMultiTexImage2D = 89
    | CopyMultiTexSubImage1D = 90
    | CopyMultiTexSubImage2D = 91
    | CopyMultiTexSubImage3D = 92
    | CopyNamedBufferSubData = 93
    | CopyTexImage1D = 94
    | CopyTexImage2D = 95
    | CopyTexSubImage1D = 96
    | CopyTexSubImage2D = 97
    | CopyTexSubImage3D = 98
    | CopyTextureImage1D = 99
    | CopyTextureImage2D = 100
    | CopyTextureSubImage1D = 101
    | CopyTextureSubImage2D = 102
    | CopyTextureSubImage3D = 103
    | CreateBuffers = 104
    | CreateFramebuffers = 105
    | CreateProgramPipelines = 106
    | CreateQueries = 107
    | CreateRenderbuffers = 108
    | CreateSamplers = 109
    | CreateTextures = 110
    | CreateTransformFeedbacks = 111
    | CreateVertexArrays = 112
    | CullFace = 113
    | DebugMessageControl = 114
    | DeleteBuffer = 115
    | DeleteBuffers = 116
    | DeleteFramebuffer = 117
    | DeleteFramebuffers = 118
    | DeleteProgram = 119
    | DeleteProgramPipeline = 120
    | DeleteProgramPipelines = 121
    | DeleteQueries = 122
    | DeleteQuery = 123
    | DeleteRenderbuffer = 124
    | DeleteRenderbuffers = 125
    | DeleteSampler = 126
    | DeleteSamplers = 127
    | DeleteShader = 128
    | DeleteSync = 129
    | DeleteTexture = 130
    | DeleteTextures = 131
    | DeleteTransformFeedback = 132
    | DeleteTransformFeedbacks = 133
    | DeleteVertexArray = 134
    | DeleteVertexArrays = 135
    | DepthFunc = 136
    | DepthMask = 137
    | DepthRange = 138
    | DepthRangeArray = 139
    | DepthRangeIndexed = 140
    | DetachShader = 141
    | Disable = 142
    | DisableClientState = 143
    | DisableClientStateIndexed = 144
    | DisableIndexed = 145
    | DisableVertexArray = 146
    | DisableVertexArrayAttrib = 147
    | DisableVertexAttribArray = 148
    | DispatchCompute = 149
    | DispatchComputeGroupSize = 150
    | DispatchComputeIndirect = 151
    | DrawArrays = 152
    | DrawArraysIndirect = 153
    | DrawArraysInstanced = 154
    | DrawArraysInstancedBaseInstance = 155
    | DrawBuffer = 156
    | DrawBuffers = 157
    | DrawElements = 158
    | DrawElementsBaseVertex = 159
    | DrawElementsIndirect = 160
    | DrawElementsInstanced = 161
    | DrawElementsInstancedBaseInstance = 162
    | DrawElementsInstancedBaseVertex = 163
    | DrawElementsInstancedBaseVertexBaseInstance = 164
    | DrawRangeElements = 165
    | DrawRangeElementsBaseVertex = 166
    | DrawTransformFeedback = 167
    | DrawTransformFeedbackInstanced = 168
    | DrawTransformFeedbackStream = 169
    | DrawTransformFeedbackStreamInstanced = 170
    | Enable = 171
    | EnableClientState = 172
    | EnableClientStateIndexed = 173
    | EnableIndexed = 174
    | EnableVertexArray = 175
    | EnableVertexArrayAttrib = 176
    | EnableVertexAttribArray = 177
    | EndConditionalRender = 178
    | EndQuery = 179
    | EndQueryIndexed = 180
    | EndTransformFeedback = 181
    | EvaluateDepthValues = 182
    | Finish = 183
    | Flush = 184
    | FlushMappedBufferRange = 185
    | FlushMappedNamedBufferRange = 186
    | FramebufferDrawBuffer = 187
    | FramebufferDrawBuffers = 188
    | FramebufferParameter = 189
    | FramebufferReadBuffer = 190
    | FramebufferRenderbuffer = 191
    | FramebufferSampleLocations = 192
    | FramebufferTexture = 193
    | FramebufferTexture1D = 194
    | FramebufferTexture2D = 195
    | FramebufferTexture3D = 196
    | FramebufferTextureFace = 197
    | FramebufferTextureLayer = 198
    | FrontFace = 199
    | GenBuffers = 200
    | GenFramebuffers = 201
    | GenProgramPipelines = 202
    | GenQueries = 203
    | GenRenderbuffers = 204
    | GenSamplers = 205
    | GenTextures = 206
    | GenTransformFeedbacks = 207
    | GenVertexArrays = 208
    | GenerateMipmap = 209
    | GenerateMultiTexMipmap = 210
    | GenerateTextureMipmap = 211
    | GetActiveAtomicCounterBuffer = 212
    | GetActiveSubroutineUniform = 213
    | GetActiveUniformBlock = 214
    | GetActiveUniforms = 215
    | GetAttachedShaders = 216
    | GetBoolean = 217
    | GetBooleanIndexed = 218
    | GetBufferParameter = 219
    | GetBufferPointer = 220
    | GetBufferSubData = 221
    | GetColorTable = 222
    | GetColorTableParameter = 223
    | GetCompressedMultiTexImage = 224
    | GetCompressedTexImage = 225
    | GetCompressedTextureImage = 226
    | GetCompressedTextureSubImage = 227
    | GetConvolutionFilter = 228
    | GetConvolutionParameter = 229
    | GetDouble = 230
    | GetDoubleIndexed = 231
    | GetFloat = 232
    | GetFloatIndexed = 233
    | GetFramebufferAttachmentParameter = 234
    | GetFramebufferParameter = 235
    | GetHistogram = 236
    | GetHistogramParameter = 237
    | GetInteger = 238
    | GetInteger64 = 239
    | GetIntegerIndexed = 240
    | GetInternalformat = 241
    | GetMinmax = 242
    | GetMinmaxParameter = 243
    | GetMultiTexEnv = 244
    | GetMultiTexGen = 245
    | GetMultiTexImage = 246
    | GetMultiTexLevelParameter = 247
    | GetMultiTexParameter = 248
    | GetMultiTexParameterI = 249
    | GetMultisample = 250
    | GetNamedBufferParameter = 251
    | GetNamedBufferPointer = 252
    | GetNamedBufferSubData = 253
    | GetNamedFramebufferAttachmentParameter = 254
    | GetNamedFramebufferParameter = 255
    | GetNamedProgram = 256
    | GetNamedProgramLocalParameter = 257
    | GetNamedProgramLocalParameterI = 258
    | GetNamedProgramString = 259
    | GetNamedRenderbufferParameter = 260
    | GetPointer = 261
    | GetPointerIndexed = 262
    | GetProgram = 263
    | GetProgramBinary = 264
    | GetProgramInterface = 265
    | GetProgramPipeline = 266
    | GetProgramResource = 267
    | GetProgramStage = 268
    | GetQuery = 269
    | GetQueryBufferObject = 270
    | GetQueryIndexed = 271
    | GetQueryObject = 272
    | GetRenderbufferParameter = 273
    | GetSamplerParameter = 274
    | GetSamplerParameterI = 275
    | GetSeparableFilter = 276
    | GetShader = 277
    | GetShaderPrecisionFormat = 278
    | GetSync = 279
    | GetTexImage = 280
    | GetTexLevelParameter = 281
    | GetTexParameter = 282
    | GetTexParameterI = 283
    | GetTextureImage = 284
    | GetTextureLevelParameter = 285
    | GetTextureParameter = 286
    | GetTextureParameterI = 287
    | GetTextureSubImage = 288
    | GetTransformFeedback = 289
    | GetTransformFeedbacki64_ = 290
    | GetUniform = 291
    | GetUniformSubroutine = 292
    | GetVertexArray = 293
    | GetVertexArrayIndexed = 294
    | GetVertexArrayIndexed64 = 295
    | GetVertexArrayInteger = 296
    | GetVertexArrayPointer = 297
    | GetVertexAttrib = 298
    | GetVertexAttribI = 299
    | GetVertexAttribL = 300
    | GetVertexAttribPointer = 301
    | GetnColorTable = 302
    | GetnCompressedTexImage = 303
    | GetnConvolutionFilter = 304
    | GetnHistogram = 305
    | GetnMap = 306
    | GetnMinmax = 307
    | GetnPixelMap = 308
    | GetnPolygonStipple = 309
    | GetnSeparableFilter = 310
    | GetnTexImage = 311
    | GetnUniform = 312
    | Hint = 313
    | Histogram = 314
    | InvalidateBufferData = 315
    | InvalidateBufferSubData = 316
    | InvalidateFramebuffer = 317
    | InvalidateNamedFramebufferData = 318
    | InvalidateNamedFramebufferSubData = 319
    | InvalidateSubFramebuffer = 320
    | InvalidateTexImage = 321
    | InvalidateTexSubImage = 322
    | LineWidth = 323
    | LinkProgram = 324
    | LogicOp = 325
    | MakeImageHandleNonResident = 326
    | MakeImageHandleResident = 327
    | MakeTextureHandleNonResident = 328
    | MakeTextureHandleResident = 329
    | MatrixFrustum = 330
    | MatrixLoad = 331
    | MatrixLoadIdentity = 332
    | MatrixLoadTranspose = 333
    | MatrixMult = 334
    | MatrixMultTranspose = 335
    | MatrixOrtho = 336
    | MatrixPop = 337
    | MatrixPush = 338
    | MatrixRotate = 339
    | MatrixScale = 340
    | MatrixTranslate = 341
    | MaxShaderCompilerThreads = 342
    | MemoryBarrier = 343
    | MemoryBarrierByRegion = 344
    | MinSampleShading = 345
    | Minmax = 346
    | MultiDrawArrays = 347
    | MultiDrawArraysIndirect = 348
    | MultiDrawArraysIndirectCount = 349
    | MultiDrawElements = 350
    | MultiDrawElementsBaseVertex = 351
    | MultiDrawElementsIndirect = 352
    | MultiDrawElementsIndirectCount = 353
    | MultiTexBuffer = 354
    | MultiTexCoordP1 = 355
    | MultiTexCoordP2 = 356
    | MultiTexCoordP3 = 357
    | MultiTexCoordP4 = 358
    | MultiTexCoordPointer = 359
    | MultiTexEnv = 360
    | MultiTexGen = 361
    | MultiTexGend = 362
    | MultiTexImage1D = 363
    | MultiTexImage2D = 364
    | MultiTexImage3D = 365
    | MultiTexParameter = 366
    | MultiTexParameterI = 367
    | MultiTexRenderbuffer = 368
    | MultiTexSubImage1D = 369
    | MultiTexSubImage2D = 370
    | MultiTexSubImage3D = 371
    | NamedBufferData = 372
    | NamedBufferPageCommitment = 373
    | NamedBufferStorage = 374
    | NamedBufferSubData = 375
    | NamedCopyBufferSubData = 376
    | NamedFramebufferDrawBuffer = 377
    | NamedFramebufferDrawBuffers = 378
    | NamedFramebufferParameter = 379
    | NamedFramebufferReadBuffer = 380
    | NamedFramebufferRenderbuffer = 381
    | NamedFramebufferSampleLocations = 382
    | NamedFramebufferTexture = 383
    | NamedFramebufferTexture1D = 384
    | NamedFramebufferTexture2D = 385
    | NamedFramebufferTexture3D = 386
    | NamedFramebufferTextureFace = 387
    | NamedFramebufferTextureLayer = 388
    | NamedProgramLocalParameter4 = 389
    | NamedProgramLocalParameterI4 = 390
    | NamedProgramLocalParameters4 = 391
    | NamedProgramLocalParametersI4 = 392
    | NamedProgramString = 393
    | NamedRenderbufferStorage = 394
    | NamedRenderbufferStorageMultisample = 395
    | NamedRenderbufferStorageMultisampleCoverage = 396
    | NormalP3 = 397
    | PatchParameter = 398
    | PauseTransformFeedback = 399
    | PixelStore = 400
    | PointParameter = 401
    | PointSize = 402
    | PolygonMode = 403
    | PolygonOffset = 404
    | PolygonOffsetClamp = 405
    | PopDebugGroup = 406
    | PopGroupMarker = 407
    | PrimitiveBoundingBox = 408
    | PrimitiveRestartIndex = 409
    | ProgramBinary = 410
    | ProgramParameter = 411
    | ProgramUniform1 = 412
    | ProgramUniform2 = 413
    | ProgramUniform3 = 414
    | ProgramUniform4 = 415
    | ProgramUniformHandle = 416
    | ProgramUniformMatrix2 = 417
    | ProgramUniformMatrix2x3 = 418
    | ProgramUniformMatrix2x4 = 419
    | ProgramUniformMatrix3 = 420
    | ProgramUniformMatrix3x2 = 421
    | ProgramUniformMatrix3x4 = 422
    | ProgramUniformMatrix4 = 423
    | ProgramUniformMatrix4x2 = 424
    | ProgramUniformMatrix4x3 = 425
    | ProvokingVertex = 426
    | PushClientAttribDefault = 427
    | QueryCounter = 428
    | RasterSamples = 429
    | ReadBuffer = 430
    | ReadPixels = 431
    | ReadnPixels = 432
    | ReleaseShaderCompiler = 433
    | RenderbufferStorage = 434
    | RenderbufferStorageMultisample = 435
    | ResetHistogram = 436
    | ResetMinmax = 437
    | ResumeTransformFeedback = 438
    | SampleCoverage = 439
    | SampleMask = 440
    | SamplerParameter = 441
    | SamplerParameterI = 442
    | Scissor = 443
    | ScissorArray = 444
    | ScissorIndexed = 445
    | SecondaryColorP3 = 446
    | SeparableFilter2D = 447
    | ShaderBinary = 448
    | ShaderStorageBlockBinding = 449
    | StencilFunc = 450
    | StencilFuncSeparate = 451
    | StencilMask = 452
    | StencilMaskSeparate = 453
    | StencilOp = 454
    | StencilOpSeparate = 455
    | TexBuffer = 456
    | TexBufferRange = 457
    | TexCoordP1 = 458
    | TexCoordP2 = 459
    | TexCoordP3 = 460
    | TexCoordP4 = 461
    | TexImage1D = 462
    | TexImage2D = 463
    | TexImage2DMultisample = 464
    | TexImage3D = 465
    | TexImage3DMultisample = 466
    | TexPageCommitment = 467
    | TexParameter = 468
    | TexParameterI = 469
    | TexStorage1D = 470
    | TexStorage2D = 471
    | TexStorage2DMultisample = 472
    | TexStorage3D = 473
    | TexStorage3DMultisample = 474
    | TexSubImage1D = 475
    | TexSubImage2D = 476
    | TexSubImage3D = 477
    | TextureBarrier = 478
    | TextureBuffer = 479
    | TextureBufferRange = 480
    | TextureImage1D = 481
    | TextureImage2D = 482
    | TextureImage3D = 483
    | TexturePageCommitment = 484
    | TextureParameter = 485
    | TextureParameterI = 486
    | TextureRenderbuffer = 487
    | TextureStorage1D = 488
    | TextureStorage2D = 489
    | TextureStorage2DMultisample = 490
    | TextureStorage3D = 491
    | TextureStorage3DMultisample = 492
    | TextureSubImage1D = 493
    | TextureSubImage2D = 494
    | TextureSubImage3D = 495
    | TextureView = 496
    | TransformFeedbackBufferBase = 497
    | TransformFeedbackBufferRange = 498
    | Uniform1 = 499
    | Uniform2 = 500
    | Uniform3 = 501
    | Uniform4 = 502
    | UniformBlockBinding = 503
    | UniformHandle = 504
    | UniformMatrix2 = 505
    | UniformMatrix2x3 = 506
    | UniformMatrix2x4 = 507
    | UniformMatrix3 = 508
    | UniformMatrix3x2 = 509
    | UniformMatrix3x4 = 510
    | UniformMatrix4 = 511
    | UniformMatrix4x2 = 512
    | UniformMatrix4x3 = 513
    | UniformSubroutines = 514
    | UseProgram = 515
    | UseProgramStages = 516
    | UseShaderProgram = 517
    | ValidateProgram = 518
    | ValidateProgramPipeline = 519
    | VertexArrayAttribBinding = 520
    | VertexArrayAttribFormat = 521
    | VertexArrayAttribIFormat = 522
    | VertexArrayAttribLFormat = 523
    | VertexArrayBindVertexBuffer = 524
    | VertexArrayBindingDivisor = 525
    | VertexArrayColorOffset = 526
    | VertexArrayEdgeFlagOffset = 527
    | VertexArrayElementBuffer = 528
    | VertexArrayFogCoordOffset = 529
    | VertexArrayIndexOffset = 530
    | VertexArrayMultiTexCoordOffset = 531
    | VertexArrayNormalOffset = 532
    | VertexArraySecondaryColorOffset = 533
    | VertexArrayTexCoordOffset = 534
    | VertexArrayVertexAttribBinding = 535
    | VertexArrayVertexAttribDivisor = 536
    | VertexArrayVertexAttribFormat = 537
    | VertexArrayVertexAttribIFormat = 538
    | VertexArrayVertexAttribIOffset = 539
    | VertexArrayVertexAttribLFormat = 540
    | VertexArrayVertexAttribLOffset = 541
    | VertexArrayVertexAttribOffset = 542
    | VertexArrayVertexBindingDivisor = 543
    | VertexArrayVertexBuffer = 544
    | VertexArrayVertexBuffers = 545
    | VertexArrayVertexOffset = 546
    | VertexAttrib1 = 547
    | VertexAttrib2 = 548
    | VertexAttrib3 = 549
    | VertexAttrib4 = 550
    | VertexAttrib4N = 551
    | VertexAttribBinding = 552
    | VertexAttribDivisor = 553
    | VertexAttribFormat = 554
    | VertexAttribI1 = 555
    | VertexAttribI2 = 556
    | VertexAttribI3 = 557
    | VertexAttribI4 = 558
    | VertexAttribIFormat = 559
    | VertexAttribIPointer = 560
    | VertexAttribL1 = 561
    | VertexAttribL2 = 562
    | VertexAttribL3 = 563
    | VertexAttribL4 = 564
    | VertexAttribLFormat = 565
    | VertexAttribLPointer = 566
    | VertexAttribP1 = 567
    | VertexAttribP2 = 568
    | VertexAttribP3 = 569
    | VertexAttribP4 = 570
    | VertexAttribPointer = 571
    | VertexBindingDivisor = 572
    | VertexP2 = 573
    | VertexP3 = 574
    | VertexP4 = 575
    | Viewport = 576
    | ViewportArray = 577
    | ViewportIndexed = 578
    | WaitSync = 579
    | WindowRectangles = 580


type ICommandStream =
    inherit IDisposable
    abstract member ActiveProgram : program : int -> unit
    abstract member ActiveShaderProgram : pipeline : int * program : int -> unit
    abstract member ActiveTexture : texture : TextureUnit -> unit
    abstract member AttachShader : program : int * shader : int -> unit
    abstract member BeginConditionalRender : id : int * mode : ConditionalRenderType -> unit
    abstract member BeginQuery : target : QueryTarget * id : int -> unit
    abstract member BeginQueryIndexed : target : QueryTarget * index : int * id : int -> unit
    abstract member BeginTransformFeedback : primitiveMode : TransformFeedbackPrimitiveType -> unit
    abstract member BindBuffer : target : BufferTarget * buffer : int -> unit
    abstract member BindBufferBase : target : BufferRangeTarget * index : int * buffer : int -> unit
    abstract member BindBufferRange : target : BufferRangeTarget * index : int * buffer : int * offset : nativeint * size : int -> unit
    abstract member BindBuffersBase : target : BufferRangeTarget * first : int * count : int * buffers : nativeptr<int> -> unit
    abstract member BindBuffersRange : target : BufferRangeTarget * first : int * count : int * buffers : nativeptr<int> * offsets : nativeptr<nativeint> * sizes : nativeptr<nativeint> -> unit
    abstract member BindFramebuffer : target : FramebufferTarget * framebuffer : int -> unit
    abstract member BindImageTexture : unit : int * texture : int * level : int * layered : bool * layer : int * access : TextureAccess * format : SizedInternalFormat -> unit
    abstract member BindImageTextures : first : int * count : int * textures : nativeptr<int> -> unit
    abstract member BindMultiTexture : texunit : TextureUnit * target : TextureTarget * texture : int -> unit
    abstract member BindProgramPipeline : pipeline : int -> unit
    abstract member BindRenderbuffer : target : RenderbufferTarget * renderbuffer : int -> unit
    abstract member BindSampler : unit : int * sampler : int -> unit
    abstract member BindSamplers : first : int * count : int * samplers : nativeptr<int> -> unit
    abstract member BindTexture : target : TextureTarget * texture : int -> unit
    abstract member BindTextureUnit : unit : int * texture : int -> unit
    abstract member BindTextures : first : int * count : int * textures : nativeptr<int> -> unit
    abstract member BindTransformFeedback : target : TransformFeedbackTarget * id : int -> unit
    abstract member BindVertexArray : array : int -> unit
    abstract member BindVertexBuffer : bindingindex : int * buffer : int * offset : nativeint * stride : int -> unit
    abstract member BindVertexBuffers : first : int * count : int * buffers : nativeptr<int> * offsets : nativeptr<nativeint> * strides : nativeptr<int> -> unit
    abstract member BlendColor : red : float32 * green : float32 * blue : float32 * alpha : float32 -> unit
    abstract member BlendEquation : buf : int * mode : BlendEquationMode -> unit
    abstract member BlendEquationSeparate : buf : int * modeRGB : BlendEquationMode * modeAlpha : BlendEquationMode -> unit
    abstract member BlendFunc : buf : int * src : BlendingFactorSrc * dst : BlendingFactorDest -> unit
    abstract member BlendFuncSeparate : buf : int * srcRGB : BlendingFactorSrc * dstRGB : BlendingFactorDest * srcAlpha : BlendingFactorSrc * dstAlpha : BlendingFactorDest -> unit
    abstract member BlitFramebuffer : srcX0 : int * srcY0 : int * srcX1 : int * srcY1 : int * dstX0 : int * dstY0 : int * dstX1 : int * dstY1 : int * mask : ClearBufferMask * filter : BlitFramebufferFilter -> unit
    abstract member BlitNamedFramebuffer : readFramebuffer : int * drawFramebuffer : int * srcX0 : int * srcY0 : int * srcX1 : int * srcY1 : int * dstX0 : int * dstY0 : int * dstX1 : int * dstY1 : int * mask : ClearBufferMask * filter : BlitFramebufferFilter -> unit
    abstract member BufferData : target : BufferTarget * size : int * data : nativeint * usage : BufferUsageHint -> unit
    abstract member BufferPageCommitment : target : All * offset : nativeint * size : int * commit : bool -> unit
    abstract member BufferStorage : target : BufferTarget * size : int * data : nativeint * flags : BufferStorageFlags -> unit
    abstract member BufferSubData : target : BufferTarget * offset : nativeint * size : int * data : nativeint -> unit
    abstract member ClampColor : target : ClampColorTarget * clamp : ClampColorMode -> unit
    abstract member Clear : mask : ClearBufferMask -> unit
    abstract member ClearBuffer : buffer : ClearBufferCombined * drawbuffer : int * depth : float32 * stencil : int -> unit
    abstract member ClearBufferData : target : BufferTarget * internalformat : PixelInternalFormat * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ClearBufferSubData : target : BufferTarget * internalformat : PixelInternalFormat * offset : nativeint * size : int * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ClearColor : red : float32 * green : float32 * blue : float32 * alpha : float32 -> unit
    abstract member ClearDepth : depth : float -> unit
    abstract member ClearNamedBufferData : buffer : int * internalformat : PixelInternalFormat * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ClearNamedBufferSubData : buffer : int * internalformat : PixelInternalFormat * offset : nativeint * size : int * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ClearNamedFramebuffer : framebuffer : int * buffer : ClearBufferCombined * drawbuffer : int * depth : float32 * stencil : int -> unit
    abstract member ClearStencil : s : int -> unit
    abstract member ClearTexImage : texture : int * level : int * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ClearTexSubImage : texture : int * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ClientAttribDefault : mask : ClientAttribMask -> unit
    abstract member ClipControl : origin : ClipOrigin * depth : ClipDepthMode -> unit
    abstract member ColorMask : index : int * r : bool * g : bool * b : bool * a : bool -> unit
    abstract member ColorP3 : _type : PackedPointerType * color : int -> unit
    abstract member ColorP4 : _type : PackedPointerType * color : int -> unit
    abstract member ColorSubTable : target : ColorTableTarget * start : int * count : int * format : PixelFormat * _type : PixelType * data : nativeint -> unit
    abstract member ColorTable : target : ColorTableTarget * internalformat : InternalFormat * width : int * format : PixelFormat * _type : PixelType * table : nativeint -> unit
    abstract member ColorTableParameter : target : ColorTableTarget * pname : ColorTableParameterPNameSgi * _params : nativeptr<float32> -> unit
    abstract member CompileShader : shader : int -> unit
    abstract member CompressedMultiTexImage1D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * border : int * imageSize : int * bits : nativeint -> unit
    abstract member CompressedMultiTexImage2D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * border : int * imageSize : int * bits : nativeint -> unit
    abstract member CompressedMultiTexImage3D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * depth : int * border : int * imageSize : int * bits : nativeint -> unit
    abstract member CompressedMultiTexSubImage1D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * width : int * format : PixelFormat * imageSize : int * bits : nativeint -> unit
    abstract member CompressedMultiTexSubImage2D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * yoffset : int * width : int * height : int * format : PixelFormat * imageSize : int * bits : nativeint -> unit
    abstract member CompressedMultiTexSubImage3D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * imageSize : int * bits : nativeint -> unit
    abstract member CompressedTexImage1D : target : TextureTarget * level : int * internalformat : InternalFormat * width : int * border : int * imageSize : int * data : nativeint -> unit
    abstract member CompressedTexImage2D : target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * border : int * imageSize : int * data : nativeint -> unit
    abstract member CompressedTexImage3D : target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * depth : int * border : int * imageSize : int * data : nativeint -> unit
    abstract member CompressedTexSubImage1D : target : TextureTarget * level : int * xoffset : int * width : int * format : PixelFormat * imageSize : int * data : nativeint -> unit
    abstract member CompressedTexSubImage2D : target : TextureTarget * level : int * xoffset : int * yoffset : int * width : int * height : int * format : PixelFormat * imageSize : int * data : nativeint -> unit
    abstract member CompressedTexSubImage3D : target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * imageSize : int * data : nativeint -> unit
    abstract member CompressedTextureImage1D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * border : int * imageSize : int * bits : nativeint -> unit
    abstract member CompressedTextureImage2D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * border : int * imageSize : int * bits : nativeint -> unit
    abstract member CompressedTextureImage3D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * depth : int * border : int * imageSize : int * bits : nativeint -> unit
    abstract member CompressedTextureSubImage1D : texture : int * target : TextureTarget * level : int * xoffset : int * width : int * format : PixelFormat * imageSize : int * bits : nativeint -> unit
    abstract member CompressedTextureSubImage2D : texture : int * target : TextureTarget * level : int * xoffset : int * yoffset : int * width : int * height : int * format : PixelFormat * imageSize : int * bits : nativeint -> unit
    abstract member CompressedTextureSubImage3D : texture : int * target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * imageSize : int * bits : nativeint -> unit
    abstract member ConvolutionFilter1D : target : ConvolutionTarget * internalformat : InternalFormat * width : int * format : PixelFormat * _type : PixelType * image : nativeint -> unit
    abstract member ConvolutionFilter2D : target : ConvolutionTarget * internalformat : InternalFormat * width : int * height : int * format : PixelFormat * _type : PixelType * image : nativeint -> unit
    abstract member ConvolutionParameter : target : ConvolutionTarget * pname : ConvolutionParameterExt * _params : float32 -> unit
    abstract member CopyBufferSubData : readTarget : BufferTarget * writeTarget : BufferTarget * readOffset : nativeint * writeOffset : nativeint * size : int -> unit
    abstract member CopyColorSubTable : target : ColorTableTarget * start : int * _x : int * y : int * width : int -> unit
    abstract member CopyColorTable : target : ColorTableTarget * internalformat : InternalFormat * _x : int * y : int * width : int -> unit
    abstract member CopyConvolutionFilter1D : target : ConvolutionTarget * internalformat : InternalFormat * _x : int * y : int * width : int -> unit
    abstract member CopyConvolutionFilter2D : target : ConvolutionTarget * internalformat : InternalFormat * _x : int * y : int * width : int * height : int -> unit
    abstract member CopyImageSubData : srcName : int * srcTarget : ImageTarget * srcLevel : int * srcX : int * srcY : int * srcZ : int * dstName : int * dstTarget : ImageTarget * dstLevel : int * dstX : int * dstY : int * dstZ : int * srcWidth : int * srcHeight : int * srcDepth : int -> unit
    abstract member CopyMultiTexImage1D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * _x : int * y : int * width : int * border : int -> unit
    abstract member CopyMultiTexImage2D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * _x : int * y : int * width : int * height : int * border : int -> unit
    abstract member CopyMultiTexSubImage1D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * _x : int * y : int * width : int -> unit
    abstract member CopyMultiTexSubImage2D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * yoffset : int * _x : int * y : int * width : int * height : int -> unit
    abstract member CopyMultiTexSubImage3D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * _x : int * y : int * width : int * height : int -> unit
    abstract member CopyNamedBufferSubData : readBuffer : int * writeBuffer : int * readOffset : nativeint * writeOffset : nativeint * size : int -> unit
    abstract member CopyTexImage1D : target : TextureTarget * level : int * internalformat : InternalFormat * _x : int * y : int * width : int * border : int -> unit
    abstract member CopyTexImage2D : target : TextureTarget * level : int * internalformat : InternalFormat * _x : int * y : int * width : int * height : int * border : int -> unit
    abstract member CopyTexSubImage1D : target : TextureTarget * level : int * xoffset : int * _x : int * y : int * width : int -> unit
    abstract member CopyTexSubImage2D : target : TextureTarget * level : int * xoffset : int * yoffset : int * _x : int * y : int * width : int * height : int -> unit
    abstract member CopyTexSubImage3D : target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * _x : int * y : int * width : int * height : int -> unit
    abstract member CopyTextureImage1D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * _x : int * y : int * width : int * border : int -> unit
    abstract member CopyTextureImage2D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * _x : int * y : int * width : int * height : int * border : int -> unit
    abstract member CopyTextureSubImage1D : texture : int * target : TextureTarget * level : int * xoffset : int * _x : int * y : int * width : int -> unit
    abstract member CopyTextureSubImage2D : texture : int * target : TextureTarget * level : int * xoffset : int * yoffset : int * _x : int * y : int * width : int * height : int -> unit
    abstract member CopyTextureSubImage3D : texture : int * target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * _x : int * y : int * width : int * height : int -> unit
    abstract member CreateBuffers : n : int * buffers : nativeptr<int> -> unit
    abstract member CreateFramebuffers : n : int * framebuffers : nativeptr<uint32> -> unit
    abstract member CreateProgramPipelines : n : int * pipelines : nativeptr<int> -> unit
    abstract member CreateQueries : target : QueryTarget * n : int * ids : nativeptr<int> -> unit
    abstract member CreateRenderbuffers : n : int * renderbuffers : nativeptr<int> -> unit
    abstract member CreateSamplers : n : int * samplers : nativeptr<int> -> unit
    abstract member CreateTextures : target : TextureTarget * n : int * textures : nativeptr<int> -> unit
    abstract member CreateTransformFeedbacks : n : int * ids : nativeptr<int> -> unit
    abstract member CreateVertexArrays : n : int * arrays : nativeptr<int> -> unit
    abstract member CullFace : mode : CullFaceMode -> unit
    abstract member DebugMessageControl : source : DebugSourceControl * _type : DebugTypeControl * severity : DebugSeverityControl * count : int * ids : nativeptr<int> * enabled : bool -> unit
    abstract member DeleteBuffer : buffers : int -> unit
    abstract member DeleteBuffers : n : int * buffers : nativeptr<int> -> unit
    abstract member DeleteFramebuffer : framebuffers : int -> unit
    abstract member DeleteFramebuffers : n : int * framebuffers : nativeptr<int> -> unit
    abstract member DeleteProgram : program : int -> unit
    abstract member DeleteProgramPipeline : pipelines : int -> unit
    abstract member DeleteProgramPipelines : n : int * pipelines : nativeptr<int> -> unit
    abstract member DeleteQueries : n : int * ids : nativeptr<int> -> unit
    abstract member DeleteQuery : ids : int -> unit
    abstract member DeleteRenderbuffer : renderbuffers : int -> unit
    abstract member DeleteRenderbuffers : n : int * renderbuffers : nativeptr<int> -> unit
    abstract member DeleteSampler : samplers : int -> unit
    abstract member DeleteSamplers : count : int * samplers : nativeptr<int> -> unit
    abstract member DeleteShader : shader : int -> unit
    abstract member DeleteSync : sync : nativeint -> unit
    abstract member DeleteTexture : textures : int -> unit
    abstract member DeleteTextures : n : int * textures : nativeptr<int> -> unit
    abstract member DeleteTransformFeedback : ids : int -> unit
    abstract member DeleteTransformFeedbacks : n : int * ids : nativeptr<int> -> unit
    abstract member DeleteVertexArray : arrays : int -> unit
    abstract member DeleteVertexArrays : n : int * arrays : nativeptr<int> -> unit
    abstract member DepthFunc : func : DepthFunction -> unit
    abstract member DepthMask : flag : bool -> unit
    abstract member DepthRange : near : float * far : float -> unit
    abstract member DepthRangeArray : first : int * count : int * v : nativeptr<float> -> unit
    abstract member DepthRangeIndexed : index : int * n : float * f : float -> unit
    abstract member DetachShader : program : int * shader : int -> unit
    abstract member Disable : target : IndexedEnableCap * index : int -> unit
    abstract member DisableClientState : array : ArrayCap * index : int -> unit
    abstract member DisableClientStateIndexed : array : ArrayCap * index : int -> unit
    abstract member DisableIndexed : target : IndexedEnableCap * index : int -> unit
    abstract member DisableVertexArray : vaobj : int * array : EnableCap -> unit
    abstract member DisableVertexArrayAttrib : vaobj : int * index : int -> unit
    abstract member DisableVertexAttribArray : index : int -> unit
    abstract member DispatchCompute : num_groups_x : int * num_groups_y : int * num_groups_z : int -> unit
    abstract member DispatchComputeGroupSize : num_groups_x : int * num_groups_y : int * num_groups_z : int * group_size_x : int * group_size_y : int * group_size_z : int -> unit
    abstract member DispatchComputeIndirect : indirect : nativeint -> unit
    abstract member DrawArrays : mode : PrimitiveType * first : int * count : int -> unit
    abstract member DrawArraysIndirect : mode : PrimitiveType * indirect : nativeint -> unit
    abstract member DrawArraysInstanced : mode : PrimitiveType * first : int * count : int * instancecount : int -> unit
    abstract member DrawArraysInstancedBaseInstance : mode : PrimitiveType * first : int * count : int * instancecount : int * baseinstance : int -> unit
    abstract member DrawBuffer : buf : DrawBufferMode -> unit
    abstract member DrawBuffers : n : int * bufs : nativeptr<DrawBuffersEnum> -> unit
    abstract member DrawElements : mode : BeginMode * count : int * _type : DrawElementsType * offset : int -> unit
    abstract member DrawElementsBaseVertex : mode : PrimitiveType * count : int * _type : DrawElementsType * indices : nativeint * basevertex : int -> unit
    abstract member DrawElementsIndirect : mode : PrimitiveType * _type : DrawElementsType * indirect : nativeint -> unit
    abstract member DrawElementsInstanced : mode : PrimitiveType * count : int * _type : DrawElementsType * indices : nativeint * instancecount : int -> unit
    abstract member DrawElementsInstancedBaseInstance : mode : PrimitiveType * count : int * _type : DrawElementsType * indices : nativeint * instancecount : int * baseinstance : int -> unit
    abstract member DrawElementsInstancedBaseVertex : mode : PrimitiveType * count : int * _type : DrawElementsType * indices : nativeint * instancecount : int * basevertex : int -> unit
    abstract member DrawElementsInstancedBaseVertexBaseInstance : mode : PrimitiveType * count : int * _type : DrawElementsType * indices : nativeint * instancecount : int * basevertex : int * baseinstance : int -> unit
    abstract member DrawRangeElements : mode : PrimitiveType * start : int * _end : int * count : int * _type : DrawElementsType * indices : nativeint -> unit
    abstract member DrawRangeElementsBaseVertex : mode : PrimitiveType * start : int * _end : int * count : int * _type : DrawElementsType * indices : nativeint * basevertex : int -> unit
    abstract member DrawTransformFeedback : mode : PrimitiveType * id : int -> unit
    abstract member DrawTransformFeedbackInstanced : mode : PrimitiveType * id : int * instancecount : int -> unit
    abstract member DrawTransformFeedbackStream : mode : PrimitiveType * id : int * stream : int -> unit
    abstract member DrawTransformFeedbackStreamInstanced : mode : PrimitiveType * id : int * stream : int * instancecount : int -> unit
    abstract member Enable : target : IndexedEnableCap * index : int -> unit
    abstract member EnableClientState : array : ArrayCap * index : int -> unit
    abstract member EnableClientStateIndexed : array : ArrayCap * index : int -> unit
    abstract member EnableIndexed : target : IndexedEnableCap * index : int -> unit
    abstract member EnableVertexArray : vaobj : int * array : EnableCap -> unit
    abstract member EnableVertexArrayAttrib : vaobj : int * index : int -> unit
    abstract member EnableVertexAttribArray : index : int -> unit
    abstract member EndConditionalRender : unit -> unit
    abstract member EndQuery : target : QueryTarget -> unit
    abstract member EndQueryIndexed : target : QueryTarget * index : int -> unit
    abstract member EndTransformFeedback : unit -> unit
    abstract member EvaluateDepthValues : unit -> unit
    abstract member Finish : unit -> unit
    abstract member Flush : unit -> unit
    abstract member FlushMappedBufferRange : target : BufferTarget * offset : nativeint * length : int -> unit
    abstract member FlushMappedNamedBufferRange : buffer : int * offset : nativeint * length : int -> unit
    abstract member FramebufferDrawBuffer : framebuffer : int * mode : DrawBufferMode -> unit
    abstract member FramebufferDrawBuffers : framebuffer : int * n : int * bufs : nativeptr<DrawBufferMode> -> unit
    abstract member FramebufferParameter : target : FramebufferTarget * pname : FramebufferDefaultParameter * param : int -> unit
    abstract member FramebufferReadBuffer : framebuffer : int * mode : ReadBufferMode -> unit
    abstract member FramebufferRenderbuffer : target : FramebufferTarget * attachment : FramebufferAttachment * renderbuffertarget : RenderbufferTarget * renderbuffer : int -> unit
    abstract member FramebufferSampleLocations : target : FramebufferTarget * start : int * count : int * v : nativeptr<float32> -> unit
    abstract member FramebufferTexture : target : FramebufferTarget * attachment : FramebufferAttachment * texture : int * level : int -> unit
    abstract member FramebufferTexture1D : target : FramebufferTarget * attachment : FramebufferAttachment * textarget : TextureTarget * texture : int * level : int -> unit
    abstract member FramebufferTexture2D : target : FramebufferTarget * attachment : FramebufferAttachment * textarget : TextureTarget * texture : int * level : int -> unit
    abstract member FramebufferTexture3D : target : FramebufferTarget * attachment : FramebufferAttachment * textarget : TextureTarget * texture : int * level : int * zoffset : int -> unit
    abstract member FramebufferTextureFace : target : FramebufferTarget * attachment : FramebufferAttachment * texture : int * level : int * face : TextureTarget -> unit
    abstract member FramebufferTextureLayer : target : FramebufferTarget * attachment : FramebufferAttachment * texture : int * level : int * layer : int -> unit
    abstract member FrontFace : mode : FrontFaceDirection -> unit
    abstract member GenBuffers : n : int * buffers : nativeptr<int> -> unit
    abstract member GenFramebuffers : n : int * framebuffers : nativeptr<int> -> unit
    abstract member GenProgramPipelines : n : int * pipelines : nativeptr<int> -> unit
    abstract member GenQueries : n : int * ids : nativeptr<int> -> unit
    abstract member GenRenderbuffers : n : int * renderbuffers : nativeptr<int> -> unit
    abstract member GenSamplers : count : int * samplers : nativeptr<int> -> unit
    abstract member GenTextures : n : int * textures : nativeptr<int> -> unit
    abstract member GenTransformFeedbacks : n : int * ids : nativeptr<int> -> unit
    abstract member GenVertexArrays : n : int * arrays : nativeptr<int> -> unit
    abstract member GenerateMipmap : target : GenerateMipmapTarget -> unit
    abstract member GenerateMultiTexMipmap : texunit : TextureUnit * target : TextureTarget -> unit
    abstract member GenerateTextureMipmap : texture : int * target : TextureTarget -> unit
    abstract member GetActiveAtomicCounterBuffer : program : int * bufferIndex : int * pname : AtomicCounterBufferParameter * _params : nativeptr<int> -> unit
    abstract member GetActiveSubroutineUniform : program : int * shadertype : ShaderType * index : int * pname : ActiveSubroutineUniformParameter * values : nativeptr<int> -> unit
    abstract member GetActiveUniformBlock : program : int * uniformBlockIndex : int * pname : ActiveUniformBlockParameter * _params : nativeptr<int> -> unit
    abstract member GetActiveUniforms : program : int * uniformCount : int * uniformIndices : nativeptr<int> * pname : ActiveUniformParameter * _params : nativeptr<int> -> unit
    abstract member GetAttachedShaders : program : int * maxCount : int * count : nativeptr<int> * shaders : nativeptr<int> -> unit
    abstract member GetBoolean : target : GetIndexedPName * index : int * data : nativeptr<bool> -> unit
    abstract member GetBooleanIndexed : target : BufferTargetArb * index : int * data : nativeptr<bool> -> unit
    abstract member GetBufferParameter : target : BufferTarget * pname : BufferParameterName * _params : nativeptr<int64> -> unit
    abstract member GetBufferPointer : target : BufferTarget * pname : BufferPointer * _params : nativeint -> unit
    abstract member GetBufferSubData : target : BufferTarget * offset : nativeint * size : int * data : nativeint -> unit
    abstract member GetColorTable : target : ColorTableTarget * format : PixelFormat * _type : PixelType * table : nativeint -> unit
    abstract member GetColorTableParameter : target : ColorTableTarget * pname : GetColorTableParameterPNameSgi * _params : nativeptr<float32> -> unit
    abstract member GetCompressedMultiTexImage : texunit : TextureUnit * target : TextureTarget * lod : int * img : nativeint -> unit
    abstract member GetCompressedTexImage : target : TextureTarget * level : int * img : nativeint -> unit
    abstract member GetCompressedTextureImage : texture : int * level : int * bufSize : int * pixels : nativeint -> unit
    abstract member GetCompressedTextureSubImage : texture : int * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * bufSize : int * pixels : nativeint -> unit
    abstract member GetConvolutionFilter : target : ConvolutionTarget * format : PixelFormat * _type : PixelType * image : nativeint -> unit
    abstract member GetConvolutionParameter : target : ConvolutionTarget * pname : ConvolutionParameterExt * _params : nativeptr<float32> -> unit
    abstract member GetDouble : target : GetIndexedPName * index : int * data : nativeptr<float> -> unit
    abstract member GetDoubleIndexed : target : TypeEnum * index : int * data : nativeptr<float> -> unit
    abstract member GetFloat : target : GetIndexedPName * index : int * data : nativeptr<float32> -> unit
    abstract member GetFloatIndexed : target : TypeEnum * index : int * data : nativeptr<float32> -> unit
    abstract member GetFramebufferAttachmentParameter : target : FramebufferTarget * attachment : FramebufferAttachment * pname : FramebufferParameterName * _params : nativeptr<int> -> unit
    abstract member GetFramebufferParameter : target : FramebufferTarget * pname : FramebufferDefaultParameter * _params : nativeptr<int> -> unit
    abstract member GetHistogram : target : HistogramTargetExt * reset : bool * format : PixelFormat * _type : PixelType * values : nativeint -> unit
    abstract member GetHistogramParameter : target : HistogramTargetExt * pname : GetHistogramParameterPNameExt * _params : nativeptr<float32> -> unit
    abstract member GetInteger : target : GetIndexedPName * index : int * data : nativeptr<int> -> unit
    abstract member GetInteger64 : target : GetIndexedPName * index : int * data : nativeptr<int64> -> unit
    abstract member GetIntegerIndexed : target : GetIndexedPName * index : int * data : nativeptr<int> -> unit
    abstract member GetInternalformat : target : ImageTarget * internalformat : SizedInternalFormat * pname : InternalFormatParameter * bufSize : int * _params : nativeptr<int64> -> unit
    abstract member GetMinmax : target : MinmaxTargetExt * reset : bool * format : PixelFormat * _type : PixelType * values : nativeint -> unit
    abstract member GetMinmaxParameter : target : MinmaxTargetExt * pname : GetMinmaxParameterPNameExt * _params : nativeptr<float32> -> unit
    abstract member GetMultiTexEnv : texunit : TextureUnit * target : TextureEnvTarget * pname : TextureEnvParameter * _params : nativeptr<float32> -> unit
    abstract member GetMultiTexGen : texunit : TextureUnit * coord : TextureCoordName * pname : TextureGenParameter * _params : nativeptr<float> -> unit
    abstract member GetMultiTexImage : texunit : TextureUnit * target : TextureTarget * level : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member GetMultiTexLevelParameter : texunit : TextureUnit * target : TextureTarget * level : int * pname : GetTextureParameter * _params : nativeptr<float32> -> unit
    abstract member GetMultiTexParameter : texunit : TextureUnit * target : TextureTarget * pname : GetTextureParameter * _params : nativeptr<float32> -> unit
    abstract member GetMultiTexParameterI : texunit : TextureUnit * target : TextureTarget * pname : GetTextureParameter * _params : nativeptr<int> -> unit
    abstract member GetMultisample : pname : GetMultisamplePName * index : int * _val : nativeptr<float32> -> unit
    abstract member GetNamedBufferParameter : buffer : int * pname : BufferParameterName * _params : nativeptr<int64> -> unit
    abstract member GetNamedBufferPointer : buffer : int * pname : BufferPointer * _params : nativeint -> unit
    abstract member GetNamedBufferSubData : buffer : int * offset : nativeint * size : int * data : nativeint -> unit
    abstract member GetNamedFramebufferAttachmentParameter : framebuffer : int * attachment : FramebufferAttachment * pname : FramebufferParameterName * _params : nativeptr<int> -> unit
    abstract member GetNamedFramebufferParameter : framebuffer : int * pname : FramebufferDefaultParameter * param : nativeptr<int> -> unit
    abstract member GetNamedProgram : program : int * target : All * pname : ProgramPropertyArb * _params : nativeptr<int> -> unit
    abstract member GetNamedProgramLocalParameter : program : int * target : All * index : int * _params : nativeptr<float> -> unit
    abstract member GetNamedProgramLocalParameterI : program : int * target : All * index : int * _params : nativeptr<int> -> unit
    abstract member GetNamedProgramString : program : int * target : All * pname : All * string : nativeint -> unit
    abstract member GetNamedRenderbufferParameter : renderbuffer : int * pname : RenderbufferParameterName * _params : nativeptr<int> -> unit
    abstract member GetPointer : pname : TypeEnum * index : int * _params : nativeint -> unit
    abstract member GetPointerIndexed : target : TypeEnum * index : int * data : nativeint -> unit
    abstract member GetProgram : program : int * pname : GetProgramParameterName * _params : nativeptr<int> -> unit
    abstract member GetProgramBinary : program : int * bufSize : int * length : nativeptr<int> * binaryFormat : nativeptr<BinaryFormat> * binary : nativeint -> unit
    abstract member GetProgramInterface : program : int * programInterface : ProgramInterface * pname : ProgramInterfaceParameter * _params : nativeptr<int> -> unit
    abstract member GetProgramPipeline : pipeline : int * pname : ProgramPipelineParameter * _params : nativeptr<int> -> unit
    abstract member GetProgramResource : program : int * programInterface : ProgramInterface * index : int * propCount : int * props : nativeptr<ProgramProperty> * bufSize : int * length : nativeptr<int> * _params : nativeptr<int> -> unit
    abstract member GetProgramStage : program : int * shadertype : ShaderType * pname : ProgramStageParameter * values : nativeptr<int> -> unit
    abstract member GetQuery : target : QueryTarget * pname : GetQueryParam * _params : nativeptr<int> -> unit
    abstract member GetQueryBufferObject : id : int * buffer : int * pname : QueryObjectParameterName * offset : nativeint -> unit
    abstract member GetQueryIndexed : target : QueryTarget * index : int * pname : GetQueryParam * _params : nativeptr<int> -> unit
    abstract member GetQueryObject : id : int * pname : GetQueryObjectParam * _params : nativeptr<int64> -> unit
    abstract member GetRenderbufferParameter : target : RenderbufferTarget * pname : RenderbufferParameterName * _params : nativeptr<int> -> unit
    abstract member GetSamplerParameter : sampler : int * pname : SamplerParameterName * _params : nativeptr<float32> -> unit
    abstract member GetSamplerParameterI : sampler : int * pname : SamplerParameterName * _params : nativeptr<int> -> unit
    abstract member GetSeparableFilter : target : SeparableTargetExt * format : PixelFormat * _type : PixelType * row : nativeint * column : nativeint * span : nativeint -> unit
    abstract member GetShader : shader : int * pname : ShaderParameter * _params : nativeptr<int> -> unit
    abstract member GetShaderPrecisionFormat : shadertype : ShaderType * precisiontype : ShaderPrecision * range : nativeptr<int> * precision : nativeptr<int> -> unit
    abstract member GetSync : sync : nativeint * pname : SyncParameterName * bufSize : int * length : nativeptr<int> * values : nativeptr<int> -> unit
    abstract member GetTexImage : target : TextureTarget * level : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member GetTexLevelParameter : target : TextureTarget * level : int * pname : GetTextureParameter * _params : nativeptr<float32> -> unit
    abstract member GetTexParameter : target : TextureTarget * pname : GetTextureParameter * _params : nativeptr<float32> -> unit
    abstract member GetTexParameterI : target : TextureTarget * pname : GetTextureParameter * _params : nativeptr<int> -> unit
    abstract member GetTextureImage : texture : int * level : int * format : PixelFormat * _type : PixelType * bufSize : int * pixels : nativeint -> unit
    abstract member GetTextureLevelParameter : texture : int * target : TextureTarget * level : int * pname : GetTextureParameter * _params : nativeptr<float32> -> unit
    abstract member GetTextureParameter : texture : int * target : TextureTarget * pname : GetTextureParameter * _params : nativeptr<float32> -> unit
    abstract member GetTextureParameterI : texture : int * target : TextureTarget * pname : GetTextureParameter * _params : nativeptr<int> -> unit
    abstract member GetTextureSubImage : texture : int * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * _type : PixelType * bufSize : int * pixels : nativeint -> unit
    abstract member GetTransformFeedback : xfb : int * pname : TransformFeedbackIndexedParameter * index : int * param : nativeptr<int> -> unit
    abstract member GetTransformFeedbacki64_ : xfb : int * pname : TransformFeedbackIndexedParameter * index : int * param : nativeptr<int64> -> unit
    abstract member GetUniform : program : int * location : int * _params : nativeptr<float> -> unit
    abstract member GetUniformSubroutine : shadertype : ShaderType * location : int * _params : nativeptr<int> -> unit
    abstract member GetVertexArray : vaobj : int * pname : VertexArrayParameter * param : nativeptr<int> -> unit
    abstract member GetVertexArrayIndexed : vaobj : int * index : int * pname : VertexArrayIndexedParameter * param : nativeptr<int> -> unit
    abstract member GetVertexArrayIndexed64 : vaobj : int * index : int * pname : VertexArrayIndexed64Parameter * param : nativeptr<int64> -> unit
    abstract member GetVertexArrayInteger : vaobj : int * index : int * pname : VertexArrayPName * param : nativeptr<int> -> unit
    abstract member GetVertexArrayPointer : vaobj : int * index : int * pname : VertexArrayPName * param : nativeint -> unit
    abstract member GetVertexAttrib : index : int * pname : VertexAttribParameter * _params : nativeptr<int> -> unit
    abstract member GetVertexAttribI : index : int * pname : VertexAttribParameter * _params : nativeptr<int> -> unit
    abstract member GetVertexAttribL : index : int * pname : VertexAttribParameter * _params : nativeptr<float> -> unit
    abstract member GetVertexAttribPointer : index : int * pname : VertexAttribPointerParameter * pointer : nativeint -> unit
    abstract member GetnColorTable : target : ColorTableTarget * format : PixelFormat * _type : PixelType * bufSize : int * table : nativeint -> unit
    abstract member GetnCompressedTexImage : target : TextureTarget * lod : int * bufSize : int * pixels : nativeint -> unit
    abstract member GetnConvolutionFilter : target : ConvolutionTarget * format : PixelFormat * _type : PixelType * bufSize : int * image : nativeint -> unit
    abstract member GetnHistogram : target : HistogramTargetExt * reset : bool * format : PixelFormat * _type : PixelType * bufSize : int * values : nativeint -> unit
    abstract member GetnMap : target : MapTarget * query : MapQuery * bufSize : int * v : nativeptr<float> -> unit
    abstract member GetnMinmax : target : MinmaxTargetExt * reset : bool * format : PixelFormat * _type : PixelType * bufSize : int * values : nativeint -> unit
    abstract member GetnPixelMap : map : PixelMap * bufSize : int * values : nativeptr<float32> -> unit
    abstract member GetnPolygonStipple : bufSize : int * pattern : nativeptr<byte> -> unit
    abstract member GetnSeparableFilter : target : SeparableTargetExt * format : PixelFormat * _type : PixelType * rowBufSize : int * row : nativeint * columnBufSize : int * column : nativeint * span : nativeint -> unit
    abstract member GetnTexImage : target : TextureTarget * level : int * format : PixelFormat * _type : PixelType * bufSize : int * pixels : nativeint -> unit
    abstract member GetnUniform : program : int * location : int * bufSize : int * _params : nativeptr<float> -> unit
    abstract member Hint : target : HintTarget * mode : HintMode -> unit
    abstract member Histogram : target : HistogramTargetExt * width : int * internalformat : InternalFormat * sink : bool -> unit
    abstract member InvalidateBufferData : buffer : int -> unit
    abstract member InvalidateBufferSubData : buffer : int * offset : nativeint * length : int -> unit
    abstract member InvalidateFramebuffer : target : FramebufferTarget * numAttachments : int * attachments : nativeptr<FramebufferAttachment> -> unit
    abstract member InvalidateNamedFramebufferData : framebuffer : int * numAttachments : int * attachments : nativeptr<FramebufferAttachment> -> unit
    abstract member InvalidateNamedFramebufferSubData : framebuffer : int * numAttachments : int * attachments : nativeptr<FramebufferAttachment> * _x : int * y : int * width : int * height : int -> unit
    abstract member InvalidateSubFramebuffer : target : FramebufferTarget * numAttachments : int * attachments : nativeptr<FramebufferAttachment> * _x : int * y : int * width : int * height : int -> unit
    abstract member InvalidateTexImage : texture : int * level : int -> unit
    abstract member InvalidateTexSubImage : texture : int * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int -> unit
    abstract member LineWidth : width : float32 -> unit
    abstract member LinkProgram : program : int -> unit
    abstract member LogicOp : opcode : LogicOp -> unit
    abstract member MakeImageHandleNonResident : handle : int64 -> unit
    abstract member MakeImageHandleResident : handle : int64 * access : All -> unit
    abstract member MakeTextureHandleNonResident : handle : int64 -> unit
    abstract member MakeTextureHandleResident : handle : int64 -> unit
    abstract member MatrixFrustum : mode : MatrixMode * left : float * right : float * bottom : float * top : float * zNear : float * zFar : float -> unit
    abstract member MatrixLoad : mode : MatrixMode * m : nativeptr<float> -> unit
    abstract member MatrixLoadIdentity : mode : MatrixMode -> unit
    abstract member MatrixLoadTranspose : mode : MatrixMode * m : nativeptr<float> -> unit
    abstract member MatrixMult : mode : MatrixMode * m : nativeptr<float> -> unit
    abstract member MatrixMultTranspose : mode : MatrixMode * m : nativeptr<float> -> unit
    abstract member MatrixOrtho : mode : MatrixMode * left : float * right : float * bottom : float * top : float * zNear : float * zFar : float -> unit
    abstract member MatrixPop : mode : MatrixMode -> unit
    abstract member MatrixPush : mode : MatrixMode -> unit
    abstract member MatrixRotate : mode : MatrixMode * angle : float * _x : float * y : float * z : float -> unit
    abstract member MatrixScale : mode : MatrixMode * _x : float * y : float * z : float -> unit
    abstract member MatrixTranslate : mode : MatrixMode * _x : float * y : float * z : float -> unit
    abstract member MaxShaderCompilerThreads : count : int -> unit
    abstract member MemoryBarrier : barriers : MemoryBarrierFlags -> unit
    abstract member MemoryBarrierByRegion : barriers : MemoryBarrierRegionFlags -> unit
    abstract member MinSampleShading : value : float32 -> unit
    abstract member Minmax : target : MinmaxTargetExt * internalformat : InternalFormat * sink : bool -> unit
    abstract member MultiDrawArrays : mode : PrimitiveType * first : nativeptr<int> * count : nativeptr<int> * drawcount : int -> unit
    abstract member MultiDrawArraysIndirect : mode : PrimitiveType * indirect : nativeint * drawcount : int * stride : int -> unit
    abstract member MultiDrawArraysIndirectCount : mode : PrimitiveType * indirect : nativeint * drawcount : nativeint * maxdrawcount : int * stride : int -> unit
    abstract member MultiDrawElements : mode : PrimitiveType * count : nativeptr<int> * _type : DrawElementsType * indices : nativeint * drawcount : int -> unit
    abstract member MultiDrawElementsBaseVertex : mode : PrimitiveType * count : nativeptr<int> * _type : DrawElementsType * indices : nativeint * drawcount : int * basevertex : nativeptr<int> -> unit
    abstract member MultiDrawElementsIndirect : mode : PrimitiveType * _type : DrawElementsType * indirect : nativeint * drawcount : int * stride : int -> unit
    abstract member MultiDrawElementsIndirectCount : mode : PrimitiveType * _type : All * indirect : nativeint * drawcount : nativeint * maxdrawcount : int * stride : int -> unit
    abstract member MultiTexBuffer : texunit : TextureUnit * target : TextureTarget * internalformat : TypeEnum * buffer : int -> unit
    abstract member MultiTexCoordP1 : texture : TextureUnit * _type : PackedPointerType * coords : int -> unit
    abstract member MultiTexCoordP2 : texture : TextureUnit * _type : PackedPointerType * coords : int -> unit
    abstract member MultiTexCoordP3 : texture : TextureUnit * _type : PackedPointerType * coords : int -> unit
    abstract member MultiTexCoordP4 : texture : TextureUnit * _type : PackedPointerType * coords : int -> unit
    abstract member MultiTexCoordPointer : texunit : TextureUnit * size : int * _type : TexCoordPointerType * stride : int * pointer : nativeint -> unit
    abstract member MultiTexEnv : texunit : TextureUnit * target : TextureEnvTarget * pname : TextureEnvParameter * param : float32 -> unit
    abstract member MultiTexGen : texunit : TextureUnit * coord : TextureCoordName * pname : TextureGenParameter * _params : nativeptr<float> -> unit
    abstract member MultiTexGend : texunit : TextureUnit * coord : TextureCoordName * pname : TextureGenParameter * param : float -> unit
    abstract member MultiTexImage1D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member MultiTexImage2D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member MultiTexImage3D : texunit : TextureUnit * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * depth : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member MultiTexParameter : texunit : TextureUnit * target : TextureTarget * pname : TextureParameterName * param : float32 -> unit
    abstract member MultiTexParameterI : texunit : TextureUnit * target : TextureTarget * pname : TextureParameterName * _params : nativeptr<int> -> unit
    abstract member MultiTexRenderbuffer : texunit : TextureUnit * target : TextureTarget * renderbuffer : int -> unit
    abstract member MultiTexSubImage1D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * width : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member MultiTexSubImage2D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * yoffset : int * width : int * height : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member MultiTexSubImage3D : texunit : TextureUnit * target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member NamedBufferData : buffer : int * size : int * data : nativeint * usage : BufferUsageHint -> unit
    abstract member NamedBufferPageCommitment : buffer : int * offset : nativeint * size : int * commit : bool -> unit
    abstract member NamedBufferStorage : buffer : int * size : int * data : nativeint * flags : BufferStorageFlags -> unit
    abstract member NamedBufferSubData : buffer : int * offset : nativeint * size : int * data : nativeint -> unit
    abstract member NamedCopyBufferSubData : readBuffer : int * writeBuffer : int * readOffset : nativeint * writeOffset : nativeint * size : int -> unit
    abstract member NamedFramebufferDrawBuffer : framebuffer : int * buf : DrawBufferMode -> unit
    abstract member NamedFramebufferDrawBuffers : framebuffer : int * n : int * bufs : nativeptr<DrawBuffersEnum> -> unit
    abstract member NamedFramebufferParameter : framebuffer : int * pname : FramebufferDefaultParameter * param : int -> unit
    abstract member NamedFramebufferReadBuffer : framebuffer : int * src : ReadBufferMode -> unit
    abstract member NamedFramebufferRenderbuffer : framebuffer : int * attachment : FramebufferAttachment * renderbuffertarget : RenderbufferTarget * renderbuffer : int -> unit
    abstract member NamedFramebufferSampleLocations : framebuffer : int * start : int * count : int * v : nativeptr<float32> -> unit
    abstract member NamedFramebufferTexture : framebuffer : int * attachment : FramebufferAttachment * texture : int * level : int -> unit
    abstract member NamedFramebufferTexture1D : framebuffer : int * attachment : FramebufferAttachment * textarget : TextureTarget * texture : int * level : int -> unit
    abstract member NamedFramebufferTexture2D : framebuffer : int * attachment : FramebufferAttachment * textarget : TextureTarget * texture : int * level : int -> unit
    abstract member NamedFramebufferTexture3D : framebuffer : int * attachment : FramebufferAttachment * textarget : TextureTarget * texture : int * level : int * zoffset : int -> unit
    abstract member NamedFramebufferTextureFace : framebuffer : int * attachment : FramebufferAttachment * texture : int * level : int * face : TextureTarget -> unit
    abstract member NamedFramebufferTextureLayer : framebuffer : int * attachment : FramebufferAttachment * texture : int * level : int * layer : int -> unit
    abstract member NamedProgramLocalParameter4 : program : int * target : All * index : int * _x : float * y : float * z : float * w : float -> unit
    abstract member NamedProgramLocalParameterI4 : program : int * target : All * index : int * _x : int * y : int * z : int * w : int -> unit
    abstract member NamedProgramLocalParameters4 : program : int * target : All * index : int * count : int * _params : nativeptr<float32> -> unit
    abstract member NamedProgramLocalParametersI4 : program : int * target : All * index : int * count : int * _params : nativeptr<int> -> unit
    abstract member NamedProgramString : program : int * target : All * format : All * len : int * string : nativeint -> unit
    abstract member NamedRenderbufferStorage : renderbuffer : int * internalformat : RenderbufferStorage * width : int * height : int -> unit
    abstract member NamedRenderbufferStorageMultisample : renderbuffer : int * samples : int * internalformat : RenderbufferStorage * width : int * height : int -> unit
    abstract member NamedRenderbufferStorageMultisampleCoverage : renderbuffer : int * coverageSamples : int * colorSamples : int * internalformat : InternalFormat * width : int * height : int -> unit
    abstract member NormalP3 : _type : PackedPointerType * coords : int -> unit
    abstract member PatchParameter : pname : PatchParameterFloat * values : nativeptr<float32> -> unit
    abstract member PauseTransformFeedback : unit -> unit
    abstract member PixelStore : pname : PixelStoreParameter * param : float32 -> unit
    abstract member PointParameter : pname : PointParameterName * param : float32 -> unit
    abstract member PointSize : size : float32 -> unit
    abstract member PolygonMode : face : MaterialFace * mode : PolygonMode -> unit
    abstract member PolygonOffset : factor : float32 * units : float32 -> unit
    abstract member PolygonOffsetClamp : factor : float32 * units : float32 * clamp : float32 -> unit
    abstract member PopDebugGroup : unit -> unit
    abstract member PopGroupMarker : unit -> unit
    abstract member PrimitiveBoundingBox : minX : float32 * minY : float32 * minZ : float32 * minW : float32 * maxX : float32 * maxY : float32 * maxZ : float32 * maxW : float32 -> unit
    abstract member PrimitiveRestartIndex : index : int -> unit
    abstract member ProgramBinary : program : int * binaryFormat : BinaryFormat * binary : nativeint * length : int -> unit
    abstract member ProgramParameter : program : int * pname : ProgramParameterName * value : int -> unit
    abstract member ProgramUniform1 : program : int * location : int * count : int * value : nativeptr<float> -> unit
    abstract member ProgramUniform2 : program : int * location : int * v0 : float * v1 : float -> unit
    abstract member ProgramUniform3 : program : int * location : int * v0 : float * v1 : float * v2 : float -> unit
    abstract member ProgramUniform4 : program : int * location : int * v0 : float * v1 : float * v2 : float * v3 : float -> unit
    abstract member ProgramUniformHandle : program : int * location : int * count : int * values : nativeptr<int64> -> unit
    abstract member ProgramUniformMatrix2 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix2x3 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix2x4 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix3 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix3x2 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix3x4 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix4 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix4x2 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProgramUniformMatrix4x3 : program : int * location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member ProvokingVertex : mode : ProvokingVertexMode -> unit
    abstract member PushClientAttribDefault : mask : ClientAttribMask -> unit
    abstract member QueryCounter : id : int * target : QueryCounterTarget -> unit
    abstract member RasterSamples : samples : int * fixedsamplelocations : bool -> unit
    abstract member ReadBuffer : src : ReadBufferMode -> unit
    abstract member ReadPixels : _x : int * y : int * width : int * height : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member ReadnPixels : _x : int * y : int * width : int * height : int * format : PixelFormat * _type : PixelType * bufSize : int * data : nativeint -> unit
    abstract member ReleaseShaderCompiler : unit -> unit
    abstract member RenderbufferStorage : target : RenderbufferTarget * internalformat : RenderbufferStorage * width : int * height : int -> unit
    abstract member RenderbufferStorageMultisample : target : RenderbufferTarget * samples : int * internalformat : RenderbufferStorage * width : int * height : int -> unit
    abstract member ResetHistogram : target : HistogramTargetExt -> unit
    abstract member ResetMinmax : target : MinmaxTargetExt -> unit
    abstract member ResumeTransformFeedback : unit -> unit
    abstract member SampleCoverage : value : float32 * invert : bool -> unit
    abstract member SampleMask : maskNumber : int * mask : int -> unit
    abstract member SamplerParameter : sampler : int * pname : SamplerParameterName * param : float32 -> unit
    abstract member SamplerParameterI : sampler : int * pname : SamplerParameterName * param : nativeptr<int> -> unit
    abstract member Scissor : _x : int * y : int * width : int * height : int -> unit
    abstract member ScissorArray : first : int * count : int * v : nativeptr<int> -> unit
    abstract member ScissorIndexed : index : int * left : int * bottom : int * width : int * height : int -> unit
    abstract member SecondaryColorP3 : _type : PackedPointerType * color : int -> unit
    abstract member SeparableFilter2D : target : SeparableTargetExt * internalformat : InternalFormat * width : int * height : int * format : PixelFormat * _type : PixelType * row : nativeint * column : nativeint -> unit
    abstract member ShaderBinary : count : int * shaders : nativeptr<int> * binaryformat : BinaryFormat * binary : nativeint * length : int -> unit
    abstract member ShaderStorageBlockBinding : program : int * storageBlockIndex : int * storageBlockBinding : int -> unit
    abstract member StencilFunc : func : StencilFunction * ref : int * mask : int -> unit
    abstract member StencilFuncSeparate : face : StencilFace * func : StencilFunction * ref : int * mask : int -> unit
    abstract member StencilMask : mask : int -> unit
    abstract member StencilMaskSeparate : face : StencilFace * mask : int -> unit
    abstract member StencilOp : fail : StencilOp * zfail : StencilOp * zpass : StencilOp -> unit
    abstract member StencilOpSeparate : face : StencilFace * sfail : StencilOp * dpfail : StencilOp * dppass : StencilOp -> unit
    abstract member TexBuffer : target : TextureBufferTarget * internalformat : SizedInternalFormat * buffer : int -> unit
    abstract member TexBufferRange : target : TextureBufferTarget * internalformat : SizedInternalFormat * buffer : int * offset : nativeint * size : int -> unit
    abstract member TexCoordP1 : _type : PackedPointerType * coords : int -> unit
    abstract member TexCoordP2 : _type : PackedPointerType * coords : int -> unit
    abstract member TexCoordP3 : _type : PackedPointerType * coords : int -> unit
    abstract member TexCoordP4 : _type : PackedPointerType * coords : int -> unit
    abstract member TexImage1D : target : TextureTarget * level : int * internalformat : PixelInternalFormat * width : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TexImage2D : target : TextureTarget * level : int * internalformat : PixelInternalFormat * width : int * height : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TexImage2DMultisample : target : TextureTargetMultisample * samples : int * internalformat : PixelInternalFormat * width : int * height : int * fixedsamplelocations : bool -> unit
    abstract member TexImage3D : target : TextureTarget * level : int * internalformat : PixelInternalFormat * width : int * height : int * depth : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TexImage3DMultisample : target : TextureTargetMultisample * samples : int * internalformat : PixelInternalFormat * width : int * height : int * depth : int * fixedsamplelocations : bool -> unit
    abstract member TexPageCommitment : target : All * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * commit : bool -> unit
    abstract member TexParameter : target : TextureTarget * pname : TextureParameterName * param : float32 -> unit
    abstract member TexParameterI : target : TextureTarget * pname : TextureParameterName * _params : nativeptr<int> -> unit
    abstract member TexStorage1D : target : TextureTarget1d * levels : int * internalformat : SizedInternalFormat * width : int -> unit
    abstract member TexStorage2D : target : TextureTarget2d * levels : int * internalformat : SizedInternalFormat * width : int * height : int -> unit
    abstract member TexStorage2DMultisample : target : TextureTargetMultisample2d * samples : int * internalformat : SizedInternalFormat * width : int * height : int * fixedsamplelocations : bool -> unit
    abstract member TexStorage3D : target : TextureTarget3d * levels : int * internalformat : SizedInternalFormat * width : int * height : int * depth : int -> unit
    abstract member TexStorage3DMultisample : target : TextureTargetMultisample3d * samples : int * internalformat : SizedInternalFormat * width : int * height : int * depth : int * fixedsamplelocations : bool -> unit
    abstract member TexSubImage1D : target : TextureTarget * level : int * xoffset : int * width : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TexSubImage2D : target : TextureTarget * level : int * xoffset : int * yoffset : int * width : int * height : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TexSubImage3D : target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TextureBarrier : unit -> unit
    abstract member TextureBuffer : texture : int * target : TextureTarget * internalformat : ExtDirectStateAccess * buffer : int -> unit
    abstract member TextureBufferRange : texture : int * target : TextureTarget * internalformat : ExtDirectStateAccess * buffer : int * offset : nativeint * size : int -> unit
    abstract member TextureImage1D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TextureImage2D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TextureImage3D : texture : int * target : TextureTarget * level : int * internalformat : InternalFormat * width : int * height : int * depth : int * border : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TexturePageCommitment : texture : int * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * commit : bool -> unit
    abstract member TextureParameter : texture : int * target : TextureTarget * pname : TextureParameterName * param : float32 -> unit
    abstract member TextureParameterI : texture : int * target : TextureTarget * pname : TextureParameterName * _params : nativeptr<int> -> unit
    abstract member TextureRenderbuffer : texture : int * target : TextureTarget * renderbuffer : int -> unit
    abstract member TextureStorage1D : texture : int * target : All * levels : int * internalformat : ExtDirectStateAccess * width : int -> unit
    abstract member TextureStorage2D : texture : int * target : All * levels : int * internalformat : ExtDirectStateAccess * width : int * height : int -> unit
    abstract member TextureStorage2DMultisample : texture : int * target : TextureTarget * samples : int * internalformat : ExtDirectStateAccess * width : int * height : int * fixedsamplelocations : bool -> unit
    abstract member TextureStorage3D : texture : int * target : All * levels : int * internalformat : ExtDirectStateAccess * width : int * height : int * depth : int -> unit
    abstract member TextureStorage3DMultisample : texture : int * target : All * samples : int * internalformat : ExtDirectStateAccess * width : int * height : int * depth : int * fixedsamplelocations : bool -> unit
    abstract member TextureSubImage1D : texture : int * target : TextureTarget * level : int * xoffset : int * width : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TextureSubImage2D : texture : int * target : TextureTarget * level : int * xoffset : int * yoffset : int * width : int * height : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TextureSubImage3D : texture : int * target : TextureTarget * level : int * xoffset : int * yoffset : int * zoffset : int * width : int * height : int * depth : int * format : PixelFormat * _type : PixelType * pixels : nativeint -> unit
    abstract member TextureView : texture : int * target : TextureTarget * origtexture : int * internalformat : PixelInternalFormat * minlevel : int * numlevels : int * minlayer : int * numlayers : int -> unit
    abstract member TransformFeedbackBufferBase : xfb : int * index : int * buffer : int -> unit
    abstract member TransformFeedbackBufferRange : xfb : int * index : int * buffer : int * offset : nativeint * size : int -> unit
    abstract member Uniform1 : location : int * count : int * value : nativeptr<float> -> unit
    abstract member Uniform2 : location : int * _x : float * y : float -> unit
    abstract member Uniform3 : location : int * _x : float * y : float * z : float -> unit
    abstract member Uniform4 : location : int * _x : float * y : float * z : float * w : float -> unit
    abstract member UniformBlockBinding : program : int * uniformBlockIndex : int * uniformBlockBinding : int -> unit
    abstract member UniformHandle : location : int * count : int * value : nativeptr<int64> -> unit
    abstract member UniformMatrix2 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix2x3 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix2x4 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix3 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix3x2 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix3x4 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix4 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix4x2 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformMatrix4x3 : location : int * count : int * transpose : bool * value : nativeptr<float> -> unit
    abstract member UniformSubroutines : shadertype : ShaderType * count : int * indices : nativeptr<int> -> unit
    abstract member UseProgram : program : int -> unit
    abstract member UseProgramStages : pipeline : int * stages : ProgramStageMask * program : int -> unit
    abstract member UseShaderProgram : _type : All * program : int -> unit
    abstract member ValidateProgram : program : int -> unit
    abstract member ValidateProgramPipeline : pipeline : int -> unit
    abstract member VertexArrayAttribBinding : vaobj : int * attribindex : int * bindingindex : int -> unit
    abstract member VertexArrayAttribFormat : vaobj : int * attribindex : int * size : int * _type : VertexAttribType * normalized : bool * relativeoffset : int -> unit
    abstract member VertexArrayAttribIFormat : vaobj : int * attribindex : int * size : int * _type : VertexAttribType * relativeoffset : int -> unit
    abstract member VertexArrayAttribLFormat : vaobj : int * attribindex : int * size : int * _type : VertexAttribType * relativeoffset : int -> unit
    abstract member VertexArrayBindVertexBuffer : vaobj : int * bindingindex : int * buffer : int * offset : nativeint * stride : int -> unit
    abstract member VertexArrayBindingDivisor : vaobj : int * bindingindex : int * divisor : int -> unit
    abstract member VertexArrayColorOffset : vaobj : int * buffer : int * size : int * _type : ColorPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArrayEdgeFlagOffset : vaobj : int * buffer : int * stride : int * offset : nativeint -> unit
    abstract member VertexArrayElementBuffer : vaobj : int * buffer : int -> unit
    abstract member VertexArrayFogCoordOffset : vaobj : int * buffer : int * _type : FogPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArrayIndexOffset : vaobj : int * buffer : int * _type : IndexPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArrayMultiTexCoordOffset : vaobj : int * buffer : int * texunit : All * size : int * _type : TexCoordPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArrayNormalOffset : vaobj : int * buffer : int * _type : NormalPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArraySecondaryColorOffset : vaobj : int * buffer : int * size : int * _type : ColorPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArrayTexCoordOffset : vaobj : int * buffer : int * size : int * _type : TexCoordPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexArrayVertexAttribBinding : vaobj : int * attribindex : int * bindingindex : int -> unit
    abstract member VertexArrayVertexAttribDivisor : vaobj : int * index : int * divisor : int -> unit
    abstract member VertexArrayVertexAttribFormat : vaobj : int * attribindex : int * size : int * _type : All * normalized : bool * relativeoffset : int -> unit
    abstract member VertexArrayVertexAttribIFormat : vaobj : int * attribindex : int * size : int * _type : All * relativeoffset : int -> unit
    abstract member VertexArrayVertexAttribIOffset : vaobj : int * buffer : int * index : int * size : int * _type : VertexAttribEnum * stride : int * offset : nativeint -> unit
    abstract member VertexArrayVertexAttribLFormat : vaobj : int * attribindex : int * size : int * _type : All * relativeoffset : int -> unit
    abstract member VertexArrayVertexAttribLOffset : vaobj : int * buffer : int * index : int * size : int * _type : All * stride : int * offset : nativeint -> unit
    abstract member VertexArrayVertexAttribOffset : vaobj : int * buffer : int * index : int * size : int * _type : VertexAttribPointerType * normalized : bool * stride : int * offset : nativeint -> unit
    abstract member VertexArrayVertexBindingDivisor : vaobj : int * bindingindex : int * divisor : int -> unit
    abstract member VertexArrayVertexBuffer : vaobj : int * bindingindex : int * buffer : int * offset : nativeint * stride : int -> unit
    abstract member VertexArrayVertexBuffers : vaobj : int * first : int * count : int * buffers : nativeptr<int> * offsets : nativeptr<nativeint> * strides : nativeptr<int> -> unit
    abstract member VertexArrayVertexOffset : vaobj : int * buffer : int * size : int * _type : VertexPointerType * stride : int * offset : nativeint -> unit
    abstract member VertexAttrib1 : index : int * _x : float -> unit
    abstract member VertexAttrib2 : index : int * _x : float32 * y : float32 -> unit
    abstract member VertexAttrib3 : index : int * _x : float * y : float * z : float -> unit
    abstract member VertexAttrib4 : index : int * _x : float * y : float * z : float * w : float -> unit
    abstract member VertexAttrib4N : index : int * _x : byte * y : byte * z : byte * w : byte -> unit
    abstract member VertexAttribBinding : attribindex : int * bindingindex : int -> unit
    abstract member VertexAttribDivisor : index : int * divisor : int -> unit
    abstract member VertexAttribFormat : attribindex : int * size : int * _type : VertexAttribType * normalized : bool * relativeoffset : int -> unit
    abstract member VertexAttribI1 : index : int * v : nativeptr<int> -> unit
    abstract member VertexAttribI2 : index : int * _x : int * y : int -> unit
    abstract member VertexAttribI3 : index : int * _x : int * y : int * z : int -> unit
    abstract member VertexAttribI4 : index : int * _x : int * y : int * z : int * w : int -> unit
    abstract member VertexAttribIFormat : attribindex : int * size : int * _type : VertexAttribIntegerType * relativeoffset : int -> unit
    abstract member VertexAttribIPointer : index : int * size : int * _type : VertexAttribIntegerType * stride : int * pointer : nativeint -> unit
    abstract member VertexAttribL1 : index : int * _x : float -> unit
    abstract member VertexAttribL2 : index : int * _x : float * y : float -> unit
    abstract member VertexAttribL3 : index : int * _x : float * y : float * z : float -> unit
    abstract member VertexAttribL4 : index : int * _x : float * y : float * z : float * w : float -> unit
    abstract member VertexAttribLFormat : attribindex : int * size : int * _type : VertexAttribDoubleType * relativeoffset : int -> unit
    abstract member VertexAttribLPointer : index : int * size : int * _type : VertexAttribDoubleType * stride : int * pointer : nativeint -> unit
    abstract member VertexAttribP1 : index : int * _type : PackedPointerType * normalized : bool * value : nativeptr<int> -> unit
    abstract member VertexAttribP2 : index : int * _type : PackedPointerType * normalized : bool * value : int -> unit
    abstract member VertexAttribP3 : index : int * _type : PackedPointerType * normalized : bool * value : int -> unit
    abstract member VertexAttribP4 : index : int * _type : PackedPointerType * normalized : bool * value : int -> unit
    abstract member VertexAttribPointer : index : int * size : int * _type : VertexAttribPointerType * normalized : bool * stride : int * pointer : nativeint -> unit
    abstract member VertexBindingDivisor : bindingindex : int * divisor : int -> unit
    abstract member VertexP2 : _type : PackedPointerType * value : int -> unit
    abstract member VertexP3 : _type : PackedPointerType * value : int -> unit
    abstract member VertexP4 : _type : PackedPointerType * value : int -> unit
    abstract member Viewport : _x : int * y : int * width : int * height : int -> unit
    abstract member ViewportArray : first : int * count : int * v : nativeptr<float32> -> unit
    abstract member ViewportIndexed : index : int * _x : float32 * y : float32 * w : float32 * h : float32 -> unit
    abstract member WaitSync : sync : nativeint * flags : WaitSyncFlags * timeout : int64 -> unit
    abstract member WindowRectangles : mode : All * count : int * box : nativeptr<int> -> unit
    abstract member Run : unit -> unit
    abstract member Count : int
    abstract member Clear : unit -> unit


[<AutoOpen>]
module private NativeHelpers =
    let inline nsize<'a> = nativeint sizeof<'a>
    let inline read<'a when 'a : unmanaged> (ptr : byref<nativeint>) : 'a =
        let a = NativePtr.read (NativePtr.ofNativeInt ptr)
        ptr <- ptr + nsize<'a>
        a


type NativeCommandStream(initialSize : nativeint) = 
    let initialSize = nativeint (max (Fun.NextPowerOfTwo (int64 initialSize)) 128L)
    static let ptrSize = nativeint sizeof<nativeint>
    let mutable capacity = initialSize
    let mutable mem = Marshal.AllocHGlobal capacity
    let mutable offset = 0n
    let mutable count = 0

    let resize (minSize : nativeint) =
        let newCapacity = nativeint (Fun.NextPowerOfTwo(int64 minSize)) |> max initialSize
        if capacity <> newCapacity then
            mem <- Marshal.ReAllocHGlobal(mem, newCapacity)
            capacity <- newCapacity
    member x.Memory = mem
    member x.Size = offset
    member inline private x.Append(code : InstructionCode) =
        let size = 8n
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a) =
        let sa = nativeint sizeof<'a>
        let size = 8n+sa
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let size = 8n+sa+sb
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let size = 8n+sa+sb+sc
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let size = 8n+sa+sb+sc+sd
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let size = 8n+sa+sb+sc+sd+se
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let size = 8n+sa+sb+sc+sd+se+sf
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let size = 8n+sa+sb+sc+sd+se+sf+sg
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let sj = nativeint sizeof<'j>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si+sj
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let sj = nativeint sizeof<'j>
        let sk = nativeint sizeof<'k>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si+sj+sk
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k, arg11 : 'l) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let sj = nativeint sizeof<'j>
        let sk = nativeint sizeof<'k>
        let sl = nativeint sizeof<'l>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si+sj+sk+sl
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
        NativePtr.write (NativePtr.ofNativeInt ptr) arg11; ptr <- ptr + sl
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k, arg11 : 'l, arg12 : 'm) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let sj = nativeint sizeof<'j>
        let sk = nativeint sizeof<'k>
        let sl = nativeint sizeof<'l>
        let sm = nativeint sizeof<'m>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si+sj+sk+sl+sm
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
        NativePtr.write (NativePtr.ofNativeInt ptr) arg11; ptr <- ptr + sl
        NativePtr.write (NativePtr.ofNativeInt ptr) arg12; ptr <- ptr + sm
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k, arg11 : 'l, arg12 : 'm, arg13 : 'n) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let sj = nativeint sizeof<'j>
        let sk = nativeint sizeof<'k>
        let sl = nativeint sizeof<'l>
        let sm = nativeint sizeof<'m>
        let sn = nativeint sizeof<'n>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si+sj+sk+sl+sm+sn
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
        NativePtr.write (NativePtr.ofNativeInt ptr) arg11; ptr <- ptr + sl
        NativePtr.write (NativePtr.ofNativeInt ptr) arg12; ptr <- ptr + sm
        NativePtr.write (NativePtr.ofNativeInt ptr) arg13; ptr <- ptr + sn
        offset <- offset + size
        count <- count + 1
    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k, arg11 : 'l, arg12 : 'm, arg13 : 'n, arg14 : 'o) =
        let sa = nativeint sizeof<'a>
        let sb = nativeint sizeof<'b>
        let sc = nativeint sizeof<'c>
        let sd = nativeint sizeof<'d>
        let se = nativeint sizeof<'e>
        let sf = nativeint sizeof<'f>
        let sg = nativeint sizeof<'g>
        let sh = nativeint sizeof<'h>
        let si = nativeint sizeof<'i>
        let sj = nativeint sizeof<'j>
        let sk = nativeint sizeof<'k>
        let sl = nativeint sizeof<'l>
        let sm = nativeint sizeof<'m>
        let sn = nativeint sizeof<'n>
        let so = nativeint sizeof<'o>
        let size = 8n+sa+sb+sc+sd+se+sf+sg+sh+si+sj+sk+sl+sm+sn+so
        if offset + size > capacity then resize (offset + size)
        let mutable ptr = mem + offset
        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
        NativePtr.write (NativePtr.ofNativeInt ptr) arg11; ptr <- ptr + sl
        NativePtr.write (NativePtr.ofNativeInt ptr) arg12; ptr <- ptr + sm
        NativePtr.write (NativePtr.ofNativeInt ptr) arg13; ptr <- ptr + sn
        NativePtr.write (NativePtr.ofNativeInt ptr) arg14; ptr <- ptr + so
        offset <- offset + size
        count <- count + 1
    member x.Dispose() =
        Marshal.FreeHGlobal mem
        capacity <- 0n
        mem <- 0n
        offset <- 0n
        count <- 0
    member x.Clear() =
        resize initialSize
        offset <- 0n
        count <- 0
    member x.Count = count
    member x.ActiveProgram(program : int) =
        x.Append(InstructionCode.ActiveProgram, program)
    member x.ActiveShaderProgram(pipeline : int, program : int) =
        x.Append(InstructionCode.ActiveShaderProgram, pipeline, program)
    member x.ActiveTexture(texture : TextureUnit) =
        x.Append(InstructionCode.ActiveTexture, texture)
    member x.AttachShader(program : int, shader : int) =
        x.Append(InstructionCode.AttachShader, program, shader)
    member x.BeginConditionalRender(id : int, mode : ConditionalRenderType) =
        x.Append(InstructionCode.BeginConditionalRender, id, mode)
    member x.BeginQuery(target : QueryTarget, id : int) =
        x.Append(InstructionCode.BeginQuery, target, id)
    member x.BeginQueryIndexed(target : QueryTarget, index : int, id : int) =
        x.Append(InstructionCode.BeginQueryIndexed, target, index, id)
    member x.BeginTransformFeedback(primitiveMode : TransformFeedbackPrimitiveType) =
        x.Append(InstructionCode.BeginTransformFeedback, primitiveMode)
    member x.BindBuffer(target : BufferTarget, buffer : int) =
        x.Append(InstructionCode.BindBuffer, target, buffer)
    member x.BindBufferBase(target : BufferRangeTarget, index : int, buffer : int) =
        x.Append(InstructionCode.BindBufferBase, target, index, buffer)
    member x.BindBufferRange(target : BufferRangeTarget, index : int, buffer : int, offset : nativeint, size : int) =
        x.Append(InstructionCode.BindBufferRange, target, index, buffer, offset, size)
    member x.BindBuffersBase(target : BufferRangeTarget, first : int, count : int, buffers : nativeptr<int>) =
        x.Append(InstructionCode.BindBuffersBase, target, first, count, buffers)
    member x.BindBuffersRange(target : BufferRangeTarget, first : int, count : int, buffers : nativeptr<int>, offsets : nativeptr<nativeint>, sizes : nativeptr<nativeint>) =
        x.Append(InstructionCode.BindBuffersRange, target, first, count, buffers, offsets, sizes)
    member x.BindFramebuffer(target : FramebufferTarget, framebuffer : int) =
        x.Append(InstructionCode.BindFramebuffer, target, framebuffer)
    member x.BindImageTexture(unit : int, texture : int, level : int, layered : bool, layer : int, access : TextureAccess, format : SizedInternalFormat) =
        x.Append(InstructionCode.BindImageTexture, unit, texture, level, (if layered then 1 else 0), layer, access, format)
    member x.BindImageTextures(first : int, count : int, textures : nativeptr<int>) =
        x.Append(InstructionCode.BindImageTextures, first, count, textures)
    member x.BindMultiTexture(texunit : TextureUnit, target : TextureTarget, texture : int) =
        x.Append(InstructionCode.BindMultiTexture, texunit, target, texture)
    member x.BindProgramPipeline(pipeline : int) =
        x.Append(InstructionCode.BindProgramPipeline, pipeline)
    member x.BindRenderbuffer(target : RenderbufferTarget, renderbuffer : int) =
        x.Append(InstructionCode.BindRenderbuffer, target, renderbuffer)
    member x.BindSampler(unit : int, sampler : int) =
        x.Append(InstructionCode.BindSampler, unit, sampler)
    member x.BindSamplers(first : int, count : int, samplers : nativeptr<int>) =
        x.Append(InstructionCode.BindSamplers, first, count, samplers)
    member x.BindTexture(target : TextureTarget, texture : int) =
        x.Append(InstructionCode.BindTexture, target, texture)
    member x.BindTextureUnit(unit : int, texture : int) =
        x.Append(InstructionCode.BindTextureUnit, unit, texture)
    member x.BindTextures(first : int, count : int, textures : nativeptr<int>) =
        x.Append(InstructionCode.BindTextures, first, count, textures)
    member x.BindTransformFeedback(target : TransformFeedbackTarget, id : int) =
        x.Append(InstructionCode.BindTransformFeedback, target, id)
    member x.BindVertexArray(array : int) =
        x.Append(InstructionCode.BindVertexArray, array)
    member x.BindVertexBuffer(bindingindex : int, buffer : int, offset : nativeint, stride : int) =
        x.Append(InstructionCode.BindVertexBuffer, bindingindex, buffer, offset, stride)
    member x.BindVertexBuffers(first : int, count : int, buffers : nativeptr<int>, offsets : nativeptr<nativeint>, strides : nativeptr<int>) =
        x.Append(InstructionCode.BindVertexBuffers, first, count, buffers, offsets, strides)
    member x.BlendColor(red : float32, green : float32, blue : float32, alpha : float32) =
        x.Append(InstructionCode.BlendColor, red, green, blue, alpha)
    member x.BlendEquation(buf : int, mode : BlendEquationMode) =
        x.Append(InstructionCode.BlendEquation, buf, mode)
    member x.BlendEquationSeparate(buf : int, modeRGB : BlendEquationMode, modeAlpha : BlendEquationMode) =
        x.Append(InstructionCode.BlendEquationSeparate, buf, modeRGB, modeAlpha)
    member x.BlendFunc(buf : int, src : BlendingFactorSrc, dst : BlendingFactorDest) =
        x.Append(InstructionCode.BlendFunc, buf, src, dst)
    member x.BlendFuncSeparate(buf : int, srcRGB : BlendingFactorSrc, dstRGB : BlendingFactorDest, srcAlpha : BlendingFactorSrc, dstAlpha : BlendingFactorDest) =
        x.Append(InstructionCode.BlendFuncSeparate, buf, srcRGB, dstRGB, srcAlpha, dstAlpha)
    member x.BlitFramebuffer(srcX0 : int, srcY0 : int, srcX1 : int, srcY1 : int, dstX0 : int, dstY0 : int, dstX1 : int, dstY1 : int, mask : ClearBufferMask, filter : BlitFramebufferFilter) =
        x.Append(InstructionCode.BlitFramebuffer, srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter)
    member x.BlitNamedFramebuffer(readFramebuffer : int, drawFramebuffer : int, srcX0 : int, srcY0 : int, srcX1 : int, srcY1 : int, dstX0 : int, dstY0 : int, dstX1 : int, dstY1 : int, mask : ClearBufferMask, filter : BlitFramebufferFilter) =
        x.Append(InstructionCode.BlitNamedFramebuffer, readFramebuffer, drawFramebuffer, srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter)
    member x.BufferData(target : BufferTarget, size : int, data : nativeint, usage : BufferUsageHint) =
        x.Append(InstructionCode.BufferData, target, size, data, usage)
    member x.BufferPageCommitment(target : All, offset : nativeint, size : int, commit : bool) =
        x.Append(InstructionCode.BufferPageCommitment, target, offset, size, (if commit then 1 else 0))
    member x.BufferStorage(target : BufferTarget, size : int, data : nativeint, flags : BufferStorageFlags) =
        x.Append(InstructionCode.BufferStorage, target, size, data, flags)
    member x.BufferSubData(target : BufferTarget, offset : nativeint, size : int, data : nativeint) =
        x.Append(InstructionCode.BufferSubData, target, offset, size, data)
    member x.ClampColor(target : ClampColorTarget, clamp : ClampColorMode) =
        x.Append(InstructionCode.ClampColor, target, clamp)
    member x.Clear(mask : ClearBufferMask) =
        x.Append(InstructionCode.Clear, mask)
    member x.ClearBuffer(buffer : ClearBufferCombined, drawbuffer : int, depth : float32, stencil : int) =
        x.Append(InstructionCode.ClearBuffer, buffer, drawbuffer, depth, stencil)
    member x.ClearBufferData(target : BufferTarget, internalformat : PixelInternalFormat, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ClearBufferData, target, internalformat, format, _type, data)
    member x.ClearBufferSubData(target : BufferTarget, internalformat : PixelInternalFormat, offset : nativeint, size : int, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ClearBufferSubData, target, internalformat, offset, size, format, _type, data)
    member x.ClearColor(red : float32, green : float32, blue : float32, alpha : float32) =
        x.Append(InstructionCode.ClearColor, red, green, blue, alpha)
    member x.ClearDepth(depth : float) =
        x.Append(InstructionCode.ClearDepth, depth)
    member x.ClearNamedBufferData(buffer : int, internalformat : PixelInternalFormat, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ClearNamedBufferData, buffer, internalformat, format, _type, data)
    member x.ClearNamedBufferSubData(buffer : int, internalformat : PixelInternalFormat, offset : nativeint, size : int, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ClearNamedBufferSubData, buffer, internalformat, offset, size, format, _type, data)
    member x.ClearNamedFramebuffer(framebuffer : int, buffer : ClearBufferCombined, drawbuffer : int, depth : float32, stencil : int) =
        x.Append(InstructionCode.ClearNamedFramebuffer, framebuffer, buffer, drawbuffer, depth, stencil)
    member x.ClearStencil(s : int) =
        x.Append(InstructionCode.ClearStencil, s)
    member x.ClearTexImage(texture : int, level : int, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ClearTexImage, texture, level, format, _type, data)
    member x.ClearTexSubImage(texture : int, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ClearTexSubImage, texture, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, data)
    member x.ClientAttribDefault(mask : ClientAttribMask) =
        x.Append(InstructionCode.ClientAttribDefault, mask)
    member x.ClipControl(origin : ClipOrigin, depth : ClipDepthMode) =
        x.Append(InstructionCode.ClipControl, origin, depth)
    member x.ColorMask(index : int, r : bool, g : bool, b : bool, a : bool) =
        x.Append(InstructionCode.ColorMask, index, (if r then 1 else 0), (if g then 1 else 0), (if b then 1 else 0), (if a then 1 else 0))
    member x.ColorP3(_type : PackedPointerType, color : int) =
        x.Append(InstructionCode.ColorP3, _type, color)
    member x.ColorP4(_type : PackedPointerType, color : int) =
        x.Append(InstructionCode.ColorP4, _type, color)
    member x.ColorSubTable(target : ColorTableTarget, start : int, count : int, format : PixelFormat, _type : PixelType, data : nativeint) =
        x.Append(InstructionCode.ColorSubTable, target, start, count, format, _type, data)
    member x.ColorTable(target : ColorTableTarget, internalformat : InternalFormat, width : int, format : PixelFormat, _type : PixelType, table : nativeint) =
        x.Append(InstructionCode.ColorTable, target, internalformat, width, format, _type, table)
    member x.ColorTableParameter(target : ColorTableTarget, pname : ColorTableParameterPNameSgi, _params : nativeptr<float32>) =
        x.Append(InstructionCode.ColorTableParameter, target, pname, _params)
    member x.CompileShader(shader : int) =
        x.Append(InstructionCode.CompileShader, shader)
    member x.CompressedMultiTexImage1D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, border : int, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedMultiTexImage1D, texunit, target, level, internalformat, width, border, imageSize, bits)
    member x.CompressedMultiTexImage2D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, border : int, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedMultiTexImage2D, texunit, target, level, internalformat, width, height, border, imageSize, bits)
    member x.CompressedMultiTexImage3D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, depth : int, border : int, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedMultiTexImage3D, texunit, target, level, internalformat, width, height, depth, border, imageSize, bits)
    member x.CompressedMultiTexSubImage1D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, width : int, format : PixelFormat, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedMultiTexSubImage1D, texunit, target, level, xoffset, width, format, imageSize, bits)
    member x.CompressedMultiTexSubImage2D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, yoffset : int, width : int, height : int, format : PixelFormat, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedMultiTexSubImage2D, texunit, target, level, xoffset, yoffset, width, height, format, imageSize, bits)
    member x.CompressedMultiTexSubImage3D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedMultiTexSubImage3D, texunit, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, bits)
    member x.CompressedTexImage1D(target : TextureTarget, level : int, internalformat : InternalFormat, width : int, border : int, imageSize : int, data : nativeint) =
        x.Append(InstructionCode.CompressedTexImage1D, target, level, internalformat, width, border, imageSize, data)
    member x.CompressedTexImage2D(target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, border : int, imageSize : int, data : nativeint) =
        x.Append(InstructionCode.CompressedTexImage2D, target, level, internalformat, width, height, border, imageSize, data)
    member x.CompressedTexImage3D(target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, depth : int, border : int, imageSize : int, data : nativeint) =
        x.Append(InstructionCode.CompressedTexImage3D, target, level, internalformat, width, height, depth, border, imageSize, data)
    member x.CompressedTexSubImage1D(target : TextureTarget, level : int, xoffset : int, width : int, format : PixelFormat, imageSize : int, data : nativeint) =
        x.Append(InstructionCode.CompressedTexSubImage1D, target, level, xoffset, width, format, imageSize, data)
    member x.CompressedTexSubImage2D(target : TextureTarget, level : int, xoffset : int, yoffset : int, width : int, height : int, format : PixelFormat, imageSize : int, data : nativeint) =
        x.Append(InstructionCode.CompressedTexSubImage2D, target, level, xoffset, yoffset, width, height, format, imageSize, data)
    member x.CompressedTexSubImage3D(target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, imageSize : int, data : nativeint) =
        x.Append(InstructionCode.CompressedTexSubImage3D, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, data)
    member x.CompressedTextureImage1D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, border : int, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedTextureImage1D, texture, target, level, internalformat, width, border, imageSize, bits)
    member x.CompressedTextureImage2D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, border : int, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedTextureImage2D, texture, target, level, internalformat, width, height, border, imageSize, bits)
    member x.CompressedTextureImage3D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, depth : int, border : int, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedTextureImage3D, texture, target, level, internalformat, width, height, depth, border, imageSize, bits)
    member x.CompressedTextureSubImage1D(texture : int, target : TextureTarget, level : int, xoffset : int, width : int, format : PixelFormat, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedTextureSubImage1D, texture, target, level, xoffset, width, format, imageSize, bits)
    member x.CompressedTextureSubImage2D(texture : int, target : TextureTarget, level : int, xoffset : int, yoffset : int, width : int, height : int, format : PixelFormat, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedTextureSubImage2D, texture, target, level, xoffset, yoffset, width, height, format, imageSize, bits)
    member x.CompressedTextureSubImage3D(texture : int, target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, imageSize : int, bits : nativeint) =
        x.Append(InstructionCode.CompressedTextureSubImage3D, texture, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, bits)
    member x.ConvolutionFilter1D(target : ConvolutionTarget, internalformat : InternalFormat, width : int, format : PixelFormat, _type : PixelType, image : nativeint) =
        x.Append(InstructionCode.ConvolutionFilter1D, target, internalformat, width, format, _type, image)
    member x.ConvolutionFilter2D(target : ConvolutionTarget, internalformat : InternalFormat, width : int, height : int, format : PixelFormat, _type : PixelType, image : nativeint) =
        x.Append(InstructionCode.ConvolutionFilter2D, target, internalformat, width, height, format, _type, image)
    member x.ConvolutionParameter(target : ConvolutionTarget, pname : ConvolutionParameterExt, _params : float32) =
        x.Append(InstructionCode.ConvolutionParameter, target, pname, _params)
    member x.CopyBufferSubData(readTarget : BufferTarget, writeTarget : BufferTarget, readOffset : nativeint, writeOffset : nativeint, size : int) =
        x.Append(InstructionCode.CopyBufferSubData, readTarget, writeTarget, readOffset, writeOffset, size)
    member x.CopyColorSubTable(target : ColorTableTarget, start : int, _x : int, y : int, width : int) =
        x.Append(InstructionCode.CopyColorSubTable, target, start, _x, y, width)
    member x.CopyColorTable(target : ColorTableTarget, internalformat : InternalFormat, _x : int, y : int, width : int) =
        x.Append(InstructionCode.CopyColorTable, target, internalformat, _x, y, width)
    member x.CopyConvolutionFilter1D(target : ConvolutionTarget, internalformat : InternalFormat, _x : int, y : int, width : int) =
        x.Append(InstructionCode.CopyConvolutionFilter1D, target, internalformat, _x, y, width)
    member x.CopyConvolutionFilter2D(target : ConvolutionTarget, internalformat : InternalFormat, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyConvolutionFilter2D, target, internalformat, _x, y, width, height)
    member x.CopyImageSubData(srcName : int, srcTarget : ImageTarget, srcLevel : int, srcX : int, srcY : int, srcZ : int, dstName : int, dstTarget : ImageTarget, dstLevel : int, dstX : int, dstY : int, dstZ : int, srcWidth : int, srcHeight : int, srcDepth : int) =
        x.Append(InstructionCode.CopyImageSubData, srcName, srcTarget, srcLevel, srcX, srcY, srcZ, dstName, dstTarget, dstLevel, dstX, dstY, dstZ, srcWidth, srcHeight, srcDepth)
    member x.CopyMultiTexImage1D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, _x : int, y : int, width : int, border : int) =
        x.Append(InstructionCode.CopyMultiTexImage1D, texunit, target, level, internalformat, _x, y, width, border)
    member x.CopyMultiTexImage2D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, _x : int, y : int, width : int, height : int, border : int) =
        x.Append(InstructionCode.CopyMultiTexImage2D, texunit, target, level, internalformat, _x, y, width, height, border)
    member x.CopyMultiTexSubImage1D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, _x : int, y : int, width : int) =
        x.Append(InstructionCode.CopyMultiTexSubImage1D, texunit, target, level, xoffset, _x, y, width)
    member x.CopyMultiTexSubImage2D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, yoffset : int, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyMultiTexSubImage2D, texunit, target, level, xoffset, yoffset, _x, y, width, height)
    member x.CopyMultiTexSubImage3D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyMultiTexSubImage3D, texunit, target, level, xoffset, yoffset, zoffset, _x, y, width, height)
    member x.CopyNamedBufferSubData(readBuffer : int, writeBuffer : int, readOffset : nativeint, writeOffset : nativeint, size : int) =
        x.Append(InstructionCode.CopyNamedBufferSubData, readBuffer, writeBuffer, readOffset, writeOffset, size)
    member x.CopyTexImage1D(target : TextureTarget, level : int, internalformat : InternalFormat, _x : int, y : int, width : int, border : int) =
        x.Append(InstructionCode.CopyTexImage1D, target, level, internalformat, _x, y, width, border)
    member x.CopyTexImage2D(target : TextureTarget, level : int, internalformat : InternalFormat, _x : int, y : int, width : int, height : int, border : int) =
        x.Append(InstructionCode.CopyTexImage2D, target, level, internalformat, _x, y, width, height, border)
    member x.CopyTexSubImage1D(target : TextureTarget, level : int, xoffset : int, _x : int, y : int, width : int) =
        x.Append(InstructionCode.CopyTexSubImage1D, target, level, xoffset, _x, y, width)
    member x.CopyTexSubImage2D(target : TextureTarget, level : int, xoffset : int, yoffset : int, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyTexSubImage2D, target, level, xoffset, yoffset, _x, y, width, height)
    member x.CopyTexSubImage3D(target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyTexSubImage3D, target, level, xoffset, yoffset, zoffset, _x, y, width, height)
    member x.CopyTextureImage1D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, _x : int, y : int, width : int, border : int) =
        x.Append(InstructionCode.CopyTextureImage1D, texture, target, level, internalformat, _x, y, width, border)
    member x.CopyTextureImage2D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, _x : int, y : int, width : int, height : int, border : int) =
        x.Append(InstructionCode.CopyTextureImage2D, texture, target, level, internalformat, _x, y, width, height, border)
    member x.CopyTextureSubImage1D(texture : int, target : TextureTarget, level : int, xoffset : int, _x : int, y : int, width : int) =
        x.Append(InstructionCode.CopyTextureSubImage1D, texture, target, level, xoffset, _x, y, width)
    member x.CopyTextureSubImage2D(texture : int, target : TextureTarget, level : int, xoffset : int, yoffset : int, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyTextureSubImage2D, texture, target, level, xoffset, yoffset, _x, y, width, height)
    member x.CopyTextureSubImage3D(texture : int, target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.CopyTextureSubImage3D, texture, target, level, xoffset, yoffset, zoffset, _x, y, width, height)
    member x.CreateBuffers(n : int, buffers : nativeptr<int>) =
        x.Append(InstructionCode.CreateBuffers, n, buffers)
    member x.CreateFramebuffers(n : int, framebuffers : nativeptr<uint32>) =
        x.Append(InstructionCode.CreateFramebuffers, n, framebuffers)
    member x.CreateProgramPipelines(n : int, pipelines : nativeptr<int>) =
        x.Append(InstructionCode.CreateProgramPipelines, n, pipelines)
    member x.CreateQueries(target : QueryTarget, n : int, ids : nativeptr<int>) =
        x.Append(InstructionCode.CreateQueries, target, n, ids)
    member x.CreateRenderbuffers(n : int, renderbuffers : nativeptr<int>) =
        x.Append(InstructionCode.CreateRenderbuffers, n, renderbuffers)
    member x.CreateSamplers(n : int, samplers : nativeptr<int>) =
        x.Append(InstructionCode.CreateSamplers, n, samplers)
    member x.CreateTextures(target : TextureTarget, n : int, textures : nativeptr<int>) =
        x.Append(InstructionCode.CreateTextures, target, n, textures)
    member x.CreateTransformFeedbacks(n : int, ids : nativeptr<int>) =
        x.Append(InstructionCode.CreateTransformFeedbacks, n, ids)
    member x.CreateVertexArrays(n : int, arrays : nativeptr<int>) =
        x.Append(InstructionCode.CreateVertexArrays, n, arrays)
    member x.CullFace(mode : CullFaceMode) =
        x.Append(InstructionCode.CullFace, mode)
    member x.DebugMessageControl(source : DebugSourceControl, _type : DebugTypeControl, severity : DebugSeverityControl, count : int, ids : nativeptr<int>, enabled : bool) =
        x.Append(InstructionCode.DebugMessageControl, source, _type, severity, count, ids, (if enabled then 1 else 0))
    member x.DeleteBuffer(buffers : int) =
        x.Append(InstructionCode.DeleteBuffer, buffers)
    member x.DeleteBuffers(n : int, buffers : nativeptr<int>) =
        x.Append(InstructionCode.DeleteBuffers, n, buffers)
    member x.DeleteFramebuffer(framebuffers : int) =
        x.Append(InstructionCode.DeleteFramebuffer, framebuffers)
    member x.DeleteFramebuffers(n : int, framebuffers : nativeptr<int>) =
        x.Append(InstructionCode.DeleteFramebuffers, n, framebuffers)
    member x.DeleteProgram(program : int) =
        x.Append(InstructionCode.DeleteProgram, program)
    member x.DeleteProgramPipeline(pipelines : int) =
        x.Append(InstructionCode.DeleteProgramPipeline, pipelines)
    member x.DeleteProgramPipelines(n : int, pipelines : nativeptr<int>) =
        x.Append(InstructionCode.DeleteProgramPipelines, n, pipelines)
    member x.DeleteQueries(n : int, ids : nativeptr<int>) =
        x.Append(InstructionCode.DeleteQueries, n, ids)
    member x.DeleteQuery(ids : int) =
        x.Append(InstructionCode.DeleteQuery, ids)
    member x.DeleteRenderbuffer(renderbuffers : int) =
        x.Append(InstructionCode.DeleteRenderbuffer, renderbuffers)
    member x.DeleteRenderbuffers(n : int, renderbuffers : nativeptr<int>) =
        x.Append(InstructionCode.DeleteRenderbuffers, n, renderbuffers)
    member x.DeleteSampler(samplers : int) =
        x.Append(InstructionCode.DeleteSampler, samplers)
    member x.DeleteSamplers(count : int, samplers : nativeptr<int>) =
        x.Append(InstructionCode.DeleteSamplers, count, samplers)
    member x.DeleteShader(shader : int) =
        x.Append(InstructionCode.DeleteShader, shader)
    member x.DeleteSync(sync : nativeint) =
        x.Append(InstructionCode.DeleteSync, sync)
    member x.DeleteTexture(textures : int) =
        x.Append(InstructionCode.DeleteTexture, textures)
    member x.DeleteTextures(n : int, textures : nativeptr<int>) =
        x.Append(InstructionCode.DeleteTextures, n, textures)
    member x.DeleteTransformFeedback(ids : int) =
        x.Append(InstructionCode.DeleteTransformFeedback, ids)
    member x.DeleteTransformFeedbacks(n : int, ids : nativeptr<int>) =
        x.Append(InstructionCode.DeleteTransformFeedbacks, n, ids)
    member x.DeleteVertexArray(arrays : int) =
        x.Append(InstructionCode.DeleteVertexArray, arrays)
    member x.DeleteVertexArrays(n : int, arrays : nativeptr<int>) =
        x.Append(InstructionCode.DeleteVertexArrays, n, arrays)
    member x.DepthFunc(func : DepthFunction) =
        x.Append(InstructionCode.DepthFunc, func)
    member x.DepthMask(flag : bool) =
        x.Append(InstructionCode.DepthMask, (if flag then 1 else 0))
    member x.DepthRange(near : float, far : float) =
        x.Append(InstructionCode.DepthRange, near, far)
    member x.DepthRangeArray(first : int, count : int, v : nativeptr<float>) =
        x.Append(InstructionCode.DepthRangeArray, first, count, v)
    member x.DepthRangeIndexed(index : int, n : float, f : float) =
        x.Append(InstructionCode.DepthRangeIndexed, index, n, f)
    member x.DetachShader(program : int, shader : int) =
        x.Append(InstructionCode.DetachShader, program, shader)
    member x.Disable(target : IndexedEnableCap, index : int) =
        x.Append(InstructionCode.Disable, target, index)
    member x.DisableClientState(array : ArrayCap, index : int) =
        x.Append(InstructionCode.DisableClientState, array, index)
    member x.DisableClientStateIndexed(array : ArrayCap, index : int) =
        x.Append(InstructionCode.DisableClientStateIndexed, array, index)
    member x.DisableIndexed(target : IndexedEnableCap, index : int) =
        x.Append(InstructionCode.DisableIndexed, target, index)
    member x.DisableVertexArray(vaobj : int, array : EnableCap) =
        x.Append(InstructionCode.DisableVertexArray, vaobj, array)
    member x.DisableVertexArrayAttrib(vaobj : int, index : int) =
        x.Append(InstructionCode.DisableVertexArrayAttrib, vaobj, index)
    member x.DisableVertexAttribArray(index : int) =
        x.Append(InstructionCode.DisableVertexAttribArray, index)
    member x.DispatchCompute(num_groups_x : int, num_groups_y : int, num_groups_z : int) =
        x.Append(InstructionCode.DispatchCompute, num_groups_x, num_groups_y, num_groups_z)
    member x.DispatchComputeGroupSize(num_groups_x : int, num_groups_y : int, num_groups_z : int, group_size_x : int, group_size_y : int, group_size_z : int) =
        x.Append(InstructionCode.DispatchComputeGroupSize, num_groups_x, num_groups_y, num_groups_z, group_size_x, group_size_y, group_size_z)
    member x.DispatchComputeIndirect(indirect : nativeint) =
        x.Append(InstructionCode.DispatchComputeIndirect, indirect)
    member x.DrawArrays(mode : PrimitiveType, first : int, count : int) =
        x.Append(InstructionCode.DrawArrays, mode, first, count)
    member x.DrawArraysIndirect(mode : PrimitiveType, indirect : nativeint) =
        x.Append(InstructionCode.DrawArraysIndirect, mode, indirect)
    member x.DrawArraysInstanced(mode : PrimitiveType, first : int, count : int, instancecount : int) =
        x.Append(InstructionCode.DrawArraysInstanced, mode, first, count, instancecount)
    member x.DrawArraysInstancedBaseInstance(mode : PrimitiveType, first : int, count : int, instancecount : int, baseinstance : int) =
        x.Append(InstructionCode.DrawArraysInstancedBaseInstance, mode, first, count, instancecount, baseinstance)
    member x.DrawBuffer(buf : DrawBufferMode) =
        x.Append(InstructionCode.DrawBuffer, buf)
    member x.DrawBuffers(n : int, bufs : nativeptr<DrawBuffersEnum>) =
        x.Append(InstructionCode.DrawBuffers, n, bufs)
    member x.DrawElements(mode : BeginMode, count : int, _type : DrawElementsType, offset : int) =
        x.Append(InstructionCode.DrawElements, mode, count, _type, offset)
    member x.DrawElementsBaseVertex(mode : PrimitiveType, count : int, _type : DrawElementsType, indices : nativeint, basevertex : int) =
        x.Append(InstructionCode.DrawElementsBaseVertex, mode, count, _type, indices, basevertex)
    member x.DrawElementsIndirect(mode : PrimitiveType, _type : DrawElementsType, indirect : nativeint) =
        x.Append(InstructionCode.DrawElementsIndirect, mode, _type, indirect)
    member x.DrawElementsInstanced(mode : PrimitiveType, count : int, _type : DrawElementsType, indices : nativeint, instancecount : int) =
        x.Append(InstructionCode.DrawElementsInstanced, mode, count, _type, indices, instancecount)
    member x.DrawElementsInstancedBaseInstance(mode : PrimitiveType, count : int, _type : DrawElementsType, indices : nativeint, instancecount : int, baseinstance : int) =
        x.Append(InstructionCode.DrawElementsInstancedBaseInstance, mode, count, _type, indices, instancecount, baseinstance)
    member x.DrawElementsInstancedBaseVertex(mode : PrimitiveType, count : int, _type : DrawElementsType, indices : nativeint, instancecount : int, basevertex : int) =
        x.Append(InstructionCode.DrawElementsInstancedBaseVertex, mode, count, _type, indices, instancecount, basevertex)
    member x.DrawElementsInstancedBaseVertexBaseInstance(mode : PrimitiveType, count : int, _type : DrawElementsType, indices : nativeint, instancecount : int, basevertex : int, baseinstance : int) =
        x.Append(InstructionCode.DrawElementsInstancedBaseVertexBaseInstance, mode, count, _type, indices, instancecount, basevertex, baseinstance)
    member x.DrawRangeElements(mode : PrimitiveType, start : int, _end : int, count : int, _type : DrawElementsType, indices : nativeint) =
        x.Append(InstructionCode.DrawRangeElements, mode, start, _end, count, _type, indices)
    member x.DrawRangeElementsBaseVertex(mode : PrimitiveType, start : int, _end : int, count : int, _type : DrawElementsType, indices : nativeint, basevertex : int) =
        x.Append(InstructionCode.DrawRangeElementsBaseVertex, mode, start, _end, count, _type, indices, basevertex)
    member x.DrawTransformFeedback(mode : PrimitiveType, id : int) =
        x.Append(InstructionCode.DrawTransformFeedback, mode, id)
    member x.DrawTransformFeedbackInstanced(mode : PrimitiveType, id : int, instancecount : int) =
        x.Append(InstructionCode.DrawTransformFeedbackInstanced, mode, id, instancecount)
    member x.DrawTransformFeedbackStream(mode : PrimitiveType, id : int, stream : int) =
        x.Append(InstructionCode.DrawTransformFeedbackStream, mode, id, stream)
    member x.DrawTransformFeedbackStreamInstanced(mode : PrimitiveType, id : int, stream : int, instancecount : int) =
        x.Append(InstructionCode.DrawTransformFeedbackStreamInstanced, mode, id, stream, instancecount)
    member x.Enable(target : IndexedEnableCap, index : int) =
        x.Append(InstructionCode.Enable, target, index)
    member x.EnableClientState(array : ArrayCap, index : int) =
        x.Append(InstructionCode.EnableClientState, array, index)
    member x.EnableClientStateIndexed(array : ArrayCap, index : int) =
        x.Append(InstructionCode.EnableClientStateIndexed, array, index)
    member x.EnableIndexed(target : IndexedEnableCap, index : int) =
        x.Append(InstructionCode.EnableIndexed, target, index)
    member x.EnableVertexArray(vaobj : int, array : EnableCap) =
        x.Append(InstructionCode.EnableVertexArray, vaobj, array)
    member x.EnableVertexArrayAttrib(vaobj : int, index : int) =
        x.Append(InstructionCode.EnableVertexArrayAttrib, vaobj, index)
    member x.EnableVertexAttribArray(index : int) =
        x.Append(InstructionCode.EnableVertexAttribArray, index)
    member x.EndConditionalRender() =
        x.Append(InstructionCode.EndConditionalRender)
    member x.EndQuery(target : QueryTarget) =
        x.Append(InstructionCode.EndQuery, target)
    member x.EndQueryIndexed(target : QueryTarget, index : int) =
        x.Append(InstructionCode.EndQueryIndexed, target, index)
    member x.EndTransformFeedback() =
        x.Append(InstructionCode.EndTransformFeedback)
    member x.EvaluateDepthValues() =
        x.Append(InstructionCode.EvaluateDepthValues)
    member x.Finish() =
        x.Append(InstructionCode.Finish)
    member x.Flush() =
        x.Append(InstructionCode.Flush)
    member x.FlushMappedBufferRange(target : BufferTarget, offset : nativeint, length : int) =
        x.Append(InstructionCode.FlushMappedBufferRange, target, offset, length)
    member x.FlushMappedNamedBufferRange(buffer : int, offset : nativeint, length : int) =
        x.Append(InstructionCode.FlushMappedNamedBufferRange, buffer, offset, length)
    member x.FramebufferDrawBuffer(framebuffer : int, mode : DrawBufferMode) =
        x.Append(InstructionCode.FramebufferDrawBuffer, framebuffer, mode)
    member x.FramebufferDrawBuffers(framebuffer : int, n : int, bufs : nativeptr<DrawBufferMode>) =
        x.Append(InstructionCode.FramebufferDrawBuffers, framebuffer, n, bufs)
    member x.FramebufferParameter(target : FramebufferTarget, pname : FramebufferDefaultParameter, param : int) =
        x.Append(InstructionCode.FramebufferParameter, target, pname, param)
    member x.FramebufferReadBuffer(framebuffer : int, mode : ReadBufferMode) =
        x.Append(InstructionCode.FramebufferReadBuffer, framebuffer, mode)
    member x.FramebufferRenderbuffer(target : FramebufferTarget, attachment : FramebufferAttachment, renderbuffertarget : RenderbufferTarget, renderbuffer : int) =
        x.Append(InstructionCode.FramebufferRenderbuffer, target, attachment, renderbuffertarget, renderbuffer)
    member x.FramebufferSampleLocations(target : FramebufferTarget, start : int, count : int, v : nativeptr<float32>) =
        x.Append(InstructionCode.FramebufferSampleLocations, target, start, count, v)
    member x.FramebufferTexture(target : FramebufferTarget, attachment : FramebufferAttachment, texture : int, level : int) =
        x.Append(InstructionCode.FramebufferTexture, target, attachment, texture, level)
    member x.FramebufferTexture1D(target : FramebufferTarget, attachment : FramebufferAttachment, textarget : TextureTarget, texture : int, level : int) =
        x.Append(InstructionCode.FramebufferTexture1D, target, attachment, textarget, texture, level)
    member x.FramebufferTexture2D(target : FramebufferTarget, attachment : FramebufferAttachment, textarget : TextureTarget, texture : int, level : int) =
        x.Append(InstructionCode.FramebufferTexture2D, target, attachment, textarget, texture, level)
    member x.FramebufferTexture3D(target : FramebufferTarget, attachment : FramebufferAttachment, textarget : TextureTarget, texture : int, level : int, zoffset : int) =
        x.Append(InstructionCode.FramebufferTexture3D, target, attachment, textarget, texture, level, zoffset)
    member x.FramebufferTextureFace(target : FramebufferTarget, attachment : FramebufferAttachment, texture : int, level : int, face : TextureTarget) =
        x.Append(InstructionCode.FramebufferTextureFace, target, attachment, texture, level, face)
    member x.FramebufferTextureLayer(target : FramebufferTarget, attachment : FramebufferAttachment, texture : int, level : int, layer : int) =
        x.Append(InstructionCode.FramebufferTextureLayer, target, attachment, texture, level, layer)
    member x.FrontFace(mode : FrontFaceDirection) =
        x.Append(InstructionCode.FrontFace, mode)
    member x.GenBuffers(n : int, buffers : nativeptr<int>) =
        x.Append(InstructionCode.GenBuffers, n, buffers)
    member x.GenFramebuffers(n : int, framebuffers : nativeptr<int>) =
        x.Append(InstructionCode.GenFramebuffers, n, framebuffers)
    member x.GenProgramPipelines(n : int, pipelines : nativeptr<int>) =
        x.Append(InstructionCode.GenProgramPipelines, n, pipelines)
    member x.GenQueries(n : int, ids : nativeptr<int>) =
        x.Append(InstructionCode.GenQueries, n, ids)
    member x.GenRenderbuffers(n : int, renderbuffers : nativeptr<int>) =
        x.Append(InstructionCode.GenRenderbuffers, n, renderbuffers)
    member x.GenSamplers(count : int, samplers : nativeptr<int>) =
        x.Append(InstructionCode.GenSamplers, count, samplers)
    member x.GenTextures(n : int, textures : nativeptr<int>) =
        x.Append(InstructionCode.GenTextures, n, textures)
    member x.GenTransformFeedbacks(n : int, ids : nativeptr<int>) =
        x.Append(InstructionCode.GenTransformFeedbacks, n, ids)
    member x.GenVertexArrays(n : int, arrays : nativeptr<int>) =
        x.Append(InstructionCode.GenVertexArrays, n, arrays)
    member x.GenerateMipmap(target : GenerateMipmapTarget) =
        x.Append(InstructionCode.GenerateMipmap, target)
    member x.GenerateMultiTexMipmap(texunit : TextureUnit, target : TextureTarget) =
        x.Append(InstructionCode.GenerateMultiTexMipmap, texunit, target)
    member x.GenerateTextureMipmap(texture : int, target : TextureTarget) =
        x.Append(InstructionCode.GenerateTextureMipmap, texture, target)
    member x.GetActiveAtomicCounterBuffer(program : int, bufferIndex : int, pname : AtomicCounterBufferParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetActiveAtomicCounterBuffer, program, bufferIndex, pname, _params)
    member x.GetActiveSubroutineUniform(program : int, shadertype : ShaderType, index : int, pname : ActiveSubroutineUniformParameter, values : nativeptr<int>) =
        x.Append(InstructionCode.GetActiveSubroutineUniform, program, shadertype, index, pname, values)
    member x.GetActiveUniformBlock(program : int, uniformBlockIndex : int, pname : ActiveUniformBlockParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetActiveUniformBlock, program, uniformBlockIndex, pname, _params)
    member x.GetActiveUniforms(program : int, uniformCount : int, uniformIndices : nativeptr<int>, pname : ActiveUniformParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetActiveUniforms, program, uniformCount, uniformIndices, pname, _params)
    member x.GetAttachedShaders(program : int, maxCount : int, count : nativeptr<int>, shaders : nativeptr<int>) =
        x.Append(InstructionCode.GetAttachedShaders, program, maxCount, count, shaders)
    member x.GetBoolean(target : GetIndexedPName, index : int, data : nativeptr<bool>) =
        x.Append(InstructionCode.GetBoolean, target, index, data)
    member x.GetBooleanIndexed(target : BufferTargetArb, index : int, data : nativeptr<bool>) =
        x.Append(InstructionCode.GetBooleanIndexed, target, index, data)
    member x.GetBufferParameter(target : BufferTarget, pname : BufferParameterName, _params : nativeptr<int64>) =
        x.Append(InstructionCode.GetBufferParameter, target, pname, _params)
    member x.GetBufferPointer(target : BufferTarget, pname : BufferPointer, _params : nativeint) =
        x.Append(InstructionCode.GetBufferPointer, target, pname, _params)
    member x.GetBufferSubData(target : BufferTarget, offset : nativeint, size : int, data : nativeint) =
        x.Append(InstructionCode.GetBufferSubData, target, offset, size, data)
    member x.GetColorTable(target : ColorTableTarget, format : PixelFormat, _type : PixelType, table : nativeint) =
        x.Append(InstructionCode.GetColorTable, target, format, _type, table)
    member x.GetColorTableParameter(target : ColorTableTarget, pname : GetColorTableParameterPNameSgi, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetColorTableParameter, target, pname, _params)
    member x.GetCompressedMultiTexImage(texunit : TextureUnit, target : TextureTarget, lod : int, img : nativeint) =
        x.Append(InstructionCode.GetCompressedMultiTexImage, texunit, target, lod, img)
    member x.GetCompressedTexImage(target : TextureTarget, level : int, img : nativeint) =
        x.Append(InstructionCode.GetCompressedTexImage, target, level, img)
    member x.GetCompressedTextureImage(texture : int, level : int, bufSize : int, pixels : nativeint) =
        x.Append(InstructionCode.GetCompressedTextureImage, texture, level, bufSize, pixels)
    member x.GetCompressedTextureSubImage(texture : int, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, bufSize : int, pixels : nativeint) =
        x.Append(InstructionCode.GetCompressedTextureSubImage, texture, level, xoffset, yoffset, zoffset, width, height, depth, bufSize, pixels)
    member x.GetConvolutionFilter(target : ConvolutionTarget, format : PixelFormat, _type : PixelType, image : nativeint) =
        x.Append(InstructionCode.GetConvolutionFilter, target, format, _type, image)
    member x.GetConvolutionParameter(target : ConvolutionTarget, pname : ConvolutionParameterExt, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetConvolutionParameter, target, pname, _params)
    member x.GetDouble(target : GetIndexedPName, index : int, data : nativeptr<float>) =
        x.Append(InstructionCode.GetDouble, target, index, data)
    member x.GetDoubleIndexed(target : TypeEnum, index : int, data : nativeptr<float>) =
        x.Append(InstructionCode.GetDoubleIndexed, target, index, data)
    member x.GetFloat(target : GetIndexedPName, index : int, data : nativeptr<float32>) =
        x.Append(InstructionCode.GetFloat, target, index, data)
    member x.GetFloatIndexed(target : TypeEnum, index : int, data : nativeptr<float32>) =
        x.Append(InstructionCode.GetFloatIndexed, target, index, data)
    member x.GetFramebufferAttachmentParameter(target : FramebufferTarget, attachment : FramebufferAttachment, pname : FramebufferParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetFramebufferAttachmentParameter, target, attachment, pname, _params)
    member x.GetFramebufferParameter(target : FramebufferTarget, pname : FramebufferDefaultParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetFramebufferParameter, target, pname, _params)
    member x.GetHistogram(target : HistogramTargetExt, reset : bool, format : PixelFormat, _type : PixelType, values : nativeint) =
        x.Append(InstructionCode.GetHistogram, target, (if reset then 1 else 0), format, _type, values)
    member x.GetHistogramParameter(target : HistogramTargetExt, pname : GetHistogramParameterPNameExt, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetHistogramParameter, target, pname, _params)
    member x.GetInteger(target : GetIndexedPName, index : int, data : nativeptr<int>) =
        x.Append(InstructionCode.GetInteger, target, index, data)
    member x.GetInteger64(target : GetIndexedPName, index : int, data : nativeptr<int64>) =
        x.Append(InstructionCode.GetInteger64, target, index, data)
    member x.GetIntegerIndexed(target : GetIndexedPName, index : int, data : nativeptr<int>) =
        x.Append(InstructionCode.GetIntegerIndexed, target, index, data)
    member x.GetInternalformat(target : ImageTarget, internalformat : SizedInternalFormat, pname : InternalFormatParameter, bufSize : int, _params : nativeptr<int64>) =
        x.Append(InstructionCode.GetInternalformat, target, internalformat, pname, bufSize, _params)
    member x.GetMinmax(target : MinmaxTargetExt, reset : bool, format : PixelFormat, _type : PixelType, values : nativeint) =
        x.Append(InstructionCode.GetMinmax, target, (if reset then 1 else 0), format, _type, values)
    member x.GetMinmaxParameter(target : MinmaxTargetExt, pname : GetMinmaxParameterPNameExt, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetMinmaxParameter, target, pname, _params)
    member x.GetMultiTexEnv(texunit : TextureUnit, target : TextureEnvTarget, pname : TextureEnvParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetMultiTexEnv, texunit, target, pname, _params)
    member x.GetMultiTexGen(texunit : TextureUnit, coord : TextureCoordName, pname : TextureGenParameter, _params : nativeptr<float>) =
        x.Append(InstructionCode.GetMultiTexGen, texunit, coord, pname, _params)
    member x.GetMultiTexImage(texunit : TextureUnit, target : TextureTarget, level : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.GetMultiTexImage, texunit, target, level, format, _type, pixels)
    member x.GetMultiTexLevelParameter(texunit : TextureUnit, target : TextureTarget, level : int, pname : GetTextureParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetMultiTexLevelParameter, texunit, target, level, pname, _params)
    member x.GetMultiTexParameter(texunit : TextureUnit, target : TextureTarget, pname : GetTextureParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetMultiTexParameter, texunit, target, pname, _params)
    member x.GetMultiTexParameterI(texunit : TextureUnit, target : TextureTarget, pname : GetTextureParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetMultiTexParameterI, texunit, target, pname, _params)
    member x.GetMultisample(pname : GetMultisamplePName, index : int, _val : nativeptr<float32>) =
        x.Append(InstructionCode.GetMultisample, pname, index, _val)
    member x.GetNamedBufferParameter(buffer : int, pname : BufferParameterName, _params : nativeptr<int64>) =
        x.Append(InstructionCode.GetNamedBufferParameter, buffer, pname, _params)
    member x.GetNamedBufferPointer(buffer : int, pname : BufferPointer, _params : nativeint) =
        x.Append(InstructionCode.GetNamedBufferPointer, buffer, pname, _params)
    member x.GetNamedBufferSubData(buffer : int, offset : nativeint, size : int, data : nativeint) =
        x.Append(InstructionCode.GetNamedBufferSubData, buffer, offset, size, data)
    member x.GetNamedFramebufferAttachmentParameter(framebuffer : int, attachment : FramebufferAttachment, pname : FramebufferParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetNamedFramebufferAttachmentParameter, framebuffer, attachment, pname, _params)
    member x.GetNamedFramebufferParameter(framebuffer : int, pname : FramebufferDefaultParameter, param : nativeptr<int>) =
        x.Append(InstructionCode.GetNamedFramebufferParameter, framebuffer, pname, param)
    member x.GetNamedProgram(program : int, target : All, pname : ProgramPropertyArb, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetNamedProgram, program, target, pname, _params)
    member x.GetNamedProgramLocalParameter(program : int, target : All, index : int, _params : nativeptr<float>) =
        x.Append(InstructionCode.GetNamedProgramLocalParameter, program, target, index, _params)
    member x.GetNamedProgramLocalParameterI(program : int, target : All, index : int, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetNamedProgramLocalParameterI, program, target, index, _params)
    member x.GetNamedProgramString(program : int, target : All, pname : All, string : nativeint) =
        x.Append(InstructionCode.GetNamedProgramString, program, target, pname, string)
    member x.GetNamedRenderbufferParameter(renderbuffer : int, pname : RenderbufferParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetNamedRenderbufferParameter, renderbuffer, pname, _params)
    member x.GetPointer(pname : TypeEnum, index : int, _params : nativeint) =
        x.Append(InstructionCode.GetPointer, pname, index, _params)
    member x.GetPointerIndexed(target : TypeEnum, index : int, data : nativeint) =
        x.Append(InstructionCode.GetPointerIndexed, target, index, data)
    member x.GetProgram(program : int, pname : GetProgramParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetProgram, program, pname, _params)
    member x.GetProgramBinary(program : int, bufSize : int, length : nativeptr<int>, binaryFormat : nativeptr<BinaryFormat>, binary : nativeint) =
        x.Append(InstructionCode.GetProgramBinary, program, bufSize, length, binaryFormat, binary)
    member x.GetProgramInterface(program : int, programInterface : ProgramInterface, pname : ProgramInterfaceParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetProgramInterface, program, programInterface, pname, _params)
    member x.GetProgramPipeline(pipeline : int, pname : ProgramPipelineParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetProgramPipeline, pipeline, pname, _params)
    member x.GetProgramResource(program : int, programInterface : ProgramInterface, index : int, propCount : int, props : nativeptr<ProgramProperty>, bufSize : int, length : nativeptr<int>, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetProgramResource, program, programInterface, index, propCount, props, bufSize, length, _params)
    member x.GetProgramStage(program : int, shadertype : ShaderType, pname : ProgramStageParameter, values : nativeptr<int>) =
        x.Append(InstructionCode.GetProgramStage, program, shadertype, pname, values)
    member x.GetQuery(target : QueryTarget, pname : GetQueryParam, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetQuery, target, pname, _params)
    member x.GetQueryBufferObject(id : int, buffer : int, pname : QueryObjectParameterName, offset : nativeint) =
        x.Append(InstructionCode.GetQueryBufferObject, id, buffer, pname, offset)
    member x.GetQueryIndexed(target : QueryTarget, index : int, pname : GetQueryParam, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetQueryIndexed, target, index, pname, _params)
    member x.GetQueryObject(id : int, pname : GetQueryObjectParam, _params : nativeptr<int64>) =
        x.Append(InstructionCode.GetQueryObject, id, pname, _params)
    member x.GetRenderbufferParameter(target : RenderbufferTarget, pname : RenderbufferParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetRenderbufferParameter, target, pname, _params)
    member x.GetSamplerParameter(sampler : int, pname : SamplerParameterName, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetSamplerParameter, sampler, pname, _params)
    member x.GetSamplerParameterI(sampler : int, pname : SamplerParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetSamplerParameterI, sampler, pname, _params)
    member x.GetSeparableFilter(target : SeparableTargetExt, format : PixelFormat, _type : PixelType, row : nativeint, column : nativeint, span : nativeint) =
        x.Append(InstructionCode.GetSeparableFilter, target, format, _type, row, column, span)
    member x.GetShader(shader : int, pname : ShaderParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetShader, shader, pname, _params)
    member x.GetShaderPrecisionFormat(shadertype : ShaderType, precisiontype : ShaderPrecision, range : nativeptr<int>, precision : nativeptr<int>) =
        x.Append(InstructionCode.GetShaderPrecisionFormat, shadertype, precisiontype, range, precision)
    member x.GetSync(sync : nativeint, pname : SyncParameterName, bufSize : int, length : nativeptr<int>, values : nativeptr<int>) =
        x.Append(InstructionCode.GetSync, sync, pname, bufSize, length, values)
    member x.GetTexImage(target : TextureTarget, level : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.GetTexImage, target, level, format, _type, pixels)
    member x.GetTexLevelParameter(target : TextureTarget, level : int, pname : GetTextureParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetTexLevelParameter, target, level, pname, _params)
    member x.GetTexParameter(target : TextureTarget, pname : GetTextureParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetTexParameter, target, pname, _params)
    member x.GetTexParameterI(target : TextureTarget, pname : GetTextureParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetTexParameterI, target, pname, _params)
    member x.GetTextureImage(texture : int, level : int, format : PixelFormat, _type : PixelType, bufSize : int, pixels : nativeint) =
        x.Append(InstructionCode.GetTextureImage, texture, level, format, _type, bufSize, pixels)
    member x.GetTextureLevelParameter(texture : int, target : TextureTarget, level : int, pname : GetTextureParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetTextureLevelParameter, texture, target, level, pname, _params)
    member x.GetTextureParameter(texture : int, target : TextureTarget, pname : GetTextureParameter, _params : nativeptr<float32>) =
        x.Append(InstructionCode.GetTextureParameter, texture, target, pname, _params)
    member x.GetTextureParameterI(texture : int, target : TextureTarget, pname : GetTextureParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetTextureParameterI, texture, target, pname, _params)
    member x.GetTextureSubImage(texture : int, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, _type : PixelType, bufSize : int, pixels : nativeint) =
        x.Append(InstructionCode.GetTextureSubImage, texture, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, bufSize, pixels)
    member x.GetTransformFeedback(xfb : int, pname : TransformFeedbackIndexedParameter, index : int, param : nativeptr<int>) =
        x.Append(InstructionCode.GetTransformFeedback, xfb, pname, index, param)
    member x.GetTransformFeedbacki64_(xfb : int, pname : TransformFeedbackIndexedParameter, index : int, param : nativeptr<int64>) =
        x.Append(InstructionCode.GetTransformFeedbacki64_, xfb, pname, index, param)
    member x.GetUniform(program : int, location : int, _params : nativeptr<float>) =
        x.Append(InstructionCode.GetUniform, program, location, _params)
    member x.GetUniformSubroutine(shadertype : ShaderType, location : int, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetUniformSubroutine, shadertype, location, _params)
    member x.GetVertexArray(vaobj : int, pname : VertexArrayParameter, param : nativeptr<int>) =
        x.Append(InstructionCode.GetVertexArray, vaobj, pname, param)
    member x.GetVertexArrayIndexed(vaobj : int, index : int, pname : VertexArrayIndexedParameter, param : nativeptr<int>) =
        x.Append(InstructionCode.GetVertexArrayIndexed, vaobj, index, pname, param)
    member x.GetVertexArrayIndexed64(vaobj : int, index : int, pname : VertexArrayIndexed64Parameter, param : nativeptr<int64>) =
        x.Append(InstructionCode.GetVertexArrayIndexed64, vaobj, index, pname, param)
    member x.GetVertexArrayInteger(vaobj : int, index : int, pname : VertexArrayPName, param : nativeptr<int>) =
        x.Append(InstructionCode.GetVertexArrayInteger, vaobj, index, pname, param)
    member x.GetVertexArrayPointer(vaobj : int, index : int, pname : VertexArrayPName, param : nativeint) =
        x.Append(InstructionCode.GetVertexArrayPointer, vaobj, index, pname, param)
    member x.GetVertexAttrib(index : int, pname : VertexAttribParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetVertexAttrib, index, pname, _params)
    member x.GetVertexAttribI(index : int, pname : VertexAttribParameter, _params : nativeptr<int>) =
        x.Append(InstructionCode.GetVertexAttribI, index, pname, _params)
    member x.GetVertexAttribL(index : int, pname : VertexAttribParameter, _params : nativeptr<float>) =
        x.Append(InstructionCode.GetVertexAttribL, index, pname, _params)
    member x.GetVertexAttribPointer(index : int, pname : VertexAttribPointerParameter, pointer : nativeint) =
        x.Append(InstructionCode.GetVertexAttribPointer, index, pname, pointer)
    member x.GetnColorTable(target : ColorTableTarget, format : PixelFormat, _type : PixelType, bufSize : int, table : nativeint) =
        x.Append(InstructionCode.GetnColorTable, target, format, _type, bufSize, table)
    member x.GetnCompressedTexImage(target : TextureTarget, lod : int, bufSize : int, pixels : nativeint) =
        x.Append(InstructionCode.GetnCompressedTexImage, target, lod, bufSize, pixels)
    member x.GetnConvolutionFilter(target : ConvolutionTarget, format : PixelFormat, _type : PixelType, bufSize : int, image : nativeint) =
        x.Append(InstructionCode.GetnConvolutionFilter, target, format, _type, bufSize, image)
    member x.GetnHistogram(target : HistogramTargetExt, reset : bool, format : PixelFormat, _type : PixelType, bufSize : int, values : nativeint) =
        x.Append(InstructionCode.GetnHistogram, target, (if reset then 1 else 0), format, _type, bufSize, values)
    member x.GetnMap(target : MapTarget, query : MapQuery, bufSize : int, v : nativeptr<float>) =
        x.Append(InstructionCode.GetnMap, target, query, bufSize, v)
    member x.GetnMinmax(target : MinmaxTargetExt, reset : bool, format : PixelFormat, _type : PixelType, bufSize : int, values : nativeint) =
        x.Append(InstructionCode.GetnMinmax, target, (if reset then 1 else 0), format, _type, bufSize, values)
    member x.GetnPixelMap(map : PixelMap, bufSize : int, values : nativeptr<float32>) =
        x.Append(InstructionCode.GetnPixelMap, map, bufSize, values)
    member x.GetnPolygonStipple(bufSize : int, pattern : nativeptr<byte>) =
        x.Append(InstructionCode.GetnPolygonStipple, bufSize, pattern)
    member x.GetnSeparableFilter(target : SeparableTargetExt, format : PixelFormat, _type : PixelType, rowBufSize : int, row : nativeint, columnBufSize : int, column : nativeint, span : nativeint) =
        x.Append(InstructionCode.GetnSeparableFilter, target, format, _type, rowBufSize, row, columnBufSize, column, span)
    member x.GetnTexImage(target : TextureTarget, level : int, format : PixelFormat, _type : PixelType, bufSize : int, pixels : nativeint) =
        x.Append(InstructionCode.GetnTexImage, target, level, format, _type, bufSize, pixels)
    member x.GetnUniform(program : int, location : int, bufSize : int, _params : nativeptr<float>) =
        x.Append(InstructionCode.GetnUniform, program, location, bufSize, _params)
    member x.Hint(target : HintTarget, mode : HintMode) =
        x.Append(InstructionCode.Hint, target, mode)
    member x.Histogram(target : HistogramTargetExt, width : int, internalformat : InternalFormat, sink : bool) =
        x.Append(InstructionCode.Histogram, target, width, internalformat, (if sink then 1 else 0))
    member x.InvalidateBufferData(buffer : int) =
        x.Append(InstructionCode.InvalidateBufferData, buffer)
    member x.InvalidateBufferSubData(buffer : int, offset : nativeint, length : int) =
        x.Append(InstructionCode.InvalidateBufferSubData, buffer, offset, length)
    member x.InvalidateFramebuffer(target : FramebufferTarget, numAttachments : int, attachments : nativeptr<FramebufferAttachment>) =
        x.Append(InstructionCode.InvalidateFramebuffer, target, numAttachments, attachments)
    member x.InvalidateNamedFramebufferData(framebuffer : int, numAttachments : int, attachments : nativeptr<FramebufferAttachment>) =
        x.Append(InstructionCode.InvalidateNamedFramebufferData, framebuffer, numAttachments, attachments)
    member x.InvalidateNamedFramebufferSubData(framebuffer : int, numAttachments : int, attachments : nativeptr<FramebufferAttachment>, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.InvalidateNamedFramebufferSubData, framebuffer, numAttachments, attachments, _x, y, width, height)
    member x.InvalidateSubFramebuffer(target : FramebufferTarget, numAttachments : int, attachments : nativeptr<FramebufferAttachment>, _x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.InvalidateSubFramebuffer, target, numAttachments, attachments, _x, y, width, height)
    member x.InvalidateTexImage(texture : int, level : int) =
        x.Append(InstructionCode.InvalidateTexImage, texture, level)
    member x.InvalidateTexSubImage(texture : int, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int) =
        x.Append(InstructionCode.InvalidateTexSubImage, texture, level, xoffset, yoffset, zoffset, width, height, depth)
    member x.LineWidth(width : float32) =
        x.Append(InstructionCode.LineWidth, width)
    member x.LinkProgram(program : int) =
        x.Append(InstructionCode.LinkProgram, program)
    member x.LogicOp(opcode : LogicOp) =
        x.Append(InstructionCode.LogicOp, opcode)
    member x.MakeImageHandleNonResident(handle : int64) =
        x.Append(InstructionCode.MakeImageHandleNonResident, handle)
    member x.MakeImageHandleResident(handle : int64, access : All) =
        x.Append(InstructionCode.MakeImageHandleResident, handle, access)
    member x.MakeTextureHandleNonResident(handle : int64) =
        x.Append(InstructionCode.MakeTextureHandleNonResident, handle)
    member x.MakeTextureHandleResident(handle : int64) =
        x.Append(InstructionCode.MakeTextureHandleResident, handle)
    member x.MatrixFrustum(mode : MatrixMode, left : float, right : float, bottom : float, top : float, zNear : float, zFar : float) =
        x.Append(InstructionCode.MatrixFrustum, mode, left, right, bottom, top, zNear, zFar)
    member x.MatrixLoad(mode : MatrixMode, m : nativeptr<float>) =
        x.Append(InstructionCode.MatrixLoad, mode, m)
    member x.MatrixLoadIdentity(mode : MatrixMode) =
        x.Append(InstructionCode.MatrixLoadIdentity, mode)
    member x.MatrixLoadTranspose(mode : MatrixMode, m : nativeptr<float>) =
        x.Append(InstructionCode.MatrixLoadTranspose, mode, m)
    member x.MatrixMult(mode : MatrixMode, m : nativeptr<float>) =
        x.Append(InstructionCode.MatrixMult, mode, m)
    member x.MatrixMultTranspose(mode : MatrixMode, m : nativeptr<float>) =
        x.Append(InstructionCode.MatrixMultTranspose, mode, m)
    member x.MatrixOrtho(mode : MatrixMode, left : float, right : float, bottom : float, top : float, zNear : float, zFar : float) =
        x.Append(InstructionCode.MatrixOrtho, mode, left, right, bottom, top, zNear, zFar)
    member x.MatrixPop(mode : MatrixMode) =
        x.Append(InstructionCode.MatrixPop, mode)
    member x.MatrixPush(mode : MatrixMode) =
        x.Append(InstructionCode.MatrixPush, mode)
    member x.MatrixRotate(mode : MatrixMode, angle : float, _x : float, y : float, z : float) =
        x.Append(InstructionCode.MatrixRotate, mode, angle, _x, y, z)
    member x.MatrixScale(mode : MatrixMode, _x : float, y : float, z : float) =
        x.Append(InstructionCode.MatrixScale, mode, _x, y, z)
    member x.MatrixTranslate(mode : MatrixMode, _x : float, y : float, z : float) =
        x.Append(InstructionCode.MatrixTranslate, mode, _x, y, z)
    member x.MaxShaderCompilerThreads(count : int) =
        x.Append(InstructionCode.MaxShaderCompilerThreads, count)
    member x.MemoryBarrier(barriers : MemoryBarrierFlags) =
        x.Append(InstructionCode.MemoryBarrier, barriers)
    member x.MemoryBarrierByRegion(barriers : MemoryBarrierRegionFlags) =
        x.Append(InstructionCode.MemoryBarrierByRegion, barriers)
    member x.MinSampleShading(value : float32) =
        x.Append(InstructionCode.MinSampleShading, value)
    member x.Minmax(target : MinmaxTargetExt, internalformat : InternalFormat, sink : bool) =
        x.Append(InstructionCode.Minmax, target, internalformat, (if sink then 1 else 0))
    member x.MultiDrawArrays(mode : PrimitiveType, first : nativeptr<int>, count : nativeptr<int>, drawcount : int) =
        x.Append(InstructionCode.MultiDrawArrays, mode, first, count, drawcount)
    member x.MultiDrawArraysIndirect(mode : PrimitiveType, indirect : nativeint, drawcount : int, stride : int) =
        x.Append(InstructionCode.MultiDrawArraysIndirect, mode, indirect, drawcount, stride)
    member x.MultiDrawArraysIndirectCount(mode : PrimitiveType, indirect : nativeint, drawcount : nativeint, maxdrawcount : int, stride : int) =
        x.Append(InstructionCode.MultiDrawArraysIndirectCount, mode, indirect, drawcount, maxdrawcount, stride)
    member x.MultiDrawElements(mode : PrimitiveType, count : nativeptr<int>, _type : DrawElementsType, indices : nativeint, drawcount : int) =
        x.Append(InstructionCode.MultiDrawElements, mode, count, _type, indices, drawcount)
    member x.MultiDrawElementsBaseVertex(mode : PrimitiveType, count : nativeptr<int>, _type : DrawElementsType, indices : nativeint, drawcount : int, basevertex : nativeptr<int>) =
        x.Append(InstructionCode.MultiDrawElementsBaseVertex, mode, count, _type, indices, drawcount, basevertex)
    member x.MultiDrawElementsIndirect(mode : PrimitiveType, _type : DrawElementsType, indirect : nativeint, drawcount : int, stride : int) =
        x.Append(InstructionCode.MultiDrawElementsIndirect, mode, _type, indirect, drawcount, stride)
    member x.MultiDrawElementsIndirectCount(mode : PrimitiveType, _type : All, indirect : nativeint, drawcount : nativeint, maxdrawcount : int, stride : int) =
        x.Append(InstructionCode.MultiDrawElementsIndirectCount, mode, _type, indirect, drawcount, maxdrawcount, stride)
    member x.MultiTexBuffer(texunit : TextureUnit, target : TextureTarget, internalformat : TypeEnum, buffer : int) =
        x.Append(InstructionCode.MultiTexBuffer, texunit, target, internalformat, buffer)
    member x.MultiTexCoordP1(texture : TextureUnit, _type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.MultiTexCoordP1, texture, _type, coords)
    member x.MultiTexCoordP2(texture : TextureUnit, _type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.MultiTexCoordP2, texture, _type, coords)
    member x.MultiTexCoordP3(texture : TextureUnit, _type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.MultiTexCoordP3, texture, _type, coords)
    member x.MultiTexCoordP4(texture : TextureUnit, _type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.MultiTexCoordP4, texture, _type, coords)
    member x.MultiTexCoordPointer(texunit : TextureUnit, size : int, _type : TexCoordPointerType, stride : int, pointer : nativeint) =
        x.Append(InstructionCode.MultiTexCoordPointer, texunit, size, _type, stride, pointer)
    member x.MultiTexEnv(texunit : TextureUnit, target : TextureEnvTarget, pname : TextureEnvParameter, param : float32) =
        x.Append(InstructionCode.MultiTexEnv, texunit, target, pname, param)
    member x.MultiTexGen(texunit : TextureUnit, coord : TextureCoordName, pname : TextureGenParameter, _params : nativeptr<float>) =
        x.Append(InstructionCode.MultiTexGen, texunit, coord, pname, _params)
    member x.MultiTexGend(texunit : TextureUnit, coord : TextureCoordName, pname : TextureGenParameter, param : float) =
        x.Append(InstructionCode.MultiTexGend, texunit, coord, pname, param)
    member x.MultiTexImage1D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.MultiTexImage1D, texunit, target, level, internalformat, width, border, format, _type, pixels)
    member x.MultiTexImage2D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.MultiTexImage2D, texunit, target, level, internalformat, width, height, border, format, _type, pixels)
    member x.MultiTexImage3D(texunit : TextureUnit, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, depth : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.MultiTexImage3D, texunit, target, level, internalformat, width, height, depth, border, format, _type, pixels)
    member x.MultiTexParameter(texunit : TextureUnit, target : TextureTarget, pname : TextureParameterName, param : float32) =
        x.Append(InstructionCode.MultiTexParameter, texunit, target, pname, param)
    member x.MultiTexParameterI(texunit : TextureUnit, target : TextureTarget, pname : TextureParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.MultiTexParameterI, texunit, target, pname, _params)
    member x.MultiTexRenderbuffer(texunit : TextureUnit, target : TextureTarget, renderbuffer : int) =
        x.Append(InstructionCode.MultiTexRenderbuffer, texunit, target, renderbuffer)
    member x.MultiTexSubImage1D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, width : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.MultiTexSubImage1D, texunit, target, level, xoffset, width, format, _type, pixels)
    member x.MultiTexSubImage2D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, yoffset : int, width : int, height : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.MultiTexSubImage2D, texunit, target, level, xoffset, yoffset, width, height, format, _type, pixels)
    member x.MultiTexSubImage3D(texunit : TextureUnit, target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.MultiTexSubImage3D, texunit, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels)
    member x.NamedBufferData(buffer : int, size : int, data : nativeint, usage : BufferUsageHint) =
        x.Append(InstructionCode.NamedBufferData, buffer, size, data, usage)
    member x.NamedBufferPageCommitment(buffer : int, offset : nativeint, size : int, commit : bool) =
        x.Append(InstructionCode.NamedBufferPageCommitment, buffer, offset, size, (if commit then 1 else 0))
    member x.NamedBufferStorage(buffer : int, size : int, data : nativeint, flags : BufferStorageFlags) =
        x.Append(InstructionCode.NamedBufferStorage, buffer, size, data, flags)
    member x.NamedBufferSubData(buffer : int, offset : nativeint, size : int, data : nativeint) =
        x.Append(InstructionCode.NamedBufferSubData, buffer, offset, size, data)
    member x.NamedCopyBufferSubData(readBuffer : int, writeBuffer : int, readOffset : nativeint, writeOffset : nativeint, size : int) =
        x.Append(InstructionCode.NamedCopyBufferSubData, readBuffer, writeBuffer, readOffset, writeOffset, size)
    member x.NamedFramebufferDrawBuffer(framebuffer : int, buf : DrawBufferMode) =
        x.Append(InstructionCode.NamedFramebufferDrawBuffer, framebuffer, buf)
    member x.NamedFramebufferDrawBuffers(framebuffer : int, n : int, bufs : nativeptr<DrawBuffersEnum>) =
        x.Append(InstructionCode.NamedFramebufferDrawBuffers, framebuffer, n, bufs)
    member x.NamedFramebufferParameter(framebuffer : int, pname : FramebufferDefaultParameter, param : int) =
        x.Append(InstructionCode.NamedFramebufferParameter, framebuffer, pname, param)
    member x.NamedFramebufferReadBuffer(framebuffer : int, src : ReadBufferMode) =
        x.Append(InstructionCode.NamedFramebufferReadBuffer, framebuffer, src)
    member x.NamedFramebufferRenderbuffer(framebuffer : int, attachment : FramebufferAttachment, renderbuffertarget : RenderbufferTarget, renderbuffer : int) =
        x.Append(InstructionCode.NamedFramebufferRenderbuffer, framebuffer, attachment, renderbuffertarget, renderbuffer)
    member x.NamedFramebufferSampleLocations(framebuffer : int, start : int, count : int, v : nativeptr<float32>) =
        x.Append(InstructionCode.NamedFramebufferSampleLocations, framebuffer, start, count, v)
    member x.NamedFramebufferTexture(framebuffer : int, attachment : FramebufferAttachment, texture : int, level : int) =
        x.Append(InstructionCode.NamedFramebufferTexture, framebuffer, attachment, texture, level)
    member x.NamedFramebufferTexture1D(framebuffer : int, attachment : FramebufferAttachment, textarget : TextureTarget, texture : int, level : int) =
        x.Append(InstructionCode.NamedFramebufferTexture1D, framebuffer, attachment, textarget, texture, level)
    member x.NamedFramebufferTexture2D(framebuffer : int, attachment : FramebufferAttachment, textarget : TextureTarget, texture : int, level : int) =
        x.Append(InstructionCode.NamedFramebufferTexture2D, framebuffer, attachment, textarget, texture, level)
    member x.NamedFramebufferTexture3D(framebuffer : int, attachment : FramebufferAttachment, textarget : TextureTarget, texture : int, level : int, zoffset : int) =
        x.Append(InstructionCode.NamedFramebufferTexture3D, framebuffer, attachment, textarget, texture, level, zoffset)
    member x.NamedFramebufferTextureFace(framebuffer : int, attachment : FramebufferAttachment, texture : int, level : int, face : TextureTarget) =
        x.Append(InstructionCode.NamedFramebufferTextureFace, framebuffer, attachment, texture, level, face)
    member x.NamedFramebufferTextureLayer(framebuffer : int, attachment : FramebufferAttachment, texture : int, level : int, layer : int) =
        x.Append(InstructionCode.NamedFramebufferTextureLayer, framebuffer, attachment, texture, level, layer)
    member x.NamedProgramLocalParameter4(program : int, target : All, index : int, _x : float, y : float, z : float, w : float) =
        x.Append(InstructionCode.NamedProgramLocalParameter4, program, target, index, _x, y, z, w)
    member x.NamedProgramLocalParameterI4(program : int, target : All, index : int, _x : int, y : int, z : int, w : int) =
        x.Append(InstructionCode.NamedProgramLocalParameterI4, program, target, index, _x, y, z, w)
    member x.NamedProgramLocalParameters4(program : int, target : All, index : int, count : int, _params : nativeptr<float32>) =
        x.Append(InstructionCode.NamedProgramLocalParameters4, program, target, index, count, _params)
    member x.NamedProgramLocalParametersI4(program : int, target : All, index : int, count : int, _params : nativeptr<int>) =
        x.Append(InstructionCode.NamedProgramLocalParametersI4, program, target, index, count, _params)
    member x.NamedProgramString(program : int, target : All, format : All, len : int, string : nativeint) =
        x.Append(InstructionCode.NamedProgramString, program, target, format, len, string)
    member x.NamedRenderbufferStorage(renderbuffer : int, internalformat : RenderbufferStorage, width : int, height : int) =
        x.Append(InstructionCode.NamedRenderbufferStorage, renderbuffer, internalformat, width, height)
    member x.NamedRenderbufferStorageMultisample(renderbuffer : int, samples : int, internalformat : RenderbufferStorage, width : int, height : int) =
        x.Append(InstructionCode.NamedRenderbufferStorageMultisample, renderbuffer, samples, internalformat, width, height)
    member x.NamedRenderbufferStorageMultisampleCoverage(renderbuffer : int, coverageSamples : int, colorSamples : int, internalformat : InternalFormat, width : int, height : int) =
        x.Append(InstructionCode.NamedRenderbufferStorageMultisampleCoverage, renderbuffer, coverageSamples, colorSamples, internalformat, width, height)
    member x.NormalP3(_type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.NormalP3, _type, coords)
    member x.PatchParameter(pname : PatchParameterFloat, values : nativeptr<float32>) =
        x.Append(InstructionCode.PatchParameter, pname, values)
    member x.PauseTransformFeedback() =
        x.Append(InstructionCode.PauseTransformFeedback)
    member x.PixelStore(pname : PixelStoreParameter, param : float32) =
        x.Append(InstructionCode.PixelStore, pname, param)
    member x.PointParameter(pname : PointParameterName, param : float32) =
        x.Append(InstructionCode.PointParameter, pname, param)
    member x.PointSize(size : float32) =
        x.Append(InstructionCode.PointSize, size)
    member x.PolygonMode(face : MaterialFace, mode : PolygonMode) =
        x.Append(InstructionCode.PolygonMode, face, mode)
    member x.PolygonOffset(factor : float32, units : float32) =
        x.Append(InstructionCode.PolygonOffset, factor, units)
    member x.PolygonOffsetClamp(factor : float32, units : float32, clamp : float32) =
        x.Append(InstructionCode.PolygonOffsetClamp, factor, units, clamp)
    member x.PopDebugGroup() =
        x.Append(InstructionCode.PopDebugGroup)
    member x.PopGroupMarker() =
        x.Append(InstructionCode.PopGroupMarker)
    member x.PrimitiveBoundingBox(minX : float32, minY : float32, minZ : float32, minW : float32, maxX : float32, maxY : float32, maxZ : float32, maxW : float32) =
        x.Append(InstructionCode.PrimitiveBoundingBox, minX, minY, minZ, minW, maxX, maxY, maxZ, maxW)
    member x.PrimitiveRestartIndex(index : int) =
        x.Append(InstructionCode.PrimitiveRestartIndex, index)
    member x.ProgramBinary(program : int, binaryFormat : BinaryFormat, binary : nativeint, length : int) =
        x.Append(InstructionCode.ProgramBinary, program, binaryFormat, binary, length)
    member x.ProgramParameter(program : int, pname : ProgramParameterName, value : int) =
        x.Append(InstructionCode.ProgramParameter, program, pname, value)
    member x.ProgramUniform1(program : int, location : int, count : int, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniform1, program, location, count, value)
    member x.ProgramUniform2(program : int, location : int, v0 : float, v1 : float) =
        x.Append(InstructionCode.ProgramUniform2, program, location, v0, v1)
    member x.ProgramUniform3(program : int, location : int, v0 : float, v1 : float, v2 : float) =
        x.Append(InstructionCode.ProgramUniform3, program, location, v0, v1, v2)
    member x.ProgramUniform4(program : int, location : int, v0 : float, v1 : float, v2 : float, v3 : float) =
        x.Append(InstructionCode.ProgramUniform4, program, location, v0, v1, v2, v3)
    member x.ProgramUniformHandle(program : int, location : int, count : int, values : nativeptr<int64>) =
        x.Append(InstructionCode.ProgramUniformHandle, program, location, count, values)
    member x.ProgramUniformMatrix2(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix2, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix2x3(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix2x3, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix2x4(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix2x4, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix3(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix3, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix3x2(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix3x2, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix3x4(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix3x4, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix4(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix4, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix4x2(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix4x2, program, location, count, (if transpose then 1 else 0), value)
    member x.ProgramUniformMatrix4x3(program : int, location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.ProgramUniformMatrix4x3, program, location, count, (if transpose then 1 else 0), value)
    member x.ProvokingVertex(mode : ProvokingVertexMode) =
        x.Append(InstructionCode.ProvokingVertex, mode)
    member x.PushClientAttribDefault(mask : ClientAttribMask) =
        x.Append(InstructionCode.PushClientAttribDefault, mask)
    member x.QueryCounter(id : int, target : QueryCounterTarget) =
        x.Append(InstructionCode.QueryCounter, id, target)
    member x.RasterSamples(samples : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.RasterSamples, samples, (if fixedsamplelocations then 1 else 0))
    member x.ReadBuffer(src : ReadBufferMode) =
        x.Append(InstructionCode.ReadBuffer, src)
    member x.ReadPixels(_x : int, y : int, width : int, height : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.ReadPixels, _x, y, width, height, format, _type, pixels)
    member x.ReadnPixels(_x : int, y : int, width : int, height : int, format : PixelFormat, _type : PixelType, bufSize : int, data : nativeint) =
        x.Append(InstructionCode.ReadnPixels, _x, y, width, height, format, _type, bufSize, data)
    member x.ReleaseShaderCompiler() =
        x.Append(InstructionCode.ReleaseShaderCompiler)
    member x.RenderbufferStorage(target : RenderbufferTarget, internalformat : RenderbufferStorage, width : int, height : int) =
        x.Append(InstructionCode.RenderbufferStorage, target, internalformat, width, height)
    member x.RenderbufferStorageMultisample(target : RenderbufferTarget, samples : int, internalformat : RenderbufferStorage, width : int, height : int) =
        x.Append(InstructionCode.RenderbufferStorageMultisample, target, samples, internalformat, width, height)
    member x.ResetHistogram(target : HistogramTargetExt) =
        x.Append(InstructionCode.ResetHistogram, target)
    member x.ResetMinmax(target : MinmaxTargetExt) =
        x.Append(InstructionCode.ResetMinmax, target)
    member x.ResumeTransformFeedback() =
        x.Append(InstructionCode.ResumeTransformFeedback)
    member x.SampleCoverage(value : float32, invert : bool) =
        x.Append(InstructionCode.SampleCoverage, value, (if invert then 1 else 0))
    member x.SampleMask(maskNumber : int, mask : int) =
        x.Append(InstructionCode.SampleMask, maskNumber, mask)
    member x.SamplerParameter(sampler : int, pname : SamplerParameterName, param : float32) =
        x.Append(InstructionCode.SamplerParameter, sampler, pname, param)
    member x.SamplerParameterI(sampler : int, pname : SamplerParameterName, param : nativeptr<int>) =
        x.Append(InstructionCode.SamplerParameterI, sampler, pname, param)
    member x.Scissor(_x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.Scissor, _x, y, width, height)
    member x.ScissorArray(first : int, count : int, v : nativeptr<int>) =
        x.Append(InstructionCode.ScissorArray, first, count, v)
    member x.ScissorIndexed(index : int, left : int, bottom : int, width : int, height : int) =
        x.Append(InstructionCode.ScissorIndexed, index, left, bottom, width, height)
    member x.SecondaryColorP3(_type : PackedPointerType, color : int) =
        x.Append(InstructionCode.SecondaryColorP3, _type, color)
    member x.SeparableFilter2D(target : SeparableTargetExt, internalformat : InternalFormat, width : int, height : int, format : PixelFormat, _type : PixelType, row : nativeint, column : nativeint) =
        x.Append(InstructionCode.SeparableFilter2D, target, internalformat, width, height, format, _type, row, column)
    member x.ShaderBinary(count : int, shaders : nativeptr<int>, binaryformat : BinaryFormat, binary : nativeint, length : int) =
        x.Append(InstructionCode.ShaderBinary, count, shaders, binaryformat, binary, length)
    member x.ShaderStorageBlockBinding(program : int, storageBlockIndex : int, storageBlockBinding : int) =
        x.Append(InstructionCode.ShaderStorageBlockBinding, program, storageBlockIndex, storageBlockBinding)
    member x.StencilFunc(func : StencilFunction, ref : int, mask : int) =
        x.Append(InstructionCode.StencilFunc, func, ref, mask)
    member x.StencilFuncSeparate(face : StencilFace, func : StencilFunction, ref : int, mask : int) =
        x.Append(InstructionCode.StencilFuncSeparate, face, func, ref, mask)
    member x.StencilMask(mask : int) =
        x.Append(InstructionCode.StencilMask, mask)
    member x.StencilMaskSeparate(face : StencilFace, mask : int) =
        x.Append(InstructionCode.StencilMaskSeparate, face, mask)
    member x.StencilOp(fail : StencilOp, zfail : StencilOp, zpass : StencilOp) =
        x.Append(InstructionCode.StencilOp, fail, zfail, zpass)
    member x.StencilOpSeparate(face : StencilFace, sfail : StencilOp, dpfail : StencilOp, dppass : StencilOp) =
        x.Append(InstructionCode.StencilOpSeparate, face, sfail, dpfail, dppass)
    member x.TexBuffer(target : TextureBufferTarget, internalformat : SizedInternalFormat, buffer : int) =
        x.Append(InstructionCode.TexBuffer, target, internalformat, buffer)
    member x.TexBufferRange(target : TextureBufferTarget, internalformat : SizedInternalFormat, buffer : int, offset : nativeint, size : int) =
        x.Append(InstructionCode.TexBufferRange, target, internalformat, buffer, offset, size)
    member x.TexCoordP1(_type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.TexCoordP1, _type, coords)
    member x.TexCoordP2(_type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.TexCoordP2, _type, coords)
    member x.TexCoordP3(_type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.TexCoordP3, _type, coords)
    member x.TexCoordP4(_type : PackedPointerType, coords : int) =
        x.Append(InstructionCode.TexCoordP4, _type, coords)
    member x.TexImage1D(target : TextureTarget, level : int, internalformat : PixelInternalFormat, width : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TexImage1D, target, level, internalformat, width, border, format, _type, pixels)
    member x.TexImage2D(target : TextureTarget, level : int, internalformat : PixelInternalFormat, width : int, height : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TexImage2D, target, level, internalformat, width, height, border, format, _type, pixels)
    member x.TexImage2DMultisample(target : TextureTargetMultisample, samples : int, internalformat : PixelInternalFormat, width : int, height : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.TexImage2DMultisample, target, samples, internalformat, width, height, (if fixedsamplelocations then 1 else 0))
    member x.TexImage3D(target : TextureTarget, level : int, internalformat : PixelInternalFormat, width : int, height : int, depth : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TexImage3D, target, level, internalformat, width, height, depth, border, format, _type, pixels)
    member x.TexImage3DMultisample(target : TextureTargetMultisample, samples : int, internalformat : PixelInternalFormat, width : int, height : int, depth : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.TexImage3DMultisample, target, samples, internalformat, width, height, depth, (if fixedsamplelocations then 1 else 0))
    member x.TexPageCommitment(target : All, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, commit : bool) =
        x.Append(InstructionCode.TexPageCommitment, target, level, xoffset, yoffset, zoffset, width, height, depth, (if commit then 1 else 0))
    member x.TexParameter(target : TextureTarget, pname : TextureParameterName, param : float32) =
        x.Append(InstructionCode.TexParameter, target, pname, param)
    member x.TexParameterI(target : TextureTarget, pname : TextureParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.TexParameterI, target, pname, _params)
    member x.TexStorage1D(target : TextureTarget1d, levels : int, internalformat : SizedInternalFormat, width : int) =
        x.Append(InstructionCode.TexStorage1D, target, levels, internalformat, width)
    member x.TexStorage2D(target : TextureTarget2d, levels : int, internalformat : SizedInternalFormat, width : int, height : int) =
        x.Append(InstructionCode.TexStorage2D, target, levels, internalformat, width, height)
    member x.TexStorage2DMultisample(target : TextureTargetMultisample2d, samples : int, internalformat : SizedInternalFormat, width : int, height : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.TexStorage2DMultisample, target, samples, internalformat, width, height, (if fixedsamplelocations then 1 else 0))
    member x.TexStorage3D(target : TextureTarget3d, levels : int, internalformat : SizedInternalFormat, width : int, height : int, depth : int) =
        x.Append(InstructionCode.TexStorage3D, target, levels, internalformat, width, height, depth)
    member x.TexStorage3DMultisample(target : TextureTargetMultisample3d, samples : int, internalformat : SizedInternalFormat, width : int, height : int, depth : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.TexStorage3DMultisample, target, samples, internalformat, width, height, depth, (if fixedsamplelocations then 1 else 0))
    member x.TexSubImage1D(target : TextureTarget, level : int, xoffset : int, width : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TexSubImage1D, target, level, xoffset, width, format, _type, pixels)
    member x.TexSubImage2D(target : TextureTarget, level : int, xoffset : int, yoffset : int, width : int, height : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TexSubImage2D, target, level, xoffset, yoffset, width, height, format, _type, pixels)
    member x.TexSubImage3D(target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TexSubImage3D, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels)
    member x.TextureBarrier() =
        x.Append(InstructionCode.TextureBarrier)
    member x.TextureBuffer(texture : int, target : TextureTarget, internalformat : ExtDirectStateAccess, buffer : int) =
        x.Append(InstructionCode.TextureBuffer, texture, target, internalformat, buffer)
    member x.TextureBufferRange(texture : int, target : TextureTarget, internalformat : ExtDirectStateAccess, buffer : int, offset : nativeint, size : int) =
        x.Append(InstructionCode.TextureBufferRange, texture, target, internalformat, buffer, offset, size)
    member x.TextureImage1D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TextureImage1D, texture, target, level, internalformat, width, border, format, _type, pixels)
    member x.TextureImage2D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TextureImage2D, texture, target, level, internalformat, width, height, border, format, _type, pixels)
    member x.TextureImage3D(texture : int, target : TextureTarget, level : int, internalformat : InternalFormat, width : int, height : int, depth : int, border : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TextureImage3D, texture, target, level, internalformat, width, height, depth, border, format, _type, pixels)
    member x.TexturePageCommitment(texture : int, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, commit : bool) =
        x.Append(InstructionCode.TexturePageCommitment, texture, level, xoffset, yoffset, zoffset, width, height, depth, (if commit then 1 else 0))
    member x.TextureParameter(texture : int, target : TextureTarget, pname : TextureParameterName, param : float32) =
        x.Append(InstructionCode.TextureParameter, texture, target, pname, param)
    member x.TextureParameterI(texture : int, target : TextureTarget, pname : TextureParameterName, _params : nativeptr<int>) =
        x.Append(InstructionCode.TextureParameterI, texture, target, pname, _params)
    member x.TextureRenderbuffer(texture : int, target : TextureTarget, renderbuffer : int) =
        x.Append(InstructionCode.TextureRenderbuffer, texture, target, renderbuffer)
    member x.TextureStorage1D(texture : int, target : All, levels : int, internalformat : ExtDirectStateAccess, width : int) =
        x.Append(InstructionCode.TextureStorage1D, texture, target, levels, internalformat, width)
    member x.TextureStorage2D(texture : int, target : All, levels : int, internalformat : ExtDirectStateAccess, width : int, height : int) =
        x.Append(InstructionCode.TextureStorage2D, texture, target, levels, internalformat, width, height)
    member x.TextureStorage2DMultisample(texture : int, target : TextureTarget, samples : int, internalformat : ExtDirectStateAccess, width : int, height : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.TextureStorage2DMultisample, texture, target, samples, internalformat, width, height, (if fixedsamplelocations then 1 else 0))
    member x.TextureStorage3D(texture : int, target : All, levels : int, internalformat : ExtDirectStateAccess, width : int, height : int, depth : int) =
        x.Append(InstructionCode.TextureStorage3D, texture, target, levels, internalformat, width, height, depth)
    member x.TextureStorage3DMultisample(texture : int, target : All, samples : int, internalformat : ExtDirectStateAccess, width : int, height : int, depth : int, fixedsamplelocations : bool) =
        x.Append(InstructionCode.TextureStorage3DMultisample, texture, target, samples, internalformat, width, height, depth, (if fixedsamplelocations then 1 else 0))
    member x.TextureSubImage1D(texture : int, target : TextureTarget, level : int, xoffset : int, width : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TextureSubImage1D, texture, target, level, xoffset, width, format, _type, pixels)
    member x.TextureSubImage2D(texture : int, target : TextureTarget, level : int, xoffset : int, yoffset : int, width : int, height : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TextureSubImage2D, texture, target, level, xoffset, yoffset, width, height, format, _type, pixels)
    member x.TextureSubImage3D(texture : int, target : TextureTarget, level : int, xoffset : int, yoffset : int, zoffset : int, width : int, height : int, depth : int, format : PixelFormat, _type : PixelType, pixels : nativeint) =
        x.Append(InstructionCode.TextureSubImage3D, texture, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels)
    member x.TextureView(texture : int, target : TextureTarget, origtexture : int, internalformat : PixelInternalFormat, minlevel : int, numlevels : int, minlayer : int, numlayers : int) =
        x.Append(InstructionCode.TextureView, texture, target, origtexture, internalformat, minlevel, numlevels, minlayer, numlayers)
    member x.TransformFeedbackBufferBase(xfb : int, index : int, buffer : int) =
        x.Append(InstructionCode.TransformFeedbackBufferBase, xfb, index, buffer)
    member x.TransformFeedbackBufferRange(xfb : int, index : int, buffer : int, offset : nativeint, size : int) =
        x.Append(InstructionCode.TransformFeedbackBufferRange, xfb, index, buffer, offset, size)
    member x.Uniform1(location : int, count : int, value : nativeptr<float>) =
        x.Append(InstructionCode.Uniform1, location, count, value)
    member x.Uniform2(location : int, _x : float, y : float) =
        x.Append(InstructionCode.Uniform2, location, _x, y)
    member x.Uniform3(location : int, _x : float, y : float, z : float) =
        x.Append(InstructionCode.Uniform3, location, _x, y, z)
    member x.Uniform4(location : int, _x : float, y : float, z : float, w : float) =
        x.Append(InstructionCode.Uniform4, location, _x, y, z, w)
    member x.UniformBlockBinding(program : int, uniformBlockIndex : int, uniformBlockBinding : int) =
        x.Append(InstructionCode.UniformBlockBinding, program, uniformBlockIndex, uniformBlockBinding)
    member x.UniformHandle(location : int, count : int, value : nativeptr<int64>) =
        x.Append(InstructionCode.UniformHandle, location, count, value)
    member x.UniformMatrix2(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix2, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix2x3(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix2x3, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix2x4(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix2x4, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix3(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix3, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix3x2(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix3x2, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix3x4(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix3x4, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix4(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix4, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix4x2(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix4x2, location, count, (if transpose then 1 else 0), value)
    member x.UniformMatrix4x3(location : int, count : int, transpose : bool, value : nativeptr<float>) =
        x.Append(InstructionCode.UniformMatrix4x3, location, count, (if transpose then 1 else 0), value)
    member x.UniformSubroutines(shadertype : ShaderType, count : int, indices : nativeptr<int>) =
        x.Append(InstructionCode.UniformSubroutines, shadertype, count, indices)
    member x.UseProgram(program : int) =
        x.Append(InstructionCode.UseProgram, program)
    member x.UseProgramStages(pipeline : int, stages : ProgramStageMask, program : int) =
        x.Append(InstructionCode.UseProgramStages, pipeline, stages, program)
    member x.UseShaderProgram(_type : All, program : int) =
        x.Append(InstructionCode.UseShaderProgram, _type, program)
    member x.ValidateProgram(program : int) =
        x.Append(InstructionCode.ValidateProgram, program)
    member x.ValidateProgramPipeline(pipeline : int) =
        x.Append(InstructionCode.ValidateProgramPipeline, pipeline)
    member x.VertexArrayAttribBinding(vaobj : int, attribindex : int, bindingindex : int) =
        x.Append(InstructionCode.VertexArrayAttribBinding, vaobj, attribindex, bindingindex)
    member x.VertexArrayAttribFormat(vaobj : int, attribindex : int, size : int, _type : VertexAttribType, normalized : bool, relativeoffset : int) =
        x.Append(InstructionCode.VertexArrayAttribFormat, vaobj, attribindex, size, _type, (if normalized then 1 else 0), relativeoffset)
    member x.VertexArrayAttribIFormat(vaobj : int, attribindex : int, size : int, _type : VertexAttribType, relativeoffset : int) =
        x.Append(InstructionCode.VertexArrayAttribIFormat, vaobj, attribindex, size, _type, relativeoffset)
    member x.VertexArrayAttribLFormat(vaobj : int, attribindex : int, size : int, _type : VertexAttribType, relativeoffset : int) =
        x.Append(InstructionCode.VertexArrayAttribLFormat, vaobj, attribindex, size, _type, relativeoffset)
    member x.VertexArrayBindVertexBuffer(vaobj : int, bindingindex : int, buffer : int, offset : nativeint, stride : int) =
        x.Append(InstructionCode.VertexArrayBindVertexBuffer, vaobj, bindingindex, buffer, offset, stride)
    member x.VertexArrayBindingDivisor(vaobj : int, bindingindex : int, divisor : int) =
        x.Append(InstructionCode.VertexArrayBindingDivisor, vaobj, bindingindex, divisor)
    member x.VertexArrayColorOffset(vaobj : int, buffer : int, size : int, _type : ColorPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayColorOffset, vaobj, buffer, size, _type, stride, offset)
    member x.VertexArrayEdgeFlagOffset(vaobj : int, buffer : int, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayEdgeFlagOffset, vaobj, buffer, stride, offset)
    member x.VertexArrayElementBuffer(vaobj : int, buffer : int) =
        x.Append(InstructionCode.VertexArrayElementBuffer, vaobj, buffer)
    member x.VertexArrayFogCoordOffset(vaobj : int, buffer : int, _type : FogPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayFogCoordOffset, vaobj, buffer, _type, stride, offset)
    member x.VertexArrayIndexOffset(vaobj : int, buffer : int, _type : IndexPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayIndexOffset, vaobj, buffer, _type, stride, offset)
    member x.VertexArrayMultiTexCoordOffset(vaobj : int, buffer : int, texunit : All, size : int, _type : TexCoordPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayMultiTexCoordOffset, vaobj, buffer, texunit, size, _type, stride, offset)
    member x.VertexArrayNormalOffset(vaobj : int, buffer : int, _type : NormalPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayNormalOffset, vaobj, buffer, _type, stride, offset)
    member x.VertexArraySecondaryColorOffset(vaobj : int, buffer : int, size : int, _type : ColorPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArraySecondaryColorOffset, vaobj, buffer, size, _type, stride, offset)
    member x.VertexArrayTexCoordOffset(vaobj : int, buffer : int, size : int, _type : TexCoordPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayTexCoordOffset, vaobj, buffer, size, _type, stride, offset)
    member x.VertexArrayVertexAttribBinding(vaobj : int, attribindex : int, bindingindex : int) =
        x.Append(InstructionCode.VertexArrayVertexAttribBinding, vaobj, attribindex, bindingindex)
    member x.VertexArrayVertexAttribDivisor(vaobj : int, index : int, divisor : int) =
        x.Append(InstructionCode.VertexArrayVertexAttribDivisor, vaobj, index, divisor)
    member x.VertexArrayVertexAttribFormat(vaobj : int, attribindex : int, size : int, _type : All, normalized : bool, relativeoffset : int) =
        x.Append(InstructionCode.VertexArrayVertexAttribFormat, vaobj, attribindex, size, _type, (if normalized then 1 else 0), relativeoffset)
    member x.VertexArrayVertexAttribIFormat(vaobj : int, attribindex : int, size : int, _type : All, relativeoffset : int) =
        x.Append(InstructionCode.VertexArrayVertexAttribIFormat, vaobj, attribindex, size, _type, relativeoffset)
    member x.VertexArrayVertexAttribIOffset(vaobj : int, buffer : int, index : int, size : int, _type : VertexAttribEnum, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayVertexAttribIOffset, vaobj, buffer, index, size, _type, stride, offset)
    member x.VertexArrayVertexAttribLFormat(vaobj : int, attribindex : int, size : int, _type : All, relativeoffset : int) =
        x.Append(InstructionCode.VertexArrayVertexAttribLFormat, vaobj, attribindex, size, _type, relativeoffset)
    member x.VertexArrayVertexAttribLOffset(vaobj : int, buffer : int, index : int, size : int, _type : All, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayVertexAttribLOffset, vaobj, buffer, index, size, _type, stride, offset)
    member x.VertexArrayVertexAttribOffset(vaobj : int, buffer : int, index : int, size : int, _type : VertexAttribPointerType, normalized : bool, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayVertexAttribOffset, vaobj, buffer, index, size, _type, (if normalized then 1 else 0), stride, offset)
    member x.VertexArrayVertexBindingDivisor(vaobj : int, bindingindex : int, divisor : int) =
        x.Append(InstructionCode.VertexArrayVertexBindingDivisor, vaobj, bindingindex, divisor)
    member x.VertexArrayVertexBuffer(vaobj : int, bindingindex : int, buffer : int, offset : nativeint, stride : int) =
        x.Append(InstructionCode.VertexArrayVertexBuffer, vaobj, bindingindex, buffer, offset, stride)
    member x.VertexArrayVertexBuffers(vaobj : int, first : int, count : int, buffers : nativeptr<int>, offsets : nativeptr<nativeint>, strides : nativeptr<int>) =
        x.Append(InstructionCode.VertexArrayVertexBuffers, vaobj, first, count, buffers, offsets, strides)
    member x.VertexArrayVertexOffset(vaobj : int, buffer : int, size : int, _type : VertexPointerType, stride : int, offset : nativeint) =
        x.Append(InstructionCode.VertexArrayVertexOffset, vaobj, buffer, size, _type, stride, offset)
    member x.VertexAttrib1(index : int, _x : float) =
        x.Append(InstructionCode.VertexAttrib1, index, _x)
    member x.VertexAttrib2(index : int, _x : float32, y : float32) =
        x.Append(InstructionCode.VertexAttrib2, index, _x, y)
    member x.VertexAttrib3(index : int, _x : float, y : float, z : float) =
        x.Append(InstructionCode.VertexAttrib3, index, _x, y, z)
    member x.VertexAttrib4(index : int, _x : float, y : float, z : float, w : float) =
        x.Append(InstructionCode.VertexAttrib4, index, _x, y, z, w)
    member x.VertexAttrib4N(index : int, _x : byte, y : byte, z : byte, w : byte) =
        x.Append(InstructionCode.VertexAttrib4N, index, _x, y, z, w)
    member x.VertexAttribBinding(attribindex : int, bindingindex : int) =
        x.Append(InstructionCode.VertexAttribBinding, attribindex, bindingindex)
    member x.VertexAttribDivisor(index : int, divisor : int) =
        x.Append(InstructionCode.VertexAttribDivisor, index, divisor)
    member x.VertexAttribFormat(attribindex : int, size : int, _type : VertexAttribType, normalized : bool, relativeoffset : int) =
        x.Append(InstructionCode.VertexAttribFormat, attribindex, size, _type, (if normalized then 1 else 0), relativeoffset)
    member x.VertexAttribI1(index : int, v : nativeptr<int>) =
        x.Append(InstructionCode.VertexAttribI1, index, v)
    member x.VertexAttribI2(index : int, _x : int, y : int) =
        x.Append(InstructionCode.VertexAttribI2, index, _x, y)
    member x.VertexAttribI3(index : int, _x : int, y : int, z : int) =
        x.Append(InstructionCode.VertexAttribI3, index, _x, y, z)
    member x.VertexAttribI4(index : int, _x : int, y : int, z : int, w : int) =
        x.Append(InstructionCode.VertexAttribI4, index, _x, y, z, w)
    member x.VertexAttribIFormat(attribindex : int, size : int, _type : VertexAttribIntegerType, relativeoffset : int) =
        x.Append(InstructionCode.VertexAttribIFormat, attribindex, size, _type, relativeoffset)
    member x.VertexAttribIPointer(index : int, size : int, _type : VertexAttribIntegerType, stride : int, pointer : nativeint) =
        x.Append(InstructionCode.VertexAttribIPointer, index, size, _type, stride, pointer)
    member x.VertexAttribL1(index : int, _x : float) =
        x.Append(InstructionCode.VertexAttribL1, index, _x)
    member x.VertexAttribL2(index : int, _x : float, y : float) =
        x.Append(InstructionCode.VertexAttribL2, index, _x, y)
    member x.VertexAttribL3(index : int, _x : float, y : float, z : float) =
        x.Append(InstructionCode.VertexAttribL3, index, _x, y, z)
    member x.VertexAttribL4(index : int, _x : float, y : float, z : float, w : float) =
        x.Append(InstructionCode.VertexAttribL4, index, _x, y, z, w)
    member x.VertexAttribLFormat(attribindex : int, size : int, _type : VertexAttribDoubleType, relativeoffset : int) =
        x.Append(InstructionCode.VertexAttribLFormat, attribindex, size, _type, relativeoffset)
    member x.VertexAttribLPointer(index : int, size : int, _type : VertexAttribDoubleType, stride : int, pointer : nativeint) =
        x.Append(InstructionCode.VertexAttribLPointer, index, size, _type, stride, pointer)
    member x.VertexAttribP1(index : int, _type : PackedPointerType, normalized : bool, value : nativeptr<int>) =
        x.Append(InstructionCode.VertexAttribP1, index, _type, (if normalized then 1 else 0), value)
    member x.VertexAttribP2(index : int, _type : PackedPointerType, normalized : bool, value : int) =
        x.Append(InstructionCode.VertexAttribP2, index, _type, (if normalized then 1 else 0), value)
    member x.VertexAttribP3(index : int, _type : PackedPointerType, normalized : bool, value : int) =
        x.Append(InstructionCode.VertexAttribP3, index, _type, (if normalized then 1 else 0), value)
    member x.VertexAttribP4(index : int, _type : PackedPointerType, normalized : bool, value : int) =
        x.Append(InstructionCode.VertexAttribP4, index, _type, (if normalized then 1 else 0), value)
    member x.VertexAttribPointer(index : int, size : int, _type : VertexAttribPointerType, normalized : bool, stride : int, pointer : nativeint) =
        x.Append(InstructionCode.VertexAttribPointer, index, size, _type, (if normalized then 1 else 0), stride, pointer)
    member x.VertexBindingDivisor(bindingindex : int, divisor : int) =
        x.Append(InstructionCode.VertexBindingDivisor, bindingindex, divisor)
    member x.VertexP2(_type : PackedPointerType, value : int) =
        x.Append(InstructionCode.VertexP2, _type, value)
    member x.VertexP3(_type : PackedPointerType, value : int) =
        x.Append(InstructionCode.VertexP3, _type, value)
    member x.VertexP4(_type : PackedPointerType, value : int) =
        x.Append(InstructionCode.VertexP4, _type, value)
    member x.Viewport(_x : int, y : int, width : int, height : int) =
        x.Append(InstructionCode.Viewport, _x, y, width, height)
    member x.ViewportArray(first : int, count : int, v : nativeptr<float32>) =
        x.Append(InstructionCode.ViewportArray, first, count, v)
    member x.ViewportIndexed(index : int, _x : float32, y : float32, w : float32, h : float32) =
        x.Append(InstructionCode.ViewportIndexed, index, _x, y, w, h)
    member x.WaitSync(sync : nativeint, flags : WaitSyncFlags, timeout : int64) =
        x.Append(InstructionCode.WaitSync, sync, flags, timeout)
    member x.WindowRectangles(mode : All, count : int, box : nativeptr<int>) =
        x.Append(InstructionCode.WindowRectangles, mode, count, box)


    static member RunInstruction(ptr : byref<nativeint>) = 
        let s : int = NativePtr.read (NativePtr.ofNativeInt ptr)
        let fin = ptr + nativeint s
        ptr <- ptr + 4n
        let c : InstructionCode = NativePtr.read (NativePtr.ofNativeInt ptr)
        ptr <- ptr + 4n

        match c with
        | InstructionCode.ActiveProgram ->
            OpenTK.Graphics.OpenGL4.GL.Ext.ActiveProgram(read<int> &ptr)
        | InstructionCode.ActiveShaderProgram ->
            OpenTK.Graphics.OpenGL4.GL.ActiveShaderProgram(read<int> &ptr, read<int> &ptr)
        | InstructionCode.ActiveTexture ->
            OpenTK.Graphics.OpenGL4.GL.ActiveTexture(read<TextureUnit> &ptr)
        | InstructionCode.AttachShader ->
            OpenTK.Graphics.OpenGL4.GL.AttachShader(read<int> &ptr, read<int> &ptr)
        | InstructionCode.BeginConditionalRender ->
            OpenTK.Graphics.OpenGL4.GL.BeginConditionalRender(read<int> &ptr, read<ConditionalRenderType> &ptr)
        | InstructionCode.BeginQuery ->
            OpenTK.Graphics.OpenGL4.GL.BeginQuery(read<QueryTarget> &ptr, read<int> &ptr)
        | InstructionCode.BeginQueryIndexed ->
            OpenTK.Graphics.OpenGL4.GL.BeginQueryIndexed(read<QueryTarget> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.BeginTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.BeginTransformFeedback(read<TransformFeedbackPrimitiveType> &ptr)
        | InstructionCode.BindBuffer ->
            OpenTK.Graphics.OpenGL4.GL.BindBuffer(read<BufferTarget> &ptr, read<int> &ptr)
        | InstructionCode.BindBufferBase ->
            OpenTK.Graphics.OpenGL4.GL.BindBufferBase(read<BufferRangeTarget> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.BindBufferRange ->
            OpenTK.Graphics.OpenGL4.GL.BindBufferRange(read<BufferRangeTarget> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.BindBuffersBase ->
            OpenTK.Graphics.OpenGL4.GL.BindBuffersBase(read<BufferRangeTarget> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.BindBuffersRange ->
            OpenTK.Graphics.OpenGL4.GL.BindBuffersRange(read<BufferRangeTarget> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<nativeint>> &ptr, read<nativeptr<nativeint>> &ptr)
        | InstructionCode.BindFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.BindFramebuffer(read<FramebufferTarget> &ptr, read<int> &ptr)
        | InstructionCode.BindImageTexture ->
            OpenTK.Graphics.OpenGL4.GL.BindImageTexture(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<int> &ptr, read<TextureAccess> &ptr, read<SizedInternalFormat> &ptr)
        | InstructionCode.BindImageTextures ->
            OpenTK.Graphics.OpenGL4.GL.BindImageTextures(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.BindMultiTexture ->
            OpenTK.Graphics.OpenGL4.GL.Ext.BindMultiTexture(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr)
        | InstructionCode.BindProgramPipeline ->
            OpenTK.Graphics.OpenGL4.GL.BindProgramPipeline(read<int> &ptr)
        | InstructionCode.BindRenderbuffer ->
            OpenTK.Graphics.OpenGL4.GL.BindRenderbuffer(read<RenderbufferTarget> &ptr, read<int> &ptr)
        | InstructionCode.BindSampler ->
            OpenTK.Graphics.OpenGL4.GL.BindSampler(read<int> &ptr, read<int> &ptr)
        | InstructionCode.BindSamplers ->
            OpenTK.Graphics.OpenGL4.GL.BindSamplers(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.BindTexture ->
            OpenTK.Graphics.OpenGL4.GL.BindTexture(read<TextureTarget> &ptr, read<int> &ptr)
        | InstructionCode.BindTextureUnit ->
            OpenTK.Graphics.OpenGL4.GL.BindTextureUnit(read<int> &ptr, read<int> &ptr)
        | InstructionCode.BindTextures ->
            OpenTK.Graphics.OpenGL4.GL.BindTextures(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.BindTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.BindTransformFeedback(read<TransformFeedbackTarget> &ptr, read<int> &ptr)
        | InstructionCode.BindVertexArray ->
            OpenTK.Graphics.OpenGL4.GL.BindVertexArray(read<int> &ptr)
        | InstructionCode.BindVertexBuffer ->
            OpenTK.Graphics.OpenGL4.GL.BindVertexBuffer(read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.BindVertexBuffers ->
            OpenTK.Graphics.OpenGL4.GL.BindVertexBuffers(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<nativeint>> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.BlendColor ->
            OpenTK.Graphics.OpenGL4.GL.BlendColor(read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.BlendEquation ->
            OpenTK.Graphics.OpenGL4.GL.BlendEquation(read<int> &ptr, read<BlendEquationMode> &ptr)
        | InstructionCode.BlendEquationSeparate ->
            OpenTK.Graphics.OpenGL4.GL.BlendEquationSeparate(read<int> &ptr, read<BlendEquationMode> &ptr, read<BlendEquationMode> &ptr)
        | InstructionCode.BlendFunc ->
            OpenTK.Graphics.OpenGL4.GL.BlendFunc(read<int> &ptr, read<BlendingFactorSrc> &ptr, read<BlendingFactorDest> &ptr)
        | InstructionCode.BlendFuncSeparate ->
            OpenTK.Graphics.OpenGL4.GL.BlendFuncSeparate(read<int> &ptr, read<BlendingFactorSrc> &ptr, read<BlendingFactorDest> &ptr, read<BlendingFactorSrc> &ptr, read<BlendingFactorDest> &ptr)
        | InstructionCode.BlitFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.BlitFramebuffer(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<ClearBufferMask> &ptr, read<BlitFramebufferFilter> &ptr)
        | InstructionCode.BlitNamedFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.BlitNamedFramebuffer(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<ClearBufferMask> &ptr, read<BlitFramebufferFilter> &ptr)
        | InstructionCode.BufferData ->
            OpenTK.Graphics.OpenGL4.GL.BufferData(read<BufferTarget> &ptr, read<int> &ptr, read<nativeint> &ptr, read<BufferUsageHint> &ptr)
        | InstructionCode.BufferPageCommitment ->
            OpenTK.Graphics.OpenGL4.GL.Arb.BufferPageCommitment(read<All> &ptr, read<nativeint> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.BufferStorage ->
            OpenTK.Graphics.OpenGL4.GL.BufferStorage(read<BufferTarget> &ptr, read<int> &ptr, read<nativeint> &ptr, read<BufferStorageFlags> &ptr)
        | InstructionCode.BufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.BufferSubData(read<BufferTarget> &ptr, read<nativeint> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClampColor ->
            OpenTK.Graphics.OpenGL4.GL.ClampColor(read<ClampColorTarget> &ptr, read<ClampColorMode> &ptr)
        | InstructionCode.Clear ->
            OpenTK.Graphics.OpenGL4.GL.Clear(read<ClearBufferMask> &ptr)
        | InstructionCode.ClearBuffer ->
            OpenTK.Graphics.OpenGL4.GL.ClearBuffer(read<ClearBufferCombined> &ptr, read<int> &ptr, read<float32> &ptr, read<int> &ptr)
        | InstructionCode.ClearBufferData ->
            OpenTK.Graphics.OpenGL4.GL.ClearBufferData(read<BufferTarget> &ptr, read<PixelInternalFormat> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClearBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.ClearBufferSubData(read<BufferTarget> &ptr, read<PixelInternalFormat> &ptr, read<nativeint> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClearColor ->
            OpenTK.Graphics.OpenGL4.GL.ClearColor(read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.ClearDepth ->
            OpenTK.Graphics.OpenGL4.GL.ClearDepth(read<float> &ptr)
        | InstructionCode.ClearNamedBufferData ->
            OpenTK.Graphics.OpenGL4.GL.ClearNamedBufferData(read<int> &ptr, read<PixelInternalFormat> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClearNamedBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.ClearNamedBufferSubData(read<int> &ptr, read<PixelInternalFormat> &ptr, read<nativeint> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClearNamedFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.ClearNamedFramebuffer(read<int> &ptr, read<ClearBufferCombined> &ptr, read<int> &ptr, read<float32> &ptr, read<int> &ptr)
        | InstructionCode.ClearStencil ->
            OpenTK.Graphics.OpenGL4.GL.ClearStencil(read<int> &ptr)
        | InstructionCode.ClearTexImage ->
            OpenTK.Graphics.OpenGL4.GL.ClearTexImage(read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClearTexSubImage ->
            OpenTK.Graphics.OpenGL4.GL.ClearTexSubImage(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ClientAttribDefault ->
            OpenTK.Graphics.OpenGL4.GL.Ext.ClientAttribDefault(read<ClientAttribMask> &ptr)
        | InstructionCode.ClipControl ->
            OpenTK.Graphics.OpenGL4.GL.ClipControl(read<ClipOrigin> &ptr, read<ClipDepthMode> &ptr)
        | InstructionCode.ColorMask ->
            OpenTK.Graphics.OpenGL4.GL.ColorMask(read<int> &ptr, (read<int> &ptr = 1), (read<int> &ptr = 1), (read<int> &ptr = 1), (read<int> &ptr = 1))
        | InstructionCode.ColorP3 ->
            OpenTK.Graphics.OpenGL4.GL.ColorP3(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.ColorP4 ->
            OpenTK.Graphics.OpenGL4.GL.ColorP4(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.ColorSubTable ->
            OpenTK.Graphics.OpenGL4.GL.ColorSubTable(read<ColorTableTarget> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ColorTable ->
            OpenTK.Graphics.OpenGL4.GL.ColorTable(read<ColorTableTarget> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ColorTableParameter ->
            OpenTK.Graphics.OpenGL4.GL.ColorTableParameter(read<ColorTableTarget> &ptr, read<ColorTableParameterPNameSgi> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.CompileShader ->
            OpenTK.Graphics.OpenGL4.GL.CompileShader(read<int> &ptr)
        | InstructionCode.CompressedMultiTexImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedMultiTexImage1D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedMultiTexImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedMultiTexImage2D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedMultiTexImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedMultiTexImage3D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedMultiTexSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedMultiTexSubImage1D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedMultiTexSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedMultiTexSubImage2D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedMultiTexSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedMultiTexSubImage3D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTexImage1D ->
            OpenTK.Graphics.OpenGL4.GL.CompressedTexImage1D(read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTexImage2D ->
            OpenTK.Graphics.OpenGL4.GL.CompressedTexImage2D(read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTexImage3D ->
            OpenTK.Graphics.OpenGL4.GL.CompressedTexImage3D(read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTexSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.CompressedTexSubImage1D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTexSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.CompressedTexSubImage2D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTexSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.CompressedTexSubImage3D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTextureImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedTextureImage1D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTextureImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedTextureImage2D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTextureImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedTextureImage3D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTextureSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedTextureSubImage1D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTextureSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedTextureSubImage2D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.CompressedTextureSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CompressedTextureSubImage3D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.ConvolutionFilter1D ->
            OpenTK.Graphics.OpenGL4.GL.ConvolutionFilter1D(read<ConvolutionTarget> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ConvolutionFilter2D ->
            OpenTK.Graphics.OpenGL4.GL.ConvolutionFilter2D(read<ConvolutionTarget> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ConvolutionParameter ->
            OpenTK.Graphics.OpenGL4.GL.ConvolutionParameter(read<ConvolutionTarget> &ptr, read<ConvolutionParameterExt> &ptr, read<float32> &ptr)
        | InstructionCode.CopyBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.CopyBufferSubData(read<BufferTarget> &ptr, read<BufferTarget> &ptr, read<nativeint> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.CopyColorSubTable ->
            OpenTK.Graphics.OpenGL4.GL.CopyColorSubTable(read<ColorTableTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyColorTable ->
            OpenTK.Graphics.OpenGL4.GL.CopyColorTable(read<ColorTableTarget> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyConvolutionFilter1D ->
            OpenTK.Graphics.OpenGL4.GL.CopyConvolutionFilter1D(read<ConvolutionTarget> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyConvolutionFilter2D ->
            OpenTK.Graphics.OpenGL4.GL.CopyConvolutionFilter2D(read<ConvolutionTarget> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyImageSubData ->
            OpenTK.Graphics.OpenGL4.GL.CopyImageSubData(read<int> &ptr, read<ImageTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<ImageTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyMultiTexImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyMultiTexImage1D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyMultiTexImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyMultiTexImage2D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyMultiTexSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyMultiTexSubImage1D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyMultiTexSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyMultiTexSubImage2D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyMultiTexSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyMultiTexSubImage3D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyNamedBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.CopyNamedBufferSubData(read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.CopyTexImage1D ->
            OpenTK.Graphics.OpenGL4.GL.CopyTexImage1D(read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTexImage2D ->
            OpenTK.Graphics.OpenGL4.GL.CopyTexImage2D(read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTexSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.CopyTexSubImage1D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTexSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.CopyTexSubImage2D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTexSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.CopyTexSubImage3D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTextureImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyTextureImage1D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTextureImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyTextureImage2D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTextureSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyTextureSubImage1D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTextureSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyTextureSubImage2D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CopyTextureSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.CopyTextureSubImage3D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.CreateBuffers ->
            OpenTK.Graphics.OpenGL4.GL.CreateBuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateFramebuffers ->
            OpenTK.Graphics.OpenGL4.GL.CreateFramebuffers(read<int> &ptr, read<nativeptr<uint32>> &ptr)
        | InstructionCode.CreateProgramPipelines ->
            OpenTK.Graphics.OpenGL4.GL.CreateProgramPipelines(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateQueries ->
            OpenTK.Graphics.OpenGL4.GL.CreateQueries(read<QueryTarget> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateRenderbuffers ->
            OpenTK.Graphics.OpenGL4.GL.CreateRenderbuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateSamplers ->
            OpenTK.Graphics.OpenGL4.GL.CreateSamplers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateTextures ->
            OpenTK.Graphics.OpenGL4.GL.CreateTextures(read<TextureTarget> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateTransformFeedbacks ->
            OpenTK.Graphics.OpenGL4.GL.CreateTransformFeedbacks(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CreateVertexArrays ->
            OpenTK.Graphics.OpenGL4.GL.CreateVertexArrays(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.CullFace ->
            OpenTK.Graphics.OpenGL4.GL.CullFace(read<CullFaceMode> &ptr)
        | InstructionCode.DebugMessageControl ->
            OpenTK.Graphics.OpenGL4.GL.DebugMessageControl(read<DebugSourceControl> &ptr, read<DebugTypeControl> &ptr, read<DebugSeverityControl> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, (read<int> &ptr = 1))
        | InstructionCode.DeleteBuffer ->
            OpenTK.Graphics.OpenGL4.GL.DeleteBuffer(read<int> &ptr)
        | InstructionCode.DeleteBuffers ->
            OpenTK.Graphics.OpenGL4.GL.DeleteBuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.DeleteFramebuffer(read<int> &ptr)
        | InstructionCode.DeleteFramebuffers ->
            OpenTK.Graphics.OpenGL4.GL.DeleteFramebuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteProgram ->
            OpenTK.Graphics.OpenGL4.GL.DeleteProgram(read<int> &ptr)
        | InstructionCode.DeleteProgramPipeline ->
            OpenTK.Graphics.OpenGL4.GL.DeleteProgramPipeline(read<int> &ptr)
        | InstructionCode.DeleteProgramPipelines ->
            OpenTK.Graphics.OpenGL4.GL.DeleteProgramPipelines(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteQueries ->
            OpenTK.Graphics.OpenGL4.GL.DeleteQueries(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteQuery ->
            OpenTK.Graphics.OpenGL4.GL.DeleteQuery(read<int> &ptr)
        | InstructionCode.DeleteRenderbuffer ->
            OpenTK.Graphics.OpenGL4.GL.DeleteRenderbuffer(read<int> &ptr)
        | InstructionCode.DeleteRenderbuffers ->
            OpenTK.Graphics.OpenGL4.GL.DeleteRenderbuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteSampler ->
            OpenTK.Graphics.OpenGL4.GL.DeleteSampler(read<int> &ptr)
        | InstructionCode.DeleteSamplers ->
            OpenTK.Graphics.OpenGL4.GL.DeleteSamplers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteShader ->
            OpenTK.Graphics.OpenGL4.GL.DeleteShader(read<int> &ptr)
        | InstructionCode.DeleteSync ->
            OpenTK.Graphics.OpenGL4.GL.DeleteSync(read<nativeint> &ptr)
        | InstructionCode.DeleteTexture ->
            OpenTK.Graphics.OpenGL4.GL.DeleteTexture(read<int> &ptr)
        | InstructionCode.DeleteTextures ->
            OpenTK.Graphics.OpenGL4.GL.DeleteTextures(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.DeleteTransformFeedback(read<int> &ptr)
        | InstructionCode.DeleteTransformFeedbacks ->
            OpenTK.Graphics.OpenGL4.GL.DeleteTransformFeedbacks(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DeleteVertexArray ->
            OpenTK.Graphics.OpenGL4.GL.DeleteVertexArray(read<int> &ptr)
        | InstructionCode.DeleteVertexArrays ->
            OpenTK.Graphics.OpenGL4.GL.DeleteVertexArrays(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.DepthFunc ->
            OpenTK.Graphics.OpenGL4.GL.DepthFunc(read<DepthFunction> &ptr)
        | InstructionCode.DepthMask ->
            OpenTK.Graphics.OpenGL4.GL.DepthMask((read<int> &ptr = 1))
        | InstructionCode.DepthRange ->
            OpenTK.Graphics.OpenGL4.GL.DepthRange(read<float> &ptr, read<float> &ptr)
        | InstructionCode.DepthRangeArray ->
            OpenTK.Graphics.OpenGL4.GL.DepthRangeArray(read<int> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.DepthRangeIndexed ->
            OpenTK.Graphics.OpenGL4.GL.DepthRangeIndexed(read<int> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.DetachShader ->
            OpenTK.Graphics.OpenGL4.GL.DetachShader(read<int> &ptr, read<int> &ptr)
        | InstructionCode.Disable ->
            OpenTK.Graphics.OpenGL4.GL.Disable(read<IndexedEnableCap> &ptr, read<int> &ptr)
        | InstructionCode.DisableClientState ->
            OpenTK.Graphics.OpenGL4.GL.Ext.DisableClientState(read<ArrayCap> &ptr, read<int> &ptr)
        | InstructionCode.DisableClientStateIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.DisableClientStateIndexed(read<ArrayCap> &ptr, read<int> &ptr)
        | InstructionCode.DisableIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.DisableIndexed(read<IndexedEnableCap> &ptr, read<int> &ptr)
        | InstructionCode.DisableVertexArray ->
            OpenTK.Graphics.OpenGL4.GL.Ext.DisableVertexArray(read<int> &ptr, read<EnableCap> &ptr)
        | InstructionCode.DisableVertexArrayAttrib ->
            OpenTK.Graphics.OpenGL4.GL.DisableVertexArrayAttrib(read<int> &ptr, read<int> &ptr)
        | InstructionCode.DisableVertexAttribArray ->
            OpenTK.Graphics.OpenGL4.GL.DisableVertexAttribArray(read<int> &ptr)
        | InstructionCode.DispatchCompute ->
            OpenTK.Graphics.OpenGL4.GL.DispatchCompute(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DispatchComputeGroupSize ->
            OpenTK.Graphics.OpenGL4.GL.Arb.DispatchComputeGroupSize(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DispatchComputeIndirect ->
            OpenTK.Graphics.OpenGL4.GL.DispatchComputeIndirect(read<nativeint> &ptr)
        | InstructionCode.DrawArrays ->
            OpenTK.Graphics.OpenGL4.GL.DrawArrays(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawArraysIndirect ->
            OpenTK.Graphics.OpenGL4.GL.DrawArraysIndirect(read<PrimitiveType> &ptr, read<nativeint> &ptr)
        | InstructionCode.DrawArraysInstanced ->
            OpenTK.Graphics.OpenGL4.GL.DrawArraysInstanced(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawArraysInstancedBaseInstance ->
            OpenTK.Graphics.OpenGL4.GL.DrawArraysInstancedBaseInstance(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawBuffer ->
            OpenTK.Graphics.OpenGL4.GL.DrawBuffer(read<DrawBufferMode> &ptr)
        | InstructionCode.DrawBuffers ->
            OpenTK.Graphics.OpenGL4.GL.DrawBuffers(read<int> &ptr, read<nativeptr<DrawBuffersEnum>> &ptr)
        | InstructionCode.DrawElements ->
            OpenTK.Graphics.OpenGL4.GL.DrawElements(read<BeginMode> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<int> &ptr)
        | InstructionCode.DrawElementsBaseVertex ->
            OpenTK.Graphics.OpenGL4.GL.DrawElementsBaseVertex(read<PrimitiveType> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.DrawElementsIndirect ->
            OpenTK.Graphics.OpenGL4.GL.DrawElementsIndirect(read<PrimitiveType> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr)
        | InstructionCode.DrawElementsInstanced ->
            OpenTK.Graphics.OpenGL4.GL.DrawElementsInstanced(read<PrimitiveType> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.DrawElementsInstancedBaseInstance ->
            OpenTK.Graphics.OpenGL4.GL.DrawElementsInstancedBaseInstance(read<PrimitiveType> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawElementsInstancedBaseVertex ->
            OpenTK.Graphics.OpenGL4.GL.DrawElementsInstancedBaseVertex(read<PrimitiveType> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawElementsInstancedBaseVertexBaseInstance ->
            OpenTK.Graphics.OpenGL4.GL.DrawElementsInstancedBaseVertexBaseInstance(read<PrimitiveType> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawRangeElements ->
            OpenTK.Graphics.OpenGL4.GL.DrawRangeElements(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr)
        | InstructionCode.DrawRangeElementsBaseVertex ->
            OpenTK.Graphics.OpenGL4.GL.DrawRangeElementsBaseVertex(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.DrawTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.DrawTransformFeedback(read<PrimitiveType> &ptr, read<int> &ptr)
        | InstructionCode.DrawTransformFeedbackInstanced ->
            OpenTK.Graphics.OpenGL4.GL.DrawTransformFeedbackInstanced(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawTransformFeedbackStream ->
            OpenTK.Graphics.OpenGL4.GL.DrawTransformFeedbackStream(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.DrawTransformFeedbackStreamInstanced ->
            OpenTK.Graphics.OpenGL4.GL.DrawTransformFeedbackStreamInstanced(read<PrimitiveType> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.Enable ->
            OpenTK.Graphics.OpenGL4.GL.Enable(read<IndexedEnableCap> &ptr, read<int> &ptr)
        | InstructionCode.EnableClientState ->
            OpenTK.Graphics.OpenGL4.GL.Ext.EnableClientState(read<ArrayCap> &ptr, read<int> &ptr)
        | InstructionCode.EnableClientStateIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.EnableClientStateIndexed(read<ArrayCap> &ptr, read<int> &ptr)
        | InstructionCode.EnableIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.EnableIndexed(read<IndexedEnableCap> &ptr, read<int> &ptr)
        | InstructionCode.EnableVertexArray ->
            OpenTK.Graphics.OpenGL4.GL.Ext.EnableVertexArray(read<int> &ptr, read<EnableCap> &ptr)
        | InstructionCode.EnableVertexArrayAttrib ->
            OpenTK.Graphics.OpenGL4.GL.EnableVertexArrayAttrib(read<int> &ptr, read<int> &ptr)
        | InstructionCode.EnableVertexAttribArray ->
            OpenTK.Graphics.OpenGL4.GL.EnableVertexAttribArray(read<int> &ptr)
        | InstructionCode.EndConditionalRender ->
            OpenTK.Graphics.OpenGL4.GL.EndConditionalRender()
        | InstructionCode.EndQuery ->
            OpenTK.Graphics.OpenGL4.GL.EndQuery(read<QueryTarget> &ptr)
        | InstructionCode.EndQueryIndexed ->
            OpenTK.Graphics.OpenGL4.GL.EndQueryIndexed(read<QueryTarget> &ptr, read<int> &ptr)
        | InstructionCode.EndTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.EndTransformFeedback()
        | InstructionCode.EvaluateDepthValues ->
            OpenTK.Graphics.OpenGL4.GL.Arb.EvaluateDepthValues()
        | InstructionCode.Finish ->
            OpenTK.Graphics.OpenGL4.GL.Finish()
        | InstructionCode.Flush ->
            OpenTK.Graphics.OpenGL4.GL.Flush()
        | InstructionCode.FlushMappedBufferRange ->
            OpenTK.Graphics.OpenGL4.GL.FlushMappedBufferRange(read<BufferTarget> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.FlushMappedNamedBufferRange ->
            OpenTK.Graphics.OpenGL4.GL.FlushMappedNamedBufferRange(read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferDrawBuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.FramebufferDrawBuffer(read<int> &ptr, read<DrawBufferMode> &ptr)
        | InstructionCode.FramebufferDrawBuffers ->
            OpenTK.Graphics.OpenGL4.GL.Ext.FramebufferDrawBuffers(read<int> &ptr, read<int> &ptr, read<nativeptr<DrawBufferMode>> &ptr)
        | InstructionCode.FramebufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferParameter(read<FramebufferTarget> &ptr, read<FramebufferDefaultParameter> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferReadBuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.FramebufferReadBuffer(read<int> &ptr, read<ReadBufferMode> &ptr)
        | InstructionCode.FramebufferRenderbuffer ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferRenderbuffer(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<RenderbufferTarget> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferSampleLocations ->
            OpenTK.Graphics.OpenGL4.GL.Arb.FramebufferSampleLocations(read<FramebufferTarget> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.FramebufferTexture ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferTexture(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferTexture1D ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferTexture1D(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferTexture2D ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferTexture2D(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferTexture3D ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferTexture3D(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.FramebufferTextureFace ->
            OpenTK.Graphics.OpenGL4.GL.Arb.FramebufferTextureFace(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<int> &ptr, read<int> &ptr, read<TextureTarget> &ptr)
        | InstructionCode.FramebufferTextureLayer ->
            OpenTK.Graphics.OpenGL4.GL.FramebufferTextureLayer(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.FrontFace ->
            OpenTK.Graphics.OpenGL4.GL.FrontFace(read<FrontFaceDirection> &ptr)
        | InstructionCode.GenBuffers ->
            OpenTK.Graphics.OpenGL4.GL.GenBuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenFramebuffers ->
            OpenTK.Graphics.OpenGL4.GL.GenFramebuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenProgramPipelines ->
            OpenTK.Graphics.OpenGL4.GL.GenProgramPipelines(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenQueries ->
            OpenTK.Graphics.OpenGL4.GL.GenQueries(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenRenderbuffers ->
            OpenTK.Graphics.OpenGL4.GL.GenRenderbuffers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenSamplers ->
            OpenTK.Graphics.OpenGL4.GL.GenSamplers(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenTextures ->
            OpenTK.Graphics.OpenGL4.GL.GenTextures(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenTransformFeedbacks ->
            OpenTK.Graphics.OpenGL4.GL.GenTransformFeedbacks(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenVertexArrays ->
            OpenTK.Graphics.OpenGL4.GL.GenVertexArrays(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GenerateMipmap ->
            OpenTK.Graphics.OpenGL4.GL.GenerateMipmap(read<GenerateMipmapTarget> &ptr)
        | InstructionCode.GenerateMultiTexMipmap ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GenerateMultiTexMipmap(read<TextureUnit> &ptr, read<TextureTarget> &ptr)
        | InstructionCode.GenerateTextureMipmap ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GenerateTextureMipmap(read<int> &ptr, read<TextureTarget> &ptr)
        | InstructionCode.GetActiveAtomicCounterBuffer ->
            OpenTK.Graphics.OpenGL4.GL.GetActiveAtomicCounterBuffer(read<int> &ptr, read<int> &ptr, read<AtomicCounterBufferParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetActiveSubroutineUniform ->
            OpenTK.Graphics.OpenGL4.GL.GetActiveSubroutineUniform(read<int> &ptr, read<ShaderType> &ptr, read<int> &ptr, read<ActiveSubroutineUniformParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetActiveUniformBlock ->
            OpenTK.Graphics.OpenGL4.GL.GetActiveUniformBlock(read<int> &ptr, read<int> &ptr, read<ActiveUniformBlockParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetActiveUniforms ->
            OpenTK.Graphics.OpenGL4.GL.GetActiveUniforms(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<ActiveUniformParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetAttachedShaders ->
            OpenTK.Graphics.OpenGL4.GL.GetAttachedShaders(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetBoolean ->
            OpenTK.Graphics.OpenGL4.GL.GetBoolean(read<GetIndexedPName> &ptr, read<int> &ptr, read<nativeptr<bool>> &ptr)
        | InstructionCode.GetBooleanIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetBooleanIndexed(read<BufferTargetArb> &ptr, read<int> &ptr, read<nativeptr<bool>> &ptr)
        | InstructionCode.GetBufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetBufferParameter(read<BufferTarget> &ptr, read<BufferParameterName> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetBufferPointer ->
            OpenTK.Graphics.OpenGL4.GL.GetBufferPointer(read<BufferTarget> &ptr, read<BufferPointer> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.GetBufferSubData(read<BufferTarget> &ptr, read<nativeint> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetColorTable ->
            OpenTK.Graphics.OpenGL4.GL.GetColorTable(read<ColorTableTarget> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetColorTableParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetColorTableParameter(read<ColorTableTarget> &ptr, read<GetColorTableParameterPNameSgi> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetCompressedMultiTexImage ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetCompressedMultiTexImage(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetCompressedTexImage ->
            OpenTK.Graphics.OpenGL4.GL.GetCompressedTexImage(read<TextureTarget> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetCompressedTextureImage ->
            OpenTK.Graphics.OpenGL4.GL.GetCompressedTextureImage(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetCompressedTextureSubImage ->
            OpenTK.Graphics.OpenGL4.GL.GetCompressedTextureSubImage(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetConvolutionFilter ->
            OpenTK.Graphics.OpenGL4.GL.GetConvolutionFilter(read<ConvolutionTarget> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetConvolutionParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetConvolutionParameter(read<ConvolutionTarget> &ptr, read<ConvolutionParameterExt> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetDouble ->
            OpenTK.Graphics.OpenGL4.GL.GetDouble(read<GetIndexedPName> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetDoubleIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetDoubleIndexed(read<TypeEnum> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetFloat ->
            OpenTK.Graphics.OpenGL4.GL.GetFloat(read<GetIndexedPName> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetFloatIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetFloatIndexed(read<TypeEnum> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetFramebufferAttachmentParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetFramebufferAttachmentParameter(read<FramebufferTarget> &ptr, read<FramebufferAttachment> &ptr, read<FramebufferParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetFramebufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetFramebufferParameter(read<FramebufferTarget> &ptr, read<FramebufferDefaultParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetHistogram ->
            OpenTK.Graphics.OpenGL4.GL.GetHistogram(read<HistogramTargetExt> &ptr, (read<int> &ptr = 1), read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetHistogramParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetHistogramParameter(read<HistogramTargetExt> &ptr, read<GetHistogramParameterPNameExt> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetInteger ->
            OpenTK.Graphics.OpenGL4.GL.GetInteger(read<GetIndexedPName> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetInteger64 ->
            OpenTK.Graphics.OpenGL4.GL.GetInteger64(read<GetIndexedPName> &ptr, read<int> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetIntegerIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetIntegerIndexed(read<GetIndexedPName> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetInternalformat ->
            OpenTK.Graphics.OpenGL4.GL.GetInternalformat(read<ImageTarget> &ptr, read<SizedInternalFormat> &ptr, read<InternalFormatParameter> &ptr, read<int> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetMinmax ->
            OpenTK.Graphics.OpenGL4.GL.GetMinmax(read<MinmaxTargetExt> &ptr, (read<int> &ptr = 1), read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetMinmaxParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetMinmaxParameter(read<MinmaxTargetExt> &ptr, read<GetMinmaxParameterPNameExt> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetMultiTexEnv ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetMultiTexEnv(read<TextureUnit> &ptr, read<TextureEnvTarget> &ptr, read<TextureEnvParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetMultiTexGen ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetMultiTexGen(read<TextureUnit> &ptr, read<TextureCoordName> &ptr, read<TextureGenParameter> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetMultiTexImage ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetMultiTexImage(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetMultiTexLevelParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetMultiTexLevelParameter(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetMultiTexParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetMultiTexParameter(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetMultiTexParameterI ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetMultiTexParameterI(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetMultisample ->
            OpenTK.Graphics.OpenGL4.GL.GetMultisample(read<GetMultisamplePName> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetNamedBufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetNamedBufferParameter(read<int> &ptr, read<BufferParameterName> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetNamedBufferPointer ->
            OpenTK.Graphics.OpenGL4.GL.GetNamedBufferPointer(read<int> &ptr, read<BufferPointer> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetNamedBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.GetNamedBufferSubData(read<int> &ptr, read<nativeint> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetNamedFramebufferAttachmentParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetNamedFramebufferAttachmentParameter(read<int> &ptr, read<FramebufferAttachment> &ptr, read<FramebufferParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetNamedFramebufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetNamedFramebufferParameter(read<int> &ptr, read<FramebufferDefaultParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetNamedProgram ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetNamedProgram(read<int> &ptr, read<All> &ptr, read<ProgramPropertyArb> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetNamedProgramLocalParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetNamedProgramLocalParameter(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetNamedProgramLocalParameterI ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetNamedProgramLocalParameterI(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetNamedProgramString ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetNamedProgramString(read<int> &ptr, read<All> &ptr, read<All> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetNamedRenderbufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetNamedRenderbufferParameter(read<int> &ptr, read<RenderbufferParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetPointer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetPointer(read<TypeEnum> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetPointerIndexed ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetPointerIndexed(read<TypeEnum> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetProgram ->
            OpenTK.Graphics.OpenGL4.GL.GetProgram(read<int> &ptr, read<GetProgramParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetProgramBinary ->
            OpenTK.Graphics.OpenGL4.GL.GetProgramBinary(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<BinaryFormat>> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetProgramInterface ->
            OpenTK.Graphics.OpenGL4.GL.GetProgramInterface(read<int> &ptr, read<ProgramInterface> &ptr, read<ProgramInterfaceParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetProgramPipeline ->
            OpenTK.Graphics.OpenGL4.GL.GetProgramPipeline(read<int> &ptr, read<ProgramPipelineParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetProgramResource ->
            OpenTK.Graphics.OpenGL4.GL.GetProgramResource(read<int> &ptr, read<ProgramInterface> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<ProgramProperty>> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetProgramStage ->
            OpenTK.Graphics.OpenGL4.GL.GetProgramStage(read<int> &ptr, read<ShaderType> &ptr, read<ProgramStageParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetQuery ->
            OpenTK.Graphics.OpenGL4.GL.GetQuery(read<QueryTarget> &ptr, read<GetQueryParam> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetQueryBufferObject ->
            OpenTK.Graphics.OpenGL4.GL.GetQueryBufferObject(read<int> &ptr, read<int> &ptr, read<QueryObjectParameterName> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetQueryIndexed ->
            OpenTK.Graphics.OpenGL4.GL.GetQueryIndexed(read<QueryTarget> &ptr, read<int> &ptr, read<GetQueryParam> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetQueryObject ->
            OpenTK.Graphics.OpenGL4.GL.GetQueryObject(read<int> &ptr, read<GetQueryObjectParam> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetRenderbufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetRenderbufferParameter(read<RenderbufferTarget> &ptr, read<RenderbufferParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetSamplerParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetSamplerParameter(read<int> &ptr, read<SamplerParameterName> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetSamplerParameterI ->
            OpenTK.Graphics.OpenGL4.GL.GetSamplerParameterI(read<int> &ptr, read<SamplerParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetSeparableFilter ->
            OpenTK.Graphics.OpenGL4.GL.GetSeparableFilter(read<SeparableTargetExt> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr, read<nativeint> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetShader ->
            OpenTK.Graphics.OpenGL4.GL.GetShader(read<int> &ptr, read<ShaderParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetShaderPrecisionFormat ->
            OpenTK.Graphics.OpenGL4.GL.GetShaderPrecisionFormat(read<ShaderType> &ptr, read<ShaderPrecision> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetSync ->
            OpenTK.Graphics.OpenGL4.GL.GetSync(read<nativeint> &ptr, read<SyncParameterName> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetTexImage ->
            OpenTK.Graphics.OpenGL4.GL.GetTexImage(read<TextureTarget> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetTexLevelParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetTexLevelParameter(read<TextureTarget> &ptr, read<int> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetTexParameter ->
            OpenTK.Graphics.OpenGL4.GL.GetTexParameter(read<TextureTarget> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetTexParameterI ->
            OpenTK.Graphics.OpenGL4.GL.GetTexParameterI(read<TextureTarget> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetTextureImage ->
            OpenTK.Graphics.OpenGL4.GL.GetTextureImage(read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetTextureLevelParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetTextureLevelParameter(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetTextureParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetTextureParameter(read<int> &ptr, read<TextureTarget> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetTextureParameterI ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetTextureParameterI(read<int> &ptr, read<TextureTarget> &ptr, read<GetTextureParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetTextureSubImage ->
            OpenTK.Graphics.OpenGL4.GL.GetTextureSubImage(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.GetTransformFeedback(read<int> &ptr, read<TransformFeedbackIndexedParameter> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetTransformFeedbacki64_ ->
            OpenTK.Graphics.OpenGL4.GL.GetTransformFeedbacki64_(read<int> &ptr, read<TransformFeedbackIndexedParameter> &ptr, read<int> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetUniform ->
            OpenTK.Graphics.OpenGL4.GL.GetUniform(read<int> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetUniformSubroutine ->
            OpenTK.Graphics.OpenGL4.GL.GetUniformSubroutine(read<ShaderType> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetVertexArray ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexArray(read<int> &ptr, read<VertexArrayParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetVertexArrayIndexed ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexArrayIndexed(read<int> &ptr, read<int> &ptr, read<VertexArrayIndexedParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetVertexArrayIndexed64 ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexArrayIndexed64(read<int> &ptr, read<int> &ptr, read<VertexArrayIndexed64Parameter> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.GetVertexArrayInteger ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetVertexArrayInteger(read<int> &ptr, read<int> &ptr, read<VertexArrayPName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetVertexArrayPointer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.GetVertexArrayPointer(read<int> &ptr, read<int> &ptr, read<VertexArrayPName> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetVertexAttrib ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexAttrib(read<int> &ptr, read<VertexAttribParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetVertexAttribI ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexAttribI(read<int> &ptr, read<VertexAttribParameter> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.GetVertexAttribL ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexAttribL(read<int> &ptr, read<VertexAttribParameter> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetVertexAttribPointer ->
            OpenTK.Graphics.OpenGL4.GL.GetVertexAttribPointer(read<int> &ptr, read<VertexAttribPointerParameter> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnColorTable ->
            OpenTK.Graphics.OpenGL4.GL.GetnColorTable(read<ColorTableTarget> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnCompressedTexImage ->
            OpenTK.Graphics.OpenGL4.GL.GetnCompressedTexImage(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnConvolutionFilter ->
            OpenTK.Graphics.OpenGL4.GL.GetnConvolutionFilter(read<ConvolutionTarget> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnHistogram ->
            OpenTK.Graphics.OpenGL4.GL.GetnHistogram(read<HistogramTargetExt> &ptr, (read<int> &ptr = 1), read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnMap ->
            OpenTK.Graphics.OpenGL4.GL.GetnMap(read<MapTarget> &ptr, read<MapQuery> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.GetnMinmax ->
            OpenTK.Graphics.OpenGL4.GL.GetnMinmax(read<MinmaxTargetExt> &ptr, (read<int> &ptr = 1), read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnPixelMap ->
            OpenTK.Graphics.OpenGL4.GL.GetnPixelMap(read<PixelMap> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.GetnPolygonStipple ->
            OpenTK.Graphics.OpenGL4.GL.GetnPolygonStipple(read<int> &ptr, read<nativeptr<byte>> &ptr)
        | InstructionCode.GetnSeparableFilter ->
            OpenTK.Graphics.OpenGL4.GL.GetnSeparableFilter(read<SeparableTargetExt> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr, read<nativeint> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnTexImage ->
            OpenTK.Graphics.OpenGL4.GL.GetnTexImage(read<TextureTarget> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.GetnUniform ->
            OpenTK.Graphics.OpenGL4.GL.GetnUniform(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.Hint ->
            OpenTK.Graphics.OpenGL4.GL.Hint(read<HintTarget> &ptr, read<HintMode> &ptr)
        | InstructionCode.Histogram ->
            OpenTK.Graphics.OpenGL4.GL.Histogram(read<HistogramTargetExt> &ptr, read<int> &ptr, read<InternalFormat> &ptr, (read<int> &ptr = 1))
        | InstructionCode.InvalidateBufferData ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateBufferData(read<int> &ptr)
        | InstructionCode.InvalidateBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateBufferSubData(read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.InvalidateFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateFramebuffer(read<FramebufferTarget> &ptr, read<int> &ptr, read<nativeptr<FramebufferAttachment>> &ptr)
        | InstructionCode.InvalidateNamedFramebufferData ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateNamedFramebufferData(read<int> &ptr, read<int> &ptr, read<nativeptr<FramebufferAttachment>> &ptr)
        | InstructionCode.InvalidateNamedFramebufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateNamedFramebufferSubData(read<int> &ptr, read<int> &ptr, read<nativeptr<FramebufferAttachment>> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.InvalidateSubFramebuffer ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateSubFramebuffer(read<FramebufferTarget> &ptr, read<int> &ptr, read<nativeptr<FramebufferAttachment>> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.InvalidateTexImage ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateTexImage(read<int> &ptr, read<int> &ptr)
        | InstructionCode.InvalidateTexSubImage ->
            OpenTK.Graphics.OpenGL4.GL.InvalidateTexSubImage(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.LineWidth ->
            OpenTK.Graphics.OpenGL4.GL.LineWidth(read<float32> &ptr)
        | InstructionCode.LinkProgram ->
            OpenTK.Graphics.OpenGL4.GL.LinkProgram(read<int> &ptr)
        | InstructionCode.LogicOp ->
            OpenTK.Graphics.OpenGL4.GL.LogicOp(read<LogicOp> &ptr)
        | InstructionCode.MakeImageHandleNonResident ->
            OpenTK.Graphics.OpenGL4.GL.Arb.MakeImageHandleNonResident(read<int64> &ptr)
        | InstructionCode.MakeImageHandleResident ->
            OpenTK.Graphics.OpenGL4.GL.Arb.MakeImageHandleResident(read<int64> &ptr, read<All> &ptr)
        | InstructionCode.MakeTextureHandleNonResident ->
            OpenTK.Graphics.OpenGL4.GL.Arb.MakeTextureHandleNonResident(read<int64> &ptr)
        | InstructionCode.MakeTextureHandleResident ->
            OpenTK.Graphics.OpenGL4.GL.Arb.MakeTextureHandleResident(read<int64> &ptr)
        | InstructionCode.MatrixFrustum ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixFrustum(read<MatrixMode> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.MatrixLoad ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixLoad(read<MatrixMode> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.MatrixLoadIdentity ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixLoadIdentity(read<MatrixMode> &ptr)
        | InstructionCode.MatrixLoadTranspose ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixLoadTranspose(read<MatrixMode> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.MatrixMult ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixMult(read<MatrixMode> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.MatrixMultTranspose ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixMultTranspose(read<MatrixMode> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.MatrixOrtho ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixOrtho(read<MatrixMode> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.MatrixPop ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixPop(read<MatrixMode> &ptr)
        | InstructionCode.MatrixPush ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixPush(read<MatrixMode> &ptr)
        | InstructionCode.MatrixRotate ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixRotate(read<MatrixMode> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.MatrixScale ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixScale(read<MatrixMode> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.MatrixTranslate ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MatrixTranslate(read<MatrixMode> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.MaxShaderCompilerThreads ->
            OpenTK.Graphics.OpenGL4.GL.Arb.MaxShaderCompilerThreads(read<int> &ptr)
        | InstructionCode.MemoryBarrier ->
            OpenTK.Graphics.OpenGL4.GL.MemoryBarrier(read<MemoryBarrierFlags> &ptr)
        | InstructionCode.MemoryBarrierByRegion ->
            OpenTK.Graphics.OpenGL4.GL.MemoryBarrierByRegion(read<MemoryBarrierRegionFlags> &ptr)
        | InstructionCode.MinSampleShading ->
            OpenTK.Graphics.OpenGL4.GL.MinSampleShading(read<float32> &ptr)
        | InstructionCode.Minmax ->
            OpenTK.Graphics.OpenGL4.GL.Minmax(read<MinmaxTargetExt> &ptr, read<InternalFormat> &ptr, (read<int> &ptr = 1))
        | InstructionCode.MultiDrawArrays ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawArrays(read<PrimitiveType> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<int>> &ptr, read<int> &ptr)
        | InstructionCode.MultiDrawArraysIndirect ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawArraysIndirect(read<PrimitiveType> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.MultiDrawArraysIndirectCount ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawArraysIndirectCount(read<PrimitiveType> &ptr, read<nativeint> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.MultiDrawElements ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawElements(read<PrimitiveType> &ptr, read<nativeptr<int>> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.MultiDrawElementsBaseVertex ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawElementsBaseVertex(read<PrimitiveType> &ptr, read<nativeptr<int>> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.MultiDrawElementsIndirect ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawElementsIndirect(read<PrimitiveType> &ptr, read<DrawElementsType> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.MultiDrawElementsIndirectCount ->
            OpenTK.Graphics.OpenGL4.GL.MultiDrawElementsIndirectCount(read<PrimitiveType> &ptr, read<All> &ptr, read<nativeint> &ptr, read<nativeint> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexBuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexBuffer(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<TypeEnum> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexCoordP1 ->
            OpenTK.Graphics.OpenGL4.GL.MultiTexCoordP1(read<TextureUnit> &ptr, read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexCoordP2 ->
            OpenTK.Graphics.OpenGL4.GL.MultiTexCoordP2(read<TextureUnit> &ptr, read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexCoordP3 ->
            OpenTK.Graphics.OpenGL4.GL.MultiTexCoordP3(read<TextureUnit> &ptr, read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexCoordP4 ->
            OpenTK.Graphics.OpenGL4.GL.MultiTexCoordP4(read<TextureUnit> &ptr, read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexCoordPointer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexCoordPointer(read<TextureUnit> &ptr, read<int> &ptr, read<TexCoordPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.MultiTexEnv ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexEnv(read<TextureUnit> &ptr, read<TextureEnvTarget> &ptr, read<TextureEnvParameter> &ptr, read<float32> &ptr)
        | InstructionCode.MultiTexGen ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexGen(read<TextureUnit> &ptr, read<TextureCoordName> &ptr, read<TextureGenParameter> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.MultiTexGend ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexGend(read<TextureUnit> &ptr, read<TextureCoordName> &ptr, read<TextureGenParameter> &ptr, read<float> &ptr)
        | InstructionCode.MultiTexImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexImage1D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.MultiTexImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexImage2D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.MultiTexImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexImage3D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.MultiTexParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexParameter(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<TextureParameterName> &ptr, read<float32> &ptr)
        | InstructionCode.MultiTexParameterI ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexParameterI(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<TextureParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.MultiTexRenderbuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexRenderbuffer(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr)
        | InstructionCode.MultiTexSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexSubImage1D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.MultiTexSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexSubImage2D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.MultiTexSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.MultiTexSubImage3D(read<TextureUnit> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.NamedBufferData ->
            OpenTK.Graphics.OpenGL4.GL.NamedBufferData(read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<BufferUsageHint> &ptr)
        | InstructionCode.NamedBufferPageCommitment ->
            OpenTK.Graphics.OpenGL4.GL.Arb.NamedBufferPageCommitment(read<int> &ptr, read<nativeint> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.NamedBufferStorage ->
            OpenTK.Graphics.OpenGL4.GL.NamedBufferStorage(read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<BufferStorageFlags> &ptr)
        | InstructionCode.NamedBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.NamedBufferSubData(read<int> &ptr, read<nativeint> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.NamedCopyBufferSubData ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedCopyBufferSubData(read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferDrawBuffer ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferDrawBuffer(read<int> &ptr, read<DrawBufferMode> &ptr)
        | InstructionCode.NamedFramebufferDrawBuffers ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferDrawBuffers(read<int> &ptr, read<int> &ptr, read<nativeptr<DrawBuffersEnum>> &ptr)
        | InstructionCode.NamedFramebufferParameter ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferParameter(read<int> &ptr, read<FramebufferDefaultParameter> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferReadBuffer ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferReadBuffer(read<int> &ptr, read<ReadBufferMode> &ptr)
        | InstructionCode.NamedFramebufferRenderbuffer ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferRenderbuffer(read<int> &ptr, read<FramebufferAttachment> &ptr, read<RenderbufferTarget> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferSampleLocations ->
            OpenTK.Graphics.OpenGL4.GL.Arb.NamedFramebufferSampleLocations(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.NamedFramebufferTexture ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferTexture(read<int> &ptr, read<FramebufferAttachment> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferTexture1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedFramebufferTexture1D(read<int> &ptr, read<FramebufferAttachment> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferTexture2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedFramebufferTexture2D(read<int> &ptr, read<FramebufferAttachment> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferTexture3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedFramebufferTexture3D(read<int> &ptr, read<FramebufferAttachment> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedFramebufferTextureFace ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedFramebufferTextureFace(read<int> &ptr, read<FramebufferAttachment> &ptr, read<int> &ptr, read<int> &ptr, read<TextureTarget> &ptr)
        | InstructionCode.NamedFramebufferTextureLayer ->
            OpenTK.Graphics.OpenGL4.GL.NamedFramebufferTextureLayer(read<int> &ptr, read<FramebufferAttachment> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedProgramLocalParameter4 ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedProgramLocalParameter4(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.NamedProgramLocalParameterI4 ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedProgramLocalParameterI4(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedProgramLocalParameters4 ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedProgramLocalParameters4(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.NamedProgramLocalParametersI4 ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedProgramLocalParametersI4(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.NamedProgramString ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedProgramString(read<int> &ptr, read<All> &ptr, read<All> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.NamedRenderbufferStorage ->
            OpenTK.Graphics.OpenGL4.GL.NamedRenderbufferStorage(read<int> &ptr, read<RenderbufferStorage> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedRenderbufferStorageMultisample ->
            OpenTK.Graphics.OpenGL4.GL.NamedRenderbufferStorageMultisample(read<int> &ptr, read<int> &ptr, read<RenderbufferStorage> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NamedRenderbufferStorageMultisampleCoverage ->
            OpenTK.Graphics.OpenGL4.GL.Ext.NamedRenderbufferStorageMultisampleCoverage(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.NormalP3 ->
            OpenTK.Graphics.OpenGL4.GL.NormalP3(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.PatchParameter ->
            OpenTK.Graphics.OpenGL4.GL.PatchParameter(read<PatchParameterFloat> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.PauseTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.PauseTransformFeedback()
        | InstructionCode.PixelStore ->
            OpenTK.Graphics.OpenGL4.GL.PixelStore(read<PixelStoreParameter> &ptr, read<float32> &ptr)
        | InstructionCode.PointParameter ->
            OpenTK.Graphics.OpenGL4.GL.PointParameter(read<PointParameterName> &ptr, read<float32> &ptr)
        | InstructionCode.PointSize ->
            OpenTK.Graphics.OpenGL4.GL.PointSize(read<float32> &ptr)
        | InstructionCode.PolygonMode ->
            OpenTK.Graphics.OpenGL4.GL.PolygonMode(read<MaterialFace> &ptr, read<PolygonMode> &ptr)
        | InstructionCode.PolygonOffset ->
            OpenTK.Graphics.OpenGL4.GL.PolygonOffset(read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.PolygonOffsetClamp ->
            OpenTK.Graphics.OpenGL4.GL.PolygonOffsetClamp(read<float32> &ptr, read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.PopDebugGroup ->
            OpenTK.Graphics.OpenGL4.GL.PopDebugGroup()
        | InstructionCode.PopGroupMarker ->
            OpenTK.Graphics.OpenGL4.GL.Ext.PopGroupMarker()
        | InstructionCode.PrimitiveBoundingBox ->
            OpenTK.Graphics.OpenGL4.GL.Arb.PrimitiveBoundingBox(read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.PrimitiveRestartIndex ->
            OpenTK.Graphics.OpenGL4.GL.PrimitiveRestartIndex(read<int> &ptr)
        | InstructionCode.ProgramBinary ->
            OpenTK.Graphics.OpenGL4.GL.ProgramBinary(read<int> &ptr, read<BinaryFormat> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.ProgramParameter ->
            OpenTK.Graphics.OpenGL4.GL.ProgramParameter(read<int> &ptr, read<ProgramParameterName> &ptr, read<int> &ptr)
        | InstructionCode.ProgramUniform1 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniform1(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniform2 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniform2(read<int> &ptr, read<int> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.ProgramUniform3 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniform3(read<int> &ptr, read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.ProgramUniform4 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniform4(read<int> &ptr, read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.ProgramUniformHandle ->
            OpenTK.Graphics.OpenGL4.GL.Arb.ProgramUniformHandle(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.ProgramUniformMatrix2 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix2(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix2x3 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix2x3(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix2x4 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix2x4(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix3 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix3(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix3x2 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix3x2(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix3x4 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix3x4(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix4 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix4(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix4x2 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix4x2(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProgramUniformMatrix4x3 ->
            OpenTK.Graphics.OpenGL4.GL.ProgramUniformMatrix4x3(read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.ProvokingVertex ->
            OpenTK.Graphics.OpenGL4.GL.ProvokingVertex(read<ProvokingVertexMode> &ptr)
        | InstructionCode.PushClientAttribDefault ->
            OpenTK.Graphics.OpenGL4.GL.Ext.PushClientAttribDefault(read<ClientAttribMask> &ptr)
        | InstructionCode.QueryCounter ->
            OpenTK.Graphics.OpenGL4.GL.QueryCounter(read<int> &ptr, read<QueryCounterTarget> &ptr)
        | InstructionCode.RasterSamples ->
            OpenTK.Graphics.OpenGL4.GL.Ext.RasterSamples(read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.ReadBuffer ->
            OpenTK.Graphics.OpenGL4.GL.ReadBuffer(read<ReadBufferMode> &ptr)
        | InstructionCode.ReadPixels ->
            OpenTK.Graphics.OpenGL4.GL.ReadPixels(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.ReadnPixels ->
            OpenTK.Graphics.OpenGL4.GL.ReadnPixels(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.ReleaseShaderCompiler ->
            OpenTK.Graphics.OpenGL4.GL.ReleaseShaderCompiler()
        | InstructionCode.RenderbufferStorage ->
            OpenTK.Graphics.OpenGL4.GL.RenderbufferStorage(read<RenderbufferTarget> &ptr, read<RenderbufferStorage> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.RenderbufferStorageMultisample ->
            OpenTK.Graphics.OpenGL4.GL.RenderbufferStorageMultisample(read<RenderbufferTarget> &ptr, read<int> &ptr, read<RenderbufferStorage> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.ResetHistogram ->
            OpenTK.Graphics.OpenGL4.GL.ResetHistogram(read<HistogramTargetExt> &ptr)
        | InstructionCode.ResetMinmax ->
            OpenTK.Graphics.OpenGL4.GL.ResetMinmax(read<MinmaxTargetExt> &ptr)
        | InstructionCode.ResumeTransformFeedback ->
            OpenTK.Graphics.OpenGL4.GL.ResumeTransformFeedback()
        | InstructionCode.SampleCoverage ->
            OpenTK.Graphics.OpenGL4.GL.SampleCoverage(read<float32> &ptr, (read<int> &ptr = 1))
        | InstructionCode.SampleMask ->
            OpenTK.Graphics.OpenGL4.GL.SampleMask(read<int> &ptr, read<int> &ptr)
        | InstructionCode.SamplerParameter ->
            OpenTK.Graphics.OpenGL4.GL.SamplerParameter(read<int> &ptr, read<SamplerParameterName> &ptr, read<float32> &ptr)
        | InstructionCode.SamplerParameterI ->
            OpenTK.Graphics.OpenGL4.GL.SamplerParameterI(read<int> &ptr, read<SamplerParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.Scissor ->
            OpenTK.Graphics.OpenGL4.GL.Scissor(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.ScissorArray ->
            OpenTK.Graphics.OpenGL4.GL.ScissorArray(read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.ScissorIndexed ->
            OpenTK.Graphics.OpenGL4.GL.ScissorIndexed(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.SecondaryColorP3 ->
            OpenTK.Graphics.OpenGL4.GL.SecondaryColorP3(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.SeparableFilter2D ->
            OpenTK.Graphics.OpenGL4.GL.SeparableFilter2D(read<SeparableTargetExt> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr, read<nativeint> &ptr)
        | InstructionCode.ShaderBinary ->
            OpenTK.Graphics.OpenGL4.GL.ShaderBinary(read<int> &ptr, read<nativeptr<int>> &ptr, read<BinaryFormat> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.ShaderStorageBlockBinding ->
            OpenTK.Graphics.OpenGL4.GL.ShaderStorageBlockBinding(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.StencilFunc ->
            OpenTK.Graphics.OpenGL4.GL.StencilFunc(read<StencilFunction> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.StencilFuncSeparate ->
            OpenTK.Graphics.OpenGL4.GL.StencilFuncSeparate(read<StencilFace> &ptr, read<StencilFunction> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.StencilMask ->
            OpenTK.Graphics.OpenGL4.GL.StencilMask(read<int> &ptr)
        | InstructionCode.StencilMaskSeparate ->
            OpenTK.Graphics.OpenGL4.GL.StencilMaskSeparate(read<StencilFace> &ptr, read<int> &ptr)
        | InstructionCode.StencilOp ->
            OpenTK.Graphics.OpenGL4.GL.StencilOp(read<StencilOp> &ptr, read<StencilOp> &ptr, read<StencilOp> &ptr)
        | InstructionCode.StencilOpSeparate ->
            OpenTK.Graphics.OpenGL4.GL.StencilOpSeparate(read<StencilFace> &ptr, read<StencilOp> &ptr, read<StencilOp> &ptr, read<StencilOp> &ptr)
        | InstructionCode.TexBuffer ->
            OpenTK.Graphics.OpenGL4.GL.TexBuffer(read<TextureBufferTarget> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr)
        | InstructionCode.TexBufferRange ->
            OpenTK.Graphics.OpenGL4.GL.TexBufferRange(read<TextureBufferTarget> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.TexCoordP1 ->
            OpenTK.Graphics.OpenGL4.GL.TexCoordP1(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.TexCoordP2 ->
            OpenTK.Graphics.OpenGL4.GL.TexCoordP2(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.TexCoordP3 ->
            OpenTK.Graphics.OpenGL4.GL.TexCoordP3(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.TexCoordP4 ->
            OpenTK.Graphics.OpenGL4.GL.TexCoordP4(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.TexImage1D ->
            OpenTK.Graphics.OpenGL4.GL.TexImage1D(read<TextureTarget> &ptr, read<int> &ptr, read<PixelInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TexImage2D ->
            OpenTK.Graphics.OpenGL4.GL.TexImage2D(read<TextureTarget> &ptr, read<int> &ptr, read<PixelInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TexImage2DMultisample ->
            OpenTK.Graphics.OpenGL4.GL.TexImage2DMultisample(read<TextureTargetMultisample> &ptr, read<int> &ptr, read<PixelInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TexImage3D ->
            OpenTK.Graphics.OpenGL4.GL.TexImage3D(read<TextureTarget> &ptr, read<int> &ptr, read<PixelInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TexImage3DMultisample ->
            OpenTK.Graphics.OpenGL4.GL.TexImage3DMultisample(read<TextureTargetMultisample> &ptr, read<int> &ptr, read<PixelInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TexPageCommitment ->
            OpenTK.Graphics.OpenGL4.GL.Arb.TexPageCommitment(read<All> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TexParameter ->
            OpenTK.Graphics.OpenGL4.GL.TexParameter(read<TextureTarget> &ptr, read<TextureParameterName> &ptr, read<float32> &ptr)
        | InstructionCode.TexParameterI ->
            OpenTK.Graphics.OpenGL4.GL.TexParameterI(read<TextureTarget> &ptr, read<TextureParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.TexStorage1D ->
            OpenTK.Graphics.OpenGL4.GL.TexStorage1D(read<TextureTarget1d> &ptr, read<int> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr)
        | InstructionCode.TexStorage2D ->
            OpenTK.Graphics.OpenGL4.GL.TexStorage2D(read<TextureTarget2d> &ptr, read<int> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.TexStorage2DMultisample ->
            OpenTK.Graphics.OpenGL4.GL.TexStorage2DMultisample(read<TextureTargetMultisample2d> &ptr, read<int> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TexStorage3D ->
            OpenTK.Graphics.OpenGL4.GL.TexStorage3D(read<TextureTarget3d> &ptr, read<int> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.TexStorage3DMultisample ->
            OpenTK.Graphics.OpenGL4.GL.TexStorage3DMultisample(read<TextureTargetMultisample3d> &ptr, read<int> &ptr, read<SizedInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TexSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.TexSubImage1D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TexSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.TexSubImage2D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TexSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.TexSubImage3D(read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TextureBarrier ->
            OpenTK.Graphics.OpenGL4.GL.TextureBarrier()
        | InstructionCode.TextureBuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureBuffer(read<int> &ptr, read<TextureTarget> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr)
        | InstructionCode.TextureBufferRange ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureBufferRange(read<int> &ptr, read<TextureTarget> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.TextureImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureImage1D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TextureImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureImage2D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TextureImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureImage3D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<InternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TexturePageCommitment ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TexturePageCommitment(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TextureParameter ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureParameter(read<int> &ptr, read<TextureTarget> &ptr, read<TextureParameterName> &ptr, read<float32> &ptr)
        | InstructionCode.TextureParameterI ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureParameterI(read<int> &ptr, read<TextureTarget> &ptr, read<TextureParameterName> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.TextureRenderbuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureRenderbuffer(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr)
        | InstructionCode.TextureStorage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureStorage1D(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr)
        | InstructionCode.TextureStorage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureStorage2D(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.TextureStorage2DMultisample ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureStorage2DMultisample(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TextureStorage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureStorage3D(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.TextureStorage3DMultisample ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureStorage3DMultisample(read<int> &ptr, read<All> &ptr, read<int> &ptr, read<ExtDirectStateAccess> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1))
        | InstructionCode.TextureSubImage1D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureSubImage1D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TextureSubImage2D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureSubImage2D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TextureSubImage3D ->
            OpenTK.Graphics.OpenGL4.GL.Ext.TextureSubImage3D(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<PixelFormat> &ptr, read<PixelType> &ptr, read<nativeint> &ptr)
        | InstructionCode.TextureView ->
            OpenTK.Graphics.OpenGL4.GL.TextureView(read<int> &ptr, read<TextureTarget> &ptr, read<int> &ptr, read<PixelInternalFormat> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.TransformFeedbackBufferBase ->
            OpenTK.Graphics.OpenGL4.GL.TransformFeedbackBufferBase(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.TransformFeedbackBufferRange ->
            OpenTK.Graphics.OpenGL4.GL.TransformFeedbackBufferRange(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.Uniform1 ->
            OpenTK.Graphics.OpenGL4.GL.Uniform1(read<int> &ptr, read<int> &ptr, read<nativeptr<float>> &ptr)
        | InstructionCode.Uniform2 ->
            OpenTK.Graphics.OpenGL4.GL.Uniform2(read<int> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.Uniform3 ->
            OpenTK.Graphics.OpenGL4.GL.Uniform3(read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.Uniform4 ->
            OpenTK.Graphics.OpenGL4.GL.Uniform4(read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.UniformBlockBinding ->
            OpenTK.Graphics.OpenGL4.GL.UniformBlockBinding(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.UniformHandle ->
            OpenTK.Graphics.OpenGL4.GL.Arb.UniformHandle(read<int> &ptr, read<int> &ptr, read<nativeptr<int64>> &ptr)
        | InstructionCode.UniformMatrix2 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix2(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix2x3 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix2x3(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix2x4 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix2x4(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix3 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix3(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix3x2 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix3x2(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix3x4 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix3x4(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix4 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix4(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix4x2 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix4x2(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformMatrix4x3 ->
            OpenTK.Graphics.OpenGL4.GL.UniformMatrix4x3(read<int> &ptr, read<int> &ptr, (read<int> &ptr = 1), read<nativeptr<float>> &ptr)
        | InstructionCode.UniformSubroutines ->
            OpenTK.Graphics.OpenGL4.GL.UniformSubroutines(read<ShaderType> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.UseProgram ->
            OpenTK.Graphics.OpenGL4.GL.UseProgram(read<int> &ptr)
        | InstructionCode.UseProgramStages ->
            OpenTK.Graphics.OpenGL4.GL.UseProgramStages(read<int> &ptr, read<ProgramStageMask> &ptr, read<int> &ptr)
        | InstructionCode.UseShaderProgram ->
            OpenTK.Graphics.OpenGL4.GL.Ext.UseShaderProgram(read<All> &ptr, read<int> &ptr)
        | InstructionCode.ValidateProgram ->
            OpenTK.Graphics.OpenGL4.GL.ValidateProgram(read<int> &ptr)
        | InstructionCode.ValidateProgramPipeline ->
            OpenTK.Graphics.OpenGL4.GL.ValidateProgramPipeline(read<int> &ptr)
        | InstructionCode.VertexArrayAttribBinding ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayAttribBinding(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayAttribFormat ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayAttribFormat(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<VertexAttribType> &ptr, (read<int> &ptr = 1), read<int> &ptr)
        | InstructionCode.VertexArrayAttribIFormat ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayAttribIFormat(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<VertexAttribType> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayAttribLFormat ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayAttribLFormat(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<VertexAttribType> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayBindVertexBuffer ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayBindVertexBuffer(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayBindingDivisor ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayBindingDivisor(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayColorOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayColorOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<ColorPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayEdgeFlagOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayEdgeFlagOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayElementBuffer ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayElementBuffer(read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayFogCoordOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayFogCoordOffset(read<int> &ptr, read<int> &ptr, read<FogPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayIndexOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayIndexOffset(read<int> &ptr, read<int> &ptr, read<IndexPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayMultiTexCoordOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayMultiTexCoordOffset(read<int> &ptr, read<int> &ptr, read<All> &ptr, read<int> &ptr, read<TexCoordPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayNormalOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayNormalOffset(read<int> &ptr, read<int> &ptr, read<NormalPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArraySecondaryColorOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArraySecondaryColorOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<ColorPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayTexCoordOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayTexCoordOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<TexCoordPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayVertexAttribBinding ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribBinding(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayVertexAttribDivisor ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribDivisor(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayVertexAttribFormat ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribFormat(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<All> &ptr, (read<int> &ptr = 1), read<int> &ptr)
        | InstructionCode.VertexArrayVertexAttribIFormat ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribIFormat(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<All> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayVertexAttribIOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribIOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<VertexAttribEnum> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayVertexAttribLFormat ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribLFormat(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<All> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayVertexAttribLOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribLOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<All> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayVertexAttribOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexAttribOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<VertexAttribPointerType> &ptr, (read<int> &ptr = 1), read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexArrayVertexBindingDivisor ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexBindingDivisor(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayVertexBuffer ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayVertexBuffer(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<int> &ptr)
        | InstructionCode.VertexArrayVertexBuffers ->
            OpenTK.Graphics.OpenGL4.GL.VertexArrayVertexBuffers(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr, read<nativeptr<nativeint>> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.VertexArrayVertexOffset ->
            OpenTK.Graphics.OpenGL4.GL.Ext.VertexArrayVertexOffset(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<VertexPointerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexAttrib1 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttrib1(read<int> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttrib2 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttrib2(read<int> &ptr, read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.VertexAttrib3 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttrib3(read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttrib4 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttrib4(read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttrib4N ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttrib4N(read<int> &ptr, read<byte> &ptr, read<byte> &ptr, read<byte> &ptr, read<byte> &ptr)
        | InstructionCode.VertexAttribBinding ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribBinding(read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribDivisor ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribDivisor(read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribFormat ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribFormat(read<int> &ptr, read<int> &ptr, read<VertexAttribType> &ptr, (read<int> &ptr = 1), read<int> &ptr)
        | InstructionCode.VertexAttribI1 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribI1(read<int> &ptr, read<nativeptr<int>> &ptr)
        | InstructionCode.VertexAttribI2 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribI2(read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribI3 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribI3(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribI4 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribI4(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribIFormat ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribIFormat(read<int> &ptr, read<int> &ptr, read<VertexAttribIntegerType> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribIPointer ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribIPointer(read<int> &ptr, read<int> &ptr, read<VertexAttribIntegerType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexAttribL1 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribL1(read<int> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttribL2 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribL2(read<int> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttribL3 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribL3(read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttribL4 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribL4(read<int> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr, read<float> &ptr)
        | InstructionCode.VertexAttribLFormat ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribLFormat(read<int> &ptr, read<int> &ptr, read<VertexAttribDoubleType> &ptr, read<int> &ptr)
        | InstructionCode.VertexAttribLPointer ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribLPointer(read<int> &ptr, read<int> &ptr, read<VertexAttribDoubleType> &ptr, read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexAttribP1 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribP1(read<int> &ptr, read<PackedPointerType> &ptr, (read<int> &ptr = 1), read<nativeptr<int>> &ptr)
        | InstructionCode.VertexAttribP2 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribP2(read<int> &ptr, read<PackedPointerType> &ptr, (read<int> &ptr = 1), read<int> &ptr)
        | InstructionCode.VertexAttribP3 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribP3(read<int> &ptr, read<PackedPointerType> &ptr, (read<int> &ptr = 1), read<int> &ptr)
        | InstructionCode.VertexAttribP4 ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribP4(read<int> &ptr, read<PackedPointerType> &ptr, (read<int> &ptr = 1), read<int> &ptr)
        | InstructionCode.VertexAttribPointer ->
            OpenTK.Graphics.OpenGL4.GL.VertexAttribPointer(read<int> &ptr, read<int> &ptr, read<VertexAttribPointerType> &ptr, (read<int> &ptr = 1), read<int> &ptr, read<nativeint> &ptr)
        | InstructionCode.VertexBindingDivisor ->
            OpenTK.Graphics.OpenGL4.GL.VertexBindingDivisor(read<int> &ptr, read<int> &ptr)
        | InstructionCode.VertexP2 ->
            OpenTK.Graphics.OpenGL4.GL.VertexP2(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.VertexP3 ->
            OpenTK.Graphics.OpenGL4.GL.VertexP3(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.VertexP4 ->
            OpenTK.Graphics.OpenGL4.GL.VertexP4(read<PackedPointerType> &ptr, read<int> &ptr)
        | InstructionCode.Viewport ->
            OpenTK.Graphics.OpenGL4.GL.Viewport(read<int> &ptr, read<int> &ptr, read<int> &ptr, read<int> &ptr)
        | InstructionCode.ViewportArray ->
            OpenTK.Graphics.OpenGL4.GL.ViewportArray(read<int> &ptr, read<int> &ptr, read<nativeptr<float32>> &ptr)
        | InstructionCode.ViewportIndexed ->
            OpenTK.Graphics.OpenGL4.GL.ViewportIndexed(read<int> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr, read<float32> &ptr)
        | InstructionCode.WaitSync ->
            OpenTK.Graphics.OpenGL4.GL.WaitSync(read<nativeint> &ptr, read<WaitSyncFlags> &ptr, read<int64> &ptr)
        | InstructionCode.WindowRectangles ->
            OpenTK.Graphics.OpenGL4.GL.Ext.WindowRectangles(read<All> &ptr, read<int> &ptr, read<nativeptr<int>> &ptr)
        | code -> 
            Log.warn "unknown instruction: %A" code

        ptr <- fin


    member x.Run() = 
        let mutable ptr = mem
        let e = ptr + offset
        while ptr <> e do
            NativeCommandStream.RunInstruction(&ptr)


    interface IDisposable with
        member x.Dispose() = x.Dispose()
    interface ICommandStream with
        member x.ActiveProgram(program) = x.ActiveProgram(program)
        member x.ActiveShaderProgram(pipeline, program) = x.ActiveShaderProgram(pipeline, program)
        member x.ActiveTexture(texture) = x.ActiveTexture(texture)
        member x.AttachShader(program, shader) = x.AttachShader(program, shader)
        member x.BeginConditionalRender(id, mode) = x.BeginConditionalRender(id, mode)
        member x.BeginQuery(target, id) = x.BeginQuery(target, id)
        member x.BeginQueryIndexed(target, index, id) = x.BeginQueryIndexed(target, index, id)
        member x.BeginTransformFeedback(primitiveMode) = x.BeginTransformFeedback(primitiveMode)
        member x.BindBuffer(target, buffer) = x.BindBuffer(target, buffer)
        member x.BindBufferBase(target, index, buffer) = x.BindBufferBase(target, index, buffer)
        member x.BindBufferRange(target, index, buffer, offset, size) = x.BindBufferRange(target, index, buffer, offset, size)
        member x.BindBuffersBase(target, first, count, buffers) = x.BindBuffersBase(target, first, count, buffers)
        member x.BindBuffersRange(target, first, count, buffers, offsets, sizes) = x.BindBuffersRange(target, first, count, buffers, offsets, sizes)
        member x.BindFramebuffer(target, framebuffer) = x.BindFramebuffer(target, framebuffer)
        member x.BindImageTexture(unit, texture, level, layered, layer, access, format) = x.BindImageTexture(unit, texture, level, layered, layer, access, format)
        member x.BindImageTextures(first, count, textures) = x.BindImageTextures(first, count, textures)
        member x.BindMultiTexture(texunit, target, texture) = x.BindMultiTexture(texunit, target, texture)
        member x.BindProgramPipeline(pipeline) = x.BindProgramPipeline(pipeline)
        member x.BindRenderbuffer(target, renderbuffer) = x.BindRenderbuffer(target, renderbuffer)
        member x.BindSampler(unit, sampler) = x.BindSampler(unit, sampler)
        member x.BindSamplers(first, count, samplers) = x.BindSamplers(first, count, samplers)
        member x.BindTexture(target, texture) = x.BindTexture(target, texture)
        member x.BindTextureUnit(unit, texture) = x.BindTextureUnit(unit, texture)
        member x.BindTextures(first, count, textures) = x.BindTextures(first, count, textures)
        member x.BindTransformFeedback(target, id) = x.BindTransformFeedback(target, id)
        member x.BindVertexArray(array) = x.BindVertexArray(array)
        member x.BindVertexBuffer(bindingindex, buffer, offset, stride) = x.BindVertexBuffer(bindingindex, buffer, offset, stride)
        member x.BindVertexBuffers(first, count, buffers, offsets, strides) = x.BindVertexBuffers(first, count, buffers, offsets, strides)
        member x.BlendColor(red, green, blue, alpha) = x.BlendColor(red, green, blue, alpha)
        member x.BlendEquation(buf, mode) = x.BlendEquation(buf, mode)
        member x.BlendEquationSeparate(buf, modeRGB, modeAlpha) = x.BlendEquationSeparate(buf, modeRGB, modeAlpha)
        member x.BlendFunc(buf, src, dst) = x.BlendFunc(buf, src, dst)
        member x.BlendFuncSeparate(buf, srcRGB, dstRGB, srcAlpha, dstAlpha) = x.BlendFuncSeparate(buf, srcRGB, dstRGB, srcAlpha, dstAlpha)
        member x.BlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter) = x.BlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter)
        member x.BlitNamedFramebuffer(readFramebuffer, drawFramebuffer, srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter) = x.BlitNamedFramebuffer(readFramebuffer, drawFramebuffer, srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter)
        member x.BufferData(target, size, data, usage) = x.BufferData(target, size, data, usage)
        member x.BufferPageCommitment(target, offset, size, commit) = x.BufferPageCommitment(target, offset, size, commit)
        member x.BufferStorage(target, size, data, flags) = x.BufferStorage(target, size, data, flags)
        member x.BufferSubData(target, offset, size, data) = x.BufferSubData(target, offset, size, data)
        member x.ClampColor(target, clamp) = x.ClampColor(target, clamp)
        member x.Clear(mask) = x.Clear(mask)
        member x.ClearBuffer(buffer, drawbuffer, depth, stencil) = x.ClearBuffer(buffer, drawbuffer, depth, stencil)
        member x.ClearBufferData(target, internalformat, format, _type, data) = x.ClearBufferData(target, internalformat, format, _type, data)
        member x.ClearBufferSubData(target, internalformat, offset, size, format, _type, data) = x.ClearBufferSubData(target, internalformat, offset, size, format, _type, data)
        member x.ClearColor(red, green, blue, alpha) = x.ClearColor(red, green, blue, alpha)
        member x.ClearDepth(depth) = x.ClearDepth(depth)
        member x.ClearNamedBufferData(buffer, internalformat, format, _type, data) = x.ClearNamedBufferData(buffer, internalformat, format, _type, data)
        member x.ClearNamedBufferSubData(buffer, internalformat, offset, size, format, _type, data) = x.ClearNamedBufferSubData(buffer, internalformat, offset, size, format, _type, data)
        member x.ClearNamedFramebuffer(framebuffer, buffer, drawbuffer, depth, stencil) = x.ClearNamedFramebuffer(framebuffer, buffer, drawbuffer, depth, stencil)
        member x.ClearStencil(s) = x.ClearStencil(s)
        member x.ClearTexImage(texture, level, format, _type, data) = x.ClearTexImage(texture, level, format, _type, data)
        member x.ClearTexSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, data) = x.ClearTexSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, data)
        member x.ClientAttribDefault(mask) = x.ClientAttribDefault(mask)
        member x.ClipControl(origin, depth) = x.ClipControl(origin, depth)
        member x.ColorMask(index, r, g, b, a) = x.ColorMask(index, r, g, b, a)
        member x.ColorP3(_type, color) = x.ColorP3(_type, color)
        member x.ColorP4(_type, color) = x.ColorP4(_type, color)
        member x.ColorSubTable(target, start, count, format, _type, data) = x.ColorSubTable(target, start, count, format, _type, data)
        member x.ColorTable(target, internalformat, width, format, _type, table) = x.ColorTable(target, internalformat, width, format, _type, table)
        member x.ColorTableParameter(target, pname, _params) = x.ColorTableParameter(target, pname, _params)
        member x.CompileShader(shader) = x.CompileShader(shader)
        member x.CompressedMultiTexImage1D(texunit, target, level, internalformat, width, border, imageSize, bits) = x.CompressedMultiTexImage1D(texunit, target, level, internalformat, width, border, imageSize, bits)
        member x.CompressedMultiTexImage2D(texunit, target, level, internalformat, width, height, border, imageSize, bits) = x.CompressedMultiTexImage2D(texunit, target, level, internalformat, width, height, border, imageSize, bits)
        member x.CompressedMultiTexImage3D(texunit, target, level, internalformat, width, height, depth, border, imageSize, bits) = x.CompressedMultiTexImage3D(texunit, target, level, internalformat, width, height, depth, border, imageSize, bits)
        member x.CompressedMultiTexSubImage1D(texunit, target, level, xoffset, width, format, imageSize, bits) = x.CompressedMultiTexSubImage1D(texunit, target, level, xoffset, width, format, imageSize, bits)
        member x.CompressedMultiTexSubImage2D(texunit, target, level, xoffset, yoffset, width, height, format, imageSize, bits) = x.CompressedMultiTexSubImage2D(texunit, target, level, xoffset, yoffset, width, height, format, imageSize, bits)
        member x.CompressedMultiTexSubImage3D(texunit, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, bits) = x.CompressedMultiTexSubImage3D(texunit, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, bits)
        member x.CompressedTexImage1D(target, level, internalformat, width, border, imageSize, data) = x.CompressedTexImage1D(target, level, internalformat, width, border, imageSize, data)
        member x.CompressedTexImage2D(target, level, internalformat, width, height, border, imageSize, data) = x.CompressedTexImage2D(target, level, internalformat, width, height, border, imageSize, data)
        member x.CompressedTexImage3D(target, level, internalformat, width, height, depth, border, imageSize, data) = x.CompressedTexImage3D(target, level, internalformat, width, height, depth, border, imageSize, data)
        member x.CompressedTexSubImage1D(target, level, xoffset, width, format, imageSize, data) = x.CompressedTexSubImage1D(target, level, xoffset, width, format, imageSize, data)
        member x.CompressedTexSubImage2D(target, level, xoffset, yoffset, width, height, format, imageSize, data) = x.CompressedTexSubImage2D(target, level, xoffset, yoffset, width, height, format, imageSize, data)
        member x.CompressedTexSubImage3D(target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, data) = x.CompressedTexSubImage3D(target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, data)
        member x.CompressedTextureImage1D(texture, target, level, internalformat, width, border, imageSize, bits) = x.CompressedTextureImage1D(texture, target, level, internalformat, width, border, imageSize, bits)
        member x.CompressedTextureImage2D(texture, target, level, internalformat, width, height, border, imageSize, bits) = x.CompressedTextureImage2D(texture, target, level, internalformat, width, height, border, imageSize, bits)
        member x.CompressedTextureImage3D(texture, target, level, internalformat, width, height, depth, border, imageSize, bits) = x.CompressedTextureImage3D(texture, target, level, internalformat, width, height, depth, border, imageSize, bits)
        member x.CompressedTextureSubImage1D(texture, target, level, xoffset, width, format, imageSize, bits) = x.CompressedTextureSubImage1D(texture, target, level, xoffset, width, format, imageSize, bits)
        member x.CompressedTextureSubImage2D(texture, target, level, xoffset, yoffset, width, height, format, imageSize, bits) = x.CompressedTextureSubImage2D(texture, target, level, xoffset, yoffset, width, height, format, imageSize, bits)
        member x.CompressedTextureSubImage3D(texture, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, bits) = x.CompressedTextureSubImage3D(texture, target, level, xoffset, yoffset, zoffset, width, height, depth, format, imageSize, bits)
        member x.ConvolutionFilter1D(target, internalformat, width, format, _type, image) = x.ConvolutionFilter1D(target, internalformat, width, format, _type, image)
        member x.ConvolutionFilter2D(target, internalformat, width, height, format, _type, image) = x.ConvolutionFilter2D(target, internalformat, width, height, format, _type, image)
        member x.ConvolutionParameter(target, pname, _params) = x.ConvolutionParameter(target, pname, _params)
        member x.CopyBufferSubData(readTarget, writeTarget, readOffset, writeOffset, size) = x.CopyBufferSubData(readTarget, writeTarget, readOffset, writeOffset, size)
        member x.CopyColorSubTable(target, start, _x, y, width) = x.CopyColorSubTable(target, start, _x, y, width)
        member x.CopyColorTable(target, internalformat, _x, y, width) = x.CopyColorTable(target, internalformat, _x, y, width)
        member x.CopyConvolutionFilter1D(target, internalformat, _x, y, width) = x.CopyConvolutionFilter1D(target, internalformat, _x, y, width)
        member x.CopyConvolutionFilter2D(target, internalformat, _x, y, width, height) = x.CopyConvolutionFilter2D(target, internalformat, _x, y, width, height)
        member x.CopyImageSubData(srcName, srcTarget, srcLevel, srcX, srcY, srcZ, dstName, dstTarget, dstLevel, dstX, dstY, dstZ, srcWidth, srcHeight, srcDepth) = x.CopyImageSubData(srcName, srcTarget, srcLevel, srcX, srcY, srcZ, dstName, dstTarget, dstLevel, dstX, dstY, dstZ, srcWidth, srcHeight, srcDepth)
        member x.CopyMultiTexImage1D(texunit, target, level, internalformat, _x, y, width, border) = x.CopyMultiTexImage1D(texunit, target, level, internalformat, _x, y, width, border)
        member x.CopyMultiTexImage2D(texunit, target, level, internalformat, _x, y, width, height, border) = x.CopyMultiTexImage2D(texunit, target, level, internalformat, _x, y, width, height, border)
        member x.CopyMultiTexSubImage1D(texunit, target, level, xoffset, _x, y, width) = x.CopyMultiTexSubImage1D(texunit, target, level, xoffset, _x, y, width)
        member x.CopyMultiTexSubImage2D(texunit, target, level, xoffset, yoffset, _x, y, width, height) = x.CopyMultiTexSubImage2D(texunit, target, level, xoffset, yoffset, _x, y, width, height)
        member x.CopyMultiTexSubImage3D(texunit, target, level, xoffset, yoffset, zoffset, _x, y, width, height) = x.CopyMultiTexSubImage3D(texunit, target, level, xoffset, yoffset, zoffset, _x, y, width, height)
        member x.CopyNamedBufferSubData(readBuffer, writeBuffer, readOffset, writeOffset, size) = x.CopyNamedBufferSubData(readBuffer, writeBuffer, readOffset, writeOffset, size)
        member x.CopyTexImage1D(target, level, internalformat, _x, y, width, border) = x.CopyTexImage1D(target, level, internalformat, _x, y, width, border)
        member x.CopyTexImage2D(target, level, internalformat, _x, y, width, height, border) = x.CopyTexImage2D(target, level, internalformat, _x, y, width, height, border)
        member x.CopyTexSubImage1D(target, level, xoffset, _x, y, width) = x.CopyTexSubImage1D(target, level, xoffset, _x, y, width)
        member x.CopyTexSubImage2D(target, level, xoffset, yoffset, _x, y, width, height) = x.CopyTexSubImage2D(target, level, xoffset, yoffset, _x, y, width, height)
        member x.CopyTexSubImage3D(target, level, xoffset, yoffset, zoffset, _x, y, width, height) = x.CopyTexSubImage3D(target, level, xoffset, yoffset, zoffset, _x, y, width, height)
        member x.CopyTextureImage1D(texture, target, level, internalformat, _x, y, width, border) = x.CopyTextureImage1D(texture, target, level, internalformat, _x, y, width, border)
        member x.CopyTextureImage2D(texture, target, level, internalformat, _x, y, width, height, border) = x.CopyTextureImage2D(texture, target, level, internalformat, _x, y, width, height, border)
        member x.CopyTextureSubImage1D(texture, target, level, xoffset, _x, y, width) = x.CopyTextureSubImage1D(texture, target, level, xoffset, _x, y, width)
        member x.CopyTextureSubImage2D(texture, target, level, xoffset, yoffset, _x, y, width, height) = x.CopyTextureSubImage2D(texture, target, level, xoffset, yoffset, _x, y, width, height)
        member x.CopyTextureSubImage3D(texture, target, level, xoffset, yoffset, zoffset, _x, y, width, height) = x.CopyTextureSubImage3D(texture, target, level, xoffset, yoffset, zoffset, _x, y, width, height)
        member x.CreateBuffers(n, buffers) = x.CreateBuffers(n, buffers)
        member x.CreateFramebuffers(n, framebuffers) = x.CreateFramebuffers(n, framebuffers)
        member x.CreateProgramPipelines(n, pipelines) = x.CreateProgramPipelines(n, pipelines)
        member x.CreateQueries(target, n, ids) = x.CreateQueries(target, n, ids)
        member x.CreateRenderbuffers(n, renderbuffers) = x.CreateRenderbuffers(n, renderbuffers)
        member x.CreateSamplers(n, samplers) = x.CreateSamplers(n, samplers)
        member x.CreateTextures(target, n, textures) = x.CreateTextures(target, n, textures)
        member x.CreateTransformFeedbacks(n, ids) = x.CreateTransformFeedbacks(n, ids)
        member x.CreateVertexArrays(n, arrays) = x.CreateVertexArrays(n, arrays)
        member x.CullFace(mode) = x.CullFace(mode)
        member x.DebugMessageControl(source, _type, severity, count, ids, enabled) = x.DebugMessageControl(source, _type, severity, count, ids, enabled)
        member x.DeleteBuffer(buffers) = x.DeleteBuffer(buffers)
        member x.DeleteBuffers(n, buffers) = x.DeleteBuffers(n, buffers)
        member x.DeleteFramebuffer(framebuffers) = x.DeleteFramebuffer(framebuffers)
        member x.DeleteFramebuffers(n, framebuffers) = x.DeleteFramebuffers(n, framebuffers)
        member x.DeleteProgram(program) = x.DeleteProgram(program)
        member x.DeleteProgramPipeline(pipelines) = x.DeleteProgramPipeline(pipelines)
        member x.DeleteProgramPipelines(n, pipelines) = x.DeleteProgramPipelines(n, pipelines)
        member x.DeleteQueries(n, ids) = x.DeleteQueries(n, ids)
        member x.DeleteQuery(ids) = x.DeleteQuery(ids)
        member x.DeleteRenderbuffer(renderbuffers) = x.DeleteRenderbuffer(renderbuffers)
        member x.DeleteRenderbuffers(n, renderbuffers) = x.DeleteRenderbuffers(n, renderbuffers)
        member x.DeleteSampler(samplers) = x.DeleteSampler(samplers)
        member x.DeleteSamplers(count, samplers) = x.DeleteSamplers(count, samplers)
        member x.DeleteShader(shader) = x.DeleteShader(shader)
        member x.DeleteSync(sync) = x.DeleteSync(sync)
        member x.DeleteTexture(textures) = x.DeleteTexture(textures)
        member x.DeleteTextures(n, textures) = x.DeleteTextures(n, textures)
        member x.DeleteTransformFeedback(ids) = x.DeleteTransformFeedback(ids)
        member x.DeleteTransformFeedbacks(n, ids) = x.DeleteTransformFeedbacks(n, ids)
        member x.DeleteVertexArray(arrays) = x.DeleteVertexArray(arrays)
        member x.DeleteVertexArrays(n, arrays) = x.DeleteVertexArrays(n, arrays)
        member x.DepthFunc(func) = x.DepthFunc(func)
        member x.DepthMask(flag) = x.DepthMask(flag)
        member x.DepthRange(near, far) = x.DepthRange(near, far)
        member x.DepthRangeArray(first, count, v) = x.DepthRangeArray(first, count, v)
        member x.DepthRangeIndexed(index, n, f) = x.DepthRangeIndexed(index, n, f)
        member x.DetachShader(program, shader) = x.DetachShader(program, shader)
        member x.Disable(target, index) = x.Disable(target, index)
        member x.DisableClientState(array, index) = x.DisableClientState(array, index)
        member x.DisableClientStateIndexed(array, index) = x.DisableClientStateIndexed(array, index)
        member x.DisableIndexed(target, index) = x.DisableIndexed(target, index)
        member x.DisableVertexArray(vaobj, array) = x.DisableVertexArray(vaobj, array)
        member x.DisableVertexArrayAttrib(vaobj, index) = x.DisableVertexArrayAttrib(vaobj, index)
        member x.DisableVertexAttribArray(index) = x.DisableVertexAttribArray(index)
        member x.DispatchCompute(num_groups_x, num_groups_y, num_groups_z) = x.DispatchCompute(num_groups_x, num_groups_y, num_groups_z)
        member x.DispatchComputeGroupSize(num_groups_x, num_groups_y, num_groups_z, group_size_x, group_size_y, group_size_z) = x.DispatchComputeGroupSize(num_groups_x, num_groups_y, num_groups_z, group_size_x, group_size_y, group_size_z)
        member x.DispatchComputeIndirect(indirect) = x.DispatchComputeIndirect(indirect)
        member x.DrawArrays(mode, first, count) = x.DrawArrays(mode, first, count)
        member x.DrawArraysIndirect(mode, indirect) = x.DrawArraysIndirect(mode, indirect)
        member x.DrawArraysInstanced(mode, first, count, instancecount) = x.DrawArraysInstanced(mode, first, count, instancecount)
        member x.DrawArraysInstancedBaseInstance(mode, first, count, instancecount, baseinstance) = x.DrawArraysInstancedBaseInstance(mode, first, count, instancecount, baseinstance)
        member x.DrawBuffer(buf) = x.DrawBuffer(buf)
        member x.DrawBuffers(n, bufs) = x.DrawBuffers(n, bufs)
        member x.DrawElements(mode, count, _type, offset) = x.DrawElements(mode, count, _type, offset)
        member x.DrawElementsBaseVertex(mode, count, _type, indices, basevertex) = x.DrawElementsBaseVertex(mode, count, _type, indices, basevertex)
        member x.DrawElementsIndirect(mode, _type, indirect) = x.DrawElementsIndirect(mode, _type, indirect)
        member x.DrawElementsInstanced(mode, count, _type, indices, instancecount) = x.DrawElementsInstanced(mode, count, _type, indices, instancecount)
        member x.DrawElementsInstancedBaseInstance(mode, count, _type, indices, instancecount, baseinstance) = x.DrawElementsInstancedBaseInstance(mode, count, _type, indices, instancecount, baseinstance)
        member x.DrawElementsInstancedBaseVertex(mode, count, _type, indices, instancecount, basevertex) = x.DrawElementsInstancedBaseVertex(mode, count, _type, indices, instancecount, basevertex)
        member x.DrawElementsInstancedBaseVertexBaseInstance(mode, count, _type, indices, instancecount, basevertex, baseinstance) = x.DrawElementsInstancedBaseVertexBaseInstance(mode, count, _type, indices, instancecount, basevertex, baseinstance)
        member x.DrawRangeElements(mode, start, _end, count, _type, indices) = x.DrawRangeElements(mode, start, _end, count, _type, indices)
        member x.DrawRangeElementsBaseVertex(mode, start, _end, count, _type, indices, basevertex) = x.DrawRangeElementsBaseVertex(mode, start, _end, count, _type, indices, basevertex)
        member x.DrawTransformFeedback(mode, id) = x.DrawTransformFeedback(mode, id)
        member x.DrawTransformFeedbackInstanced(mode, id, instancecount) = x.DrawTransformFeedbackInstanced(mode, id, instancecount)
        member x.DrawTransformFeedbackStream(mode, id, stream) = x.DrawTransformFeedbackStream(mode, id, stream)
        member x.DrawTransformFeedbackStreamInstanced(mode, id, stream, instancecount) = x.DrawTransformFeedbackStreamInstanced(mode, id, stream, instancecount)
        member x.Enable(target, index) = x.Enable(target, index)
        member x.EnableClientState(array, index) = x.EnableClientState(array, index)
        member x.EnableClientStateIndexed(array, index) = x.EnableClientStateIndexed(array, index)
        member x.EnableIndexed(target, index) = x.EnableIndexed(target, index)
        member x.EnableVertexArray(vaobj, array) = x.EnableVertexArray(vaobj, array)
        member x.EnableVertexArrayAttrib(vaobj, index) = x.EnableVertexArrayAttrib(vaobj, index)
        member x.EnableVertexAttribArray(index) = x.EnableVertexAttribArray(index)
        member x.EndConditionalRender() = x.EndConditionalRender()
        member x.EndQuery(target) = x.EndQuery(target)
        member x.EndQueryIndexed(target, index) = x.EndQueryIndexed(target, index)
        member x.EndTransformFeedback() = x.EndTransformFeedback()
        member x.EvaluateDepthValues() = x.EvaluateDepthValues()
        member x.Finish() = x.Finish()
        member x.Flush() = x.Flush()
        member x.FlushMappedBufferRange(target, offset, length) = x.FlushMappedBufferRange(target, offset, length)
        member x.FlushMappedNamedBufferRange(buffer, offset, length) = x.FlushMappedNamedBufferRange(buffer, offset, length)
        member x.FramebufferDrawBuffer(framebuffer, mode) = x.FramebufferDrawBuffer(framebuffer, mode)
        member x.FramebufferDrawBuffers(framebuffer, n, bufs) = x.FramebufferDrawBuffers(framebuffer, n, bufs)
        member x.FramebufferParameter(target, pname, param) = x.FramebufferParameter(target, pname, param)
        member x.FramebufferReadBuffer(framebuffer, mode) = x.FramebufferReadBuffer(framebuffer, mode)
        member x.FramebufferRenderbuffer(target, attachment, renderbuffertarget, renderbuffer) = x.FramebufferRenderbuffer(target, attachment, renderbuffertarget, renderbuffer)
        member x.FramebufferSampleLocations(target, start, count, v) = x.FramebufferSampleLocations(target, start, count, v)
        member x.FramebufferTexture(target, attachment, texture, level) = x.FramebufferTexture(target, attachment, texture, level)
        member x.FramebufferTexture1D(target, attachment, textarget, texture, level) = x.FramebufferTexture1D(target, attachment, textarget, texture, level)
        member x.FramebufferTexture2D(target, attachment, textarget, texture, level) = x.FramebufferTexture2D(target, attachment, textarget, texture, level)
        member x.FramebufferTexture3D(target, attachment, textarget, texture, level, zoffset) = x.FramebufferTexture3D(target, attachment, textarget, texture, level, zoffset)
        member x.FramebufferTextureFace(target, attachment, texture, level, face) = x.FramebufferTextureFace(target, attachment, texture, level, face)
        member x.FramebufferTextureLayer(target, attachment, texture, level, layer) = x.FramebufferTextureLayer(target, attachment, texture, level, layer)
        member x.FrontFace(mode) = x.FrontFace(mode)
        member x.GenBuffers(n, buffers) = x.GenBuffers(n, buffers)
        member x.GenFramebuffers(n, framebuffers) = x.GenFramebuffers(n, framebuffers)
        member x.GenProgramPipelines(n, pipelines) = x.GenProgramPipelines(n, pipelines)
        member x.GenQueries(n, ids) = x.GenQueries(n, ids)
        member x.GenRenderbuffers(n, renderbuffers) = x.GenRenderbuffers(n, renderbuffers)
        member x.GenSamplers(count, samplers) = x.GenSamplers(count, samplers)
        member x.GenTextures(n, textures) = x.GenTextures(n, textures)
        member x.GenTransformFeedbacks(n, ids) = x.GenTransformFeedbacks(n, ids)
        member x.GenVertexArrays(n, arrays) = x.GenVertexArrays(n, arrays)
        member x.GenerateMipmap(target) = x.GenerateMipmap(target)
        member x.GenerateMultiTexMipmap(texunit, target) = x.GenerateMultiTexMipmap(texunit, target)
        member x.GenerateTextureMipmap(texture, target) = x.GenerateTextureMipmap(texture, target)
        member x.GetActiveAtomicCounterBuffer(program, bufferIndex, pname, _params) = x.GetActiveAtomicCounterBuffer(program, bufferIndex, pname, _params)
        member x.GetActiveSubroutineUniform(program, shadertype, index, pname, values) = x.GetActiveSubroutineUniform(program, shadertype, index, pname, values)
        member x.GetActiveUniformBlock(program, uniformBlockIndex, pname, _params) = x.GetActiveUniformBlock(program, uniformBlockIndex, pname, _params)
        member x.GetActiveUniforms(program, uniformCount, uniformIndices, pname, _params) = x.GetActiveUniforms(program, uniformCount, uniformIndices, pname, _params)
        member x.GetAttachedShaders(program, maxCount, count, shaders) = x.GetAttachedShaders(program, maxCount, count, shaders)
        member x.GetBoolean(target, index, data) = x.GetBoolean(target, index, data)
        member x.GetBooleanIndexed(target, index, data) = x.GetBooleanIndexed(target, index, data)
        member x.GetBufferParameter(target, pname, _params) = x.GetBufferParameter(target, pname, _params)
        member x.GetBufferPointer(target, pname, _params) = x.GetBufferPointer(target, pname, _params)
        member x.GetBufferSubData(target, offset, size, data) = x.GetBufferSubData(target, offset, size, data)
        member x.GetColorTable(target, format, _type, table) = x.GetColorTable(target, format, _type, table)
        member x.GetColorTableParameter(target, pname, _params) = x.GetColorTableParameter(target, pname, _params)
        member x.GetCompressedMultiTexImage(texunit, target, lod, img) = x.GetCompressedMultiTexImage(texunit, target, lod, img)
        member x.GetCompressedTexImage(target, level, img) = x.GetCompressedTexImage(target, level, img)
        member x.GetCompressedTextureImage(texture, level, bufSize, pixels) = x.GetCompressedTextureImage(texture, level, bufSize, pixels)
        member x.GetCompressedTextureSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth, bufSize, pixels) = x.GetCompressedTextureSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth, bufSize, pixels)
        member x.GetConvolutionFilter(target, format, _type, image) = x.GetConvolutionFilter(target, format, _type, image)
        member x.GetConvolutionParameter(target, pname, _params) = x.GetConvolutionParameter(target, pname, _params)
        member x.GetDouble(target, index, data) = x.GetDouble(target, index, data)
        member x.GetDoubleIndexed(target, index, data) = x.GetDoubleIndexed(target, index, data)
        member x.GetFloat(target, index, data) = x.GetFloat(target, index, data)
        member x.GetFloatIndexed(target, index, data) = x.GetFloatIndexed(target, index, data)
        member x.GetFramebufferAttachmentParameter(target, attachment, pname, _params) = x.GetFramebufferAttachmentParameter(target, attachment, pname, _params)
        member x.GetFramebufferParameter(target, pname, _params) = x.GetFramebufferParameter(target, pname, _params)
        member x.GetHistogram(target, reset, format, _type, values) = x.GetHistogram(target, reset, format, _type, values)
        member x.GetHistogramParameter(target, pname, _params) = x.GetHistogramParameter(target, pname, _params)
        member x.GetInteger(target, index, data) = x.GetInteger(target, index, data)
        member x.GetInteger64(target, index, data) = x.GetInteger64(target, index, data)
        member x.GetIntegerIndexed(target, index, data) = x.GetIntegerIndexed(target, index, data)
        member x.GetInternalformat(target, internalformat, pname, bufSize, _params) = x.GetInternalformat(target, internalformat, pname, bufSize, _params)
        member x.GetMinmax(target, reset, format, _type, values) = x.GetMinmax(target, reset, format, _type, values)
        member x.GetMinmaxParameter(target, pname, _params) = x.GetMinmaxParameter(target, pname, _params)
        member x.GetMultiTexEnv(texunit, target, pname, _params) = x.GetMultiTexEnv(texunit, target, pname, _params)
        member x.GetMultiTexGen(texunit, coord, pname, _params) = x.GetMultiTexGen(texunit, coord, pname, _params)
        member x.GetMultiTexImage(texunit, target, level, format, _type, pixels) = x.GetMultiTexImage(texunit, target, level, format, _type, pixels)
        member x.GetMultiTexLevelParameter(texunit, target, level, pname, _params) = x.GetMultiTexLevelParameter(texunit, target, level, pname, _params)
        member x.GetMultiTexParameter(texunit, target, pname, _params) = x.GetMultiTexParameter(texunit, target, pname, _params)
        member x.GetMultiTexParameterI(texunit, target, pname, _params) = x.GetMultiTexParameterI(texunit, target, pname, _params)
        member x.GetMultisample(pname, index, _val) = x.GetMultisample(pname, index, _val)
        member x.GetNamedBufferParameter(buffer, pname, _params) = x.GetNamedBufferParameter(buffer, pname, _params)
        member x.GetNamedBufferPointer(buffer, pname, _params) = x.GetNamedBufferPointer(buffer, pname, _params)
        member x.GetNamedBufferSubData(buffer, offset, size, data) = x.GetNamedBufferSubData(buffer, offset, size, data)
        member x.GetNamedFramebufferAttachmentParameter(framebuffer, attachment, pname, _params) = x.GetNamedFramebufferAttachmentParameter(framebuffer, attachment, pname, _params)
        member x.GetNamedFramebufferParameter(framebuffer, pname, param) = x.GetNamedFramebufferParameter(framebuffer, pname, param)
        member x.GetNamedProgram(program, target, pname, _params) = x.GetNamedProgram(program, target, pname, _params)
        member x.GetNamedProgramLocalParameter(program, target, index, _params) = x.GetNamedProgramLocalParameter(program, target, index, _params)
        member x.GetNamedProgramLocalParameterI(program, target, index, _params) = x.GetNamedProgramLocalParameterI(program, target, index, _params)
        member x.GetNamedProgramString(program, target, pname, string) = x.GetNamedProgramString(program, target, pname, string)
        member x.GetNamedRenderbufferParameter(renderbuffer, pname, _params) = x.GetNamedRenderbufferParameter(renderbuffer, pname, _params)
        member x.GetPointer(pname, index, _params) = x.GetPointer(pname, index, _params)
        member x.GetPointerIndexed(target, index, data) = x.GetPointerIndexed(target, index, data)
        member x.GetProgram(program, pname, _params) = x.GetProgram(program, pname, _params)
        member x.GetProgramBinary(program, bufSize, length, binaryFormat, binary) = x.GetProgramBinary(program, bufSize, length, binaryFormat, binary)
        member x.GetProgramInterface(program, programInterface, pname, _params) = x.GetProgramInterface(program, programInterface, pname, _params)
        member x.GetProgramPipeline(pipeline, pname, _params) = x.GetProgramPipeline(pipeline, pname, _params)
        member x.GetProgramResource(program, programInterface, index, propCount, props, bufSize, length, _params) = x.GetProgramResource(program, programInterface, index, propCount, props, bufSize, length, _params)
        member x.GetProgramStage(program, shadertype, pname, values) = x.GetProgramStage(program, shadertype, pname, values)
        member x.GetQuery(target, pname, _params) = x.GetQuery(target, pname, _params)
        member x.GetQueryBufferObject(id, buffer, pname, offset) = x.GetQueryBufferObject(id, buffer, pname, offset)
        member x.GetQueryIndexed(target, index, pname, _params) = x.GetQueryIndexed(target, index, pname, _params)
        member x.GetQueryObject(id, pname, _params) = x.GetQueryObject(id, pname, _params)
        member x.GetRenderbufferParameter(target, pname, _params) = x.GetRenderbufferParameter(target, pname, _params)
        member x.GetSamplerParameter(sampler, pname, _params) = x.GetSamplerParameter(sampler, pname, _params)
        member x.GetSamplerParameterI(sampler, pname, _params) = x.GetSamplerParameterI(sampler, pname, _params)
        member x.GetSeparableFilter(target, format, _type, row, column, span) = x.GetSeparableFilter(target, format, _type, row, column, span)
        member x.GetShader(shader, pname, _params) = x.GetShader(shader, pname, _params)
        member x.GetShaderPrecisionFormat(shadertype, precisiontype, range, precision) = x.GetShaderPrecisionFormat(shadertype, precisiontype, range, precision)
        member x.GetSync(sync, pname, bufSize, length, values) = x.GetSync(sync, pname, bufSize, length, values)
        member x.GetTexImage(target, level, format, _type, pixels) = x.GetTexImage(target, level, format, _type, pixels)
        member x.GetTexLevelParameter(target, level, pname, _params) = x.GetTexLevelParameter(target, level, pname, _params)
        member x.GetTexParameter(target, pname, _params) = x.GetTexParameter(target, pname, _params)
        member x.GetTexParameterI(target, pname, _params) = x.GetTexParameterI(target, pname, _params)
        member x.GetTextureImage(texture, level, format, _type, bufSize, pixels) = x.GetTextureImage(texture, level, format, _type, bufSize, pixels)
        member x.GetTextureLevelParameter(texture, target, level, pname, _params) = x.GetTextureLevelParameter(texture, target, level, pname, _params)
        member x.GetTextureParameter(texture, target, pname, _params) = x.GetTextureParameter(texture, target, pname, _params)
        member x.GetTextureParameterI(texture, target, pname, _params) = x.GetTextureParameterI(texture, target, pname, _params)
        member x.GetTextureSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, bufSize, pixels) = x.GetTextureSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, bufSize, pixels)
        member x.GetTransformFeedback(xfb, pname, index, param) = x.GetTransformFeedback(xfb, pname, index, param)
        member x.GetTransformFeedbacki64_(xfb, pname, index, param) = x.GetTransformFeedbacki64_(xfb, pname, index, param)
        member x.GetUniform(program, location, _params) = x.GetUniform(program, location, _params)
        member x.GetUniformSubroutine(shadertype, location, _params) = x.GetUniformSubroutine(shadertype, location, _params)
        member x.GetVertexArray(vaobj, pname, param) = x.GetVertexArray(vaobj, pname, param)
        member x.GetVertexArrayIndexed(vaobj, index, pname, param) = x.GetVertexArrayIndexed(vaobj, index, pname, param)
        member x.GetVertexArrayIndexed64(vaobj, index, pname, param) = x.GetVertexArrayIndexed64(vaobj, index, pname, param)
        member x.GetVertexArrayInteger(vaobj, index, pname, param) = x.GetVertexArrayInteger(vaobj, index, pname, param)
        member x.GetVertexArrayPointer(vaobj, index, pname, param) = x.GetVertexArrayPointer(vaobj, index, pname, param)
        member x.GetVertexAttrib(index, pname, _params) = x.GetVertexAttrib(index, pname, _params)
        member x.GetVertexAttribI(index, pname, _params) = x.GetVertexAttribI(index, pname, _params)
        member x.GetVertexAttribL(index, pname, _params) = x.GetVertexAttribL(index, pname, _params)
        member x.GetVertexAttribPointer(index, pname, pointer) = x.GetVertexAttribPointer(index, pname, pointer)
        member x.GetnColorTable(target, format, _type, bufSize, table) = x.GetnColorTable(target, format, _type, bufSize, table)
        member x.GetnCompressedTexImage(target, lod, bufSize, pixels) = x.GetnCompressedTexImage(target, lod, bufSize, pixels)
        member x.GetnConvolutionFilter(target, format, _type, bufSize, image) = x.GetnConvolutionFilter(target, format, _type, bufSize, image)
        member x.GetnHistogram(target, reset, format, _type, bufSize, values) = x.GetnHistogram(target, reset, format, _type, bufSize, values)
        member x.GetnMap(target, query, bufSize, v) = x.GetnMap(target, query, bufSize, v)
        member x.GetnMinmax(target, reset, format, _type, bufSize, values) = x.GetnMinmax(target, reset, format, _type, bufSize, values)
        member x.GetnPixelMap(map, bufSize, values) = x.GetnPixelMap(map, bufSize, values)
        member x.GetnPolygonStipple(bufSize, pattern) = x.GetnPolygonStipple(bufSize, pattern)
        member x.GetnSeparableFilter(target, format, _type, rowBufSize, row, columnBufSize, column, span) = x.GetnSeparableFilter(target, format, _type, rowBufSize, row, columnBufSize, column, span)
        member x.GetnTexImage(target, level, format, _type, bufSize, pixels) = x.GetnTexImage(target, level, format, _type, bufSize, pixels)
        member x.GetnUniform(program, location, bufSize, _params) = x.GetnUniform(program, location, bufSize, _params)
        member x.Hint(target, mode) = x.Hint(target, mode)
        member x.Histogram(target, width, internalformat, sink) = x.Histogram(target, width, internalformat, sink)
        member x.InvalidateBufferData(buffer) = x.InvalidateBufferData(buffer)
        member x.InvalidateBufferSubData(buffer, offset, length) = x.InvalidateBufferSubData(buffer, offset, length)
        member x.InvalidateFramebuffer(target, numAttachments, attachments) = x.InvalidateFramebuffer(target, numAttachments, attachments)
        member x.InvalidateNamedFramebufferData(framebuffer, numAttachments, attachments) = x.InvalidateNamedFramebufferData(framebuffer, numAttachments, attachments)
        member x.InvalidateNamedFramebufferSubData(framebuffer, numAttachments, attachments, _x, y, width, height) = x.InvalidateNamedFramebufferSubData(framebuffer, numAttachments, attachments, _x, y, width, height)
        member x.InvalidateSubFramebuffer(target, numAttachments, attachments, _x, y, width, height) = x.InvalidateSubFramebuffer(target, numAttachments, attachments, _x, y, width, height)
        member x.InvalidateTexImage(texture, level) = x.InvalidateTexImage(texture, level)
        member x.InvalidateTexSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth) = x.InvalidateTexSubImage(texture, level, xoffset, yoffset, zoffset, width, height, depth)
        member x.LineWidth(width) = x.LineWidth(width)
        member x.LinkProgram(program) = x.LinkProgram(program)
        member x.LogicOp(opcode) = x.LogicOp(opcode)
        member x.MakeImageHandleNonResident(handle) = x.MakeImageHandleNonResident(handle)
        member x.MakeImageHandleResident(handle, access) = x.MakeImageHandleResident(handle, access)
        member x.MakeTextureHandleNonResident(handle) = x.MakeTextureHandleNonResident(handle)
        member x.MakeTextureHandleResident(handle) = x.MakeTextureHandleResident(handle)
        member x.MatrixFrustum(mode, left, right, bottom, top, zNear, zFar) = x.MatrixFrustum(mode, left, right, bottom, top, zNear, zFar)
        member x.MatrixLoad(mode, m) = x.MatrixLoad(mode, m)
        member x.MatrixLoadIdentity(mode) = x.MatrixLoadIdentity(mode)
        member x.MatrixLoadTranspose(mode, m) = x.MatrixLoadTranspose(mode, m)
        member x.MatrixMult(mode, m) = x.MatrixMult(mode, m)
        member x.MatrixMultTranspose(mode, m) = x.MatrixMultTranspose(mode, m)
        member x.MatrixOrtho(mode, left, right, bottom, top, zNear, zFar) = x.MatrixOrtho(mode, left, right, bottom, top, zNear, zFar)
        member x.MatrixPop(mode) = x.MatrixPop(mode)
        member x.MatrixPush(mode) = x.MatrixPush(mode)
        member x.MatrixRotate(mode, angle, _x, y, z) = x.MatrixRotate(mode, angle, _x, y, z)
        member x.MatrixScale(mode, _x, y, z) = x.MatrixScale(mode, _x, y, z)
        member x.MatrixTranslate(mode, _x, y, z) = x.MatrixTranslate(mode, _x, y, z)
        member x.MaxShaderCompilerThreads(count) = x.MaxShaderCompilerThreads(count)
        member x.MemoryBarrier(barriers) = x.MemoryBarrier(barriers)
        member x.MemoryBarrierByRegion(barriers) = x.MemoryBarrierByRegion(barriers)
        member x.MinSampleShading(value) = x.MinSampleShading(value)
        member x.Minmax(target, internalformat, sink) = x.Minmax(target, internalformat, sink)
        member x.MultiDrawArrays(mode, first, count, drawcount) = x.MultiDrawArrays(mode, first, count, drawcount)
        member x.MultiDrawArraysIndirect(mode, indirect, drawcount, stride) = x.MultiDrawArraysIndirect(mode, indirect, drawcount, stride)
        member x.MultiDrawArraysIndirectCount(mode, indirect, drawcount, maxdrawcount, stride) = x.MultiDrawArraysIndirectCount(mode, indirect, drawcount, maxdrawcount, stride)
        member x.MultiDrawElements(mode, count, _type, indices, drawcount) = x.MultiDrawElements(mode, count, _type, indices, drawcount)
        member x.MultiDrawElementsBaseVertex(mode, count, _type, indices, drawcount, basevertex) = x.MultiDrawElementsBaseVertex(mode, count, _type, indices, drawcount, basevertex)
        member x.MultiDrawElementsIndirect(mode, _type, indirect, drawcount, stride) = x.MultiDrawElementsIndirect(mode, _type, indirect, drawcount, stride)
        member x.MultiDrawElementsIndirectCount(mode, _type, indirect, drawcount, maxdrawcount, stride) = x.MultiDrawElementsIndirectCount(mode, _type, indirect, drawcount, maxdrawcount, stride)
        member x.MultiTexBuffer(texunit, target, internalformat, buffer) = x.MultiTexBuffer(texunit, target, internalformat, buffer)
        member x.MultiTexCoordP1(texture, _type, coords) = x.MultiTexCoordP1(texture, _type, coords)
        member x.MultiTexCoordP2(texture, _type, coords) = x.MultiTexCoordP2(texture, _type, coords)
        member x.MultiTexCoordP3(texture, _type, coords) = x.MultiTexCoordP3(texture, _type, coords)
        member x.MultiTexCoordP4(texture, _type, coords) = x.MultiTexCoordP4(texture, _type, coords)
        member x.MultiTexCoordPointer(texunit, size, _type, stride, pointer) = x.MultiTexCoordPointer(texunit, size, _type, stride, pointer)
        member x.MultiTexEnv(texunit, target, pname, param) = x.MultiTexEnv(texunit, target, pname, param)
        member x.MultiTexGen(texunit, coord, pname, _params) = x.MultiTexGen(texunit, coord, pname, _params)
        member x.MultiTexGend(texunit, coord, pname, param) = x.MultiTexGend(texunit, coord, pname, param)
        member x.MultiTexImage1D(texunit, target, level, internalformat, width, border, format, _type, pixels) = x.MultiTexImage1D(texunit, target, level, internalformat, width, border, format, _type, pixels)
        member x.MultiTexImage2D(texunit, target, level, internalformat, width, height, border, format, _type, pixels) = x.MultiTexImage2D(texunit, target, level, internalformat, width, height, border, format, _type, pixels)
        member x.MultiTexImage3D(texunit, target, level, internalformat, width, height, depth, border, format, _type, pixels) = x.MultiTexImage3D(texunit, target, level, internalformat, width, height, depth, border, format, _type, pixels)
        member x.MultiTexParameter(texunit, target, pname, param) = x.MultiTexParameter(texunit, target, pname, param)
        member x.MultiTexParameterI(texunit, target, pname, _params) = x.MultiTexParameterI(texunit, target, pname, _params)
        member x.MultiTexRenderbuffer(texunit, target, renderbuffer) = x.MultiTexRenderbuffer(texunit, target, renderbuffer)
        member x.MultiTexSubImage1D(texunit, target, level, xoffset, width, format, _type, pixels) = x.MultiTexSubImage1D(texunit, target, level, xoffset, width, format, _type, pixels)
        member x.MultiTexSubImage2D(texunit, target, level, xoffset, yoffset, width, height, format, _type, pixels) = x.MultiTexSubImage2D(texunit, target, level, xoffset, yoffset, width, height, format, _type, pixels)
        member x.MultiTexSubImage3D(texunit, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels) = x.MultiTexSubImage3D(texunit, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels)
        member x.NamedBufferData(buffer, size, data, usage) = x.NamedBufferData(buffer, size, data, usage)
        member x.NamedBufferPageCommitment(buffer, offset, size, commit) = x.NamedBufferPageCommitment(buffer, offset, size, commit)
        member x.NamedBufferStorage(buffer, size, data, flags) = x.NamedBufferStorage(buffer, size, data, flags)
        member x.NamedBufferSubData(buffer, offset, size, data) = x.NamedBufferSubData(buffer, offset, size, data)
        member x.NamedCopyBufferSubData(readBuffer, writeBuffer, readOffset, writeOffset, size) = x.NamedCopyBufferSubData(readBuffer, writeBuffer, readOffset, writeOffset, size)
        member x.NamedFramebufferDrawBuffer(framebuffer, buf) = x.NamedFramebufferDrawBuffer(framebuffer, buf)
        member x.NamedFramebufferDrawBuffers(framebuffer, n, bufs) = x.NamedFramebufferDrawBuffers(framebuffer, n, bufs)
        member x.NamedFramebufferParameter(framebuffer, pname, param) = x.NamedFramebufferParameter(framebuffer, pname, param)
        member x.NamedFramebufferReadBuffer(framebuffer, src) = x.NamedFramebufferReadBuffer(framebuffer, src)
        member x.NamedFramebufferRenderbuffer(framebuffer, attachment, renderbuffertarget, renderbuffer) = x.NamedFramebufferRenderbuffer(framebuffer, attachment, renderbuffertarget, renderbuffer)
        member x.NamedFramebufferSampleLocations(framebuffer, start, count, v) = x.NamedFramebufferSampleLocations(framebuffer, start, count, v)
        member x.NamedFramebufferTexture(framebuffer, attachment, texture, level) = x.NamedFramebufferTexture(framebuffer, attachment, texture, level)
        member x.NamedFramebufferTexture1D(framebuffer, attachment, textarget, texture, level) = x.NamedFramebufferTexture1D(framebuffer, attachment, textarget, texture, level)
        member x.NamedFramebufferTexture2D(framebuffer, attachment, textarget, texture, level) = x.NamedFramebufferTexture2D(framebuffer, attachment, textarget, texture, level)
        member x.NamedFramebufferTexture3D(framebuffer, attachment, textarget, texture, level, zoffset) = x.NamedFramebufferTexture3D(framebuffer, attachment, textarget, texture, level, zoffset)
        member x.NamedFramebufferTextureFace(framebuffer, attachment, texture, level, face) = x.NamedFramebufferTextureFace(framebuffer, attachment, texture, level, face)
        member x.NamedFramebufferTextureLayer(framebuffer, attachment, texture, level, layer) = x.NamedFramebufferTextureLayer(framebuffer, attachment, texture, level, layer)
        member x.NamedProgramLocalParameter4(program, target, index, _x, y, z, w) = x.NamedProgramLocalParameter4(program, target, index, _x, y, z, w)
        member x.NamedProgramLocalParameterI4(program, target, index, _x, y, z, w) = x.NamedProgramLocalParameterI4(program, target, index, _x, y, z, w)
        member x.NamedProgramLocalParameters4(program, target, index, count, _params) = x.NamedProgramLocalParameters4(program, target, index, count, _params)
        member x.NamedProgramLocalParametersI4(program, target, index, count, _params) = x.NamedProgramLocalParametersI4(program, target, index, count, _params)
        member x.NamedProgramString(program, target, format, len, string) = x.NamedProgramString(program, target, format, len, string)
        member x.NamedRenderbufferStorage(renderbuffer, internalformat, width, height) = x.NamedRenderbufferStorage(renderbuffer, internalformat, width, height)
        member x.NamedRenderbufferStorageMultisample(renderbuffer, samples, internalformat, width, height) = x.NamedRenderbufferStorageMultisample(renderbuffer, samples, internalformat, width, height)
        member x.NamedRenderbufferStorageMultisampleCoverage(renderbuffer, coverageSamples, colorSamples, internalformat, width, height) = x.NamedRenderbufferStorageMultisampleCoverage(renderbuffer, coverageSamples, colorSamples, internalformat, width, height)
        member x.NormalP3(_type, coords) = x.NormalP3(_type, coords)
        member x.PatchParameter(pname, values) = x.PatchParameter(pname, values)
        member x.PauseTransformFeedback() = x.PauseTransformFeedback()
        member x.PixelStore(pname, param) = x.PixelStore(pname, param)
        member x.PointParameter(pname, param) = x.PointParameter(pname, param)
        member x.PointSize(size) = x.PointSize(size)
        member x.PolygonMode(face, mode) = x.PolygonMode(face, mode)
        member x.PolygonOffset(factor, units) = x.PolygonOffset(factor, units)
        member x.PolygonOffsetClamp(factor, units, clamp) = x.PolygonOffsetClamp(factor, units, clamp)
        member x.PopDebugGroup() = x.PopDebugGroup()
        member x.PopGroupMarker() = x.PopGroupMarker()
        member x.PrimitiveBoundingBox(minX, minY, minZ, minW, maxX, maxY, maxZ, maxW) = x.PrimitiveBoundingBox(minX, minY, minZ, minW, maxX, maxY, maxZ, maxW)
        member x.PrimitiveRestartIndex(index) = x.PrimitiveRestartIndex(index)
        member x.ProgramBinary(program, binaryFormat, binary, length) = x.ProgramBinary(program, binaryFormat, binary, length)
        member x.ProgramParameter(program, pname, value) = x.ProgramParameter(program, pname, value)
        member x.ProgramUniform1(program, location, count, value) = x.ProgramUniform1(program, location, count, value)
        member x.ProgramUniform2(program, location, v0, v1) = x.ProgramUniform2(program, location, v0, v1)
        member x.ProgramUniform3(program, location, v0, v1, v2) = x.ProgramUniform3(program, location, v0, v1, v2)
        member x.ProgramUniform4(program, location, v0, v1, v2, v3) = x.ProgramUniform4(program, location, v0, v1, v2, v3)
        member x.ProgramUniformHandle(program, location, count, values) = x.ProgramUniformHandle(program, location, count, values)
        member x.ProgramUniformMatrix2(program, location, count, transpose, value) = x.ProgramUniformMatrix2(program, location, count, transpose, value)
        member x.ProgramUniformMatrix2x3(program, location, count, transpose, value) = x.ProgramUniformMatrix2x3(program, location, count, transpose, value)
        member x.ProgramUniformMatrix2x4(program, location, count, transpose, value) = x.ProgramUniformMatrix2x4(program, location, count, transpose, value)
        member x.ProgramUniformMatrix3(program, location, count, transpose, value) = x.ProgramUniformMatrix3(program, location, count, transpose, value)
        member x.ProgramUniformMatrix3x2(program, location, count, transpose, value) = x.ProgramUniformMatrix3x2(program, location, count, transpose, value)
        member x.ProgramUniformMatrix3x4(program, location, count, transpose, value) = x.ProgramUniformMatrix3x4(program, location, count, transpose, value)
        member x.ProgramUniformMatrix4(program, location, count, transpose, value) = x.ProgramUniformMatrix4(program, location, count, transpose, value)
        member x.ProgramUniformMatrix4x2(program, location, count, transpose, value) = x.ProgramUniformMatrix4x2(program, location, count, transpose, value)
        member x.ProgramUniformMatrix4x3(program, location, count, transpose, value) = x.ProgramUniformMatrix4x3(program, location, count, transpose, value)
        member x.ProvokingVertex(mode) = x.ProvokingVertex(mode)
        member x.PushClientAttribDefault(mask) = x.PushClientAttribDefault(mask)
        member x.QueryCounter(id, target) = x.QueryCounter(id, target)
        member x.RasterSamples(samples, fixedsamplelocations) = x.RasterSamples(samples, fixedsamplelocations)
        member x.ReadBuffer(src) = x.ReadBuffer(src)
        member x.ReadPixels(_x, y, width, height, format, _type, pixels) = x.ReadPixels(_x, y, width, height, format, _type, pixels)
        member x.ReadnPixels(_x, y, width, height, format, _type, bufSize, data) = x.ReadnPixels(_x, y, width, height, format, _type, bufSize, data)
        member x.ReleaseShaderCompiler() = x.ReleaseShaderCompiler()
        member x.RenderbufferStorage(target, internalformat, width, height) = x.RenderbufferStorage(target, internalformat, width, height)
        member x.RenderbufferStorageMultisample(target, samples, internalformat, width, height) = x.RenderbufferStorageMultisample(target, samples, internalformat, width, height)
        member x.ResetHistogram(target) = x.ResetHistogram(target)
        member x.ResetMinmax(target) = x.ResetMinmax(target)
        member x.ResumeTransformFeedback() = x.ResumeTransformFeedback()
        member x.SampleCoverage(value, invert) = x.SampleCoverage(value, invert)
        member x.SampleMask(maskNumber, mask) = x.SampleMask(maskNumber, mask)
        member x.SamplerParameter(sampler, pname, param) = x.SamplerParameter(sampler, pname, param)
        member x.SamplerParameterI(sampler, pname, param) = x.SamplerParameterI(sampler, pname, param)
        member x.Scissor(_x, y, width, height) = x.Scissor(_x, y, width, height)
        member x.ScissorArray(first, count, v) = x.ScissorArray(first, count, v)
        member x.ScissorIndexed(index, left, bottom, width, height) = x.ScissorIndexed(index, left, bottom, width, height)
        member x.SecondaryColorP3(_type, color) = x.SecondaryColorP3(_type, color)
        member x.SeparableFilter2D(target, internalformat, width, height, format, _type, row, column) = x.SeparableFilter2D(target, internalformat, width, height, format, _type, row, column)
        member x.ShaderBinary(count, shaders, binaryformat, binary, length) = x.ShaderBinary(count, shaders, binaryformat, binary, length)
        member x.ShaderStorageBlockBinding(program, storageBlockIndex, storageBlockBinding) = x.ShaderStorageBlockBinding(program, storageBlockIndex, storageBlockBinding)
        member x.StencilFunc(func, ref, mask) = x.StencilFunc(func, ref, mask)
        member x.StencilFuncSeparate(face, func, ref, mask) = x.StencilFuncSeparate(face, func, ref, mask)
        member x.StencilMask(mask) = x.StencilMask(mask)
        member x.StencilMaskSeparate(face, mask) = x.StencilMaskSeparate(face, mask)
        member x.StencilOp(fail, zfail, zpass) = x.StencilOp(fail, zfail, zpass)
        member x.StencilOpSeparate(face, sfail, dpfail, dppass) = x.StencilOpSeparate(face, sfail, dpfail, dppass)
        member x.TexBuffer(target, internalformat, buffer) = x.TexBuffer(target, internalformat, buffer)
        member x.TexBufferRange(target, internalformat, buffer, offset, size) = x.TexBufferRange(target, internalformat, buffer, offset, size)
        member x.TexCoordP1(_type, coords) = x.TexCoordP1(_type, coords)
        member x.TexCoordP2(_type, coords) = x.TexCoordP2(_type, coords)
        member x.TexCoordP3(_type, coords) = x.TexCoordP3(_type, coords)
        member x.TexCoordP4(_type, coords) = x.TexCoordP4(_type, coords)
        member x.TexImage1D(target, level, internalformat, width, border, format, _type, pixels) = x.TexImage1D(target, level, internalformat, width, border, format, _type, pixels)
        member x.TexImage2D(target, level, internalformat, width, height, border, format, _type, pixels) = x.TexImage2D(target, level, internalformat, width, height, border, format, _type, pixels)
        member x.TexImage2DMultisample(target, samples, internalformat, width, height, fixedsamplelocations) = x.TexImage2DMultisample(target, samples, internalformat, width, height, fixedsamplelocations)
        member x.TexImage3D(target, level, internalformat, width, height, depth, border, format, _type, pixels) = x.TexImage3D(target, level, internalformat, width, height, depth, border, format, _type, pixels)
        member x.TexImage3DMultisample(target, samples, internalformat, width, height, depth, fixedsamplelocations) = x.TexImage3DMultisample(target, samples, internalformat, width, height, depth, fixedsamplelocations)
        member x.TexPageCommitment(target, level, xoffset, yoffset, zoffset, width, height, depth, commit) = x.TexPageCommitment(target, level, xoffset, yoffset, zoffset, width, height, depth, commit)
        member x.TexParameter(target, pname, param) = x.TexParameter(target, pname, param)
        member x.TexParameterI(target, pname, _params) = x.TexParameterI(target, pname, _params)
        member x.TexStorage1D(target, levels, internalformat, width) = x.TexStorage1D(target, levels, internalformat, width)
        member x.TexStorage2D(target, levels, internalformat, width, height) = x.TexStorage2D(target, levels, internalformat, width, height)
        member x.TexStorage2DMultisample(target, samples, internalformat, width, height, fixedsamplelocations) = x.TexStorage2DMultisample(target, samples, internalformat, width, height, fixedsamplelocations)
        member x.TexStorage3D(target, levels, internalformat, width, height, depth) = x.TexStorage3D(target, levels, internalformat, width, height, depth)
        member x.TexStorage3DMultisample(target, samples, internalformat, width, height, depth, fixedsamplelocations) = x.TexStorage3DMultisample(target, samples, internalformat, width, height, depth, fixedsamplelocations)
        member x.TexSubImage1D(target, level, xoffset, width, format, _type, pixels) = x.TexSubImage1D(target, level, xoffset, width, format, _type, pixels)
        member x.TexSubImage2D(target, level, xoffset, yoffset, width, height, format, _type, pixels) = x.TexSubImage2D(target, level, xoffset, yoffset, width, height, format, _type, pixels)
        member x.TexSubImage3D(target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels) = x.TexSubImage3D(target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels)
        member x.TextureBarrier() = x.TextureBarrier()
        member x.TextureBuffer(texture, target, internalformat, buffer) = x.TextureBuffer(texture, target, internalformat, buffer)
        member x.TextureBufferRange(texture, target, internalformat, buffer, offset, size) = x.TextureBufferRange(texture, target, internalformat, buffer, offset, size)
        member x.TextureImage1D(texture, target, level, internalformat, width, border, format, _type, pixels) = x.TextureImage1D(texture, target, level, internalformat, width, border, format, _type, pixels)
        member x.TextureImage2D(texture, target, level, internalformat, width, height, border, format, _type, pixels) = x.TextureImage2D(texture, target, level, internalformat, width, height, border, format, _type, pixels)
        member x.TextureImage3D(texture, target, level, internalformat, width, height, depth, border, format, _type, pixels) = x.TextureImage3D(texture, target, level, internalformat, width, height, depth, border, format, _type, pixels)
        member x.TexturePageCommitment(texture, level, xoffset, yoffset, zoffset, width, height, depth, commit) = x.TexturePageCommitment(texture, level, xoffset, yoffset, zoffset, width, height, depth, commit)
        member x.TextureParameter(texture, target, pname, param) = x.TextureParameter(texture, target, pname, param)
        member x.TextureParameterI(texture, target, pname, _params) = x.TextureParameterI(texture, target, pname, _params)
        member x.TextureRenderbuffer(texture, target, renderbuffer) = x.TextureRenderbuffer(texture, target, renderbuffer)
        member x.TextureStorage1D(texture, target, levels, internalformat, width) = x.TextureStorage1D(texture, target, levels, internalformat, width)
        member x.TextureStorage2D(texture, target, levels, internalformat, width, height) = x.TextureStorage2D(texture, target, levels, internalformat, width, height)
        member x.TextureStorage2DMultisample(texture, target, samples, internalformat, width, height, fixedsamplelocations) = x.TextureStorage2DMultisample(texture, target, samples, internalformat, width, height, fixedsamplelocations)
        member x.TextureStorage3D(texture, target, levels, internalformat, width, height, depth) = x.TextureStorage3D(texture, target, levels, internalformat, width, height, depth)
        member x.TextureStorage3DMultisample(texture, target, samples, internalformat, width, height, depth, fixedsamplelocations) = x.TextureStorage3DMultisample(texture, target, samples, internalformat, width, height, depth, fixedsamplelocations)
        member x.TextureSubImage1D(texture, target, level, xoffset, width, format, _type, pixels) = x.TextureSubImage1D(texture, target, level, xoffset, width, format, _type, pixels)
        member x.TextureSubImage2D(texture, target, level, xoffset, yoffset, width, height, format, _type, pixels) = x.TextureSubImage2D(texture, target, level, xoffset, yoffset, width, height, format, _type, pixels)
        member x.TextureSubImage3D(texture, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels) = x.TextureSubImage3D(texture, target, level, xoffset, yoffset, zoffset, width, height, depth, format, _type, pixels)
        member x.TextureView(texture, target, origtexture, internalformat, minlevel, numlevels, minlayer, numlayers) = x.TextureView(texture, target, origtexture, internalformat, minlevel, numlevels, minlayer, numlayers)
        member x.TransformFeedbackBufferBase(xfb, index, buffer) = x.TransformFeedbackBufferBase(xfb, index, buffer)
        member x.TransformFeedbackBufferRange(xfb, index, buffer, offset, size) = x.TransformFeedbackBufferRange(xfb, index, buffer, offset, size)
        member x.Uniform1(location, count, value) = x.Uniform1(location, count, value)
        member x.Uniform2(location, _x, y) = x.Uniform2(location, _x, y)
        member x.Uniform3(location, _x, y, z) = x.Uniform3(location, _x, y, z)
        member x.Uniform4(location, _x, y, z, w) = x.Uniform4(location, _x, y, z, w)
        member x.UniformBlockBinding(program, uniformBlockIndex, uniformBlockBinding) = x.UniformBlockBinding(program, uniformBlockIndex, uniformBlockBinding)
        member x.UniformHandle(location, count, value) = x.UniformHandle(location, count, value)
        member x.UniformMatrix2(location, count, transpose, value) = x.UniformMatrix2(location, count, transpose, value)
        member x.UniformMatrix2x3(location, count, transpose, value) = x.UniformMatrix2x3(location, count, transpose, value)
        member x.UniformMatrix2x4(location, count, transpose, value) = x.UniformMatrix2x4(location, count, transpose, value)
        member x.UniformMatrix3(location, count, transpose, value) = x.UniformMatrix3(location, count, transpose, value)
        member x.UniformMatrix3x2(location, count, transpose, value) = x.UniformMatrix3x2(location, count, transpose, value)
        member x.UniformMatrix3x4(location, count, transpose, value) = x.UniformMatrix3x4(location, count, transpose, value)
        member x.UniformMatrix4(location, count, transpose, value) = x.UniformMatrix4(location, count, transpose, value)
        member x.UniformMatrix4x2(location, count, transpose, value) = x.UniformMatrix4x2(location, count, transpose, value)
        member x.UniformMatrix4x3(location, count, transpose, value) = x.UniformMatrix4x3(location, count, transpose, value)
        member x.UniformSubroutines(shadertype, count, indices) = x.UniformSubroutines(shadertype, count, indices)
        member x.UseProgram(program) = x.UseProgram(program)
        member x.UseProgramStages(pipeline, stages, program) = x.UseProgramStages(pipeline, stages, program)
        member x.UseShaderProgram(_type, program) = x.UseShaderProgram(_type, program)
        member x.ValidateProgram(program) = x.ValidateProgram(program)
        member x.ValidateProgramPipeline(pipeline) = x.ValidateProgramPipeline(pipeline)
        member x.VertexArrayAttribBinding(vaobj, attribindex, bindingindex) = x.VertexArrayAttribBinding(vaobj, attribindex, bindingindex)
        member x.VertexArrayAttribFormat(vaobj, attribindex, size, _type, normalized, relativeoffset) = x.VertexArrayAttribFormat(vaobj, attribindex, size, _type, normalized, relativeoffset)
        member x.VertexArrayAttribIFormat(vaobj, attribindex, size, _type, relativeoffset) = x.VertexArrayAttribIFormat(vaobj, attribindex, size, _type, relativeoffset)
        member x.VertexArrayAttribLFormat(vaobj, attribindex, size, _type, relativeoffset) = x.VertexArrayAttribLFormat(vaobj, attribindex, size, _type, relativeoffset)
        member x.VertexArrayBindVertexBuffer(vaobj, bindingindex, buffer, offset, stride) = x.VertexArrayBindVertexBuffer(vaobj, bindingindex, buffer, offset, stride)
        member x.VertexArrayBindingDivisor(vaobj, bindingindex, divisor) = x.VertexArrayBindingDivisor(vaobj, bindingindex, divisor)
        member x.VertexArrayColorOffset(vaobj, buffer, size, _type, stride, offset) = x.VertexArrayColorOffset(vaobj, buffer, size, _type, stride, offset)
        member x.VertexArrayEdgeFlagOffset(vaobj, buffer, stride, offset) = x.VertexArrayEdgeFlagOffset(vaobj, buffer, stride, offset)
        member x.VertexArrayElementBuffer(vaobj, buffer) = x.VertexArrayElementBuffer(vaobj, buffer)
        member x.VertexArrayFogCoordOffset(vaobj, buffer, _type, stride, offset) = x.VertexArrayFogCoordOffset(vaobj, buffer, _type, stride, offset)
        member x.VertexArrayIndexOffset(vaobj, buffer, _type, stride, offset) = x.VertexArrayIndexOffset(vaobj, buffer, _type, stride, offset)
        member x.VertexArrayMultiTexCoordOffset(vaobj, buffer, texunit, size, _type, stride, offset) = x.VertexArrayMultiTexCoordOffset(vaobj, buffer, texunit, size, _type, stride, offset)
        member x.VertexArrayNormalOffset(vaobj, buffer, _type, stride, offset) = x.VertexArrayNormalOffset(vaobj, buffer, _type, stride, offset)
        member x.VertexArraySecondaryColorOffset(vaobj, buffer, size, _type, stride, offset) = x.VertexArraySecondaryColorOffset(vaobj, buffer, size, _type, stride, offset)
        member x.VertexArrayTexCoordOffset(vaobj, buffer, size, _type, stride, offset) = x.VertexArrayTexCoordOffset(vaobj, buffer, size, _type, stride, offset)
        member x.VertexArrayVertexAttribBinding(vaobj, attribindex, bindingindex) = x.VertexArrayVertexAttribBinding(vaobj, attribindex, bindingindex)
        member x.VertexArrayVertexAttribDivisor(vaobj, index, divisor) = x.VertexArrayVertexAttribDivisor(vaobj, index, divisor)
        member x.VertexArrayVertexAttribFormat(vaobj, attribindex, size, _type, normalized, relativeoffset) = x.VertexArrayVertexAttribFormat(vaobj, attribindex, size, _type, normalized, relativeoffset)
        member x.VertexArrayVertexAttribIFormat(vaobj, attribindex, size, _type, relativeoffset) = x.VertexArrayVertexAttribIFormat(vaobj, attribindex, size, _type, relativeoffset)
        member x.VertexArrayVertexAttribIOffset(vaobj, buffer, index, size, _type, stride, offset) = x.VertexArrayVertexAttribIOffset(vaobj, buffer, index, size, _type, stride, offset)
        member x.VertexArrayVertexAttribLFormat(vaobj, attribindex, size, _type, relativeoffset) = x.VertexArrayVertexAttribLFormat(vaobj, attribindex, size, _type, relativeoffset)
        member x.VertexArrayVertexAttribLOffset(vaobj, buffer, index, size, _type, stride, offset) = x.VertexArrayVertexAttribLOffset(vaobj, buffer, index, size, _type, stride, offset)
        member x.VertexArrayVertexAttribOffset(vaobj, buffer, index, size, _type, normalized, stride, offset) = x.VertexArrayVertexAttribOffset(vaobj, buffer, index, size, _type, normalized, stride, offset)
        member x.VertexArrayVertexBindingDivisor(vaobj, bindingindex, divisor) = x.VertexArrayVertexBindingDivisor(vaobj, bindingindex, divisor)
        member x.VertexArrayVertexBuffer(vaobj, bindingindex, buffer, offset, stride) = x.VertexArrayVertexBuffer(vaobj, bindingindex, buffer, offset, stride)
        member x.VertexArrayVertexBuffers(vaobj, first, count, buffers, offsets, strides) = x.VertexArrayVertexBuffers(vaobj, first, count, buffers, offsets, strides)
        member x.VertexArrayVertexOffset(vaobj, buffer, size, _type, stride, offset) = x.VertexArrayVertexOffset(vaobj, buffer, size, _type, stride, offset)
        member x.VertexAttrib1(index, _x) = x.VertexAttrib1(index, _x)
        member x.VertexAttrib2(index, _x, y) = x.VertexAttrib2(index, _x, y)
        member x.VertexAttrib3(index, _x, y, z) = x.VertexAttrib3(index, _x, y, z)
        member x.VertexAttrib4(index, _x, y, z, w) = x.VertexAttrib4(index, _x, y, z, w)
        member x.VertexAttrib4N(index, _x, y, z, w) = x.VertexAttrib4N(index, _x, y, z, w)
        member x.VertexAttribBinding(attribindex, bindingindex) = x.VertexAttribBinding(attribindex, bindingindex)
        member x.VertexAttribDivisor(index, divisor) = x.VertexAttribDivisor(index, divisor)
        member x.VertexAttribFormat(attribindex, size, _type, normalized, relativeoffset) = x.VertexAttribFormat(attribindex, size, _type, normalized, relativeoffset)
        member x.VertexAttribI1(index, v) = x.VertexAttribI1(index, v)
        member x.VertexAttribI2(index, _x, y) = x.VertexAttribI2(index, _x, y)
        member x.VertexAttribI3(index, _x, y, z) = x.VertexAttribI3(index, _x, y, z)
        member x.VertexAttribI4(index, _x, y, z, w) = x.VertexAttribI4(index, _x, y, z, w)
        member x.VertexAttribIFormat(attribindex, size, _type, relativeoffset) = x.VertexAttribIFormat(attribindex, size, _type, relativeoffset)
        member x.VertexAttribIPointer(index, size, _type, stride, pointer) = x.VertexAttribIPointer(index, size, _type, stride, pointer)
        member x.VertexAttribL1(index, _x) = x.VertexAttribL1(index, _x)
        member x.VertexAttribL2(index, _x, y) = x.VertexAttribL2(index, _x, y)
        member x.VertexAttribL3(index, _x, y, z) = x.VertexAttribL3(index, _x, y, z)
        member x.VertexAttribL4(index, _x, y, z, w) = x.VertexAttribL4(index, _x, y, z, w)
        member x.VertexAttribLFormat(attribindex, size, _type, relativeoffset) = x.VertexAttribLFormat(attribindex, size, _type, relativeoffset)
        member x.VertexAttribLPointer(index, size, _type, stride, pointer) = x.VertexAttribLPointer(index, size, _type, stride, pointer)
        member x.VertexAttribP1(index, _type, normalized, value) = x.VertexAttribP1(index, _type, normalized, value)
        member x.VertexAttribP2(index, _type, normalized, value) = x.VertexAttribP2(index, _type, normalized, value)
        member x.VertexAttribP3(index, _type, normalized, value) = x.VertexAttribP3(index, _type, normalized, value)
        member x.VertexAttribP4(index, _type, normalized, value) = x.VertexAttribP4(index, _type, normalized, value)
        member x.VertexAttribPointer(index, size, _type, normalized, stride, pointer) = x.VertexAttribPointer(index, size, _type, normalized, stride, pointer)
        member x.VertexBindingDivisor(bindingindex, divisor) = x.VertexBindingDivisor(bindingindex, divisor)
        member x.VertexP2(_type, value) = x.VertexP2(_type, value)
        member x.VertexP3(_type, value) = x.VertexP3(_type, value)
        member x.VertexP4(_type, value) = x.VertexP4(_type, value)
        member x.Viewport(_x, y, width, height) = x.Viewport(_x, y, width, height)
        member x.ViewportArray(first, count, v) = x.ViewportArray(first, count, v)
        member x.ViewportIndexed(index, _x, y, w, h) = x.ViewportIndexed(index, _x, y, w, h)
        member x.WaitSync(sync, flags, timeout) = x.WaitSync(sync, flags, timeout)
        member x.WindowRectangles(mode, count, box) = x.WindowRectangles(mode, count, box)
        member x.Run() = x.Run()
        member x.Clear() = x.Clear()
        member x.Count = x.Count


