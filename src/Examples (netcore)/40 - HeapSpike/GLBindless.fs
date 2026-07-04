namespace HeapSpike

// Bindless per-object textures on the GL backend via ARB_bindless_texture.
//
// GL has no descriptor indexing, so the Vulkan bindless path (unbounded sampler
// array + nonuniformEXT) doesn't apply. Instead: each texture is made RESIDENT
// (glGetTextureHandleARB + glMakeTextureHandleResident) and its uint64 handle is
// stored in an ordinary storage buffer. The heap routes a per-object texture
// INDEX through the arena (as on Vulkan); the fragment shader reads the handle
// `TexHandles[index]` and samples it with `texture(sampler2D(handle), uv)` — a
// plain GLSLIntrinsic, so FShade needs no sampler-model change and the aardvark
// GL backend needs no array-sampler support. One indirect multidraw, gl_DrawID
// per-draw routing (so no baseInstance), N distinct textures, no atlas.

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Application
open FSharp.Data.Adaptive
open FShade
open OpenTK.Graphics.OpenGL4

module GLBindless =

    // texture(sampler2D(handle), uv) — construct a sampler from a resident uint64
    // handle (ARB_bindless_texture) and sample it. Returns the value directly, so
    // FShade treats it as a normal intrinsic call (no first-class sampler needed).
    [<GLSLIntrinsic("texture(sampler2D({0}), {1})", "GL_ARB_bindless_texture", "GL_ARB_gpu_shader_int64")>]
    let private sampleHandle (h : uint64) (uv : V2f) : V4f = onlyInShaderCode "sampleHandle"

    [<AutoOpen>]
    module private Uni =
        type UniformScope with
            // resident texture handles, one per texture, addressed by per-object index
            member x.TexHandles : uint64[] = uniform?StorageBuffer?TexHandles

    module S =
        type V =
            { [<Position>]                                               pos : V4f
              [<Semantic("TexCoord")>]                                   tc  : V2f
              [<Semantic("TexId"); Interpolation(InterpolationMode.Flat)>] ti : int }
        let shade (v : V) =
            vertex {
                let m  : M44f = uniform?HeapModelTrafo
                let vp : M44f = uniform?ViewProjTrafo
                let ti : int  = uniform?HeapTexIndex
                return { v with pos = vp * (m * v.pos); tc = v.pos.XY + V2f(0.5f, 0.5f); ti = ti }
            }
        let frag (v : V) =
            fragment {
                let h = uniform.TexHandles.[v.ti]   // per-object index -> resident handle
                return sampleHandle h v.tc
            }

    let private mkTexture (i : int) : PixImage<byte> =
        let cols = [| C3b(230,60,60); C3b(60,200,60); C3b(60,120,230); C3b(230,200,40)
                      C3b(210,60,210); C3b(40,210,210); C3b(230,140,40); C3b(180,180,180) |]
        let col = cols.[i % cols.Length]
        let img = PixImage<byte>(Col.Format.RGBA, V2i(64, 64))
        img.GetMatrix<C4b>().SetByIndex(fun (idx : int64) ->
            let x = int idx % 64
            let y = int idx / 64
            if ((x / 8) + (y / 8)) % 2 = 0 then C4b(col) else C4b.White) |> ignore
        img

    let run () =
        Aardvark.Init()
        use app = new Aardvark.Application.Slim.OpenGlApplication(false)
        let runtime = app.Runtime

        let signature =
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8 ]
        let size = AVal.constant (V2i(1024, 1024))

        let texCount = 8
        // prepare textures on the GL runtime, then make their handles resident
        let backTex = Array.init texCount (fun i -> runtime.PrepareTexture(PixTexture2d(mkTexture i)))
        let handles =
            using runtime.ContextLock (fun _ ->
                backTex |> Array.map (fun t ->
                    let name = int t.Handle
                    GL.TextureParameter(name, TextureParameterName.TextureMinFilter, int TextureMinFilter.Linear)
                    GL.TextureParameter(name, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)
                    let h = GL.Arb.GetTextureHandle name
                    GL.Arb.MakeTextureHandleResident h
                    uint64 h))
        let handleBuf = runtime.CreateBuffer<uint64>(handles)
        let handleU = (AVal.constant (handleBuf :> IBuffer)) :> IAdaptiveValue

        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.7)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let view = CameraView.lookAt (V3d(0.0, -1.0, 1.0) * 13.0) V3d.Zero V3d.OOI |> CameraView.viewTrafo
        let proj = Frustum.perspective 70.0 0.1 5000.0 1.0 |> Frustum.projTrafo
        let viewProj = AVal.constant (view * proj) :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction S.shade; Effect.ofFunction S.frag ]

        let s = 8
        let inputs =
            Array.init (s*s) (fun i ->
                let p = V3d(float (i % s - s/2) * 1.4, float (i / s - s/2) * 1.4, 0.0)
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "HeapTexIndex",   (AVal.constant (i % texCount) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj
                    Symbol.Create "TexHandles",     handleU ]
                ro :> IRenderObject)
        let heap = Heap.ofRenderObjects (runtime.CreateHeapStorage()) (ASet.ofArray inputs)

        use task = runtime.CompileRender(signature, heap)
        let out = task |> RenderTask.renderToColor size
        out.Acquire()
        let img = out.GetValue().Download().AsPixImage<uint8>()
        let m = img.GetMatrix<C4b>()
        let mutable coverage = 0L
        let distinct = System.Collections.Generic.HashSet<int>()
        m.ForeachCoord(fun (p : V2l) ->
            let v = m.[p]
            if v.R <> 0uy || v.G <> 0uy || v.B <> 0uy then
                coverage <- coverage + 1L
                distinct.Add(int v.R / 32 * 64 + int v.G / 32 * 8 + int v.B / 32) |> ignore)
        out.Release()
        using runtime.ContextLock (fun _ -> for u in handles do GL.Arb.MakeTextureHandleNonResident(int64 u))

        Log.line "gl-bindless: %d cubes, %d textures -> %d bucket(s); coverage=%d, %d distinct colors" inputs.Length texCount Heap.lastBucketCount coverage distinct.Count
        let pass = coverage > 50000L && distinct.Count >= 6   // textured (many colors), not a single flat color
        if pass then Log.line "gl-bindless: PASS (ARB_bindless_texture per-object textures render on GL via the heap)"
        else Log.warn "gl-bindless: FAIL (coverage=%d distinctColors=%d)" coverage distinct.Count
        try img.SaveAsPng (System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gl-bindless.png")) with _ -> ()
        pass

    /// Windowed variant (GL backend) for a visual check of ARB_bindless textures.
    let runWin () =
        Aardvark.Init()
        let win = window { backend Backend.GL; display Display.Mono; debug false; samples 8 }
        let runtime = win.Runtime
        let texCount = 8
        let backTex = Array.init texCount (fun i -> runtime.PrepareTexture(PixTexture2d(mkTexture i)))
        let handles =
            using runtime.ContextLock (fun _ ->
                backTex |> Array.map (fun t ->
                    let name = int t.Handle
                    GL.TextureParameter(name, TextureParameterName.TextureMinFilter, int TextureMinFilter.Linear)
                    GL.TextureParameter(name, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)
                    let h = GL.Arb.GetTextureHandle name
                    GL.Arb.MakeTextureHandleResident h
                    uint64 h))
        let handleU = (AVal.constant (runtime.CreateBuffer<uint64>(handles) :> IBuffer)) :> IAdaptiveValue
        let g = (IndexedGeometryPrimitives.Box.solidBox (Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.7)) C4b.White).ToIndexed()
        let positions = g.IndexedAttributes.[DefaultSemantic.Positions] |> unbox<V3f[]>
        let normals   = g.IndexedAttributes.[DefaultSemantic.Normals]   |> unbox<V3f[]>
        let index     = g.IndexArray |> unbox<int[]>
        let bv (a : System.Array) t = BufferView(AVal.constant (ArrayBuffer(a) :> IBuffer), t)
        let vattrs = AttributeProvider.ofList [ DefaultSemantic.Positions, bv positions typeof<V3f>; DefaultSemantic.Normals, bv normals typeof<V3f> ]
        let viewProj = AVal.map2 (fun (v : Trafo3d[]) (p : Trafo3d[]) -> v.[0] * p.[0]) win.View win.Proj :> IAdaptiveValue
        let effect = Effect.compose [ Effect.ofFunction S.shade; Effect.ofFunction S.frag ]
        let s = 8
        let inputs =
            Array.init (s*s) (fun i ->
                let p = V3d(float (i % s - s/2) * 1.4, float (i / s - s/2) * 1.4, 0.0)
                let ro = RenderObject()
                ro.Surface   <- Surface.Effect effect
                ro.Mode      <- IndexedGeometryMode.TriangleList
                ro.VertexAttributes <- vattrs
                ro.Indices   <- Some (bv index typeof<int>)
                ro.DrawCalls <- DrawCalls.Direct (AVal.constant [| DrawCallInfo(FaceVertexCount = index.Length, InstanceCount = 1) |])
                ro.Uniforms  <- UniformProvider.ofList [
                    Symbol.Create "HeapModelTrafo", (AVal.constant ((Trafo3d.Translation p).Forward |> M44f.op_Explicit) :> IAdaptiveValue)
                    Symbol.Create "HeapTexIndex",   (AVal.constant (i % texCount) :> IAdaptiveValue)
                    Symbol.Create "ViewProjTrafo",  viewProj
                    Symbol.Create "TexHandles",     handleU ]
                ro :> IRenderObject)
        win.Scene <- Sg.renderObjectSet (Heap.ofRenderObjects (win.Runtime.CreateHeapStorage()) (ASet.ofArray inputs))
        Log.warn "gl-bindless window: %d cubes, %d ARB_bindless textures, GL backend, one indirect draw" inputs.Length texCount
        win.Run()
