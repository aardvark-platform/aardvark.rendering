namespace Aardvark.Assembler

open System
open System.IO
open System.Runtime.InteropServices
open Aardvark.Base.Runtime

module AssemblerStream =
    let create (s : Stream) =
        match RuntimeInformation.ProcessArchitecture with
        | Architecture.X64 -> new Aardvark.Assembler.AMD64.AssemblerStream(s, true) :> IAssemblerStream
        | Architecture.Arm64 -> new ARM64.Arm64Stream(s, true) :> IAssemblerStream
        | a -> raise <| new NotSupportedException(sprintf "Architecture %A not supported" a)