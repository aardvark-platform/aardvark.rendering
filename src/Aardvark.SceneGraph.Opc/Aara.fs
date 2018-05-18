#nowarn "9"
#if INTERACTIVE
#r "../../bin/Debug/Aardvark.Base.dll"
#r "../../bin/Debug/Aardvark.Base.TypeProviders.dll"
#r "../../bin/Debug/Aardvark.Base.FSharp.dll"
#r "System.Xml.Linq.dll"
#else
namespace Aardvark.SceneGraph.Opc
#endif

open System
open System.IO
open System.Runtime.InteropServices

open Aardvark.Base
open Aardvark.Prinziple

[<AutoOpen>]
module Aara =

    let readerChars2String (f : Stream)  = 
        let cnt = f.ReadByte()
        
        let target = Array.zeroCreate cnt
        let read = f.Read(target,0,cnt)
        if cnt <> read then failwith ""
        System.Text.Encoding.Default.GetString target

    let loadRaw<'a  when 'a : (new : unit -> 'a) and 'a : struct and 'a :> ValueType> (elementCount : int) (f : Stream)  =
        let result = Array.zeroCreate<'a> elementCount
        let buffer = Array.zeroCreate<byte> (1 <<< 22)

        let gc = GCHandle.Alloc(result, GCHandleType.Pinned)
        try
            let mutable ptr = gc.AddrOfPinnedObject()
            let mutable remaining = sizeof<'a> * result.Length
            while remaining > 0 do
                let s = f.Read(buffer, 0, buffer.Length)
                Marshal.Copy(buffer, 0, ptr, s)
                ptr <- ptr + nativeint s
                remaining <- remaining - s
        finally
            gc.Free()
        result

    let loadRaw2<'a  when 'a : unmanaged> (elementCount : int) (f : Stream) : 'a[] =
        let target = Array.zeroCreate elementCount
        target.UnsafeCoercedApply<byte>(fun arr ->
            let r = f.Read(arr, 0, arr.Length)
            if r <> arr.Length then failwith "asdfj2"
        )
        target

    let loadFromStream<'a when 'a : unmanaged> (f : Stream) =
        let binaryReader = new BinaryReader(f,Text.Encoding.ASCII, true)
        let typeName = readerChars2String f
        let dimensions = f.ReadByte() |> int
        let sizes = [| for d in 0 .. dimensions - 1 do yield binaryReader.ReadInt32() |]

        let elementCount = sizes |> Array.fold ((*)) 1
        
        let result =
            if typeof<'a>.Name = typeName then
                loadRaw2<'a> elementCount f
            else
                match typeName with
                    | "V3d" -> f |> loadRaw<V3d> elementCount |> PrimitiveValueConverter.arrayConverter typeof<V3d>
                    | "V2d" -> f |> loadRaw<V2d> elementCount |> PrimitiveValueConverter.arrayConverter typeof<V2d>
                    | "double" -> f |> loadRaw<double> elementCount |> PrimitiveValueConverter.arrayConverter typeof<double>
                //    | "float" -> f |> loadRaw<float32> elementCount |> PrimitiveValueConverter.arrayConverter typeof<float32>
                    | _ -> failwith ""

        let dim =
            match sizes with
                | [| x |] -> V3i(x,1,1)
                | [| x; y |] -> V3i(x,y,1)
                | [| x; y; z |] -> V3i(x,y,z)
                | _ -> failwith ""

        Volume<'a>(result, dim)

    let fromFile<'a when 'a : unmanaged and 'a : (new : unit -> 'a) and 'a : struct and 'a :> ValueType> (s : string) =
        use fs = Prinziple.openRead s
        loadFromStream<'a> fs

    let inline isNan (v : V3f) = 
        v.X.IsNaN() || v.Y.IsNaN() || v.Z.IsNaN()

    let inline triangleIsNan (t:Triangle3d) =
        t.P0.AnyNaN || t.P1.AnyNaN || t.P2.AnyNaN

    let createIndex (vi : Matrix<V3f>) =
        let dx = vi.Info.DX
        let dy = vi.Info.DY
        let dxy = dx + dy
        let mutable arr = Array.zeroCreate (int (vi.SX - 1L) * int (vi.SY - 1L) * 6)
        let mutable cnt = 0
        
        vi.SubMatrix(V2l.Zero,vi.Size-V2l.II).ForeachXYIndex(fun x y index -> 
            let i00 = index
            let i10 = index + dy
            let i01 = index + dx
            let i11 = index + dxy
            
            arr.[cnt + 0] <- (int i00)
            arr.[cnt + 1] <- (int i10)
            arr.[cnt + 2] <- (int i11)
            arr.[cnt + 3] <- (int i00)
            arr.[cnt + 4] <- (int i11)
            arr.[cnt + 5] <- (int i01)
            cnt <- cnt + 6
        )
        Array.Resize(&arr, cnt)
        arr

    let createIndex2 (vi : Matrix<V3f>) (invalids : int64[])=

        let invalids = invalids |> Array.map (fun x -> (x, x)) |> HMap.ofArray

        let dx = vi.Info.DX
        let dy = vi.Info.DY
        let dxy = dx + dy
        let mutable arr = Array.zeroCreate (int (vi.SX - 1L) * int (vi.SY - 1L) * 6)
        let mutable cnt = 0
        
        vi.SubMatrix(V2l.Zero,vi.Size-V2l.II).ForeachXYIndex(fun x y index -> 

            let inv = invalids |> HMap.tryFind index

            match inv with
                | Some _ ->
                    arr.[cnt + 0] <- 0
                    arr.[cnt + 1] <- 0
                    arr.[cnt + 2] <- 0
                    arr.[cnt + 3] <- 0
                    arr.[cnt + 4] <- 0
                    arr.[cnt + 5] <- 0
                    cnt <- cnt + 6
                | None ->                    
                    let i00 = index
                    let i10 = index + dy
                    let i01 = index + dx
                    let i11 = index + dxy
                    
                    arr.[cnt + 0] <- (int i00)
                    arr.[cnt + 1] <- (int i10)
                    arr.[cnt + 2] <- (int i11)
                    arr.[cnt + 3] <- (int i00)
                    arr.[cnt + 4] <- (int i11)
                    arr.[cnt + 5] <- (int i01)
                    cnt <- cnt + 6
        )
        Array.Resize(&arr, cnt)
        arr
    
    // Patch Size to index array (faces with invalid points will be degenerated or skipped)
    let computeIndexArray (size : V2i) (degenerateInvalids : bool) (invalidPoints : int Set) =
      // vertex x/y to point index of face
      let getFaceIndices y x sizeX =
        let pntA = y * sizeX + x
        let pntB = (y + 1) * sizeX + x
        let pntC = pntA + 1
        let pntD = pntB + 1

        [| pntA; pntB; pntC;
           pntC; pntB; pntD |]
    
      // replace invalid faces with another array (invalidReplacement)
      let getFaceIndicesWReplacedInvalids invalidReplacement y x sizeX =
        let faceIndices = getFaceIndices y x sizeX
        if faceIndices |> Array.exists (fun i -> Set.contains i invalidPoints) then 
          invalidReplacement
        else 
          faceIndices
          
      // choose function to use
      let f = 
        match (invalidPoints.IsEmptyOrNull(), degenerateInvalids) with
        | (true, _)      -> getFaceIndices
        // skip faces with invalid points
        | (false, false) -> getFaceIndicesWReplacedInvalids Array.empty
        // replace invalid faces with degenerated face
        | (false, true)  ->
            // find first valid point
            let p = [| 0..(size.X * size.Y - 1) |] |> Array.find (fun i -> not (Set.contains i invalidPoints))
            getFaceIndicesWReplacedInvalids [| p; p; p; p; p; p |]
        
      // step through all vertices to get index-array per face      
      let indexArray = 
        [|
          for y in [| 0..(size.Y-2) |] do
            for x in [| 0..(size.X-2) |] do
              yield f y x size.X
        |]
    
      let invalidFaceCount = indexArray |> Array.filter (fun a -> a.IsEmpty()) |> Array.length
      if invalidFaceCount > 0 then
        Report.Line(5, "Invalid faces found: " + invalidFaceCount.ToString())

      indexArray |> Array.concat

    let getInvalidIndices (positions : V3d[]) =
      positions |> List.ofArray |> List.mapi (fun i x -> if x.AnyNaN then Some i else None) |> List.choose id
    
    // load triangles from aaraFile and transform them with matrix
    let loadTrianglesFromFile (aaraFile : string) (matrix : M44d) =
        let positions = aaraFile |> fromFile<V3f>

        let data = 
            positions.Data |> Array.map (fun x ->  x.ToV3d() |> matrix.TransformPos)

        let invalidIndices = getInvalidIndices data
        let index = computeIndexArray (positions.Size.XY.ToV2i()) true (Set.ofList invalidIndices)
              
        let triangles =             
            index 
                |> Seq.map(fun x -> data.[x])
                |> Seq.chunkBySize 3
                |> Seq.map(fun x -> Triangle3d(x))
                |> Seq.filter(fun x -> triangleIsNan x |> not) |> Seq.toArray

        triangles

    module Offset = 

      let loadRawWithOffset<'a when 'a : (new : unit -> 'a) and 'a : struct and 'a :> ValueType> (offset : int) (elementCount : int) (f : Stream) =
        let result = Array.zeroCreate<'a> elementCount
        let buffer = Array.zeroCreate<byte> (1 <<< 22)

        let gc = GCHandle.Alloc(result, GCHandleType.Pinned)
        try
            let mutable ptr = gc.AddrOfPinnedObject()
            let mutable remaining = sizeof<'a> * result.Length
            while remaining > 0 do
                let s = f.Read(buffer, offset, buffer.Length)
                Marshal.Copy(buffer, 0, ptr, s)
                ptr <- ptr + nativeint s
                remaining <- remaining - s
        finally
            gc.Free()
        result

      let loadRawColumnWithOffset<'a when 'a : (new : unit -> 'a) and 'a : struct and 'a :> ValueType> (offset : int) (elementCount : int) (f : Stream) = 
        let result = Array.zeroCreate<'a> elementCount
        let buffer = Array.zeroCreate<byte> sizeof<'a>
        let startPos = f.Position
        let mutable counter = 1
        let byteOffset = offset*sizeof<'a>

        let gc = GCHandle.Alloc(result, GCHandleType.Pinned)
        try
            let mutable ptr = gc.AddrOfPinnedObject()
            while counter <= elementCount do
                f.Seek((startPos + (int64(byteOffset * counter))), SeekOrigin.Begin) |> ignore
                let s = f.Read(buffer, 0, buffer.Length)
                Marshal.Copy(buffer, 0, ptr, s)
                ptr <- ptr + nativeint s
                counter <- counter+1                
        finally
            gc.Free()
        result

      let loadFromStreamWithOffset<'a when 'a : unmanaged> (offset : int) (size : int) (f : Stream) : 'a[]=
          let binaryReader = new BinaryReader(f,Text.Encoding.ASCII, true)
          let typeName = readerChars2String f
          let dimensions = f.ReadByte() |> int
          let sizes = [| for d in 0 .. dimensions - 1 do yield binaryReader.ReadInt32() |]
      
          let elementCount = sizes |> Array.fold ((*)) 1
          
          let result =
                  match typeName with
                      | "V3d" -> f |> loadRawWithOffset<V3d> offset elementCount |> PrimitiveValueConverter.arrayConverter typeof<V3d>
                      | "V3f" -> f |> loadRawWithOffset<V3f> offset elementCount |> PrimitiveValueConverter.arrayConverter typeof<V3f>
                      | "V2d" -> f |> loadRawWithOffset<V2d> offset elementCount |> PrimitiveValueConverter.arrayConverter typeof<V2d>
                      | "double" -> f |> loadRawWithOffset<double> offset elementCount |> PrimitiveValueConverter.arrayConverter typeof<double>
                      | "float" -> f |> loadRawWithOffset<float32> offset elementCount |> PrimitiveValueConverter.arrayConverter typeof<float32>
                      | _ -> failwith ("Aara.fs: No support for loading type " + typeName)
                      
      
          result
      
      let loadFromStreamColumnsWithOffset<'a when 'a : unmanaged> (offset : int) (size : int) (f : Stream) : 'a[]=
          f.Seek ((int64 0), SeekOrigin.Begin) |> ignore
          
          let binaryReader = new BinaryReader(f,Text.Encoding.ASCII, true)
          let typeName = readerChars2String f
          let dimensions = f.ReadByte() |> int
          let sizes = [| for d in 0 .. dimensions - 1 do yield binaryReader.ReadInt32() |]
      
          let elementCount = sizes |> Array.fold ((*)) 1
          
          let result =
                  match typeName with
                      | "V3d" -> f |> loadRawColumnWithOffset<V3d> offset sizes.[1] |> PrimitiveValueConverter.arrayConverter typeof<V3d>
                      | "V3f" -> f |> loadRawColumnWithOffset<V3f> offset sizes.[1] |> PrimitiveValueConverter.arrayConverter typeof<V3f>
                      | "V2d" -> f |> loadRawColumnWithOffset<V2d> offset sizes.[1] |> PrimitiveValueConverter.arrayConverter typeof<V2d>
                      | "double" -> f |> loadRawColumnWithOffset<double> offset sizes.[1] |> PrimitiveValueConverter.arrayConverter typeof<double>
                      | "float" -> f |> loadRawColumnWithOffset<float32> offset sizes.[1] |> PrimitiveValueConverter.arrayConverter typeof<float32>
                      | _ -> failwith ("Aara.fs: No support for loading type " + typeName)
                      
      
          result

      let fromFileWithOffsetAndSize<'a when 'a : unmanaged and 'a : (new : unit -> 'a) and 'a : struct and 'a :> ValueType> (offset : int) (size : int) (fileName : string) =
        let stream = File.OpenRead fileName
        loadFromStreamWithOffset<'a> offset size stream

      let fromFileColumnsWithOffsetAndSize<'a when 'a : unmanaged and 'a : (new : unit -> 'a) and 'a : struct and 'a :> ValueType> (offset : int) (size : int) (fileName : string) =
        let stream = File.OpenRead fileName
        loadFromStreamColumnsWithOffset<'a> offset size stream