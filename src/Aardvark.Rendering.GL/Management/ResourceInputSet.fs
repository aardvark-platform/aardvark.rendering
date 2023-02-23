namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive

type ResourceInputSet() =
    inherit AdaptiveObject()

    let mutable updateBuffer = Array.zeroCreate<IResource> 0
    let dirty = System.Collections.Generic.HashSet<IResource>()
    let all = ReferenceCountingSet<IResource>()

    let updateOne (token : AdaptiveToken) (r : IResource) (t : RenderToken) =
        r.Update(token, t)

    let updateDirty (token : AdaptiveToken) (rt : RenderToken) =
        let rec run (level : int) (token : AdaptiveToken) (rt : RenderToken) =
            let struct(buffer, cnt) =
                lock all (fun () ->
                    let cnt = dirty.Count
                    if updateBuffer.Length < cnt then
                        updateBuffer <- Array.zeroCreate<_> cnt
                    dirty.CopyTo(updateBuffer, 0)
                    dirty.Clear()
                    updateBuffer, cnt
                )

            if level > 4 && cnt > 0 then
                Log.warn "nested shit"

            if cnt > 0 then
                for i in 0..cnt-1 do
                    let res = buffer.[i]
                    buffer.[i] <- Unchecked.defaultof<_>
                    if not res.IsDisposed then
                        updateOne token res rt

                run (level + 1) token rt

        run 0 token rt

    override x.InputChangedObject(transaction, object) =
        match object with
        | :? IResource as resource ->
            lock all (fun () -> dirty.Add resource |> ignore)
        | _ ->
            ()

    member x.Count = all.Count

    member x.Add (r : IResource) =
        let needsUpdate =
            lock all (fun () ->
                if all.Add r then
                    lock r (fun () ->
                        if r.OutOfDate then
                            dirty.Add r |> ignore
                            true

                        else
                            r.Outputs.Add x |> ignore
                            false
                    )
                else
                    false
            )

        if needsUpdate then
            Log.warn "adding outdated resource: %A" r.Kind
            x.EvaluateAlways AdaptiveToken.Top (fun token ->
                updateDirty token RenderToken.Empty
            )

    member x.Remove (r : IResource) =
        lock all (fun () ->
            if all.Remove r then
                dirty.Remove r |> ignore
                lock r (fun () -> r.Outputs.Remove x |> ignore)
        )

    member x.Update (token : AdaptiveToken, rt : RenderToken) =
        x.EvaluateAlways token  (fun token ->
            if x.OutOfDate then
                updateDirty token rt
        )

    member x.Dispose () =
        lock all (fun () ->
            for r in all do
                lock r (fun () ->
                    r.Outputs.Remove x |> ignore)

            all.Clear()
            dirty.Clear()
        )

    interface IDisposable with
        member x.Dispose() = x.Dispose()