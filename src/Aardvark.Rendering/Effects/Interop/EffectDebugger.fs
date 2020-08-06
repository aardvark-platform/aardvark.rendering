namespace Aardvark.Rendering

open FSharp.Data.Adaptive

type EffectDebugger private() =

    static let rec hookObject (o : IRenderObject) =
        match o with
        | :? RenderObject as o ->
            match o.Surface with
            | Surface.FShadeSimple e ->
                match FShade.EffectDebugger.register e with
                | Some (:? aval<FShade.Effect> as e) ->
                    e |> AVal.map (fun e -> { o with Id = newId(); Surface = Surface.FShadeSimple e } :> IRenderObject)
                | _ ->
                    AVal.constant (o :> IRenderObject)
            | _ ->
                AVal.constant (o :> IRenderObject)

        | :? MultiRenderObject as m ->
            let mods = m.Children |> List.map hookObject

            if mods |> List.forall (fun m -> m.IsConstant) then
                AVal.constant (m :> IRenderObject)
            else
                AVal.custom (fun t ->
                    mods |> List.map (fun m -> m.GetValue t) |> MultiRenderObject :> IRenderObject
                )

        | _ ->
            AVal.constant o

    static member Hook (o : IRenderObject) = hookObject o
    static member Hook (set : aset<IRenderObject>) =
        match FShade.EffectDebugger.registerFun with
        | Some _ ->
            set |> ASet.mapA EffectDebugger.Hook
        | None ->
            set