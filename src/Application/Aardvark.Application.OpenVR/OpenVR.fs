namespace Aardvark.Application.OpenVR

open System
open Valve.VR
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Incremental
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.SceneGraph

#nowarn "9"

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
 
    let private all = List<ModRef<Flags>>()
    let private trafos = Dict<string, ModRef<Flags>>()
 
 
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
 
    let register (name : string) (m : IMod<Trafo3d>) =
        let flags = trafos.GetOrCreate(name, fun _ ->
            let m = Mod.init Flags.None
            all.Add m
            m
        )
        Mod.map2 applyFlags flags m
 
module UnhateTest =
    let run() =
        let trafo =
                Mod.init (Trafo3d.Translation(5.0, 6.0, 7.0))
                |> Unhate.register "a"
 
        while true do
            printfn "%A" (trafo.GetValue().Forward)
            Console.ReadLine() |> ignore
            Unhate.unhate()



[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Trafo =
    
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
    let isValid = Mod.init false
    let pose = Mod.init Trafo3d.Identity
    let angularVelocity = Mod.init V3d.Zero
    let velocity = Mod.init V3d.Zero

    member x.IsValid = isValid :> IMod<_>
    member x.Pose = pose :> IMod<_>
    member x.Velocity = velocity :> IMod<_>
    member x.AngularVelocity = angularVelocity :> IMod<_>

    member x.Update(newPose : byref<TrackedDevicePose_t>) =
        if newPose.bDeviceIsConnected && newPose.bPoseIsValid && newPose.eTrackingResult = ETrackingResult.Running_OK then
            let t = Trafo.ofHmdMatrix34 newPose.mDeviceToAbsoluteTracking
            isValid.Value <- true

//            let m = M44d.Identity
//            let m = M44d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero) * m * M44d.FromBasis(V3d.IOO, V3d.OOI, -V3d.OIO, V3d.Zero)

            pose.Value <- Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero) * t * Trafo3d.FromBasis(V3d.IOO, V3d.OOI, -V3d.OIO, V3d.Zero)
            angularVelocity.Value <- Trafo.angularVelocity newPose.vAngularVelocity
            velocity.Value <- Trafo.velocity newPose.vVelocity
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
        let indexBufferView = BufferView(Mod.constant (indexBuffer :> IBuffer), typeof<int>)

        //let vertexBuffer = NativeMemoryBuffer(NativePtr.toNativeInt m.VertexData, int m.VertexCount * sizeof<Vertex>) :> IBuffer
        let positions = BufferView(Mod.constant (ArrayBuffer(positions) :> IBuffer), typeof<V3f>)
        let normals = BufferView(Mod.constant (ArrayBuffer(normals) :> IBuffer), typeof<V3f>)
        let coords = BufferView(Mod.constant (ArrayBuffer(tc) :> IBuffer), typeof<V2f>)

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

    member internal x.Update(poses : TrackedDevicePose_t[]) =
        state.Update(&poses.[index])

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
    let touched = Mod.init false
    let pressed = Mod.init false
    let position = Mod.init None
    
    let touch = Event<unit>()
    let untouch = Event<unit>()
    let press = Event<unit>()
    let unpress = Event<unit>()

    override x.ToString() =
        sprintf "{ device = %d; axis = %d }" deviceIndex axisIndex

    member x.Touched = touched :> IMod<_>
    member x.Pressed = pressed :> IMod<_>
    member x.Position = position :> IMod<_>
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
            let i = Texture_t(eColorSpace = EColorSpace.Gamma, eType = ETextureType.OpenGL, handle = nativeint handle)
            let b = VRTextureBounds_t(uMin = 0.0f, uMax = 1.0f, vMin = 0.0f, vMax = 1.0f)
            new VrTexture(0n, i, EVRSubmitFlags.Submit_Default, b)
            
        static member OpenGL(handle : int, bounds : Box2d) =
            let i = Texture_t(eColorSpace = EColorSpace.Gamma, eType = ETextureType.OpenGL, handle = nativeint handle)
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
        viewTrafos          : IMod<Trafo3d[]>
        projTrafos          : IMod<Trafo3d[]>
    }

[<AbstractClass>]
type VrRenderer() =
    let system =
        let mutable err = EVRInitError.None
        let sys = OpenVR.Init(&err)
        if err <> EVRInitError.None then
            Log.error "[OpenVR] %s" (OpenVR.GetStringForHmdError err)
            failwithf "[OpenVR] %s" (OpenVR.GetStringForHmdError err)
        sys
    
    let devicesPerIndex =
        [|
            for i in 0u .. OpenVR.k_unMaxTrackedDeviceCount-1u do
                let deviceType = system.GetTrackedDeviceClass i
                if deviceType <> ETrackedDeviceClass.Invalid then
                    yield VrDevice(system, VrDeviceType.ofETrackedDeviceClass deviceType, int i) |> Some
                else
                    yield None
        |]

    let hiddenAreaMesh =
        let lMesh = system.GetHiddenAreaMesh(EVREye.Eye_Left, EHiddenAreaMeshType.k_eHiddenAreaMesh_Standard)
        let rMesh = system.GetHiddenAreaMesh(EVREye.Eye_Right, EHiddenAreaMeshType.k_eHiddenAreaMesh_Standard)

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
                    Symbol.Create "EyeIndex", eyeIndex :> System.Array
                ]
        )

    let devices = devicesPerIndex |> Array.choose id
        

    let hmds = devices |> Array.filter (fun d -> d.Type = VrDeviceType.Hmd)


    let compositor = OpenVR.Compositor
    let renderPoses = Array.zeroCreate (int OpenVR.k_unMaxTrackedDeviceCount)
    let gamePoses = Array.zeroCreate (int OpenVR.k_unMaxTrackedDeviceCount)


    [<VolatileField>]
    let mutable isAlive = true

    let check (str : string) (err : EVRCompositorError) =
        if err <> EVRCompositorError.None then
            Log.error "[OpenVR] %A: %s" err str
            //failwithf "[OpenVR] %A: %s" err str

    let depthRange = Range1d(0.15, 1000.0) |> Mod.init


    let projections =
        //let headToEye = system.GetEyeToHeadTransform(EVREye.Eye_Right) |> Trafo.ofHmdMatrix34 |> Trafo.inverse
        //let headToEye = Trafo3d.Scale(1.0, 1.0, -1.0) * headToEye
        depthRange |> Mod.map (fun range ->
            let proj = system.GetProjectionMatrix(EVREye.Eye_Left, float32 range.Min, float32 range.Max)
            let lProj = Trafo.ofHmdMatrix44 proj

            let proj = system.GetProjectionMatrix(EVREye.Eye_Right, float32 range.Min, float32 range.Max)
            let rProj = Trafo.ofHmdMatrix44 proj
            [| lProj; rProj|]
        )

    let desiredSize =
        let mutable width = 0u
        let mutable height = 0u
        system.GetRecommendedRenderTargetSize(&width,&height)
        V2i(int width, int height)


    let view (t : Trafo3d) =
        let lHeadToEye = system.GetEyeToHeadTransform(EVREye.Eye_Left) |> Trafo.ofHmdMatrix34 |> Trafo.inverse
        let rHeadToEye = system.GetEyeToHeadTransform(EVREye.Eye_Right) |> Trafo.ofHmdMatrix34 |> Trafo.inverse

        let view = t.Inverse * Trafo3d.FromBasis(V3d.IOO, -V3d.OOI, V3d.OIO, V3d.Zero)

        [|
            view * lHeadToEye
            view * rHeadToEye
        |]

    let infos =
        hmds |> Array.map (fun hmd ->
            {
                framebufferSize = desiredSize
                viewTrafos = hmd.MotionState.Pose |> Mod.map view  //CameraView.lookAt (V3d(3,4,5)) V3d.Zero V3d.OOI |> CameraView.viewTrafo |> Mod.constant //hmd.MotionState.Pose |> Mod.map Trafo.inverse |> Unhate.register "viewTrafo"
                projTrafos = projections
            }
        )

    let mutable backgroundColor = C4f.Black

    let eventQueue = System.Collections.Generic.Queue<VREvent_t>()

    member private x.ProcessEvents(events : System.Collections.Generic.Queue<VREvent_t>) =
        
        let mutable evt : VREvent_t = Unchecked.defaultof<VREvent_t>
        while system.PollNextEvent(&evt, uint32 sizeof<VREvent_t>) do
            
            events.Enqueue evt
            let id = evt.trackedDeviceIndex |> int
            if id >= 0 && id < devicesPerIndex.Length then
                match devicesPerIndex.[id] with
                    | Some device ->
                        device.Trigger(&evt)
                    | None ->
                        ()


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
    member x.DesiredSize = desiredSize

    member x.Shutdown() =
        isAlive <- false

    member x.Info = infos.[0]

    member x.HiddenAreaMesh = hiddenAreaMesh

    abstract member OnLoad : info : VrRenderInfo -> VrTexture * VrTexture
    abstract member Render : unit -> unit
    abstract member Release : unit -> unit

    abstract member ProcessEvent : VREvent_t -> unit
    default x.ProcessEvent _ = ()

    abstract member UpdatePoses : TrackedDevicePose_t[] -> unit
    default x.UpdatePoses _ = ()

    member x.Run () =
        if not isAlive then raise <| ObjectDisposedException("VrSystem")
        let (lTex, rTex) = x.OnLoad infos.[0] 

        

        while isAlive do
            x.ProcessEvents(eventQueue)
            let err = compositor.WaitGetPoses(renderPoses, gamePoses)
            if err = EVRCompositorError.None then

                // update all poses
                transact (fun () ->
                    for d in devices do d.Update(renderPoses)
                )


                x.UpdatePoses(renderPoses)
                while eventQueue.Count > 0 do
                    let evt = eventQueue.Dequeue()
                    x.ProcessEvent evt

            
                // render for all HMDs
                for i in 0 .. hmds.Length - 1 do
                    let hmd = hmds.[i]
                     
                    if hmd.MotionState.IsValid.GetValue() then
                        x.Render()

                        compositor.Submit(EVREye.Eye_Left, &lTex.Info, &lTex.Bounds, lTex.Flags) |> check "submit left"
                        compositor.Submit(EVREye.Eye_Right, &rTex.Info, &rTex.Bounds, rTex.Flags) |> check "submit right"

            else
                Log.error "[OpenVR] %A" err
        
        OpenVR.Shutdown()
        lTex.Dispose()
        rTex.Dispose()
        x.Release()

    member x.Hmd = hmds.[0]

    member x.Controllers = devices |> Array.filter (fun d -> d.Type = VrDeviceType.Controller)

