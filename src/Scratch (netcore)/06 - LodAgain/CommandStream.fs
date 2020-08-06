namespace Aardvark.Rendering.GL

open OpenTK
open OpenTK.Graphics.OpenGL4
open Aardvark.Base
open Aardvark.Rendering
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

//#nowarn "9"

//type ICommandStream =
//    abstract member Enable : EnableCap -> unit
//    abstract member Disable : EnableCap -> unit
//    abstract member CullMode : CullMode -> unit

//[<AutoOpen>]
//module private NativeHelpersasdasd =

//    let inline nsize<'a> = nativeint sizeof<'a>
    
//    let inline read<'a when 'a : unmanaged> (ptr : byref<nativeint>) : 'a =
//        let a = NativePtr.read (NativePtr.ofNativeInt ptr)
//        ptr <- ptr + nsize<'a>
//        a

//type NativeMemoryCommandStreamOld() =
//    static let ptrSize = nativeint sizeof<nativeint>
//    static let initialSize = 256n
//    let mutable capacity = 256n
//    let mutable mem = Marshal.AllocHGlobal capacity
//    let mutable offset = 0n
//    let mutable count = 0

//    static let decode (ptr : nativeint) =
//        let code : InstructionCode  = NativeInt.read ptr
//        let ptr = ptr + 4n
//        let args = 
//            match code with
//                | InstructionCode.BindVertexArray -> [| NativeInt.read<int> ptr :> obj |]
//                | InstructionCode.UseProgram -> [| NativeInt.read<int> ptr :> obj |]
//                | InstructionCode.ActiveTexture -> [| NativeInt.read<TextureUnit> ptr :> obj |]
//                | InstructionCode.BindSampler -> [| NativeInt.read<int> ptr :> obj; NativeInt.read<int> (ptr + 4n) :> obj |]
//                | _ -> [||]
//        code, args
        
//    let resize (minSize : nativeint) =
//        let newCapacity = nativeint (Fun.NextPowerOfTwo(int64 minSize)) |> max initialSize
//        if capacity <> newCapacity then
//            mem <- Marshal.ReAllocHGlobal(mem, newCapacity)
//            capacity <- newCapacity

//    member x.Memory = mem
//    member x.Size = offset

//    static member RunInstruction(ptr : byref<nativeint>) =
//        let s : int = NativePtr.read (NativePtr.ofNativeInt ptr)
//        let fin = ptr + nativeint s
//        ptr <- ptr + 4n
//        let c : InstructionCode = NativePtr.read (NativePtr.ofNativeInt ptr)
//        ptr <- ptr + 4n

//        match c with
//            | InstructionCode.BindVertexArray -> 
//                GL.BindVertexArray (read<int> &ptr)

//            | InstructionCode.UseProgram -> 
//                GL.UseProgram(read<int> &ptr)

//            | InstructionCode.ActiveTexture ->
//                GL.ActiveTexture(read &ptr)

//            | InstructionCode.BindSampler ->
//                GL.BindSampler(read<int> &ptr, read<int> &ptr)

//            | InstructionCode.BindTexture ->
//                GL.BindTexture(read<TextureTarget> &ptr, read<int> &ptr)

//            | InstructionCode.BindBuffer ->
//                GL.BindBuffer(read<BufferTarget> &ptr, read<int> &ptr)

//            | InstructionCode.BindBufferBase ->
//                GL.BindBufferBase(read<BufferRangeTarget> &ptr, read<int> &ptr, read<int> &ptr)
                
//            | InstructionCode.BindBufferRange ->
//                GL.BindBufferRange(read<BufferRangeTarget> &ptr, read<int> &ptr, read<int> &ptr, read<nativeint> &ptr, read<nativeint> &ptr)

//            | InstructionCode.BindFramebuffer ->
//                GL.BindFramebuffer(read<FramebufferTarget> &ptr, read<int> &ptr)

//            | code ->
//                Log.warn "bad instruction: %A" code

//        ptr <- fin

//    static member Run(stream : NativeMemoryCommandStreamOld) =
//        let mutable ptr = stream.Memory
//        let e = ptr + stream.Size
//        while ptr <> e do
//            NativeMemoryCommandStreamOld.RunInstruction(&ptr)

//    member inline private x.Append(code : InstructionCode) =
//        let sa = nativeint sizeof<'a>
//        let size = 8n
//        if offset + size > capacity then resize (offset + size)
//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        offset <- offset + size
//        count <- count + 1


//    member inline private x.Append(code : InstructionCode, arg0 : 'a) =
//        let sa = nativeint sizeof<'a>
//        let size = 8n + sa
//        if offset + size > capacity then resize (offset + size)
        
//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa

//        offset <- offset + size
//        count <- count + 1

//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let size = 8n + sa + sb
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb

//        offset <- offset + size
//        count <- count + 1

//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let size = 8n + sa + sb + sc
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc

//        offset <- offset + size
//        count <- count + 1
        
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let size = 8n + sa + sb + sc + sd
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd

//        offset <- offset + size
//        count <- count + 1
        
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let size = 8n + sa + sb + sc + sd + se
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se

//        offset <- offset + size
//        count <- count + 1
         
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let size = 8n + sa + sb + sc + sd + se + sf
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf

//        offset <- offset + size
//        count <- count + 1
           
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg

//        offset <- offset + size
//        count <- count + 1
        
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let sh = nativeint sizeof<'h>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg + sh
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh

//        offset <- offset + size
//        count <- count + 1
        
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let sh = nativeint sizeof<'h>
//        let si = nativeint sizeof<'i>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg + sh + si
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si

//        offset <- offset + size
//        count <- count + 1
       
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let sh = nativeint sizeof<'h>
//        let si = nativeint sizeof<'i>
//        let sj = nativeint sizeof<'j>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg + sh + si + sj
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj

//        offset <- offset + size
//        count <- count + 1
        
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let sh = nativeint sizeof<'h>
//        let si = nativeint sizeof<'i>
//        let sj = nativeint sizeof<'j>
//        let sk = nativeint sizeof<'k>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg + sh + si + sj + sk
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk

//        offset <- offset + size
//        count <- count + 1
          
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k, arg11 : 'l) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let sh = nativeint sizeof<'h>
//        let si = nativeint sizeof<'i>
//        let sj = nativeint sizeof<'j>
//        let sk = nativeint sizeof<'k>
//        let sl = nativeint sizeof<'l>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg + sh + si + sj + sk + sl
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg11; ptr <- ptr + sl

//        offset <- offset + size
//        count <- count + 1
             
//    member inline private x.Append(code : InstructionCode, arg0 : 'a, arg1 : 'b, arg2 : 'c, arg3 : 'd, arg4 : 'e, arg5 : 'f, arg6 : 'g, arg7 : 'h, arg8 : 'i, arg9 : 'j, arg10 : 'k, arg11 : 'l, arg12 : 'm, arg13 : 'n, arg14 : 'o) =
//        let sa = nativeint sizeof<'a>
//        let sb = nativeint sizeof<'b>
//        let sc = nativeint sizeof<'c>
//        let sd = nativeint sizeof<'d>
//        let se = nativeint sizeof<'e>
//        let sf = nativeint sizeof<'f>
//        let sg = nativeint sizeof<'g>
//        let sh = nativeint sizeof<'h>
//        let si = nativeint sizeof<'i>
//        let sj = nativeint sizeof<'j>
//        let sk = nativeint sizeof<'k>
//        let sl = nativeint sizeof<'l>
//        let sm = nativeint sizeof<'m>
//        let sn = nativeint sizeof<'n>
//        let so = nativeint sizeof<'o>
//        let size = 8n + sa + sb + sc + sd + se + sf + sg + sh + si + sj + sk + sl + sm + sn + so
//        if offset + size > capacity then resize (offset + size)

//        let mutable ptr = mem + offset
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int size); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) (int code); ptr <- ptr + 4n
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg0; ptr <- ptr + sa
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg1; ptr <- ptr + sb
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg2; ptr <- ptr + sc
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg3; ptr <- ptr + sd
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg4; ptr <- ptr + se
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg5; ptr <- ptr + sf
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg6; ptr <- ptr + sg
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg7; ptr <- ptr + sh
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg8; ptr <- ptr + si
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg9; ptr <- ptr + sj
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg10; ptr <- ptr + sk
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg11; ptr <- ptr + sl
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg12; ptr <- ptr + sm
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg13; ptr <- ptr + sn
//        NativePtr.write (NativePtr.ofNativeInt ptr) arg14; ptr <- ptr + so
//        offset <- offset + size
//        count <- count + 1
           
     


//    member x.Dispose() =
//        Marshal.FreeHGlobal mem
//        capacity <- 0n
//        mem <- 0n
//        offset <- 0n



