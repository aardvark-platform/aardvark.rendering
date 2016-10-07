namespace Aardvark.Base.Rendering

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Native

#nowarn "9"

type ComponentType =
    | Int8 = 0
    | Int16 = 1
    | Int32 = 2
    | Int64 = 3
    | UInt8 = 4
    | UInt16 = 5
    | UInt32 = 6
    | UInt64 = 7
    | Float16 = 8
    | Float32 = 9
    | Float64 = 10

type UniformKind =
    | Texture = 0
    | Value = 1


type private IRefCounted =
    abstract member AddRef : unit -> unit
    abstract member RemoveRef : unit -> unit

[<AutoOpen>]
module private Operators =
    let inline (!) (p : ^a) = (^a : (member get_Value : unit -> ^b) (p))
    let inline (:=) (p : ^a) (v : ^b) = (^a : (member set_Value : ^b -> unit) (p, v))

    type HeaderPtr(p : ptr) =
        static member Size = 128

        member x.Magic = p |> Ptr.cast<Guid>
        member x.Mode = p + 16n |> Ptr.cast<IndexedGeometryMode>
        member x.IndexArrayLength = p + 20n |> Ptr.cast<int>
        member x.AttributeCount = p + 24n |> Ptr.cast<int>
        member x.TextureCount = p + 28n |> Ptr.cast<int>

module private Utils =
    open System.Reflection
    open DevILSharp
    open Aardvark.Base.NativeTensors
    open Microsoft.FSharp.NativeInterop

    let devilLock =
        let fi = typeof<PixImage>.GetField("s_devilLock", BindingFlags.NonPublic ||| BindingFlags.Static)
        fi.GetValue(null)

    let magic = Guid("252fd451-fde4-4d2b-ab03-f247434a612f")

    let componentSize =
        LookupTable.lookupTable [
            ComponentType.Int8, 1
            ComponentType.Int16, 2
            ComponentType.Int32, 4
            ComponentType.Int64, 8 
            ComponentType.UInt8, 1
            ComponentType.UInt16, 2
            ComponentType.UInt32, 4 
            ComponentType.UInt64, 8
            ComponentType.Float16, 2
            ComponentType.Float32, 4
            ComponentType.Float64, 8
        ]

    let toViewType =
        LookupTable.lookupTable [
            (ComponentType.Int8, 1), typeof<int8>
            (ComponentType.Int16, 1), typeof<int16>

            (ComponentType.Int32, 1), typeof<int32>
            (ComponentType.Int32, 2), typeof<V2i>
            (ComponentType.Int32, 3), typeof<V3i>
            (ComponentType.Int32, 4), typeof<V4i>
            (ComponentType.Int32, 9), typeof<M33i>
            (ComponentType.Int32, 16), typeof<M44i>

            (ComponentType.Int64, 1), typeof<int64>
            (ComponentType.Int64, 2), typeof<V2l>
            (ComponentType.Int64, 3), typeof<V3l>
            (ComponentType.Int64, 4), typeof<V4l>
            (ComponentType.Int64, 9), typeof<M33l>
            (ComponentType.Int64, 16), typeof<M44l>

            
            (ComponentType.UInt8, 1), typeof<uint8>
            (ComponentType.UInt8, 3), typeof<C3b>
            (ComponentType.UInt8, 4), typeof<C4b>
            
            (ComponentType.UInt16, 1), typeof<uint16>
            (ComponentType.UInt16, 3), typeof<C3us>
            (ComponentType.UInt16, 4), typeof<C4us>
            
            (ComponentType.UInt32, 1), typeof<uint32>
            (ComponentType.UInt32, 3), typeof<C3ui>
            (ComponentType.UInt32, 4), typeof<C4ui>
            
            (ComponentType.UInt64, 1), typeof<uint64>


            (ComponentType.Float16, 1), typeof<float16>

            (ComponentType.Float32, 1), typeof<float32>
            (ComponentType.Float32, 2), typeof<V2f>
            (ComponentType.Float32, 3), typeof<V3f>
            (ComponentType.Float32, 4), typeof<V4f>
            (ComponentType.Float32, 9), typeof<M33f>
            (ComponentType.Float32, 12), typeof<M34f>
            (ComponentType.Float32, 16), typeof<M44f>

            (ComponentType.Float64, 1), typeof<float>
            (ComponentType.Float64, 2), typeof<V2d>
            (ComponentType.Float64, 3), typeof<V3d>
            (ComponentType.Float64, 4), typeof<V4d>
            (ComponentType.Float64, 9), typeof<M33d>
            (ComponentType.Float64, 12), typeof<M34d>
            (ComponentType.Float64, 16), typeof<M44d>
        ]

    let ofViewType =
        LookupTable.lookupTable [
            typeof<int8>, (ComponentType.Int8, 1)
            typeof<int16>, (ComponentType.Int16, 1)

            typeof<int32>, (ComponentType.Int32, 1)
            typeof<V2i>, (ComponentType.Int32, 2)
            typeof<V3i>, (ComponentType.Int32, 3)
            typeof<V4i>, (ComponentType.Int32, 4)
            typeof<M33i>, (ComponentType.Int32, 9)
            typeof<M44i>, (ComponentType.Int32, 16)

            typeof<int64>, (ComponentType.Int64, 1)
            typeof<V2l>, (ComponentType.Int64, 2)
            typeof<V3l>, (ComponentType.Int64, 3)
            typeof<V4l>, (ComponentType.Int64, 4)
            typeof<M33l>, (ComponentType.Int64, 9)
            typeof<M44l>, (ComponentType.Int64, 16)


            typeof<uint8>, (ComponentType.UInt8, 1)
            typeof<C3b>, (ComponentType.UInt8, 3)
            typeof<C4b>, (ComponentType.UInt8, 4)

            typeof<uint16>, (ComponentType.UInt16, 1)
            typeof<C3us>, (ComponentType.UInt16, 3)
            typeof<C4us>, (ComponentType.UInt16, 4)

            typeof<uint32>, (ComponentType.UInt32, 1)
            typeof<C3ui>, (ComponentType.UInt32, 3)
            typeof<C4ui>, (ComponentType.UInt32, 4)

            typeof<uint64>, (ComponentType.UInt64, 1)


            typeof<float16>, (ComponentType.Float16, 1)

            typeof<float32>, (ComponentType.Float32, 1)
            typeof<V2f>, (ComponentType.Float32, 2)
            typeof<V3f>, (ComponentType.Float32, 3)
            typeof<V4f>, (ComponentType.Float32, 4)
            typeof<M33f>, (ComponentType.Float32, 9)
            typeof<M34f>, (ComponentType.Float32, 12)
            typeof<M44f>, (ComponentType.Float32, 16)

            typeof<float>, (ComponentType.Float64, 1)
            typeof<V2d>, (ComponentType.Float64, 2)
            typeof<V3d>, (ComponentType.Float64, 3)
            typeof<V4d>, (ComponentType.Float64, 4)
            typeof<M33d>, (ComponentType.Float64, 9)
            typeof<M34d>, (ComponentType.Float64, 12)
            typeof<M44d>, (ComponentType.Float64, 16)
        ]

    let readInt (current : byref<ptr>) =
        let value = current.Read<int>()
        current <- current + 4n
        value

    let readEnum<'a> (current : byref<ptr>) =
        let value = current.Read<int>()
        current <- current + 4n
        value |> unbox<'a>

    let readString (current : byref<ptr>) =
        let nameLen = readInt &current
        let bytes = Array.zeroCreate nameLen
        current.Read(bytes, 0L, bytes.LongLength)
        current <- current + nativeint bytes.Length
        System.Text.Encoding.ASCII.GetString(bytes)   
    
    let toBuffer (size : int) (ptr : ptr) =
        { new INativeBuffer with
            member x.Pin() = ptr.Pointer
            member x.Unpin() = ()
            member x.Use f = f ptr.Pointer
            member x.SizeInBytes = size
        }

    let private compressedFormat =
        LookupTable.lookupTable [
            (ChannelFormat.RGB, ChannelType.UnsignedByte), (CompressedDataFormat.Dxt1, TextureFormat.CompressedRgbS3tcDxt1Ext)
            (ChannelFormat.RGBA, ChannelType.UnsignedByte), (CompressedDataFormat.Dxt5, TextureFormat.CompressedRgbaS3tcDxt5Ext)
            (ChannelFormat.BGR, ChannelType.UnsignedByte), (CompressedDataFormat.Dxt1, TextureFormat.CompressedRgbS3tcDxt1Ext)
            (ChannelFormat.BGRA, ChannelType.UnsignedByte), (CompressedDataFormat.Dxt5, TextureFormat.CompressedRgbaS3tcDxt5Ext)
         
            (ChannelFormat.Luminance, ChannelType.UnsignedShort), (CompressedDataFormat.DxtNoCompression, TextureFormat.R16ui)
            (ChannelFormat.LuminanceAlpha, ChannelType.UnsignedShort), (CompressedDataFormat.DxtNoCompression, TextureFormat.Rg16ui)
            (ChannelFormat.RGB, ChannelType.UnsignedShort), (CompressedDataFormat.DxtNoCompression, TextureFormat.Rgb16ui)
            (ChannelFormat.RGBA, ChannelType.UnsignedShort), (CompressedDataFormat.DxtNoCompression, TextureFormat.Rgba16ui)
            (ChannelFormat.BGR, ChannelType.UnsignedShort), (CompressedDataFormat.DxtNoCompression, TextureFormat.Rgb16ui)
            (ChannelFormat.BGRA, ChannelType.UnsignedShort), (CompressedDataFormat.DxtNoCompression, TextureFormat.Rgba16ui)
         

        ]

    let toTextureData (size : V2i) (sizeInBytes : int64) (ptr : ptr) =
        { new INativeTextureData with
            member x.Size = V3i(size.X, size.Y, 1)
            member x.SizeInBytes = sizeInBytes
            member x.Use f = f ptr.Pointer
        }
    
    let writeFileTexture (file : string) (writer : IO.BinaryWriter) =
        lock devilLock (fun () ->
            let img = IL.GenImage()

            IL.BindImage img
            IL.LoadImage file |> ignore
            ILU.FlipImage() |> ignore
            IL.SetInteger(IntName.Filter, int Filter.Linear)
            ILU.BuildMipmaps() |> ignore

            let levels = IL.GetInteger(IntName.ImageMipMapCount)
            let cfmt = IL.GetInteger(IntName.ImageFormat) |> unbox<ChannelFormat>
            let ct = IL.GetDataType()

            let compression, fmt = compressedFormat(cfmt, ct)

            writer.Write(levels)
            writer.Write(int fmt)

            for i in 0 .. levels - 1 do
                if i <> 0 then IL.ActiveMipmap 1 |> ignore

                let size = 
                    if compression = CompressedDataFormat.DxtNoCompression then IL.GetInteger(IntName.ImageSizeOfData)
                    else IL.GetDXTCData(0n, 0, compression)

                let w = IL.GetInteger(IntName.ImageWidth)
                let h = IL.GetInteger(IntName.ImageHeight)


                let lineSize = size / h
                let alignedLineSize =
                    if lineSize % 4 = 0 then lineSize
                    else (lineSize + 3) &&& ~~~3

                let dataSize = alignedLineSize * h

                let data : byte[] = Array.zeroCreate dataSize
                let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
                try 
                    if compression = CompressedDataFormat.DxtNoCompression then
                        if alignedLineSize = lineSize then
                            Marshal.Copy(IL.GetData(), gc.AddrOfPinnedObject(), dataSize)
                        else
                            let d = lineSize / w
                            let srcInfo =
                                VolumeInfo(
                                    0L, 
                                    V3l(int64 w, int64 h, int64 d),
                                    V3l(int64 d, int64 lineSize, 1L)
                                )

                            let dstInfo = 
                                VolumeInfo(
                                    0L, 
                                    srcInfo.Size, 
                                    V3l(srcInfo.DX, int64 alignedLineSize, srcInfo.DZ)
                                )

                            let vSrc = 
                                Aardvark.Base.NativeTensors.NativeVolume<byte>(
                                    NativePtr.ofNativeInt (IL.GetData()), 
                                    srcInfo
                                )

                            let vDst = 
                                Aardvark.Base.NativeTensors.NativeVolume<byte>(
                                    NativePtr.ofNativeInt (gc.AddrOfPinnedObject()), 
                                    dstInfo
                                )

                            NativeVolume.iter2 vSrc vDst (fun s d -> NativePtr.write d (NativePtr.read s))

                    else 
                        IL.GetDXTCData(gc.AddrOfPinnedObject(), dataSize, compression) |> ignore
                finally gc.Free()
                writer.Write(dataSize)
                writer.Write(w)
                writer.Write(h)
                writer.Write(data, 0, dataSize)

            IL.BindImage 0
            IL.DeleteImage img

        )



type GeometryStoreConfig =
    {
        compressTextures : bool
    }

[<ReferenceEquality; NoComparison>]
type Geometry =
    {
        mode             : IndexedGeometryMode
        faceVertexCount  : int
        vertexCount      : int
        indices          : Option<BufferView>
        uniforms         : Map<Symbol,IMod>
        vertexAttributes : Map<Symbol,BufferView>
        [<DefaultValue>]
        mutable dispose          : Option<unit -> unit>
    }

    member x.Dispose(disposing : bool) =
        if disposing then GC.SuppressFinalize x
        match x.dispose with
            | Some d -> d()
            | _ -> ()
   
    member x.Dispose() = x.Dispose(true)
    interface IDisposable with
        member x.Dispose() = x.Dispose(true)

    override x.Finalize() = x.Dispose(false)

    static member Load(mem : Memory) =
        
        let header = HeaderPtr mem
        let valid = mem.Size >= int64 HeaderPtr.Size && !header.Magic = Utils.magic

        if not valid then
            failwith "[Geometry] could not load pointer (invalid size or magic)"

        let mutable current = mem + nativeint HeaderPtr.Size
        
        let mutable vertexCount = Int32.MaxValue
        
        let ci = !header.IndexArrayLength
        let index = 
            if ci >= 0 then 
                let ptr = current
                let size = sizeof<int> * ci
                let b = Utils.toBuffer size ptr :> IBuffer |> Mod.constant
                current <- current + nativeint size
                BufferView(b, typeof<int>) |> Some
            else
                None

        let ca = !header.AttributeCount

        let attributes =
            Map.ofList [
                for ia in 0 .. ca-1 do
                    let name = Utils.readString &current
                    let elementType = Utils.readEnum<ComponentType> &current
                    let dim = Utils.readInt &current
                    let cnt = Utils.readInt &current
                    vertexCount <- min vertexCount cnt
                    let size = dim * Utils.componentSize elementType * cnt

                    let buffer = current |> Utils.toBuffer size :> IBuffer |> Mod.constant
                    let viewType = Utils.toViewType(elementType, dim)
                    yield Symbol.Create name, BufferView(buffer, viewType)

                    current <- current + nativeint size
            ]

        if vertexCount = Int32.MaxValue then vertexCount <- 0

        let ct = !header.TextureCount
        let uniforms =
            Map.ofList [
                for ia in 0 .. ct-1 do
                    let name = Utils.readString &current
                    let kind = Utils.readEnum<UniformKind> &current
                    match kind with
                        | UniformKind.Texture -> 
                            let levels = Utils.readInt &current
                            let fmt = Utils.readEnum<TextureFormat> &current

                            let data =
                                [|
                                    for i in 0 .. levels-1 do
                                        let s = Utils.readInt &current
                                        let w = Utils.readInt &current
                                        let h = Utils.readInt &current

                                        yield Utils.toTextureData (V2i(w,h)) (int64 s) current
                                        current <- current + nativeint s
                                |]


                            let texture =
                                { new INativeTexture with
                                    member x.Format = fmt
                                    member x.Dimension = TextureDimension.Texture2D
                                    member x.MipMapLevels = levels
                                    member x.Count = 1
                                    member x.WantMipMaps = false
                                    member x.Item
                                        with get(slice : int, level : int) = data.[level]
                                }

                            let t = texture :> ITexture |> Mod.constant :> IMod

                            yield Symbol.Create name, t

                        | _ ->
                            let typeName = Utils.readString &current
                            let t = Type.GetType(typeName, true)
                            let value = Marshal.PtrToStructure(current.Pointer, t)
                            current <- current + nativeint (Marshal.SizeOf t)
                            let tmod = typedefof<ConstantMod<_>>.MakeGenericType [| t |]
                            let ctor = tmod.GetConstructor [| t |]
                            let m = ctor.Invoke [| value |] |> unbox<IMod>

                            yield Symbol.Create name, m

                            ()
            ]

        let res = 
            {
                mode             = !header.Mode
                faceVertexCount  = if ci < 0 then vertexCount else ci
                vertexCount      = vertexCount
                indices          = index
                uniforms         = uniforms
                vertexAttributes = attributes
            }
        res.dispose <- Some (fun () -> mem.Dispose())
        res

    static member Load(file : string) =
        Memory.mapped file |> Geometry.Load

    static member Load(s : IO.Stream) =
        let arr = Array.zeroCreate (int s.Length)
        let mutable read = 0
        let mutable rem = arr.Length
        while rem > 0 do
            let r = s.Read(arr, read, rem)
            read <- read + r
            rem <- rem - r

        let mem = Memory.hglobal arr.LongLength
        mem.Write(arr, 0L, arr.LongLength)
        Geometry.Load(mem)

    member x.Save(ms : IO.Stream) =
        let w = new System.IO.BinaryWriter(ms, Text.Encoding.ASCII, true)

        let header = Array.zeroCreate HeaderPtr.Size
        let gc = GCHandle.Alloc(header, GCHandleType.Pinned)
        try
            let header = 
                let ptr = gc.AddrOfPinnedObject()
                HeaderPtr { new ptr() with 
                    member x.IsValid = true 
                    member x.Pointer = ptr
                }


            header.Magic := Utils.magic
            header.IndexArrayLength := (if Option.isNone x.indices then -1 else x.faceVertexCount)
            header.Mode := x.mode
            header.AttributeCount := x.vertexAttributes.Count
            header.TextureCount := x.uniforms.Count
        finally
            gc.Free()

        ms.Write(header, 0, header.Length)


        match x.indices with
            | Some i ->
                let v : int[] = BufferView.download 0 x.faceVertexCount i |> Mod.force |> PrimitiveValueConverter.arrayConverter i.ElementType
                let b = v.UnsafeCoerce<byte>()
                w.Write(b, 0, b.Length)

            | _ ->
                ()

        for (name, view) in Map.toSeq x.vertexAttributes do
            let name = string name

            w.Write(name.Length)
            w.Write(System.Text.Encoding.ASCII.GetBytes name)

            let elementType, dim = Utils.ofViewType view.ElementType
            w.Write(int elementType)
            w.Write(dim)
            w.Write(x.vertexCount)

            let arr = view |> BufferView.download 0 x.vertexCount |> Mod.force
            let arr = arr.UnsafeCoerce<byte>()
            w.Write(arr, 0, arr.Length)

        for (name, value) in Map.toSeq x.uniforms do
            let name = string name
            w.Write(name.Length)
            w.Write(Text.Encoding.ASCII.GetBytes name)

            match value with
                | :? IMod<ITexture> as t ->
                    let t = t |> Mod.force
                    match t with
                        | :? FileTexture as t -> 
                            w.Write(int UniformKind.Texture)
                            Utils.writeFileTexture t.FileName w
                        | _ ->
                            failwith "[Geometry] texture save not implemented"

                | _ ->
                    let v = value.GetValue(null)
                    let t = v.GetType()
                    let typeName = t.AssemblyQualifiedName

                    w.Write(int UniformKind.Value)
                    w.Write(typeName.Length)
                    w.Write(Text.Encoding.ASCII.GetBytes typeName)

                    let arr : byte[] = Array.zeroCreate (Marshal.SizeOf t)
                    let gc = GCHandle.Alloc(arr, GCHandleType.Pinned)
                    try Marshal.StructureToPtr(v, gc.AddrOfPinnedObject(), false)
                    finally gc.Free()
                    w.Write(arr, 0, arr.Length)



                    ()

    member x.Save(mem : Memory) =
        use ms = new IO.MemoryStream()
        x.Save(ms)
        let arr = ms.ToArray()
        mem.Clear(arr.LongLength)
        mem.Write(arr, 0L, arr.LongLength)

    member x.Save(file : string) =
        use s = new IO.FileStream(file, IO.FileMode.Create, IO.FileAccess.Write)
        x.Save(s)



module GeometryTest =
    open Aardvark.SceneGraph

    [<AutoOpen>]
    module ExtendedSg = 
        open Aardvark.Base.Ag
        open Aardvark.SceneGraph
        open Aardvark.SceneGraph.Semantics

        module Sg =
            type GeometryNode(g : Geometry) =
                interface ISg
                member x.Geometry = g

            let ofGeometry (g : Geometry) =
                GeometryNode(g) :> ISg

        [<Semantic>]
        type GeometrySem() =
        
            member x.RenderObjects(n : Sg.GeometryNode) =
                let ro = RenderObject.create()
                let g = n.Geometry

                let call = 
                    DrawCallInfo(
                        FaceVertexCount = g.faceVertexCount,
                        InstanceCount = 1,
                        FirstIndex = 0,
                        BaseVertex = 0,
                        FirstInstance = 0
                
                    )

                ro.Mode <- Mod.constant g.mode
                ro.DrawCallInfos <- Mod.constant [call]
                ro.Indices <- g.indices

                let u = ro.Uniforms
                let va = ro.VertexAttributes
                ro.Uniforms <-
                    { new IUniformProvider with
                        member x.Dispose() = u.Dispose()
                        member x.TryGetUniform(s,name) =
                            match Map.tryFind name g.uniforms with
                                | Some v -> Some v
                                | None -> u.TryGetUniform(s, name)
                    }

                ro.VertexAttributes <-
                    { new IAttributeProvider with
                        member x.All = Seq.empty
                        member x.Dispose() = va.Dispose()
                        member x.TryGetAttribute(name) =
                            match Map.tryFind name g.vertexAttributes with
                                | Some v -> Some v
                                | None -> va.TryGetAttribute(name)
                    }

                ASet.single (ro :> IRenderObject)



    [<Demo("Geometry Serialization")>]
    let run() =
        


        let file = Path.combine [Environment.GetFolderPath(Environment.SpecialFolder.Desktop); "bla.aard"]
        
        let mutable test = Unchecked.defaultof<_>
        if true ||  not (IO.File.Exists file) then
            let t = FileTexture(@"C:\Aardwork\ps_height_1k.png", true) :> ITexture |> Mod.constant :> IMod
            let geometry =
                {
                    mode             = IndexedGeometryMode.TriangleList
                    faceVertexCount  = 6
                    vertexCount      = 4
                    indices          = Some (BufferView.ofArray [| 0;1;2; 0;2;3 |])
                    uniforms =
                        Map.ofList [ 
                            Symbol.Create "ModelTrafo", Mod.constant (Trafo3d.Scale(10.0)) :> IMod 
                            DefaultSemantic.DiffuseColorTexture, t
                        ]
                    vertexAttributes = 
                        Map.ofList [ 
                            DefaultSemantic.Positions, BufferView.ofArray [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] 
                            DefaultSemantic.DiffuseColorCoordinates, BufferView.ofArray [| V2f.OO; V2f.IO; V2f.II; V2f.OI |] 
                        ]
                }

            geometry.Save(file)
            test <- geometry

        //let test = Geometry.Load(file)
        

        test
            |> Sg.ofGeometry
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.diffuseTexture |> toEffect
            ]
            



//
//[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
//module Geometry =
//    
//    let load (ptr : ptr)


//type NativeGeometry (mem : Memory) =
//    let header = HeaderPtr mem
//    let mutable pData = ptr.Null
//    let mutable pIndexArray = ptr.Null
//    let pAttributes = Dictionary.empty
//    let pTextures = Dictionary.empty
//
//    let exists = mem.Size >= int64 HeaderPtr.Size && !header.Magic = magic
//
//    let clear() =
//        mem.Clear(int64 HeaderPtr.Size)
//        header.Magic := magic
//        header.Mode := IndexedGeometryMode.TriangleList
//        header.IndexArrayLength := -1
//        header.AttributeCount := 0
//        header.TextureCount := 0
//
//    let initPointers() =
//        pAttributes.Clear()
//        pTextures.Clear()
//        pData <- mem + nativeint HeaderPtr.Size
//        if not exists then 
//            clear()
//            pIndexArray <- ptr.Null
//        else
//            let ci = !header.IndexArrayLength
//            let mutable current = pData
//
//            if ci >= 0 then 
//                pIndexArray <- current
//                current <- current + nativeint (sizeof<int> * ci)
//            else
//                pIndexArray <- ptr.Null
//
//            let ca = !header.AttributeCount
//            for ia in 0 .. ca-1 do
//                let name = Utils.readString &current
//                let elementType = Utils.readEnum<ComponentType> &current
//                let dim = Utils.readInt &current
//                let cnt = Utils.readInt &current
//                let size = dim * Utils.sizeof elementType * cnt
//
//                let ptr =
//                    let p = current
//                    { new sizedptr() with 
//                        member x.IsValid = p.IsValid
//                        member x.Pointer = p.Pointer 
//                        member x.Size = int64 size
//                    }
//
//                pAttributes.[name] <- { 
//                    name            = name
//                    elementType     = elementType
//                    dimension       = dim
//                    data            = ptr     
//                    count           = cnt 
//                }
//
//                current <- current + nativeint size
//
//
//            let ct = !header.TextureCount
//            for it in 0 .. ct-1 do
//                let name = Utils.readString &current
//                let levels = Utils.readInt &current
//                let fmt = Utils.readEnum<TextureFormat> &current
//                ()
//
//    do initPointers()
//
//    interface IDisposable with
//        member x.Dispose() = mem.Dispose()
//
//    interface IAttributeProvider with
//        member x.All = Seq.empty
//        member x.TryGetAttribute(name : Symbol) =
//            match pAttributes.TryGetValue (string name) with
//                | (true, att) ->
//                    let b = Utils.toBuffer att.data :> IBuffer |> Mod.constant
//                    let t = Utils.toViewType(att.elementType, att.dimension)
//                    BufferView(b, t) |> Some
//                | _ ->
//                    None
//    
// 


        


    




    




