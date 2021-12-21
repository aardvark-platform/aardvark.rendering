namespace Aardvark.SceneGraph


open System
open Aardvark.Base

open Aardvark.Base.Ag
open Aardvark.Rendering
open TrafoOperators
open FSharp.Data.Adaptive

type AirState =
    {
        isActive            : aval<bool>
        drawCalls           : DrawCalls
        mode                : IndexedGeometryMode
        surface             : Surface

        blendState          : BlendState
        depthState          : DepthState
        stencilState        : StencilState
        rasterizerState     : RasterizerState

        indices             : Option<BufferView>
        instanceAttributes  : Map<Symbol, BufferView>
        vertexAttributes    : Map<Symbol, BufferView>

        uniforms            : Map<Symbol, IAdaptiveValue>
        trafos              : list<aval<Trafo3d>>

        scope               : RenderObject
        final               : list<IRenderObject>
    }

[<AbstractClass>]
type Air<'a>() =
    abstract member RunUnit : byref<AirState> -> unit
    abstract member Run : byref<AirState> -> 'a

    default x.Run(s) = x.RunUnit(&s); Unchecked.defaultof<_>
    default x.RunUnit(s) = x.Run(&s) |> ignore

[<AutoOpen>]
module ``Air Builder`` =
    type AirBuilder() =
        member x.Bind(m : Air<'a>, f : 'a -> Air<'b>) =
            { new Air<'b>() with
                member x.Run(state) =
                    let v = m.Run(&state)
                    (f v).Run(&state)
            }

        member x.Return(v : 'a) : Air<'a> =
            { new Air<'a>() with
                member x.Run(state) =
                    v
            }

        member x.Zero() : Air<unit> =
            { new Air<unit>() with
                member x.RunUnit(state) = ()
            }

        member x.Delay(f : unit -> Air<'a>) =
            { new Air<'a>() with
                member x.Run(s) = f().Run(&s)
            }

        member x.Combine(l : Air<unit>, r : Air<'a>) =
            { new Air<'a>() with
                member x.Run(s) =
                    l.Run(&s)
                    r.Run(&s)
            }

        member x.For(seq : seq<'a>, f : 'a -> Air<unit>) =
            { new Air<unit>() with
                member x.RunUnit(s) =
                    for e in seq do
                        (f e).Run(&s)
            }

        member x.While(guard : unit -> bool, body : Air<unit>) =
            { new Air<unit>() with
                member x.RunUnit(s) =
                    while guard() do body.Run(&s)
            }

    let air = AirBuilder()

    type AirShaderBuilder() =
        inherit EffectBuilder()

        member x.Run(f : unit -> list<FShadeEffect>) =
            let surface = f() |> FShade.Effect.compose |> Surface.FShadeSimple

            { new Air<unit>() with
                member x.RunUnit(state) =
                    state <- { state with surface = surface }
            }

    let internal airShader = AirShaderBuilder()


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AirState =
    open Aardvark.SceneGraph.Semantics
    let private reader (f : AirState -> 'a) =
        { new Air<'a>() with
            member x.Run(state) = f state
        }

    let ofScope (scope : Ag.Scope) =
        let ro = RenderObject.ofScope scope


        {
            isActive            = ro.IsActive
            drawCalls           = ro.DrawCalls
            mode                = ro.Mode
            surface             = ro.Surface

            blendState          = ro.BlendState
            depthState          = ro.DepthState
            stencilState        = ro.StencilState
            rasterizerState     = ro.RasterizerState

            indices             = ro.Indices
            instanceAttributes  = Map.empty
            vertexAttributes    = Map.empty

            uniforms            = Map.empty
            trafos              = scope?ModelTrafoStack

            scope               = ro
            final               = []
        }

    let isActive            = reader (fun s -> s.isActive)
    let mode                = reader (fun s -> s.mode)
    let surface             = reader (fun s -> s.surface)
    let blendState          = reader (fun s -> s.blendState)
    let depthState          = reader (fun s -> s.depthState)
    let stencilState        = reader (fun s -> s.stencilState)
    let rasterizerState     = reader (fun s -> s.rasterizerState)
    let indices             = reader (fun s -> s.indices)
    let instanceAttributes  = reader (fun s -> s.instanceAttributes)
    let vertexAttributes    = reader (fun s -> s.vertexAttributes)
    let uniforms            = reader (fun s -> s.uniforms)
    let trafos              = reader (fun s -> s.trafos)
    let scope               = reader (fun s -> s.scope)


type private AirAttributeProvider(local : Map<Symbol, BufferView>, inh : IAttributeProvider) =
    interface IAttributeProvider with
        member x.TryGetAttribute(sem) =
            match Map.tryFind sem local with
                | Some v -> Some v
                | None -> inh.TryGetAttribute sem

        member x.All = Seq.empty
        member x.Dispose() = ()

type private AirUniformProvider(local : Map<Symbol, IAdaptiveValue>, trafos : list<aval<Trafo3d>>, inh : IUniformProvider) =
    static let mt = Symbol.Create "ModelTrafo"
    let model = lazy (Aardvark.SceneGraph.Semantics.TrafoSemantics.flattenStack trafos)



    interface IUniformProvider with
        member x.TryGetUniform(scope, sem) =
            if sem = mt then
                model.Value :> IAdaptiveValue |> Some
            else
                match Map.tryFind sem local with
                    | Some u -> Some u
                    | None -> inh.TryGetUniform(scope, sem)

        member x.Dispose() = ()

type Air private() =

    static let modify (f : AirState -> AirState) =
        { new Air<unit>() with
            member x.RunUnit(state) =
                state <- f state
        }

    static let emit =
        { new Air<unit>() with
            member x.RunUnit(state : byref<AirState>) =
                let ro = RenderObject.Clone state.scope

                ro.IsActive <- state.isActive
                ro.DrawCalls <- state.drawCalls
                ro.Mode <- state.mode
                ro.Surface <- state.surface
                ro.BlendState <- state.blendState
                ro.DepthState <- state.depthState
                ro.StencilState <- state.stencilState
                ro.RasterizerState <- state.rasterizerState
                ro.Indices <- state.indices

                ro.VertexAttributes <- new AirAttributeProvider(state.vertexAttributes, state.scope.VertexAttributes)
                ro.InstanceAttributes <- new AirAttributeProvider(state.instanceAttributes, state.scope.InstanceAttributes)
                ro.Uniforms <- new AirUniformProvider(state.uniforms, state.trafos, state.scope.Uniforms)

                state <- { state with final = state.final @ [ro] }
        }


    // ================================================================================================================
    // Vertex Buffers
    // ================================================================================================================
    static member BindVertexBuffer(slot : Symbol, view : BufferView) =
        modify (fun s -> { s with vertexAttributes = Map.add slot view s.vertexAttributes })

    static member BindVertexBuffer(slot : Symbol, buffer : aval<IBuffer>, elementType : Type, offset : int, stride : int) =
        let view = BufferView(buffer, elementType, offset, stride)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : aval<IBuffer>, elementType : Type, offset : int) =
        let view = BufferView(buffer, elementType, offset)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : aval<IBuffer>, elementType : Type) =
        let view = BufferView(buffer, elementType)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : aval<Array>, elementType : Type) =
        let view = BufferView(buffer |> AVal.map (fun d -> d |> ArrayBuffer :> IBuffer), elementType)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : aval<'a[]>) =
        let view = BufferView(buffer |> AVal.map (fun d -> d |> ArrayBuffer :> IBuffer), typeof<'a>)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int, stride : int) =
        let view = BufferView(AVal.constant buffer, elementType, offset, stride)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int) =
        let view = BufferView(AVal.constant buffer, elementType, offset)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IBuffer, elementType : Type) =
        let view = BufferView(AVal.constant buffer, elementType)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : Array) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> AVal.constant, buffer.GetType().GetElementType())
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : 'a[]) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> AVal.constant, typeof<'a>)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffers (buffers : list<Symbol * BufferView>) =
        modify (fun s ->
            let mutable b = s.vertexAttributes
            for (sem, v) in buffers do
                b <- Map.add sem v b

            { s with vertexAttributes = b }
        )

    static member BindVertexBuffers (buffers : list<Symbol * Array>) =
        modify (fun s ->
            let mutable b = s.vertexAttributes
            for (sem, v) in buffers do
                let view = BufferView(v |> ArrayBuffer :> IBuffer |> AVal.constant, v.GetType().GetElementType())
                b <- Map.add sem view b

            { s with vertexAttributes = b }
        )





    // ================================================================================================================
    // Instance Buffers
    // ================================================================================================================
    static member BindInstanceBuffer(slot : Symbol, view : BufferView) =
        modify (fun s -> { s with instanceAttributes = Map.add slot view s.instanceAttributes })

    static member BindInstanceBuffer(slot : Symbol, buffer : aval<IBuffer>, elementType : Type, offset : int, stride : int) =
        let view = BufferView(buffer, elementType, offset, stride)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : aval<IBuffer>, elementType : Type, offset : int) =
        let view = BufferView(buffer, elementType, offset)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : aval<IBuffer>, elementType : Type) =
        let view = BufferView(buffer, elementType)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : aval<Array>, elementType : Type) =
        let view = BufferView(buffer |> AVal.map (fun d -> d |> ArrayBuffer :> IBuffer), elementType)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : aval<'a[]>) =
        let view = BufferView(buffer |> AVal.map (fun d -> d |> ArrayBuffer :> IBuffer), typeof<'a>)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int, stride : int) =
        let view = BufferView(AVal.constant buffer, elementType, offset, stride)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int) =
        let view = BufferView(AVal.constant buffer, elementType, offset)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IBuffer, elementType : Type) =
        let view = BufferView(AVal.constant buffer, elementType)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : Array) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> AVal.constant, buffer.GetType().GetElementType())
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : 'a[]) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> AVal.constant, typeof<'a>)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffers (buffers : list<Symbol * BufferView>) =
        modify (fun s ->
            let mutable b = s.instanceAttributes
            for (sem, v) in buffers do
                b <- Map.add sem v b

            { s with instanceAttributes = b }
        )

    static member BindInstanceBuffers (buffers : list<Symbol * Array>) =
        modify (fun s ->
            let mutable b = s.instanceAttributes
            for (sem, v) in buffers do
                let view = BufferView(v |> ArrayBuffer :> IBuffer |> AVal.constant, v.GetType().GetElementType())
                b <- Map.add sem view b

            { s with instanceAttributes = b }
        )



    // ================================================================================================================
    // Index Buffer
    // ================================================================================================================
    static member BindIndexBuffer(index : BufferView) =
        modify (fun s -> { s with indices = Some index })

    static member BindIndexBuffer(index : 'a[]) =
        if isNull index then
            modify (fun s -> { s with indices = None })
        else
            modify (fun s -> { s with indices = BufferView(AVal.constant (ArrayBuffer index :> IBuffer), typeof<'a>) |> Some })


    // ================================================================================================================
    // Uniforms
    // ================================================================================================================
    static member BindUniform(sem : string, value : IAdaptiveValue) =
        modify (fun s -> { s with uniforms = Map.add (Symbol.Create sem) value s.uniforms })

    static member BindUniform(sem : string, value : aval<'a>) =
        modify (fun s -> { s with uniforms = Map.add (Symbol.Create sem) (value :> IAdaptiveValue) s.uniforms })

    static member BindUniform(sem : Symbol, value : IAdaptiveValue) =
        modify (fun s -> { s with uniforms = Map.add sem value s.uniforms })

    static member BindUniform(sem : Symbol, value : aval<'a>) =
        modify (fun s -> { s with uniforms = Map.add sem (value :> IAdaptiveValue) s.uniforms })

    static member BindUniform(sem : Symbol, value : 'a) =
        modify (fun s -> { s with uniforms = Map.add sem (AVal.constant value :> IAdaptiveValue) s.uniforms })

    static member BindUniforms(values : list<Symbol * IAdaptiveValue>) =
        modify (fun s ->
            let mutable uniforms = s.uniforms

            for (sem, u) in values do
                uniforms <- Map.add sem u uniforms


            { s with uniforms = uniforms }
        )

    static member BindUniforms(values : list<Symbol * obj>) =
        modify (fun s ->
            let mutable uniforms = s.uniforms

            for (sem, u) in values do
                let t =
                    if isNull u then typeof<int>
                    else u.GetType()

                let constantType = typedefof<ConstantVal<_>>.MakeGenericType [|t|]
                let m = Activator.CreateInstance(constantType, [|u|]) |> unbox<IAdaptiveValue>

                uniforms <- Map.add sem m uniforms


            { s with uniforms = uniforms }
        )

    static member BindUniforms(values : list<string * IAdaptiveValue>) =
        modify (fun s ->
            let mutable uniforms = s.uniforms

            for (sem, u) in values do
                uniforms <- Map.add (Symbol.Create sem) u uniforms


            { s with uniforms = uniforms }
        )

    static member BindUniforms(values : list<string * obj>) =
        modify (fun s ->
            let mutable uniforms = s.uniforms

            for (sem, u) in values do
                let t =
                    if isNull u then typeof<int>
                    else u.GetType()

                let constantType = typedefof<ConstantVal<_>>.MakeGenericType [|t|]
                let m = Activator.CreateInstance(constantType, [|u|]) |> unbox<IAdaptiveValue>

                uniforms <- Map.add (Symbol.Create sem) m uniforms


            { s with uniforms = uniforms }
        )


    static member BindTexture(sem : Symbol, value : ITexture) =
        modify (fun s -> { s with uniforms = Map.add sem (AVal.constant value :> IAdaptiveValue) s.uniforms })

    static member BindTexture(sem : Symbol, value : ITexture[]) =
        modify (fun s -> { s with uniforms = Map.add sem (AVal.constant value :> IAdaptiveValue) s.uniforms })

    static member BindTexture(sem : Symbol, file : string, config : TextureParams) =
        let value = FileTexture(file, config) :> ITexture
        modify (fun s -> { s with uniforms = Map.add sem (AVal.constant value :> IAdaptiveValue) s.uniforms })

    static member BindTexture(sem : Symbol, file : string, wantMipMaps : bool) =
        let value = FileTexture(file, wantMipMaps) :> ITexture
        modify (fun s -> { s with uniforms = Map.add sem (AVal.constant value :> IAdaptiveValue) s.uniforms })

    static member BindTexture(sem : Symbol, file : string) =
        let value = FileTexture(file, true) :> ITexture
        modify (fun s -> { s with uniforms = Map.add sem (AVal.constant value :> IAdaptiveValue) s.uniforms })



    static member PushTrafo (t : aval<Trafo3d>) =
        modify (fun s -> { s with trafos = t::s.trafos })

    static member PushTrafo (t : Trafo3d) =
        modify (fun s -> { s with trafos = (AVal.constant t)::s.trafos })


    static member PushScale (scale : float) =
        modify (fun s -> { s with trafos = (AVal.constant (Trafo3d.Scale scale))::s.trafos })


    static member PopTrafo () =
        modify (fun s -> { s with trafos = List.tail s.trafos })



    // ================================================================================================================
    // Surface
    // ================================================================================================================
    static member BindSurface(surface : ISurface) =
        modify (fun s -> { s with surface = Surface.Backend surface })

    static member BindEffect (l : list<FShadeEffect>) =
        let surf = FShade.Effect.compose l |> Surface.FShadeSimple
        modify (fun s -> { s with surface = surf })

    static member BindShader = airShader

    // ================================================================================================================
    // Blending
    // ================================================================================================================
    static member BlendMode (mode : aval<BlendMode>) =
        modify (fun s -> {s with blendState = { s.blendState with Mode = mode }})

    static member BlendModes (modes : aval<Map<Symbol, BlendMode>>) =
        modify (fun s -> {s with blendState = { s.blendState with AttachmentMode = modes }})

    static member BlendConstant (color : aval<C4f>) =
        modify (fun s -> {s with blendState = { s.blendState with ConstantColor = color }})

    static member ColorMask (mask : aval<ColorMask>) =
        modify (fun s -> {s with blendState = { s.blendState with ColorWriteMask = mask }})

    static member ColorMasks (masks : aval<Map<Symbol, ColorMask>>) =
        modify (fun s -> {s with blendState = { s.blendState with AttachmentWriteMask = masks }})

    static member ColorWrite (enabled : aval<bool>) =
        modify (fun s -> {s with blendState = { s.blendState with ColorWriteMask = enabled |> AVal.map ColorMask.enable }})

    static member ColorWrites (enabled : aval<Map<Symbol, bool>>) =
        modify (fun s -> {s with blendState = { s.blendState with AttachmentWriteMask = enabled |> AVal.map (Map.map (fun _ x -> x |> ColorMask.enable)) }})

    static member BlendMode mode       = Air.BlendMode(AVal.constant mode)
    static member BlendModes modes     = Air.BlendModes(AVal.constant modes)
    static member BlendConstant color  = Air.BlendConstant(AVal.constant color)
    static member ColorMask mask       = Air.ColorMask(AVal.constant mask)
    static member ColorMasks masks     = Air.ColorMasks(AVal.constant masks)
    static member ColorWrite enabled   = Air.ColorWrite(AVal.constant enabled)
    static member ColorWrites enabled  = Air.ColorWrites(AVal.constant enabled)

    // ================================================================================================================
    // Depth
    // ================================================================================================================
    static member DepthTest(test : aval<DepthTest>) =
        modify (fun s -> { s with depthState = { s.depthState with Test = test }})

    static member DepthBias(mode : aval<DepthBias>) =
        modify (fun s -> { s with depthState = { s.depthState with Bias = mode }})

    static member DepthWrite(mode : aval<bool>) =
        modify (fun s -> { s with depthState = { s.depthState with WriteMask = mode }})

    static member DepthClamp(mode : aval<bool>) =
        modify (fun s -> { s with depthState = { s.depthState with Clamp = mode }})

    static member DepthTest mode = Air.DepthTest(AVal.constant mode)
    static member DepthBias mode = Air.DepthBias(AVal.constant mode)
    static member DepthWrite mode = Air.DepthWrite(AVal.constant mode)
    static member DepthClamp mode = Air.DepthClamp(AVal.constant mode)

    // ================================================================================================================
    // Stencil
    // ================================================================================================================
    static member StencilModeFront (mode : aval<StencilMode>) =
        modify (fun s -> { s with stencilState = { s.stencilState with ModeFront = mode }})

    static member StencilWriteMaskFront (mask : aval<StencilMask>) =
        modify (fun s -> { s with stencilState = { s.stencilState with WriteMaskFront = mask }})

    static member StencilWriteFront (enabled : aval<bool>) =
        modify (fun s -> { s with stencilState = { s.stencilState with WriteMaskFront = enabled |> AVal.map StencilMask }})

    static member StencilModeBack (mode : aval<StencilMode>) =
        modify (fun s -> { s with stencilState = { s.stencilState with ModeBack = mode }})

    static member StencilWriteMaskBack (mask : aval<StencilMask>) =
        modify (fun s -> { s with stencilState = { s.stencilState with WriteMaskBack = mask }})

    static member StencilWriteBack (enabled : aval<bool>) =
        modify (fun s -> { s with stencilState = { s.stencilState with WriteMaskBack = enabled |> AVal.map StencilMask }})

    static member StencilModes (front : aval<StencilMode>, back : aval<StencilMode>) =
        air {
            do! Air.StencilModeFront front
            do! Air.StencilModeBack back
        }

    static member StencilWriteMasks (front : aval<StencilMask>, back : aval<StencilMask>) =
        air {
            do! Air.StencilWriteMaskFront front
            do! Air.StencilWriteMaskBack back
        }

    static member StencilWrites (front : aval<bool>, back : aval<bool>) =
        air {
            do! Air.StencilWriteFront front
            do! Air.StencilWriteBack back
        }

    static member StencilMode (mode : aval<StencilMode>) =
        Air.StencilModes(mode, mode)

    static member StencilWriteMask (mask : aval<StencilMask>) =
        Air.StencilWriteMasks(mask, mask)

    static member StencilWrite (enabled : aval<bool>) =
        Air.StencilWrites(enabled, enabled)

    static member StencilModeFront mode           = Air.StencilModeFront(AVal.constant mode)
    static member StencilWriteMaskFront mask      = Air.StencilWriteMaskFront(AVal.constant mask)
    static member StencilWriteFront enabled       = Air.StencilWriteFront(AVal.constant enabled)
    static member StencilModeBack mode            = Air.StencilModeBack(AVal.constant mode)
    static member StencilWriteMaskBack mask       = Air.StencilWriteMaskBack(AVal.constant mask)
    static member StencilWriteBack enabled        = Air.StencilWriteBack(AVal.constant enabled)
    static member StencilModes(front, back)       = Air.StencilModes(AVal.constant front, AVal.constant back)
    static member StencilWriteMasks (front, back) = Air.StencilWriteMasks(AVal.constant front, AVal.constant back)
    static member StencilWrites (front, back)     = Air.StencilWrites(AVal.constant front, AVal.constant back)
    static member StencilMode mode                = Air.StencilMode(AVal.constant mode)
    static member StencilWriteMask mask           = Air.StencilWriteMask(AVal.constant mask)
    static member StencilWrite enabled            = Air.StencilWrite(AVal.constant enabled)

    // ================================================================================================================
    // Rasterizer
    // ================================================================================================================
    static member FillMode(mode : aval<FillMode>) =
        modify (fun s -> { s with rasterizerState = { s.rasterizerState with FillMode = mode }})

    static member FrontFace(order : aval<WindingOrder>) =
        modify (fun s -> { s with rasterizerState = { s.rasterizerState with FrontFace = order }})

    static member CullMode(mode : aval<CullMode>) =
        modify (fun s -> { s with rasterizerState = { s.rasterizerState with CullMode = mode }})

    static member Multisample(mode : aval<bool>) =
        modify (fun s -> { s with rasterizerState = { s.rasterizerState with Multisample = mode }})

    static member ConservativeRaster(mode : aval<bool>) =
        modify (fun s -> { s with rasterizerState = { s.rasterizerState with ConservativeRaster = mode }})

    static member CullMode mode           = Air.CullMode(AVal.constant mode)
    static member FrontFace mode          = Air.FrontFace(AVal.constant mode)
    static member FillMode mode           = Air.FillMode(AVal.constant mode)
    static member Multisample mode        = Air.Multisample(AVal.constant mode)
    static member ConservativeRaster mode = Air.ConservativeRaster(AVal.constant mode)

    // ================================================================================================================
    // WriteBuffers
    // ================================================================================================================
    static member WriteBuffers(buffers : list<WriteBuffer>) =
        let depthEnable =
            buffers |> List.contains WriteBuffer.Depth

        let stencilEnable =
            buffers |> List.contains WriteBuffer.Stencil

        let colorEnable =
            buffers
            |> List.choose (function
                | WriteBuffer.Color s -> Some (s, true)
                | _ -> None
            )
            |> Map.ofList

        air {
            do! Air.DepthWrite depthEnable
            do! Air.StencilWrite stencilEnable
            do! Air.ColorWrites colorEnable
            do! Air.ColorWrite false
        }

    static member WriteAllBuffers() =
        air {
            do! Air.DepthWrite true
            do! Air.StencilWrite true
            do! Air.ColorWrites Map.empty
            do! Air.ColorWrite true
        }


    // ================================================================================================================
    // Toplogy
    // ================================================================================================================
    static member Toplogy (mode : IndexedGeometryMode) =
        modify (fun s -> { s with mode = mode })



    // ================================================================================================================
    // Draw
    // ================================================================================================================
    static member Draw(infos : aval<list<DrawCallInfo>>) =
        air {
            do! modify (fun s -> { s with drawCalls = Direct infos })
            do! emit
        }

    static member DrawIndirect(b : aval<IndirectBuffer>) =
        air {
            do! modify (fun s -> { s with drawCalls = Indirect b })
            do! emit
        }

    static member Draw(infos : aval<DrawCallInfo>) =
        air {
            do! modify (fun s -> { s with drawCalls = Direct (AVal.map (fun i -> [i]) infos) })
            do! emit
        }

    static member Draw(infos : list<DrawCallInfo>) =
        air {
            do! modify (fun s -> { s with drawCalls = Direct (AVal.constant infos) })
            do! emit
        }

    static member Draw(info : DrawCallInfo) =
        air {
            do! modify (fun s -> { s with drawCalls = Direct (AVal.constant [info]) })
            do! emit
        }


    static member DrawInstanced(instanceCount : int, offset : int, count : int, baseVertex : int) =
        let info =
            DrawCallInfo(
                FirstIndex = offset,
                FaceVertexCount = count,
                FirstInstance = 0,
                InstanceCount = instanceCount,
                BaseVertex = baseVertex
            )

        Air.Draw(info)

    static member DrawInstanced(instanceCount : int, offset : int, count : int) =
        Air.DrawInstanced(instanceCount, offset, count, 0)

    static member DrawInstanced(instanceCount : int, count : int) =
        Air.DrawInstanced(instanceCount, 0, count, 0)


    static member Draw(offset : int, count : int, baseVertex : int) =
        Air.DrawInstanced(1, offset, count, baseVertex)

    static member Draw(offset : int, count : int) =
        Air.Draw(offset, count, 0)

    static member Draw(count : int) =
        Air.Draw(0, count, 0)


    static member RunInScope (scope : Ag.Scope) (a : Air<unit>) =
        let mutable state = AirState.ofScope scope
        a.RunUnit(&state)
        MultiRenderObject(state.final) :> IRenderObject



[<AutoOpen>]
module ``Air Sg Interop`` =

    module Sg =
        type AirNode(content : Air<unit>) =
            interface ISg
            member x.Content = content


[<AutoOpen>]
module ``Air Sg F#`` =
    module ``Air Sg Builder`` =
        type AirSgBuilder() =
            inherit AirBuilder()
            member x.Run(v : Air<unit>) = Sg.AirNode(v) :> ISg

    let inline uniformValue (v : 'a) =
        match v :> obj with
            | :? IAdaptiveValue as m -> m
            | _ -> AVal.constant v :> IAdaptiveValue

    let inline attValue (v : 'a[]) =
        let view = BufferView(v |> ArrayBuffer :> IBuffer |> AVal.constant, typeof<'a>)
        view


    module Sg =
        let air = ``Air Sg Builder``.AirSgBuilder()




namespace Aardvark.SceneGraph.Semantics
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph

[<Rule>]
type AirNodeSem() =
    member x.RenderObjects(n : Sg.AirNode, scope : Ag.Scope) =
        n.Content |> Air.RunInScope scope |> ASet.single



