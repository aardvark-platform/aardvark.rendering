namespace Scratch

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.IO
open Microsoft.FSharp.NativeInterop
open Aardvark.Base

#nowarn "9"

type ICompressedTexture =
    abstract member Format : TextureFormat
    abstract member MipMapLevels : int
    abstract member Size : V2i
    abstract member UseLevel : level : int * action : (V2i -> nativeint -> nativeint -> 'r) -> 'r


module DDS =

    let Magic = 0x20534444u

    [<StructLayout(LayoutKind.Explicit, Size = 44)>]
    type uint32_11 =
        struct
            [<FieldOffset(0)>]
            val mutable public First : uint32

        end

    [<StructLayout(LayoutKind.Sequential)>]
    type Caps =
        struct
            val mutable public E0 : uint32
            val mutable public E1 : uint32
            val mutable public E2 : uint32
            val mutable public E3 : uint32

        end

    [<Flags>]
    type Flags =
        | Caps = 0x1
        | Height = 0x2
        | Width = 0x4
        | Pitch = 0x8
        | PixelFormat = 0x1000
        | MipMapCount = 0x20000
        | LinearSize = 0x80000
        | Depth = 0x800000
        
    [<Flags>]
    type FormatFlags =
        | AlphaPixels = 0x1
        | Alpha = 0x2
        | FourCC = 0x4
        | Rgb = 0x40
        | Yuv = 0x200
        | Luminance = 0x20000
        
    [<StructLayout(LayoutKind.Sequential); StructuredFormatDisplay("{String}")>]
    type FourCC =
        struct
            val mutable public C0 : byte
            val mutable public C1 : byte
            val mutable public C2 : byte
            val mutable public C3 : byte

            member x.Length =
                if x.C0 = 0uy then 0
                elif x.C1 = 0uy then 1
                elif x.C2 = 0uy then 2
                elif x.C3 = 0uy then 3
                else 4
                

            member x.String =
                System.String([| char x.C0; char x.C1; char x.C2; char x.C3 |], 0, x.Length)
                
            override x.ToString() = x.String


            new (str : string) =
                let bytes = System.Text.Encoding.ASCII.GetBytes(str)

                if bytes.Length = 0 then { C0 = 0uy; C1 = 0uy; C2 = 0uy; C3 = 0uy }
                elif bytes.Length = 1 then { C0 = bytes.[0]; C1 = 0uy; C2 = 0uy; C3 = 0uy }
                elif bytes.Length = 2 then { C0 = bytes.[0]; C1 = bytes.[1]; C2 = 0uy; C3 = 0uy }
                elif bytes.Length = 3 then { C0 = bytes.[0]; C1 = bytes.[1]; C2 = bytes.[2]; C3 = 0uy }
                else { C0 = bytes.[0]; C1 = bytes.[1]; C2 = bytes.[2]; C3 = bytes.[3] }

        end

    [<StructLayout(LayoutKind.Sequential)>]
    type PixelFormat =
        struct
            /// structure size (32)
            val mutable public Size : uint32

            /// flags to indicate which members contain valid data
            val mutable public Flags : FormatFlags

            /// four characters 
            val mutable public FourCC : FourCC


            val mutable public RgbBitCount : uint32
            val mutable public RMask : uint32
            val mutable public GMask : uint32
            val mutable public BMask : uint32
            val mutable public AMask : uint32
        end
        
    [<StructLayout(LayoutKind.Sequential)>]
    type DDSHeader =
        struct
            /// size of the header (124)
            val mutable public Size         : uint32

            /// flags to indicate which members contain valid data
            val mutable public Flags        : Flags

            /// height in pixels
            val mutable public Height       : uint32

            /// width in pixels
            val mutable public Width        : uint32

            /// number of bytes per line
            val mutable public PitchOrSize  : uint32

            /// depth in pixels (for volumes)
            val mutable public Depth        : uint32

            /// number of mipmap levels
            val mutable public MipMapCount  : uint32

            /// reserved
            val mutable public Reserved     : uint32_11

            /// the pixel format
            val mutable public Format       : PixelFormat
            val mutable public Caps         : Caps
            val mutable public Reserved2    : uint32
        end
    
    [<StructLayout(LayoutKind.Sequential)>]
    type DX10Header =
        struct
            val mutable public dxgiFormat : uint32
            val mutable public dimension : uint32
            val mutable public miscFlag : uint32
            val mutable public arraySize : uint32
            val mutable public miscFlag2 : uint32
        end

    let inline private read<'a when 'a : unmanaged> (ptr : nativeint) : 'a =
        NativePtr.read (NativePtr.ofNativeInt ptr)


    type Header =
        {
            ddsHeader           : DDSHeader
            dxHeader            : Option<DX10Header>
            format              : TextureFormat
            dataRanges          : Range1l[]
            isCompressed        : bool
            blockSizeInBytes    : int64
        }

        member x.mipMapLevels = x.dataRanges.Length

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Header =
        let private ceilDiv (v : uint32) (d : uint32) =
            if v % d = 0u then v / d
            else 1u + v / d

        type BinaryReader with 
            member x.ReadFourCC() =
                let mutable res = Unchecked.defaultof<FourCC>
                res.C0 <- x.ReadByte()
                res.C1 <- x.ReadByte()
                res.C2 <- x.ReadByte()
                res.C3 <- x.ReadByte()
                res


            member x.ReadPixelFormat() =
                let mutable res = PixelFormat()
                res.Size <- x.ReadUInt32()
                res.Flags <- unbox (x.ReadInt32())
                res.FourCC <- x.ReadFourCC()
                res.RgbBitCount <- x.ReadUInt32()
                res.RMask <- x.ReadUInt32()
                res.GMask <- x.ReadUInt32()
                res.BMask <- x.ReadUInt32()
                res.AMask <- x.ReadUInt32()
                res

            member x.ReadCaps() =
                let mutable caps = Unchecked.defaultof<Caps>
                caps.E0 <- x.ReadUInt32()
                caps.E1 <- x.ReadUInt32()
                caps.E2 <- x.ReadUInt32()
                caps.E3 <- x.ReadUInt32()
                caps

            member x.ReadDDSHeader() =
                let mutable header = DDSHeader()
                
                header.Size <- x.ReadUInt32()
                header.Flags <- unbox (x.ReadInt32())
                header.Height <- x.ReadUInt32()
                header.Width <- x.ReadUInt32()
                header.PitchOrSize <- x.ReadUInt32()
                header.Depth <- x.ReadUInt32()
                header.MipMapCount <- x.ReadUInt32()
                // reserved
                x.BaseStream.Seek(44L, SeekOrigin.Current) |> ignore
                header.Format <- x.ReadPixelFormat()
                header.Caps <- x.ReadCaps()
                header.Reserved2 <- x.ReadUInt32()

                header

            member x.ReadDX10Header() =
                let mutable header = Unchecked.defaultof<DX10Header>

                header.dxgiFormat <- x.ReadUInt32()
                header.dimension <- x.ReadUInt32()
                header.miscFlag <- x.ReadUInt32()
                header.arraySize <- x.ReadUInt32()
                header.miscFlag2 <- x.ReadUInt32()

                header

        let private popcnt (u : uint32) =
            let mutable value = u
            value <- value - ((value >>> 1) &&& 0x55555555u)
            value <- (value &&& 0x33333333u) + ((value >>> 2) &&& 0x33333333u)
            value <- ((value + (value >>> 4) &&& 0xF0F0F0Fu) * 0x1010101u) >>> 24
            int value

        let private uncompressedFormats =
            Dictionary.ofList [
                [Col.Channel.Red, 8], TextureFormat.R8
                [Col.Channel.Red, 16], TextureFormat.R16
                [Col.Channel.Red, 32], TextureFormat.R32ui

                [Col.Channel.Red, 8; Col.Channel.Green, 8], TextureFormat.Rg8
                [Col.Channel.Red, 16; Col.Channel.Green, 16], TextureFormat.Rg16

                [Col.Channel.Red, 8; Col.Channel.Green, 8; Col.Channel.Blue, 8], TextureFormat.Rgb8
                [Col.Channel.Blue, 8; Col.Channel.Green, 8; Col.Channel.Red, 8], TextureFormat.Bgr8
                [Col.Channel.Red, 8; Col.Channel.Green, 8; Col.Channel.Blue, 8; Col.Channel.Alpha, 8], TextureFormat.Rgba8
                [Col.Channel.Blue, 8; Col.Channel.Green, 8; Col.Channel.Red, 8;Col.Channel.Alpha, 8], TextureFormat.Bgra8

            ]

        let tryGetTextureFormat (ddsHeader : DDSHeader) (dxHeader : Option<DX10Header>) =
            match dxHeader with
                | Some dx ->
                    failwithf "asdasd"

                | None ->
                    let fmt = ddsHeader.Format
                    if fmt.Flags.HasFlag FormatFlags.FourCC then
                        let format = 
                            match fmt.FourCC.String with
                                | "DXT1"            -> TextureFormat.CompressedRgbS3tcDxt1Ext   |> Some
                                | "DXT2" | "DXT3"   -> TextureFormat.CompressedRgbaS3tcDxt3Ext  |> Some
                                | "DXT4" | "DXT5"   -> TextureFormat.CompressedRgbaS3tcDxt5Ext  |> Some
                                | "BC4U"            -> TextureFormat.CompressedRedRgtc1         |> Some
                                | "BC4S"            -> TextureFormat.CompressedSignedRedRgtc1   |> Some
                                | "BC5U"            -> TextureFormat.CompressedRgRgtc2          |> Some
                                | "BC5S"            -> TextureFormat.CompressedSignedRgRgtc2    |> Some
                                | _                 -> None

                        format |> Option.map (fun f -> (true, f))

                    elif fmt.Flags.HasFlag FormatFlags.Rgb then
                        
                        let rBits = popcnt fmt.RMask
                        let gBits = popcnt fmt.GMask
                        let bBits = popcnt fmt.BMask
                        let aBits = popcnt fmt.AMask

                        let bits = rBits + gBits + bBits + aBits

                        if bits <> int fmt.RgbBitCount then
                            None
                        else

                            // r = 0xFF000000
                            // g = 0x00FF0000
                            // b = 0x0000FF00

                            let channels =
                                Map.ofList [
                                    if rBits <> 0 then yield fmt.RMask, (Col.Channel.Red, rBits)
                                    if gBits <> 0 then yield fmt.GMask, (Col.Channel.Green, gBits)
                                    if bBits <> 0 then yield fmt.BMask, (Col.Channel.Blue, bBits)
                                    if aBits <> 0 then yield fmt.AMask, (Col.Channel.Alpha, aBits)
                                ]
                                |> Map.toList 
                                |> List.map snd

                            match uncompressedFormats.TryGetValue channels with
                                | (true, fmt) ->
                                    Some (false, fmt)
                                | _ ->
                                    None

                    else
                        failwith ""


        let private getDataSize (level : int) (format : TextureFormat) (header : DDSHeader) =

            let pixelSize = TextureFormat.pixelSizeInBytes format

            let width = max 1u (header.Width / (1u <<< level))
            let height = max 1u (header.Height / (1u <<< level))

            if pixelSize < 0 then
                let blocksX = ceilDiv width 4u
                let blocksY = ceilDiv height 4u

                match format with
                    // 8 bytes per block
                    | TextureFormat.CompressedRgbS3tcDxt1Ext | TextureFormat.CompressedRgbaS3tcDxt1Ext ->
                        8L, int64 blocksX * int64 blocksY * 8L

                    // 16 bytes per block      
                    | TextureFormat.CompressedRgbaS3tcDxt3Ext | TextureFormat.CompressedRgbaS3tcDxt5Ext
                    | TextureFormat.CompressedRedRgtc1 | TextureFormat.CompressedSignedRedRgtc1
                    | TextureFormat.CompressedRgRgtc2 | TextureFormat.CompressedSignedRgRgtc2 ->  
                        16L, int64 blocksX * int64 blocksY * 16L

                    | _ ->
                        failwith "bad compressed format"

            else
                
                let rowSize =
                    if level = 0 && header.Flags.HasFlag Flags.Pitch then int64 header.PitchOrSize
                    else int64 width * int64 pixelSize

                let totalSize = rowSize * int64 height

                0L, totalSize

        let private ofHeader (header : DDSHeader) (dx : Option<DX10Header>) =
            let levels =
                if header.Flags.HasFlag Flags.MipMapCount then int header.MipMapCount
                else 1

            match tryGetTextureFormat header dx with
                | Some (isCompressed, fmt) -> 
                    let blockSize, _ = getDataSize 0 fmt header
                    let levelSizes = Array.init levels (fun l -> getDataSize l fmt header |> snd)
                    let levelOffset = Array.scan (+) 0L levelSizes |> Array.take levels

                    let levelRanges = Array.map2 (fun o s -> Range1l(o, o + s - 1L)) levelOffset levelSizes

                    Some { ddsHeader = header; dxHeader = dx; format = fmt; dataRanges = levelRanges; isCompressed = isCompressed; blockSizeInBytes = blockSize }
                | None ->
                    None

        let tryOfStream (stream : BinaryReader) =
            if stream.ReadUInt32() = Magic then
                let header = stream.ReadDDSHeader()

                if header.Format.Flags.HasFlag FormatFlags.FourCC && header.Format.FourCC.String = "DX10" then
                    let dx = stream.ReadDX10Header()
                    ofHeader header (Some dx)
                else
                    ofHeader header None

            else
                None

        let tryOfPtr (ptr : byref<nativeint>) =
            if read<uint32> ptr = Magic then
                ptr <- ptr + 4n
                let header = read<DDSHeader> ptr
                ptr <- ptr + nativeint sizeof<DDSHeader> 
                if header.Format.Flags.HasFlag FormatFlags.FourCC && header.Format.FourCC.String = "DX10" then
                    let dx = read<DX10Header> ptr
                    ptr <- ptr + nativeint sizeof<DX10Header>
                    ofHeader header (Some dx)
                else
                    ofHeader header None
            else
                None


    type private ImageLevel(data : byte[], level : int, dataRange : Range1l, size : V2i) =
        let sizeInBytes = dataRange.Max - dataRange.Min + 1L

        member x.SizeInBytes = sizeInBytes
        member x.Size = V3i(size.X, size.Y, 1)
        member x.Use(action) =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try
                let ptr = gc.AddrOfPinnedObject() + nativeint dataRange.Min
                action ptr
            finally
                gc.Free()

        interface INativeTextureData with
            member x.SizeInBytes = x.SizeInBytes
            member x.Size = x.Size
            member x.Use(action) = x.Use(action)

    type Image(header : Header, data : byte[]) =
        
        let levels = 
            header.dataRanges |> Array.mapi (fun level range -> 
                let size =
                    V2i(
                        int (max 1u (header.ddsHeader.Width / (1u <<< level))),
                        int (max 1u (header.ddsHeader.Height / (1u <<< level)))
                    )
                ImageLevel(data, level, range, size)
            )


        member x.MipMapLevels = header.mipMapLevels
        member x.Format = header.format
        member x.Size = V2i(int header.ddsHeader.Width, int header.ddsHeader.Height)
        member x.IsCompressed = header.isCompressed
        member x.Data = data

        member x.Item
            with get(level : int) = levels.[level] :> INativeTextureData

        interface ITexture with
            member x.WantMipMaps = x.MipMapLevels > 1

        interface INativeTexture with
            member x.Count = 1
            member x.MipMapLevels = x.MipMapLevels
            member x.Dimension = TextureDimension.Texture2D
            member x.Format = x.Format
            member x.Item
                with get(slice : int, level : int) =
                    if slice <> 0 then raise <| System.IndexOutOfRangeException()
                    x.[level]

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Image =
       

        let tryOfPtr (basePtr : nativeint) (size : nativeint) =
            let mutable ptr = basePtr
            match Header.tryOfPtr(&ptr) with
                | Some header ->
                    let remaining = size - (ptr - basePtr)
                    let arr : byte[] = Array.zeroCreate (int remaining)
                    Marshal.Copy(ptr, arr, 0, arr.Length)
                    Image(header, arr) |> Some
                | None ->
                    None

        let tryOfArray (data : byte[]) =
            let gc = GCHandle.Alloc(data, GCHandleType.Pinned)
            try tryOfPtr (gc.AddrOfPinnedObject()) (nativeint data.LongLength)
            finally gc.Free()

        let tryOfFile (file : string) =
            let data = File.ReadAllBytes(file)
            tryOfArray data
                   
        let ofFile (file : string) =
            match tryOfFile file with
                | Some img -> img
                | None -> failwith "[DDS] not a DDS image"
                    
        let ofArray (data : byte[]) =
            match tryOfArray data with
                | Some img -> img
                | None -> failwith "[DDS] not a DDS image"
            
        let ofPtr (data : nativeint) (size : nativeint) =
            match tryOfPtr data size with
                | Some img -> img
                | None -> failwith "[DDS] not a DDS image"
            






        
        
                   

module DDSTest =
    open DevILSharp
    open Aardvark.Application
    open Aardvark.SceneGraph
    open Aardvark.Base
    open Aardvark.Base.Rendering
    open FSharp.Data.Adaptive

    module DiffuseTexture = 
        open FShade

        let diffuseSampler =
            sampler2d {
                texture uniform?DiffuseColorTexture
                filter Filter.MinMagMipPoint
                addressU WrapMode.Wrap
                addressV WrapMode.Wrap
            }

        [<GLSLIntrinsic("fract({0})")>]
        let fract (v : V2d) : V2d =
            onlyInShaderCode "frac"

        [<GLSLIntrinsic("smoothstep({0}, {1}, {2})")>]
        let smooth (edge0 : float) (edge1 : float) (x : float) : 'a =
            onlyInShaderCode "smooth"

        [<ReflectedDefinition>]
        let solveX (p : V2d) (dpx : V2d) (dpy : V2d) (nx : float) =
            let v = dpx.Y * (nx - p.X) / (dpy.X * dpx.Y - dpx.X * dpy.Y)
            let u = dpy.Y * (nx - p.X) / (dpx.X * dpy.Y - dpy.X * dpx.Y)
            sqrt (u*u + v*v)
            
        [<ReflectedDefinition>]
        let solveY (p : V2d) (dpx : V2d) (dpy : V2d) (ny : float) =
            let u = dpy.X * (ny - p.Y) / (dpx.Y * dpy.X - dpx.X * dpy.Y)
            let v = dpx.X * (ny - p.Y) / (dpx.X * dpy.Y - dpy.X * dpx.Y)
            sqrt (u*u + v*v)


        let diffuseTexture (v : Effects.Vertex) =
            fragment {
                let level : int = uniform?TexLevel

                let s = diffuseSampler.GetSize level
                let pos = v.tc * V2d s

                let p = pos
                let dpx = ddx v.tc * V2d s
                let dpy = ddy v.tc * V2d s

                let nx = ceil pos.X
                let px = floor pos.X
                let ny = ceil pos.Y
                let py = floor pos.Y

                
                // p.Y + u * dpx.Y + v * dpy.Y = ny
                //       u * dpx.X + v * dpy.X = 0
                

                //               u * dpx.Y * dpy.X + v * dpy.Y * dpy.X = dpy.X * (ny - p.Y)
                //               u * dpx.X * dpy.Y + v * dpy.X * dpy.Y = 0


                // u  = dpy.X * (ny - p.Y) / (dpx.Y * dpy.X - dpx.X * dpy.Y)
                



                
                // p.Y * dpx.X + u * dpx.X * dpx.Y + v * dpx.X * dpy.Y = ny * dpx.X
                //               u * dpx.X * dpx.Y + v * dpy.X * dpx.Y = 0

                // v = dpx.X * (ny - p.Y) / (dpx.X * dpy.Y - dpy.X * dpx.Y)




                // p.X + u * dpx.X + v * dpy.X = nx
                //       u * dpx.Y + v * dpy.Y = 0

                
                // p.X * dpx.Y + u * dpx.X * dpx.Y + v * dpy.X * dpx.Y = nx * dpx.Y
                //               u * dpx.X * dpx.Y + v * dpx.X * dpy.Y = 0
                

                // p.X * dpy.Y + u * dpx.X * dpy.Y + v * dpy.X * dpy.Y = nx * dpy.Y
                //               u * dpy.X * dpx.Y + v * dpy.X * dpy.Y = 0

                

                // v = dpx.Y * (nx - p.X) / (dpy.X * dpx.Y - dpx.X * dpy.Y)
                // u = dpy.Y * (nx - p.X) / (dpx.X * dpy.Y - dpy.X * dpx.Y)
                
                let grad = max (max (abs dpx.X) (abs dpx.Y)) (max (abs dpy.X) (abs dpy.Y))

                let alpha = 
                    if grad > 1.0 then 0.0
                    else smooth 0.0 1.0 (1.0 - grad)


                let dx = min (solveX p dpx dpy nx) (solveX p dpx dpy px)
                let dy = min (solveY p dpx dpy ny) (solveY p dpx dpy py)
                let dist = min dx dy
                let texColor = diffuseSampler.Read(V2i pos, level)

                //let dist = dist / 2.0c

                let borderColor =
                    (1.0 - alpha) * texColor + alpha * (V4d(V3d.III - texColor.XYZ, 1.0))


                if dist < 1.0 then
                    let f = dist //smooth 0.0 1.0 dist 
                    return texColor * f + borderColor * (1.0 - f)
                else
                    return texColor
            }


    let run() =
        Ag.initialize()
        Aardvark.Init()

        let file = "texture.dds"
        let img = DDS.Image.ofFile file


        let win =
            window {
                backend Backend.Vulkan
                debug true
                display Display.Mono
            }

        let level = AVal.init 0
        win.Keyboard.KeyDown(Keys.Space).Values.Add (fun () ->
            transact (fun () ->
                let n = (level.Value + 1) % img.MipMapLevels
                level.Value <- n
                Log.line "level: %d" n
            )
        )

        let cubes =
            Sg.ofList [
                Sg.box' C4b.White Box3d.Unit
                    |> Sg.diffuseTexture (AVal.constant (img :> ITexture))

                Sg.box' C4b.White Box3d.Unit
                    |> Sg.translate 2.0 0.0 0.0
                    |> Sg.diffuseTexture (AVal.constant (FileTexture(file, TextureParams.mipmapped) :> ITexture))
            ]

        let sg =
            cubes
                |> Sg.uniform "TexLevel" level
                |> Sg.shader {
                    do! DefaultSurfaces.trafo
                    do! DiffuseTexture.diffuseTexture
                }
                

        win.Scene <- sg
        win.Run()



    let runLoad() =
        let file = "texture.dds"
        let img = DDS.Image.ofFile file
        let level = img.[4]


        level.Use(fun ptr ->
            let size = level.Size.XY
            let sizeInBytes = level.SizeInBytes

            IL.Init()

            let i = IL.GenImage()
            IL.BindImage(i)


            if img.IsCompressed then
                IL.TexImageDxtc(size.X, size.Y, 1, CompressedDataFormat.Dxt5, ptr) |> printfn "dxtc: %A"
                IL.DxtcDataToImage() |> printfn "uncompress: %A"

                let fmt = IL.GetFormat()
                let pt = IL.GetDataType()
                let w = IL.GetInteger(IntName.ImageWidth)
                let h = IL.GetInteger(IntName.ImageHeight)
                printfn "%dx%d: %A %A" w  h fmt pt

            else
                let channels, cmft =
                    match img.Format with
                        | TextureFormat.Bgr8 -> 3, ChannelFormat.BGR
                        | _ -> failwith ""

                IL.TexImage(size.X, size.Y, 1, byte channels, cmft, ChannelType.UnsignedByte, ptr) |> printfn "texImage: %A"


            let outFile = "texture.jpg"
            if File.Exists outFile then File.Delete outFile
            IL.Save(ImageType.Jpg, outFile) |> printfn "save: %A"

            IL.BindImage(0)
            IL.DeleteImage i

        )




        ()

