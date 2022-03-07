namespace Aardvark.Assembler

open System
open System.IO
open Aardvark.Base


type SystemMemoryStream() =
    inherit Stream()

    let mutable data = Array.zeroCreate<byte> 128
    let mutable length = 0L
    let mutable position = 0L

    let ensureWriteable(endpos : int64) =
        if endpos > data.LongLength then
            Array.Resize(&data, Fun.NextPowerOfTwo (int endpos))

    override x.CanRead = true
    override x.CanSeek = true
    override x.CanWrite = true

    override x.Length = length

    override x.Position 
        with get() = position
        and set p = position <- p

    override x.Seek(offset : int64, origin : SeekOrigin) =
        match origin with
        | SeekOrigin.Begin -> 
            if offset < 0L || offset > length then raise <| new ArgumentOutOfRangeException("offset")
            position <- offset
            position
        | SeekOrigin.End -> 
            let newOffset = length + offset
            if newOffset < 0L || newOffset > length then raise <| new ArgumentOutOfRangeException("offset")
            position <- newOffset
            position
        | SeekOrigin.Current -> 
            let newOffset = position + offset
            if newOffset < 0L || newOffset > length then raise <| new ArgumentOutOfRangeException("offset")
            position <- newOffset
            position
        | o -> 
            raise <| new ArgumentOutOfRangeException(sprintf "bad origin: %A" o)

    override x.Write(buffer : byte[], index : int, count : int) =
        let newPos = position + int64 count
        ensureWriteable newPos
        Span<byte>(buffer, index, count).CopyTo(Span<byte>(data, int position, count))
        position <- newPos
        if newPos > length then length <- newPos

    override x.Read(buffer : byte[], index : int, count : int) =
        let readSize = min (int64 count) (length - position)
        if readSize <= 0L then 
            0
        else
            Span<byte>(data, int position, int readSize).CopyTo(Span<byte>(buffer, index, int readSize))
            position <- position + readSize
            int readSize

    override x.Flush() =
        ()

    override x.SetLength len =
        if len < 0L then raise <| new ArgumentOutOfRangeException("len")
        Array.Resize(&data, Fun.NextPowerOfTwo (int len))
        if position > len then position <- len
        length <- len

    override x.Dispose(_disposing : bool) =
        data <- null
        length <- -1L
        position <- -1L

    member x.ToMemory() =
        System.Memory<byte>(data, 0, int length)

    member x.Reset() =
        data <- Array.zeroCreate 128
        length <- 0L
        position <- 0L
