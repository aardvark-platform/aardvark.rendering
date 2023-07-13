namespace Aardvark.Rendering

open System
open FShade
open FSharp.Data.Adaptive

[<AutoOpen>]
module ShaderDebuggerExtensions =

    /// Render object with modified / hooked surface.
    type HookedRenderObject internal (hooked : RenderObject, original : RenderObject) =
        inherit RenderObject(hooked)
        member x.Hooked = hooked
        member x.Original = original

        member x.IsModified =
            match hooked.Surface, original.Surface with
            | Surface.FShadeSimple h, Surface.FShadeSimple o -> h <> o
            | _ -> false

    module HookedRenderObject =

        /// Applies the given mapping function to the hooked and original render object.
        let map (mapping : RenderObject -> RenderObject) (ro : HookedRenderObject) =
            HookedRenderObject(mapping ro.Hooked, mapping ro.Original)

    module ShaderDebugger =

        let rec hookRenderObject (ro : IRenderObject) : aval<IRenderObject>=
            match ro with
            | :? RenderObject as ro ->
                match ro.Surface with
                | Surface.FShadeSimple e ->
                    match ShaderDebugger.tryRegisterEffect e with
                    | Some hooked ->
                        hooked |> AVal.map (fun e ->
                            let hooked = RenderObject.Clone ro
                            hooked.Surface <- Surface.FShadeSimple e
                            HookedRenderObject(hooked, ro)
                        )
                    | _ ->
                        AVal.constant ro
                | _ ->
                    AVal.constant ro

            | :? MultiRenderObject as m ->
                let children = m.Children |> List.map hookRenderObject

                if children |> List.forall (fun c -> c.IsConstant) then
                    AVal.constant m
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