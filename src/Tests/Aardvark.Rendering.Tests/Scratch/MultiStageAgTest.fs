namespace Aardvark.Rendering.GL.Tests


module MultipleStageAgMemoryLeakTest =

    open System
    open Aardvark.Base
    open FSharp.Data.Adaptive
    open Aardvark.Rendering
    open Aardvark.SceneGraph
    open Aardvark.SceneGraph.Semantics

    open Aardvark.Application
    open Aardvark.Application.Slim

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
            cset [ Trafo3d.Identity, 
                   Array.init 1000 (constF V3f.OOI), 
                   Array.init 1000 (constF C4b.Red),
                   ZZZZZLeak globalLeakCnt ]
        interface IContentWithImperativeInterface with
            member x.AdaptiveRenderArrays = cset :> aset<_>

    type EmptyImperativeSceneStructure() =
        interface IContentWithImperativeInterface with
            member x.AdaptiveRenderArrays = ASet.empty

    type Engine = { p : cval<Option<IContentWithImperativeInterface>> }

    type IDog = interface end

    type DogGroup(xs : aset<IDog>) =
        interface IDog
        member x.Children = xs

    type DataDog(d : aval<Data>) =
        interface IDog
        member x.Data = d

    type TrafoNode(localTrafos : list<cval<Trafo3d>>, c : aval<IDog>) =
        interface IDog 
        member x.Trafos = localTrafos
        member x.Child = c

    type DogRoot(c : aval<IDog>) =
        interface IDog
        member x.Child = c

    type RenderData = Data * aval<Trafo3d>

    [<Rule>]
    type DogSemantics() = 

        let getMyTrafo (d : Ag.Scope) =
            AVal.custom (fun t -> (d?Trafos : seq<aval<_>>) |> Seq.map (fun v -> v.GetValue t) |> Seq.fold (*) Trafo3d.Identity) 

        member x.Leafs(data : DataDog, scope : Ag.Scope) : aset<RenderData> =
            aset {
                let! d = data.Data
//                let trafo : aval<Trafo3d> = AVal.constant Trafo3d.Identity // data?Trafo()
//                let trafo : aval<Trafo3d> = data?Trafo()
//                let trafo : aval<Trafo3d> = getMyTrafo data
                let trafo : aval<Trafo3d> = scope?Trafo2
                yield d,trafo
            }

        member x.Leafs(t : TrafoNode, scope : Ag.Scope) : aset<RenderData> =
            aset {
                let! c = t.Child
                yield! (c?Leafs(scope) : aset<_>)
            }

        member x.Leafs(t : DogRoot, scope : Ag.Scope) : aset<RenderData> =
            aset {
                let! c = t.Child
                yield! (c?Leafs(scope) : aset<_>)
            }

        member x.Leafs(t : DogGroup, scope : Ag.Scope) : aset<RenderData> =
            aset {
                for e in t.Children do
                    yield! (e?Leafs(scope) : aset<_>)
            }

//        member x.Trafo2(r : Root<IDog>) =
//            r.Child?Trafo2 <- AVal.init Trafo3d.Identity

        member x.Trafo2(r : DogRoot, scope : Ag.Scope) =
            r.Child?Trafo2 <- AVal.init Trafo3d.Identity

        member x.Trafo2(r : TrafoNode, scope : Ag.Scope) =
            r.Child?Trafo2 <- AVal.map2 (*) (AVal.init Trafo3d.Identity) scope?Trafo2 // BUG: this makes a leak
            //r.Child?Trafo2 <- AVal.init Trafo3d.Identity // this works

        member x.Trafos(r : Root<IDog>, scope : Ag.Scope) = 
            r.Child?Trafo <- [AVal.init Trafo3d.Identity]
        member x.Trafos(t : TrafoNode, scope : Ag.Scope) =
            t.Child?Trafos <- t.Trafos @ t.Trafos
        member x.Trafo(d : IDog, scope : Ag.Scope) =
            AVal.custom (fun t -> (scope?Trafos : seq<aval<_>>) |> Seq.map (fun v -> v.GetValue t) |> Seq.fold (*) Trafo3d.Identity) 


    let run () =
        Aardvark.Init()

        let activeEngine = AVal.init { p = AVal.init (Some ( EmptyImperativeSceneStructure() :> IContentWithImperativeInterface))}

        let sceneData (e : aval<Engine>) =
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
                    yield TrafoNode(t1, (AVal.init (DataDog (AVal.init d) :> IDog))) :> IDog
                    //yield TrafoNode(t1, (AVal.init (DataDog (AVal.init d) :> IDog))) :> IDog
            }


        let dog = DogRoot (AVal.init <| (DogGroup d1 :> IDog))


        let chunkVisualization t2 ((trafo,vertics,colors,leak) : PointChunk) : ISg =
            Sg.draw IndexedGeometryMode.PointList
                |> Sg.vertexAttribute DefaultSemantic.Positions     (vertics |> AVal.constant)
                |> Sg.vertexAttribute DefaultSemantic.Colors        (colors  |> AVal.constant)
                |> Sg.trafo (AVal.map (fun t -> t * trafo) t2)
                //|> Sg.trafo (AVal.init trafo)

        let renderView (d : IDog) =
            let leafs  : aset<RenderData> = d?Leafs(Ag.Scope.Root)
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
                    activeEngine.Value <-  { p = AVal.init <| Some (ImperativeSceneStructure() :> _)}
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
                        activeEngine.Value <- { p = AVal.init <| None}
                    )
                    reader.GetChanges() |> ignore

                foo ()
                GC.Collect()
                GC.WaitForPendingFinalizers()

        ) |> ignore

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 10.0 (float s.X / float s.Y))


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
               |> Sg.viewTrafo (viewTrafo   |> AVal.map CameraView.viewTrafo )
               |> Sg.projTrafo (perspective |> AVal.map Frustum.projTrafo    )

        let task = app.Runtime.CompileRender(win.FramebufferSignature, sg)

        win.RenderTask <- task 
        win.Run()
        0
