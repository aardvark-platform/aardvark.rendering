// Learn more about F# at http://fsharp.org

open System
open Aardvark.Base
open Aardvark.Base.Rendering
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim


open FSharp.Data.Adaptive
open FSharp.Data.Traceable

[<AutoOpen>]
module PicklerExtensions =
    open MBrace.FsPickler
    open MBrace.FsPickler.Combinators
    
    open System.Reflection
    open System.Reflection.Emit


    let private tryUnifyTypes (decl : Type) (real : Type) =
        let assignment = System.Collections.Generic.Dictionary<Type, Type>()

        let rec recurse (decl : Type) (real : Type) =
            if decl = real then
                true

            elif decl.IsGenericParameter then
                match assignment.TryGetValue decl with
                    | (true, old) ->
                        if old.IsAssignableFrom real then 
                            true

                        elif real.IsAssignableFrom old then
                            assignment.[decl] <- real
                            true

                        else 
                            false
                    | _ ->
                        assignment.[decl] <- real
                        true
            
            elif decl.IsArray then
                if real.IsArray then
                    let de = decl.GetElementType()
                    let re = real.GetElementType()
                    recurse de re
                else
                    false

            elif decl.ContainsGenericParameters then
                let dgen = decl.GetGenericTypeDefinition()
                let rgen = 
                    if real.IsGenericType then real.GetGenericTypeDefinition()
                    else real

                if dgen = rgen then
                    let dargs = decl.GetGenericArguments()
                    let rargs = real.GetGenericArguments()
                    Array.forall2 recurse dargs rargs

                elif dgen.IsInterface then
                    let rface = real.GetInterface(dgen.FullName)
                    if isNull rface then
                        false
                    else
                        recurse decl rface

                elif not (isNull real.BaseType) then
                    recurse decl real.BaseType

                else
                    false

            elif decl.IsAssignableFrom real then
                true

            else
                false


        if recurse decl real then
            Some (assignment |> Dictionary.toSeq |> HashMap.ofSeq)
        else
            None

    type private PicklerRegistry(types : list<Type>, fallback : ICustomPicklerRegistry) =

        let picklerGen = typedefof<Pickler<_>>
        let allMeths = types |> List.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic) |> Array.toList)

        let upcastToPicker (mi : MethodInfo) =
            let meth = 
                DynamicMethod(
                    sprintf "upcasted.%s" mi.Name,
                    MethodAttributes.Public ||| MethodAttributes.Static,
                    CallingConventions.Standard,
                    typeof<Pickler>,
                    [| typeof<IPicklerResolver> |],
                    typeof<obj>,
                    true
                )
            let il = meth.GetILGenerator()

            il.Emit(OpCodes.Ldarg_0)
            il.Emit(OpCodes.Tailcall)
            il.EmitCall(OpCodes.Call, mi, null)
            il.Emit(OpCodes.Ret)
            let func = 
                meth.CreateDelegate(typeof<Func<IPicklerResolver, Pickler>>) 
                    |> unbox<Func<IPicklerResolver, Pickler>>        
            fun (r : IPicklerResolver) -> func.Invoke(r)

        let genericThings = 
            allMeths
            |> List.filter (fun mi -> mi.GetGenericArguments().Length > 0)
            |> List.choose (fun mi ->
                let ret = mi.ReturnType
                if ret.IsGenericType && ret.GetGenericTypeDefinition() = picklerGen && mi.GetParameters().Length = 1 then
                    let pickledType = ret.GetGenericArguments().[0]

                    let tryInstantiate (t : Type) =
                        match tryUnifyTypes pickledType t with
                            | Some ass ->
                                let targs = mi.GetGenericArguments() |> Array.map (fun a -> ass.[a])
                                let mi = mi.MakeGenericMethod targs
                                Some (upcastToPicker mi)
                                            
                            | None ->
                                None
                                        

                    Some tryInstantiate
                else
                    None
            )

        let nonGenericThings = 
            allMeths
            |> List.filter (fun mi -> mi.GetGenericArguments().Length = 0)
            |> List.choose (fun mi ->
                let ret = mi.ReturnType
                if ret.IsGenericType && ret.GetGenericTypeDefinition() = picklerGen && mi.GetParameters().Length = 1 then
                    let pickledType = ret.GetGenericArguments().[0]

                    let create = upcastToPicker mi
                    Some (pickledType, create)

                else
                    None
            )
            |> Dictionary.ofList
   
        member x.GetRegistration(t : Type) : CustomPicklerRegistration =
            if t.IsGenericType then
                match genericThings |> List.tryPick (fun a -> a t) with
                | Some r -> 
                    CustomPicklerRegistration.CustomPickler r
                | None ->
                    match nonGenericThings.TryGetValue t with   
                    | (true, r) -> CustomPicklerRegistration.CustomPickler r
                    | _ -> fallback.GetRegistration t
            else
                match nonGenericThings.TryGetValue t with   
                | (true, r) -> CustomPicklerRegistration.CustomPickler r
                | _ -> fallback.GetRegistration t

        interface ICustomPicklerRegistry with
            member x.GetRegistration(t : Type) = x.GetRegistration t

    //let private createRegistry (old : ICustomPicklerRegistry) = 
    //    PicklerRegistry([ typeof<CustomPicklers.AdaptivePicklers> ], old) :> ICustomPicklerRegistry

    let private installCustomPicklers(types : list<Type>) =
        let instance = MBrace.FsPickler.PicklerCache.Instance
        lock instance (fun () ->
            let t = instance.GetType()

            let registryField =
                t.GetFields(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance)
                |> Array.tryFind (fun f -> f.FieldType = typeof<ICustomPicklerRegistry>)

            match registryField with
            | Some field ->
                let old = 
                    match field.GetValue(instance) with
                    | null -> EmptyPicklerRegistry() :> ICustomPicklerRegistry
                    | :? ICustomPicklerRegistry as o -> o
                    | _ -> EmptyPicklerRegistry() :> ICustomPicklerRegistry
                let reg = PicklerRegistry(types, old) :> ICustomPicklerRegistry
                field.SetValue(instance, reg)
            | None ->
                Log.warn "cannot register custom picklers"
        )


    type FsPickler with
        static member AddCustomPicklers (types : list<Type>) =
            installCustomPicklers types



module AdaptivePicklers =
    open MBrace.FsPickler
    open MBrace.FsPickler.Combinators

    //[<CustomPicklerProvider>]
    type AdaptivePicklers() =
        static member HashSet (r : IPicklerResolver) : Pickler<HashSet<'a>> =
            let pint = r.Resolve<int>()
            let pv = r.Resolve<'a>()
            let parr = r.Resolve<'a[]>()
            let read (rs : ReadState) =
                let cnt = pint.Read rs "count"
                let elements = parr.Read rs "elements"
                HashSet<'a>.OfArray elements

            let write (ws : WriteState) (s : HashSet<'a>) =
                pint.Write ws "count" s.Count
                parr.Write ws "elements" (s.ToArray())

            let clone (cs : CloneState) (m : HashSet<'a>) =
                m |> HashSet.map (pv.Clone cs)

            let accept (vs : VisitState) (m : HashSet<'a>) =
                for v in m do pv.Accept vs v
            
            Pickler.FromPrimitives(read, write, clone, accept)

        static member CountingHashSet (r : IPicklerResolver) : Pickler<CountingHashSet<'a>> =
            failwith "CountingHashSet"

        static member HashSetDelta (r : IPicklerResolver) : Pickler<HashSetDelta<'a>> =
            failwith "HashSetDelta"

        static member IndexList (r : IPicklerResolver) : Pickler<IndexList<'a>> =
            failwith "IndexList"

        static member IndexListDelta (r : IPicklerResolver) : Pickler<IndexListDelta<'a>> =
            failwith "IndexListDelta"

        static member HashMap (r : IPicklerResolver) : Pickler<HashMap<'k, 'v>> =
            let pint = r.Resolve<int>()
            let parr = r.Resolve<array<'k * 'v>>()
            let kp = r.Resolve<'k>()
            let vp = r.Resolve<'v>()

            let read (rs : ReadState) =
                let cnt = pint.Read rs "count"
                let arr = parr.Read rs "items"
                HashMap<'k, 'v>.OfArray arr

            let write (ws : WriteState) (m : HashMap<'k, 'v>) =
                pint.Write ws "count" m.Count
                parr.Write ws "items" (m.ToArray())

            let clone (cs : CloneState) (m : HashMap<'k, 'v>) =
                m.ToSeqV()
                |> Seq.map (fun struct(k,v) -> struct(kp.Clone cs k, vp.Clone cs v))
                |> HashMap.OfSeqV

            let accept (vs : VisitState) (m : HashMap<'k, 'v>) =
                for (k,v) in m do kp.Accept vs k; vp.Accept vs v

            Pickler.FromPrimitives(read, write, clone, accept)

        static member HashMapDelta (r : IPicklerResolver) : Pickler<HashMapDelta<'k, 'v>> =
            failwith "HashMapDelta"

    let init() =
        FsPickler.AddCustomPicklers [
            typeof<AdaptivePicklers>
        ]

[<EntryPoint>]
let main argv =
    AdaptivePicklers.init()

    //let ser = MBrace.FsPickler.FsPickler.CreateBinarySerializer()
    //let input = HashMap.ofList [1,2;3,4]
    //let arr = ser.Pickle input

    //let test : HashMap<int, int> = ser.UnPickle arr
    //Log.warn "%A" (test = input)

    //System.Environment.Exit 0

    Ag.initialize()
    Aardvark.Init()

    use app = new VulkanApplication()
    use win = app.CreateGameWindow(8)

    // Given eye, target and sky vector we compute our initial camera pose
    let initialView = CameraView.LookAt(V3d(2.0,2.0,2.0), V3d.Zero, V3d.OOI)
    // the class Frustum describes camera frusta, which can be used to compute a projection matrix.
    let frustum = 
        // the frustum needs to depend on the window size (in oder to get proper aspect ratio)
        win.Sizes 
            // construct a standard perspective frustum (60 degrees horizontal field of view,
            // near plane 0.1, far plane 50.0 and aspect ratio x/y.
            |> AVal.map (fun s -> Frustum.perspective 60.0 0.1 50.0 (float s.X / float s.Y))

    // create a controlled camera using the window mouse and keyboard input devices
    // the window also provides a so called time mod, which serves as tick signal to create
    // animations - seealso: https://github.com/aardvark-platform/aardvark.docs/wiki/animation
    let cameraView = DefaultCameraController.control win.Mouse win.Keyboard win.Time initialView

    // create a quad using low level primitives (IndexedGeometry is our base type for specifying
    // geometries using vertices etc)
    let quadSg =
        let quad =
            IndexedGeometry(
                Mode = IndexedGeometryMode.TriangleList,
                IndexArray = ([|0;1;2; 0;2;3|] :> System.Array),
                IndexedAttributes =
                    SymDict.ofList [
                        DefaultSemantic.Positions,                  [| V3f(-1,-1,0); V3f(1,-1,0); V3f(1,1,0); V3f(-1,1,0) |] :> Array
                        DefaultSemantic.Normals,                    [| V3f.OOI; V3f.OOI; V3f.OOI; V3f.OOI |] :> Array
                        DefaultSemantic.DiffuseColorCoordinates,    [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] :> Array
                    ]
            )
                
        // create a scenegraph, given a IndexedGeometry instance...
        quad |> Sg.ofIndexedGeometry

    let sg =
        Sg.box' C4b.White Box3d.Unit 
            // here we use fshade to construct a shader: https://github.com/aardvark-platform/aardvark.docs/wiki/FShadeOverview
            |> Sg.effect [
                    DefaultSurfaces.trafo                 |> toEffect
                    DefaultSurfaces.constantColor C4f.Red |> toEffect
                    DefaultSurfaces.simpleLighting        |> toEffect
                ]
            // extract our viewTrafo from the dynamic cameraView and attach it to the scene graphs viewTrafo 
            |> Sg.viewTrafo (cameraView  |> AVal.map CameraView.viewTrafo )
            // compute a projection trafo, given the frustum contained in frustum
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo    )


    let renderTask = 
        // compile the scene graph into a render task
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    // assign the render task to our window...
    win.RenderTask <- renderTask

    win.Run()
    
    0 // return an integer exit code
