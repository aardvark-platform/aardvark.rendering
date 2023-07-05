namespace Aardvark.Rendering

open System
open FShade
open FSharp.Data.Adaptive

[<AutoOpen>]
module ShaderDebuggerExtensions =

    module ShaderDebugger =

        let rec hookRenderObject (ro : IRenderObject) =
            match ro with
            | :? RenderObject as ro ->
                match ro.Surface with
                | Surface.FShadeSimple e ->
                    match ShaderDebugger.tryRegisterEffect e with
                    | Some hooked ->
                        hooked |> AVal.map (fun e ->
                            let ro = RenderObject.Clone ro
                            ro.Surface <- Surface.FShadeSimple e
                            ro :> IRenderObject
                        )
                    | _ ->
                        AVal.constant (ro :> IRenderObject)
                | _ ->
                    AVal.constant (ro :> IRenderObject)

            | :? MultiRenderObject as m ->
                let children = m.Children |> List.map hookRenderObject

                if children |> List.forall (fun c -> c.IsConstant) then
                    AVal.constant (m :> IRenderObject)
                else
                    AVal.custom (fun t ->
                        children |> List.map (fun m -> m.GetValue t) |> MultiRenderObject :> IRenderObject
                    )

            | _ ->
                AVal.constant ro

        let hookRenderObjects (set : aset<IRenderObject>) =
            if ShaderDebugger.isInitialized() then
                set |> ASet.mapA hookRenderObject
            else
                set

[<Obsolete("Use ShaderDebugger module instead.")>]
type EffectDebugger private() =
    static member Hook (o : IRenderObject) = ShaderDebugger.hookRenderObject o
    static member Hook (set : aset<IRenderObject>) = ShaderDebugger.hookRenderObjects set