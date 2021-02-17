namespace Aardvark.Rendering

open System
open System.Threading
open Aardvark.Base
open FSharp.Data.Adaptive

[<AbstractClass>]
type AbstractRenderTask() =
    inherit AdaptiveObject()

    let id = newId()

    static let resourcesInUse = obj()

    //static let dynamicUniforms =
    //    Set.ofList [
    //        "ViewTrafo"
    //        "ProjTrafo"
    //    ]

    static let runtimeUniforms =
        Map.ofList [
            "ViewportSize", fun (o : OutputDescription) -> o.viewport.Size + V2i.II
        ]

    let mutable frameId = 0UL

    let mutable disposed = 0

    let runtimeValueCache = Dict.empty
    let currentOutput = AVal.init { framebuffer = Unchecked.defaultof<_>; viewport = Box2i(V2i.OO, V2i.II) }
    let tryGetRuntimeValue (name : string) =
        runtimeValueCache.GetOrCreate(name, fun name ->
            // TODO: different runtime-types
            match Map.tryFind name runtimeUniforms with
                | Some f ->
                    currentOutput |> AVal.map f :> IAdaptiveValue |> Some
                | None ->
                    None
        )

    let transaction = new Transaction()

    //let hooks : Dictionary<string, DefaultingModTable> = Dictionary.empty
    //let hook (name : string) (m : IAdaptiveValue) : IAdaptiveValue =
        //if Set.contains name dynamicUniforms then
        //    match hooks.TryGetValue(name) with
        //        | (true, table) ->
        //            table.Hook m

        //        | _ ->
        //            let tValue = m.GetType().GetInterface(typedefof<aval<_>>.Name).GetGenericArguments().[0]
        //            let tTable = typedefof<DefaultingModTable<_>>.MakeGenericType [| tValue |]
        //            let table = Activator.CreateInstance(tTable) |> unbox<DefaultingModTable>
        //            hooks.[name] <- table
        //            table.Hook m
        //else
        //    m

    let hookProvider (provider : IUniformProvider) =
        { new IUniformProvider with
            member x.TryGetUniform(scope, name) =
                match tryGetRuntimeValue (string name)  with
                    | Some v -> Some v
                    | _ ->
                        provider.TryGetUniform(scope, name)
                        //let res = provider.TryGetUniform(scope, name)
                        //match res with
                        //    | Some res -> hook (string name) res |> Some
                        //    | None -> None

            member x.Dispose() =
                provider.Dispose()
        }

    member private x.UseValues (token : AdaptiveToken, output : OutputDescription, f : AdaptiveToken -> 'a) =
        //let toReset = List()

        // simulate transact but with reusing transaction
        useTransaction transaction (fun () ->
            currentOutput.Value <- output

            //output.overrides |> Map.iter (fun name value ->
            //    match hooks.TryGetValue(name) with
            //        | (true, table) ->
            //            table.Set(value)
            //            toReset.Add table
            //        | _ ->
            //            ()
            //    )
        )
        transaction.Commit() // only commit as "transact"

        f(token)
        //if toReset.Count = 0 then
        //    f(token)
        //else
        //    let innerToken = token.Isolated //AdaptiveToken(token.Depth, token.Caller, System.Collections.Generic.HashSet())
        //    try
        //        f(innerToken)
        //    finally
        //        innerToken.Release()
        //        transact (fun () ->
        //            for r in toReset do r.Reset()
        //        )
        //        x.PerformUpdate(token, RenderToken.Empty)

    member x.HookProvider (uniforms : IUniformProvider) =
        hookProvider uniforms

    member x.HookRenderObject (ro : RenderObject) =
        { ro with Uniforms = hookProvider ro.Uniforms }

    static member ResourcesInUse = resourcesInUse

    abstract member FramebufferSignature : Option<IFramebufferSignature>
    abstract member Runtime : Option<IRuntime>
    abstract member PerformUpdate : AdaptiveToken * RenderToken -> unit
    abstract member Perform : AdaptiveToken * RenderToken * OutputDescription * IQuery -> unit
    abstract member Release : unit -> unit
    abstract member Use : (unit -> 'a) -> 'a

    member x.Dispose() =
        if Interlocked.Exchange(&disposed, 1) = 0 then
            x.Release()

    member x.FrameId = frameId
    member x.Run(token : AdaptiveToken, t : RenderToken, out : OutputDescription, queries : IQuery) =
        lock resourcesInUse (fun _ ->
            x.EvaluateAlways token (fun token ->
                x.OutOfDate <- true
                x.UseValues(token, out, fun token ->
                    x.Perform(token, t, out, queries)
                    frameId <- frameId + 1UL
                )
            )
        )

    member x.Update(token : AdaptiveToken, t : RenderToken) =
        lock resourcesInUse (fun _ ->
            x.EvaluateAlways token (fun token ->
                if x.OutOfDate then
                    x.PerformUpdate(token, t)
            )
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    interface IRenderTask with
        member x.Id = id
        member x.FramebufferSignature = x.FramebufferSignature
        member x.Runtime = x.Runtime
        member x.FrameId = frameId
        member x.Update(token,t) = x.Update(token,t)
        member x.Run(token, t, out, queries) = x.Run(token, t, out, queries)
        member x.Use f = x.Use f