namespace Aardvark.Rendering.GL

open System
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Rendering


module InstructionCompiler =

    type CompilerState = 
        { resourceManager       : ResourceManager
          resources             : list<IChangeableResource>
          registrations         : list<IDisposable>
          percontext            : list<unit -> unit> 
          statistics            : EventSource<FrameStatistics>
          dependencies          : list<IMod> } with

        member x.Dispose() =
            x.resources |> Seq.iter (fun r -> r.Dispose())
            x.registrations |> Seq.iter (fun r -> r.Dispose())

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    /// <summary>
    /// Compile represents a stateful compilation using IDynamicFragment
    /// while maintaining a CompilerState.
    /// NOTE: Compile mutates the IDynamicFragment internally and is therefore
    ///       impure. This was a design choice in order to allow for an efficient
    ///       implementation.
    /// </summary>
    type Compile = { compile : CompilerState -> IDynamicFragment -> CompilerState }


    /// <summary>
    /// Since compilation may depend on state-values we augment Compile
    /// with a special type for reading/writing state-values.
    /// </summary>
    type CompileValue<'a> = { run : CompilerState -> CompilerState * 'a }

    /// <summary>
    /// CompileBuilder implements the Compile-Monad.
    /// NOTE: CompileBuilder has Bind/Return overloads
    ///       that can be used with CompileValue&lt;'a&gt; which
    ///       are not intended to be used directly.
    /// </summary>
    type CompileBuilder() =
        /// <summary>
        /// Evaluates the given CompileValue&lt;'a&gt; inside the state-monad and
        /// allows compilation depending on the current state.
        /// </summary>
        member x.Bind(c : CompileValue<'a>, f : 'a -> Compile) : Compile =
            { compile = fun s p ->
                let s,v = c.run s
                (f v).compile s p
            }

        /// <summary>
        /// Evaluates the given CompileValue&lt;'a&gt; inside the state-monad and
        /// allows for dependent computations on its value.
        /// </summary>
        member x.Bind(c : CompileValue<'a>, f : 'a -> CompileValue<'b>) : CompileValue<'b> =
            { run = fun s->
                let s,v = c.run s
                (f v).run s
            }

        /// <summary>
        /// Lifts the given value into a CompileValue&lt;'a&gt;.
        /// </summary>
        member x.Return(v : 'a) : CompileValue<'a> =
            { run = fun s -> s, v }


        /// <summary>
        /// Appends the given instruction to the DynamicFragment being compiled
        /// </summary>
        member x.Yield(i : Instruction) =
            { compile = fun s p ->
                
                // append the instruction
                [i] |> p.Append |> ignore

                // modify the frame-statistcis according to the instruction
                i |> InstructionStatistics.add s.statistics

                // return the modified state
                s
            }

        /// <summary>
        /// Appends the given changeable instruction to the DynamicFragment
        /// and annotates the CompilerState with the given list of dependencies
        /// </summary>
        member x.YieldFrom(instructionAndDeps : IMod<Instruction> * list<IMod>) =
            { compile = fun s p ->
                let i, deps = instructionAndDeps

                // evaluate the initial instruction-value
                let i' = Mod.force i

                // initially append the instruction to the DynamicFragment
                let id = p.Append [i']

                // add the current instruction to the statistics
                InstructionStatistics.add s.statistics i'

                // register for changes on the given instruction
                // Whenever it changes replace it in the DynamicFragment 
                // and adapt the statistics accordingly.
                let old = ref i'
                let registration =
                    i |> Mod.registerCallback (fun v ->
                        p.Update id [v]
                        InstructionStatistics.replace s.statistics !old v
                        old := v
                    )

                // return a new CompilerState including the
                // registration (created above) and the dependencies
                { s with 
                      registrations = registration::s.registrations; 
                      dependencies = List.append deps s.dependencies 
                }
            }

        /// <summary>
        /// Appends the given changeable instructions to the DynamicFragment
        /// and annotates the CompilerState with the given list of dependencies
        /// </summary>
        member x.YieldFrom(instructionsAndDeps : IMod<list<Instruction>> * list<IMod>) =
            { compile = fun s p ->
                let i, deps = instructionsAndDeps

                // evaluate the initial instruction-value
                let i' = Mod.force i

                // initially append the instruction to the DynamicFragment
                let id = p.Append i'

                // add the current instruction to the statistics
                InstructionStatistics.addList s.statistics i'

                // register for changes on the given instruction
                // Whenever it changes replace it in the DynamicFragment 
                // and adapt the statistics accordingly.
                let old = ref i'
                let registration =
                    i |> Mod.registerCallback (fun v ->
                        p.Update id v
                        InstructionStatistics.replaceList s.statistics !old v
                        old := v
                    )

                // return a new CompilerState including the
                // registration (created above) and the dependencies
                { s with 
                      registrations = registration::s.registrations; 
                      dependencies = List.append deps s.dependencies 
                }
            }


        member x.YieldFrom(instructionsAndDeps : IEvent<Instruction> * list<IMod>) =
            { compile = fun s p ->
                let i, deps = instructionsAndDeps

                // evaluate the initial instruction-value
                let i' = i.Latest

                // initially append the instruction to the DynamicFragment
                let id = p.Append [i']

                // add the current instruction to the statistics
                InstructionStatistics.add s.statistics i'

                // register for changes on the given instruction
                // Whenever it changes replace it in the DynamicFragment 
                // and adapt the statistics accordingly.
                let old = ref i'
                let registration =
                    i.Values.Subscribe(fun v ->
                        p.Update id [v]
                        InstructionStatistics.replace s.statistics !old v
                        old := v
                    )

                // return a new CompilerState including the
                // registration (created above) and the dependencies
                { s with 
                      registrations = registration::s.registrations; 
                      dependencies = List.append deps s.dependencies 
                }
            }

        member x.YieldFrom(instructionsAndDeps : IEvent<list<Instruction>> * list<IMod>) =
            { compile = fun s p ->
                let i, deps = instructionsAndDeps

                // evaluate the initial instruction-value
                let i' = i.Latest

                // initially append the instruction to the DynamicFragment
                let id = p.Append i'

                // add the current instruction to the statistics
                InstructionStatistics.addList s.statistics i'

                // register for changes on the given instruction
                // Whenever it changes replace it in the DynamicFragment 
                // and adapt the statistics accordingly.
                let old = ref i'
                let registration =
                    i.Values.Subscribe(fun v ->
                        p.Update id v
                        InstructionStatistics.replaceList s.statistics !old v
                        old := v
                    )

                // return a new CompilerState including the
                // registration (created above) and the dependencies
                { s with 
                      registrations = registration::s.registrations; 
                      dependencies = List.append deps s.dependencies 
                }
            }

        member x.YieldFrom(instructionsAndDeps : (unit -> Instruction) * list<IMod>) =
            { compile = fun s p ->
                let i, deps = instructionsAndDeps

                let old : ref<Option<Instruction>> = ref None
                let stats = s.statistics
                    
                let id = p.Append []

                let writer() =
                    let i' = i()
                    p.Update id [i']

                    match !old with
                        | Some o -> InstructionStatistics.remove stats o
                        | None -> ()

                    InstructionStatistics.add stats i'
                    old := Some i'

                // return a new CompilerState including the
                // registration (created above) and the dependencies
                { s with 
                      percontext = writer::s.percontext; 
                      dependencies = List.append deps s.dependencies 
                }
            }

        member x.YieldFrom(instructionsAndDeps : (unit -> list<Instruction>) * list<IMod>) =
            { compile = fun s p ->
                let i, deps = instructionsAndDeps

                let old : ref<Option<list<Instruction>>> = ref None
                let stats = s.statistics
                    
                let id = p.Append []

                let writer() =
                    let i' = i()
                    p.Update id i'

                    match !old with
                        | Some o -> InstructionStatistics.removeList stats o
                        | None -> ()

                    InstructionStatistics.addList stats i'
                    old := Some i'

                // return a new CompilerState including the
                // registration (created above) and the dependencies
                { s with 
                      percontext = writer::s.percontext; 
                      dependencies = List.append deps s.dependencies 
                }
            }

        member x.YieldFrom(instructions : list<Instruction>) =
            { compile = fun s p ->

                let old : ref<Option<list<Instruction>>> = ref None
                let stats = s.statistics
                    
                p.Append instructions |> ignore
                s
            }

        member x.YieldFrom(instruction : IMod<Instruction>) =
            x.YieldFrom((instruction, []))

        member x.YieldFrom(instruction : IEvent<Instruction>) =
            x.YieldFrom((instruction, []))

        member x.YieldFrom(instruction : unit -> Instruction) =
            x.YieldFrom((instruction, []))

        member x.YieldFrom(instruction : IMod<list<Instruction>>) =
            x.YieldFrom((instruction, []))

        member x.YieldFrom(instruction : IEvent<list<Instruction>>) =
            x.YieldFrom((instruction, []))

        member x.YieldFrom(instruction : unit -> list<Instruction>) =
            x.YieldFrom((instruction, []))



        // Standard State-Monad implementations needed by F# 
        // to support loops, empty else-branches, etc.
        member x.Delay(f : unit -> Compile) =
            { compile = fun s p ->
                (f ()).compile s p
            }

        member x.For(seq : seq<'a>, f : 'a -> Compile) =
            { compile = fun s p ->
                let mutable current = s
                for e in seq do
                    let v = f e
                    current <- v.compile current p
                current
            }

        member x.While(guard : unit -> bool, f : unit -> Compile) =
            { compile = fun s p ->
                let mutable current = s
                while guard() do
                    current <- (f ()).compile current p
                current
            }

        member x.Combine(l : Compile, r : Compile) =
            { compile = fun s p ->
                let s' = l.compile s p
                r.compile s' p
            }

        member x.Zero() =
            { compile = fun s _ -> s }

    /// <summary>
    /// <see cref="Aardvark.Rendering.GL.InstructionCompiler.CompileBuilder" />
    /// </summary>
    let compile = CompileBuilder()

    /// <summary>
    /// Manager provides stateful wrappers for resource-creations
    /// allowing them to be used inside the compile-monad
    /// </summary>
    module Manager =

        let getContext =
            { run = fun s ->
                s, s.resourceManager.Context
            }

        let private createAndAddResource (f : ResourceManager -> ChangeableResource<'a>) =
            { run = fun s ->
                let r = f s.resourceManager
                {s with resources = (r:> IChangeableResource)::s.resources}, r
            }

        let createProgram surface =
            createAndAddResource (fun m -> m.CreateSurface(surface))

        let createUniformBuffer block surface provider =
            { run = fun s ->
                let mutable values = []
                let r = s.resourceManager.CreateUniformBuffer(block, surface, provider, &values)
                {s with resources = (r:> IChangeableResource)::s.resources}, (r,values)
            }

        let createUniformLocation u provider =
            { run = fun s ->
                let mutable values = []
                let r = s.resourceManager.CreateUniformLocation(provider, u)
                {s with resources = (r:> IChangeableResource)::s.resources}, (r,values)
            }

        let createTexture texture =
            createAndAddResource (fun m -> m.CreateTexture texture)

        let createSampler sampler =
            createAndAddResource (fun m -> m.CreateSampler sampler)

        let viewCache = System.Collections.Concurrent.ConcurrentDictionary<BufferView * ChangeableResource<Aardvark.Rendering.GL.Buffer>, IMod<AttributeDescription>>()

        let private createView (frequency : AttributeFrequency) (m : BufferView) (b : ChangeableResource<Aardvark.Rendering.GL.Buffer>) =
            viewCache.GetOrAdd(((m,b)), (fun (m,b) ->
                b.Resource |> Mod.map (fun b ->
                    { Type = m.ElementType; Frequency = frequency; Normalized = false; Stride = m.Stride; Offset = m.Offset; Buffer = b }
                )
            ))

        let createVertexArrayObject (program : Program) (rj : RenderJob) =
            { run = fun s -> 
                let manager = s.resourceManager

                let buffers = System.Collections.Generic.List<IChangeableResource>()
                let bindings = System.Collections.Generic.Dictionary()
                for v in program.Inputs do
                    match rj.VertexAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                        | (true,value) ->
                            let dep = manager.CreateBuffer(value.Buffer)
                            buffers.Add dep
                            bindings.[v.attributeIndex] <- createView AttributeFrequency.PerVertex value dep
                        | _  -> 
                            match rj.InstanceAttributes with
                                | null -> failwithf "could not get attribute %A (not found in vertex attributes, and instance attributes is null) for rj: %A" v.semantic rj
                                | _ -> 
                                    printfn "looking up %s in instance attribs" v.semantic
                                    match rj.InstanceAttributes.TryGetAttribute (v.semantic |> Symbol.Create) with
                                        | (true,value) ->
                                            let dep = manager.CreateBuffer(value.Buffer)
                                            buffers.Add dep
                                            bindings.[v.attributeIndex] <- createView (AttributeFrequency.PerInstances 1) value dep
                                        | _ -> failwithf "could not get attribute %A" v.semantic

                //create the index-buffer if desired
                let index = ref None
                if rj.Indices <> null then
                    let dep = manager.CreateBuffer(rj.Indices)
                    buffers.Add dep
                    index := Some dep


                let bindings = bindings |> Seq.map (fun (KeyValue(k,v)) -> k,v) |> Seq.toList
                //create a vertex-array-object
                let vao = manager.CreateVertexArrayObject(bindings, !index)

                let newResources = (vao :> IChangeableResource)::(buffers |> Seq.toList)

                { s with resources = List.append newResources s.resources }, vao
            }


    /// <summary>
    /// Instructions provides high-level abstractions for creating
    /// instructions.
    /// </summary>
    [<AutoOpen>]
    module private Instructions = 
        open Aardvark.Rendering.GL.OpenGl
        

        module Translations =

            type private ABlendFactor = Aardvark.Base.BlendFactor
            type private GLBlendFactor = Aardvark.Rendering.GL.OpenGl.Enums.BlendFactor
            type private ABlendOperation = Aardvark.Base.BlendOperation
            type private GLBlendOperation = Aardvark.Rendering.GL.OpenGl.Enums.BlendOperation

            let toGLMode (m : IndexedGeometryMode) =
                match m with 
                    | IndexedGeometryMode.TriangleList -> DrawMode.Triangles |> int
                    | IndexedGeometryMode.TriangleStrip -> DrawMode.TriangleStrip |> int
                    | IndexedGeometryMode.LineList -> DrawMode.Lines |> int
                    | IndexedGeometryMode.LineStrip -> DrawMode.LineStrip |> int
                    | IndexedGeometryMode.PointList -> DrawMode.Points |> int
                    | _ -> failwith "not handled IndexedGeometryMode"

            let toPatchCount (m : IndexedGeometryMode) =
                    match m with 
                    | IndexedGeometryMode.TriangleList -> 3
                    | IndexedGeometryMode.TriangleStrip -> 3 
                    | IndexedGeometryMode.LineList -> 2
                    | IndexedGeometryMode.LineStrip -> 2
                    | IndexedGeometryMode.PointList -> 1
                    | _ -> failwith "not handled IndexedGeometryMode"

            let toGLFactor (f : ABlendFactor) =
                match f with
                    | ABlendFactor.Zero -> GLBlendFactor.Zero |> int
                    | ABlendFactor.One -> GLBlendFactor.One |> int
                    | ABlendFactor.DestinationAlpha -> GLBlendFactor.DstAlpha |> int
                    | ABlendFactor.DestinationColor -> GLBlendFactor.DstColor |> int
                    | ABlendFactor.SourceAlpha -> GLBlendFactor.SrcAlpha |> int
                    | ABlendFactor.SourceColor -> GLBlendFactor.SrcColor |> int
                    | ABlendFactor.InvDestinationAlpha -> GLBlendFactor.InvDstAlpha |> int
                    | ABlendFactor.InvDestinationColor -> GLBlendFactor.InvDstColor |> int
                    | ABlendFactor.InvSourceAlpha -> GLBlendFactor.InvSrcAlpha |> int
                    | ABlendFactor.InvSourceColor -> GLBlendFactor.InvSrcColor |> int
                    | _ -> failwithf "unknown blend factor: %A" f

            let toGLOperation (f : ABlendOperation) =
                match f with
                    | ABlendOperation.Add -> GLBlendOperation.Add |> int
                    | ABlendOperation.Subtract -> GLBlendOperation.Subtract |> int
                    | ABlendOperation.ReverseSubtract -> GLBlendOperation.ReverseSubtract |> int
                    | _ -> failwithf "unknown blend operation %A" f

            let toGLComparison (f : DepthTestMode) =
                match f with
                    | DepthTestMode.Greater -> CompareFunction.Greater |> int
                    | DepthTestMode.GreaterOrEqual -> CompareFunction.GreaterEqual |> int
                    | DepthTestMode.Less -> CompareFunction.Less |> int
                    | DepthTestMode.LessOrEqual -> CompareFunction.LessEqual |> int
                    | _ -> failwithf "unknown comparison %A" f

            let toGLFace (f : CullMode) =
                match f with
                    | CullMode.Clockwise -> Face.Back |> int
                    | CullMode.CounterClockwise -> Face.Front |> int
                    | _ -> failwithf "unknown comparison %A" f

            let toGLFunction (f : StencilCompareFunction) =
                match f with
                    | StencilCompareFunction.Always -> CompareFunction.Always |> int
                    | StencilCompareFunction.Equal -> CompareFunction.Equal |> int
                    | StencilCompareFunction.Greater -> CompareFunction.Greater |> int
                    | StencilCompareFunction.GreaterOrEqual -> CompareFunction.GreaterEqual |> int
                    | StencilCompareFunction.Less -> CompareFunction.Less |> int
                    | StencilCompareFunction.LessOrEqual -> CompareFunction.LessEqual |> int
                    | StencilCompareFunction.Never -> CompareFunction.Never |> int
                    | StencilCompareFunction.NotEqual -> CompareFunction.NotEqual |> int
                    | _ -> failwithf "unknown comparison %A" f

            let toGLStencilOperation (o : StencilOperationFunction) =
                match o with
                    | StencilOperationFunction.Decrement -> StencilOperation.Decrement |> int
                    | StencilOperationFunction.DecrementWrap -> StencilOperation.DecrementWrap  |> int
                    | StencilOperationFunction.Increment -> StencilOperation.Increment  |> int
                    | StencilOperationFunction.IncrementWrap -> StencilOperation.IncrementWrap  |> int
                    | StencilOperationFunction.Invert -> StencilOperation.Invert  |> int
                    | StencilOperationFunction.Keep -> StencilOperation.Keep  |> int
                    | StencilOperationFunction.Replace -> StencilOperation.Replace  |> int
                    | StencilOperationFunction.Zero -> StencilOperation.Zero  |> int
                    | _ -> failwith "unknown stencil operation %A" o

            let toGLPolygonMode (f : FillMode) =
                match f with
                    | FillMode.Fill -> Enums.PolygonMode.Fill |> int
                    | FillMode.Line -> Enums.PolygonMode.Line |> int
                    | FillMode.Point -> Enums.PolygonMode.Point |> int
                    | _ -> failwithf "unknown FillMode: %A" f

            let toGLTarget (d : TextureDimension) (isArray : bool) (samples : int) =
                match d with
                    | TextureDimension.Texture1D -> int TextureTarget.Texture1D
                    | TextureDimension.Texture2D -> int TextureTarget.Texture2D
                    | TextureDimension.Texture3D -> int TextureTarget.Texture3D
                    | TextureDimension.TextureCube -> int TextureTarget.TextureCubeMap
                    | _ -> failwithf "unknown TextureDimension: %A" d

        let private wrapWithDeps (l : list<IMod>) (i : IMod<'a>) =
            (i, l)

        let setDepthTest (m : IMod<DepthTestMode>) =
            m |> Mod.map (fun dt ->
                if dt <> DepthTestMode.None then
                    [ Instruction.Enable(int OpenGl.Enums.State.DepthTest)
                      Instruction.DepthFunc(Translations.toGLComparison dt) ]
                else
                    [Instruction.Disable(int OpenGl.Enums.State.DepthTest)])
                |> wrapWithDeps [m]

        let setFillMode (m : IMod<FillMode>) =
            m |> Mod.map (fun fm -> Instruction.PolygonMode (int OpenGl.Enums.Face.FrontAndBack) (Translations.toGLPolygonMode fm))
                |> wrapWithDeps [m]

        let setCullMode (m : IMod<CullMode>) =
            m |> Mod.map (fun cm -> 
                    if cm <> CullMode.None then
                        [ Instruction.Enable(int OpenGl.Enums.State.CullFace)
                          Instruction.CullFace(Translations.toGLFace cm) ]
                    else
                        [ Instruction.Disable(int OpenGl.Enums.State.CullFace) ]
                    )
                |> wrapWithDeps [m]

        let setBlendMode (m : IMod<BlendMode>) =
            m |> Mod.map (fun bm -> 
                    //TODO: actually depending on the Framebuffer (premultiplied alpha)
                    if bm.Enabled then
                        let src = Translations.toGLFactor bm.SourceFactor
                        let dst = Translations.toGLFactor bm.DestinationFactor
                        let op = Translations.toGLOperation bm.Operation

                        let srcA = Translations.toGLFactor bm.SourceAlphaFactor
                        let dstA = Translations.toGLFactor bm.DestinationAlphaFactor
                        let opA = Translations.toGLOperation bm.AlphaOperation

                        [ Instruction.Enable(int OpenGl.Enums.State.Blend)
                          Instruction.BlendFuncSeparate src dst srcA dstA
                          Instruction.BlendEquationSeparate op opA
                        ]
                    else
                        [ Instruction.Disable(int OpenGl.Enums.State.Blend) ]
                    )
                |> wrapWithDeps [m]

        let setStencilMode (m : IMod<StencilMode>) =
            m |> Mod.map (fun sm -> 
                    //TODO: actually depending on the Framebuffer (premultiplied alpha)
                    if sm.IsEnabled then
                        let cmpFront = Translations.toGLFunction sm.CompareFront.Function
                        let cmpBack= Translations.toGLFunction sm.CompareBack.Function
                        let opFrontSF = Translations.toGLStencilOperation sm.OperationFront.StencilFail
                        let opBackSF = Translations.toGLStencilOperation sm.OperationBack.StencilFail
                        let opFrontDF = Translations.toGLStencilOperation sm.OperationFront.DepthFail
                        let opBackDF = Translations.toGLStencilOperation sm.OperationBack.DepthFail
                        let opFrontP = Translations.toGLStencilOperation sm.OperationFront.DepthPass
                        let opBackP = Translations.toGLStencilOperation sm.OperationBack.DepthPass

                        [ Instruction.Enable(int OpenGl.Enums.State.StencilTest) 
                          Instruction.StencilFuncSeparate(int OpenGl.Enums.Face.Front) cmpFront sm.CompareFront.Reference (int sm.CompareFront.Mask)
                          Instruction.StencilFuncSeparate(int OpenGl.Enums.Face.Back) cmpBack sm.CompareBack.Reference (int sm.CompareBack.Mask)
                          Instruction.StencilOpSeparate(int OpenGl.Enums.Face.Front) opFrontSF opFrontDF opFrontP
                          Instruction.StencilOpSeparate(int OpenGl.Enums.Face.Back) opBackSF opBackDF opBackP
                        ]
                    else
                        [ Instruction.Disable(int OpenGl.Enums.State.StencilTest) ]
                    )
                |> wrapWithDeps [m]

        let bindProgram (p : ChangeableResource<Program>) =
            p.Resource |> Mod.map (fun r -> Instruction.BindProgram(r.Handle))

        let bindUniformBuffer (index : int) (u : ChangeableResource<UniformBuffer>) =
            
            u.Resource |> Mod.map (fun r -> 
                //ExecutionContext.bindUniformBuffer index r
                Instruction.BindBufferRange (int BufferTarget.UniformBuffer) index r.Handle 0n (nativeint r.Size)
            )

        let setActiveTexture (index : int) =
            Instruction.ActiveTexture (((int OpenGl.Enums.TextureUnit.Texture0) + index) |> unbox)

        let bindSampler (index : int) (sampler : ChangeableResource<Sampler>) =
            sampler.Resource |> Mod.map (fun r -> Instruction.BindSampler index r.Handle)

        let bindTexture (tex : ChangeableResource<Texture>) =
            tex.Resource |> Mod.map(fun r -> 
                let target = Translations.toGLTarget r.Dimension r.IsArray r.Multisamples
                Instruction.BindTexture target r.Handle
            )

        let bindVertexArray (vao : ChangeableResource<VertexArrayObject>) =
            fun () -> Instruction.BindVertexArray(vao.Resource.GetValue().Handle)
                
        let draw (indexArray : IMod<System.Array>) (hasTess : bool) (call : IMod<DrawCallInfo>) (isActive : IMod<bool>) =
          
            let indexType = 
                if indexArray <> null then
                    indexArray |> Mod.map (fun ia -> (ia <> null, if ia <> null then ia.GetType().GetElementType() else typeof<obj>))
                else
                    Mod.initConstant (false, typeof<obj>)

            let patchSize (mode : IndexedGeometryMode) =
                match mode with
                    | IndexedGeometryMode.LineList -> 2
                    | IndexedGeometryMode.PointList -> 1
                    | IndexedGeometryMode.TriangleList -> 3
                    | m -> failwithf "unsupported patch-mode: %A" m

            let instruction =
                adaptive {
                    let! (indexed, indexType) = indexType
                    let! (call, isActive) = call, isActive

                    let mode =
                        if hasTess then int OpenGl.Enums.DrawMode.Patches
                        else Translations.toGLMode call.Mode

                    if indexed then
                        let offset = nativeint (call.FirstIndex * indexType.GLSize)

                        let indexType =
                            if indexType = typeof<byte> then int OpenGl.Enums.IndexType.UnsignedByte
                            elif indexType = typeof<uint16> then int OpenGl.Enums.IndexType.UnsignedShort
                            elif indexType = typeof<uint32> then int OpenGl.Enums.IndexType.UnsignedInt
                            elif indexType = typeof<sbyte> then int OpenGl.Enums.IndexType.UnsignedByte
                            elif indexType = typeof<int16> then int OpenGl.Enums.IndexType.UnsignedShort
                            elif indexType = typeof<int32> then int OpenGl.Enums.IndexType.UnsignedInt
                            else failwithf "unsupported index type: %A"  indexType

                        match call.InstanceCount with
                            | 1 -> return call.Mode, Instruction.DrawElements mode call.FaceVertexCount indexType offset
                            | n -> return call.Mode, Instruction.DrawElementsInstanced mode call.FaceVertexCount indexType offset n
                    else
                        match call.InstanceCount with
                            | 1 -> return call.Mode, Instruction.DrawArrays mode call.FirstIndex call.FaceVertexCount
                            | n -> return call.Mode, Instruction.DrawArraysInstanced mode call.FirstIndex call.FaceVertexCount n
                }

            let final =
                instruction |> Mod.map (fun (mode,i) ->
                    if hasTess then
                        let size = patchSize mode
                        [ Instruction.PatchParameter (int OpenTK.Graphics.OpenGL4.PatchParameterInt.PatchVertices) size
                          i]
                    else
                        [i]
                )

            final |> wrapWithDeps [call;isActive;indexArray]

        let rec allUniformsEqual (rj : RenderJob) (values : list<string * IMod>) =
            match values with
                | (k,v)::xs -> 
                    match rj.Uniforms.TryGetUniform(Symbol.Create k) with
                        | (true, c) ->
                            if c = v then
                                allUniformsEqual rj xs
                            else
                                false

                        | _ -> false

                | [] -> true

    /// <summary>
    /// compileDeltaInternal compiles all instructions needed to render [next] 
    /// assuming [prev] was rendered immediately before.
    /// This function is the core-ingredient making our rendering-system
    /// fast as hell \o/.
    /// </summary>
    let compileDeltaInternal (prev : RenderJob) (next : RenderJob) =
        compile {
                
            //set all modes if needed
            if prev.DepthTest <> next.DepthTest && next.DepthTest <> null then
                yield! setDepthTest next.DepthTest

            if prev.FillMode <> next.FillMode && next.FillMode <> null then
                yield! setFillMode next.FillMode

            if prev.CullMode <> next.CullMode && next.CullMode <> null then
                yield! setCullMode next.CullMode

            if prev.BlendMode <> next.BlendMode && next.BlendMode <> null then
                yield! setBlendMode next.BlendMode

            if prev.StencilMode <> next.StencilMode && next.StencilMode <> null then
                yield! setStencilMode next.StencilMode

            let! ctx = Manager.getContext

            //bind the program
            let! program = Manager.createProgram next.Surface
            let programEqual = prev.Surface = next.Surface

            if prev.Surface <> next.Surface then
                yield! bindProgram program

                

            //create and bind all needed uniform-buffers
            let program = program.Resource.GetValue()

            for b in program.UniformBlocks do
                let! (uniformBuffer, values) = Manager.createUniformBuffer b next.Surface next.Uniforms

                //ISSUE: what if the programs are not equal but use the same uniform buffers?
                if not programEqual || not (allUniformsEqual prev values) then
                    yield! bindUniformBuffer b.index uniformBuffer

            //create and bind all textures/samplers
            for uniform in program.Uniforms do
                let nextTexture = next.Uniforms.TryGetUniform(uniform.semantic |> Symbol.Create)
                let prevTexture = prev.Uniforms.TryGetUniform(uniform.semantic |> Symbol.Create)


                match uniform.uniformType with
                    | SamplerType ->
                        let sampler =
                            match program.SamplerStates.TryGetValue (Symbol.Create uniform.semantic) with
                                | (true, sampler) -> sampler
                                | _ -> 
                                    match uniform.samplerState with
                                        | Some sam ->
                                            match program.SamplerStates.TryGetValue (Symbol.Create sam) with
                                                | (true, sampler) -> sampler
                                                | _ -> SamplerStateDescription()
                                        | None ->
                                            SamplerStateDescription()

                        match nextTexture with
                            | (true, value) ->
                                match value with
                                    | :? IMod<ITexture> as value ->
                                        let! texture = Manager.createTexture value
                                        let! sampler = Manager.createSampler (Mod.initConstant sampler)
                              
                                        //ISSUE:      
                                        //there is a special case when the prev renderjob has the same texture but binds it to
                                        //a different slot!!!
                                        match prevTexture with
                                            | (false,old) ->
                                                yield setActiveTexture uniform.index
                                                yield! bindSampler uniform.index sampler
                                                yield! bindTexture texture

                                            | (true,old) when old <> (value :> IMod) ->
                                                yield setActiveTexture uniform.index
                                                yield! bindSampler uniform.index sampler
                                                yield! bindTexture texture

                                            | _ -> ()
                                    | _ ->
                                        Log.warn "Urdar: using default texture since none was found"
                            | _ ->
                                Log.warn "using default texture since none was found"
                                yield setActiveTexture uniform.index 
                                //let tex = Texture(program.Context, 0, TextureDimension.Texture2D, 1, V3i(1,1,0), 1, ChannelType.RGBA8)
                                yield Instruction.BindTexture 0x0DE1 0 //bindTexture tex

                    | _ ->
                        match prevTexture, nextTexture with
                            | (true, p), (true, n) when p = n -> ()
                            | _ ->
                                let! (loc,_) = Manager.createUniformLocation uniform next.Uniforms
                                let l = ExecutionContext.bindUniformLocation uniform.location (loc.Resource.GetValue())
                                yield! l

                    | _ ->
                        Log.warn "trying to set unknown top-level uniform: %A" uniform


            let! vao = Manager.createVertexArrayObject program next
            yield! bindVertexArray vao
            let hasTess = program.Shaders |> List.exists (fun s -> s.Stage = ShaderStage.TessControl)

            yield! draw next.Indices hasTess next.DrawCallInfo next.IsActive 
        }

    /// <summary>
    /// compileDelta compiles all instructions needed to render [next] 
    /// assuming [prev] was rendered immediately before.
    /// This function is the core-ingredient making our rendering-system
    /// fast as hell \o/.
    /// </summary>
    let compileDelta (manager : ResourceManager) (fragment : IDynamicFragment) (prev : RenderJob) (next : RenderJob) =

        let c = compileDeltaInternal prev next
        let statistics = EventSource(FrameStatistics.Zero)
        let state = { resourceManager = manager
                      resources = []
                      registrations = []
                      percontext = []
                      statistics = statistics
                      dependencies = [] }

        c.compile state fragment

