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

//type ExtensionSet(s : seq<string>) =
//    let extPrefix (e : string) =
//        let id = e.IndexOf '_'
//        if id > 0 then
//            e.Substring(0, id)
//        else
//            ""
//
//    let extName(e : string) =
//        let id = e.IndexOf '_'
//        if id > 0 then
//            e.Substring(id + 1)
//        else
//            e
//
//    let children = s |> Seq.groupBy extPrefix |> Seq.filter (fun (g,_) -> g <> "") |> Seq.map (fun (g,v) -> g, ExtensionSet (v |> Seq.map extName)) |> Map.ofSeq
//    let entries = s |> Seq.filter (fun s -> s |> extPrefix = "") |> Set.ofSeq
//
//    [<System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.RootHidden)>]
//    member x.Children = children
//
//    [<System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.RootHidden)>]
//    member x.Entries = children

type Driver = { device : GPUVendor; vendor : string; renderer : string; glsl : Version; version : Version; extensions : Set<string> }

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
            let name = GL.GetString(StringNameIndexed.Extensions, i)
            extensions <- Set.add name extensions


        let pat = (vendor + "_" + renderer).ToLower()
        let gpu = 
            if pat.Contains "nvidia" then Aardvark.Base.GPUVendor.nVidia
            elif pat.Contains "ati" || pat.Contains "amd" then Aardvark.Base.GPUVendor.AMD
            elif pat.Contains "intel" then Aardvark.Base.GPUVendor.Intel
            else Aardvark.Base.GPUVendor.Unknown



        { device = gpu; vendor = vendor; renderer = renderer; glsl = glslVersion; version = version; extensions = extensions }


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