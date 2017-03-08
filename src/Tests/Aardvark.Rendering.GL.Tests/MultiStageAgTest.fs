namespace Aardvark.Rendering.GL.Tests


module MultipleStageAgMemoryLeakTest =

    open System
    open Aardvark.Base
    open Aardvark.Base.Incremental
    open Aardvark.Base.Rendering
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    open Aardvark.Application
    open Aardvark.Application.WinForms

    open Aardvark.Base.Ag

    type ZZZZZLeak(cnt : ref<int>) =
        do cnt := !cnt + 1
        override x.Finalize() = cnt := !cnt - 1

    let globalLeakCnt = ref 0

    type PointChunk = Trafo3d * V3f[] * C4b[] * ZZZZZLeak

    type Data =
        | PointSet of aset<PointChunk>

    type IContentWithImperativeInterface = 
        abstract member AdaptiveRenderArrays : aset<PointChunk>

    type ImperativeSceneStructure() =
        let cset = 
            CSet.ofSeq [ Trafo3d.Identity, 
                         Array.init 1000 (constF V3f.OOI), 
                         Array.init 1000 (constF C4b.Red),
                         ZZZZZLeak globalLeakCnt ]
        interface IContentWithImperativeInterface with
            member x.AdaptiveRenderArrays = cset :> aset<_>

    type EmptyImperativeSceneStructure() =
        interface IContentWithImperativeInterface with
            member x.AdaptiveRenderArrays = ASet.empty

    type Engine = { p : IModRef<Option<IContentWithImperativeInterface>> }

    type IDog = interface end

    type DogGroup(xs : aset<IDog>) =
        interface IDog
        member x.Children = xs

    type DataDog(d : IMod<Data>) =
        interface IDog
        member x.Data = d

    type TrafoNode(localTrafos : list<IModRef<Trafo3d>>, c : IMod<IDog>) =
        interface IDog 
        member x.Trafos = localTrafos
        member x.Child = c

    type DogRoot(c : IMod<IDog>) =
        interface IDog
        member x.Child = c

    type RenderData = Data * IMod<Trafo3d>

    [<Semantic>]
    type DogSemantics() = 

        let getMyTrafo d =
            Mod.mapN (fun (xs:seq<Trafo3d>) -> Seq.fold (*) Trafo3d.Identity xs) ( d?Trafos )

        member x.Leafs(data : DataDog) : aset<RenderData> =
            aset {
                let! d = data.Data
//                let trafo : IMod<Trafo3d> = Mod.constant Trafo3d.Identity // data?Trafo()
//                let trafo : IMod<Trafo3d> = data?Trafo()
//                let trafo : IMod<Trafo3d> = getMyTrafo data
                let trafo : IMod<Trafo3d> = data?Trafo2
                yield d,trafo
            }

        member x.Leafs(t : TrafoNode) : aset<RenderData> =
            aset {
                let! c = t.Child
                yield! c?Leafs()
            }

        member x.Leafs(t : DogRoot) : aset<RenderData> =
            aset {
                let! c = t.Child
                yield! c?Leafs()
            }

        member x.Leafs(t : DogGroup) : aset<RenderData> =
            aset {
                for e in t.Children do
                    yield! e?Leafs()
            }

//        member x.Trafo2(r : Root<IDog>) =
//            r.Child?Trafo2 <- Mod.init Trafo3d.Identity

        member x.Trafo2(r : DogRoot) =
            r.Child?Trafo2 <- Mod.init Trafo3d.Identity

        member x.Trafo2(r : TrafoNode) =
            r.Child?Trafo2 <- Mod.map2 (*) (Mod.init Trafo3d.Identity) r?Trafo2 // BUG: this makes a leak
            //r.Child?Trafo2 <- Mod.init Trafo3d.Identity // this works

        member x.Trafos(r : Root<IDog>) = 
            r.Child?Trafo <- [Mod.init Trafo3d.Identity]
        member x.Trafos(t : TrafoNode) =
            t.Child?Trafos <- t.Trafos @ t.Trafos
        member x.Trafo(d : IDog) =
            Mod.mapN (fun (xs:seq<Trafo3d>) -> Seq.fold (*) Trafo3d.Identity xs) ( d?Trafos )


    let run () =

        Ag.initialize()
        Aardvark.Init()

        let activeEngine = Mod.init { p = Mod.init (Some ( EmptyImperativeSceneStructure() :> IContentWithImperativeInterface))}

        let sceneData (e : IMod<Engine>) =
            aset {
                let! engine = e
                let! s = engine.p
                match s with 
                    | Some s -> 
                        let o = s.AdaptiveRenderArrays
                        yield PointSet o
                    | None -> ()
            }

        let data = sceneData activeEngine

        let t1 = []
        let d1 = 
            aset {
                for d in data do
                    yield TrafoNode(t1, (Mod.init (DataDog (Mod.init d) :> IDog))) :> IDog
                    //yield TrafoNode(t1, (Mod.init (DataDog (Mod.init d) :> IDog))) :> IDog
            }


        let dog = DogRoot (Mod.init <| (DogGroup d1 :> IDog))


        let chunkVisualization t2 ((trafo,vertics,colors,leak) : PointChunk) : ISg =
            Sg.draw IndexedGeometryMode.PointList
                |> Sg.vertexAttribute DefaultSemantic.Positions     (vertics |> Mod.constant)
                |> Sg.vertexAttribute DefaultSemantic.Colors        (colors  |> Mod.constant)
                |> Sg.trafo (Mod.map (fun t -> t * trafo) t2)
                //|> Sg.trafo (Mod.init trafo)

        let renderView (d : IDog) =
            let leafs  : aset<RenderData> = d?Leafs()
            aset {
                for l,trafo in leafs do
                    match l with 
                     | PointSet data -> 
                        for d in data do
                            yield chunkVisualization trafo d
            }


        let sg = renderView dog
        let reader = sg.GetReader()
        let rsg = Sg.set sg

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow()
   

        win.Keyboard.Down.Values.Subscribe(fun k -> 
            if k = Keys.N then 
                transact (fun () ->
                    Mod.change activeEngine  { p = Mod.init <| Some (ImperativeSceneStructure() :> _)}
                )
                GC.Collect()
                GC.WaitForPendingFinalizers()
                printfn "leak cnt: %A" !globalLeakCnt
        ) |> ignore

        win.Keyboard.Down.Values.Subscribe(fun k -> 
            
            if k = Keys.G then
                let doit () =
                    GC.Collect()
                    GC.WaitForPendingFinalizers()
                    printfn "leak cnt: %A" !globalLeakCnt
                    //printfn "%A" Aardvark.SceneGraph.Semantics.TrafoSemantics.mulCache

                System.Threading.Tasks.Task.Factory.StartNew(fun () ->
                    System.Threading.Thread.Sleep 100
                    doit ()
                ) |> ignore

                let foo () =
                    transact (fun () ->
                        Mod.change activeEngine  { p = Mod.init <| None}
                    )
                    reader.GetOperations() |> ignore

                foo ()
                GC.Collect()
                GC.WaitForPendingFinalizers()

        ) |> ignore

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


        let viewTrafo = DefaultCameraController.control win.Mouse win.Keyboard win.Time view

        let quadSg =
            let quad =
                let index = [|0;1;2; 0;2;3|]
                let positions = [|V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |]

                IndexedGeometry(IndexedGeometryMode.TriangleList, index, SymDict.ofList [DefaultSemantic.Positions, positions :> Array], SymDict.empty)

            quad |> Sg.ofIndexedGeometry

        let sg =
            rsg |> Sg.effect [
                    DefaultSurfaces.trafo |> toEffect
                    DefaultSurfaces.constantColor C4f.White |> toEffect
                  ]
               |> Sg.viewTrafo (viewTrafo   |> Mod.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- task 
        win.Run()
        0
