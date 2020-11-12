namespace Aardvark.Application.OpenVR

open System
open Valve.VR
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.SceneGraph
open FSharp.Data.Traceable

#nowarn "9"

open System.Diagnostics

type VrDeviceType =
    | Other = 0
    | Hmd = 1
    | Controller = 2
    | TrackingReference = 3

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal VrDeviceType =
    let ofETrackedDeviceClass =
        LookupTable.lookupTable [
            ETrackedDeviceClass.Controller, VrDeviceType.Controller
            ETrackedDeviceClass.HMD, VrDeviceType.Hmd
            ETrackedDeviceClass.TrackingReference, VrDeviceType.TrackingReference

            ETrackedDeviceClass.DisplayRedirect, VrDeviceType.Other
            ETrackedDeviceClass.GenericTracker, VrDeviceType.Other
            ETrackedDeviceClass.Invalid, VrDeviceType.Other
        ]


module Unhate =
    open System.Collections.Generic
   
    [<Flags>]
    type Flags =
        | None          = 0x00
        | Transpose     = 0x01
        | Invert        = 0x02
        | GLFlip        = 0x04
        
       
    let private oursToTheirs = Trafo3d.FromBasis(V3d.IOO, V3d.OOI, V3d.OIO, V3d.Zero)
    let private theirsToOurs = oursToTheirs.Inverse
 
    let private applyFlags (f : Flags) (res : Trafo3d) =
        let t = f.HasFlag Flags.Transpose
        let i = f.HasFlag Flags.Invert
        let f = f.HasFlag Flags.GLFlip

        let mutable res = res
        if t then
            res <- Trafo3d(res.Forward.Transposed, res.Backward.Transposed)
 
        if i then
            res <- res.Inverse
 
        if f then
            res <- oursToTheirs * res * theirsToOurs
 
       
        res
 
    let private allFlags = unbox<Flags> 0x07
 
    let private all = List<cval<Flags>>()
    let private trafos = Dict<string, cval<Flags>>()
 
 
    let unhate() =
        let rec unhate (id : int) =
            if id >= all.Count then
                true
 
            elif unhate (id + 1) then
                let v = all.[id].Value
                let nv = int v + 1
                let newFlags = unbox<Flags> nv &&& allFlags
                let step = nv > int allFlags
                all.[id].Value <- newFlags
                step
 
            else
                false
 
        transact (fun () -> unhate 0 |> ignore)
 
        printf " 0: unhate# "
        for (name, v) in Dict.toSeq trafos do
            printf "%s: %A " name v.Value
        printfn ""
 
    let register (name : string) (m : aval<Trafo3d>) =
        let flags = trafos.GetOrCreate(name, fun _ ->
            let m = AVal.init Flags.None
            all.Add m
            m
        )
        AVal.map2 applyFlags flags m
 
module UnhateTest =
    let run() =
        let trafo =
                AVal.init (Trafo3d.Translation(5.0, 6.0, 7.0))
                |> Unhate.register "a"
 
        while true do
            printfn "%A" (trafo.GetValue().Forward)
            Console.ReadLine() |> ignore
            Unhate.unhate()



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VrTrafo =
    
    let private flip = Trafo3d.Identity

    let ofHmdMatrix34 (x : HmdMatrix34_t) =
        let t = 
            M44f(
                x.m0, x.m1, x.m2,  x.m3,
                x.m4, x.m5, x.m6,  x.m7,
                x.m8, x.m9, x.m10, x.m11,
                0.0f, 0.0f, 0.0f, 1.0f
            ) 

        let t = M44d.op_Explicit(t)
        Trafo3d(t,t.Inverse) //* flip

    let ofHmdMatrix44 (x : HmdMatrix44_t) =
        let t = M44f(x.m0,x.m1,x.m2,x.m3,x.m4,x.m5,x.m6,x.m7,x.m8,x.m9,x.m10,x.m11,x.m12,x.m13,x.m14,x.m15)
        let t = M44d.op_Explicit(t)
        Trafo3d(t,t.Inverse)


    let angularVelocity (v : HmdVector3_t) =
        let v = V3d(-v.v0, -v.v1, -v.v2) // transposed world
        flip.Forward.TransformDir v


    let velocity (v : HmdVector3_t) =
        let v = V3d(v.v0, v.v1, v.v2)
        flip.Forward.TransformDir v

    let inline inverse (t : Trafo3d) = t.Inverse

type MotionState() =
    let isValid = AVal.init false
    let pose = AVal.init Trafo3d.Identity
    let angularVelocity = AVal.init V3d.Zero
    let velocity = AVal.init V3d.Zero

    member x.IsValid = isValid :> aval<_>
    member x.Pose = pose :> aval<_>
    member x.Velocity = velocity :> aval<_>
    member x.AngularVelocity = angularVelocity :> aval<_>

    member x.Update(newPose : byref<TrackedDevicePose_t>) =
        if newPose.bDeviceIsConnected && newPose.bPoseIsValid && newPose.eTrackingResult = ETrackingResult.Running_OK then
            let t = VrTrafo.ofHmdMatrix34 newPose.mDeviceToAbsoluteTracking
            isValid.Value <- true

            pose.Value <- Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero) * t * Trafo3d.FromBasis(V3d.IOO, V3d.OOI, -V3d.OIO, V3d.Zero)

            angularVelocity.Value <- VrTrafo.angularVelocity newPose.vAngularVelocity
            velocity.Value <- VrTrafo.velocity newPose.vVelocity
        else
            isValid.Value <- false


module RenderModels =
    open Aardvark.SceneGraph

    [<StructLayout(LayoutKind.Sequential)>]
    type Vertex =
        struct
            val mutable public Postion : V3f
            val mutable public Normal : V3f
            val mutable public TexCoord : V2f
        end
        
    [<StructLayout(LayoutKind.Sequential)>]
    type TextureMap =
        struct
            val mutable public Width : uint16
            val mutable public Height : uint16
            val mutable public Data : nativeptr<byte>
        end
        
    [<StructLayout(LayoutKind.Sequential)>]
    type RenderModel =
        struct
            val mutable public VertexData : nativeptr<Vertex>
            val mutable public VertexCount : uint32
            val mutable public IndexData : nativeptr<uint16>
            val mutable public TriangleCount : uint32
            val mutable public DiffuseTextureId : int32
        end

    let private modelCache = System.Collections.Concurrent.ConcurrentDictionary<string, Option<ISg>>()

    let toSg (m : RenderModel) =
        let call = DrawCallInfo(FaceVertexCount = int m.TriangleCount * 3, InstanceCount = 1)
        let mode = IndexedGeometryMode.TriangleList

        let index = 
            let data : uint16[] = Array.zeroCreate (int m.TriangleCount * 3)
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            Marshal.Copy(NativePtr.toNativeInt m.IndexData, gc.AddrOfPinnedObject(), nativeint m.TriangleCount * 3n * 2n)
            gc.Free()
            data |> Array.map int

        let vertices : Vertex[] = Array.zeroCreate (int m.VertexCount)
        do 
            let gc = GCHandle.Alloc(vertices, GCHandleType.Pinned)
            Marshal.Copy(NativePtr.toNativeInt m.VertexData, gc.AddrOfPinnedObject(), nativeint m.VertexCount * nativeint sizeof<Vertex>)
            gc.Free()


        let positions = vertices |> Array.map (fun v -> V3f(v.Postion.X, -v.Postion.Z, v.Postion.Y))
        let normals = vertices |> Array.map (fun v -> V3f(v.Normal.X, -v.Normal.Z, v.Normal.Y).Normalized)
        let tc = vertices |> Array.map (fun v -> V2f(v.TexCoord.X, 1.0f - v.TexCoord.Y))


        let indexBuffer = ArrayBuffer(index) //NativeMemoryBuffer(NativePtr.toNativeInt m.IndexData, int m.TriangleCount * 3 * sizeof<uint16>)
        let indexBufferView = BufferView(AVal.constant (indexBuffer :> IBuffer), typeof<int>)

        //let vertexBuffer = NativeMemoryBuffer(NativePtr.toNativeInt m.VertexData, int m.VertexCount * sizeof<Vertex>) :> IBuffer
        let positions = BufferView(AVal.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>)
        let normals = BufferView(AVal.constant (ArrayBuffer(normals) :> IBuffer), typeof<V3f>)
        let coords = BufferView(AVal.constant (ArrayBuffer(tc) :> IBuffer), typeof<V2f>)

        let sg = 
            Sg.VertexIndexApplicator(indexBufferView, Sg.render mode call) :> ISg 
                |> Sg.vertexBuffer DefaultSemantic.Positions positions
                |> Sg.vertexBuffer DefaultSemantic.Normals normals
                |> Sg.vertexBuffer DefaultSemantic.DiffuseColorCoordinates coords

        let mutable ptr = 0n
        let mutable err = EVRRenderModelError.InvalidTexture
        if m.DiffuseTextureId >= 0 then
            err <- OpenVR.RenderModels.LoadTexture_Async(m.DiffuseTextureId, &ptr)
            while err = EVRRenderModelError.Loading do
                System.Threading.Thread.Sleep 10
                err <- OpenVR.RenderModels.LoadTexture_Async(m.DiffuseTextureId, &ptr)

        if err = EVRRenderModelError.None then
            let tex : TextureMap = NativeInt.read ptr
            if tex.Width > 0us && tex.Height > 0us then
                let info =
                    VolumeInfo(
                        0L,
                        V3l(int tex.Width, int tex.Height, 4),
                        V3l(4, int tex.Width * 4, 1)
                    )

                let volume = NativeVolume<byte>(tex.Data, info)
                let image = PixImage<byte>(Col.Format.RGBA, V2i(int tex.Width, int tex.Height))

                NativeVolume.using image.Volume (fun dst ->
                    NativeVolume.copy volume dst
                )
            
                let texture = PixTexture2d(PixImageMipMap [| image :> PixImage |], true) :> ITexture

                Sg.diffuseTexture' texture sg
            else
                sg
        else
            sg

    let load (name : string) =
        modelCache.GetOrAdd(name, fun name ->
            let mutable ptr = 0n
            let mutable err = OpenVR.RenderModels.LoadRenderModel_Async(name, &ptr)
            while err = EVRRenderModelError.Loading do   
                System.Threading.Thread.Sleep 10
                err <- OpenVR.RenderModels.LoadRenderModel_Async(name, &ptr)


            if err = EVRRenderModelError.None then
                let model : RenderModel = NativeInt.read ptr
                let sg = toSg model
                Some sg
            else
                Log.warn "[OpenVR] could not load model %s: %A" name err
                None
        )



type VrDevice(system : CVRSystem, deviceType : VrDeviceType, index : int) =

    let getString (prop : ETrackedDeviceProperty) =
        let builder = System.Text.StringBuilder(4096, 4096)
        let mutable err = ETrackedPropertyError.TrackedProp_Success
        let len = system.GetStringTrackedDeviceProperty(uint32 index, prop, builder, uint32 builder.Capacity, &err)
        builder.ToString()

    let getInt (prop : ETrackedDeviceProperty) =
        let mutable err = ETrackedPropertyError.TrackedProp_Success
        let len = system.GetInt32TrackedDeviceProperty(uint32 index, prop, &err)

        len
        
    let renderModelName = lazy ( getString ETrackedDeviceProperty.Prop_RenderModelName_String )
    let renderModel = lazy ( RenderModels.load renderModelName.Value )

    let vendor  = lazy ( getString ETrackedDeviceProperty.Prop_ManufacturerName_String )
    let model   = lazy ( getString ETrackedDeviceProperty.Prop_ModelNumber_String )
    
    let axis = 
        [|
            if deviceType = VrDeviceType.Controller then
                for i in 0 .. 4 do
                    let t = getInt (ETrackedDeviceProperty.Prop_Axis0Type_Int32 + unbox i) |> unbox<EVRControllerAxisType>
                    if t <> EVRControllerAxisType.k_eControllerAxis_None then
                        yield VrAxis(system, t, index, i)
        |]

    let axisByIndex =
        axis |> Seq.map (fun a -> a.Index, a) |> Map.ofSeq

    let events = Event<VREvent_t>()

    let state = MotionState()

    member x.Model = renderModel.Value

    member x.RenderModel =
        model.Value

    member x.Axis = axis

    member x.Type = deviceType

    member x.MotionState = state

    member x.Events = events.Publish

    member x.Index = index

    member x.RenderModelName = renderModelName.Value 

    member internal x.Update(pose : byref<TrackedDevicePose_t>) =
        state.Update(&pose)

        let mutable state = VRControllerState_t()
        if system.GetControllerState(uint32 index, &state, uint32 sizeof<VRControllerState_t>) then
            for a in axis do a.Update(&state)

    member internal x.Trigger(evt : byref<VREvent_t>) =
        let buttonIndex = int evt.data.controller.button |> unbox<EVRButtonId>
        if buttonIndex >= EVRButtonId.k_EButton_Axis0 && buttonIndex <= EVRButtonId.k_EButton_Axis4 then
            let ai = buttonIndex - EVRButtonId.k_EButton_Axis0 |> int
            match Map.tryFind ai axisByIndex with
                | Some a -> a.Trigger(&evt)
                | None -> ()

        events.Trigger(evt)

and VrAxis(system : CVRSystem, axisType : EVRControllerAxisType, deviceIndex : int, axisIndex : int) =
    let touched = AVal.init false
    let pressed = AVal.init false
    let position = AVal.init None
    
    let touch = Event<unit>()
    let untouch = Event<unit>()
    let press = Event<unit>()
    let unpress = Event<unit>()

    override x.ToString() =
        sprintf "{ device = %d; axis = %d }" deviceIndex axisIndex

    member x.Touched = touched :> aval<_>
    member x.Pressed = pressed :> aval<_>
    member x.Position = position :> aval<_>
    member x.Touch = touch.Publish
    member x.UnTouch = untouch.Publish
    member x.Press = press.Publish
    member x.UnPress = unpress.Publish

    member x.Index = axisIndex

    member internal x.Update(s : byref<VRControllerState_t>) =
        if touched.Value then
            let pos : VRControllerAxis_t =
                match axisIndex with
                    | 0 -> s.rAxis0
                    | 1 -> s.rAxis1
                    | 2 -> s.rAxis2
                    | 3 -> s.rAxis3
                    | 4 -> s.rAxis4
                    | _ -> raise <| System.IndexOutOfRangeException()
                
            transact (fun () ->
                position.Value <- Some (V2d(pos.x, pos.y))
            )
        else
            match position.Value with
                | Some _ -> transact (fun () -> position.Value <- None)
                | _ -> ()
        ()

    member internal x.Trigger(e : byref<VREvent_t>) =
        let eventType = e.eventType |> int |> unbox<EVREventType>
        transact (fun () ->
            match eventType with
                | EVREventType.VREvent_ButtonTouch -> 
                    touch.Trigger()
                    touched.Value <- true
                | EVREventType.VREvent_ButtonUntouch -> 
                    untouch.Trigger()
                    touched.Value <- false
                | EVREventType.VREvent_ButtonPress -> 
                    press.Trigger()
                    pressed.Value <- true
                | EVREventType.VREvent_ButtonUnpress -> 
                    unpress.Trigger()
                    pressed.Value <- false
                | _ -> ()
        )


type VrTexture =
    class
        val mutable public Data : nativeint
        val mutable public Info : Texture_t
        val mutable public Flags : EVRSubmitFlags
        val mutable public Bounds : VRTextureBounds_t

        new(d,i,f,b) = { Data = d; Info = i; Flags = f; Bounds = b }

        static member OpenGL(handle : int) =
            let i = Texture_t(eColorSpace = EColorSpace.Auto, eType = ETextureType.OpenGL, handle = nativeint handle)
            let b = VRTextureBounds_t(uMin = 0.0f, uMax = 1.0f, vMin = 0.0f, vMax = 1.0f)
            new VrTexture(0n, i, EVRSubmitFlags.Submit_Default, b)
            
        static member OpenGL(handle : int, bounds : Box2d) =
            let i = Texture_t(eColorSpace = EColorSpace.Auto, eType = ETextureType.OpenGL, handle = nativeint handle)
            let b = VRTextureBounds_t(uMin = float32 bounds.Min.X, uMax = float32 bounds.Max.X, vMin = float32 bounds.Min.Y, vMax = float32 bounds.Max.Y)
            new VrTexture(0n, i, EVRSubmitFlags.Submit_Default, b)

        static member Vulkan(data : VRVulkanTextureData_t) =
            let ptr = Marshal.AllocHGlobal sizeof<VRVulkanTextureData_t>
            NativeInt.write ptr data
            let i = Texture_t(eColorSpace = EColorSpace.Auto, eType = ETextureType.Vulkan, handle = ptr)
            let b = VRTextureBounds_t(uMin = 0.0f, uMax = 1.0f, vMin = 0.0f, vMax = 1.0f)
            new VrTexture(ptr, i, EVRSubmitFlags.Submit_Default, b)

        static member Vulkan(data : VRVulkanTextureData_t, bounds : Box2d) =
            let ptr = Marshal.AllocHGlobal sizeof<VRVulkanTextureData_t>
            NativeInt.write ptr data
            let i = Texture_t(eColorSpace = EColorSpace.Auto, eType = ETextureType.Vulkan, handle = ptr)
            let b = VRTextureBounds_t(uMin = float32 bounds.Min.X, uMax = float32 bounds.Max.X, vMin = float32 bounds.Min.Y, vMax = float32 bounds.Max.Y)
            new VrTexture(ptr, i, EVRSubmitFlags.Submit_Default, b)
            
        static member D3D12(data : D3D12TextureData_t) =
            let ptr = Marshal.AllocHGlobal sizeof<D3D12TextureData_t>
            NativeInt.write ptr data
            let i = Texture_t(eColorSpace = EColorSpace.Auto, eType = ETextureType.DirectX12, handle = ptr)
            let b = VRTextureBounds_t(uMin = 0.0f, uMax = 1.0f, vMin = 0.0f, vMax = 1.0f)
            new VrTexture(ptr, i, EVRSubmitFlags.Submit_Default, b)
            
        member x.Dispose() =
            if x.Data <> 0n then Marshal.FreeHGlobal x.Data

        interface IDisposable with
            member x.Dispose() = x.Dispose()

    end

type VrRenderInfo =
    {
        framebufferSize     : V2i
        viewTrafos          : aval<Trafo3d[]>
        projTrafos          : aval<Trafo3d[]>
    }

    //let swProcessEvents = Stopwatch()
    //let swWaitPoses = Stopwatch()
    //let swUpdatePoses = Stopwatch()
    //let swRender = Stopwatch()
    //let swSubmit = Stopwatch()
    //let swTotal = Stopwatch()

type VrRenderStats =
    {
        Total   : MicroTime
        Clear   : MicroTime
        Render  : MicroTime
        Resolve : MicroTime
    }

    static member Zero = { Total = MicroTime.Zero; Clear = MicroTime.Zero; Render = MicroTime.Zero; Resolve = MicroTime.Zero }

type VrSystemStats =
    {
        ProcessEvents   : MicroTime
        WaitGetPoses    : MicroTime
        UpdatePoses     : MicroTime
        Render          : VrRenderStats
        Submit          : MicroTime
        Total           : MicroTime
        FrameCount      : int
    }
    
    static member Zero = { ProcessEvents = MicroTime.Zero; WaitGetPoses = MicroTime.Zero; UpdatePoses = MicroTime.Zero; Render = VrRenderStats.Zero; Submit = MicroTime.Zero; Total = MicroTime.Zero; FrameCount = 0 }


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VrSystemStats =
    open Aardvark.SceneGraph
    open FShade

    let toStackedGraph (stats : aval<VrSystemStats>) =
        
        let col (v : int) =
            C4b(byte (v >>> 16), byte (v >>> 8), byte v)

        let trafosAndColors =
            stats |> AVal.map (fun s ->
                

                let timesAndColors =
                    [|
                        s.ProcessEvents.TotalSeconds, col 0x161148
                        s.WaitGetPoses.TotalSeconds, col 0xB8B4DE
                        s.UpdatePoses.TotalSeconds, col 0x6B66A5

                        s.Render.Clear.TotalSeconds, col 0xFFE9C7
                        s.Render.Render.TotalSeconds, col 0x67430B
                        s.Render.Resolve.TotalSeconds, col 0xEDC384

                        s.Submit.TotalSeconds, col 0x25705A
                    |]

                let scale = 1.0 / s.Total.TotalSeconds
                
                let scales =
                    timesAndColors 
                        |> Array.map (fun (t,_) -> t * scale)

                let offsets =
                    scales
                        |> Array.scan (+) 0.0
                        |> Array.take timesAndColors.Length
                

                let trafos = 
                    Array.map2 (fun (o : float) (s : float) -> Trafo3d.Scale(1.0, s, 1.0) * Trafo3d.Translation(0.0, o, 0.0)) offsets scales

                let colors =
                    Array.map snd timesAndColors
                    
                trafos, colors
            )

        let trafos = AVal.map (fun (t,_) -> t :> System.Array) trafosAndColors
        let colors = AVal.map (fun (_,c) -> c :> System.Array) trafosAndColors

        let baseBox = 
            Sg.box' C4b.White (Box3d(V3d.Zero, V3d.III))
                |> Sg.shader {
                    do! DefaultSurfaces.trafo

                    do! fun (v : Effects.Vertex) ->
                        fragment {
                            let c : V4d = uniform?Color
                            return c
                        }
                    
                    do! DefaultSurfaces.simpleLighting
                }

        let instanced =
            Map.ofList [
                "ModelTrafo", (typeof<Trafo3d>, trafos)
                "Color",      (typeof<C4b>, colors)
            ]

        Sg.instanced' instanced baseBox


type VrApplicationType =
    | Scene = 1
    | Overlay = 2
    | Background = 3
    | Utility = 4
    | VRMonitor = 5
    | SteamWatchdog = 6
    | Bootstrapper = 7
    
type VrSystem(appType : VrApplicationType) =

    let mutable shutdown = false

    let system =
        let mutable err = EVRInitError.None
        let sys = OpenVR.Init(&err, unbox<_> appType)
        if err <> EVRInitError.None then
            Log.error "[OpenVR] %s" (OpenVR.GetStringForHmdError err)
            failwithf "[OpenVR] %s" (OpenVR.GetStringForHmdError err)
        sys

    let deviceCache = 
        [|
            for i in 0u .. OpenVR.k_unMaxTrackedDeviceCount-1u do
                let deviceType = system.GetTrackedDeviceClass i
                if deviceType <> ETrackedDeviceClass.Invalid then
                    yield VrDevice(system, VrDeviceType.ofETrackedDeviceClass deviceType, int i) |> Some
                else
                    yield None
        |]
        
    let connectedDevices = AVal.custom (fun _ -> lock deviceCache (fun () -> Seq.choose id deviceCache |> HashSet.ofSeq))
    let connectedAll = connectedDevices |> ASet.ofAVal
    let connectedHmds = connectedAll |> ASet.filter (fun d -> d.Type = VrDeviceType.Hmd)
    let connectedControllers = connectedAll |> ASet.filter (fun d -> d.Type = VrDeviceType.Controller)
    
    let updateDevice (i : uint32) =
        if i >= 0u && i < uint32 deviceCache.Length then
            lock deviceCache (fun () ->
                let deviceType = system.GetTrackedDeviceClass i
                if deviceType <> ETrackedDeviceClass.Invalid then
                    let d = VrDevice(system, VrDeviceType.ofETrackedDeviceClass deviceType, int i)
                    deviceCache.[int i] <- Some d
                else
                    deviceCache.[int i] <- None
            )
            transact connectedDevices.MarkOutdated

    let tryGetDevice (i : uint32) =
        if i >= 0u && i < uint32 deviceCache.Length then
            let mutable changed = false
            let result = 
                lock deviceCache (fun () ->
                    match deviceCache.[int i] with
                    | Some d -> Some d
                    | None -> 
                        let deviceType = system.GetTrackedDeviceClass i
                        if deviceType <> ETrackedDeviceClass.Invalid then
                            let d = VrDevice(system, VrDeviceType.ofETrackedDeviceClass deviceType, int i)
                            deviceCache.[int i] <- Some d
                            changed <- true
                            Some d
                        else
                            None
                )
            if changed then 
                transact connectedDevices.MarkOutdated
            result
        else
            None

    let events = System.Collections.Generic.List<VREvent_t>()
            
    /// <summary>
    /// Returns true if any Shutdown/ProcessQuit event has been received
    /// </summary
    member x.ShutdownRequested with get() = shutdown        

    /// <summary>
    /// OpenVR system
    /// </summary
    member x.System with get() = system

    /// <summary>
    /// Incremental view of all connected devices
    /// </summary
    member x.ConnectedAll with get() = connectedAll

    /// <summary>
    /// Incremental set of connected devices
    /// </summary
    member x.ConnectedDevices with get() = connectedDevices

    /// <summary>
    /// Incremental set of connected Hmd devices
    /// </summary
    member x.ConnectedHmds with get() = connectedHmds
    
    /// <summary>
    /// Incremental set of connected controllers
    /// </summary
    member x.ConnectedControllers with get() = connectedControllers

    /// <summary>
    /// Enumeration of connected devices
    /// </summary
    member x.AllDevices
        with get() = Seq.init (int OpenVR.k_unMaxTrackedDeviceCount) uint32 |> Seq.choose tryGetDevice

    /// <summary>
    /// Enumeration of controllers
    /// </summary
    member x.Controllers = x.AllDevices |> Seq.filter (fun d -> d.Type = VrDeviceType.Controller)

    /// <summary>
    /// Try get device by index
    /// </summary 
    member x.TryGetDevice(deviceIndex : int) =
        tryGetDevice(uint32 deviceIndex)
        
    /// <summary>
    /// Process events: poll system events and update device states and triggers
    /// <returns>Returns new events to allow application side additional processing</returns>
    /// </summary
    member x.ProcessEvents() : seq<_> =
        events.Clear()
        let mutable evt : VREvent_t = Unchecked.defaultof<VREvent_t>
        while system.PollNextEvent(&evt, uint32 sizeof<VREvent_t>) do
            events.Add(evt)
            
            let t = unbox<EVREventType> (int evt.eventType)

            //Log.line "%s" (system.GetEventTypeNameFromEnum(t))

            let id = evt.trackedDeviceIndex
            match t with
            | EVREventType.VREvent_TrackedDeviceActivated 
            | EVREventType.VREvent_TrackedDeviceDeactivated 
            | EVREventType.VREvent_TrackedDeviceUpdated
            | EVREventType.VREvent_TrackedDeviceRoleChanged ->
                updateDevice id
            | EVREventType.VREvent_ProcessQuit
            | EVREventType.VREvent_ProcessDisconnected
            | EVREventType.VREvent_DriverRequestedQuit
            | EVREventType.VREvent_Quit
                -> 
                    system.AcknowledgeQuit_Exiting()
                    shutdown <- true
            | _ ->
                match tryGetDevice id with
                | Some device ->
                    device.Trigger(&evt)
                | None ->
                    ()

        events :> seq<_>

    /// <summary>
    /// Updates device poses
    /// NOTE: requries transaction on application side
    /// </summary 
    member x.UpdatePoses(renderPoses: TrackedDevicePose_t[]) =
        for i in 0 .. renderPoses.Length - 1 do
            let mutable pose = renderPoses.[i]
            match tryGetDevice (uint32 i) with
            | Some device -> device.Update(&pose)
            | None -> ()


[<AbstractClass>]
type VrRenderer(adjustSize : V2i -> V2i, system : VrSystem) =
    
    static let sEyeIndex = Symbol.Create "EyeIndex"
    static let sRCoord = Symbol.Create "RCoord"
    static let sGCoord = Symbol.Create "GCoord"
    static let sBCoord = Symbol.Create "BCoord"

    let mutable adjustSize = adjustSize

    let hiddenAreaMesh =
        let lMesh = system.System.GetHiddenAreaMesh(EVREye.Eye_Left, EHiddenAreaMeshType.k_eHiddenAreaMesh_Standard)
        let rMesh = system.System.GetHiddenAreaMesh(EVREye.Eye_Right, EHiddenAreaMeshType.k_eHiddenAreaMesh_Standard)

        let lFvc = int lMesh.unTriangleCount * 3
        let rFvc = int rMesh.unTriangleCount * 3
        let fvc = lFvc + rFvc
        let arr : V2f[] = Array.zeroCreate fvc
        let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
        try
            let ptr = gc.AddrOfPinnedObject()
            Marshal.Copy(lMesh.pVertexData, ptr, nativeint lFvc * 8n)
            Marshal.Copy(rMesh.pVertexData, ptr + nativeint 8 * nativeint lFvc, nativeint rFvc * 8n)
        finally 
            gc.Free()

        let eyeIndex : int[] = Array.init fvc (fun vi -> if vi < lFvc then 0 else 1)

        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, arr |> Array.map (fun v -> V3f(2.0f, 2.0f, 1.0f) * V3f(v, 0.0f) - V3f(1,1,0)) :> System.Array
                    sEyeIndex, eyeIndex :> System.Array
                ]
        )
                
    let hmds() = system.AllDevices |> Seq.filter (fun d -> d.Type = VrDeviceType.Hmd)

    let getDistortionGeometry (gridSize : V2i) =
        
        let cnt = gridSize.X * gridSize.Y

        let scale = V2d.II / V2d(gridSize)


        let eyeIndex    : int[] = Array.zeroCreate (2 * cnt)
        let position    : V3f[] = Array.zeroCreate (2 * cnt)
        let rCoord      : V2f[] = Array.zeroCreate (2 * cnt)
        let gCoord      : V2f[] = Array.zeroCreate (2 * cnt)
        let bCoord      : V2f[] = Array.zeroCreate (2 * cnt)
                
        let mutable i = 0
        let mutable coords = DistortionCoordinates_t()

        for eye in [EVREye.Eye_Left; EVREye.Eye_Right] do
            let ei = int eye
            for y in 0 .. gridSize.Y - 1 do
                for x in 0 .. gridSize.X - 1 do
                    let ci = V2i(x,y)
                    let c = V2d ci * scale
                    let ndc = c * 2.0 - V2d.II

                    position.[i] <- V3f(ndc.X, ndc.Y, -1.0)
                    eyeIndex.[i] <- ei

                    if system.System.ComputeDistortion(eye, float32 c.X, float32 c.Y, &coords) then
                        rCoord.[i] <- V2f(coords.rfRed0, coords.rfRed1)
                        gCoord.[i] <- V2f(coords.rfGreen0, coords.rfGreen1)
                        bCoord.[i] <- V2f(coords.rfBlue0, coords.rfBlue1)
                    else
                        rCoord.[i] <- V2f.Zero
                        gCoord.[i] <- V2f.Zero
                        bCoord.[i] <- V2f.Zero

                    i <- i + 1

        let indices = Array.zeroCreate (6 * 2 * cnt)
        
        let jx = 1
        let jy = gridSize.X
        let mutable ti = 0
        let mutable offset = 0
        for eye in [EVREye.Eye_Left; EVREye.Eye_Right] do
            for y in 1 .. gridSize.Y - 1 do
                for x in 1 .. gridSize.X - 1 do
                    let i11 = offset + x + y * gridSize.X
                    let i01 = i11 - jx
                    let i10 = i11 - jy
                    let i00 = i10 - jx

                    indices.[ti + 0] <- i00
                    indices.[ti + 1] <- i10
                    indices.[ti + 2] <- i11

                    indices.[ti + 3] <- i00
                    indices.[ti + 4] <- i11
                    indices.[ti + 5] <- i01
                    ti <- ti + 6

            offset <- offset + cnt


        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = indices,
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, position :> Array
                    sEyeIndex, eyeIndex :> Array
                    sRCoord, rCoord :> Array
                    sGCoord, gCoord :> Array
                    sBCoord, bCoord :> Array
                ]
        )
            
    let getChaperone () =
        let mutable quad = Unchecked.defaultof<_>
        if OpenVR.Chaperone.GetPlayAreaRect(&quad) then
            let quad = 
                Polygon3d [|
                    V3d(quad.vCorners0.v0, quad.vCorners0.v2, -quad.vCorners0.v1)
                    V3d(quad.vCorners1.v0, quad.vCorners1.v2, -quad.vCorners1.v1)
                    V3d(quad.vCorners2.v0, quad.vCorners2.v2, -quad.vCorners2.v1)
                    V3d(quad.vCorners3.v0, quad.vCorners3.v2, -quad.vCorners3.v1)
                |]
            Some quad
        else
            None

    let compositor = OpenVR.Compositor
    let renderPoses = Array.zeroCreate (int OpenVR.k_unMaxTrackedDeviceCount)
    let gamePoses = Array.zeroCreate (int OpenVR.k_unMaxTrackedDeviceCount)


    [<VolatileField>]
    let mutable running = false
    let mutable isDisposed = false

    let check (str : string) (err : EVRCompositorError) =
        if err <> EVRCompositorError.None then
            Log.error "[OpenVR] %A: %s" err str
            //failwithf "[OpenVR] %A: %s" err str

    let depthRange = Range1d(0.15, 1000.0) |> AVal.init

    let projections =
        depthRange |> AVal.map (fun range ->
            let proj = system.System.GetProjectionMatrix(EVREye.Eye_Left, float32 range.Min, float32 range.Max)
            let lProj = VrTrafo.ofHmdMatrix44 proj

            let proj = system.System.GetProjectionMatrix(EVREye.Eye_Right, float32 range.Min, float32 range.Max)
            let rProj = VrTrafo.ofHmdMatrix44 proj
            [| lProj; rProj|]
        )

    let mutable desiredSize : option<V2i> = None
    
    let getDesiredSize() =
        match desiredSize with
        | Some s -> s
        | None -> 
            let mutable width = 0u
            let mutable height = 0u
            system.System.GetRecommendedRenderTargetSize(&width,&height)
            let s = adjustSize(V2i(int width, int height))
            let s = V2i(max 1 s.X, max 1 s.Y)
            desiredSize <- Some s
            s
       


    let view (handedness : Trafo3d) (t : Trafo3d) =
        //let vk = Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero)
        //let gl = Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, -V3d.OIO, V3d.Zero)
        let centerView = t.Inverse * handedness
        let lHeadToEye = system.System.GetEyeToHeadTransform(EVREye.Eye_Left) |> VrTrafo.ofHmdMatrix34 |> VrTrafo.inverse
        let rHeadToEye = system.System.GetEyeToHeadTransform(EVREye.Eye_Right) |> VrTrafo.ofHmdMatrix34 |> VrTrafo.inverse

        [|
            centerView * lHeadToEye
            centerView * rHeadToEye
        |]

    let mutable infos : VrRenderInfo[] = null

    let getInfos (handedness : Trafo3d) =
        if isNull infos then
            let res =
                hmds() |> Seq.toArray |> Array.map (fun hmd ->
                    {
                        framebufferSize = getDesiredSize()
                        viewTrafos = hmd.MotionState.Pose |> AVal.map (view handedness)  
                        projTrafos = projections
                    }
                )
            infos <- res
            res
        else
            infos

    let mutable backgroundColor = C4f.Black

    let eventQueue = System.Collections.Generic.Queue<VREvent_t>()

    let swProcessEvents = Stopwatch()
    let swWaitPoses = Stopwatch()
    let swUpdatePoses = Stopwatch()
    let swRender = Stopwatch()
    let swSubmit = Stopwatch()
    let swTotal = Stopwatch()
    let mutable frameCount = 0
    let mutable statFrameCount = 20
    let evtStatistics = EventSource<VrSystemStats>()

    let mutable textures = None

    static member EyeIndexSym = sEyeIndex
    static member RCoordSym = sRCoord
    static member GCoordSym = sGCoord
    static member BCoordSym = sBCoord

    abstract member Handedness : Trafo3d
    default x.Handedness = 
        // for gl use: Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, -V3d.OIO, V3d.Zero)
        Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero) // vk
    
    member x.Chaperone = getChaperone()

    member x.BackgroundColor
        with get() = backgroundColor
        and set c = backgroundColor <- c

    member x.DepthRange
        with get() = depthRange.Value
        and set r = transact (fun () -> depthRange.Value <- r)

    member x.GetVulkanInstanceExtensions() = 
        let b = System.Text.StringBuilder(4096, 4096)
        let len = compositor.GetVulkanInstanceExtensionsRequired(b, 4096u) 
        let str = b.ToString()
        str.Split(' ') |> Array.toList

    member x.GetVulkanDeviceExtensions(physicalDevice : nativeint) = 
        let b = System.Text.StringBuilder(4096, 4096)
        let len = compositor.GetVulkanDeviceExtensionsRequired(physicalDevice, b, 4096u) 
        let str = b.ToString()
        str.Split(' ') |> Array.toList

    member x.System = system
    member x.Compositor = compositor
    member x.DesiredSize = getDesiredSize()

    member x.Shutdown() =
        running <- false

    member x.Info = getInfos(x.Handedness).[0]

    member x.HiddenAreaMesh = hiddenAreaMesh

    member x.GetDistortionMesh (gridSize : V2i) = getDistortionGeometry gridSize


    abstract member GetRenderStats : unit -> VrRenderStats
    default x.GetRenderStats() =
        let t = swRender.MicroTime
        {
            Total   = t
            Clear   = MicroTime.Zero
            Render  = t
            Resolve = MicroTime.Zero
        }

    abstract member ResetRenderStats : unit -> unit
    default x.ResetRenderStats() =
        swRender.Reset()


    abstract member OnLoad : info : VrRenderInfo -> VrTexture * VrTexture
    abstract member Render : unit -> unit
    abstract member Release : unit -> unit

    abstract member ProcessEvent : VREvent_t -> unit
    default x.ProcessEvent _ = ()

    abstract member UpdatePoses : TrackedDevicePose_t[] -> unit
    default x.UpdatePoses _ = ()
    
    abstract member AfterSubmit : unit -> unit
    default x.AfterSubmit () = ()

    abstract member Use : action : (unit -> 'r) -> 'r
    default x.Use f = f()

    member x.StatisticsFrameCount
        with get() = statFrameCount
        and set v = statFrameCount <- v

    member x.Statistics = evtStatistics :> IEvent<_>

    member x.IsRunning with get() = running

    member x.UpdateFrame(lTex : VrTexture, rTex : VrTexture) =
        lock x (fun () ->
            swTotal.Start()
            swProcessEvents.Start()
            let events = system.ProcessEvents()
            swProcessEvents.Stop()

            if system.ShutdownRequested then
                x.Shutdown()
            else

                swWaitPoses.Start()
                let err = x.Use (fun () -> compositor.WaitGetPoses(renderPoses, gamePoses))
                swWaitPoses.Stop()

                if err = EVRCompositorError.None then
                    swUpdatePoses.Start()
                    // update all poses
                    transact (fun () ->
                        system.UpdatePoses(renderPoses)
                    )

                    x.UpdatePoses(renderPoses)
                    swUpdatePoses.Stop()

                    swProcessEvents.Start()
                    for evt in events do
                        x.ProcessEvent evt // user application event processing
                    swProcessEvents.Stop()
            
                    // render for all HMDs
                    for hmd in hmds() do
                                         
                        if hmd.MotionState.IsValid.GetValue() then
                            swRender.Start()
                            x.Render()
                            swRender.Stop()

                            swSubmit.Start()
                            x.Use(fun () ->
                                compositor.Submit(EVREye.Eye_Left, &lTex.Info, &lTex.Bounds, lTex.Flags) |> check "submit left"
                                //x.AfterSubmit()
                                compositor.Submit(EVREye.Eye_Right, &rTex.Info, &rTex.Bounds, rTex.Flags) |> check "submit right"
                                //x.AfterSubmit()
                            )
                            swSubmit.Stop()
                else
                    Log.error "[OpenVR] %A" err
            
                swTotal.Stop()
                frameCount <- frameCount + 1

                if frameCount >= statFrameCount then
                    let r = x.GetRenderStats()
                    x.ResetRenderStats()

                    let stats = 
                        {
                            ProcessEvents   = swProcessEvents.MicroTime / frameCount
                            WaitGetPoses    = swWaitPoses.MicroTime / frameCount
                            UpdatePoses     = swUpdatePoses.MicroTime / frameCount
                            Render          = 
                                { 
                                    Total   = r.Total / frameCount
                                    Clear   = r.Clear / frameCount
                                    Render  = r.Render / frameCount
                                    Resolve = r.Resolve / frameCount
                                }
                            Submit          = swSubmit.MicroTime / frameCount
                            Total           = swTotal.MicroTime / frameCount
                            FrameCount      = frameCount
                        }
                    
                    evtStatistics.Emit(stats)

                    swProcessEvents.Reset()
                    swWaitPoses.Reset()
                    swUpdatePoses.Reset()
                    swSubmit.Reset()
                    swTotal.Reset()
                    swRender.Reset()
                    frameCount <- 0
        )

    member x.Run () =
        if isDisposed then raise <| ObjectDisposedException("VrRenderer")
        running <- true

        while running do
            let (lTex, rTex) =
                match textures with
                | Some t -> t
                | None ->
                    let t = x.OnLoad ((getInfos x.Handedness).[0])
                    textures <- Some t
                    t
            x.UpdateFrame(lTex, rTex)

    member x.Dispose() =
        if not isDisposed then
            isDisposed <- true

            if running then x.Shutdown()

            Log.line "[OpenVR] Shutdown"
            
            OpenVR.Shutdown()
            match textures with
            | Some (lTex, rTex) -> 
                lTex.Dispose()
                rTex.Dispose()
                textures <- None
            | None ->
                ()
            x.Release()

    member x.AdjustSize 
        with get() = adjustSize
        and set v = 
            lock x (fun () ->
                adjustSize <- v
                infos <- null
                desiredSize <- None
                match textures with
                | Some (l,r) ->
                    l.Dispose()
                    r.Dispose()
                    textures <- None
                | None ->
                    ()
            )

    member x.Hmd = hmds() |> Seq.head

    interface IDisposable with
        member x.Dispose() = x.Dispose()

    new() = new VrRenderer(id, VrSystem(VrApplicationType.Scene))
    new(adjustSize) = new VrRenderer(adjustSize, VrSystem(VrApplicationType.Scene))
    new(system : VrSystem) = new VrRenderer(id, system)