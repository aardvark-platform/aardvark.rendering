namespace Aardvark.Rendering


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms
open Aardvark.Rendering.NanoVg
open Aardvark.Base.Monads.State
open System.Threading
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Reflection
open Microsoft.FSharp.NativeInterop
    
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Optimizer =
    open Aardvark.Base.Monads.Option

    type RenderObjectSignature =
        {
            IsActive            : IMod<bool>
            RenderPass          : RenderPass
            Mode                : IndexedGeometryMode
            Surface             : IResource<IBackendSurface>
            DepthTest           : IMod<DepthTestMode>
            CullMode            : IMod<CullMode>
            BlendMode           : IMod<BlendMode>
            FillMode            : IMod<FillMode>
            StencilMode         : IMod<StencilMode>
            WriteBuffers        : Option<Set<Symbol>>
            Uniforms            : Map<Symbol, IMod>
            Geometry            : GeometrySignature
        }

    let sw = System.Diagnostics.Stopwatch()

    let modType =
        let cache = 
            Cache<Type, Type>(fun t ->
                match t with
                    | ModOf t -> t
                    | _ -> failwith ""
            )

        cache.Invoke

    let private tryDecomposeRO (runtime : IRuntime) (signature : IFramebufferSignature) (ro : RenderObject) =
        option {
            let! surface =
                if ro.Surface.IsConstant then
                    let s = ro.Surface |> Mod.force
                    runtime.ResourceManager.CreateSurface(signature, Mod.constant s) |> Some
                else
                    None
            sw.Start()

            let! mode =
                if ro.Mode.IsConstant then Some (Mod.force ro.Mode)
                else None


            let! drawCall =
                if ro.DrawCallInfos.IsConstant then
                    match ro.DrawCallInfos |> Mod.force with
                        | [call] when call.InstanceCount = 1 && call.BaseVertex = 0 && call.FirstInstance = 0 && call.FirstIndex = 0 -> 
                            Some call
                        | _ -> None
                else
                    None
          
            let surf = surface.Handle |> Mod.force

            let mutable attributes = []
            let mutable uniforms = []

                
            for (n,_) in surf.Inputs do
                let n = Symbol.Create n
                match ro.VertexAttributes.TryGetAttribute n with
                    | Some v ->
                        attributes <- (n, v, v.ElementType) :: attributes
                    | None ->
                        match ro.Uniforms.TryGetUniform(ro.AttributeScope, n) with
                            | Some v ->
                                uniforms <- (n, v, v.GetType() |> modType) :: uniforms
                            | _ ->
                                ()

            let realUniforms =
                surf.Uniforms
                    |> List.map (fun (n,_) -> Symbol.Create n)
                    |> List.choose (fun n -> match ro.Uniforms.TryGetUniform(ro.AttributeScope, n) with | Some v -> Some (n, v) | _ -> None)
                    |> Map.ofList

                

            let signature = 
                {
                    indexType = match ro.Indices with | Some i -> i.ElementType | _ -> typeof<int>
                    vertexBufferTypes = attributes |> List.map (fun (n,_,t) -> (n,t)) |> Map.ofList
                    uniformTypes = uniforms |> List.map (fun (n,_,t) -> (n,t)) |> Map.ofList
                }

            let! vertexCount =
                match ro.Indices with
                    | None -> Some drawCall.FaceVertexCount
                    | Some i ->
                        let (_,v,_) = attributes |> List.head 
                        let b = Mod.force v.Buffer
                        match b with
                            | :? ArrayBuffer as b -> Some b.Data.Length
                            | :? INativeBuffer as b -> Some (b.SizeInBytes / (Marshal.SizeOf v.ElementType))
                            | _ -> None

            let geometry =
                {
                    faceVertexCount  = drawCall.FaceVertexCount
                    vertexCount      = vertexCount
                    indices          = ro.Indices
                    uniforms         = uniforms |> List.map (fun (n,v,_) -> (n,v)) |> Map.ofList
                    vertexAttributes = attributes |> List.map (fun (n,v,_) -> (n,v)) |> Map.ofList
                }

            let roSignature =
                {
                    IsActive            = ro.IsActive
                    RenderPass          = ro.RenderPass
                    Mode                = mode
                    Surface             = surface
                    DepthTest           = ro.DepthTest
                    CullMode            = ro.CullMode
                    BlendMode           = ro.BlendMode
                    FillMode            = ro.FillMode
                    StencilMode         = ro.StencilMode
                    WriteBuffers        = ro.WriteBuffers
                    Uniforms            = realUniforms
                    Geometry            = signature
                }

            sw.Stop()
            return roSignature, geometry

        }

    let tryDecompose (runtime : IRuntime) (signature : IFramebufferSignature) (ro : IRenderObject) =
        match ro with
            | :? RenderObject as ro -> tryDecomposeRO runtime signature ro
            | _ -> None

    let optimize (runtime : IRuntime) (signature : IFramebufferSignature) (objects : aset<IRenderObject>) : aset<IRenderObject> =
            
        let pools = Dict<GeometrySignature, ManagedPool>()
        let calls = Dict<RenderObjectSignature, DrawCallBuffer * IRenderObject>()
        let disposables = Dict<IRenderObject, IDisposable>()

        let reader = objects.GetReader()
        ASet.custom (fun hugo ->
                
            let output = List<_>()
            let total = System.Diagnostics.Stopwatch()
            total.Start()
            let deltas = reader.GetDelta hugo
            total.Stop()
            printfn "pull: %A" total.MicroTime

            total.Restart()
            sw.Reset()
            for d in deltas do
                match d with
                    | Add ro ->
                        let res = tryDecompose runtime signature ro
                        match res with
                            | Some (signature, o) ->

                                let pool = pools.GetOrCreate(signature.Geometry, fun v -> runtime.CreateManagedPool v)

                                let call = pool.Add o

                                let callBuffer =
                                    match calls.TryGetValue signature with
                                        | (true, (calls, _)) -> 
                                            calls
                                        | _ ->
                                            let buffer = DrawCallBuffer(runtime, true)
                                            let ro =
                                                {
                                                    RenderObject.Create() with
                                                        AttributeScope      = Ag.emptyScope
                
                                                        IsActive            = signature.IsActive
                                                        RenderPass          = signature.RenderPass
                                                        DrawCallInfos       = null
                                                        IndirectBuffer      = buffer
                                                        Mode                = Mod.constant signature.Mode
                                                        Surface             = Mod.constant (signature.Surface.Handle |> Mod.force :> ISurface)
                                                        DepthTest           = signature.DepthTest
                                                        CullMode            = signature.CullMode
                                                        BlendMode           = signature.BlendMode
                                                        FillMode            = signature.FillMode
                                                        StencilMode         = signature.StencilMode
                
                                                        Indices             = Some pool.IndexBuffer
                                                        InstanceAttributes  = pool.InstanceAttributes
                                                        VertexAttributes    = pool.VertexAttributes
                
                                                        Uniforms            = UniformProvider.ofMap signature.Uniforms

                                                        Activate            = fun () -> { new IDisposable with member x.Dispose() = () }
                                                        WriteBuffers        = signature.WriteBuffers
                                                }

                                            calls.[signature] <- (buffer, ro :> IRenderObject)

                                            output.Add(Add (ro :> IRenderObject))

                                            buffer

                                callBuffer.Add call |> ignore
                                disposables.[ro] <- 
                                    { new IDisposable with
                                        member x.Dispose() = 
                                            signature.Surface.Dispose()
                                            call.Dispose()
                                            callBuffer.Remove call |> ignore
                                    }

                            | None ->
                                output.Add (Add ro)
                                    
                    | Rem ro ->
                        match disposables.TryRemove ro with
                            | (true, d) -> d.Dispose()
                            | _ -> output.Add (Rem ro)
            total.Stop()

            printfn "total:     %A" total.MicroTime
            printfn "grounding: %A" sw.MicroTime

            output |> CSharpList.toList
        )
