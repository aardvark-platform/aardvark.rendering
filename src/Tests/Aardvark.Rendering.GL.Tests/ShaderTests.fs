namespace Aardvark.Rendering.GL.Tests

open System
open NUnit.Framework
open FsUnit
open Aardvark.Rendering
open Aardvark.Rendering.GL
open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open FSharp.Data.Adaptive.Operators
open Aardvark.Application
open System.Diagnostics
open Aardvark.SceneGraph.Semantics
open Aardvark.Application.WinForms


module ShaderTests =

    let code = """#version 410

    uniform sampler2D sam;

    uniform mat3 m3;
    uniform Global {
        mat4 m4;
    };


    #ifdef Vertex
    layout(location = 0) in vec2 v2;
    layout(location = 1) in vec3 v3;
    layout(location = 2) in vec4 v4;
    void VS()
    {
        gl_Position = m4 * (vec4(v2, 1, 1) + vec4(m3 * v3, 1) + v4);
    }
    #endif


    #ifdef Fragment

    out vec4 color;
    void FS()
    {
        color = texture(sam, vec2(0,0));
    }
    #endif


    """

    let testSurface =
        BackendSurface(code, Dictionary.ofList [ShaderStage.Vertex, "VS"; ShaderStage.Fragment, "FS"]) :> ISurface

    [<Test>]
    let ``[Shader] prepare signature``() =
        use runtime = new Runtime()
        use ctx = new Context(runtime, fun () -> ContextHandleOpenTK.create false)
        runtime.Initialize(ctx)

        let signature =
            runtime.CreateFramebufferSignature [
                Symbol.Create "color", RenderbufferFormat.Rgba8
            ]

        let prep = runtime.PrepareSurface(signature, testSurface)

        //let uniforms = prep.Uniforms
        //let inputs = prep.Inputs
        //let outputs = prep.Outputs
        //uniforms |> List.sortBy fst |> should equal ["m3", typeof<M33f>; "m4", typeof<M44f>; "sam", typeof<ITexture>]
        //inputs |> List.sortBy fst |> should equal ["v2", typeof<V2f>; "v3", typeof<V3f>; "v4", typeof<V4f>]
        //outputs |> List.sortBy fst |> should equal ["color", typeof<V4f>]

        //prep.Samplers |> List.length |> should equal 0
        //prep.UniformGetters.Count |> should equal 0


        ()

