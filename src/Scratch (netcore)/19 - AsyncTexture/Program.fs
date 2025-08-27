open Aardvark.Base
open Aardvark.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application

let rng = RandomSystem()

module PixImage =

    let checkerboard (size : V2i) =
        let mutable colors = HashMap.empty

        let pi = PixImage<byte>(Col.Format.RGBA, size)
        pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
            let c = c / 11L
            if (c.X + c.Y) % 2L = 0L then
                C4b.White
            else
                match colors |> HashMap.tryFind c with
                | Some c -> c
                | _ ->
                    let color = C4b(rng.UniformInt(256), rng.UniformInt(256), rng.UniformInt(256), 255)
                    colors <- colors |> HashMap.add c color
                    color
        ) |> ignore
        pi

[<AutoOpen>]
module WorkerPoolImplementation =

    open System
    open System.Threading
    open System.Threading.Tasks
    open System.Collections.Generic

    type WorkerPool(threadCount : int) =
        let pending = Queue<CancellationTokenSource * (CancellationToken -> unit)>()
        let mutable running = true

        let tryGetPending() =
            Monitor.Enter pending

            try
                while pending.Count = 0 && running do
                    Monitor.Wait pending |> ignore

                if running then
                    Some <| pending.Dequeue()
                else
                    None

            finally
                Monitor.Exit pending

        let run() =
            while running do
                match tryGetPending() with
                | Some (cancel, action) when not cancel.IsCancellationRequested ->
                    action cancel.Token

                | _ ->
                    ()

        let tasks =
            Array.init threadCount (fun _ ->
                Task.Run run
            )

        member x.Enqueue(action : CancellationToken -> unit) =
            let cancel = new CancellationTokenSource()

            lock pending (fun _ ->
                pending.Enqueue(cancel, action)
                Monitor.Pulse pending
            )

            cancel

        member x.Dispose() =
            running <- false

            lock pending (fun _ ->
                pending.Clear()
                Monitor.PulseAll pending
            )

            Task.WaitAll(tasks)
            tasks |> Array.iter Disposable.dispose

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    let threadCount = 8
    let workerPool = new WorkerPool(threadCount)

module Sg =
    open System.Threading
    open System.Threading.Tasks

    // Asynchronously loaded texture resource.
    type AsyncTexture(runtime : IRuntime, texture : ITexture, preview : ITexture) =
        inherit AdaptiveResource<ITexture>()

        let mutable handle = preview
        let mutable prepared = Unchecked.defaultof<IBackendTexture>
        let mutable cancel = null

        member private x.Prepare(token : CancellationToken) =
            Log.startTimed "Prepare texture asynchronously"

            let prep = runtime.PrepareTexture(texture)
            Thread.Sleep(700 + rng.UniformInt 1000)

            // Texture has been prepared, now we enter a critical section.
            // We don't want another thread to enter Destroy() while we are in here.
            let changed =
                lock x (fun _ ->

                    // If the tasks has been cancelled we have to free the texture
                    // here, the Destroy() method may have been called already.
                    if token.IsCancellationRequested then
                        Log.line "Prepare() cancelled"
                        runtime.DeleteTexture prep
                        false

                    // Otherwise, everything is fine and we can set the new texture
                    else
                        handle <- prep
                        prepared <- prep
                        true
                )

            // Trigger the change
            if changed then
                transact x.MarkOutdated

            Log.stop()

        // Invoked when the resource is first acquired (reference counted, thread-safe via lock)
        // Here we start preparing our texture in a separate thread.
        override x.Create() =
            Log.line "Create()"
            cancel <- workerPool.Enqueue x.Prepare

        // Invoked when the resource is released by the last user (reference counted, thread-safe via lock)
        override x.Destroy() =
            Log.line "Destroy()"

            // Request cancel for pending or running thread.
            // Note: We will not waste time waiting for the thread here.
            if cancel <> null then
                cancel.Cancel()
                cancel.Dispose()

            // Free texture if it has been prepared successfully
            if prepared <> Unchecked.defaultof<_> then
                runtime.DeleteTexture prepared
                prepared <- Unchecked.defaultof<_>

            // Reset handle to preview
            handle <- preview

        override x.Compute(t : AdaptiveToken, rt : RenderToken) =
            handle


    // Below are some examples for how to define Sg.texture* functions for async loading
    let asyncTexture' (semantic : Symbol) (texture : ITexture) (preview : ITexture) (sg : ISg) =
        sg |> Sg.runtimeDependentTexture semantic (fun runtime ->
            AsyncTexture(runtime, texture, preview)
        )

    let asyncTexture (semantic : Symbol) (texture : ITexture) (sg : ISg) =
        let preview = PixTexture2d(DefaultTextures.checkerboardPix, true)
        sg |> asyncTexture' semantic texture preview

    let diffuseAsyncTexture' (texture : ITexture) (preview : ITexture) (sg : ISg) =
        sg |> asyncTexture' DefaultSemantic.DiffuseColorTexture texture preview

    let diffuseAsyncTexture (texture : ITexture) (sg : ISg) =
        sg |> asyncTexture DefaultSemantic.DiffuseColorTexture texture

    let asyncFileTexture' (semantic : Symbol) (path : string) (preview : ITexture) (sg : ISg) =
        let texture = FileTexture(path, true)
        sg |> asyncTexture' semantic texture preview

    let asyncFileTexture (semantic : Symbol) (path : string) (sg : ISg) =
        let texture = FileTexture(path, true)
        sg |> asyncTexture semantic texture

    let asyncDiffuseFileTexture' (path : string) (preview : ITexture) (sg : ISg) =
        sg |> asyncFileTexture' DefaultSemantic.DiffuseColorTexture path preview

    let asyncDiffuseFileTexture (path : string) (sg : ISg) =
        sg |> asyncFileTexture DefaultSemantic.DiffuseColorTexture path


[<EntryPoint>]
let main argv =

    Aardvark.Init()

    use win =
        window {
            backend Backend.Vulkan
            display Display.Mono
            debug true
            samples 8
        }


    let visible = AVal.init true

    win.Keyboard.KeyDown(Keys.Enter).Values.Add(fun _ ->
        transact (fun _ ->
            visible.Value <- not visible.Value
        )
    )

    let makeBox =
        let mutable lastTexture = None

        let getTexture (runtime : IRuntime) =
            // Either use the last texture, or make a new one
            // Just diversifying our test setup here
            match lastTexture with
            | Some last when rng.UniformInt(2) = 0 -> last
            | _ ->
                // The texture we want to prepare asynchronously.
                // Could also be a FileTexture.
                let pix = PixImage.checkerboard (V2i(512))
                let tex = PixTexture2d(pix, true)
                let atex = Sg.AsyncTexture(runtime, tex, DefaultTextures.checkerboard.GetValue()) :> aval<_>
                lastTexture <- Some atex
                atex

        let box =
            Box3d(-V3d(0.5), V3d(0.5))

        fun (position : V3d) ->
            Sg.box' C4b.Black box
            |> Sg.runtimeDependentDiffuseTexture getTexture
            |> Sg.translation' position
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.diffuseTexture
                do! DefaultSurfaces.simpleLighting
            }

    // Define our ISg as usual
    let sg =
        let cnt = 5
        let offset = 1.5

        List.init (sqr cnt) (fun i ->
            let x = float (i % cnt) * offset - offset * (float (cnt / 2))
            let y = float (i / cnt) * offset - offset * (float (cnt / 2))

            makeBox <| V3d(float x, float y, 0.0)
        )
        |> Sg.ofList

    win.Scene <-
        visible |> AVal.map (fun v ->
            if v then sg else Sg.empty
        )
        |> Sg.dynamic

    win.Run()

    workerPool.Dispose()

    0
