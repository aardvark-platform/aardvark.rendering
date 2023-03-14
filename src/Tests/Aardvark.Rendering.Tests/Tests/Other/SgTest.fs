namespace Aardvark.Rendering.Tests

open System
open System.Reflection
open System.Text.RegularExpressions
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Application
open FSharp.Data.Adaptive
open Expecto
open FShade

module ``SceneGraph Tests`` =

    [<AutoOpen>]
    module private Utilities =
        let semantics =
            [
                "RenderObjects",        typeof<aset<IRenderObject>>
                "GlobalBoundingBox",    typeof<aval<Box3d>>
                "LocalBoundingBox",     typeof<aval<Box3d>>
            ]

        let genericNameRx = Regex @"(?<name>.*?)´[0-9]+"
        let cleanName (name : string) =
            let m = genericNameRx.Match name
            if m.Success then m.Groups.["name"].Value
            else name

        let intrisicNames =
            Dict.ofList [
                typeof<byte>, "byte"
                typeof<int8>, "int8"
                typeof<uint16>, "uint16"
                typeof<int16>, "int16"
                typeof<int>, "int"
                typeof<uint32>, "uint32"
                typeof<int64>, "int64"
                typeof<uint64>, "uint64"
                typeof<obj>, "obj"
            ]

        let rec prettyName (t : Type) =
            match intrisicNames.TryGetValue t with
                | (true, n) -> n
                | _ ->
                    if t.IsArray then
                        sprintf "%s[]" (t.GetElementType() |> prettyName)
                    elif t.IsGenericType then
                        let args = t.GetGenericArguments() |> Seq.map prettyName |> String.concat ","
                        sprintf "%s<%s>" (cleanName t.Name) args
                    else
                        cleanName t.Name

    let checkCompleteness =
        test "General.Check Completeness" {
            IntrospectionProperties.CustomEntryAssembly <- Assembly.GetAssembly(typeof<ISg>)
            let sgTypes = Introspection.GetAllClassesImplementingInterface(typeof<ISg>)

            let sgModule = typeof<Sg.Set>.DeclaringType

            for (att, expected) in semantics do
                for t in sgTypes do
                    if t.DeclaringType = sgModule then
                        let hasRule = Ag.hasSynRule t expected att
                        Expect.isTrue hasRule <| sprintf "no semantic %A for type %s" att (prettyName t)
        }


    let private testOnActivation (countAfterPrepare : int) (countAfterDispose : int) (sgWithActivator : (ISg -> ISg) -> ISg) =
        use app = TestApplication.create Backend.Vulkan
        let runtime = app.Runtime

        use signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
            ]

        let mutable count = 0

        let sg =
            sgWithActivator (
                Sg.onActivation (fun _ ->
                    count <- count + 1
                    { new IDisposable with member x.Dispose() = count <- count - 1 }
                )
            )

        let prepared =
            sg.RenderObjects(Ag.Scope.Root)
            |> ASet.toAVal
            |> AVal.force
            |> HashSet.toList
            |> List.map (fun ro -> runtime.PrepareRenderObject(signature, ro))

        try
            Expect.equal count countAfterPrepare "unexpected count after preparing"
        with e ->
            prepared |> List.iter Disposable.dispose
            raise e

        prepared |> List.iter Disposable.dispose
        Expect.equal count countAfterDispose "unexpected count after disposing"

    let onActivationMultiRenderObject =
        test "Sg.OnActivation multiple RenderObjects" {

            let sg (withCountingActivator : ISg -> ISg) =
                Sg.ofList [
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Blue }
                    Sg.quad |> Sg.shader { do! DefaultSurfaces.constantColor C4f.Red }
                ]
                |> withCountingActivator

            sg |> testOnActivation 1 0
        }

    let delayModifySurface =
        test "Sg.Delay modify surface" {
            use app = TestApplication.create Backend.Vulkan
            let runtime = app.Runtime

            use signature =
                runtime.CreateFramebufferSignature [
                    DefaultSemantic.Colors, TextureFormat.Rgba8
                ]

            let effectAddBlue =
                let shader (v : Effects.Vertex) =
                    fragment {
                        return v.c + V4d.OOIO
                    }

                Effect.ofFunction shader

            use task =
                Sg.delay (fun scope ->
                    Sg.fullScreenQuad
                    |> Sg.effect [
                        match scope.Surface with
                        | Surface.FShadeSimple effect -> yield effect
                        | _ -> ()

                        yield effectAddBlue
                    ]
                )
                |> Sg.shader {
                    do! DefaultSurfaces.constantColor C4f.Red
                }
                |> Sg.compile runtime signature

            let size = AVal.constant <| V2i(128)
            let buffer = task |> RenderTask.renderToColor size
            buffer.Acquire()

            try
                let color = buffer.GetValue().Download().AsPixImage<uint8>()
                color |> PixImage.isColor [| 255uy; 0uy; 255uy; 255uy |]

            finally
                buffer.Release()
        }

    [<Tests>]
    let tests =
        testList "SceneGraph" [
            checkCompleteness
            onActivationMultiRenderObject
            delayModifySurface
        ]