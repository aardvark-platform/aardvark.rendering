namespace Aardvark.Rendering.GL

open System
open OpenTK.Graphics.OpenGL4
open System.Text.RegularExpressions
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering.GL

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


// profileMask: 
// GL_CONTEXT_CORE_PROFILE_BIT          1
// GL_CONTEXT_COMPATIBILITY_PROFILE_BIT 2
// contextFlags:
// GL_CONTEXT_FLAG_FORWARD_COMPATIBLE_BIT 1
// GL_CONTEXT_FLAG_DEBUG_BIT              2
// GL_CONTEXT_FLAG_ROBUST_ACCESS_BIT      4
// GL_CONTEXT_FLAG_NO_ERROR_BIT           8
type Driver = { device : GPUVendor; vendor : string; renderer : string; glsl : Version; version : Version; versionString : string; profileMask : int; contextFlags : int; extensions : Set<string> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Driver =

    let private versionRx = Regex @"^[ \t\r\n]*((?:[0-9]+\.)*[0-9]+)"

  
    let parseGLVersion (str : string) =
        let m = versionRx.Match str
        if m.Success then
            let str = m.Groups.[1].Value

            let v = 
                str.Split('.') 
                |> Array.map Int32.Parse 
                |> Array.toList
                //|> List.collect clean 

            match v with
            | [] -> failwithf "could not read version from: %A" str
            | [a] -> Version(a, 0, 0)
            | [a;b] -> Version(a, b, 0)
            | a::b::c::_ -> Version(a, b, c)
        else
            failwithf "could not read version from: %A" str

    let parseGLSLVersion (str : string) =
        let m = versionRx.Match str
        if m.Success then
            let str = m.Groups.[1].Value

            let v = 
                str.Split('.') 
                |> Array.map Int32.Parse 
                |> Array.toList

            match v with
            | [] -> failwithf "could not read version from: %A" str
            | [a] -> Version(a, 0, 0)
            | a::b::_ -> 
                if b > 9 then
                    Version(a, b/10, b%10)
                else 
                    Version(a, b, 0)
        else
            failwithf "could not read version from: %A" str


    let readInfo() =
        let vendor = GL.GetString(StringName.Vendor)  
        Report.Line(4, "[GL] vendor: {0}", vendor)
        let renderer = GL.GetString(StringName.Renderer)  
        Report.Line(4, "[GL] renderer: {0}", renderer)
        let versionStr = GL.GetString(StringName.Version)
        Report.Line(4, "[GL] version: {0}", versionStr)
        let version = 
            parseGLVersion versionStr

        let glslVersion = 
            let str = GL.GetString(StringName.ShadingLanguageVersion)
            Report.Line(4, "[GL] glsl: {0}", str)
            parseGLSLVersion str
        let profileMask =  GL.GetInteger(unbox<_> OpenTK.Graphics.OpenGL4.All.ContextProfileMask)
        Report.Line(4, "[GL] profileMask: {0}", profileMask)
        let contextFlags = GL.GetInteger(GetPName.ContextFlags)
        Report.Line(4, "[GL] contextFlags: {0}", contextFlags)

        let mutable extensions = Set.empty
        let extensionCount = GL.GetInteger(0x821d |> unbox<GetPName>) // GL_NUM_EXTENSIONS
        Report.Begin(4, "[GL] extensions")
        for i in 0..extensionCount-1 do
            let name = GL.GetString(StringNameIndexed.Extensions, i)
            Report.Line(4, "{0}", name)
            extensions <- Set.add name extensions
        Report.End(4) |> ignore

        let pat = (vendor + "_" + renderer).ToLower()
        let gpu = 
            if pat.Contains "nvidia" then Aardvark.Base.GPUVendor.nVidia
            elif pat.Contains "ati" || pat.Contains "amd" then Aardvark.Base.GPUVendor.AMD
            elif pat.Contains "intel" then Aardvark.Base.GPUVendor.Intel
            else Aardvark.Base.GPUVendor.Unknown



        { 
            device = gpu
            vendor = vendor
            renderer = renderer
            glsl = glslVersion
            version = version
            versionString = versionStr
            profileMask = profileMask
            contextFlags = contextFlags
            extensions = extensions 
        }


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

        /// Creates a single buffer using GL.CreateBuffers()
        static member CreateBuffer() =
            let mutable i = 0
            GL.CreateBuffers(1, &i)
            i

        /// Creates a single frame buffer using GL.CreateFramebuffers()
        static member CreateFramebuffer() =
            let mutable i = 0
            GL.CreateFramebuffers(1, &i)
            i

        /// Creates a single texture for the given target using GL.CreateTextures()
        static member CreateTexture(target : TextureTarget) =
            let mutable i = 0
            GL.CreateTextures(target, 1, &i)
            i