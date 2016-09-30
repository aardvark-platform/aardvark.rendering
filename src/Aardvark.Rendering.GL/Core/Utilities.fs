namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open System.Text.RegularExpressions
open System.Runtime.InteropServices
open Aardvark.Base

[<AutoOpen>]
module TypeSizeExtensions =
    type Type with
        /// <summary>
        /// gets the byte-size of a type according to the
        /// OpenGL implementation.
        /// </summary>
        member x.GLSize =
            // TODO: improve for non-standard types (e.g. M23f)
            System.Runtime.InteropServices.Marshal.SizeOf(x)

[<AutoOpen>]
module UniformTypePatterns =
    open System.Collections.Generic
    open OpenTK.Graphics.OpenGL4

    let private samplerTypes =
        HashSet [
            ActiveUniformType.Sampler1D
            ActiveUniformType.Sampler1DArray 
            ActiveUniformType.Sampler1DArrayShadow 
            ActiveUniformType.Sampler1DShadow
            ActiveUniformType.Sampler2D
            ActiveUniformType.Sampler2DArray
            ActiveUniformType.Sampler2DArrayShadow
            ActiveUniformType.Sampler2DMultisample
            ActiveUniformType.Sampler2DMultisampleArray
            ActiveUniformType.Sampler2DRect
            ActiveUniformType.Sampler2DRectShadow
            ActiveUniformType.Sampler2DShadow
            ActiveUniformType.Sampler3D
            ActiveUniformType.SamplerCube
            ActiveUniformType.SamplerCubeMapArray
            ActiveUniformType.SamplerCubeMapArrayShadow
            ActiveUniformType.SamplerCubeShadow
            ActiveUniformType.SamplerBuffer
            ActiveUniformType.UnsignedIntSampler1D
            ActiveUniformType.UnsignedIntSampler1DArray
            ActiveUniformType.UnsignedIntSampler2D
            ActiveUniformType.UnsignedIntSampler2DArray
            ActiveUniformType.UnsignedIntSampler2DMultisample
            ActiveUniformType.UnsignedIntSampler2DMultisampleArray
            ActiveUniformType.UnsignedIntSampler2DRect
            ActiveUniformType.UnsignedIntSampler3D
            ActiveUniformType.UnsignedIntSamplerBuffer
            ActiveUniformType.UnsignedIntSamplerCube
            ActiveUniformType.UnsignedIntSamplerCubeMapArray
        ]

    let private imageTypes =
        HashSet [
            ActiveUniformType.Image1D
            ActiveUniformType.Image1DArray
            ActiveUniformType.Image2D
            ActiveUniformType.Image2DArray
            ActiveUniformType.Image2DMultisample
            ActiveUniformType.Image2DMultisampleArray
            ActiveUniformType.Image2DRect
            ActiveUniformType.Image3D
            ActiveUniformType.ImageBuffer
            ActiveUniformType.ImageCube
            ActiveUniformType.ImageCubeMapArray
            ActiveUniformType.IntImage1D
            ActiveUniformType.IntImage1DArray
            ActiveUniformType.IntImage2D
            ActiveUniformType.IntImage2DArray
            ActiveUniformType.IntImage2DMultisample
            ActiveUniformType.IntImage2DMultisampleArray
            ActiveUniformType.IntImage2DRect
            ActiveUniformType.IntImage3D
            ActiveUniformType.IntImageBuffer
            ActiveUniformType.UnsignedIntImage1D
            ActiveUniformType.UnsignedIntImage1DArray
            ActiveUniformType.UnsignedIntImage2D
            ActiveUniformType.UnsignedIntImage2DArray
            ActiveUniformType.UnsignedIntImage2DMultisample
            ActiveUniformType.UnsignedIntImage2DMultisampleArray
            ActiveUniformType.UnsignedIntImage2DRect
            ActiveUniformType.UnsignedIntImage3D
            ActiveUniformType.UnsignedIntImageBuffer
        ]

    let (|SamplerType|_|) (a : ActiveUniformType) =
        if samplerTypes.Contains a then
            Some ()
        else
            None   

    let (|ImageType|_|) (a : ActiveUniformType) =
        if imageTypes.Contains a then
            Some ()
        else
            None  

    let (|FloatMatrixType|_|) (a : ActiveUniformType) =
        if a = ActiveUniformType.FloatMat2 then Some (2,2)
        elif a = ActiveUniformType.FloatMat2x3 then Some (2,3)
        elif a = ActiveUniformType.FloatMat2x4 then Some (2,4)
        elif a = ActiveUniformType.FloatMat3 then Some (3,3)
        elif a = ActiveUniformType.FloatMat3x2 then Some (3,2)
        elif a = ActiveUniformType.FloatMat3x4 then Some (3,4)
        elif a = ActiveUniformType.FloatMat4 then Some (4,4)
        elif a = ActiveUniformType.FloatMat4x2 then Some (4,2)
        elif a = ActiveUniformType.FloatMat4x3 then Some (4,3)
        else None

    let (|FloatVectorType|_|) (a : ActiveUniformType) =
        if a = ActiveUniformType.Float then Some 1
        elif a = ActiveUniformType.FloatVec2 then Some 2
        elif a = ActiveUniformType.FloatVec3 then Some 3
        elif a = ActiveUniformType.FloatVec4 then Some 4
        else None

    let (|IntVectorType|_|) (a : ActiveUniformType) =
        if a = ActiveUniformType.Int then Some 1
        elif a = ActiveUniformType.IntVec2 then Some 2
        elif a = ActiveUniformType.IntVec3 then Some 3
        elif a = ActiveUniformType.IntVec4 then Some 4
        else None


    type ActiveUniformType with
        member x.SizeInBytes =
            match x with
                | ActiveUniformType.Bool -> 4
                | ActiveUniformType.BoolVec2 -> 8
                | ActiveUniformType.BoolVec3 -> 12
                | ActiveUniformType.BoolVec4 -> 16
                | ActiveUniformType.Float -> 4
                | ActiveUniformType.FloatMat2 -> 16
                | ActiveUniformType.FloatMat2x3 -> 32
                | ActiveUniformType.FloatMat2x4 -> 32
                | ActiveUniformType.FloatMat3 -> 48
                | ActiveUniformType.FloatMat3x2 -> 32
                | ActiveUniformType.FloatMat3x4 -> 48
                | ActiveUniformType.FloatMat4 -> 64
                | ActiveUniformType.FloatMat4x2 -> 32
                | ActiveUniformType.FloatMat4x3 -> 48
                | ActiveUniformType.FloatVec2 -> 8
                | ActiveUniformType.FloatVec3 -> 12
                | ActiveUniformType.FloatVec4 -> 16
                | ActiveUniformType.Int -> 4
//                | ActiveUniformType.IntSampler1D -> undefined	
//                | ActiveUniformType.IntSampler1DArray -> undefined	
//                | ActiveUniformType.IntSampler2D -> undefined	
//                | ActiveUniformType.IntSampler2DArray -> undefined	
//                | ActiveUniformType.IntSampler2DMultisample -> undefined	
//                | ActiveUniformType.IntSampler2DMultisampleArray -> undefined	
//                | ActiveUniformType.IntSampler2DRect -> undefined	
//                | ActiveUniformType.IntSampler3D -> undefined	
//                | ActiveUniformType.IntSamplerBuffer -> undefined	
//                | ActiveUniformType.IntSamplerCube -> undefined	
                | ActiveUniformType.IntVec2 -> 8
                | ActiveUniformType.IntVec3 -> 12
                | ActiveUniformType.IntVec4 -> 16
//                | ActiveUniformType.Sampler1D -> undefined	
//                | ActiveUniformType.Sampler1DArray -> undefined	
//                | ActiveUniformType.Sampler1DArrayShadow -> undefined	
//                | ActiveUniformType.Sampler1DShadow -> undefined	
//                | ActiveUniformType.Sampler2D -> undefined	
//                | ActiveUniformType.Sampler2DArray -> undefined	
//                | ActiveUniformType.Sampler2DArrayShadow -> undefined	
//                | ActiveUniformType.Sampler2DMultisample -> undefined	
//                | ActiveUniformType.Sampler2DMultisampleArray -> undefined	
//                | ActiveUniformType.Sampler2DRect -> undefined	
//                | ActiveUniformType.Sampler2DRectShadow -> undefined	
//                | ActiveUniformType.Sampler2DShadow -> undefined	
//                | ActiveUniformType.Sampler3D -> undefined	
//                | ActiveUniformType.SamplerBuffer -> undefined	
//                | ActiveUniformType.SamplerCube -> undefined	
//                | ActiveUniformType.SamplerCubeShadow -> undefined	
//                | ActiveUniformType.UnsignedIntSampler1D -> undefined	
//                | ActiveUniformType.UnsignedIntSampler1DArray -> undefined	
//                | ActiveUniformType.UnsignedIntSampler2D -> undefined	
//                | ActiveUniformType.UnsignedIntSampler2DArray -> undefined	
//                | ActiveUniformType.UnsignedIntSampler2DMultisample -> undefined	
//                | ActiveUniformType.UnsignedIntSampler2DMultisampleArray -> undefined	
//                | ActiveUniformType.UnsignedIntSampler2DRect -> undefined	
//                | ActiveUniformType.UnsignedIntSampler3D -> undefined	
//                | ActiveUniformType.UnsignedIntSamplerBuffer -> undefined	
//                | ActiveUniformType.UnsignedIntSamplerCube -> undefined	
                | ActiveUniformType.UnsignedInt -> 4
                | ActiveUniformType.UnsignedIntVec2 -> 8
                | ActiveUniformType.UnsignedIntVec3 -> 12
                | ActiveUniformType.UnsignedIntVec4 -> 16
                | _ -> failwithf "could not get size for type: %A" x


type ExtensionSet(s : seq<string>) =
    let extPrefix (e : string) =
        let id = e.IndexOf '_'
        if id > 0 then
            e.Substring(0, id)
        else
            ""

    let extName(e : string) =
        let id = e.IndexOf '_'
        if id > 0 then
            e.Substring(id + 1)
        else
            e

    let children = s |> Seq.groupBy extPrefix |> Seq.filter (fun (g,_) -> g <> "") |> Seq.map (fun (g,v) -> g, ExtensionSet (v |> Seq.map extName)) |> Map.ofSeq
    let entries = s |> Seq.filter (fun s -> s |> extPrefix = "") |> Set.ofSeq

    [<System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.RootHidden)>]
    member x.Children = children

    [<System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.RootHidden)>]
    member x.Entries = children

type Driver = { device : GPUVendor; vendor : string; renderer : string; glsl : Version; version : Version; extensions : ExtensionSet }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Driver =

    let private versionRx = Regex @"([0-9]+\.)*[0-9]+"

    let rec clean (v : int) =
        if v = 0 then 0
        elif v % 10 = 0 then clean (v / 10)
        else v

    let parseVersion (str : string) =
        let m = versionRx.Match str
        if m.Success then
            let str = m.Value

            let v = str.Split('.') |> Array.map Int32.Parse |> Array.map clean |> Array.map string |> String.concat "."

            Version.Parse v
        else
            failwithf "could not read version from: %A" str



    let readInfo() =
        let vendor = GL.GetString(StringName.Vendor)  
        let renderer = GL.GetString(StringName.Renderer)  
        let version = GL.GetString(StringName.Version) |> parseVersion
        let glslVersion = GL.GetString(StringName.ShadingLanguageVersion) |> parseVersion

        let mutable extensions = Set.empty
        let extensionCount = GL.GetInteger(0x821d |> unbox<GetPName>) // GL_NUM_EXTENSIONS
        for i in 0..extensionCount-1 do
            extensions <- Set.add (GL.GetString(StringNameIndexed.Extensions, i)) extensions


        let pat = (vendor + "_" + renderer).ToLower()
        let gpu = 
            if pat.Contains "nvidia" then Aardvark.Base.GPUVendor.nVidia
            elif pat.Contains "ati" || pat.Contains "amd" then Aardvark.Base.GPUVendor.AMD
            elif pat.Contains "intel" then Aardvark.Base.GPUVendor.Intel
            else Aardvark.Base.GPUVendor.Unknown



        { device = gpu; vendor = vendor; renderer = renderer; glsl = glslVersion; version = version; extensions = ExtensionSet extensions }


module MemoryManagementUtilities = 
    open System.Collections.Generic

    type FreeList<'k, 'v when 'k : comparison>() =
        static let comparer = { new IComparer<'k * HashSet<'v>> with member x.Compare((l,_), (r,_)) = compare l r }
        let sortedSet = SortedSetExt comparer
        let sets = Dictionary<'k, HashSet<'v>>()

        let tryGet (minimal : 'k) =
            let _, self, right = sortedSet.FindNeighbours((minimal, Unchecked.defaultof<_>))
    
            let fitting =
                if self.HasValue then Some self.Value
                elif right.HasValue then Some right.Value
                else None
        
            match fitting with
                | Some (k,container) -> 

                    if container.Count <= 0 then
                        raise <| ArgumentException "invalid memory manager state"

                    let any = container |> Seq.head
                    container.Remove any |> ignore

                    // if the container just got empty we remove it from the
                    // sorted set and the cache-dictionary
                    if container.Count = 0 then
                       sortedSet.Remove(k, container) |> ignore
                       sets.Remove(k) |> ignore

                    Some any

                | None -> None

        let insert (k : 'k) (v : 'v) =
            match sets.TryGetValue k with
                | (true, container) ->
                    container.Add(v) |> ignore
                | _ ->
                    let container = HashSet [v]
                    sortedSet.Add((k, container)) |> ignore
                    sets.[k] <- container

        let remove (k : 'k) (v : 'v) =
            let _, self, _ = sortedSet.FindNeighbours((k, Unchecked.defaultof<_>))
   
            if self.HasValue then
                let (_,container) = self.Value

                if container.Count <= 0 then
                    raise <| ArgumentException "invalid memory manager state"

                let res = container.Remove v

                // if the container just got empty we remove it from the
                // sorted set and the cache-dictionary
                if container.Count = 0 then
                    sortedSet.Remove(k, container) |> ignore
                    sets.Remove(k) |> ignore

                res
            else 
                false

        let contains (k : 'k) (v : 'v) =
            let _, self, _ = sortedSet.FindNeighbours((k, Unchecked.defaultof<_>))
   
            if self.HasValue then
                let (_,container) = self.Value
                container.Contains v
            else 
                false


        member x.TryGetGreaterOrEqual (minimal : 'k) = tryGet minimal
        member x.Insert (key : 'k, value : 'v) = insert key value
        member x.Remove (key : 'k, value : 'v) = remove key value
        member x.Contains (key : 'k, value : 'v) = contains key value
        member x.Clear() =
            sortedSet.Clear()
            sets.Clear()

[<AutoOpen>]
module GLExtensions =

    type GL with
        static member UnbindAllBuffers() =
            GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.ArrayBuffer, 0)
            GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.ElementArrayBuffer, 0)
            GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.CopyReadBuffer, 0)
            GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.CopyWriteBuffer, 0)
            GL.BindBuffer(OpenTK.Graphics.OpenGL4.BufferTarget.DrawIndirectBuffer, 0)
            for i in 0 .. 7 do
                GL.BindBufferBase(OpenTK.Graphics.OpenGL4.BufferRangeTarget.UniformBuffer, i, 0)