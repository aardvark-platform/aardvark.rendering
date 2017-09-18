namespace Jpeg

open Aardvark.Base
open Aardvark.Rendering.Vulkan

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
    }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HuffmanTable =
    [<AutoOpen>]
    module private Helpers = 
        
        type private HuffTree =
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

        let private build (counts : int[]) (values : byte[]) : HuffTree =
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

        let createEncodeTable (counts : int[]) (values : byte[]) =
            let max = 256
            let tree = build counts values

            let arr = Array.zeroCreate max

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
        {
            counts = counts
            values = values
            table = createEncodeTable counts values
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

    


type Quantization =
    {
        luminance : int[]
        chroma : int[]
    }
    
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Quantization =
    let private qBase =
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

    let private getQ (q : float) =
        let q = q |> max 1.0 |> min 100.0
        let s = if q < 50.0 then 5000.0 / q else 200.0 - 2.0 * q
        qBase |> Array.map (fun v ->
            floor ((s * float v + 50.0) / 100.0) |> int  |> max 1
        )  
        
    let create (quality : float) =
        let v = getQ quality
        { luminance = v; chroma = v }
         
    let photoshop80 =
        {
            luminance =
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
            chroma = 
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
        }



[<ReflectedDefinition>]
module Kernels =
    open FShade

    let encoder = HuffmanEncoder.photoshop

    module Constants = 
        let cosTable =
            Array.init 64 (fun i ->
                let m = i % 8
                let x = i / 8
                cos ( (2.0 * float m + 1.0) * System.Math.PI * float x / 16.0)
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

    type UniformScope with
        member x.QLuminance : Arr<64 N, int> = uniform?QLuminance
        member x.QChroma : Arr<64 N, int> = uniform?QChroma

    let ycbcr (v : V3d) =
        let v = 255.0 * v
        V3d(
            Vec.dot Constants.y (V4d(v, 1.0)),
            Vec.dot Constants.cb (V4d(v, 1.0)),
            Vec.dot Constants.cr (V4d(v, 1.0))
        )

    let quantify (i : int) (v : V3d) =
        if i < 0 || i > 64 then
            V3d(666.6, 666.6, 666.6)
        else
            let ql = uniform.QLuminance.[i] |> max 1
            let qc = uniform.QChroma.[i] |> max 1
            let t = v / V3d(ql,qc,qc)
            V3d(round t.X, round t.Y, round t.Z)
        

    let inputImage =
        sampler2d {
            texture uniform?InputImage
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let test (pos : bool) =
        let Sqrt2Half = sqrt 2.0 / 2.0
        let mutable maxDCT = 0.0
        for x in 0 .. 7 do
            for y in 0 .. 7 do
                let mutable sum = 0.0
                let mutable i = 0
                for n in 0 .. 7 do
                    for m in 0 .. 7 do
                        let a = cos ( (2.0 * float m + 1.0) * System.Math.PI * float x / 16.0)
                        let b = cos ( (2.0 * float n + 1.0) * System.Math.PI * float y / 16.0)

                        let f = a * b
                        if pos && f > 0.0 then
                            sum <- sum + f * 255.0
                            
                        elif not pos && f < 0.0 then
                            sum <- sum + -f * 255.0

                let f = (if x = 0 then Sqrt2Half else 1.0) * (if y = 0 then Sqrt2Half else 1.0)
                let dct = 0.25 * f * sum

                maxDCT <- max maxDCT dct
        maxDCT

    let dctFactor (m : int) (x : int) =
        //cos ( (2.0 * float m + 1.0) * Constant.Pi * float x / 16.0)
        Constants.cosTable.[m + 8 * x]

    [<LocalSize(X = 8, Y = 8)>]
    let dct (size : V2i) (target : V4d[]) =
        compute {
            let size = size
            let ll : V3d[] = allocateShared 64

            let gc = getGlobalId().XY
            let lc = getLocalId().XY
            let lid = lc.Y * 8 + lc.X

            ll.[lid] <- ycbcr inputImage.[gc, 0].XYZ
            barrier()
            let f = (if lc.X = 0 then Constant.Sqrt2Half else 1.0) * (if lc.Y = 0 then Constant.Sqrt2Half else 1.0)

            let mutable sum = V3d.Zero
            let mutable i = 0
            for n in 0 .. 7 do
                for m in 0 .. 7 do
                    sum <- sum + ll.[i] * dctFactor m lc.X * dctFactor n lc.Y
                    i <- i + 1

            let dct = 0.25 * f * sum |> quantify lid
            barrier()


            ll.[Constants.inverseZigZagOrder.[lid]] <- dct
            barrier()
            
            let blockId = getWorkGroupId().XY 
            let blockCount = getWorkGroupCount().XY
            let tid = (blockId.X + blockCount.X * blockId.Y) * 64 + lid
 
            target.[tid] <- V4d(ll.[lid], 1.0)
        }

    type Ballot () =
            
        [<GLSLIntrinsic("gl_SubGroupSizeARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member SubGroupSize() : uint32 =
            failwith ""

        [<GLSLIntrinsic("ballotARB({0})", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member Ballot(a : bool) : uint64 =
            failwith ""

        [<GLSLIntrinsic("bitCount({0})")>]
        static member BitCount(u : uint32) : int =
            failwith ""

        [<GLSLIntrinsic("findMSB({0})")>]
        static member MSB(u : uint32) : int =
            failwith ""

        [<GLSLIntrinsic("gl_SubGroupLtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member LessMask() : uint64 =
            failwith ""

        [<GLSLIntrinsic("gl_SubGroupLeMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member LessEqualMask() : uint64 =
            failwith ""

        [<GLSLIntrinsic("gl_SubGroupGtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member GreaterMask() : uint64 =
            failwith ""

        [<GLSLIntrinsic("gl_SubGroupGeMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member GreaterEqualMask() : uint64 =
            failwith ""
                
        [<GLSLIntrinsic("addInvocationsAMD", "GL_AMD_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        static member AddInvocations(v : int) : int =
            failwith ""
        
        [<GLSLIntrinsic("atomicOr({0}, {1})")>]
        static member AtomicOr(r : 'a, v : 'a) : unit =
            failwith ""
        [<GLSLIntrinsic("atomicAdd({0}, {1})")>]
        static member AtomicAdd(r : 'a, v : 'a) : 'a =
            failwith ""


    let dcLum = encoder.dcLuminance.table
    let acLum = encoder.acLuminance.table
    let dcChrom = encoder.dcChroma.table
    let acChrom = encoder.acChroma.table


    let encode (index : int) (chroma : bool) (leading : int) (value : float) : int * uint32 =
        let value = int value
        
        let dc = Ballot.MSB(uint32 (abs value)) + 1
        let off = if value < 0 then (1 <<< dc) else 0
        let v = uint32 (off + value)

        let mutable key = dc
        if index <> 0 then
            key <- (leading <<< 4) ||| dc

        let mutable huff = 0u
        if index = 0 && chroma then huff <- dcChrom.[key]
        elif index = 0 then huff <- dcLum.[key]
        elif chroma then huff <- acChrom.[key]
        else huff <- acLum.[key]
            
        let len = Codeword.length huff
        let huff = Codeword.code huff
        len + dc, (huff <<< dc) ||| (v &&& ((1u <<< dc) - 1u))
       

    [<LocalSize(X = 32)>]
    let encodeKernel (channel : int) (counter : int[]) (data : float[]) (ranges : V2i[]) (mask : uint32[]) =
        compute {
            let mem = allocateShared 64
            let temp = allocateShared 64
            let offsetStore = allocateShared 1

            let offset = getWorkGroupId().X * 64

            let lid = getLocalId().X
            let llid = 2 * lid 
            let rlid = llid + 1

            let gid = getGlobalId().X
            let li = 2 * gid
            let ri = li + 1


            let lv = data.[li * 4 + channel]
            let rv = data.[ri * 4 + channel]

            let lnz = llid = 0 || lv <> 0.0
            let rnz = rv <> 0.0


            // count leading zeros
            let lessMask = Ballot.LessMask() |> uint32
            let greaterMask = Ballot.GreaterMask() |> uint32
            let lb = Ballot.Ballot(lnz) |> uint32
            let rb = Ballot.Ballot(rnz) |> uint32


            let lpm = Ballot.MSB(lb &&& lessMask)
            let rpm = Ballot.MSB(rb &&& lessMask)
                
            let nonZeroAfterR = (lb &&& greaterMask) <> 0u || (rb &&& greaterMask) <> 0u
            let nonZeroAfterL = rnz || nonZeroAfterR

            let lp =
                if lpm > rpm then 2 * lpm
                else 2 * rpm + 1

            let rp =
                if lnz then li
                else lp

            let llz = li - lp - 1
            let rlz = ri - rp - 1


            let scanSize = 64

            // encode values and write their bit-counts to mem
            // TODO: what about >=16 zeros??? => should be done
            // TODO: what about EOB marker
            let mutable lCode = 0u
            let mutable rCode = 0u
            let mutable lLength = 0
            let mutable rLength = 0

            if llid = 0 then
                let v = if lid >= 64 then lv - data.[lid - 4*64] else lv
                let lSize, lc = encode 0 (channel <> 0) 0 v
                lCode <- lc
                lLength <- lSize

            elif lnz then
                let lSize, lc = encode llid (channel <> 0) (llz % 16) lv
                lCode <- lc
                lLength <- lSize
//
//            elif llz >= 15 && llz % 16 = 1 && nonZeroAfterL then
//                let lSize, lc = encode llid (channel <> 0) 15 0.0
//                lCode <- lc
//                lLength <- lSize


            if rnz then
                let rSize, rc = encode rlid (channel <> 0) (rlz % 16) rv
                rCode <- rc
                rLength <- rSize

//            elif rlz >= 15 && rlz % 16 = 1 && nonZeroAfterR then
//                let rSize, rc = encode rlid (channel <> 0) 15 0.0
//                rCode <- rc
//                rLength <- rSize




            mem.[llid] <- lLength
            mem.[rlid] <- rLength        

            // scan mem
            barrier()

            let mutable s = 1
            let mutable d = 2
            while d <= scanSize do
                if llid % d = 0 && llid >= s then
                    mem.[llid] <- mem.[llid - s] + mem.[llid]

                barrier()
                s <- s <<< 1
                d <- d <<< 1

            d <- d >>> 1
            s <- s >>> 1
            while s >= 1 do
                if llid % d = 0 && llid + s < scanSize then
                    mem.[llid + s] <- mem.[llid] + mem.[llid + s]
                    
                barrier()
                s <- s >>> 1
                d <- d >>> 1
                    


            temp.[llid] <- 0u
            temp.[rlid] <- 0u
            barrier()

            if lLength > 0 then
                let bitOffset = if llid > 0 then mem.[llid - 1] else 0
                let bitLength = lLength
                let store = lCode

                let oi = bitOffset / 32
                let oo = bitOffset % 32

                let word = store
                let space = 32 - oo

                if bitLength <= space then
                    // oo = 0 => word <<< (32 - bitLength)
                    // oo = 16 => word <<< (16 - bitLength)
                    // oo = 24 => word <<< (8 - bitLength)

                    let a = word <<< (space - bitLength)
                    //let a = FShade.Primitives.Bitwise.BitFieldInsert(0u, word, oo, bitLength)
                    Ballot.AtomicOr(temp.[oi], a)

                else
                    let rest = bitLength - space
                    let a = (word >>> rest) &&& ((1u <<< space) - 1u)
                    //let a = FShade.Primitives.Bitwise.BitFieldInsert(0u, word >>> rest, oo, space)
                    Ballot.AtomicOr(temp.[oi], a)
                    
                    let b = (word &&& ((1u <<< rest) - 1u)) <<< (32 - rest)
                    //let b = FShade.Primitives.Bitwise.BitFieldInsert(0u, word, 0, rest)
                    Ballot.AtomicOr(temp.[oi+1], b)

            if rLength > 0 then
                let bitOffset = mem.[rlid - 1]
                let bitLength = rLength
                let store = rCode

                let oi = bitOffset / 32
                let oo = bitOffset % 32

                let word = store
                let space = 32 - oo

                if bitLength <= space then
                    // oo = 0 => word <<< (32 - bitLength)
                    // oo = 16 => word <<< (16 - bitLength)
                    // oo = 24 => word <<< (8 - bitLength)
                    let a = word <<< (space - bitLength)
                    //let a = FShade.Primitives.Bitwise.BitFieldInsert(0u, word, oo, bitLength)
                    Ballot.AtomicOr(temp.[oi], a)

                else
                    let rest = bitLength - space
                    let a = (word >>> rest) &&& ((1u <<< space) - 1u)
                    //let a = FShade.Primitives.Bitwise.BitFieldInsert(0u, word >>> rest, oo, space)
                    Ballot.AtomicOr(temp.[oi], a)
                    
                    let b = (word &&& ((1u <<< rest) - 1u)) <<< (32 - rest)
                    //let b = FShade.Primitives.Bitwise.BitFieldInsert(0u, word, 0, rest)
                    Ballot.AtomicOr(temp.[oi+1], b)



            barrier()

            let bitCnt = mem.[63]
            let intCnt = (if bitCnt % 32 = 0 then bitCnt / 32 else 1 + bitCnt / 32)
            if lid = 0 then
                let off = Ballot.AtomicAdd(counter.[0], intCnt)
                ranges.[getWorkGroupId().X] <- V2i(off, bitCnt)
                offsetStore.[0] <- off

            barrier()
            let offset = offsetStore.[0]

            if llid < intCnt then
                mask.[offset + llid] <- temp.[llid]
                    
            if rlid < intCnt then
                mask.[offset + rlid] <- temp.[rlid]

            
        }



module Align =
    let next (a : int) (v : int) =
        if v % a = 0 then v
        else (1 + v / a) * a

    let next2 (a : int) (v : V2i) =
        V2i(next a v.X, next a v.Y)

module Bit =
    let take (offset : int) (size : int) (word : uint32) =
        if offset >= 32 then 0u
        elif offset <= 0 then
            if size > 0 then
                word >>> (32 - size)
            else
                0u
        else
            let e = min 32 (offset + size)
            let size = e - offset
            (word >>> (32 - e)) &&& ((1u <<< size) - 1u)
            
    let toString (offset : int) (size : int) (word : uint32) =
        
        if offset >= 32 then ""
        elif offset <= 0 then
            if size > 0 then
                let mutable str = ""
                let mutable mask = 1u <<< (size - 1)
                let mutable v = word >>> (32 - size)
                for i in 1 .. size do
                    if v &&& mask <> 0u then str <- str + "1"
                    else str <- str + "0"
                    mask <- mask >>> 1
                str
            else
                ""
        else
            let e = min 32 (offset + size)
            let size = e - offset

            
            let mutable str = ""
            let mutable mask = 1u <<< (size - 1)
            let mutable v = (word >>> (32 - e)) &&& ((1u <<< size) - 1u)
            for i in 1 .. size do
                if v &&& mask <> 0u then str <- str + "1"
                else str <- str + "0"
                mask <- mask >>> 1
            str

    let print (v : uint32) =
        let mutable mask = 0x80000000u
        let mutable str = ""
        let mutable started = false
        for i in 0 .. 31 do
            if v &&& mask <> 0u then
                started <- true
                str <- str + "1"
            elif started then
                str <- str + "0"
            mask <- mask >>> 1
        sprintf "%s" str

type BitStream() =
    let mutable current = 0u
    let mutable currentLength = 0
    let result = System.Collections.Generic.List<byte>()
    let mutable str = ""

    static let bla (f : byte[]) =
        f |> Array.collect (fun b ->
            if b = 0xFFuy then [| b; 0x00uy |]
            else [| b |]
        )
   
    let write arr =
        let arr = bla arr
        result.AddRange arr

    let s = ()

    let writeCurrent() =
        assert(currentLength = 32)
        write [|
            Bit.take 0 8 current |> byte
            Bit.take 8 8 current |> byte
            Bit.take 16 8 current |> byte
            Bit.take 24 8 current |> byte
        |]
        current <- 0u
        currentLength <- 0

    member x.Write(word : uint32, offset : int, size : int) =
        str <- str + Bit.toString offset size word
        let space = 32 - currentLength
        if size <= space then
            let bits = Bit.take offset size word
            current <- (current <<< size) ||| bits
            currentLength <- currentLength + size
        else
            if space > 0 then
                let bits = Bit.take offset space word
                current <- (current <<< space) ||| bits
                currentLength <- 32
            writeCurrent()

            current <- Bit.take (offset + space) (size - space) word
            currentLength <- (size - space)

    member x.Write(word : uint32) =
        x.Write(word, 0, 32)
        
    member x.WriteCode(word : Codeword) =
        let len = Codeword.length word
        let data = Codeword.code word
        x.Write(data, (32 - len), len)

    member x.Write(b : byte) =
        x.Write(uint32 b, 24, 8)

    member x.Flush() =
        let mutable offset = 32 - currentLength
        while currentLength > 0 do
            write [| byte (Bit.take offset 8 current) |]
            currentLength <- currentLength - 8
            offset <- offset + 8

        current <- 0u
        currentLength <- 0

    override x.ToString() =
        let mutable res = ""
        for i in 0 .. 8 .. str.Length - 1 do
            let ss = str.Substring(i, min 8 (str.Length - i))
            res <- res + ss + " "
        res

    member x.ToArray() =
        x.Flush()
        result.ToArray()

type JpegStream() =
    let bs = BitStream()

    

    let encode (chroma : bool) (dc : bool) (leading : int) (value : float32) : unit =
        let v = int value

        let table =
            match chroma, dc with
                | false, true  -> Kernels.encoder.dcLuminance
                | false, false -> Kernels.encoder.acLuminance
                | true,  true  -> Kernels.encoder.dcChroma
                | true,  false -> Kernels.encoder.acChroma
                
        let scale = Fun.HighestBit(abs v) + 1
        let off = if v < 0 then (1 <<< scale) - 1 else 0
        let v = uint32 (off + v)
        let key =
            match dc with
                | true -> scale
                | false -> (leading <<< 4) ||| scale

        let huff = table.table.[key]
        let name = if chroma then "cr" else "lum"
        let kind = if dc then "dc" else "ac"
        printfn "code(%s, %s, %d, %d): %s %s" name kind leading v (Codeword.toString huff) (Codeword.toString (Codeword.create scale v))
        bs.WriteCode table.table.[key]
        bs.Write(v, 32 - scale, scale)


    let writeBlock (lastDC : V3f) (block : V3f[]) (offset : int) =
        for d in 0 .. 2 do
            let chroma = d <> 0
            encode chroma true 0 (block.[offset].[d] - lastDC.[d])
            let mutable leading = 0
            let mutable i = 1 
            while i < 64 do
                while i < 64 && block.[offset + i].[d] = 0.0f do 
                    leading <- leading + 1
                    i <- i + 1

                if i < 64 then
                    let v = block.[offset + i]

                    while leading >= 16 do
                        encode chroma false 15 0.0f
                        leading <- leading - 16

                        
                    encode chroma false leading v.[d]
                    leading <- 0
                    i <- i + 1
                else
                    encode chroma false 0 0.0f
        block.[offset]

    static member Value(v : int) =
        let scale = Fun.HighestBit(abs v) + 1
        let off = if v < 0 then (1 <<< scale) - 1 else 0
        uint32 (off + v)

    member x.WriteBlocks(data : V3f[]) =
        let mutable offset = 0
        let mutable lastDC = V3f.Zero
        while offset < data.Length do
            lastDC <- writeBlock lastDC data offset
            offset <- offset + 64
           
    override x.ToString() = bs.ToString() 
    member x.ToArray() = bs.ToArray()

type ReferenceCompressor() =

    static let ycbcrBlock =
        let mat = 
            M34f(
                 0.299f,       0.587f,      0.114f,     -128.0f,
                -0.168736f,   -0.331264f,   0.5f,        0.0f,
                 0.5f,        -0.418688f,  -0.081312f,   0.0f
            )

        fun (c : V3f[]) ->
            c |> Array.map (fun c -> mat.TransformPos(c * V3f(255.0f, 255.0f, 255.0f)))

    static let dctBlock (block : V3f[]) =
        Array.init 64 (fun i ->
            let x = i % 8
            let y = i / 8
            let cx = if x = 0 then float32 Constant.Sqrt2Half else 1.0f
            let cy = if y = 0 then float32 Constant.Sqrt2Half else 1.0f

            let mutable sum = V3f.Zero
            for m in 0 .. 7 do
                for n in 0 .. 7 do
                    let a = cos ((2.0f * float32 m + 1.0f) * float32 x * float32 Constant.Pi / 16.0f)
                    let b = cos ((2.0f * float32 n + 1.0f) * float32 y * float32 Constant.Pi / 16.0f)
                    sum <- sum + block.[m + 8*n] * a * b
                
            0.25f * cx * cy * sum
        )
        
    static let quantifyBlock (quality : Quantization) (block : V3f[]) =
        let round (v : V3f) = V4f((round v.X), (round v.Y), (round v.Z), 0.0f)
        Array.map3 (fun ql qc v -> round(v / V3f(float ql,float qc,float qc))) quality.luminance quality.chroma block

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

    static let zigZagBlock (block : 'a[]) =
        zigZagOrder |> Array.map (fun i -> 
            block.[i]
        )
    
    let printBits (v : byte) =
        let mutable str = ""
        let mutable mask = 1uy <<< 7
        for _ in 1 .. 8 do
            if v &&& mask <> 0uy then str <- str + "1"
            else str <- str + "0"
            mask <- mask >>> 1
        str

    member x.Transform(data : PixImage<'a>, quality : Quantization) =
        //assert(data.Size.X % 8 = 0 && data.Size.Y % 8 = 0)
        let alignedSize = Align.next2 8 data.Size

        let blockCount = alignedSize / V2i(8,8)
        let blocks = Array.zeroCreate (blockCount.X * blockCount.Y)
            
        let mat = data.GetMatrix<C4f>()

        let mutable i = 0
        for y in 0 .. blockCount.Y - 1 do
            for x in 0 .. blockCount.X - 1 do
                let blockMat = mat.SubMatrix(8 * V2i(x,y), V2i(8,8))

                let arr = Array.zeroCreate 64
                blockMat.ForeachXYIndex (fun (x : int64) (y : int64) (i : int64) ->
                    let v = mat.[i]
                    arr.[int x + 8 * int y] <- v.ToV3f()
                )
                blocks.[i] <- arr
                i <- i + 1

        blocks |> Array.collect (ycbcrBlock >> dctBlock >> quantifyBlock quality >> zigZagBlock >> Array.map Vec.xyz) 

    member x.Encode(data : V3f[]) =
        let stream = JpegStream()
        stream.WriteBlocks data
        stream.ToArray()


    member x.Compress(data : PixImage<'a>, quality : Quantization) =
        let dctBuffer = x.Transform(data, quality)
        x.Encode(dctBuffer)
    
    member x.ToImageData(realSize : V2i, quality : Quantization, scanData : byte[]) =
        use ms = new System.IO.MemoryStream()

        
        let header = [| 0xFFuy; 0xD8uy;  |]
        ms.Write(header, 0, header.Length)

        let encode (v : uint16) =
            [| byte (v >>> 8); byte v |]


        let quant = 
            Array.concat [
                [| 0xFFuy; 0xDBuy; 0x00uy; 0x84uy |]
                [| 0x00uy |]
                quality.luminance |> zigZagBlock |> Array.map byte
                [| 0x01uy |]
                quality.chroma |> zigZagBlock |> Array.map byte
            ]
        ms.Write(quant, 0, quant.Length)

        let sof =
            Array.concat [
                [| 0xFFuy; 0xC0uy; 0x00uy; 0x11uy |]
                [| 0x08uy |]
                encode (uint16 realSize.Y)
                encode (uint16 realSize.X)
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
                encode (2us + huffSize Kernels.encoder.dcLuminance + huffSize  Kernels.encoder.dcChroma + huffSize  Kernels.encoder.acLuminance + huffSize  Kernels.encoder.acChroma)

                encodeHuff 0x00uy  Kernels.encoder.dcLuminance
                encodeHuff 0x01uy  Kernels.encoder.dcChroma
                encodeHuff 0x10uy  Kernels.encoder.acLuminance
                encodeHuff 0x11uy  Kernels.encoder.acChroma
            ]
        ms.Write(huff, 0, huff.Length)

        let sos = [| 0xFFuy; 0xDAuy; 0x00uy; 0x0Cuy; 0x03uy; 0x01uy; 0x00uy; 0x02uy; 0x11uy; 0x03uy; 0x11uy; 0x00uy; 0x3Fuy; 0x00uy |]
        ms.Write(sos, 0, sos.Length)

        ms.Write(scanData, 0, scanData.Length)
        ms.Write([| 0xFFuy; 0xD9uy |], 0, 2)
        ms.ToArray()

type Compressor(runtime : Runtime) =
    let device = runtime.Device
    let pool = runtime.DescriptorPool

    let dct = device |> ComputeShader.ofFunction Kernels.dct
    let encode = device |> ComputeShader.ofFunction Kernels.encodeKernel

    let transform(data : Image) (alignedSize : V2i) (quality : Quantization) (dctBuffer : Buffer<V4f>) =
        use dctInput = pool |> ComputeShader.newInputBinding dct
        
        dctInput.["InputImage"] <- data
        dctInput.["size"] <- alignedSize
        dctInput.["target"] <- dctBuffer
        dctInput.["QLuminance"] <- quality.luminance
        dctInput.["QChroma"] <- quality.chroma
        dctInput.Flush()

        dctBuffer.Upload(Array.zeroCreate (int dctBuffer.Count))

        device.perform {
            do! Command.Bind dct
            do! Command.SetInputs dctInput
            do! Command.Dispatch (alignedSize / V2i(8,8))
        }

    let encode (dctBuffer : Buffer<V4f>) (alignedSize : V2i) =
        use input = runtime.DescriptorPool |> ComputeShader.newInputBinding encode

        let blocks = (alignedSize.X * alignedSize.Y) / 64
        let ranges = device.CreateBuffer<V2i>(int64 blocks)
        let data = device.CreateBuffer<uint32>(64L * int64 blocks)
        let counter = device.CreateBuffer<int>(Array.zeroCreate 1)

        input.["data"] <- dctBuffer
        input.["ranges"] <- ranges
        input.["counter"] <- counter
        input.["mask"] <- data
        input.Flush()

        let offsets : V3i[] = Array.zeroCreate blocks
        let counts : V3i[] = Array.zeroCreate blocks

        for c in 0 .. 2 do
            input.["channel"] <- c
            input.Flush()

            device.perform {
                do! Command.Bind encode
                do! Command.SetInputs input
                do! Command.Dispatch blocks
            }

            let r = Array.zeroCreate blocks
            ranges.Download(r)
            for i in 0 .. blocks - 1 do
                offsets.[i].[c] <- r.[i].X
                counts.[i].[c] <- r.[i].Y


        let cnt = Array.zeroCreate 1
        counter.Download(cnt)

        let arr = Array.zeroCreate cnt.[0]
        data.Download(0L, arr, 0L, arr.LongLength)

        device.Delete ranges
        device.Delete data
        device.Delete counter

        let bs = BitStream()
        for i in 0 .. offsets.Length - 1 do
            let off = offsets.[i]
            let cnt = counts.[i]
            for ci in 0 .. 2 do
                let offset = off.[ci]
                let size = cnt.[ci]

                let mutable i = 0
                let mutable remaining = size
                while remaining > 0 do
                    bs.Write(arr.[offset + i], 0, min 32 remaining)
                    i <- i + 1
                    remaining <- remaining - 32

                let eob = 
                    if ci = 0 then Kernels.encoder.acLuminance.table.[0]
                    else Kernels.encoder.acChroma.table.[0]

                bs.WriteCode(eob)

        bs.ToArray()

    member x.Compress(data : Image, quality : Quantization) : byte[] =
        let alignedSize = data.Size.XY |> Align.next2 8
        let dctBuffer = device.CreateBuffer<V4f>(int64 alignedSize.X * int64 alignedSize.Y)

        transform data alignedSize quality dctBuffer

        encode dctBuffer alignedSize

    member x.Compress(data : PixImage<byte>, quality : Quantization) : byte[] =
        //assert(data.Size.X % 8 = 0 && data.Size.Y % 8 = 0)
        let image = device.CreateImage(PixTexture2d(PixImageMipMap [| data :> PixImage |], TextureParams.empty))
        let alignedSize = Align.next2 8 data.Size
        let dctBuffer = device.CreateBuffer<V4f>(int64 alignedSize.X * int64 alignedSize.Y)

        transform image alignedSize quality dctBuffer

        device.Delete image

        let res = encode dctBuffer alignedSize
        device.Delete dctBuffer
        res

    member x.Transform(data : PixImage<'a>, quality : Quantization) =
        //assert(data.Size.X % 8 = 0 && data.Size.Y % 8 = 0)
        let image = device.CreateImage(PixTexture2d(PixImageMipMap [| data :> PixImage |], TextureParams.empty))
        let alignedSize = Align.next2 8 data.Size
        let dctBuffer = device.CreateBuffer<V4f>(int64 alignedSize.X * int64 alignedSize.Y)

        transform image alignedSize quality dctBuffer

        let res = dctBuffer.Download()
        device.Delete image
        device.Delete dctBuffer
        res |> Array.map Vec.xyz

module Test =

    let printMat8 (arr : 'a[]) =
        arr |> Array.map (sprintf "%A") |> Array.chunkBySize 8 |> Array.map (String.concat " ") |> Array.iter (Log.line "%s")
        
    let printBits (data : byte[]) =
        let mutable str = ""
        for i in 0 .. data.Length - 1 do
            let v = data.[i]
            let mutable mask = 1uy <<< 7
            for _ in 1 .. 8 do
                if v &&& mask <> 0uy then str <- str + "1"
                else str <- str + "0"
                mask <- mask >>> 1
            str <- str + " "
        Log.line "%s" str

    let run() =
        use app = new HeadlessVulkanApplication(false)
        let device = app.Device
        
        let comp = Compressor(app.Runtime)
        let ref = ReferenceCompressor()

        let rand = RandomSystem()

        let pi = 
            let pi = PixImage<byte>(Col.Format.RGBA, V2i(8,8))
            pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
                if c.X >= 4L then C4b.White
                else C4b.Black
                //rand.UniformC3f().ToC4b()
            ) |> ignore
            pi

        
        Log.startTimed "CPU"
        let cpu = ref.Transform(pi, Quantization.photoshop80) 
        Log.stop()

        let cpu = cpu |> Array.chunkBySize 64

        Log.startTimed "GPU"
        let gpuData = comp.Transform(pi, Quantization.photoshop80) 
        let gpu = gpuData |> Array.chunkBySize 64
        for i in 0 .. gpu.Length - 1 do
            let cpu = cpu.[i]
            let gpu = gpu.[i]

            if cpu = gpu then 
                Log.line "block %d OK" i
            else 
                Log.start "block %d" i
                Log.error "block %d: ERROR" i
                Log.start "CPU"
                printMat8 cpu
                Log.stop()

                Log.start "GPU"
                printMat8 gpu
                Log.stop()

                Log.stop()

        Log.stop()
        
        
        Log.start "CPU"
        let encoded = ref.Encode(gpuData)
        printBits encoded
        let data = ref.ToImageData(pi.Size, Quantization.photoshop80, encoded)
        File.writeAllBytes @"C:\Users\Schorsch\Desktop\wtf.jpg" data
        Log.stop()

        Log.start "GPU"
        let encoded = comp.Compress(pi, Quantization.photoshop80)
        printBits encoded
        let data = ref.ToImageData(pi.Size, Quantization.photoshop80, encoded)
        File.writeAllBytes @"C:\Users\Schorsch\Desktop\wtf2.jpg" data
        Log.stop()


        ()

