namespace Aardvark.SceneGraph


open System
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Base.Ag
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Aardvark.Base.Incremental


[<AutoOpen>]
module SgFSharp =

    module Sg =

        let uniform (name : string) (value : IMod<'a>) (sg : ISg) =
            Sg.UniformApplicator(name, value :> IMod, sg) :> ISg

        let trafo (m : IMod<Trafo3d>) (sg : ISg) =
            Sg.TrafoApplicator(m, sg) :> ISg

        let viewTrafo (m : IMod<Trafo3d>) (sg : ISg) =
            Sg.ViewTrafoApplicator(m, sg) :> ISg

        let projTrafo (m : IMod<Trafo3d>) (sg : ISg) =
            Sg.ProjectionTrafoApplicator(m, sg) :> ISg


        let surface (m : IMod<ISurface>) (sg : ISg) =
            Sg.SurfaceApplicator(m, sg) :> ISg

        let group (s : #seq<ISg>) =
            Sg.Group s

        let group' (s : #seq<ISg>) =
            Sg.Group s :> ISg

        let set (set : aset<ISg>) =
            Sg.Set(set) :> ISg

        let onOff (active : IMod<bool>) (sg : ISg) =
            Sg.OnOffNode(active, sg) :> ISg

        let texture (sem : Symbol) (tex : IMod<ITexture>) (sg : ISg) =
            Sg.TextureApplicator(sem, tex, sg) :> ISg

        let diffuseTexture (tex : IMod<ITexture>) (sg : ISg) = 
            texture DefaultSemantic.DiffuseColorTexture tex sg

        let diffuseTexture' (tex : ITexture) (sg : ISg) = 
            texture DefaultSemantic.DiffuseColorTexture (Mod.constant tex) sg

        let diffuseFileTexture' (path : string) (wantMipMaps : bool) (sg : ISg) = 
            texture DefaultSemantic.DiffuseColorTexture (Mod.constant (FileTexture(path, wantMipMaps) :> ITexture)) sg


        let scopeDependentTexture (sem : Symbol) (tex : Scope -> IMod<ITexture>) (sg : ISg) =
            Sg.UniformApplicator(new Providers.ScopeDependentUniformHolder([sem, fun s -> tex s :> IMod]), sg) :> ISg

        let scopeDependentDiffuseTexture (tex : Scope -> IMod<ITexture>) (sg : ISg) =
            scopeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg

        let runtimeDependentTexture (sem : Symbol) (tex : IRuntime -> IMod<ITexture>) (sg : ISg) =
            let cache = Dictionary<IRuntime, IMod<ITexture>>()
            let tex runtime =
                match cache.TryGetValue runtime with
                    | (true, v) -> v
                    | _ -> 
                        let v = tex runtime
                        cache.[runtime] <- v
                        v

            scopeDependentTexture sem (fun s -> s?Runtime |> tex) sg

        let runtimeDependentDiffuseTexture(tex : IRuntime -> IMod<ITexture>) (sg : ISg) =
            runtimeDependentTexture DefaultSemantic.DiffuseColorTexture tex sg

        let fillMode (m : IMod<FillMode>) (sg : ISg) =
            Sg.FillModeApplicator(m, sg) :> ISg
        
        let blendMode (m : IMod<BlendMode>) (sg : ISg) =
            Sg.BlendModeApplicator(m, sg) :> ISg

        let cullMode (m : IMod<CullMode>) (sg : ISg) =
            Sg.CullModeApplicator(m, sg) :> ISg

        let depthTest (m : IMod<DepthTestMode>) (sg : ISg) =
            Sg.DepthTestModeApplicator(m, sg) :> ISg

        let private arrayModCache = ConditionalWeakTable<IMod, IMod<Array>>()

        let private modOfArray (m : IMod<'a[]>) =
            match arrayModCache.TryGetValue (m :> IMod) with
                | (true, r) -> r
                | _ -> 
                    let r = m |> Mod.map (fun a -> a :> Array)
                    arrayModCache.Add(m, r)
                    r

        let vertexAttribute<'a when 'a : struct> (s : Symbol) (value : IMod<'a[]>) (sg : ISg) =
            let view = BufferView(value |> Mod.map (fun data -> ArrayBuffer(data) :> IBuffer), typeof<'a>)
            Sg.VertexAttributeApplicator(Map.ofList [s, view], Mod.constant sg) :> ISg

        let index<'a when 'a : struct> (value : IMod<'a[]>) (sg : ISg) =
            Sg.VertexIndexApplicator(modOfArray value, sg) :> ISg

        let vertexAttribute'<'a when 'a : struct> (s : Symbol) (value : 'a[]) (sg : ISg) =
            let view = BufferView(Mod.constant (ArrayBuffer(value :> Array) :> IBuffer), typeof<'a>)
            Sg.VertexAttributeApplicator(Map.ofList [s, view], Mod.constant sg) :> ISg

        let index'<'a when 'a : struct> (value : 'a[]) (sg : ISg) =
            Sg.VertexIndexApplicator(Mod.constant (value :> Array), sg) :> ISg


        let draw (mode : IndexedGeometryMode) =
            Sg.RenderNode(
                DrawCallInfo(
                    FirstInstance = 0,
                    InstanceCount = 1,
                    FirstIndex = 0,
                    FaceVertexCount = -1,
                    Mode = mode
                )
            ) :> ISg

        let ofIndexedGeometry (g : IndexedGeometry) =
            let attributes = 
                g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) -> 
                    let t = v.GetType().GetElementType()
                    let view = BufferView(Mod.constant (ArrayBuffer(v) :> IBuffer), t)

                    k, view
                ) |> Map.ofSeq
        

            let index, faceVertexCount =
                if g.IsIndexed then
                    g.IndexArray, g.IndexArray.Length
                else
                    null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

            let call = 
                DrawCallInfo(
                    FaceVertexCount = faceVertexCount,
                    FirstIndex = 0,
                    InstanceCount = 1,
                    FirstInstance = 0,
                    Mode = g.Mode
                )

            let sg = Sg.VertexAttributeApplicator(attributes, Sg.RenderNode(call)) :> ISg
            if index <> null then
                Sg.VertexIndexApplicator(Mod.constant index, sg) :> ISg
            else
                sg


        let instancedGeometry (trafos : IMod<Trafo3d[]>) (g : IndexedGeometry) =
            let vertexAttributes = 
                g.IndexedAttributes |> Seq.map (fun (KeyValue(k,v)) -> 
                    let t = v.GetType().GetElementType()
                    let view = BufferView(Mod.constant (ArrayBuffer(v) :> IBuffer), t)

                    k, view
                ) |> Map.ofSeq

            let index, faceVertexCount =
                if g.IsIndexed then
                    g.IndexArray, g.IndexArray.Length
                else
                    null, g.IndexedAttributes.[DefaultSemantic.Positions].Length

            let call = trafos |> Mod.map (fun t ->
                    DrawCallInfo(
                        FaceVertexCount = faceVertexCount,
                        FirstIndex = 0,
                        InstanceCount = t.Length,
                        FirstInstance = 0,
                        Mode = g.Mode
                    )
                )

            let sg = Sg.VertexAttributeApplicator(vertexAttributes, Sg.RenderNode(call)) :> ISg
        
            let sg =
                if index <> null then
                    Sg.VertexIndexApplicator(Mod.constant index, sg) :> ISg
                else
                    sg

            let m44Trafos = trafos |> Mod.map (fun a -> a |> Array.map (fun (t : Trafo3d) -> (M44f.op_Explicit t.Forward).Transposed) :> Array)
            let m44View = BufferView(m44Trafos |> Mod.map (fun a -> ArrayBuffer a :> IBuffer), typeof<M44f>)

            Sg.InstanceAttributeApplicator([DefaultSemantic.InstanceTrafo, m44View] |> Map.ofList, sg) :> ISg

        let pass (pass : uint64) (sg : ISg) = Sg.PassApplicator(Mod.constant pass, sg)

        let normalizeToAdaptive (box : Box3d) (this : ISg) =

            let getBoxScale (fromBox : Box3d) (toBox : Box3d) : float =
                let fromSize = fromBox.Size
                let toSize = toBox.Size
                let factor = toSize / fromSize

                let mutable smallest = factor.X

                if factor.Y < smallest then
                    smallest <- factor.Y
                if factor.Z < smallest then
                    smallest <- factor.Z

                smallest

            let bb = this?GlobalBoundingBox() : IMod<Box3d>

            printfn "normalizing from: %A" ( bb.GetValue() )

            let transformBox (sbox : Box3d) = Trafo3d.Translation(-sbox.Center) * Trafo3d.Scale(getBoxScale sbox box) * Trafo3d.Translation(box.Center)

            Sg.TrafoApplicator(Mod.map transformBox bb, this) :> ISg

        let normalizeAdaptive sg = sg |> normalizeToAdaptive ( Box3d( V3d(-1,-1,-1), V3d(1,1,1) ) ) 

        let loadAsync (sg : ISg) = Sg.AsyncLoadApplicator(Mod.constant sg) :> ISg


    type IndexedGeometry with
        member x.Sg =
            Sg.ofIndexedGeometry x
