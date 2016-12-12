namespace Aardvark.Base

open System.Runtime.InteropServices

// 5 bits
type private ChannelSize =
    | S0 = 0uy 
    | S1 = 1uy    | S2 = 2uy    | S3 = 3uy    | S4 = 4uy
    | S5 = 5uy    | S6 = 6uy    | S7 = 7uy    | S8 = 8uy
    | S9 = 9uy    | S10 = 10uy  | S11 = 11uy  | S12 = 12uy
    | S13 = 13uy  | S14 = 14uy  | S15 = 15uy  | S16 = 16uy
    | S24 = 17uy
    | S32 = 18uy
    | S64 = 19uy
    | S128 = 20uy

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module private ChannelSize =

    let private real    = ChannelSize.GetNames(typeof<ChannelSize>) |> Array.map (fun n -> System.Int32.Parse(n.Substring(1)))
    let private values  = ChannelSize.GetValues(typeof<ChannelSize>) |> unbox<ChannelSize[]>
    let private ofSizeDict = System.Collections.Generic.Dictionary<int, ChannelSize>()
    do for i in 0 .. real.Length-1 do
        let real    = real.[i]
        let value   = values.[i]
        ofSizeDict.[real] <- value

    let ofSize (s : int) =
        match ofSizeDict.TryGetValue s with
            | (true, s) -> s
            | _ -> failwithf "invalid channel-size: %A" s

    let toSize (c : ChannelSize) =
        real.[int c]
 
            
// 3 bits
type ChannelSemantic =
    | None          = 0uy
    | Red           = 1uy
    | Green         = 2uy
    | Blue          = 3uy
    | Alpha         = 4uy
    | Depth         = 5uy
    | Stencil       = 6uy

// 8 bits (8 values)
type ChannelType =
    | Unknown       = 0uy
    | UInt          = 1uy
    | SInt          = 2uy
    | UNorm         = 3uy
    | SNorm         = 4uy
    | UScaled       = 5uy
    | SScaled       = 6uy
    | Float         = 7uy
    | Srgb          = 8uy

// 8 bits (11 values)
type CompressionType =
    | None          = 0uy
    | Bc1           = 1uy
    | Bc2           = 2uy
    | Bc3           = 3uy
    | Bc4           = 4uy
    | Bc5           = 5uy
    | Bc6           = 6uy
    | Bc7           = 7uy
    | Etc2          = 8uy
    | Eac           = 9uy
    | Astc          = 10uy


module private Helpers =
    let totalBits       = 8
    let semanticBits    = 3
    let sizeBits        = 5

    let semanticShift   = totalBits - semanticBits
    let sizeShift       = semanticShift - sizeBits

    let semanticMask    = ((1uy <<< semanticBits) - 1uy) <<< semanticShift
    let semanticMaskInv = ~~~semanticMask
    let sizeMask        = ((1uy <<< sizeBits) - 1uy) <<< sizeShift
    let sizeMaskInv     = ~~~sizeMask


    let semName =
        LookupTable.lookupTable [
            ChannelSemantic.None,       "X"
            ChannelSemantic.Red,        "R"
            ChannelSemantic.Green,      "G"
            ChannelSemantic.Blue,       "B"
            ChannelSemantic.Alpha,      "A"
            ChannelSemantic.Depth,      "D"
            ChannelSemantic.Stencil,    "S"
        ]

    let typeSuffix =
        LookupTable.lookupTable [
            ChannelType.Unknown,        ""
            ChannelType.UInt,           "ui"
            ChannelType.SInt,           "i"
            ChannelType.UNorm,          "un"
            ChannelType.SNorm,          "n"
            ChannelType.UScaled,        "us"
            ChannelType.SScaled,        "s"
            ChannelType.Float,          "f"
            ChannelType.Srgb,           "Srgb"
        ]

// 8 bits
type ChannelFormat =
    struct
        val mutable public Code : uint8

        member x.Semantic
            with get() =
                (x.Code &&& 0b00000111uy) |> unbox<ChannelSemantic>
            and set (v : ChannelSemantic) =
                x.Code <- (x.Code &&& 0b11111000uy) ||| (uint8 v)

        member x.SizeInBits
            with get() =
                x.Code >>> 3 |> unbox<ChannelSize> |> ChannelSize.toSize
            and set (v : int) =
                let v = ChannelSize.ofSize v
                x.Code <- (x.Code &&& 0b00000111uy) ||| ((uint8 v) <<< 3)

        override x.ToString() =
            let bits = x.SizeInBits
            if bits = 0 then ""
            else (Helpers.semName x.Semantic) + (string x.SizeInBits)

        new(sem : ChannelSemantic, bits : int) = 
            let size =  ChannelSize.ofSize bits
            { Code = ((uint8 size) <<< 3) ||| (uint8 sem) }
    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ChannelFormat =

    let withSizeInBits (s : int) (c : ChannelFormat) =
        let sem = c.Semantic
        let old = c.SizeInBits
        if old <> 0 then
            ChannelFormat(sem, s)
        else
            c

    let Empty   = ChannelFormat(ChannelSemantic.None, 0)

        
    let R1      = ChannelFormat(ChannelSemantic.Red,        1)
    let G1      = ChannelFormat(ChannelSemantic.Green,      1)
    let B1      = ChannelFormat(ChannelSemantic.Blue,       1)
    let A1      = ChannelFormat(ChannelSemantic.Alpha,      1)
    let D1      = ChannelFormat(ChannelSemantic.Depth,      1)
    let S1      = ChannelFormat(ChannelSemantic.Stencil,    1)
        
    let R2      = ChannelFormat(ChannelSemantic.Red,        2)
    let G2      = ChannelFormat(ChannelSemantic.Green,      2)
    let B2      = ChannelFormat(ChannelSemantic.Blue,       2)
    let A2      = ChannelFormat(ChannelSemantic.Alpha,      2)
    let D2      = ChannelFormat(ChannelSemantic.Depth,      2)
    let S2      = ChannelFormat(ChannelSemantic.Stencil,    2)
        
    let R3      = ChannelFormat(ChannelSemantic.Red,        3)
    let G3      = ChannelFormat(ChannelSemantic.Green,      3)
    let B3      = ChannelFormat(ChannelSemantic.Blue,       3)
    let A3      = ChannelFormat(ChannelSemantic.Alpha,      3)
    let D3      = ChannelFormat(ChannelSemantic.Depth,      3)
    let S3      = ChannelFormat(ChannelSemantic.Stencil,    3)
        
    let R4      = ChannelFormat(ChannelSemantic.Red,        4)
    let G4      = ChannelFormat(ChannelSemantic.Green,      4)
    let B4      = ChannelFormat(ChannelSemantic.Blue,       4)
    let A4      = ChannelFormat(ChannelSemantic.Alpha,      4)
    let D4      = ChannelFormat(ChannelSemantic.Depth,      4)
    let S4      = ChannelFormat(ChannelSemantic.Stencil,    4)
        
    let R5      = ChannelFormat(ChannelSemantic.Red,        5)
    let G5      = ChannelFormat(ChannelSemantic.Green,      5)
    let B5      = ChannelFormat(ChannelSemantic.Blue,       5)
    let A5      = ChannelFormat(ChannelSemantic.Alpha,      5)
    let D5      = ChannelFormat(ChannelSemantic.Depth,      5)
    let S5      = ChannelFormat(ChannelSemantic.Stencil,    5)
        
    let R6      = ChannelFormat(ChannelSemantic.Red,        6)
    let G6      = ChannelFormat(ChannelSemantic.Green,      6)
    let B6      = ChannelFormat(ChannelSemantic.Blue,       6)
    let A6      = ChannelFormat(ChannelSemantic.Alpha,      6)
    let D6      = ChannelFormat(ChannelSemantic.Depth,      6)
    let S6      = ChannelFormat(ChannelSemantic.Stencil,    6)

    let R8      = ChannelFormat(ChannelSemantic.Red,        8)
    let G8      = ChannelFormat(ChannelSemantic.Green,      8)
    let B8      = ChannelFormat(ChannelSemantic.Blue,       8)
    let A8      = ChannelFormat(ChannelSemantic.Alpha,      8)
    let D8      = ChannelFormat(ChannelSemantic.Depth,      8)
    let S8      = ChannelFormat(ChannelSemantic.Stencil,    8)

    let R9      = ChannelFormat(ChannelSemantic.Red,        9)
    let G9      = ChannelFormat(ChannelSemantic.Green,      9)
    let B9      = ChannelFormat(ChannelSemantic.Blue,       9)
    let A9      = ChannelFormat(ChannelSemantic.Alpha,      9)
    let D9      = ChannelFormat(ChannelSemantic.Depth,      9)
    let S9      = ChannelFormat(ChannelSemantic.Stencil,    9)

    let R10     = ChannelFormat(ChannelSemantic.Red,        10)
    let G10     = ChannelFormat(ChannelSemantic.Green,      10)
    let B10     = ChannelFormat(ChannelSemantic.Blue,       10)
    let A10     = ChannelFormat(ChannelSemantic.Alpha,      10)
    let D10     = ChannelFormat(ChannelSemantic.Depth,      10)
    let S10     = ChannelFormat(ChannelSemantic.Stencil,    10)

    let R11     = ChannelFormat(ChannelSemantic.Red,        11)
    let G11     = ChannelFormat(ChannelSemantic.Green,      11)
    let B11     = ChannelFormat(ChannelSemantic.Blue,       11)
    let A11     = ChannelFormat(ChannelSemantic.Alpha,      11)
    let D11     = ChannelFormat(ChannelSemantic.Depth,      11)
    let S11     = ChannelFormat(ChannelSemantic.Stencil,    11)

    let R12     = ChannelFormat(ChannelSemantic.Red,        12)
    let G12     = ChannelFormat(ChannelSemantic.Green,      12)
    let B12     = ChannelFormat(ChannelSemantic.Blue,       12)
    let A12     = ChannelFormat(ChannelSemantic.Alpha,      12)
    let D12     = ChannelFormat(ChannelSemantic.Depth,      12)
    let S12     = ChannelFormat(ChannelSemantic.Stencil,    12)
        
    let R16     = ChannelFormat(ChannelSemantic.Red,        16)
    let G16     = ChannelFormat(ChannelSemantic.Green,      16)
    let B16     = ChannelFormat(ChannelSemantic.Blue,       16)
    let A16     = ChannelFormat(ChannelSemantic.Alpha,      16)
    let D16     = ChannelFormat(ChannelSemantic.Depth,      16)
    let S16     = ChannelFormat(ChannelSemantic.Stencil,    16)
        
    let R24     = ChannelFormat(ChannelSemantic.Red,        24)
    let G24     = ChannelFormat(ChannelSemantic.Green,      24)
    let B24     = ChannelFormat(ChannelSemantic.Blue,       24)
    let A24     = ChannelFormat(ChannelSemantic.Alpha,      24)
    let D24     = ChannelFormat(ChannelSemantic.Depth,      24)
    let S24     = ChannelFormat(ChannelSemantic.Stencil,    24)
        
    let R32     = ChannelFormat(ChannelSemantic.Red,        32)
    let G32     = ChannelFormat(ChannelSemantic.Green,      32)
    let B32     = ChannelFormat(ChannelSemantic.Blue,       32)
    let A32     = ChannelFormat(ChannelSemantic.Alpha,      32)
    let D32     = ChannelFormat(ChannelSemantic.Depth,      32)
    let S32     = ChannelFormat(ChannelSemantic.Stencil,    32)

    let R64     = ChannelFormat(ChannelSemantic.Red,        64)
    let G64     = ChannelFormat(ChannelSemantic.Green,      64)
    let B64     = ChannelFormat(ChannelSemantic.Blue,       64)
    let A64     = ChannelFormat(ChannelSemantic.Alpha,      64)
    let D64     = ChannelFormat(ChannelSemantic.Depth,      64)
    let S64     = ChannelFormat(ChannelSemantic.Stencil,    64)

    let R128    = ChannelFormat(ChannelSemantic.Red,        128)
    let G128    = ChannelFormat(ChannelSemantic.Green,      128)
    let B128    = ChannelFormat(ChannelSemantic.Blue,       128)
    let A128    = ChannelFormat(ChannelSemantic.Alpha,      128)
    let D128    = ChannelFormat(ChannelSemantic.Depth,      128)
    let S128    = ChannelFormat(ChannelSemantic.Stencil,    128)


// 64 bits
    
type ImageFormat =
    struct
        val mutable public      Type            : ChannelType
        val mutable public      Compression     : CompressionType
        val mutable private     m_Channels      : uint8
        val mutable private     m_Size          : uint8
        val mutable public      C0              : ChannelFormat
        val mutable public      C1              : ChannelFormat
        val mutable public      C2              : ChannelFormat
        val mutable public      C3              : ChannelFormat

        member x.Channels = int x.m_Channels
        member x.SizeInBits = int x.m_Size

        member x.Item
            with get (i : int) =
                match i with    
                    | 0 -> x.C0
                    | 1 -> x.C1
                    | 2 -> x.C2
                    | 3 -> x.C3
                    | _ -> raise <| System.IndexOutOfRangeException() 

        member x.Semantics =
            Set.ofList [ x.C0.Semantic; x.C1.Semantic; x.C2.Semantic; x.C3.Semantic ]
                |> Set.remove ChannelSemantic.None

        member x.Contains(sem : ChannelSemantic) =
            sem = ChannelSemantic.None  ||
            sem = x.C0.Semantic         ||
            sem = x.C1.Semantic         ||
            sem = x.C2.Semantic         ||
            sem = x.C3.Semantic

        member inline x.HasRed = x.Contains ChannelSemantic.Red
        member inline x.HasGreen = x.Contains ChannelSemantic.Green
        member inline x.HasBlue = x.Contains ChannelSemantic.Blue
        member inline x.HasAlpha = x.Contains ChannelSemantic.Alpha
        member inline x.HasDepth = x.Contains ChannelSemantic.Depth
        member inline x.HasStencil = x.Contains ChannelSemantic.Stencil

        member x.UnusedSemantics =
            let all = Set.ofList [ ChannelSemantic.Red; ChannelSemantic.Green; ChannelSemantic.Blue; ChannelSemantic.Alpha; ChannelSemantic.Depth; ChannelSemantic.Stencil ]
            Set.difference all x.Semantics

            

        member x.AddChannel(c : ChannelFormat) =
            match x.Channels with
                | 0 -> ImageFormat(c, x.Type)
                | 1 -> ImageFormat(x.C0, c, x.Type)
                | 2 -> ImageFormat(x.C0, x.C1, c, x.Type)
                | 3 -> ImageFormat(x.C0, x.C1, x.C2, c, x.Type)
                | _ -> failwithf "cannot add channel to 4-channel ImageFormat"

        member x.WithNextChannel =
            let channels = x.Channels
            if channels <= 0 || channels >= 4 then 
                x
            else
                let unused = x.UnusedSemantics
                let has s = not (Set.contains s unused)

                if has ChannelSemantic.Depth then
                    if has ChannelSemantic.Stencil then x
                    else ImageFormat(x.C0, ChannelFormat.S8, x.Type)
                else 
                    let c0Size = x.C0.SizeInBits
                    if x.SizeInBits % c0Size = 0 then
                        let newChannel = ChannelFormat(unused |> Seq.head, c0Size)
                        x.AddChannel newChannel
                    else
                        x

        member x.WithNextSize =
            let channels = x.Channels
            if channels <= 0 then 
                x
            else
                let nextSize = Fun.NextPowerOfTwo(x.C0.SizeInBits + 1)

                if nextSize <= 128 then
                    ImageFormat(
                        ChannelFormat.withSizeInBits nextSize x.C0,
                        ChannelFormat.withSizeInBits nextSize x.C1,
                        ChannelFormat.withSizeInBits nextSize x.C2,
                        ChannelFormat.withSizeInBits nextSize x.C3,
                        x.Type
                    )
                else
                    x

        member x.WithCompression (c : CompressionType) =
            let mutable res = x
            res.Compression <- c
            res
                
        member x.WithType (t : ChannelType) =
            let mutable res = x
            res.Type <- t
            res

        override x.ToString() =
            match x.Compression with
                | CompressionType.None -> (string x.C0) + (string x.C1) + (string x.C2) + (string x.C3) + (Helpers.typeSuffix x.Type)
                | c -> "Compressed" + (string x.C0) + (string x.C1) + (string x.C2) + (string x.C3) + (Helpers.typeSuffix x.Type) + (string c)

        new(C0 : ChannelFormat, C1 : ChannelFormat, C2 : ChannelFormat, C3 : ChannelFormat, t : ChannelType, comp : CompressionType) = { C0 = C0; C1 = C1; C2 = C2; C3 = C3; Type = t; Compression = comp; m_Channels = 4uy; m_Size = uint8 (C0.SizeInBits + C1.SizeInBits + C2.SizeInBits + C3.SizeInBits) }
        new(C0 : ChannelFormat, C1 : ChannelFormat, C2 : ChannelFormat, C3 : ChannelFormat, t : ChannelType) = { C0 = C0; C1 = C1; C2 = C2; C3 = C3; Type = t; Compression = CompressionType.None; m_Channels = 4uy; m_Size = uint8 (C0.SizeInBits + C1.SizeInBits + C2.SizeInBits + C3.SizeInBits) }
        new(C0 : ChannelFormat, C1 : ChannelFormat, C2 : ChannelFormat, t : ChannelType, comp : CompressionType) = { C0 = C0; C1 = C1; C2 = C2; C3 = ChannelFormat(); Type = t; Compression = comp; m_Channels = 3uy; m_Size = uint8 (C0.SizeInBits + C1.SizeInBits + C2.SizeInBits) }
        new(C0 : ChannelFormat, C1 : ChannelFormat, C2 : ChannelFormat, t : ChannelType) = { C0 = C0; C1 = C1; C2 = C2; C3 = ChannelFormat(); Type = t; Compression = CompressionType.None; m_Channels = 3uy; m_Size = uint8 (C0.SizeInBits + C1.SizeInBits + C2.SizeInBits) }
        new(C0 : ChannelFormat, C1 : ChannelFormat, t : ChannelType, comp : CompressionType) = { C0 = C0; C1 = C1; C2 = ChannelFormat(); C3 = ChannelFormat(); Type = t; Compression = comp; m_Channels = 2uy; m_Size = uint8 (C0.SizeInBits + C1.SizeInBits) }
        new(C0 : ChannelFormat, C1 : ChannelFormat, t : ChannelType) = { C0 = C0; C1 = C1; C2 = ChannelFormat(); C3 = ChannelFormat(); Type = t; Compression = CompressionType.None; m_Channels = 2uy; m_Size = uint8 (C0.SizeInBits + C1.SizeInBits) }
        new(C0 : ChannelFormat, t : ChannelType, comp : CompressionType) = { C0 = C0; C1 = ChannelFormat(); C2 = ChannelFormat(); C3 = ChannelFormat(); Type = t; Compression = comp; m_Channels = 1uy; m_Size = uint8 (C0.SizeInBits) }
        new(C0 : ChannelFormat, t : ChannelType) = { C0 = C0; C1 = ChannelFormat(); C2 = ChannelFormat(); C3 = ChannelFormat(); Type = t; Compression = CompressionType.None; m_Channels = 1uy; m_Size = uint8 (C0.SizeInBits) }
        new(t : ChannelType) = { C0 = ChannelFormat(); C1 = ChannelFormat(); C2 = ChannelFormat(); C3 = ChannelFormat(); Type = t; Compression = CompressionType.None; m_Channels = 1uy; m_Size = 0uy }

        new(channels : list<ChannelFormat>, t : ChannelType) = 
            match channels with
                | [] -> ImageFormat(t)
                | [c0] -> ImageFormat(c0, t)
                | [c0; c1] -> ImageFormat(c0, c1, t)
                | [c0; c1; c2] -> ImageFormat(c0, c1, c2, t)
                | c0 :: c1 :: c2 :: c3 :: _ -> ImageFormat(c0, c1, c2, c3, t)

    end

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ImageFormat =

    let R3g3b2      = ImageFormat(ChannelFormat.R3, ChannelFormat.G3, ChannelFormat.B2, ChannelType.Unknown)
    let R5g6b5      = ImageFormat(ChannelFormat.R5, ChannelFormat.G6, ChannelFormat.B5, ChannelType.Unknown)
    let R11g11b10   = ImageFormat(ChannelFormat.R11, ChannelFormat.G11, ChannelFormat.B10, ChannelType.Unknown)
    let Rgb2        = ImageFormat(ChannelFormat.R2, ChannelFormat.G2, ChannelFormat.B2, ChannelType.Unknown)
    let Rgb4        = ImageFormat(ChannelFormat.R4, ChannelFormat.G4, ChannelFormat.B4, ChannelType.Unknown)
    let Rgb9        = ImageFormat(ChannelFormat.R9, ChannelFormat.G9, ChannelFormat.B9, ChannelType.Unknown)
    let Rgb10       = ImageFormat(ChannelFormat.R10, ChannelFormat.G10, ChannelFormat.B10, ChannelType.Unknown)
    let Rgb12       = ImageFormat(ChannelFormat.R12, ChannelFormat.G12, ChannelFormat.B12, ChannelType.Unknown)

    let R5g6b5a8    = ImageFormat(ChannelFormat.R5, ChannelFormat.G6, ChannelFormat.B5, ChannelFormat.A8, ChannelType.Unknown)
    let Rgba2       = ImageFormat(ChannelFormat.R2, ChannelFormat.G2, ChannelFormat.B2, ChannelFormat.A2, ChannelType.Unknown)
    let Rgba4       = ImageFormat(ChannelFormat.R4, ChannelFormat.G4, ChannelFormat.B4, ChannelFormat.A4, ChannelType.Unknown)
    let Rgb5a1      = ImageFormat(ChannelFormat.R5, ChannelFormat.G5, ChannelFormat.B5, ChannelFormat.A1, ChannelType.Unknown)
    let Rgb10a2     = ImageFormat(ChannelFormat.R10, ChannelFormat.G10, ChannelFormat.B10, ChannelFormat.A2, ChannelType.Unknown)
    let Rgba12      = ImageFormat(ChannelFormat.R12, ChannelFormat.G12, ChannelFormat.B12, ChannelFormat.A12, ChannelType.Unknown)

    let D16         = ImageFormat(ChannelFormat.D16, ChannelType.Unknown)
    let D16f        = ImageFormat(ChannelFormat.D16, ChannelType.Float)
    let D16ui       = ImageFormat(ChannelFormat.D16, ChannelType.UInt)
    
    let D24         = ImageFormat(ChannelFormat.D24, ChannelType.Unknown)
    let D24f        = ImageFormat(ChannelFormat.D24, ChannelType.Float)
    let D24ui       = ImageFormat(ChannelFormat.D24, ChannelType.UInt)
    let D24s8       = ImageFormat(ChannelFormat.D24, ChannelFormat.S8, ChannelType.Unknown)

    let D32         = ImageFormat(ChannelFormat.D32, ChannelType.Unknown)
    let D32f        = ImageFormat(ChannelFormat.D32, ChannelType.Float)
    let D32ui       = ImageFormat(ChannelFormat.D32, ChannelType.UInt)
    let D32s8       = ImageFormat(ChannelFormat.D32, ChannelFormat.S8, ChannelType.Unknown)


    let Srgb8       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.Srgb)
    let Srgba8      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.Srgb)
    let Sbgr8       = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.Srgb)
    let Sbgra8      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.Srgb)


    let R8          = ImageFormat(ChannelFormat.R8, ChannelType.Unknown)
    let R8ui        = ImageFormat(ChannelFormat.R8, ChannelType.UInt)
    let R8i         = ImageFormat(ChannelFormat.R8, ChannelType.SInt)
    let R8n         = ImageFormat(ChannelFormat.R8, ChannelType.UNorm)
    let R8sn        = ImageFormat(ChannelFormat.R8, ChannelType.SNorm)
    let R8s         = ImageFormat(ChannelFormat.R8, ChannelType.UScaled)
    let R8ss        = ImageFormat(ChannelFormat.R8, ChannelType.SScaled)
    let R8f         = ImageFormat(ChannelFormat.R8, ChannelType.Float)

    let Rg8         = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.Unknown)
    let Rg8ui       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.UInt)
    let Rg8i        = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.SInt)
    let Rg8n        = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.UNorm)
    let Rg8sn       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.SNorm)
    let Rg8s        = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.UScaled)
    let Rg8ss       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.SScaled)
    let Rg8f        = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelType.Float)
  
    let Rgb8        = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.Unknown)
    let Rgb8ui      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.UInt)
    let Rgb8i       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.SInt)
    let Rgb8n       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.UNorm)
    let Rgb8sn      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.SNorm)
    let Rgb8s       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.UScaled)
    let Rgb8ss      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.SScaled)
    let Rgb8f       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.Float)
 
    let Rgba8       = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.Unknown)
    let Rgba8ui     = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.UInt)
    let Rgba8i      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.SInt)
    let Rgba8n      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.UNorm)
    let Rgba8sn     = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.SNorm)
    let Rgba8s      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.UScaled)
    let Rgba8ss     = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.SScaled)
    let Rgba8f      = ImageFormat(ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelFormat.A8, ChannelType.Float)

    let Bgr8        = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.Unknown)
    let Bgr8ui      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.UInt)
    let Bgr8i       = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.SInt)
    let Bgr8n       = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.UNorm)
    let Bgr8sn      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.SNorm)
    let Bgr8s       = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.UScaled)
    let Bgr8ss      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.SScaled)
    let Bgr8f       = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.Float)

    let Bgra8       = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.Unknown)
    let Bgra8ui     = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.UInt)
    let Bgra8i      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.SInt)
    let Bgra8n      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.UNorm)
    let Bgra8sn     = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.SNorm)
    let Bgra8s      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.UScaled)
    let Bgra8ss     = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.SScaled)
    let Bgra8f      = ImageFormat(ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelFormat.A8, ChannelType.Float)

    let Argb8       = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.Unknown)
    let Argb8ui     = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.UInt)
    let Argb8i      = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.SInt)
    let Argb8n      = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.UNorm)
    let Argb8sn     = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.SNorm)
    let Argb8s      = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.UScaled)
    let Argb8ss     = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.SScaled)
    let Argb8f      = ImageFormat(ChannelFormat.A8, ChannelFormat.R8, ChannelFormat.G8, ChannelFormat.B8, ChannelType.Float)

    let Abgr8       = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.Unknown)
    let Abgr8ui     = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.UInt)
    let Abgr8i      = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.SInt)
    let Abgr8n      = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.UNorm)
    let Abgr8sn     = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.SNorm)
    let Abgr8s      = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.UScaled)
    let Abgr8ss     = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.SScaled)
    let Abgr8f      = ImageFormat(ChannelFormat.A8, ChannelFormat.B8, ChannelFormat.G8, ChannelFormat.R8, ChannelType.Float)

    

    let R16         = ImageFormat(ChannelFormat.R16, ChannelType.Unknown)
    let R16ui       = ImageFormat(ChannelFormat.R16, ChannelType.UInt)
    let R16i        = ImageFormat(ChannelFormat.R16, ChannelType.SInt)
    let R16n        = ImageFormat(ChannelFormat.R16, ChannelType.UNorm)
    let R16sn       = ImageFormat(ChannelFormat.R16, ChannelType.SNorm)
    let R16s        = ImageFormat(ChannelFormat.R16, ChannelType.UScaled)
    let R16ss       = ImageFormat(ChannelFormat.R16, ChannelType.SScaled)
    let R16f        = ImageFormat(ChannelFormat.R16, ChannelType.Float)

    let Rg16        = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.Unknown)
    let Rg16ui      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.UInt)
    let Rg16i       = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.SInt)
    let Rg16n       = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.UNorm)
    let Rg16sn      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.SNorm)
    let Rg16s       = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.UScaled)
    let Rg16ss      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.SScaled)
    let Rg16f       = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelType.Float)

    let Rgb16       = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.Unknown)
    let Rgb16ui     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.UInt)
    let Rgb16i      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.SInt)
    let Rgb16n      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.UNorm)
    let Rgb16sn     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.SNorm)
    let Rgb16s      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.UScaled)
    let Rgb16ss     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.SScaled)
    let Rgb16f      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.Float)

    let Rgba16      = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.Unknown)
    let Rgba16ui    = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.UInt)
    let Rgba16i     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.SInt)
    let Rgba16n     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.UNorm)
    let Rgba16sn    = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.SNorm)
    let Rgba16s     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.UScaled)
    let Rgba16ss    = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.SScaled)
    let Rgba16f     = ImageFormat(ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelFormat.A16, ChannelType.Float)

    let Bgr16       = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.Unknown)
    let Bgr16ui     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.UInt)
    let Bgr16i      = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.SInt)
    let Bgr16n      = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.UNorm)
    let Bgr16sn     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.SNorm)
    let Bgr16s      = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.UScaled)
    let Bgr16ss     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.SScaled)
    let Bgr16f      = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.Float)

    let Bgra16      = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.Unknown)
    let Bgra16ui    = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.UInt)
    let Bgra16i     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.SInt)
    let Bgra16n     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.UNorm)
    let Bgra16sn    = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.SNorm)
    let Bgra16s     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.UScaled)
    let Bgra16ss    = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.SScaled)
    let Bgra16f     = ImageFormat(ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelFormat.A16, ChannelType.Float)

    let Argb16      = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.Unknown)
    let Argb16ui    = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.UInt)
    let Argb16i     = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.SInt)
    let Argb16n     = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.UNorm)
    let Argb16sn    = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.SNorm)
    let Argb16s     = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.UScaled)
    let Argb16ss    = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.SScaled)
    let Argb16f     = ImageFormat(ChannelFormat.A16, ChannelFormat.R16, ChannelFormat.G16, ChannelFormat.B16, ChannelType.Float)

    let Abgr16      = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.Unknown)
    let Abgr16ui    = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.UInt)
    let Abgr16i     = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.SInt)
    let Abgr16n     = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.UNorm)
    let Abgr16sn    = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.SNorm)
    let Abgr16s     = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.UScaled)
    let Abgr16ss    = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.SScaled)
    let Abgr16f     = ImageFormat(ChannelFormat.A16, ChannelFormat.B16, ChannelFormat.G16, ChannelFormat.R16, ChannelType.Float)


    let R32         = ImageFormat(ChannelFormat.R32, ChannelType.Unknown)
    let R32ui       = ImageFormat(ChannelFormat.R32, ChannelType.UInt)
    let R32i        = ImageFormat(ChannelFormat.R32, ChannelType.SInt)
    let R32n        = ImageFormat(ChannelFormat.R32, ChannelType.UNorm)
    let R32sn       = ImageFormat(ChannelFormat.R32, ChannelType.SNorm)
    let R32s        = ImageFormat(ChannelFormat.R32, ChannelType.UScaled)
    let R32ss       = ImageFormat(ChannelFormat.R32, ChannelType.SScaled)
    let R32f        = ImageFormat(ChannelFormat.R32, ChannelType.Float)

    let Rg32        = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.Unknown)
    let Rg32ui      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.UInt)
    let Rg32i       = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.SInt)
    let Rg32n       = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.UNorm)
    let Rg32sn      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.SNorm)
    let Rg32s       = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.UScaled)
    let Rg32ss      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.SScaled)
    let Rg32f       = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelType.Float)

    let Rgb32       = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.Unknown)
    let Rgb32ui     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.UInt)
    let Rgb32i      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.SInt)
    let Rgb32n      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.UNorm)
    let Rgb32sn     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.SNorm)
    let Rgb32s      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.UScaled)
    let Rgb32ss     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.SScaled)
    let Rgb32f      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.Float)

    let Rgba32      = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.Unknown)
    let Rgba32ui    = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.UInt)
    let Rgba32i     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.SInt)
    let Rgba32n     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.UNorm)
    let Rgba32sn    = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.SNorm)
    let Rgba32s     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.UScaled)
    let Rgba32ss    = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.SScaled)
    let Rgba32f     = ImageFormat(ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelFormat.A32, ChannelType.Float)

    let Bgr32       = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.Unknown)
    let Bgr32ui     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.UInt)
    let Bgr32i      = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.SInt)
    let Bgr32n      = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.UNorm)
    let Bgr32sn     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.SNorm)
    let Bgr32s      = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.UScaled)
    let Bgr32ss     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.SScaled)
    let Bgr32f      = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.Float)

    let Bgra32      = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.Unknown)
    let Bgra32ui    = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.UInt)
    let Bgra32i     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.SInt)
    let Bgra32n     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.UNorm)
    let Bgra32sn    = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.SNorm)
    let Bgra32s     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.UScaled)
    let Bgra32ss    = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.SScaled)
    let Bgra32f     = ImageFormat(ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelFormat.A32, ChannelType.Float)

    let Argb32      = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.Unknown)
    let Argb32ui    = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.UInt)
    let Argb32i     = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.SInt)
    let Argb32n     = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.UNorm)
    let Argb32sn    = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.SNorm)
    let Argb32s     = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.UScaled)
    let Argb32ss    = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.SScaled)
    let Argb32f     = ImageFormat(ChannelFormat.A32, ChannelFormat.R32, ChannelFormat.G32, ChannelFormat.B32, ChannelType.Float)

    let Abgr32      = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.Unknown)
    let Abgr32ui    = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.UInt)
    let Abgr32i     = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.SInt)
    let Abgr32n     = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.UNorm)
    let Abgr32sn    = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.SNorm)
    let Abgr32s     = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.UScaled)
    let Abgr32ss    = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.SScaled)
    let Abgr32f     = ImageFormat(ChannelFormat.A32, ChannelFormat.B32, ChannelFormat.G32, ChannelFormat.R32, ChannelType.Float)


    module LookupTable =
        open System.Collections.Generic

        let optionLookupTable (l : list<'a * 'b>) =
            let d = Dictionary()
            for (k,v) in l do

                match d.TryGetValue k with
                    | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                    | _ -> ()

                d.[k] <- v

            fun (key : 'a) ->
                match d.TryGetValue key with
                    | (true, v) -> Some v
                    | _ -> None


    module private ChannelSemantic =
        let ofColFormat =
            LookupTable.lookupTable [
                Col.Format.Alpha,       [ChannelSemantic.Alpha]
                Col.Format.Gray,        [ChannelSemantic.Red]
                Col.Format.GrayAlpha,   [ChannelSemantic.Red; ChannelSemantic.Green]
                Col.Format.NormalUV,    [ChannelSemantic.Red; ChannelSemantic.Green]
                Col.Format.RGB,         [ChannelSemantic.Red; ChannelSemantic.Green; ChannelSemantic.Blue]
                Col.Format.RGBA,        [ChannelSemantic.Red; ChannelSemantic.Green; ChannelSemantic.Blue; ChannelSemantic.Alpha]
                Col.Format.RGBP,        [ChannelSemantic.Red; ChannelSemantic.Green; ChannelSemantic.Blue; ChannelSemantic.Alpha]
                Col.Format.BW,          [ChannelSemantic.Red]
                Col.Format.BGR,         [ChannelSemantic.Blue; ChannelSemantic.Green; ChannelSemantic.Red]
                Col.Format.BGRA,        [ChannelSemantic.Blue; ChannelSemantic.Green; ChannelSemantic.Red;  ChannelSemantic.Alpha]
                Col.Format.BGRP,        [ChannelSemantic.Blue; ChannelSemantic.Green; ChannelSemantic.Red;  ChannelSemantic.Alpha]
                Col.Format.None,        []
            ]

    module private ChannelType =
        let ofType =
            LookupTable.lookupTable [
                typeof<uint8>,      ChannelType.Unknown
                typeof<int8>,       ChannelType.SInt
                typeof<uint16>,     ChannelType.UInt
                typeof<int16>,      ChannelType.SInt
                typeof<uint32>,     ChannelType.UInt
                typeof<int32>,      ChannelType.SInt
                typeof<uint64>,     ChannelType.UInt
                typeof<int64>,      ChannelType.SInt
                typeof<unativeint>, ChannelType.UInt
                typeof<nativeint>,  ChannelType.SInt
                typeof<float16>,    ChannelType.Float
                typeof<float32>,    ChannelType.Float
                typeof<float>,      ChannelType.Float
            ]

        let toType =
            LookupTable.optionLookupTable [
                (ChannelType.Unknown, 8), typeof<uint8>
                (ChannelType.UInt, 8), typeof<uint8>
                (ChannelType.SInt, 8), typeof<int8>
                
                (ChannelType.Unknown, 16), typeof<uint16>
                (ChannelType.UInt, 16), typeof<uint16>
                (ChannelType.SInt, 16), typeof<int16>
                (ChannelType.Float, 16), typeof<float16>
                
                (ChannelType.Unknown, 32), typeof<uint32>
                (ChannelType.UInt, 32), typeof<uint32>
                (ChannelType.SInt, 32), typeof<int32>
                (ChannelType.Float, 32), typeof<float32>
                
                (ChannelType.Unknown, 64), typeof<uint64>
                (ChannelType.UInt, 64), typeof<uint64>
                (ChannelType.SInt, 64), typeof<int64>
                (ChannelType.Float, 64), typeof<float>
            ]

    module private ChannelSize =
        let ofType =
            LookupTable.lookupTable [
                typeof<uint8>,      8
                typeof<int8>,       8
                typeof<uint16>,     16
                typeof<int16>,      16
                typeof<uint32>,     32
                typeof<int32>,      32
                typeof<uint64>,     64
                typeof<int64>,      64
                typeof<unativeint>, sizeof<unativeint>
                typeof<nativeint>,  sizeof<nativeint>
                typeof<float16>,    16
                typeof<float32>,    32
                typeof<float>,      64
            ]

    let ofPixFormat (fmt : PixFormat) =
        let t = ChannelType.ofType fmt.Type
        let bits = ChannelSize.ofType fmt.Type
        let sems = ChannelSemantic.ofColFormat fmt.Format
        let channels = sems |> List.map (fun s -> ChannelFormat(s, bits))
        ImageFormat(channels, t)

    let getColFormat (i : ImageFormat) =
        match i.Channels with
            | 0 -> Col.Format.None
            | 1 -> Col.Format.Gray
            | 2 ->
                match i.C0.Semantic with
                    | ChannelSemantic.Red -> Col.Format.NormalUV
                    | _ -> Col.Format.GrayAlpha

            | 3 ->
                match i.C0.Semantic, i.C1.Semantic, i.C2.Semantic with
                    | ChannelSemantic.Red, ChannelSemantic.Green, ChannelSemantic.Blue -> Col.Format.RGB
                    | ChannelSemantic.Blue, ChannelSemantic.Green, ChannelSemantic.Red -> Col.Format.BGR
                    | _ -> Col.Format.None
            | _ ->
                match i.C0.Semantic, i.C1.Semantic, i.C2.Semantic, i.C3.Semantic with
                    | ChannelSemantic.Red, ChannelSemantic.Green, ChannelSemantic.Blue, ChannelSemantic.Alpha -> Col.Format.RGBA
                    | ChannelSemantic.Blue, ChannelSemantic.Green, ChannelSemantic.Red, ChannelSemantic.Alpha -> Col.Format.BGRA
                    | _ -> Col.Format.None

    let tryGetChannelType (fmt : ImageFormat) =
        let sizes = List.init fmt.Channels (fun i -> fmt.[i].SizeInBits) |> Set.ofList
        if Set.count sizes = 1 then
            let s = Seq.head sizes
            ChannelType.toType (fmt.Type, s)
        else
            None

    let tryGetPixFormat (fmt : ImageFormat) =
        match tryGetChannelType fmt with
            | Some t ->
                let format = getColFormat fmt
                PixFormat(t, format) |> Some
            | None ->
                None

    let toPixFormat (fmt : ImageFormat) =
        fmt |> tryGetPixFormat |> Option.get
