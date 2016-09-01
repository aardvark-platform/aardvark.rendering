#if INTERACTIVE
#I @"../../../bin/Debug"
#I @"../../../bin/Release"
#load "LoadReferences.fsx"
#else
namespace Examples
#endif

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.Interactive
open Aardvark.Base.Incremental
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.WinForms


module Pooling =
    open System.Collections.Generic
    open System.Runtime.InteropServices
    open System.Reflection

    type AdaptiveGeometry =
        {
            mode             : IndexedGeometryMode
            faceVertexCount  : int
            vertexCount      : int
            indices          : BufferView
            uniforms         : SymbolDict<IMod>
            vertexAttributes : SymbolDict<BufferView>
        }

    type Pool =
        abstract member Add : AdaptiveGeometry -> DrawCallInfo
        abstract member Remove : AdaptiveGeometry -> unit
        
        abstract member TryGetAttribute : Symbol -> Option<BufferView>
        abstract member IndexBuffer : BufferView

    type MappedBufferWriter(store : IMappedBuffer, data : IMod<Array>, offset : int, elementSize : int) =
        inherit AdaptiveObject()

        member x.Write(caller : IAdaptiveObject) =
            x.EvaluateIfNeeded caller () (fun () ->
                let data = data.GetValue x
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                try store.Write(gc.AddrOfPinnedObject(), offset * elementSize, data.Length * elementSize)
                finally gc.Free()
            )

    type Conv<'a, 'b> private() =
        static let toModArray (input : IMod) : IMod<Array> =
            let converter = PrimitiveValueConverter.converter 
            let arr = Array.CreateInstance(typeof<'b>, 1)
            input |> unbox<IMod<'a>> |> Mod.map (fun v -> v |> converter |> Array.singleton :> Array)

        static member Instance = toModArray

    let private convCache = Dict<Type * Type, IMod -> IMod<Array>>()
    let conv (inputType : Type) (outputType : Type) : IMod -> IMod<Array> =
        convCache.GetOrCreate((inputType, outputType), System.Func<_,_>(fun (inputType, outputType) ->
            let t = typedefof<Conv<_,_>>.MakeGenericType [|inputType; outputType|]
            let p = t.GetProperty("Instance", BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static)
            p.GetValue(null) |> unbox<IMod -> IMod<Array>>
        ))

    type ManagedBuffer(runtime : IRuntime) =
        inherit DirtyTrackingAdaptiveObject<MappedBufferWriter>()

        let store = runtime.CreateMappedBuffer()

        let mutable elementTypeAndSize = None

        let getElementTypeAndSize (viewType : Type) =
            match elementTypeAndSize with
                | Some (t,s) ->
                    if viewType <> t then failwith "sadasd"
                    (t,s)
                | None ->
                    let t = viewType
                    let s = System.Runtime.InteropServices.Marshal.SizeOf t
                    elementTypeAndSize <- Some (t,s)
                    (t,s)
        
        member x.ElementType = 
            match elementTypeAndSize with
                | Some (t,_) -> t
                | _ -> failwith ""

        member x.Add(range : Range1i, data : BufferView) =
            let t, s = getElementTypeAndSize data.ElementType
            let count = range.Size + 1
            let data = BufferView.download 0 count data

            let writer = MappedBufferWriter(store, data, range.Min, s)
            writer.Write x

            { new IDisposable with
                member __.Dispose() =
                    lock x (fun () ->
                        x.Dirty.Remove writer |> ignore
                        let mutable foo = 0
                        writer.Outputs.Consume(&foo) |> ignore
                    )
            }

        member x.Add(index : int, data : IMod) =
            let contentType =
                match data.GetType() with
                    | ModOf t -> t
                    | _ -> failwith ""

            let t, s = getElementTypeAndSize contentType
            let count = 1
            let data = data |> conv contentType t

            let writer = MappedBufferWriter(store, data, index, s)
            writer.Write x

            { new IDisposable with
                member __.Dispose() =
                    lock x (fun () ->
                        x.Dirty.Remove writer |> ignore
                        let mutable foo = 0
                        writer.Outputs.Consume(&foo) |> ignore
                    )
            }      

        member x.GetValue(caller : IAdaptiveObject) =
            x.EvaluateAlways' caller (fun dirty ->
                for d in dirty do
                    d.Write(x)

                store.GetValue(x)
            )

        member x.Dispose() =
            store.Dispose()

        interface IDisposable with
            member x.Dispose() = x.Dispose()

        interface IMod with
            member x.IsConstant = false
            member x.GetValue c = x.GetValue c :> obj

        interface IMod<IBuffer> with
            member x.GetValue c = x.GetValue c

    type ManagedPool(runtime : IRuntime) =
        let indexManager = MemoryManager.createNop()
        let vertexManager = MemoryManager.createNop()
        let instanceManager = MemoryManager.createNop()
        let indexBuffer = new ManagedBuffer(runtime)
        let vertexBuffers = SymbolDict<ManagedBuffer>()
        let instanceBuffers = SymbolDict<ManagedBuffer>()
        let isEmpty = Mod.init true

        let getVertexBuffer (sym : Symbol) =
            vertexBuffers.GetOrCreate(sym, fun sym -> new ManagedBuffer(runtime))

        let getInstanceBuffer (sym : Symbol) =
            instanceBuffers.GetOrCreate(sym, fun sym -> new ManagedBuffer(runtime))

        member x.IsEmpty = isEmpty :> IMod<_>

        member x.Add(g : AdaptiveGeometry) =
            let ds = List()
            let fvc = g.faceVertexCount
            let vertexCount = g.vertexCount
            
            let vertexPtr = vertexManager.Alloc vertexCount
            let vertexRange = Range1i(int vertexPtr.Offset, int vertexPtr.Offset + vertexCount - 1)
            for (KeyValue(k,v)) in g.vertexAttributes do
                 let target = getVertexBuffer k
                 target.Add(vertexRange, v) |> ds.Add

            let instancePtr = instanceManager.Alloc 1
            for (KeyValue(k,v)) in g.uniforms do
                 let target = getInstanceBuffer k
                 target.Add(int instancePtr.Offset, v) |> ds.Add

            let indexPtr = indexManager.Alloc fvc
            let indexRange = Range1i(int indexPtr.Offset, int indexPtr.Offset + fvc - 1)
            indexBuffer.Add(indexRange, g.indices) |> ds.Add

            let disposable =
                { new IDisposable with
                    member x.Dispose() =
                        for d in ds do d.Dispose()
                        vertexManager.Free vertexPtr
                        instanceManager.Free instancePtr
                        indexManager.Free indexPtr
                }

            let call =
                DrawCallInfo(
                    FaceVertexCount = fvc,
                    FirstIndex = int indexPtr.Offset,
                    FirstInstance = int instancePtr.Offset,
                    InstanceCount = 1,
                    BaseVertex = int vertexPtr.Offset
                )

            call, disposable

        member x.VertexBuffers =
            { new IAttributeProvider with
                member x.Dispose() = ()
                member x.All = Seq.empty
                member x.TryGetAttribute(sem : Symbol) =
                    match vertexBuffers.TryGetValue sem with
                        | (true, v) -> Some (BufferView(v, v.ElementType))
                        | _ -> None
            }

        member x.InstanceBuffers =
            { new IAttributeProvider with
                member x.Dispose() = ()
                member x.All = Seq.empty
                member x.TryGetAttribute(sem : Symbol) =
                    match instanceBuffers.TryGetValue sem with
                        | (true, v) -> Some (BufferView(v, v.ElementType))
                        | _ -> None
            }

module Maya = 

    module Shader =
        open FShade
        type Vertex = 
            {
                [<Semantic("ThingTrafo")>] m : M44d
                [<Semantic("ThingNormalTrafo")>] nm : M33d
                [<Position>] p : V4d
                [<Normal>] n : V3d
            }

        let thingTrafo (v : Vertex) =
            vertex {
                return { v 
                    with 
                        p = v.m * v.p 
                        n = v.nm * v.n
                }
            }

    [<Flags>]
    type ControllerPart =
        | None = 0x00
        | X = 0x01 
        | Y = 0x02 
        | Z = 0x04

    let radius = 0.025

    let intersectController (trafo : Trafo3d) (r : Ray3d) =
        let innerRay = r.Transformed(trafo.Backward)

        let mutable res = ControllerPart.None

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.IOO)) < radius then
            res <- res ||| ControllerPart.X

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.OIO)) < radius then
            res <- res ||| ControllerPart.Y

        if innerRay.GetMinimalDistanceTo(Line3d(V3d.Zero, V3d.OOI)) < radius then
            res <- res ||| ControllerPart.Z

        res


    
    let run () =

        Ag.initialize()
        Aardvark.Init()
        use app = new OpenGlApplication()
        use win = app.CreateSimpleRenderWindow(1)
        win.Text <- "Aardvark rocks \\o/"

        let view = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
        let perspective = 
            win.Sizes 
              |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))


        let viewTrafo = Mod.constant view //DefaultCameraController.control win.Mouse win.Keyboard win.Time view

      

        let pool        = GeometryPool.create()
        let box         = pool.Add Primitives.unitBox.Flat
        let cone        = pool.Add (Primitives.unitCone 16).Flat
        let cylinder    = pool.Add (Primitives.unitCylinder 16).Flat


        let scaleCylinder = Trafo3d.Scale(radius, radius, 1.0)

        let render = 
            Mod.init [
                scaleCylinder * Trafo3d.FromOrthoNormalBasis(V3d.OOI, V3d.OIO, V3d.IOO), cylinder, C4b.Red
                scaleCylinder * Trafo3d.FromOrthoNormalBasis(V3d.IOO, V3d.OOI, V3d.OIO), cylinder, C4b.Green

                scaleCylinder, cylinder, C4b.Blue
            ]

        let drawCallInfos = 
            let rangeToInfo (i : int) (r : Range1i) =
                DrawCallInfo(
                    FaceVertexCount = r.Size + 1, 
                    FirstIndex = r.Min, 
                    InstanceCount = 1, 
                    FirstInstance = i
                )
            render |> Mod.map (fun l -> l |> List.mapi (fun i (_,g,_) -> rangeToInfo i g) |> List.toArray |> ArrayBuffer :> IBuffer)

        let trafos =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (t,_,_) -> t.Forward |> M44f.op_Explicit) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<M44f>)

        let normalTrafos =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (t,_,_) -> t.Backward.Transposed.UpperLeftM33() |> M33f.op_Explicit) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<M33f>)


        let colors =
            let buffer = render |> Mod.map (fun v -> v |> List.map (fun (_,_,c) -> c) |> List.toArray |> ArrayBuffer :> IBuffer) 
            BufferView(buffer, typeof<C4b>)

        let trafo = Symbol.Create "ThingTrafo"
        let normalTrafo = Symbol.Create "ThingNormalTrafo"
        let color = DefaultSemantic.Colors

        let pos = BufferView(pool.GetBuffer DefaultSemantic.Positions, typeof<V3f>)
        let n = BufferView(pool.GetBuffer DefaultSemantic.Normals, typeof<V3f>)

        let sg =
            Sg.air {

                do! Air.BindEffect [
                        Shader.thingTrafo |> toEffect
                        DefaultSurfaces.trafo |> toEffect
                        DefaultSurfaces.vertexColor |> toEffect
                        DefaultSurfaces.simpleLighting |> toEffect
                    ]

                do! Air.BindVertexBuffers [
                        DefaultSemantic.Positions, pos
                        DefaultSemantic.Normals, n
                    ]

                do! Air.BindInstanceBuffers [
                        normalTrafo, normalTrafos
                        trafo, trafos
                        color, colors
                    ]

                do! Air.Toplogy IndexedGeometryMode.TriangleList
                do! Air.DrawIndirect drawCallInfos
            }

        let camera = Mod.map2 (fun v p -> { cameraView = v; frustum = p }) viewTrafo perspective
        let pickRay = Mod.map2 Camera.pickRay camera win.Mouse.Position
        let trafo = Mod.init Trafo3d.Identity
        let controlledAxis = Mod.map2 intersectController trafo pickRay

//        controlledAxis |> Mod.unsafeRegisterCallbackKeepDisposable (fun c ->
//            printfn "%A" c
//        ) |> ignore

        let mutable lastRay = pickRay.GetValue()
        let  moving = ref ControllerPart.None
        win.Mouse.Down.Values.Add (fun b ->
            if b = MouseButtons.Left then
                let c = controlledAxis.GetValue()
                lastRay <- pickRay.GetValue()
                moving := c
                printfn "down %A" c
        )

        win.Mouse.Move.Values.Add (fun m ->
            match !moving with
                | ControllerPart.None -> ()
                | p ->
                    printfn "move"
                    let t = trafo.GetValue()
                    let pickRay = pickRay.GetValue()
                    
                    let ray = pickRay.Transformed(t.Backward)
                    let last = lastRay.Transformed(t.Backward)

                    let delta = 
                        match p with
                            | ControllerPart.X -> 
                                V3d(ray.Intersect(Plane3d.ZPlane).X - last.Intersect(Plane3d.ZPlane).X, 0.0, 0.0)
                            | ControllerPart.Y -> 
                                V3d(0.0, ray.Intersect(Plane3d.ZPlane).Y - last.Intersect(Plane3d.ZPlane).Y, 0.0)
                            | _ -> 
                                V3d(0.0, 0.0, ray.Intersect(Plane3d.XPlane).Z - last.Intersect(Plane3d.XPlane).Z)
                    printfn "%A" delta
                    transact (fun () ->
                        trafo.Value <- t * Trafo3d.Translation(delta)
                    )

                    lastRay <- pickRay
        )
        win.Mouse.Up.Values.Add (fun b ->
            if b = MouseButtons.Left then
                moving := ControllerPart.None
        )

        let sg =
            sg
                |> Sg.trafo trafo
                // viewTrafo () creates camera controls and returns IMod<ICameraView> which we project to its view trafo component by using CameraView.viewTrafo
                |> Sg.viewTrafo (viewTrafo |> Mod.map CameraView.viewTrafo ) 
                // perspective () connects a proj trafo to the current main window (in order to take account for aspect ratio when creating the matrices.
                // Again, perspective() returns IMod<Frustum> which we project to its matrix by mapping ofer Frustum.projTrafo.
                |> Sg.projTrafo (perspective |> Mod.map Frustum.projTrafo    )

        use task = sg |> Sg.compile win.Runtime win.FramebufferSignature
        win.RenderTask <- task
        win.Run()

