namespace Aardvark.SceneGraph


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open TrafoOperators
open Aardvark.Base.Incremental

type AirState =
    {
        isActive            : IMod<bool>
        drawCallInfos       : IMod<list<DrawCallInfo>>
        mode                : IMod<IndexedGeometryMode>
        surface             : IMod<ISurface>

        depthTest           : IMod<DepthTestMode>
        cullMode            : IMod<CullMode>
        blendMode           : IMod<BlendMode>
        fillMode            : IMod<FillMode>
        stencilMode         : IMod<StencilMode>
        
        indices             : Option<BufferView>
        indirect            : Option<IMod<IIndirectBuffer>>
        instanceAttributes  : Map<Symbol, BufferView>
        vertexAttributes    : Map<Symbol, BufferView>
        
        uniforms            : Map<Symbol, IMod>
        trafos              : list<IMod<Trafo3d>>

        writeBuffers        : Option<Set<Symbol>>


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

        member x.Run(f : unit -> IMod<list<FShadeEffect>>) =
            let surface = 
                f() |> Mod.map (fun effects ->
                    effects
                        |> FShade.Effect.compose
                        |> FShadeSurface.Get
                        :> ISurface
                )
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
            drawCallInfos       = ro.DrawCallInfos
            mode                = ro.Mode
            surface             = ro.Surface
                              
            depthTest           = ro.DepthTest
            cullMode            = ro.CullMode
            blendMode           = ro.BlendMode
            fillMode            = ro.FillMode
            stencilMode         = ro.StencilMode
                  
            indirect            = if isNull ro.IndirectBuffer then None else Some ro.IndirectBuffer
            indices             = ro.Indices
            instanceAttributes  = Map.empty
            vertexAttributes    = Map.empty
                              
            uniforms            = Map.empty
            trafos              = scope?ModelTrafoStack
                        
            writeBuffers        = ro.WriteBuffers
                             
            scope               = ro
            final               = []
        }

    let create() =
        Ag.getContext() |> ofScope

    let isActive = reader (fun s -> s.isActive)
    let mode = reader (fun s -> s.mode)
    let surface = reader (fun s -> s.surface)
    let depthTest = reader (fun s -> s.depthTest)
    let cullMode = reader (fun s -> s.cullMode)
    let blendMode = reader (fun s -> s.blendMode)
    let fillMode = reader (fun s -> s.fillMode)
    let stencilMode = reader (fun s -> s.stencilMode)
    let indices = reader (fun s -> s.indices)
    let instanceAttributes = reader (fun s -> s.instanceAttributes)
    let vertexAttributes = reader (fun s -> s.vertexAttributes)
    let uniforms = reader (fun s -> s.uniforms)
    let trafos = reader (fun s -> s.trafos)
    let writeBuffers = reader (fun s -> s.writeBuffers)
    let scope = reader (fun s -> s.scope)
        

type private AirAttributeProvider(local : Map<Symbol, BufferView>, inh : IAttributeProvider) =
    interface IAttributeProvider with
        member x.TryGetAttribute(sem) =
            match Map.tryFind sem local with
                | Some v -> Some v
                | None -> inh.TryGetAttribute sem

        member x.All = Seq.empty
        member x.Dispose() = ()

type private AirUniformProvider(local : Map<Symbol, IMod>, trafos : list<IMod<Trafo3d>>, inh : IUniformProvider) =
    static let mt = Symbol.Create "ModelTrafo"
    let model = lazy (Aardvark.SceneGraph.Semantics.TrafoSemantics.flattenStack trafos)



    interface IUniformProvider with
        member x.TryGetUniform(scope, sem) =
            if sem = mt then 
                model.Value :> IMod |> Some
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

                match state.drawCallInfos with
                    | null -> 
                        match state.indirect with
                            | Some b -> ro.IndirectBuffer <- b
                            | None -> failwith "sadasdas"
                    | infos ->
                        ro.DrawCallInfos <- infos

                ro.Mode <- state.mode
                ro.Surface <- state.surface
                ro.DepthTest <- state.depthTest
                ro.CullMode <- state.cullMode
                ro.BlendMode <- state.blendMode
                ro.FillMode <- state.fillMode
                ro.StencilMode <- state.stencilMode
                ro.Indices <- state.indices
                ro.WriteBuffers <- state.writeBuffers

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

    static member BindVertexBuffer(slot : Symbol, buffer : IMod<IBuffer>, elementType : Type, offset : int, stride : int) =
        let view = BufferView(buffer, elementType, offset, stride)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IMod<IBuffer>, elementType : Type, offset : int) =
        let view = BufferView(buffer, elementType, offset)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IMod<IBuffer>, elementType : Type) =
        let view = BufferView(buffer, elementType)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IMod<Array>, elementType : Type) =
        let view = BufferView(buffer |> Mod.map (fun d -> d |> ArrayBuffer :> IBuffer), elementType)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IMod<'a[]>) =
        let view = BufferView(buffer |> Mod.map (fun d -> d |> ArrayBuffer :> IBuffer), typeof<'a>)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int, stride : int) =
        let view = BufferView(Mod.constant buffer, elementType, offset, stride)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int) =
        let view = BufferView(Mod.constant buffer, elementType, offset)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : IBuffer, elementType : Type) =
        let view = BufferView(Mod.constant buffer, elementType)
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : Array) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> Mod.constant, buffer.GetType().GetElementType())
        Air.BindVertexBuffer(slot, view)

    static member BindVertexBuffer(slot : Symbol, buffer : 'a[]) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> Mod.constant, typeof<'a>)
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
                let view = BufferView(v |> ArrayBuffer :> IBuffer |> Mod.constant, v.GetType().GetElementType())
                b <- Map.add sem view b

            { s with vertexAttributes = b }
        )





    // ================================================================================================================
    // Instance Buffers
    // ================================================================================================================
    static member BindInstanceBuffer(slot : Symbol, view : BufferView) =
        modify (fun s -> { s with instanceAttributes = Map.add slot view s.instanceAttributes })

    static member BindInstanceBuffer(slot : Symbol, buffer : IMod<IBuffer>, elementType : Type, offset : int, stride : int) =
        let view = BufferView(buffer, elementType, offset, stride)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IMod<IBuffer>, elementType : Type, offset : int) =
        let view = BufferView(buffer, elementType, offset)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IMod<IBuffer>, elementType : Type) =
        let view = BufferView(buffer, elementType)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IMod<Array>, elementType : Type) =
        let view = BufferView(buffer |> Mod.map (fun d -> d |> ArrayBuffer :> IBuffer), elementType)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IMod<'a[]>) =
        let view = BufferView(buffer |> Mod.map (fun d -> d |> ArrayBuffer :> IBuffer), typeof<'a>)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int, stride : int) =
        let view = BufferView(Mod.constant buffer, elementType, offset, stride)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IBuffer, elementType : Type, offset : int) =
        let view = BufferView(Mod.constant buffer, elementType, offset)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : IBuffer, elementType : Type) =
        let view = BufferView(Mod.constant buffer, elementType)
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : Array) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> Mod.constant, buffer.GetType().GetElementType())
        Air.BindInstanceBuffer(slot, view)

    static member BindInstanceBuffer(slot : Symbol, buffer : 'a[]) =
        let view = BufferView(buffer |> ArrayBuffer :> IBuffer |> Mod.constant, typeof<'a>)
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
                let view = BufferView(v |> ArrayBuffer :> IBuffer |> Mod.constant, v.GetType().GetElementType())
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
            modify (fun s -> { s with indices = BufferView(Mod.constant (ArrayBuffer index :> IBuffer), typeof<'a>) |> Some })


    // ================================================================================================================
    // Uniforms
    // ================================================================================================================
    static member BindUniform(sem : string, value : IMod) =
        modify (fun s -> { s with uniforms = Map.add (Symbol.Create sem) value s.uniforms })

    static member BindUniform(sem : string, value : IMod<'a>) =
        modify (fun s -> { s with uniforms = Map.add (Symbol.Create sem) (value :> IMod) s.uniforms })

    static member BindUniform(sem : Symbol, value : IMod) =
        modify (fun s -> { s with uniforms = Map.add sem value s.uniforms })

    static member BindUniform(sem : Symbol, value : IMod<'a>) =
        modify (fun s -> { s with uniforms = Map.add sem (value :> IMod) s.uniforms })

    static member BindUniform(sem : Symbol, value : 'a) =
        modify (fun s -> { s with uniforms = Map.add sem (Mod.constant value :> IMod) s.uniforms })

    static member BindUniforms(values : list<Symbol * IMod>) =
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

                let constantType = typedefof<ConstantMod<_>>.MakeGenericType [|t|]
                let m = Activator.CreateInstance(constantType, [|u|]) |> unbox<IMod>

                uniforms <- Map.add sem m uniforms

            
            { s with uniforms = uniforms }
        )

    static member BindUniforms(values : list<string * IMod>) =
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

                let constantType = typedefof<ConstantMod<_>>.MakeGenericType [|t|]
                let m = Activator.CreateInstance(constantType, [|u|]) |> unbox<IMod>

                uniforms <- Map.add (Symbol.Create sem) m uniforms

            
            { s with uniforms = uniforms }
        )


    static member BindTexture(sem : Symbol, value : ITexture) =
        modify (fun s -> { s with uniforms = Map.add sem (Mod.constant value :> IMod) s.uniforms })

    static member BindTexture(sem : Symbol, value : ITexture[]) =
        modify (fun s -> { s with uniforms = Map.add sem (Mod.constant value :> IMod) s.uniforms })

    static member BindTexture(sem : Symbol, file : string, config : TextureParams) =
        let value = FileTexture(file, config) :> ITexture
        modify (fun s -> { s with uniforms = Map.add sem (Mod.constant value :> IMod) s.uniforms })

    static member BindTexture(sem : Symbol, file : string, wantMipMaps : bool) =
        let value = FileTexture(file, wantMipMaps) :> ITexture
        modify (fun s -> { s with uniforms = Map.add sem (Mod.constant value :> IMod) s.uniforms })

    static member BindTexture(sem : Symbol, file : string) =
        let value = FileTexture(file, true) :> ITexture
        modify (fun s -> { s with uniforms = Map.add sem (Mod.constant value :> IMod) s.uniforms })



    static member PushTrafo (t : IMod<Trafo3d>) =
        modify (fun s -> { s with trafos = t::s.trafos })

    static member PushTrafo (t : Trafo3d) =
        modify (fun s -> { s with trafos = (Mod.constant t)::s.trafos })


    static member PushScale (scale : float) =
        modify (fun s -> { s with trafos = (Mod.constant (Trafo3d.Scale scale))::s.trafos })


    static member PopTrafo () =
        modify (fun s -> { s with trafos = List.tail s.trafos })



    // ================================================================================================================
    // Surface
    // ================================================================================================================
    static member BindSurface(surface : IMod<ISurface>) =
        modify (fun s -> { s with surface = surface })

    static member BindSurface(surface : ISurface) =
        modify (fun s -> { s with surface = Mod.constant surface })

    static member BindEffect (l : list<FShadeEffect>) =
        let surf = FShadeSurface.Get (FShade.Effect.compose l) :> ISurface
        modify (fun s -> { s with surface = Mod.constant surf })

    static member BindShader = airShader


    // ================================================================================================================
    // DepthTest
    // ================================================================================================================
    static member DepthTest(mode : IMod<DepthTestMode>) =
        modify (fun s -> { s with depthTest = mode })

    static member DepthTest(mode : DepthTestMode) =
        modify (fun s -> { s with depthTest = Mod.constant mode })


    // ================================================================================================================
    // CullMode
    // ================================================================================================================
    static member CullMode(mode : IMod<CullMode>) =
        modify (fun s -> { s with cullMode = mode })

    static member CullMode(mode : CullMode) =
        modify (fun s -> { s with cullMode = Mod.constant mode })

    // ================================================================================================================
    // BlendMode
    // ================================================================================================================
    static member BlendMode(mode : IMod<BlendMode>) =
        modify (fun s -> { s with blendMode = mode })

    static member BlendMode(mode : BlendMode) =
        modify (fun s -> { s with blendMode = Mod.constant mode })

    // ================================================================================================================
    // FillMode
    // ================================================================================================================
    static member FillMode(mode : IMod<FillMode>) =
        modify (fun s -> { s with fillMode = mode })

    static member FillMode(mode : FillMode) =
        modify (fun s -> { s with fillMode = Mod.constant mode })

    // ================================================================================================================
    // StencilMode
    // ================================================================================================================
    static member StencilMode(mode : IMod<StencilMode>) =
        modify (fun s -> { s with stencilMode = mode })

    static member StencilMode(mode : StencilMode) =
        modify (fun s -> { s with stencilMode = Mod.constant mode })


    // ================================================================================================================
    // WriteBuffers
    // ================================================================================================================
    static member WriteBuffers(buffers : list<Symbol>) =
        modify (fun s -> { s with writeBuffers = Some (Set.ofList buffers) })

    static member WriteBuffers(buffers : list<string>) =
        buffers |> List.map Symbol.Create |> Air.WriteBuffers

    static member WriteAllBuffers() =
        modify (fun s -> { s with writeBuffers = None })


    // ================================================================================================================
    // Toplogy
    // ================================================================================================================
    static member Toplogy (mode : IMod<IndexedGeometryMode>) =
        modify (fun s -> { s with mode = mode })
  
    static member Toplogy (mode : IndexedGeometryMode) =
        modify (fun s -> { s with mode = Mod.constant mode })
    

    // ================================================================================================================
    // Draw
    // ================================================================================================================ 
    static member Draw(infos : IMod<list<DrawCallInfo>>) =
        air {
            do! modify (fun s -> { s with drawCallInfos = infos })
            do! emit
        }

    static member DrawIndirect(b : IMod<IIndirectBuffer>) =
        air {
            do! modify (fun s -> { s with indirect = Some b; drawCallInfos = null })
            do! emit
        }

    static member Draw(infos : IMod<DrawCallInfo>) =
        air {
            do! modify (fun s -> { s with drawCallInfos = Mod.map (fun i -> [i]) infos })
            do! emit
        }

    static member Draw(infos : list<DrawCallInfo>) =
        air {
            do! modify (fun s -> { s with drawCallInfos = Mod.constant infos })
            do! emit
        }

    static member Draw(info : DrawCallInfo) =
        air {
            do! modify (fun s -> { s with drawCallInfos = Mod.constant [info] })
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

    static member Run (a : Air<unit>) =
        a |> Air.RunInScope (Ag.getContext())
   

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
            | :? IMod as m -> m
            | _ -> Mod.constant v :> IMod

    let inline attValue (v : 'a[]) =
        let view = BufferView(v |> ArrayBuffer :> IBuffer |> Mod.constant, typeof<'a>)
        view


    module Sg =
        let air = ``Air Sg Builder``.AirSgBuilder()




namespace Aardvark.SceneGraph.Semantics
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

[<Ag.Semantic>]
type AirNodeSem() =
    member x.RenderObjects(n : Sg.AirNode) =
        n.Content |> Air.Run |> ASet.single



