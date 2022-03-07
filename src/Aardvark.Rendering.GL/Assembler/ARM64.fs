namespace Aardvark.Assembler

open System
open System.IO
open Aardvark.Base
open Aardvark.Base.Runtime
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open System.Collections.Generic

#nowarn "9"

module ARM64 =
    type Register =
        | R0 = 0
        | R1 = 1
        | R2 = 2
        | R3 = 3
        | R4 = 4
        | R5 = 5
        | R6 = 6
        | R7 = 7
        | R8 = 8
        | R9 = 9
        | R10 = 10
        | R11 = 11
        | R12 = 12
        | R13 = 13
        | R14 = 14
        | R15 = 15
        | R16 = 16
        | R17 = 17
        | R18 = 18
        | R19 = 19
        | R20 = 20
        | R21 = 21
        | R22 = 22
        | R23 = 23
        | R24 = 24
        | R25 = 25
        | R26 = 26
        | R27 = 27
        | R28 = 28
        | R29 = 29
        | R30 = 30
        | SP = 31


    module private Bitwise =
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let float32Bits (v : float32) =
            let ptr = NativePtr.stackalloc 1
            NativePtr.write ptr v
            NativePtr.read (NativePtr.ofNativeInt<uint32> (NativePtr.toNativeInt ptr))
            
        [<MethodImpl(MethodImplOptions.NoInlining)>]
        let floatBits (v : float) =
            let ptr = NativePtr.stackalloc 1
            NativePtr.write ptr v
            NativePtr.read (NativePtr.ofNativeInt<uint64> (NativePtr.toNativeInt ptr))


    [<AutoOpen>]
    module private Arm64Arguments =
        type Reg = Aardvark.Base.Runtime.Register

        let inline reg (r : Reg) = unbox<Register> r.Tag

        [<Flags>]
        type ArgumentKind =
            | None = 0
            | UInt32 = 1
            | UInt64 = 2
            | Float = 4
            | Double = 8
            | Indirect = 0x10

            | TypeMask = 0xF
            
        [<Struct>]
        type Argument =
            {
                Kind    : ArgumentKind
                Value   : uint64
            }

            member x.Integral =
                match x.Kind &&& ArgumentKind.TypeMask with
                | ArgumentKind.UInt32 | ArgumentKind.UInt64 -> true
                | _ -> false

            member x.ArgumentSize =
                if x.Kind &&& ArgumentKind.UInt32 <> ArgumentKind.None then 4
                elif x.Kind &&& ArgumentKind.UInt64 <> ArgumentKind.None then 8
                elif x.Kind &&& ArgumentKind.Float <> ArgumentKind.None then 4
                elif x.Kind &&& ArgumentKind.Double <> ArgumentKind.None then 8
                else failwithf "bad argument kind: %A" x.Kind

            static member UInt32(value : uint32) = { Kind = ArgumentKind.UInt32; Value = uint64 value }
            static member UInt64(value : uint64) = { Kind = ArgumentKind.UInt64; Value = value }
            static member Float(value : float32) = { Kind = ArgumentKind.Float; Value = uint64 (Bitwise.float32Bits value) }
            static member Double(value : float) = { Kind = ArgumentKind.Double; Value = Bitwise.floatBits value }

            static member UInt32Ptr(value : nativeptr<uint32>) = { Kind = ArgumentKind.Indirect ||| ArgumentKind.UInt32; Value = uint64 (NativePtr.toNativeInt value) }
            static member UInt64Ptr(value : nativeptr<uint64>) = { Kind = ArgumentKind.Indirect ||| ArgumentKind.UInt64; Value = uint64 (NativePtr.toNativeInt value) }
            static member FloatPtr(value : nativeptr<float32>) = { Kind = ArgumentKind.Indirect ||| ArgumentKind.Float; Value = uint64 (NativePtr.toNativeInt value) }
            static member DoublePtr(value : nativeptr<float>) = { Kind = ArgumentKind.Indirect ||| ArgumentKind.Double; Value = uint64 (NativePtr.toNativeInt value) }

    type Arm64AssemblerLabel internal() =
        let mutable position = -1L

        member x.Position
            with get() = position
            and internal set p = position <- p

    type Arm64Stream(baseStream : Stream, leaveOpen : bool) =
        let writer = new BinaryWriter(baseStream, System.Text.Encoding.UTF8, leaveOpen)

        let mutable totalArgs = 0
        let mutable argCount = 0
        let mutable arguments = Array.zeroCreate<Argument> 0

        let jumpIndices = Dict<AssemblerLabel, Dict<int64, voption<JumpCondition>>>()

        static let registerArguments =
            if RuntimeInformation.IsOSPlatform OSPlatform.OSX then 8
            else 9

        static let tmp1 =
            if RuntimeInformation.IsOSPlatform OSPlatform.OSX then Register.R8
            else Register.R9

        static let tmp2 =
            if RuntimeInformation.IsOSPlatform OSPlatform.OSX then Register.R9
            else Register.R10

        static let registers =
            Array.init 31 (fun i ->
                Reg(sprintf "X%d" i, int Register.R0 + i)
            )

        static let argumentRegisters =
            Array.take registerArguments registers

        static let calleeSavedRegisters =
            Array.sub registers 19 10

        let append (instruction : uint32) =
            writer.Write instruction
            // if isNull data then 
            //     data <- Array.zeroCreate 16
            //     data.[count] <- instruction
            //     count <- 1
            // else
            //     let newCount = count + 1
            //     if newCount >= data.Length then
            //         System.Array.Resize(&data, data.Length <<< 1)
            //     data.[count] <- instruction
            //     count <- newCount
        
        member x.BeginCall(args : int) =
            argCount <- args
            totalArgs <- args
            arguments <- Array.zeroCreate args

        member private x.PrepareArgs() =
            if argCount > 0 then failwithf "insufficient arguments (%d/%d missing)" argCount totalArgs

            let stackSpace =
                let mutable ii = 0
                let mutable fi = 0
                let mutable stackOffset = 0u
                for a in arguments do
                    if a.Integral then
                        if ii >= registerArguments then stackOffset <- stackOffset + uint32 a.ArgumentSize
                        ii <- ii + 1
                    else
                        if fi >= registerArguments then stackOffset <- stackOffset + uint32 a.ArgumentSize
                        fi <- fi + 1

                // SP needs to be a multiple of 16 for some reason
                if stackOffset &&& 0xFu = 0u then stackOffset
                else (1u + (stackOffset >>> 4)) <<< 4

            let mutable ii = 0
            let mutable fi = 0
            let mutable stackOffset = 0u

            if stackSpace > 0u then
                x.sub(true, Register.SP, uint16 stackSpace, Register.SP)

            
            for a in arguments do
                if a.Integral then
                    if ii < registerArguments then
                        let reg = unbox<Register> (int Register.R0 + ii)
                        x.mov(a, reg)
                    else
                        x.store(a, Register.SP, stackOffset)
                        stackOffset <- stackOffset + uint32 a.ArgumentSize
                    ii <- ii + 1
                else
                    if fi < registerArguments then
                        let reg = unbox<Register> (int Register.R0 + fi)
                        x.mov(a, reg)
                    else
                        x.store(a, Register.SP, stackOffset)
                        stackOffset <- stackOffset + uint32 a.ArgumentSize
                    fi <- fi + 1


            stackSpace

        member x.Call(ptr : nativeint) =    
            let stackSpace = x.PrepareArgs()

            x.mov(uint64 ptr, tmp1)
            x.blr(tmp1)
            
            if stackSpace > 0u then
                x.add(true, Register.SP, uint16 stackSpace, Register.SP)

            argCount <- 0
            totalArgs <- 0
            arguments <- null
            
        member x.CallIndirect(ptr : nativeptr<nativeint>) =    
            let stackSpace = x.PrepareArgs()

            x.mov(uint64 (NativePtr.toNativeInt ptr), tmp1)
            x.load(true, tmp1, 0u, tmp1)
            x.blr(tmp1)
            
            if stackSpace > 0u then
                x.add(true, Register.SP, uint16 stackSpace, Register.SP)

            argCount <- 0
            totalArgs <- 0
            arguments <- null

        member x.PushArg(value : uint32) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.UInt32 value
                
        member x.PushArg(value : int) =
            x.PushArg(uint32 value)

        member x.PushArg(value : float32) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.Float value
                
        member x.PushArg(value : float) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.Double value

        member x.PushArg(value : uint64) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.UInt64 value

        member x.PushArg(value : int64) =
            x.PushArg(uint64 value)

        member x.PushUInt32Arg(value : nativeptr<uint32>) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.UInt32Ptr value

        member x.PushInt32Arg(value : nativeptr<int>) =
            x.PushUInt32Arg(NativePtr.ofNativeInt (NativePtr.toNativeInt value))

        member x.PushUInt64Arg(value : nativeptr<uint64>) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.UInt64Ptr value

        member x.PushInt64Arg(value : nativeptr<int64>) =
            x.PushUInt64Arg(NativePtr.ofNativeInt (NativePtr.toNativeInt value))

        member x.PushFloat32Arg(value : nativeptr<float32>) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.FloatPtr value

        member x.PushFloatArg(value : nativeptr<float>) =
            let index = argCount - 1
            argCount <- index
            arguments.[index] <- Argument.DoublePtr value



        member x.movz(wide : bool, shift : uint8, imm : uint16, reg : Register) =
            append (
                0x52800000u |||                         // opcode
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                ((uint32 shift >>> 4) <<< 21) |||       // target-position (multiple of 16)
                (uint32 imm <<< 5) |||                  // value
                (uint32 reg &&& 0x1Fu)                  // target register
            )

        member x.movk(wide : bool, shift : uint8, imm : uint16, reg : Register) =
            append (
                0x72800000u |||                         // opcode
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                ((uint32 shift >>> 4) <<< 21) |||       // target-position (multiple of 16)
                (uint32 imm <<< 5) |||                  // value
                (uint32 reg &&& 0x1Fu)                  // target register
            )

        member x.ret() =
            append 0xd65f03c0u

        member x.blr(reg : Register) =
            append (
                0xD63F0000u |||                         // opcode
                (uint32 reg &&& 0x1Fu <<< 5)            // target register
            )

        member x.start() =
            append 0xd10083ffu // sub	sp, sp, #0x20
            append 0xa9017bfdu // stp   x29, x30, [sp, #0x10]

        member x.stop() =
            append 0xa9417bfdu // ldp	x29, x30, [sp, #0x10]
            append 0x910083ffu // add	sp, sp, #0x20

        member x.push(reg : Register) =
            x.sub(true, Register.SP, 0x10us, Register.SP)
            x.store(true, reg, 0u, Register.SP)

        member x.pop(reg : Register) =
            x.load(true, Register.SP, 0u, reg)
            x.add(true, Register.SP, 0x10us, Register.SP)

        member x.load(wide : bool, src : Register, offset : uint32, dst : Register) =
            let off = if wide then offset >>> 3 else offset >>> 2
            append (
                0xB9400000u |||                         // opcode
                (if wide then 0x40000000u else 0u) |||  // mode (32|64 bit)
                (uint32 off <<< 10) |||                 // offset
                (uint32 src <<< 5) |||                  // src register
                (uint32 dst)                            // dst register
            )

        member x.store(wide : bool, src : Register, offset : uint32, dst : Register) =
            let off = if wide then offset >>> 3 else offset >>> 2
            append (
                0xB9000000u |||                         // opcode
                (if wide then 0x40000000u else 0u) |||  // mode (32|64 bit)
                (uint32 off <<< 10) |||                 // offset
                (uint32 src) |||                        // src register
                (uint32 dst <<< 5)                      // dst register
            )

        member x.mov(wide : bool, src : Register, dst : Register) =
            append (
                0x2A0003E0u |||                         // opcode
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                (uint32 src &&& 0x1Fu <<< 16) |||       // source register
                (uint32 dst &&& 0x1Fu <<< 5)            // dst register
            )

        member x.sub(wide : bool, src : Register, value : uint16, dst : Register) =
            append (
                0x51000000u |||
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                (uint32 value &&& 0xFFFu <<< 10) |||    // value
                (uint32 src <<< 5) |||                  // src register
                (uint32 dst)                            // dst register
            )
            
        member x.add(wide : bool, src : Register, value : uint16, dst : Register) =
            append (
                0x11000000u |||
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                ((uint32 value &&& 0xFFFu) <<< 10) |||    // value
                (uint32 src <<< 5) |||                  // src register
                (uint32 dst)                            // dst register
            )
        
        member x.add(wide : bool, a : Register, b : Register, dst : Register) =
            append (
                0x0B000000u |||
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                (uint32 a <<< 16) |||                   // operand 1
                (uint32 b <<< 5) |||                    // operand 2
                (uint32 dst)                            // dst register
            )
            
        member x.mul(wide : bool, a : Register, b : Register, dst : Register) =
            append (
                0x1B007C00u |||
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                (uint32 a <<< 16) |||                   // operand 1
                (uint32 b <<< 5) |||                    // operand 2
                (uint32 dst)                            // dst register
            )

        member x.cmp(wide : bool, a : Register, b : Register) =
            append (
                0x6B00001Fu |||
                (if wide then 0x80000000u else 0u) |||  // mode (32|64 bit)
                (uint32 a <<< 5) |||                    // operand 1
                (uint32 b <<< 16)                       // operand 2
            )

        member x.jmp(offset : int) =
            let offset = offset / 4
            append (
                0x14000000u |||
                ((uint32 offset &&& 0x03FFFFFFu))
            )

        member x.jmp(condition : JumpCondition, offset : int) =
            let off = offset / 4
            let cond =
                match condition with
                | JumpCondition.Equal -> 0x0u
                | JumpCondition.NotEqual -> 0x1u
                | JumpCondition.Greater -> 0xCu
                | JumpCondition.GreaterEqual -> 0xAu
                | JumpCondition.Less -> 0xBu
                | JumpCondition.LessEqual -> 0xDu
                | c -> failwithf "bad jump condition: %A" c

            append (
                0x54000000u |||
                ((uint32 off &&& 0x7FFFFu) <<< 5) |||
                cond
            )



        member x.mov(data : uint64, reg : Register) =
            let a = uint16 data
            let b = uint16 (data >>> 16)
            let c = uint16 (data >>> 32)
            let d = uint16 (data >>> 48)

            x.movz(true, 0uy, a, reg)
            x.movk(true, 16uy, b, reg)
            x.movk(true, 32uy, c, reg)
            x.movk(true, 48uy, d, reg)
            
        member x.mov(data : uint32, reg : Register) =
            let a = uint16 data
            let b = uint16 (data >>> 16)

            x.movz(false, 0uy, a, reg)
            x.movk(false, 16uy, b, reg)

        member x.fmov(wide : bool, src : Register, dst : Register) : unit =
            ()
            // S001 1110 TT10 M11C 0000 00 (rr)

            if wide then

                append (
                    0x9E670000u |||
                    (uint32 src <<< 5) |||
                    (uint32 dst)
                )
            else
                // sf = 0
                // TT = 00
                // M = 0
                // C = 1
                // 0001 1110 0010 0111 0000 00 (rr)
                append (
                    0x1E270000u |||
                    (uint32 src <<< 5) |||
                    (uint32 dst)
                )


        member private x.mov(a : Argument, dst : Register) =
            if a.Kind &&& ArgumentKind.Indirect = ArgumentKind.None then
                match a.Kind with
                | ArgumentKind.UInt32 -> x.mov(uint32 a.Value, dst)
                | ArgumentKind.UInt64 -> x.mov(a.Value, dst)
                | ArgumentKind.Float -> 
                    x.mov(uint32 a.Value, tmp1)
                    x.fmov(false, tmp1, dst)
                | ArgumentKind.Double -> 
                    x.mov(a.Value, tmp1)
                    x.fmov(true, tmp1, dst)
                | k ->  
                    failwithf "bad argument-type: %A" k
            else
                x.mov(a.Value, tmp1)
                match a.Kind &&& ArgumentKind.TypeMask with
                | ArgumentKind.UInt32 -> x.load(false, tmp1, 0u, dst)
                | ArgumentKind.UInt64 -> x.load(true, tmp1, 0u, dst)
                | ArgumentKind.Float -> 
                    x.load(false, tmp1, 0u, tmp1)
                    x.fmov(false, tmp1, dst)
                | ArgumentKind.Double -> 
                    x.load(true, tmp1, 0u, tmp1)
                    x.fmov(true, tmp1, dst)
                | k ->  
                    failwithf "bad argument-type: %A" k

        member private x.store(a : Argument, ptr : Register, offset : uint32) =
            let dst = tmp1
            if a.Kind &&& ArgumentKind.Indirect = ArgumentKind.None then
                match a.Kind with
                | ArgumentKind.UInt32 -> 
                    x.mov(uint32 a.Value, dst)
                    x.store(false, dst, offset, ptr)
                | ArgumentKind.UInt64 -> 
                    x.mov(a.Value, dst)
                    x.store(true, dst, offset, ptr)
                | ArgumentKind.Float -> 
                    x.mov(uint32 a.Value, dst)
                    x.store(false, dst, offset, ptr)
                | ArgumentKind.Double -> 
                    x.mov(uint32 a.Value, dst)
                    x.store(true, dst, offset, ptr)
                | k ->  
                    failwithf "bad argument-type: %A" k
            else
                x.mov(a.Value, dst)
                match a.Kind &&& ArgumentKind.TypeMask with
                | ArgumentKind.UInt32 -> 
                    x.load(false, dst, 0u, dst)
                    x.store(false, dst, offset, ptr)
                | ArgumentKind.UInt64 -> 
                    x.load(true, dst, 0u, dst)
                    x.store(true, dst, offset, ptr)
                | ArgumentKind.Float -> 
                    x.load(false, dst, 0u, dst)
                    x.store(false, dst, offset, ptr)
                | ArgumentKind.Double -> 
                    x.load(true, dst, 0u, dst)
                    x.store(true, dst, offset, ptr)
                | k ->  
                    failwithf "bad argument-type: %A" k



        member x.mov(data : float32, reg : Register) =
            let a = Bitwise.float32Bits data
            x.mov(a, reg)
            x.fmov(false, reg, reg)
            
        member x.mov(data : float, reg : Register) =
            let a = Bitwise.floatBits data
            x.mov(a, reg)
            x.fmov(true, reg, reg)

        interface IAssemblerStream with
            member x.AddInt(dst : Reg, src : Reg, wide : bool) =
                x.add(wide, reg src, reg dst, reg dst)

            member x.MulInt(dst : Reg, src : Reg, wide : bool) =
                x.mul(wide, reg src, reg dst, reg dst)

            member x.BeginCall(count : int) =
                x.BeginCall count

            member x.BeginFunction() =
                x.start()

            member x.EndFunction() =
                x.stop()

            member x.Call(ptr : nativeint) =
                x.Call ptr

            member x.CallIndirect(ptr : nativeptr<nativeint>) =
                x.CallIndirect ptr

            member x.Copy(src : nativeint, dst : nativeint, wide : bool) =
                x.mov(uint64 src, tmp1)
                x.load(wide, tmp1, 0u, tmp2)
                x.mov(uint64 dst, tmp1)
                x.store(wide, tmp2, 0u, tmp1)

            member x.Mov(dst, src) =
                x.mov(true, reg src, reg dst)

            member x.Load(dst, src, wide) =
                x.load(wide, reg src, 0u, reg dst)

            member x.Dispose() =
                argCount <- 0
                totalArgs <- 0
                arguments <- null

            member x.Cmp(location : nativeint, value : int) : unit =
                x.mov(uint64 location, tmp1)
                x.load(true, tmp1, 0u, tmp1)
                x.mov(uint64 value, tmp2)
                x.cmp(false, tmp1, tmp2)

            member x.Mark(label : AssemblerLabel) =
                let l = Unsafe.As<Arm64AssemblerLabel>(label)
                l.Position <- baseStream.Position

                match jumpIndices.TryRemove label with
                | (true, indices) ->    
                    let o = baseStream.Position
                    try
                        for KeyValue(o, cond) in indices do
                            baseStream.Position <- o
                            let off = int (label.Position - o)
                            match cond with
                            | ValueNone -> x.jmp(off)
                            | ValueSome c -> x.jmp(c, off)
                    finally
                        baseStream.Position <- o
                | _ ->
                    ()

            member x.NewLabel() =
                let l = Arm64AssemblerLabel()
                Unsafe.As<AssemblerLabel>(l)

            member x.Jump(offset : int) : unit =
                x.jmp(offset + 4)
                
            member x.Jump(label : AssemblerLabel) : unit =
                if label.Position >= 0L then
                    let off = int (label.Position - baseStream.Position)
                    x.jmp(off)
                else
                    let set = jumpIndices.GetOrCreate(label, fun _ -> Dict())
                    set.[baseStream.Position] <- ValueNone
                    x.jmp 0
                
            member x.Jump(cond : JumpCondition, label : AssemblerLabel) : unit =
                if label.Position >= 0L then
                    let off = int (label.Position - baseStream.Position)
                    x.jmp(cond, off)
                else
                    let set = jumpIndices.GetOrCreate(label, fun _ -> Dict())
                    set.[baseStream.Position] <- ValueSome cond
                    x.jmp(cond, 0)

            member x.Push(r : Reg) =
                x.push(reg r)

            member x.Pop(r : Reg) =
                x.pop(reg r)

            member x.Set(r : Reg, value : int) =
                x.mov(uint32 value, reg r)

            member x.Set(r : Reg, value : nativeint) =
                x.mov(uint64 value, reg r)

            member x.Set(r : Reg, value : float32) =
                x.mov(value, reg r)

            member x.Store(dst, src, wide) =
                x.store(wide, reg src, 0u, reg dst)

            member x.WriteOutput(value : int) = x.mov(uint32 value, Register.R0)
            member x.WriteOutput(value : float32) = x.mov(value, Register.R0)
            member x.WriteOutput(value : nativeint) = x.mov(uint64 value, Register.R0)

            member x.PushArg(value : nativeint) = x.PushArg(uint64 value)
            member x.PushArg(value : int) = x.PushArg(uint32 value)
            member x.PushArg(value : float32) = x.PushArg(value)
            member x.PushIntArg(location : nativeint) = x.PushUInt32Arg(NativePtr.ofNativeInt location)
            member x.PushPtrArg(location : nativeint) = x.PushUInt64Arg(NativePtr.ofNativeInt location)
            member x.PushFloatArg(location : nativeint) = x.PushFloat32Arg(NativePtr.ofNativeInt location)
            member x.PushDoubleArg(location : nativeint) = x.PushFloatArg(NativePtr.ofNativeInt location)
            member x.Ret() = x.ret()

            member x.ArgumentRegisters = argumentRegisters
            member x.Registers = registers
            member x.ReturnRegister = registers.[0]
            member x.CalleeSavedRegisters = calleeSavedRegisters

