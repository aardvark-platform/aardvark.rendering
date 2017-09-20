namespace Jpeg

open System
open System.Runtime.InteropServices
open Aardvark.Base
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"

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

        let createEncodeTable (tree : HuffTree) =
            let max = 256
//            let tree = build counts values

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
            table = createEncodeTable tree
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
        member x.QLuminance : Arr<64 N, int> = uniform?QLuminance
        member x.QChroma : Arr<64 N, int> = uniform?QChroma

    let ycbcr (v : V3d) =
        let v = 255.0 * v
        V3d(
            Vec.dot Constants.y (V4d(v, 1.0)),
            Vec.dot Constants.cb (V4d(v, 1.0)),
            Vec.dot Constants.cr (V4d(v, 1.0))
        )

    [<GLSLIntrinsic("roundEven({0})")>]
    let roundEven (v : float) =
        System.Math.Round(v, 0, System.MidpointRounding.ToEven)

    let quantify (i : int) (v : V3d) =
        let ql = uniform.QLuminance.[i] |> max 1
        let qc = uniform.QChroma.[i] |> max 1
        let t = v / V3d(ql,qc,qc)
        V3i(int (round t.X), int (round t.Y), int (round t.Z))
        

    let inputImage =
        sampler2d {
            texture uniform?InputImage
            filter Filter.MinMagPoint
            addressU WrapMode.Clamp
            addressV WrapMode.Clamp
        }

    let dctFactor (m : int) (x : int) =
        //cos ( (2.0 * float m + 1.0) * Constant.Pi * float x / 16.0)
        Constants.cosTable.[m + 8 * x]

    [<GLSLIntrinsic("fma({0}, {1}, {2})")>]
    let fma (a : float) (b : float) (c : float) : float = failwith ""

    [<LocalSize(X = 8, Y = 8)>]
    let dct (target : V4i[]) =
        compute {
            let values : V3d[] = allocateShared 64

            let blockId = getWorkGroupId().XY 
            let blockCount = getWorkGroupCount().XY

            let gc = getGlobalId().XY
            let lc = getLocalId().XY
            let lid = lc.Y * 8 + lc.X
            let cid = Constants.inverseZigZagOrder.[lid]

            // every thread loads the RGB value and stores it in values (as YCbCr)
            values.[lid] <- ycbcr inputImage.[gc, 0].XYZ
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


    let add (l : V2i) (r : V2i) =
        if r.X <> 0 then
            V2i(l.X + r.X, r.Y)
        else
            if l.X <> 0 then
                V2i(l.X, l.Y)
            else
                V2i(0, r.Y)

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


    let dcLum = encoder.dcLuminance.table
    let acLum = encoder.acLuminance.table
    let dcChrom = encoder.dcChroma.table
    let acChrom = encoder.acChroma.table


    [<GLSLIntrinsic("findMSB({0})")>]
    let msb (v : uint32) =
        Fun.HighestBit (int v)

    [<GLSLIntrinsic("atomicOr({0}, {1})")>]
    let atomicOr (buf : uint32) (v : uint32) : unit =
        failwith ""
        
    let flipByteOrder (v : uint32) =
        let v = (v >>> 16) ||| (v <<< 16)
        ((v &&& 0xFF00FF00u) >>> 8) ||| ((v &&& 0x00FF00FFu) <<< 8)




    let encode (index : int) (chroma : bool) (leading : int) (value : int) : int * uint32 =
        let scale = msb (uint32 (abs value)) + 1
        let off = if value < 0 then (1 <<< scale) - 1 else 0
        let v = uint32 (off + value)

        let mutable key = scale
        if index <> 0 then
            key <- (leading <<< 4) ||| scale

        let mutable huff = 0u
        if index = 0 && chroma then huff <- dcChrom.[key]
        elif index = 0 then huff <- dcLum.[key]
        elif chroma then huff <- acChrom.[key]
        else huff <- acLum.[key]
            
        let len = Codeword.length huff
        let cc = Codeword.code huff
        len + scale, (cc <<< scale) ||| (v &&& ((1u <<< scale) - 1u))
       
    
    module Ballot = 
        [<GLSLIntrinsic("ballotARB({0})", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        let ballot (b : bool) : uint64 =
            failwith ""

        [<GLSLIntrinsic("gl_SubGroupLtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        let lessMask() : uint64 =
            failwith ""

        [<GLSLIntrinsic("gl_SubGroupGtMaskARB", "GL_ARB_shader_ballot", "GL_ARB_gpu_shader_int64")>]
        let greaterMask() : uint64 =
            failwith ""
            
        [<GLSLIntrinsic("findMSB({0})")>]
        let msb(v : uint32) : int =
            failwith ""

        [<GLSLIntrinsic("bitCount({0})")>]
        let pop(u : uint32) : int =
            failwith ""

    let leadingIsLastBallot (ev : bool) (ov : bool) =
        let eb = Ballot.ballot(ev) |> uint32
        let ob = Ballot.ballot(ov) |> uint32

        let i = getLocalId().X
        let ei = 2 * i
        let oi = ei + 1

        let less        = Ballot.lessMask() |> uint32
        let greater     = Ballot.lessMask() |> uint32

        let les = eb &&& less |> Ballot.msb
        let los = ob &&& less |> Ballot.msb

        let eLast =
            if les > los then 2 * les
            else 2 * los + 1
             
        let oLast =
            if ev then ei
            else eLast 
        
        let eLeading = ei - eLast - 1
        let oLeading = oi - oLast - 1

        let cae = eb &&& greater |> Ballot.pop
        let cao = ob &&& greater |> Ballot.pop

        let oIsLast = ov && cae = 0 && cao = 0
        let eIsLast = ev && oIsLast && not ov

        (eLeading, oIsLast, oLeading, eIsLast)


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
                let oo = offset % 32

                let space = 32 - oo
                if space >= size then
                    let a = code <<< (space - size) |> flipByteOrder
                    atomicOr target.[oi] a
                else 
                    let rest = size - space
                    let a = (code >>> rest) &&& ((1u <<< space) - 1u) |> flipByteOrder
                    atomicOr target.[oi] a

                    let b = (code &&& ((1u <<< rest) - 1u)) <<< (32 - rest) |> flipByteOrder
                    atomicOr target.[oi + 1] b

        }
      
      
//    [<LocalSize(X = 64)>]
//    let copyKernel (input : V4d[]) (output : V4d[]) =
//        compute {
//            let id = getGlobalId().X
//            output.[id] <- 2.0 * input.[id]
//        }

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
    let result = System.Collections.Generic.List<byte>(4 <<< 20)

    let writeByte b =
        if b = 0xFFuy then
            result.Add 0xFFuy
            result.Add 0x00uy
        else
            result.Add b
       
    let writeCurrent() =
        assert(currentLength = 32)
       
        Bit.take 0 8 current |> byte  |> writeByte
        Bit.take 8 8 current |> byte  |> writeByte
        Bit.take 16 8 current |> byte |> writeByte
        Bit.take 24 8 current |> byte |> writeByte
        
        current <- 0u
        currentLength <- 0

    member x.Write(word : uint32, offset : int, size : int) =
        let total = result.Count * 8 + currentLength

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
            if currentLength < 8 then
                let v = (Bit.take offset currentLength current) <<< (8 - currentLength) |> byte
                writeByte v
                currentLength <- 0
                offset <- offset + 8
            else
                writeByte (byte (Bit.take offset 8 current))
                currentLength <- currentLength - 8
                offset <- offset + 8

        current <- 0u
        currentLength <- 0

    member x.ToArray() =
        x.Flush()
        result.ToArray()

type JpegStream() =
    let bs = BitStream()

    let encode (result : System.Collections.Generic.List<V2i>) (chroma : bool) (dc : bool) (leading : int) (value : int) : unit =
        let (len, code) = Kernels.encode (if dc then 0 else 1) chroma leading value
        result.Add(V2i(len, int code))
        bs.Write(code, 32 - len, len)


//        let v = value
//
//        let table =
//            match chroma, dc with
//                | false, true  -> Kernels.encoder.dcLuminance
//                | false, false -> Kernels.encoder.acLuminance
//                | true,  true  -> Kernels.encoder.dcChroma
//                | true,  false -> Kernels.encoder.acChroma
//                
//        let scale = Fun.HighestBit(abs v) + 1
//        let off = if v < 0 then (1 <<< scale) - 1 else 0
//        let v = uint32 (off + v)
//        let key =
//            match dc with
//                | true -> scale
//                | false -> (leading <<< 4) ||| scale
//
//        let huff = table.table.[key]
////        let name = if chroma then "cr" else "lum"
////        let kind = if dc then "dc" else "ac"
////        printfn "code(%s, %s, %d, %d): %s %s" name kind leading v (Codeword.toString huff) (Codeword.toString (Codeword.create scale v))
//        bs.WriteCode table.table.[key]
//        bs.Write(v, 32 - scale, scale)


    let writeBlock (result : System.Collections.Generic.List<V2i>) (lastDC : V3i) (block : V3i[]) (offset : int) =
        for d in 0 .. 2 do
            let chroma = d <> 0
            encode result chroma true 0 (block.[offset].[d] - lastDC.[d])
            let mutable leading = 0
            let mutable i = 1 
            while i < 64 do
                while i < 64 && block.[offset + i].[d] = 0 do 
                    leading <- leading + 1
                    if i <> 63 then result.Add V2i.Zero
                    i <- i + 1

                if i < 64 then
                    let v = block.[offset + i]

                    while leading >= 16 do
                        encode result chroma false 15 0
                        leading <- leading - 16

                    encode result chroma false leading v.[d]
                    leading <- 0
                    i <- i + 1
                else
                    encode result chroma false 0 0
        block.[offset]

    static member Value(v : int) =
        let scale = Fun.HighestBit(abs v) + 1
        let off = if v < 0 then (1 <<< scale) - 1 else 0
        uint32 (off + v)

    member x.WriteBlocks(data : V3i[]) =
        let result = System.Collections.Generic.List<V2i>()
        let mutable offset = 0
        let mutable lastDC = V3i.Zero
        while offset < data.Length do
            lastDC <- writeBlock result lastDC data offset
            offset <- offset + 64

        result.ToArray()
           
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

    static let dctBlock (bi : int) (block : V3f[]) =
        if bi = 6340 then Log.warn "sadasdas"
        Array.init 64 (fun i ->
            let x = i % 8
            let y = i / 8
            let fx = if x = 0 then float32 Constant.Sqrt2Half else 1.0f
            let fy = if y = 0 then float32 Constant.Sqrt2Half else 1.0f
            let f = fx * fy

            let mutable sum = V3f.Zero
            let mutable i = 0
            for n in 0 .. 7 do
                for m in 0 .. 7 do
                    sum <- sum + block.[i] * (float32 <| Kernels.dctFactor m x) * (float32 <| Kernels.dctFactor n y)
                    i <- i + 1
                
            0.25f * f * sum
        )
        
    static let quantifyBlock (quality : Quantization) (block : V3f[]) =
        let r (v : float32) = round v |> int
        let round (v : V3f) = V4i(r v.X, r v.Y, r v.Z, 0)
        Array.map3 (fun ql qc v -> round(v / V3f(float32 ql,float32 qc,float32 qc))) quality.luminance quality.chroma block

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

        blocks |> Array.mapi (fun i b -> b |> ycbcrBlock |> dctBlock i |> quantifyBlock quality  |> zigZagBlock |> Array.map Vec.xyz) |> Array.concat

    member x.Encode(data : V3i[]) =
        let stream = JpegStream()
        let codes = stream.WriteBlocks data
        stream.ToArray(), codes


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


type JpegCompressor(runtime : Runtime) =
    let device = runtime.Device
    let pool = runtime.DescriptorPool

    let dct         = device |> ComputeShader.ofFunction Kernels.dct
    let codewords   = device |> ComputeShader.ofFunction Kernels.codewordsKernelBallot
    let assemble    = device |> ComputeShader.ofFunction Kernels.assembleKernel
    let scanner     = runtime.CompileScan <@ (+) : int -> int -> int @>
    
    member x.Runtime = runtime
    member x.DescriptorPool = pool
    member x.Device = device
    member x.DctShader = dct
    member x.CodewordShader = codewords
    member x.AssembleShader = assemble
    member x.Scan = scanner


    member x.NewInstance(size : V2i, quality : Quantization) =
        new JpegCompressorInstance(x, size, quality)

and JpegCompressorInstance(parent : JpegCompressor, size : V2i, quality : Quantization) =
    static let check (str : string) (err : VkResult) =
        if err <> VkResult.VkSuccess then failwithf "[Vulkan] %s" str
    
    let device = parent.Device
    let alignedSize = size |> Align.next2 8
    
    let alignedPixelCount   = int64 alignedSize.X * int64 alignedSize.Y
    let outputSize          = alignedPixelCount

    let dctBuffer           = device.CreateBuffer<V4i>(alignedPixelCount)
    let codewordBuffer      = device.CreateBuffer<V2i>(alignedPixelCount * 3L)

    let outputBuffer        = device.CreateBuffer<uint32>(outputSize)

    let cpuBuffer           = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit outputSize
    let bitCountBuffer      = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit 4L

    // TODO: flexible encoders??
    let encoder = Kernels.encoder

    let codewordCountView   = codewordBuffer.Coerce<int>().Strided(2)

    let tempData = Marshal.AllocHGlobal (2n * nativeint outputSize)


    let dctInput =
        let i = parent.DescriptorPool |> ComputeShader.newInputBinding parent.DctShader
        i.["size"] <- alignedSize
        i.["target"] <- dctBuffer
        i.["QLuminance"] <- quality.luminance
        i.["QChroma"] <- quality.chroma
        i.Flush()
        i
        
    let codewordInput = 
        let i = parent.DescriptorPool |> ComputeShader.newInputBinding parent.CodewordShader
        i.["data"] <- dctBuffer
        i.["codewords"] <- codewordBuffer
        i.Flush()
        i
        
    let assembleInput =
        let i = parent.DescriptorPool |> ComputeShader.newInputBinding parent.AssembleShader
        i.["codewords"] <- codewordBuffer
        i.["target"] <- outputBuffer
        i.["codewordCount"] <- int codewordBuffer.Count
        i.Flush()
        i

    let stopwatches = parent.DescriptorPool.CreateStopwatchPool(4)

    let dctCommand =
        command {
            do! stopwatches.Start 0
            do! Command.Bind parent.DctShader
            do! Command.SetInputs dctInput
            do! Command.Dispatch (alignedSize / V2i(8,8))
            do! stopwatches.Stop 0
        }

    let codewordCommand =
        command {
            do! stopwatches.Start 1
            do! Command.Bind(parent.CodewordShader)
            do! Command.SetInputs codewordInput
            do! Command.Dispatch(int dctBuffer.Count / 64, 3)
            do! stopwatches.Stop 1
        }

    let assembleCommand = 
        command {
            do! stopwatches.Start 2
            do! Command.ZeroBuffer outputBuffer
            do! Command.Bind(parent.AssembleShader)
            do! Command.SetInputs assembleInput
            do! Command.Dispatch(int codewordBuffer.Count / 64)
            do! stopwatches.Stop 2
        }

    let header =
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

    let mutable cleanup = None
    let overallCommand = 
        let run =
            command {
                do! stopwatches.Begin()
                do! dctCommand
                do! codewordCommand
                do! stopwatches.Start 3
                do! parent.Scan codewordCountView codewordCountView
                do! stopwatches.Stop 3
                do! assembleCommand
                do! Command.Copy(codewordBuffer, codewordBuffer.Size - 8L, bitCountBuffer, 0L, 4L)
                do! stopwatches.End()
            }
        { new Command() with
            member x.Compatible = run.Compatible
            member x.Enqueue cmd =
                cleanup <- run.Enqueue(cmd) |> Some
                Disposable.Empty
        }

    member x.Encode(image : Image) =
        dctInput.["InputImage"] <- image
        dctInput.Flush()
        overallCommand
        
    member x.ClearStats() =
        device.perform {
            do! stopwatches.Reset()
        }

    member x.GetStats() =
        let times = stopwatches.Download()

        Map.ofList [
            "dct", MicroTime times.[0]
            "code", MicroTime times.[1]
            "asm", MicroTime times.[2]
            "scan", MicroTime times.[3]
        ]


    member x.DownloadStream() =
        let numberOfBits : int = bitCountBuffer.Memory.Mapped NativeInt.read
        let byteCount = if numberOfBits % 8 = 0 then numberOfBits / 8 else 1 + numberOfBits / 8

        device.perform {
            do! Command.Copy(outputBuffer, 0L, cpuBuffer, 0L, int64 byteCount)
        }

        let ms = new System.IO.MemoryStream(byteCount * 2 + header.Length + 2)

        ms.Write(header, 0, header.Length)

        cpuBuffer.Memory.Mapped(fun ptr ->
            let mutable ptr = ptr

            for i in 0 .. byteCount - 1 do
                let v : byte = NativeInt.read ptr

                ms.WriteByte v

                if v = 0xFFuy then
                    ms.WriteByte 0x00uy

                ptr <- ptr + 1n
        )

        ms.WriteByte(0xFFuy)
        ms.WriteByte(0xD9uy)
        ms.ToArray()

    member x.Download() =
        let numberOfBits : int = bitCountBuffer.Memory.Mapped NativeInt.read
        let byteCount = if numberOfBits % 8 = 0 then numberOfBits / 8 else 1 + numberOfBits / 8

        device.GraphicsFamily.run {
            do! Command.Copy(outputBuffer, 0L, cpuBuffer, 0L, int64 byteCount)
        }

        let dstStart = tempData

        // write the jpeg header
        let mutable dst = dstStart
        Marshal.Copy(header, 0, dstStart, header.Length)
        dst <- dst + nativeint header.Length

        // copy the data
        let memHandle = cpuBuffer.Memory.Memory
        let mutable ptr = 0n
        VkRaw.vkMapMemory(device.Handle, memHandle.Handle, 0UL, uint64 byteCount, VkMemoryMapFlags.MinValue, &&ptr)
            |> check "could not map memory"


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
        VkRaw.vkUnmapMemory(device.Handle, memHandle.Handle)

        // write EOI
        NativeInt.write dst 0xD9FFus
        dst <- dst + 2n
        let finalLength = dst - dstStart |> int

        let result : byte[] = Array.zeroCreate finalLength

        Marshal.Copy(tempData, result, 0, finalLength)
        //Array.Resize(&result, finalLength)
        result

    member x.Compress(image : Image) =
        dctInput.["InputImage"] <- image
        dctInput.Flush()

        device.GraphicsFamily.run {
            do! overallCommand
        }

        x.Download()

    member x.Dispose() =
        dctInput.Dispose()
        codewordInput.Dispose()
        assembleInput.Dispose()

        device.Delete dctBuffer      
        device.Delete codewordBuffer 
        device.Delete outputBuffer   
        device.Delete cpuBuffer      
        device.Delete bitCountBuffer 
        match cleanup with
            | Some c -> c.Dispose()
            | None -> ()
        Marshal.FreeHGlobal tempData

    interface IDisposable with
        member x.Dispose() = x.Dispose()


type Compressor(runtime : Runtime) =
    let device = runtime.Device
    let pool = runtime.DescriptorPool

    let dct = device |> ComputeShader.ofFunction Kernels.dct
    let codewords = device |> ComputeShader.ofFunction Kernels.codewordsKernel
    let assemble = device |> ComputeShader.ofFunction Kernels.assembleKernel
    let scanner = runtime.CompileScan <@ (+) : int -> int -> int @>

    let queries = device.CreateQueryPool 2
    
    let codewords (input : Buffer<V4i>) (target : Buffer<V2i>) =
        let i = runtime.DescriptorPool |> ComputeShader.newInputBinding codewords

        //let target = device.CreateBuffer<V2i>(input.Count * 3L)
        i.["data"] <- input
        i.["codewords"] <- target
        i.Flush()

        command {
            try
                do! Command.Bind(codewords)
                do! Command.SetInputs i
                do! Command.Dispatch(int input.Count / 64, 3)
            finally
                i.Dispose()
        }

    let assemble (codewords : Buffer<V2i>) (output : Buffer<uint32>) =
        let i = runtime.DescriptorPool |> ComputeShader.newInputBinding assemble

        //let target = device.CreateBuffer<V2i>(input.Count * 3L)
        i.["codewords"] <- codewords
        i.["target"] <- output
        i.["codewordCount"] <- int codewords.Count
        i.Flush()
        
        command {
            try
                do! Command.ZeroBuffer output
                do! Command.Bind(assemble)
                do! Command.SetInputs i
                do! Command.Dispatch(int codewords.Count / 64)
            finally
                i.Dispose()
        }
 

    let transform(data : Image) (alignedSize : V2i) (quality : Quantization) (dctBuffer : Buffer<V4i>) =
        let dctInput = pool |> ComputeShader.newInputBinding dct
        
        dctInput.["InputImage"] <- data
        dctInput.["size"] <- alignedSize
        dctInput.["target"] <- dctBuffer
        dctInput.["QLuminance"] <- quality.luminance
        dctInput.["QChroma"] <- quality.chroma
        dctInput.Flush()

        command {
            try
                do! Command.Reset(queries)
                do! Command.BeginQuery(queries, 0)
                do! Command.Bind dct
                do! Command.SetInputs dctInput
                do! Command.Dispatch (alignedSize / V2i(8,8))
                do! Command.EndQuery(queries, 0)
            finally
                dctInput.Dispose()
        }


    member x.Compress(data : Image, quality : Quantization) =
        let alignedSize = data.Size.XY |> Align.next2 8
        let dctBuffer = device.CreateBuffer<V4i>(int64 alignedSize.X * int64 alignedSize.Y)
        let codewordBuffer = device.CreateBuffer<V2i>(dctBuffer.Count * 3L)
        let count = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit 4L
        let output = device.CreateBuffer<uint32>(int64 alignedSize.X * int64 alignedSize.Y)

        let cpuTarget = device.HostMemory |> Buffer.create VkBufferUsageFlags.TransferDstBit output.Size

        let codewordCountBuffer = codewordBuffer.Coerce<int>().Strided(2)

        let cmd = 
            command {
                try
                    do! transform data alignedSize quality dctBuffer
                    do! codewords dctBuffer codewordBuffer
                    do! scanner codewordCountBuffer codewordCountBuffer
                    do! assemble codewordBuffer output
                    do! Command.Copy(codewordBuffer, codewordBuffer.Size - 8L, count, 0L, 4L)
                finally
                    device.Delete dctBuffer
                    device.Delete codewordBuffer
                    device.Delete output
            }

        let download() = 
            let numberOfBits : int = count.Memory.Mapped NativeInt.read
            let byteCount = if numberOfBits % 8 = 0 then numberOfBits / 8 else 1 + numberOfBits / 8

            device.perform {
                do! Command.Copy(output, 0L, cpuTarget, 0L, int64 byteCount)
            }

            let mutable result : byte[] = Array.zeroCreate (byteCount * 2)
            let mutable finalLength = 0

            result |> NativePtr.withA (fun dst ->
                cpuTarget.Memory.Mapped(fun ptr ->
                    let mutable ptr = ptr
                    let dstStart = NativePtr.toNativeInt dst
                    let mutable dst = dstStart

                    for i in 0 .. byteCount - 1 do
                        let v : byte = NativeInt.read ptr

                        //result.[oi] <- v
                        NativeInt.write dst v
                        dst <- dst + 1n

                        if v = 0xFFuy then
                            NativeInt.write dst 0x00uy
                            dst <- dst + 1n

                        ptr <- ptr + 1n
                    
                    finalLength <- dst - dstStart |> int
                )
            )

            System.Array.Resize(&result, finalLength)
            result

        cmd, download

    member x.GetCodewords(data : Image, quality : Quantization) =
        let alignedSize = data.Size.XY |> Align.next2 8
        let dctBuffer = device.CreateBuffer<V4i>(int64 alignedSize.X * int64 alignedSize.Y)
        let codewordBuffer = device.CreateBuffer<V2i>(dctBuffer.Count * 3L)

        let codewordCountBuffer = codewordBuffer.Coerce<int>().Strided(2)

        device.perform {
            do! transform data alignedSize quality dctBuffer
            do! codewords dctBuffer codewordBuffer
            do! scanner codewordCountBuffer codewordCountBuffer
        }

        let dct = dctBuffer.Download()
        let res = codewordBuffer.Download()
        device.Delete dctBuffer
        device.Delete codewordBuffer
        res, dct


    member x.Times =
        device.GetResults(queries) |> Array.map MicroTime

    member x.Transform(data : Image, quality : Quantization) =
        let alignedSize = data.Size.XY |> Align.next2 8
        let dctBuffer = device.CreateBuffer<V4i>(int64 alignedSize.X * int64 alignedSize.Y)

        command {
            try
                do! transform data alignedSize quality dctBuffer
            finally
                device.Delete dctBuffer
        }

    member x.Transform(data : PixImage<'a>, quality : Quantization) =
        //assert(data.Size.X % 8 = 0 && data.Size.Y % 8 = 0)
        let image = device.CreateImage(PixTexture2d(PixImageMipMap [| data :> PixImage |], TextureParams.empty))
        let alignedSize = Align.next2 8 data.Size
        let dctBuffer = device.CreateBuffer<V4i>(int64 alignedSize.X * int64 alignedSize.Y)

        device.perform {
            do! transform image alignedSize quality dctBuffer
        }

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
            str <- str + ""
        str

    let photoshop =
        [|
            0x3Fuy; 0x5Buy; 0xBDuy; 0x6Auy; 0x4Duy; 0x7Fuy; 0xCCuy; 0x1Fuy; 0x99uy; 0xFAuy; 0x44uy; 0xD7uy; 0xB3uy; 0xAEuy; 0x93uy; 0x00uy; 0x1Auy; 0xFCuy; 0x2Buy; 0x71uy; 0x2Buy; 0xC0uy; 0xF0uy; 0xC5uy; 0xC1uy; 0xEFuy; 0x2Duy; 0xF5uy; 0x38uy; 0xDEuy; 0xCEuy; 0x58uy; 0x16uy; 0xE2uy; 0xF0uy; 0xFAuy; 0x6Buy; 0x3Auy; 0xA2uy; 0xCEuy; 0xF1uy; 0xCBuy; 0x15uy; 0x27uy; 0x92uy; 0x18uy; 0x65uy; 0x89uy; 0xF3uy; 0x3Buy; 0x53uy; 0xD9uy; 0xDAuy; 0x6Duy; 0x4Euy; 0x82uy; 0x5Duy; 0x9Fuy; 0x51uy; 0xC5uy; 0x59uy; 0xCEuy; 0x1Euy; 0x2Fuy; 0x0Fuy; 0x11uy; 0xF1uy; 0x0Cuy; 0x0Euy; 0x38uy; 0xC8uy; 0x47uy; 0x26uy; 0x3Cuy; 0x92uy; 0xC9uy; 0x97uy; 0x1Cuy; 0x81uy; 0x12uy; 0xC0uy; 0x32uy; 0x78uy; 0x79uy; 0x66uy; 0x06uy; 0x49uy; 0x63uy; 0xFCuy; 0xC4uy; 0xA1uy; 0x2Duy; 0x31uy; 0xC3uy; 0xEDuy; 0xADuy; 0x4Cuy; 0xB2uy; 0xEAuy; 0x34uy; 0xBAuy; 0x59uy; 0xCAuy; 0x03uy; 0x4Fuy; 0x28uy; 0x48uy; 0x4Buy; 0xF7uy; 0xD1uy; 0x95uy; 0xC7uy; 0x1Cuy; 0xB0uy; 0x65uy; 0x86uy; 0x6Cuy; 0x58uy; 0xE1uy; 0x39uy; 0x1Auy; 0x80uy; 0x87uy; 0x8Buy; 0x18uy; 0xE3uy; 0x9Euy; 0x63uy; 0xAAuy; 0xC7uy; 0x9Buy; 0x06uy; 0x39uy; 0x43uy; 0x34uy; 0x73uy; 0x62uy; 0x9Euy; 0x6Fuy
        |]


    let valildateCodewords (runtime : Runtime) (pi : PixImage<byte>)=
        let comp = Compressor(runtime)
        let device = runtime.Device
        let image = device.CreateImage(PixTexture2d(PixImageMipMap [| pi :> PixImage |], TextureParams.empty))
        let words, dct = comp.GetCodewords(image, Quantization.photoshop80)
            
        let mutable leading = 0

        let mutable last = V2i.Zero
        for gid in 0 .. words.Length - 1 do
            let w = words.[gid]
            let lid = gid % 192
            let block = gid / 192

            let lIndex = lid % 64
            let lChannel = lid / 64
            let dIndex = block * 64 + lIndex
            let v = dct.[dIndex].[lChannel]
            if lIndex = 0 then leading <- 0
            

 

            let channelName = 
                match lChannel with
                    | 0 -> "Y"
                    | 1 -> "Cb"
                    | _ -> "Cr"
            let off = last.X
            let len = w.X - last.X
            last <- w

            let isLast = 
                let mutable res = true
                for o in lIndex+1 .. 63 do
                    if dct.[block * 64 + o].[lChannel] <> 0 then res <- false
                res

            if lIndex = 0 || v <> 0 || (not isLast && leading >= 15) || lIndex = 63 then
                let v = if lIndex = 0 && block > 0 then v - dct.[dIndex - 64].[lChannel] else v

                let l = if lIndex = 63 && v = 0 then 0 else leading

                let (cpuLength, cpuCode) = Kernels.encode lIndex (lChannel <> 0) l v

                let w = uint32 w.Y

                if len <> cpuLength || Bit.take (32 - len) len w <> Bit.take (32 - cpuLength) cpuLength cpuCode then
                    let table =
                        match lChannel, lIndex with
                            | 0, 0 -> Kernels.encoder.dcLuminance
                            | _, 0 -> Kernels.encoder.dcChroma
                            | 0, _ -> Kernels.encoder.acLuminance
                            | _, _ -> Kernels.encoder.acChroma

                    let (code, bits) = table.decode len w
                    let (ll, dc) =
                        if lIndex = 0 then 0, int code
                        else int (code >>> 4), int (code &&& 0xFuy)

                    printfn "%A %A %A" ll dc bits
                    Log.warn "bad code"

                
//                else 
//                    Log.line "%s[%d/%d] = %A => (%d:%d:%s)" channelName block lIndex v off len (Bit.toString (32 - len) len w)

                leading <- 0

            else
                if len > 0 then
                    Log.warn "%s[%d] = %A => %d" channelName lid v len
                leading <- leading + 1

        device.Delete image


    let testCodewords(runtime : Runtime) =
        let comp = JpegCompressor(runtime)
        let device = runtime.Device

        let pi = PixImage.Create(@".\nature.jpeg").ToPixImage<byte>(Col.Format.RGBA)
        let instance = comp.NewInstance(pi.Size, Quantization.photoshop80)

        let image = device.CreateImage(PixTexture2d(PixImageMipMap [| pi :> PixImage |], TextureParams.empty))

        let cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.None)
        cmd.Enqueue (instance.Encode image)
        cmd.End()
        
        let ref = ReferenceCompressor()
        let queue = device.GraphicsFamily.GetQueue()
        // warmup
        for i in 1 .. 5 do queue.RunSynchronously cmd
        for i in 1 .. 5 do instance.Download() |> ignore

        instance.ClearStats()

        Log.start "encode"
        let iter = 1000
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            queue.RunSynchronously cmd
        sw.Stop()
        let tCompress = sw.MicroTime / iter
        Log.line "total : %A" tCompress
        let stats = instance.GetStats()
        for (name, time) in Map.toSeq stats do
            Log.line "%s: %A" name (time / iter)
        Log.stop()

        let mutable data = Array.zeroCreate 0
        printf " 0: download: "
        let iter = 1000
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            data <- instance.Download()
        sw.Stop()
        let tDownload = sw.MicroTime / iter
        printfn "%A" tDownload


        Log.line "total: %A" (tDownload + tCompress)


        
        printf " 0: compress: "
        let iter = 1000
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. iter do
            data <- instance.Compress image
        sw.Stop()
        let tDownload = sw.MicroTime / iter
        printfn "%A" tDownload



        File.writeAllBytes @"C:\Users\Schorsch\Desktop\new.jpg" data


        cmd.Dispose()




        

    let run() =
        use app = new HeadlessVulkanApplication(true)
        let device = app.Device
        
        testCodewords app.Runtime
        System.Environment.Exit 0


        let comp = Compressor(app.Runtime)
        let ref = ReferenceCompressor()

        let rand = RandomSystem()

        let pi =
            PixImage.Create(@"C:\Users\Schorsch\Desktop\nature2.jpg").ToPixImage<byte>(Col.Format.RGBA)
//            let pi = PixImage<byte>(Col.Format.RGBA, V2i(1920,1080))
//            pi.GetMatrix<C4b>().SetByCoord(fun (c : V2l) ->
////                if c.X >= 4L then C4b.Red
////                else C4b.Green
//                rand.UniformC3f().ToC4b()
//            ) |> ignore
//            pi

        
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

            if cpu <> gpu then 
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
        
        
        let cpu, cpuCodes = ref.Encode(gpuData)
        let data = ref.ToImageData(pi.Size, Quantization.photoshop80, cpu)
        File.writeAllBytes @"C:\Users\Schorsch\Desktop\wtf.jpg" data

        let image = device.CreateImage(PixTexture2d(PixImageMipMap [|pi :> PixImage|], TextureParams.empty))
        Log.startTimed "GPU"
        let gpuCompress, download = comp.Compress(image, Quantization.photoshop80)
        Log.stop()

        let tcmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        tcmd.Begin(CommandBufferUsage.None)
        tcmd.Enqueue(comp.Transform(image, Quantization.photoshop80))
        tcmd.End()

    
        // warmup
        let queue = device.GraphicsFamily.GetQueue()
        for i in 1 .. 5 do queue.RunSynchronously(tcmd)

        let iter = 10
        let results = Array.zeroCreate iter

        for e in 0 .. iter - 1 do
            printf " 0: transform: "
            let sw = System.Diagnostics.Stopwatch.StartNew()
            for i in 1 .. 10000 do
                queue.RunSynchronously(tcmd)
            sw.Stop()
            let time = sw.MicroTime / 10000
            results.[e] <- time
            printfn "%A" time

        let avg = results |> Array.averageBy (fun v -> float v.TotalNanoseconds)
        let stddev = (results |> Array.sumBy (fun v -> Fun.Square(float v.TotalNanoseconds - avg))) / (float iter - 1.0)
        let dev = sqrt stddev |> int64 |> MicroTime
        let avg = avg |> int64 |> MicroTime


        Log.line "average:   %A" avg
        Log.line "deviation: %A" dev

        System.Environment.Exit 0


        let cmd = device.GraphicsFamily.DefaultCommandPool.CreateCommandBuffer(CommandBufferLevel.Primary)
        cmd.Begin(CommandBufferUsage.None)
        cmd.Enqueue gpuCompress
        cmd.End()
        

        printf " 0: compress: "
        let sw = System.Diagnostics.Stopwatch.StartNew()
        for i in 1 .. 1000 do
            device.GraphicsFamily.GetQueue().RunSynchronously(cmd)
        sw.Stop()
        printfn "%A" (sw.MicroTime / 1000)

        let times = comp.Times
        for i in 0 .. times.Length - 1 do
            Log.line "%d: %A" i times.[i]
        
        printf " 0: download: "
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let gpu = download()
        sw.Stop()
        printfn "%A" sw.MicroTime

        cmd.Dispose()



        let data = ref.ToImageData(pi.Size, Quantization.photoshop80, gpu)
        File.writeAllBytes @"C:\Users\Schorsch\Desktop\wtf2.jpg" data

        if cpu = gpu then 
            Log.line "OK"
        else 
//            let cpu = printBits cpu
//            let gpu = printBits gpu

//            File.writeAllLines @"C:\Users\Schorsch\Desktop\cpu.txt" (cpu.ToCharArray() |> Array.map (fun c -> System.String [|c|]))
//            File.writeAllLines @"C:\Users\Schorsch\Desktop\gpu.txt" (gpu.ToCharArray() |> Array.map (fun c -> System.String [|c|]))
//            File.writeAllLines @"C:\Users\Schorsch\Desktop\photoshop.txt" ((printBits photoshop).ToCharArray() |> Array.map (fun c -> System.String [|c|]))
//            
            let gpuImg = PixImage.Create(@"C:\Users\Schorsch\Desktop\wtf2.jpg").ToPixImage<byte>(Col.Format.RGB)
            let cpuImg = PixImage.Create(@"C:\Users\Schorsch\Desktop\wtf.jpg").ToPixImage<byte>(Col.Format.RGB)

            let diffImg = PixImage<byte>(Col.Format.RGB, gpuImg.Size)
            diffImg.GetMatrix<C4b>().SetMap2(gpuImg.GetMatrix<C4b>(), cpuImg.GetMatrix<C4b>(), fun (a : C4b) (b : C4b) -> 
                let d = ((a.ToV4i() - b.ToV4i())).Abs
                C4b((if d.X = 0 then 0uy else 255uy), (if d.Y = 0 then 0uy else 255uy), (if d.Z = 0 then 0uy else 255uy))
            ) |> ignore
            diffImg.SaveAsImage @"C:\Users\Schorsch\Desktop\diff.png"

//            Log.line "%s" cpu
//            Log.line "%s" gpu
            Log.warn "ERROR"



        ()

