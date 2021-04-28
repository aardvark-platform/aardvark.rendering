namespace Aardvark.GPGPU

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Rendering

#nowarn "9"
#nowarn "51"

   
[<StructuredFormatDisplay("{AsString}")>]
type Quantization =
    {
        luminance : int[]
        chroma : int[]
        table : V4i[]
    } with

    override x.ToString() =
        x.AsString

    member private x.AsString =
        let padToWidth (w : int) (s : string) =
            if s.Length < w then
                System.String(' ', w - s.Length) + s
            else
                s
        let lumLines = x.luminance |> Seq.map (string >> padToWidth 2) |> Seq.chunkBySize 8 |> Seq.map (String.concat "; " >> sprintf "    %s") |> String.concat "\r\n"
        let chromaLines = x.chroma |> Seq.map (string >> padToWidth 2) |> Seq.chunkBySize 8 |> Seq.map (String.concat "; " >> sprintf "    %s") |> String.concat "\r\n"
    

        sprintf "{\r\n  luminance: [|\r\n%s\r\n  |]\r\n  chroma: [|\r\n%s\r\n  |]\r\n}" lumLines chromaLines

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Quantization =
    let private qBaseLuminance =
        [|
            16;   11;   10;   16;   24;   40;   51;   61;
            12;   12;   14;   19;   26;   58;   60;   55;
            14;   13;   16;   24;   40;   57;   69;   56;
            14;   17;   22;   29;   51;   87;   80;   62;
            18;   22;   37;   56;   68;  109;  103;   77;
            24;   35;   55;   64;   81;  104;  113;   92;
            49;   64;   78;   87;  103;  121;  120;  101;
            72;   92;   95;   98;  112;  100;  103;   99;
        |]

    let private qBaseChroma =
        [|
            17; 18; 24; 47; 99; 99; 99; 99
            18; 21; 26; 66; 99; 99; 99; 99
            24; 26; 56; 99; 99; 99; 99; 99
            47; 66; 99; 99; 99; 99; 99; 99
            99; 99; 99; 99; 99; 99; 99; 99
            99; 99; 99; 99; 99; 99; 99; 99
            99; 99; 99; 99; 99; 99; 99; 99
            99; 99; 99; 99; 99; 99; 99; 99
        |]

    let private getQLuminance (q : float) =
        let q = q |> max 1.0 |> min 100.0
        let s = if q < 50.0 then 5000.0 / q else 200.0 - 2.0 * q
        qBaseLuminance |> Array.map (fun v ->
            floor ((s * float v + 50.0) / 100.0) |> int  |> max 1
        )  

    let private getQChroma (q : float) =
        let q = q |> max 1.0 |> min 100.0
        let s = if q < 50.0 then 5000.0 / q else 200.0 - 2.0 * q
        qBaseChroma |> Array.map (fun v ->
            floor ((s * float v + 50.0) / 100.0) |> int  |> max 1
        )  
        
    let create (l : int[]) (c : int[]) =
        {
            luminance = l
            chroma = c
            table =         
                Array.init 64 (fun i ->
                    V4i(
                        l.[i],
                        c.[i],
                        c.[i],
                        0
                    )
                )
        }
         
    let ofQuality (quality : float) =
        let v = getQLuminance quality
        create (getQLuminance quality) (getQChroma quality)
 
    let upsample (q : int[]) =
        Array.init 64 (fun i ->
            let cf = V2d(i % 8, i / 8) / 2.0
            let c00 = V2i cf
            let c10 = V2i.IO + c00
            let c01 = V2i.OI + c00
            let c11 = V2i.OI + c00

            let q00 = float q.[8*c00.Y + c00.X]
            let q01 = float q.[8*c01.Y + c01.X]
            let q10 = float q.[8*c10.Y + c10.X]
            let q11 = float q.[8*c11.Y + c11.X]

            let f = cf - V2d c00
            let qx0 = q00 * (1.0 - f.X) + q10 * f.X
            let qx1 = q01 * (1.0 - f.X) + q11 * f.X
            let q = qx0 * (1.0 - f.Y) + qx1 * f.Y

            int q
        )
            
    let scale2 (q : Quantization) =
        { q with chroma = upsample q.chroma }

    let photoshop10 =
        create
            [|
                20; 16; 25; 39; 50; 46; 62; 68
                16; 18; 23; 38; 38; 53; 65; 68
                25; 23; 31; 38; 53; 65; 68; 68
                39; 38; 38; 53; 65; 68; 68; 68
                50; 38; 53; 65; 68; 68; 68; 68
                46; 53; 65; 68; 68; 68; 68; 68
                62; 65; 68; 68; 68; 68; 68; 68
                68; 68; 68; 68; 68; 68; 68; 68
            |]
            [|
                21; 25; 32; 38; 54; 68; 68; 68
                25; 28; 24; 38; 54; 68; 68; 68
                32; 24; 32; 43; 66; 68; 68; 68
                38; 38; 43; 53; 68; 68; 68; 68
                54; 54; 66; 68; 68; 68; 68; 68
                68; 68; 68; 68; 68; 68; 68; 68
                68; 68; 68; 68; 68; 68; 68; 68
                68; 68; 68; 68; 68; 68; 68; 68

            |]
    
    let photoshop20 =
        create
            [|
                18; 14; 14; 21; 30; 35; 34; 39
                14; 16; 16; 19; 26; 24; 30; 39
                14; 16; 17; 21; 24; 34; 46; 62
                21; 19; 21; 26; 33; 48; 62; 65
                30; 26; 24; 33; 51; 65; 65; 65
                35; 24; 34; 48; 65; 65; 65; 65
                34; 30; 46; 62; 65; 65; 65; 65
                39; 39; 62; 65; 65; 65; 65; 65
            |]
            [|
                20; 19; 22; 27; 26; 33; 49; 62
                19; 25; 23; 22; 26; 33; 45; 56
                22; 23; 26; 29; 33; 39; 59; 65
                27; 22; 29; 36; 39; 51; 65; 65
                26; 26; 33; 39; 51; 62; 65; 65
                33; 33; 39; 51; 62; 65; 65; 65
                49; 45; 59; 65; 65; 65; 65; 65
                62; 56; 65; 65; 65; 65; 65; 65
            |]
    
    let photoshop30 =
        create
            [|
                16; 11; 11; 16; 23; 27; 31; 30
                11; 12; 12; 15; 20; 23; 23; 30
                11; 12; 13; 16; 23; 26; 35; 47
                16; 15; 16; 23; 26; 37; 47; 64
                23; 20; 23; 26; 39; 51; 64; 64
                27; 23; 26; 37; 51; 64; 64; 64
                31; 23; 35; 47; 64; 64; 64; 64
                30; 30; 47; 64; 64; 64; 64; 64
            |]
            [|
                17; 15; 17; 21; 20; 26; 38; 48
                15; 19; 18; 17; 20; 26; 35; 43
                17; 18; 20; 22; 26; 30; 46; 53
                21; 17; 22; 28; 30; 39; 53; 64
                20; 20; 26; 30; 39; 48; 64; 64
                26; 26; 30; 39; 48; 63; 64; 64
                38; 35; 46; 53; 64; 64; 64; 64
                48; 43; 53; 64; 64; 64; 64; 64
            |]
    
    let photoshop40 =
        create
            [|
                12; 8;  8;  12; 17; 21; 24; 23
                8;  9;  9;  11; 15; 19; 18; 23
                8;  9;  10; 12; 19; 20; 27; 36
                12; 11; 12; 21; 20; 28; 36; 53
                17; 15; 19; 20; 30; 39; 51; 59
                21; 19; 20; 28; 39; 51; 59; 59
                24; 18; 27; 36; 51; 59; 59; 59
                23; 23; 36; 53; 59; 59; 59; 59
            |]
            [|
                17; 15; 17; 21; 20; 26; 38; 48
                15; 19; 18; 17; 20; 26; 35; 43
                17; 18; 20; 22; 26; 30; 46; 53
                21; 17; 22; 28; 30; 39; 53; 64
                20; 20; 26; 30; 39; 48; 64; 64
                26; 26; 30; 39; 48; 63; 64; 64
                38; 35; 46; 53; 64; 64; 64; 64
                48; 43; 53; 64; 64; 64; 64; 64
            |]

    let photoshop50 =
        create
            [|
                8;  6;  6;  8;  12; 14; 16; 17
                6;  6;  6;  8;  10; 13; 12; 15
                6;  6;  7;  8;  13; 14; 18; 24
                8;  8;  8;  14; 13; 19; 24; 35
                12; 10; 13; 13; 20; 26; 34; 39
                14; 13; 14; 19; 26; 34; 39; 39
                16; 12; 18; 24; 34; 39; 39; 39
                17; 15; 24; 35; 39; 39; 39; 39
            |]
            [|
                9;  8;  9;  11; 14; 17; 19; 24
                8;  10; 9;  11; 14; 13; 17; 22
                9;  9;  13; 14; 13; 15; 23; 26
                11; 11; 14; 14; 15; 20; 26; 33
                14; 14; 13; 15; 20; 24; 33; 39
                17; 13; 15; 20; 24; 32; 39; 39
                19; 17; 23; 26; 33; 39; 39; 39
                24; 22; 26; 33; 39; 39; 39; 39
            |]

    // no subsampling from here
    let photoshop51 =
        create
            [|
                8;  5;  5;  8;  11; 13; 15; 17
                5;  6;  6;  7;  10; 12; 12; 15
                5;  6;  6;  8;  12; 13; 17; 23
                8;  7;  8;  13; 13; 18; 23; 34
                11; 10; 12; 13; 19; 25; 33; 38
                13; 12; 13; 18; 25; 33; 38; 38
                15; 12; 17; 23; 33; 38; 38; 38
                17; 15; 23; 34; 38; 38; 38; 38
            |]
            [|
                8;  9;  16; 29; 32; 38; 38; 38
                9;  14; 20; 26; 38; 38; 38; 38
                16; 20; 21; 38; 38; 38; 38; 38
                29; 26; 38; 38; 38; 38; 38; 38
                32; 38; 38; 38; 38; 38; 38; 38
                38; 38; 38; 38; 38; 38; 38; 38
                38; 38; 38; 38; 38; 38; 38; 38
                38; 38; 38; 38; 38; 38; 38; 38

            |]

    let photoshop60 =
        create
            [|
                6;  4;  4;  6;  9; 11; 12; 16
                4;  5;  5;  6;  8; 10; 12; 12
                4;  5;  5;  6;  10; 12; 14; 19
                6;  6;  6;  11; 12; 15; 19; 28
                9;  8;  10; 12; 16; 20; 27; 31
                11; 10; 12; 15; 20; 27; 31; 31
                12; 12; 14; 19; 27; 31; 31; 31
                16; 12; 19; 28; 31; 31; 31; 31
            |]
            [|
                7;  7;  13; 24; 26; 31; 31; 31
                7;  12; 16; 21; 31; 31; 31; 31
                13; 16; 17; 31; 31; 31; 31; 31
                24; 21; 31; 31; 31; 31; 31; 31
                26; 31; 31; 31; 31; 31; 31; 31
                31; 31; 31; 31; 31; 31; 31; 31
                31; 31; 31; 31; 31; 31; 31; 31
                31; 31; 31; 31; 31; 31; 31; 31

            |]

    let photoshop70 =
        create
            [|
                4;  3;  3;  4;  6;  7;  8;  10
                3;  3;  3;  4;  5;  6;  8;  10
                3;  3;  3;  4;  6;  9;  12; 12
                4;  4;  4;  7;  9;  12; 12; 17
                6;  5;  6;  9;  12; 13; 17; 20
                7;  6;  9;  12; 13; 17; 20; 20
                8;  8;  12; 12; 17; 20; 20; 20
                10; 10; 12; 17; 20; 20; 20; 20
            |]
            [|
                4;  5;  8;  15; 20; 20; 20; 20
                5;  7;  10; 14; 20; 20; 20; 20
                8;  10; 14; 20; 20; 20; 20; 20
                15; 14; 20; 20; 20; 20; 20; 20
                20; 20; 20; 20; 20; 20; 20; 20
                20; 20; 20; 20; 20; 20; 20; 20
                20; 20; 20; 20; 20; 20; 20; 20
                20; 20; 20; 20; 20; 20; 20; 20

            |]

    let photoshop80 =
        create
            [|
                2;  2;  2;  2;  3;  4;  5;  6;
                2;  2;  2;  2;  3;  4;  5;  6;
                2;  2;  2;  2;  4;  5;  7;  9;
                2;  2;  2;  4;  5;  7;  9; 12;
                3;  3;  4;  5;  8; 10; 12; 12;
                4;  4;  5;  7; 10; 12; 12; 12;
                5;  5;  7;  9; 12; 12; 12; 12;
                6;  6;  9; 12; 12; 12; 12; 12;
            |]
            [|
                    3;  3;  5;  9; 13; 15; 15; 15;
                    3;  4;  6; 11; 14; 12; 12; 12;
                    5;  6;  9; 14; 12; 12; 12; 12;
                    9; 11; 14; 12; 12; 12; 12; 12;
                13; 14; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
                15; 12; 12; 12; 12; 12; 12; 12;
            |]

    let photoshop90 =
        create
            [|
                1; 1; 1; 1; 2; 2; 2; 3
                1; 1; 1; 1; 2; 2; 2; 3
                1; 1; 1; 1; 2; 3; 4; 5
                1; 1; 1; 2; 3; 4; 5; 7
                2; 2; 2; 3; 4; 5; 7; 8
                2; 2; 3; 4; 5; 7; 8; 8
                2; 2; 4; 5; 7; 8; 8; 8
                3; 3; 5; 7; 8; 8; 8; 8
            |]
            [|
                1; 1; 2; 5; 7; 8; 8; 8
                1; 2; 3; 5; 8; 8; 8; 8
                2; 3; 4; 8; 8; 8; 8; 8
                5; 5; 8; 8; 8; 8; 8; 8
                7; 8; 8; 8; 8; 8; 8; 8
                8; 8; 8; 8; 8; 8; 8; 8
                8; 8; 8; 8; 8; 8; 8; 8
                8; 8; 8; 8; 8; 8; 8; 8
            |]

    let photoshop100 =
        create
            [|
                1; 1; 1; 1; 1; 1; 1; 1
                1; 1; 1; 1; 1; 1; 1; 1
                1; 1; 1; 1; 1; 1; 1; 2
                1; 1; 1; 1; 1; 1; 2; 2
                1; 1; 1; 1; 1; 2; 2; 3
                1; 1; 1; 1; 2; 2; 3; 3
                1; 1; 1; 2; 2; 3; 3; 3
                1; 1; 2; 2; 3; 3; 3; 3
            |]
            [|
                1; 1; 1; 2; 2; 3; 3; 3
                1; 1; 1; 2; 3; 3; 3; 3
                1; 1; 1; 3; 3; 3; 3; 3
                2; 2; 3; 3; 3; 3; 3; 3
                2; 3; 3; 3; 3; 3; 3; 3
                3; 3; 3; 3; 3; 3; 3; 3
                3; 3; 3; 3; 3; 3; 3; 3
                3; 3; 3; 3; 3; 3; 3; 3
            |]

    let toTable (q : Quantization) =
        q.table


[<AutoOpen>]
module private JpegHelpers =
    type Codeword = uint32

    [<ReflectedDefinition>]
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Codeword =
        let length (c : Codeword) = int (c >>> 24)
        let code (c : Codeword) = c &&& 0x00FFFFFFu

        let empty : Codeword = 0u

        let create (length : int) (code : uint32) : Codeword =
            assert (length >= 0 && length <= 24)
            (uint32 length <<< 24) ||| (code &&& ((1u <<< length) - 1u))
      
        let toString (w : Codeword) =
            let len = length w
            if len = 0 then
                "(0)"
            else
                let mutable str = sprintf "(%d:" len
                let mutable mask = 1u <<< (len - 1)
                for i in 1 .. len do
                    if w &&& mask <> 0u then str <- str + "1"
                    else str <- str + "0"
                    mask <- mask >>> 1
                str + ")"

        let take (n : int) (w : Codeword) : Codeword =
            if n <= 0 then
                0u
            else
                let len = length w
                if n >= len then 
                    w
                else
                    let code = code w >>> (len - n)
                    (uint32 n <<< 24) ||| code

        let skip (n : int) (w : Codeword) : Codeword =
            if n <= 0 then
                w
            else
                let len = length w
                if n >= len then
                    0u
                else
                    let l = len - n
                    let lMask = (1u <<< l) - 1u

                    let code = code w // 0x00FAAD
                    let code = (code &&& lMask) /// 0x000AAD
                    (uint32 l <<< 24) ||| code

        let append (l : Codeword) (r : Codeword) =
            let ll = length l
            let rl = length r
            let len = ll + rl
            assert(len <= 24)
            let lc = code l
            let rc = code r
            let code = (lc <<< rl) ||| rc
            (uint32 len <<< 24) ||| code

        let appendBit (b : bool) (l : Codeword) =
            let ll = length l
            let len = ll + 1
            assert(len <= 24)
            let lc = code l
            let code =
                if b then (lc <<< 1) ||| 1u
                else (lc <<< 1)
            (uint32 len <<< 24) ||| code
        
        let toByteArray (w : Codeword) =
            let mutable len = length w
            let code = (code w) <<< (32 - len)

            let mutable mask = 0xFF000000u
            let mutable shift = 24

            [| 
                while len > 0 do
                    let v = (code &&& mask) >>> shift |> byte
                    yield v
                    len <- len - 8
                    shift <- shift - 8
                    mask <- mask >>> 8
            |]
            

    type HuffmanTable =
        {
            counts : int[]
            values : byte[]
            table : Codeword[]
            decode : int -> uint32 -> byte * uint32
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module HuffmanTable =
        [<AutoOpen>]
        module private Helpers = 
        
            type HuffTree =
                | Empty
                | Leaf of byte
                | Node of HuffTree * HuffTree

            let rec private zipper (l : list<HuffTree>) =
                match l with
                    | [] -> []
                    | a :: b :: rest ->
                        Node(a,b) :: zipper rest
                    | [a] ->
                        [Node(a,Empty)]

            let build (counts : int[]) (values : byte[]) : HuffTree =
                let mutable currentValue = 0

                let rec build (level : int) (n : int) : list<HuffTree> =
                    if n <= 0 then
                        []
                    else
                        let cnt = counts.[level]

                        let leafs = 
                            List.init cnt (fun _ -> 
                                let i = currentValue
                                currentValue <- i + 1
                                Leaf values.[i]
                            )

                        let nodes =
                            if level >= counts.Length - 1 then
                                []
                            else
                                let nodeCount = n - cnt
                                build (level + 1) (2 * nodeCount) |> zipper

                        let res = leafs @ nodes
            
                        res

                match build 0 1 with
                    | [n] -> n
                    | _ -> failwith "magic"


            let createEncodeTable (values : byte[]) (tree : HuffTree) =
                let max = values |> Array.max |> int

                let arr = Array.zeroCreate (1 + max)

                let rec traverse (path : Codeword) (t : HuffTree) =
                    match t with
                        | Empty -> ()
                        | Leaf v -> arr.[int v] <- path
                        | Node(l,r) ->
                            traverse (Codeword.appendBit false path) l
                            traverse (Codeword.appendBit true path) r

                traverse Codeword.empty tree
                arr  

        let inline counts (t : HuffmanTable) = t.counts
        let inline values (t : HuffmanTable) = t.values
        let inline table (t : HuffmanTable) = t.table

        let create (counts : int[]) (values : byte[]) =
            let tree = build counts values

            let rec decode (t : HuffTree) (len : int) (code : uint32) =
                match t with
                    | Leaf(dc) -> 
                        let v = code >>> (32 - len)
                        dc, v
                    | Empty -> 
                        failwith ""
                    | Node(l,r) ->
                        let b = (code >>> 31) &&& 1u
                        if b = 1u then decode r (len - 1) (code <<< 1)
                        else decode l (len - 1) (code <<< 1)

            //let code = code <<< (32 - len)



            {
                counts = counts
                values = values
                table = createEncodeTable values tree
                decode = fun len code -> decode tree len (code <<< (32 - len))
            }

        let encode (word : byte) (table : HuffmanTable) =
            table.table.[int word]


    type HuffmanEncoder =
        {
            dcLuminance     : HuffmanTable
            acLuminance     : HuffmanTable
            dcChroma        : HuffmanTable
            acChroma        : HuffmanTable
        }

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module HuffmanEncoder =
    
        let turboJpeg =
            {
                dcLuminance =
                    HuffmanTable.create 
                        [| 0; 0; 1; 5; 1; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0 |]
                        [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy; 10uy; 11uy |]

                dcChroma =
                    HuffmanTable.create
                        [| 0; 0; 3; 1; 1; 1; 1; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0 |]
                        [| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy; 9uy; 10uy; 11uy |]

                acLuminance =
                    HuffmanTable.create
                        [|  0; 0; 2; 1; 3; 3; 2; 4; 3; 5; 5; 4; 4; 0; 0; 1; 0x7d |]
                        [|
                            0x01uy; 0x02uy; 0x03uy; 0x00uy; 0x04uy; 0x11uy; 0x05uy; 0x12uy;
                            0x21uy; 0x31uy; 0x41uy; 0x06uy; 0x13uy; 0x51uy; 0x61uy; 0x07uy;
                            0x22uy; 0x71uy; 0x14uy; 0x32uy; 0x81uy; 0x91uy; 0xa1uy; 0x08uy;
                            0x23uy; 0x42uy; 0xb1uy; 0xc1uy; 0x15uy; 0x52uy; 0xd1uy; 0xf0uy;
                            0x24uy; 0x33uy; 0x62uy; 0x72uy; 0x82uy; 0x09uy; 0x0auy; 0x16uy;
                            0x17uy; 0x18uy; 0x19uy; 0x1auy; 0x25uy; 0x26uy; 0x27uy; 0x28uy;
                            0x29uy; 0x2auy; 0x34uy; 0x35uy; 0x36uy; 0x37uy; 0x38uy; 0x39uy;
                            0x3auy; 0x43uy; 0x44uy; 0x45uy; 0x46uy; 0x47uy; 0x48uy; 0x49uy;
                            0x4auy; 0x53uy; 0x54uy; 0x55uy; 0x56uy; 0x57uy; 0x58uy; 0x59uy;
                            0x5auy; 0x63uy; 0x64uy; 0x65uy; 0x66uy; 0x67uy; 0x68uy; 0x69uy;
                            0x6auy; 0x73uy; 0x74uy; 0x75uy; 0x76uy; 0x77uy; 0x78uy; 0x79uy;
                            0x7auy; 0x83uy; 0x84uy; 0x85uy; 0x86uy; 0x87uy; 0x88uy; 0x89uy;
                            0x8auy; 0x92uy; 0x93uy; 0x94uy; 0x95uy; 0x96uy; 0x97uy; 0x98uy;
                            0x99uy; 0x9auy; 0xa2uy; 0xa3uy; 0xa4uy; 0xa5uy; 0xa6uy; 0xa7uy;
                            0xa8uy; 0xa9uy; 0xaauy; 0xb2uy; 0xb3uy; 0xb4uy; 0xb5uy; 0xb6uy;
                            0xb7uy; 0xb8uy; 0xb9uy; 0xbauy; 0xc2uy; 0xc3uy; 0xc4uy; 0xc5uy;
                            0xc6uy; 0xc7uy; 0xc8uy; 0xc9uy; 0xcauy; 0xd2uy; 0xd3uy; 0xd4uy;
                            0xd5uy; 0xd6uy; 0xd7uy; 0xd8uy; 0xd9uy; 0xdauy; 0xe1uy; 0xe2uy;
                            0xe3uy; 0xe4uy; 0xe5uy; 0xe6uy; 0xe7uy; 0xe8uy; 0xe9uy; 0xeauy;
                            0xf1uy; 0xf2uy; 0xf3uy; 0xf4uy; 0xf5uy; 0xf6uy; 0xf7uy; 0xf8uy;
                            0xf9uy; 0xfauy
                        |]

                acChroma = 
                    HuffmanTable.create
                        [| 0; 0; 2; 1; 2; 4; 4; 3; 4; 7; 5; 4; 4; 0; 1; 2; 0x77 |]
                        [|
                            0x00uy; 0x01uy; 0x02uy; 0x03uy; 0x11uy; 0x04uy; 0x05uy; 0x21uy;
                            0x31uy; 0x06uy; 0x12uy; 0x41uy; 0x51uy; 0x07uy; 0x61uy; 0x71uy;
                            0x13uy; 0x22uy; 0x32uy; 0x81uy; 0x08uy; 0x14uy; 0x42uy; 0x91uy;
                            0xa1uy; 0xb1uy; 0xc1uy; 0x09uy; 0x23uy; 0x33uy; 0x52uy; 0xf0uy;
                            0x15uy; 0x62uy; 0x72uy; 0xd1uy; 0x0auy; 0x16uy; 0x24uy; 0x34uy;
                            0xe1uy; 0x25uy; 0xf1uy; 0x17uy; 0x18uy; 0x19uy; 0x1auy; 0x26uy;
                            0x27uy; 0x28uy; 0x29uy; 0x2auy; 0x35uy; 0x36uy; 0x37uy; 0x38uy;
                            0x39uy; 0x3auy; 0x43uy; 0x44uy; 0x45uy; 0x46uy; 0x47uy; 0x48uy;
                            0x49uy; 0x4auy; 0x53uy; 0x54uy; 0x55uy; 0x56uy; 0x57uy; 0x58uy;
                            0x59uy; 0x5auy; 0x63uy; 0x64uy; 0x65uy; 0x66uy; 0x67uy; 0x68uy;
                            0x69uy; 0x6auy; 0x73uy; 0x74uy; 0x75uy; 0x76uy; 0x77uy; 0x78uy;
                            0x79uy; 0x7auy; 0x82uy; 0x83uy; 0x84uy; 0x85uy; 0x86uy; 0x87uy;
                            0x88uy; 0x89uy; 0x8auy; 0x92uy; 0x93uy; 0x94uy; 0x95uy; 0x96uy;
                            0x97uy; 0x98uy; 0x99uy; 0x9auy; 0xa2uy; 0xa3uy; 0xa4uy; 0xa5uy;
                            0xa6uy; 0xa7uy; 0xa8uy; 0xa9uy; 0xaauy; 0xb2uy; 0xb3uy; 0xb4uy;
                            0xb5uy; 0xb6uy; 0xb7uy; 0xb8uy; 0xb9uy; 0xbauy; 0xc2uy; 0xc3uy;
                            0xc4uy; 0xc5uy; 0xc6uy; 0xc7uy; 0xc8uy; 0xc9uy; 0xcauy; 0xd2uy;
                            0xd3uy; 0xd4uy; 0xd5uy; 0xd6uy; 0xd7uy; 0xd8uy; 0xd9uy; 0xdauy;
                            0xe2uy; 0xe3uy; 0xe4uy; 0xe5uy; 0xe6uy; 0xe7uy; 0xe8uy; 0xe9uy;
                            0xeauy; 0xf2uy; 0xf3uy; 0xf4uy; 0xf5uy; 0xf6uy; 0xf7uy; 0xf8uy;
                            0xf9uy; 0xfauy
                        |]
         
            }

        let photoshop =
            {
                dcLuminance =
                    HuffmanTable.create
                        [| 0; 0; 0; 7; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0; 0 |]
                        [| 0x04uy; 0x05uy; 0x03uy; 0x02uy; 0x06uy; 0x01uy; 0x00uy; 0x07uy; 0x08uy; 0x09uy; 0x0Auy; 0x0Buy |]
                        
                dcChroma =
                    HuffmanTable.create
                        [| 0; 0; 2; 2; 3; 1; 1; 1; 1; 1; 0; 0; 0; 0; 0; 0; 0 |]
                        [| 0x01uy; 0x00uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy; 0x09uy; 0x0Auy; 0x0Buy |]

                acLuminance = 
                    HuffmanTable.create 
                        [|  0; 0; 2; 1; 3; 3; 2; 4; 2; 6; 7; 3; 4; 2; 6; 2; 115 |]
                        [|
                            0x01uy; 0x02uy; 0x03uy; 0x11uy; 0x04uy; 0x00uy; 0x05uy; 0x21uy
                            0x12uy; 0x31uy; 0x41uy; 0x51uy; 0x06uy; 0x13uy; 0x61uy; 0x22uy
                            0x71uy; 0x81uy; 0x14uy; 0x32uy; 0x91uy; 0xA1uy; 0x07uy; 0x15uy
                            0xB1uy; 0x42uy; 0x23uy; 0xC1uy; 0x52uy; 0xD1uy; 0xE1uy; 0x33uy
                            0x16uy; 0x62uy; 0xF0uy; 0x24uy; 0x72uy; 0x82uy; 0xF1uy; 0x25uy
                            0x43uy; 0x34uy; 0x53uy; 0x92uy; 0xA2uy; 0xB2uy; 0x63uy; 0x73uy
                            0xC2uy; 0x35uy; 0x44uy; 0x27uy; 0x93uy; 0xA3uy; 0xB3uy; 0x36uy
                            0x17uy; 0x54uy; 0x64uy; 0x74uy; 0xC3uy; 0xD2uy; 0xE2uy; 0x08uy
                            0x26uy; 0x83uy; 0x09uy; 0x0Auy; 0x18uy; 0x19uy; 0x84uy; 0x94uy
                            0x45uy; 0x46uy; 0xA4uy; 0xB4uy; 0x56uy; 0xD3uy; 0x55uy; 0x28uy
                            0x1Auy; 0xF2uy; 0xE3uy; 0xF3uy; 0xC4uy; 0xD4uy; 0xE4uy; 0xF4uy
                            0x65uy; 0x75uy; 0x85uy; 0x95uy; 0xA5uy; 0xB5uy; 0xC5uy; 0xD5uy
                            0xE5uy; 0xF5uy; 0x66uy; 0x76uy; 0x86uy; 0x96uy; 0xA6uy; 0xB6uy
                            0xC6uy; 0xD6uy; 0xE6uy; 0xF6uy; 0x37uy; 0x47uy; 0x57uy; 0x67uy
                            0x77uy; 0x87uy; 0x97uy; 0xA7uy; 0xB7uy; 0xC7uy; 0xD7uy; 0xE7uy
                            0xF7uy; 0x38uy; 0x48uy; 0x58uy; 0x68uy; 0x78uy; 0x88uy; 0x98uy
                            0xA8uy; 0xB8uy; 0xC8uy; 0xD8uy; 0xE8uy; 0xF8uy; 0x29uy; 0x39uy
                            0x49uy; 0x59uy; 0x69uy; 0x79uy; 0x89uy; 0x99uy; 0xA9uy; 0xB9uy
                            0xC9uy; 0xD9uy; 0xE9uy; 0xF9uy; 0x2Auy; 0x3Auy; 0x4Auy; 0x5Auy
                            0x6Auy; 0x7Auy; 0x8Auy; 0x9Auy; 0xAAuy; 0xBAuy; 0xCAuy; 0xDAuy
                            0xEAuy; 0xFAuy
                        |]

                acChroma =
                    HuffmanTable.create
                        [| 0; 0; 2; 2; 1; 2; 3; 5; 5; 4; 5; 6; 4; 8; 3; 3; 109 |]
                        [|
                            0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x04uy; 0x21uy; 0x12uy
                            0x31uy; 0x41uy; 0x05uy; 0x51uy; 0x13uy; 0x61uy; 0x22uy; 0x06uy
                            0x71uy; 0x81uy; 0x91uy; 0x32uy; 0xA1uy; 0xB1uy; 0xF0uy; 0x14uy
                            0xC1uy; 0xD1uy; 0xE1uy; 0x23uy; 0x42uy; 0x15uy; 0x52uy; 0x62uy
                            0x72uy; 0xF1uy; 0x33uy; 0x24uy; 0x34uy; 0x43uy; 0x82uy; 0x16uy
                            0x92uy; 0x53uy; 0x25uy; 0xA2uy; 0x63uy; 0xB2uy; 0xC2uy; 0x07uy
                            0x73uy; 0xD2uy; 0x35uy; 0xE2uy; 0x44uy; 0x83uy; 0x17uy; 0x54uy
                            0x93uy; 0x08uy; 0x09uy; 0x0Auy; 0x18uy; 0x19uy; 0x26uy; 0x36uy
                            0x45uy; 0x1Auy; 0x27uy; 0x64uy; 0x74uy; 0x55uy; 0x37uy; 0xF2uy
                            0xA3uy; 0xB3uy; 0xC3uy; 0x28uy; 0x29uy; 0xD3uy; 0xE3uy; 0xF3uy
                            0x84uy; 0x94uy; 0xA4uy; 0xB4uy; 0xC4uy; 0xD4uy; 0xE4uy; 0xF4uy
                            0x65uy; 0x75uy; 0x85uy; 0x95uy; 0xA5uy; 0xB5uy; 0xC5uy; 0xD5uy
                            0xE5uy; 0xF5uy; 0x46uy; 0x56uy; 0x66uy; 0x76uy; 0x86uy; 0x96uy
                            0xA6uy; 0xB6uy; 0xC6uy; 0xD6uy; 0xE6uy; 0xF6uy; 0x47uy; 0x57uy
                            0x67uy; 0x77uy; 0x87uy; 0x97uy; 0xA7uy; 0xB7uy; 0xC7uy; 0xD7uy
                            0xE7uy; 0xF7uy; 0x38uy; 0x48uy; 0x58uy; 0x68uy; 0x78uy; 0x88uy
                            0x98uy; 0xA8uy; 0xB8uy; 0xC8uy; 0xD8uy; 0xE8uy; 0xF8uy; 0x39uy
                            0x49uy; 0x59uy; 0x69uy; 0x79uy; 0x89uy; 0x99uy; 0xA9uy; 0xB9uy
                            0xC9uy; 0xD9uy; 0xE9uy; 0xF9uy; 0x2Auy; 0x3Auy; 0x4Auy; 0x5Auy
                            0x6Auy; 0x7Auy; 0x8Auy; 0x9Auy; 0xAAuy; 0xBAuy; 0xCAuy; 0xDAuy
                            0xEAuy; 0xFAuy
                        |]
            }

[<ReflectedDefinition>]
module private JpegKernels =
    open FShade

    module Constants = 
        let cosTable =
            Array.init 64 (fun i ->
                let m = i % 8
                let x = i / 8
                cos ( (2.0 * float m + 1.0) * Constant.Pi * float x / 16.0)
            )

        let y =  V4d(  0.299,       0.587,      0.114,     -128.0 )
        let cb = V4d( -0.168736,   -0.331264,   0.5,        0.0   )
        let cr = V4d(  0.5,        -0.418688,  -0.081312,   0.0   )

        let inverseZigZagOrder =
            [|
                0; 1; 5; 6; 14; 15; 27; 28
                2; 4; 7; 13; 16; 26; 29; 42
                3; 8; 12; 17; 25; 30; 41; 43
                9; 11; 18; 24; 31; 40; 44; 53
                10; 19; 23; 32; 39; 45; 52; 54
                20; 22; 33; 38; 46; 51; 55; 60
                21; 34; 37; 47; 50; 56; 59; 61
                35; 36; 48; 49; 57; 58; 62; 63
            |]
        let zigZagOrder =
            [|
                0;  1;  8;  16;  9;  2;  3; 10
                17; 24; 32; 25; 18; 11;  4;  5
                12; 19; 26; 33; 40; 48; 41; 34 
                27; 20; 13;  6;  7; 14; 21; 28
                35; 42; 49; 56; 57; 50; 43; 36
                29; 22; 15; 23; 30; 37; 44; 51
                58; 59; 52; 45; 38; 31; 39; 46
                53; 60; 61; 54; 47; 55; 62; 63
            |]

    type UniformScope with
        member x.Quantization: Arr<64 N, V4i> = uniform?Quantization
        member x.DCLum : Arr<12 N, uint32> = uniform?Encoder?DCLum
        member x.DCChroma : Arr<12 N, uint32> = uniform?Encoder?DCChroma
        member x.ACLum : Arr<255 N, uint32> = uniform?Encoder?ACLum
        member x.ACChroma : Arr<255 N, uint32> = uniform?Encoder?ACChroma

    let inputImage =
        sampler2d {
            texture uniform?InputImage
            filter Filter.MinMagLinear
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    [<Inline>]
    let ycbcr (v : V3d) =
        let v = 255.0 * v
        V3d(
            Vec.dot Constants.y (V4d(v, 1.0)),
            Vec.dot Constants.cb (V4d(v, 1.0)),
            Vec.dot Constants.cr (V4d(v, 1.0))
        )

    [<Inline>]
    let quantify (i : int) (v : V3d) =
        let ql = V3i.Max(V3i.III, uniform.Quantization.[i].XYZ)
        let t = v / V3d ql
        V3i(int (round t.X), int (round t.Y), int (round t.Z))
   
    [<Inline>]
    let dctFactor (m : int) (x : int) =
        Constants.cosTable.[m + 8 * x]

    [<Inline>]
    let add (l : V2i) (r : V2i) =
        if r.X <> 0 then
            V2i(l.X + r.X, r.Y)
        else
            if l.X <> 0 then
                V2i(l.X, l.Y)
            else
                V2i(0, r.Y)

    [<Inline>]
    let flipByteOrder (v : uint32) =
        let v = (v >>> 16) ||| (v <<< 16)
        ((v &&& 0xFF00FF00u) >>> 8) ||| ((v &&& 0x00FF00FFu) <<< 8)

    module Ballot = 
        [<GLSLIntrinsic("ballotARB({0})", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        let ballot (b : bool) : uint64 =
            onlyInShaderCode "ballot"

        [<GLSLIntrinsic("gl_SubGroupLtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        let lessMask() : uint64 =
            onlyInShaderCode "lessMask"

        [<GLSLIntrinsic("gl_SubGroupGtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        let greaterMask() : uint64 =
            onlyInShaderCode "greaterMask"
         
    module Tools =
        [<GLSLIntrinsic("findMSB({0})")>]
        let msb (v : uint32) =
            Fun.HighestBit (int v)

        [<GLSLIntrinsic("atomicOr({0}, {1})")>]
        let atomicOr (buf : uint32) (v : uint32) : unit =
            onlyInShaderCode "atomicOr"

        [<GLSLIntrinsic("memoryBarrier()")>]
        let memoryBarrier () : unit =
            onlyInShaderCode "memoryBarrier"
        
            
    let leadingIsLast (b : bool) =
        let scanSize = LocalSize.X
        let mem : V2i[] = allocateShared LocalSize.X
        let lid = getLocalId().X

        mem.[lid] <- V2i((if b then 1 else 0), lid)
        barrier()

        let mutable s = 1
        let mutable d = 2
        while d <= scanSize do
            if lid % d = 0 && lid >= s then
                mem.[lid] <- add mem.[lid - s] mem.[lid]

            barrier()
            s <- s <<< 1
            d <- d <<< 1

        d <- d >>> 1
        s <- s >>> 1
        while s >= 1 do
            if lid % d = 0 && lid + s < scanSize then
                mem.[lid + s] <- add mem.[lid] mem.[lid + s]
                    
            barrier()
            s <- s >>> 1
            d <- d >>> 1
              

        let vl = if lid > 0 then mem.[lid - 1] else V2i(0, -1)
        let total = mem.[scanSize - 1].X
        let mine = mem.[lid].X

        let last = 
            if vl.X <> 0 then vl.Y
            else -1

        let leading = lid - last - 1
        let isLast = mine = total
        leading, isLast

    let leadingIsLastBallot (ev : bool) (ov : bool) =
        let eb = Ballot.ballot(ev) |> uint32
        let ob = Ballot.ballot(ov) |> uint32

        let i = getLocalId().X
        let ei = 2 * i
        let oi = ei + 1

        let less        = Ballot.lessMask() |> uint32
        let greater     = Ballot.lessMask() |> uint32

        let les = eb &&& less |> Bitwise.MSB
        let los = ob &&& less |> Bitwise.MSB

        let eLast =
            if les > los then 2 * les
            else 2 * los + 1
             
        let oLast =
            if ev then ei
            else eLast 
        
        let eLeading = ei - eLast - 1
        let oLeading = oi - oLast - 1

        let cae = eb &&& greater |> Bitwise.BitCount
        let cao = ob &&& greater |> Bitwise.BitCount

        let oIsLast = ov && cae = 0 && cao = 0
        let eIsLast = ev && oIsLast && not ov

        (eLeading, oIsLast, oLeading, eIsLast)

    let encode (index : int) (chroma : bool) (leading : int) (value : int) : int * uint32 =
        let scale = Tools.msb (uint32 (abs value)) + 1
        let off = if value < 0 then (1 <<< scale) - 1 else 0
        let v = uint32 (off + value)

        let mutable key = scale
        if index <> 0 then
            key <- (leading <<< 4) ||| scale

        let mutable huff = 0u
        if index = 0 && chroma then huff <- uniform.DCChroma.[key]
        elif index = 0 then huff <- uniform.DCLum.[key]
        elif chroma then huff <- uniform.ACChroma.[key]
        else huff <- uniform.ACLum.[key]
            
        let len = Codeword.length huff
        let cc = Codeword.code huff
        len + scale, (cc <<< scale) ||| (v &&& ((1u <<< scale) - 1u))
       
    
    [<LocalSize(X = 8, Y = 8)>]
    let dct (target : V4i[]) =
        compute {
            let values : V3d[] = allocateShared 64
            let imageSize : V2i = uniform?ImageSize
            let imageLevel : int = uniform?ImageLevel

            let blockId = getWorkGroupId().XY 
            let blockCount = getWorkGroupCount().XY

            let gc = getGlobalId().XY
            let lc = getLocalId().XY
            let lid = lc.Y * 8 + lc.X
            let cid = Constants.inverseZigZagOrder.[lid]
            let gc = V2i(gc.X, imageSize.Y - 1 - gc.Y)

            let tc = (V2d gc + V2d(0.5, 0.5)) / (V2d imageSize)
     

            // every thread loads the RGB value and stores it in values (as YCbCr)
            values.[lid] <- ycbcr (inputImage.SampleLevel(tc, float imageLevel).XYZ)
            barrier()

            // figure out the DCT normalization factors
            let fx = if lc.X = 0 then Constant.Sqrt2Half else 1.0
            let fy = if lc.Y = 0 then Constant.Sqrt2Half else 1.0
            let f = fx * fy
            

            // separated DCT
            let mutable inner = V3d.Zero
            let mutable i = 8 * lc.Y
            for m in 0 .. 7 do
                inner <- inner + values.[i] * dctFactor m lc.X
                i <- i + 1

            barrier()
            values.[lid] <- inner
            barrier()

            let mutable sum = V3d.Zero
            let mutable i = lc.X
            for n in 0 .. 7 do
                sum <- sum + values.[i] * dctFactor n lc.Y
                i <- i + 8

            // SIMPLE DCT:
            // // sum all DCT coefficients for the current pixel
            // let mutable sum = V3d.Zero
            // let mutable i = 0
            // for n in 0 .. 7 do
            //     for m in 0 .. 7 do
            //         let f = (dctFactor m lc.X * dctFactor n lc.Y)
            //         sum <- 
            //             V3d(
            //                 fma values.[i].X f sum.X,
            //                 fma values.[i].Y f sum.Y,
            //                 fma values.[i].Z f sum.Z
            //             )
            //         //sum <- sum + values.[i] * dctFactor m lc.X * dctFactor n lc.Y
            //         i <- i + 1

            let dct = 0.25 * f * sum

            // quantify the dct values according to the quantization matrix
            let qdct = quantify lid dct

            // store the resulting values in target
            let tid = (blockId.X + blockCount.X * blockId.Y) * 64 + cid
            target.[tid] <- V4i(qdct, 0)
        }

    [<LocalSize(X = 32)>]
    let codewordsKernelBallot (data : int[]) (codewords : V2i[]) =
        compute {
            let group = getWorkGroupId().X
            let channel = getGlobalId().Y

            let llid = 2*getLocalId().X
            let rlid = llid + 1

            let lgid = 4 * (group * 64 + llid) + channel
            let rgid = lgid + 4

            let ltid = (group * 3 + channel) * 64 + llid
            let rtid = (group * 3 + channel) * 64 + rlid
                
            
            let mutable lValue = data.[lgid]
            let mutable rValue = data.[rgid]

            // differential DC encoding
            if llid = 0 && lgid >= 256 then 
                lValue <- lValue - data.[lgid - 256]


            // DC and all non-zero entries produce codewords
            let lHasOutput = llid = 0 || lValue <> 0
            let rHasOutput = rValue <> 0

            // count the leading non-encoding threads
            let lLeading, lIsLast, rLeading, rIsLast = leadingIsLastBallot lHasOutput rHasOutput

            
            // leading-counts larger than 15 are handled separately
            let lLeading = lLeading &&& 0xF
            let rLeading = rLeading &&& 0xF

            // check again if the thread shall produce an output
            let lHasOutput =
                lHasOutput ||                     // every thread that already caused an output still does
                (lLeading = 15 && not lIsLast)    // a zero with 15 leading zeros causes an output (0xF0)

            let rHasOutput =
                rHasOutput ||                     // every thread that already caused an output still does
                (rLeading = 15 && not rIsLast) || // a zero with 15 leading zeros causes an output (0xF0)
                rlid = 63                         // the last thread causes an output (EOB or simply its value)
                
            let mutable lcLen = 0
            let mutable lcData = 0u
            let mutable rcLen = 0
            let mutable rcData = 0u

            if lHasOutput then
                // figure out the code
                let (len, code) = encode llid (channel <> 0) lLeading lValue
                lcLen <- len
                lcData <- code

            if rHasOutput then
                // the last thread writes EOB (encode 0 0) if zero and the standard code if not
                let leading = if rlid = 63 && rValue = 0 then 0 else rLeading

                // figure out the code
                let (len, code) = encode rlid (channel <> 0) leading rValue
                rcLen <- len
                rcData <- code

            // store the code in the codeword list
            codewords.[ltid] <- V2i(lcLen, int lcData)
            codewords.[rtid] <- V2i(rcLen, int rcData)
        }

    [<LocalSize(X = 64)>]
    let codewordsKernel (data : int[]) (codewords : V2i[]) =
        compute {
            let group = getWorkGroupId().X
            let lid = getLocalId().X
            let channel = getGlobalId().Y
            let gid = 4 * getGlobalId().X + channel
            let tid = (group * 3 + channel) * 64 + lid

            let mutable value = data.[gid]

            // differential DC encoding
            if lid = 0 && gid >= 256 then 
                value <- value - data.[gid - 256]
            

            // DC and all non-zero entries produce codewords
            let hasOutput = lid = 0 || value <> 0

            // count the leading non-encoding threads
            let leading, isLast = leadingIsLast hasOutput

            // leading-counts larger than 15 are handled separately
            let leading = leading &&& 0xF

            // check again if the thread shall produce an output
            let hasOutput =
                hasOutput ||                    // every thread that already caused an output still does
                (leading = 15 && not isLast) || // a zero with 15 leading zeros causes an output (0xF0)
                lid = 63                        // the last thread causes an output (EOB or simply its value)


            let mutable cLen = 0
            let mutable cData = 0u
            if hasOutput then
                // the last thread writes EOB (encode 0 0) if zero and the standard code if not
                let leading = if lid = 63 && value = 0 then 0 else leading

                // figure out the code
                let (len, code) = encode lid (channel <> 0) leading value
                cLen <- len
                cData <- code

            // store the code in the codeword list
            codewords.[tid] <- V2i(cLen, int cData)
        }

    [<LocalSize(X = 64)>]
    let assembleKernel (codewordCount : int) (codewords : V2i[]) (target : uint32[]) =
        compute {
            let id = getGlobalId().X

            let codeValue = codewords.[id]
            let offset = if id > 0 then codewords.[id - 1].X else 0
            let size = codeValue.X - offset
            let code = uint32 codeValue.Y

            if size > 0 && code <> 0u then
                let oi = offset / 32
                let oo = offset &&& 31

                let space = 32 - oo
                if space >= size then
                    let a = code <<< (space - size) |> flipByteOrder
                    Tools.atomicOr target.[oi] a
                else 
                    let rest = size - space
                    let a = (code >>> rest) |> flipByteOrder // &&& ((1u <<< space) - 1u) |> flipByteOrder
                    Tools.atomicOr target.[oi] a

                    let b = (code &&& ((1u <<< rest) - 1u)) <<< (32 - rest) |> flipByteOrder
                    Tools.atomicOr target.[oi + 1] b

        }
   

module private Align =
    let next (a : int) (v : int) =
        if v % a = 0 then v
        else (1 + v / a) * a

    let next2 (a : int) (v : V2i) =
        V2i(next a v.X, next a v.Y)

[<AutoOpen>]
module Tools = 

    let toByteArray<'a when 'a : unmanaged> (arr : 'a[]) : byte[] =
        let handle = GCHandle.Alloc(arr,GCHandleType.Pinned)
        try
            let target : byte[] = Array.zeroCreate (sizeof<'a> * arr.Length)
            Marshal.Copy(handle.AddrOfPinnedObject(), target, 0, target.Length)
            target
        finally 
            handle.Free()


type JpegCompressor(runtime : IComputeRuntime) =
    let dct         = runtime.CreateComputeShader JpegKernels.dct
    let codewords   = runtime.CreateComputeShader JpegKernels.codewordsKernelBallot
    let assemble    = runtime.CreateComputeShader JpegKernels.assembleKernel
    let scanner     = new Scan<int>(runtime, <@ (+) : int -> int -> int @>)
    
    member x.Runtime = runtime
    member internal x.DctShader = dct
    member internal x.CodewordShader = codewords
    member internal x.AssembleShader = assemble
    member internal x.Scan(s,d) = scanner.Compile(s,d)


    member x.Dispose() =
        scanner.Dispose()
        runtime.DeleteComputeShader dct
        runtime.DeleteComputeShader codewords
        runtime.DeleteComputeShader assemble

    member x.NewInstance(size : V2i, quality : Quantization) =
        new JpegCompressorInstance(x, size, quality)

    interface IDisposable with
        member x.Dispose() = x.Dispose()


and JpegCompressorInstance internal(parent : JpegCompressor, size : V2i, quality : Quantization) =
    let mutable quality = quality

    let runtime = parent.Runtime
    let alignedSize = size |> Align.next2 8
    
    let alignedPixelCount   = int64 alignedSize.X * int64 alignedSize.Y
    let outputSize          = alignedPixelCount * 3L

    let dctBuffer           = runtime.CreateBuffer<V4i>(int alignedPixelCount)
    let codewordBuffer      = runtime.CreateBuffer<V2i>(int alignedPixelCount * 3)

    let outputBuffer        = runtime.CreateBuffer<uint32>(int outputSize)

    let cpuBuffer : byte[]  = Array.zeroCreate (int outputSize) //.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit outputSize
    let bitCountBuffer : int[] = Array.zeroCreate 1 //      = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit 4L

    // TODO: flexible encoders??
    let encoder = HuffmanEncoder.photoshop

    let codewordCountView   = codewordBuffer.Buffer.Coerce<int>().Strided(2)

    let outputData = Marshal.AllocHGlobal (2n * nativeint outputSize)

   

    static let zigZagOrder =
        [|
            0;  1;  8;  16;  9;  2;  3; 10
            17; 24; 32; 25; 18; 11;  4;  5
            12; 19; 26; 33; 40; 48; 41; 34 
            27; 20; 13;  6;  7; 14; 21; 28
            35; 42; 49; 56; 57; 50; 43; 36
            29; 22; 15; 23; 30; 37; 44; 51
            58; 59; 52; 45; 38; 31; 39; 46
            53; 60; 61; 54; 47; 55; 62; 63
        |]

    let qLuminanceOffset = 7
    let qChromaOffset = 72
    let header =
        use ms = new System.IO.MemoryStream()

        let header = [| 0xFFuy; 0xD8uy;  |]
        ms.Write(header, 0, header.Length)

        let encode (v : uint16) =
            [| byte (v >>> 8); byte v |]


        let quant = 
            Array.concat [
                [| 0xFFuy; 0xDBuy; 0x00uy; 0x84uy |]
                [| 0x00uy |]
                zigZagOrder |> Array.map (fun i -> quality.luminance.[i] |> byte)
                [| 0x01uy |]
                zigZagOrder |> Array.map (fun i -> quality.chroma.[i] |> byte)
            ]
        ms.Write(quant, 0, quant.Length)

        let sof =
            Array.concat [
                [| 0xFFuy; 0xC0uy; 0x00uy; 0x11uy |]
                [| 0x08uy |]
                encode (uint16 size.Y)
                encode (uint16 size.X)
                [| 0x03uy; |]
                [| 0x01uy; 0x11uy; 0x00uy |]
                [| 0x02uy; 0x11uy; 0x01uy |]
                [| 0x03uy; 0x11uy; 0x01uy |]
            ]
        ms.Write(sof, 0, sof.Length)

        let huff =
            let huffSize (spec : HuffmanTable)  =
                uint16 (1 + 16 + spec.values.Length)

            let encodeHuff (kind : byte) (spec : HuffmanTable) =
                Array.concat [
                    [| kind |]
                    Array.skip 1 spec.counts |> Array.map byte
                    spec.values
                ]

            Array.concat [
                [| 0xFFuy; 0xC4uy |]
                encode (2us + huffSize encoder.dcLuminance + huffSize  encoder.dcChroma + huffSize  encoder.acLuminance + huffSize  encoder.acChroma)

                encodeHuff 0x00uy  encoder.dcLuminance
                encodeHuff 0x01uy  encoder.dcChroma
                encodeHuff 0x10uy  encoder.acLuminance
                encodeHuff 0x11uy  encoder.acChroma
            ]
        ms.Write(huff, 0, huff.Length)

        let sos = [| 0xFFuy; 0xDAuy; 0x00uy; 0x0Cuy; 0x03uy; 0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x11uy; 0x00uy; 0x3Fuy; 0x00uy |]
        ms.Write(sos, 0, sos.Length)
        ms.ToArray()


    do Marshal.Copy(header, 0, outputData, header.Length)

    let scan = parent.Scan(codewordCountView, codewordCountView)

    let dctInput =
        let i = runtime.NewInputBinding parent.DctShader
        i.["size"] <- alignedSize
        i.["ImageSize"] <- size
        i.["target"] <- dctBuffer
        i.["Quantization"] <- Quantization.toTable quality
        i.Flush()
        i


    let updateQuantization (q : Quantization) =
        if q != quality then
            use __ = runtime.ContextLock
            quality <- q
            dctInput.["Quantization"] <- Quantization.toTable q
            dctInput.Flush()

            for i in 0 .. 63 do
                header.[qLuminanceOffset + i] <- byte q.luminance.[zigZagOrder.[i]]
            for i in 0 .. 63 do
                header.[qChromaOffset + i] <- byte q.chroma.[zigZagOrder.[i]]
                
            Marshal.Copy(header, 0, outputData, header.Length)

        
    let codewordInput = 
        let i = runtime.NewInputBinding parent.CodewordShader
        i.["data"] <- dctBuffer
        i.["codewords"] <- codewordBuffer
        i.["DCLum"] <- encoder.dcLuminance.table
        i.["ACLum"] <- encoder.acLuminance.table
        i.["DCChroma"] <- encoder.dcChroma.table
        i.["ACChroma"] <- encoder.acChroma.table
        i.Flush()
        i
        
    let assembleInput =
        let i = runtime.NewInputBinding parent.AssembleShader
        i.["codewords"] <- codewordBuffer
        i.["target"] <- outputBuffer
        i.["codewordCount"] <- int codewordBuffer.Count
        i.Flush()
        i

    let dctCommand =
        [
            ComputeCommand.Bind parent.DctShader
            ComputeCommand.SetInput dctInput
            ComputeCommand.Dispatch (alignedSize / V2i(8,8))
        ]


    let codewordCommand =
        [
            ComputeCommand.Sync dctBuffer.Buffer
            ComputeCommand.Bind parent.CodewordShader
            ComputeCommand.SetInput codewordInput
            ComputeCommand.Dispatch(V2i(int dctBuffer.Count / 64, 3))
        ]

    let assembleCommand = 
        [
            ComputeCommand.Sync codewordBuffer.Buffer
            ComputeCommand.Zero outputBuffer
            
            ComputeCommand.Bind(parent.AssembleShader)
            ComputeCommand.SetInput assembleInput
            ComputeCommand.Dispatch(int codewordBuffer.Count / 64)
        ]
       
    let cmds =
        [
            yield! dctCommand
            yield! codewordCommand
            
            yield ComputeCommand.Sync(codewordBuffer.Buffer, ResourceAccess.ShaderWrite, ResourceAccess.ShaderRead)
            yield ComputeCommand.Execute scan
            yield! assembleCommand

            let codewordBuffer = codewordBuffer.Buffer.Coerce<int>()
            yield ComputeCommand.Copy(codewordBuffer.[codewordBuffer.Count - 2 .. codewordBuffer.Count - 1], bitCountBuffer)
        ]

    let overallCommand =
        runtime.Compile cmds

    member x.Quality
        with get() = quality
        and set q = updateQuantization q

    member x.Encode(image : ITextureSubResource) =
        assert (image.Size.XY = size)
        dctInput.["InputImage"] <- image
        dctInput.["ImageLevel"] <- image.Level
        dctInput.Flush()
        overallCommand
        
    member x.DownloadStream() =
        let numberOfBits : int = bitCountBuffer.[0]
        let byteCount = if numberOfBits % 8 = 0 then numberOfBits / 8 else 1 + numberOfBits / 8

        outputBuffer.Buffer.Coerce<byte>().Download(0, cpuBuffer, 0, byteCount)

        let ms = new System.IO.MemoryStream(byteCount * 2 + header.Length + 2)

        ms.Write(header, 0, header.Length)

        let gc = GCHandle.Alloc(cpuBuffer, GCHandleType.Pinned)
        let ptr = gc.AddrOfPinnedObject()
        try
            let mutable ptr = ptr

            for i in 0 .. byteCount - 1 do
                let v : byte = NativeInt.read ptr

                ms.WriteByte v

                if v = 0xFFuy then
                    ms.WriteByte 0x00uy

                ptr <- ptr + 1n
        finally
            gc.Free()

        ms.WriteByte(0xFFuy)
        ms.WriteByte(0xD9uy)
        ms.ToArray()

    member x.Download() =
        let numberOfBits : int = bitCountBuffer.[0]
        let byteCount = if numberOfBits % 8 = 0 then numberOfBits / 8 else 1 + numberOfBits / 8
        
        outputBuffer.Buffer.Coerce<byte>().Download(0, cpuBuffer, 0, byteCount)


        let dstStart = outputData

        // header is already written
        let mutable dst = dstStart + nativeint header.Length

        // copy the data
        let gc = GCHandle.Alloc(cpuBuffer, GCHandleType.Pinned)
        let ptr = gc.AddrOfPinnedObject()

        let mutable pSrc : nativeptr<byte> = NativePtr.ofNativeInt ptr
        let mutable pDst : nativeptr<byte> = NativePtr.ofNativeInt dst
        let mutable i = byteCount

        while i <> 0 do
            let v : byte = NativePtr.read pSrc

            if v = 0xFFuy then
                // writing two individual bytes seems to be more efficient
                // NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt pDst)) 0x00FFus
                // pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 2n)
                NativePtr.write pDst 0xFFuy
                pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 1n)
                NativePtr.write pDst 0x00uy
                pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 1n)
            else
                NativePtr.write pDst v
                pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 1n)

            pSrc <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pSrc) + 1n)
            i <- i - 1
               
        dst <- NativePtr.toNativeInt pDst  
        gc.Free()

        // write EOI
        NativeInt.write dst 0xD9FFus
        dst <- dst + 2n
        let finalLength = dst - dstStart |> int

        let result : byte[] = Array.zeroCreate finalLength

        Marshal.Copy(outputData, result, 0, finalLength)
        //Array.Resize(&result, finalLength)
        result

    member x.DownloadChunked(chunkSize : nativeint) =
        let numberOfBits : int = bitCountBuffer.[0]
        let byteCount = if numberOfBits % 8 = 0 then numberOfBits / 8 else 1 + numberOfBits / 8
        
        let mem = Marshal.AllocHGlobal (2n * chunkSize)

        let mutable ping = mem
        let mutable pong = mem + chunkSize
        let mutable offset = 0n
        let mutable remaining = nativeint byteCount
        let mutable wait = id
        let mutable copySize = min remaining chunkSize
        runtime.Copy(outputBuffer.Buffer, 0n, ping, copySize)



        let dstStart = outputData
        // header is already written
        let mutable dst = dstStart + nativeint header.Length

        while remaining > 0n do
            wait()
            Fun.Swap(&ping, &pong)
            let pongSize = copySize
            offset <- offset + copySize
            remaining <- remaining - copySize
            copySize <- min remaining chunkSize
            if copySize > 0n then
                wait <- runtime.CopyAsync(outputBuffer.Buffer, offset, ping, copySize)
            else
                wait <- id

            // read from pong now

            let mutable src = pong
            let mutable pSrc : nativeptr<byte> = NativePtr.ofNativeInt src
            let mutable pDst : nativeptr<byte> = NativePtr.ofNativeInt dst

            for i in 1 .. int pongSize do
                let v : byte = NativePtr.read pSrc

                if v = 0xFFuy then
                    // writing two individual bytes seems to be more efficient
                    // NativePtr.write (NativePtr.ofNativeInt (NativePtr.toNativeInt pDst)) 0x00FFus
                    // pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 2n)
                    NativePtr.write pDst 0xFFuy
                    pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 1n)
                    NativePtr.write pDst 0x00uy
                    pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 1n)
                else
                    NativePtr.write pDst v
                    pDst <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pDst) + 1n)

                pSrc <- NativePtr.ofNativeInt ((NativePtr.toNativeInt pSrc) + 1n)

            dst <- NativePtr.toNativeInt pDst

            ()


        Marshal.FreeHGlobal mem

        // write EOI
        NativeInt.write dst 0xD9FFus
        dst <- dst + 2n
        let finalLength = dst - dstStart |> int

        let result : byte[] = Array.zeroCreate finalLength

        Marshal.Copy(outputData, result, 0, finalLength)
        //Array.Resize(&result, finalLength)
        result


    member x.Compress(image : ITextureSubResource, queries : IQuery) =
        assert (image.Size.XY = size)
        dctInput.["InputImage"] <- image.Texture
        dctInput.["ImageLevel"] <- image.Level
        dctInput.Flush()

        overallCommand.Run(queries)

        x.Download()

    member x.Compress(image : ITextureSubResource) =
        x.Compress(image, Queries.none)

    member x.Dispose() =
        dctInput.Dispose()
        codewordInput.Dispose()
        assembleInput.Dispose()
        scan.Dispose()
        overallCommand.Dispose()

        dctBuffer.Dispose()
        codewordBuffer.Dispose()
        outputBuffer.Dispose()
        Marshal.FreeHGlobal outputData

    interface IDisposable with
        member x.Dispose() = x.Dispose()

   