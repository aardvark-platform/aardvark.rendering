namespace Aardvark.Base

open Microsoft.FSharp.NativeInterop
open System.Runtime.InteropServices
open System.Runtime.CompilerServices


#nowarn "9"


[<AbstractClass; Sealed; Extension>]
type NativeTensorExtensions private() =

    [<Extension>]
    static member MirrorX(this : NativeVector<'a>) =
        NativeVector<'a>(
            this.Pointer,
            VectorInfo(
                (this.SX - 1L) * this.DX,
                this.SX,
                -this.DX
            )
        )

    [<Extension>]
    static member MirrorX(this : NativeMatrix<'a>) =
        NativeMatrix<'a>(
            this.Pointer,
            MatrixInfo(
                this.Info.Index(this.SX - 1L, 0L),
                this.Size,
                V2l(-this.DX, this.DY)
            )
        )
        
    [<Extension>]
    static member MirrorX(this : NativeVolume<'a>) =
        NativeVolume<'a>(
            this.Pointer,
            VolumeInfo(
                this.Info.Index(this.SX - 1L, 0L, 0L),
                this.Size,
                V3l(-this.DX, this.DY, this.DZ)
            )
        )

    [<Extension>]
    static member MirrorX(this : NativeTensor4<'a>) =
        NativeTensor4<'a>(
            this.Pointer,
            Tensor4Info(
                this.Info.Index(this.SX - 1L, 0L, 0L, 0L),
                this.Size,
                V4l(-this.DX, this.DY, this.DZ, this.DW)
            )
        )
 
    [<Extension>]
    static member MirrorY(this : NativeMatrix<'a>) =
        NativeMatrix<'a>(
            this.Pointer,
            MatrixInfo(
                this.Info.Index(0L, this.SY - 1L),
                this.Size,
                V2l(this.DX, -this.DY)
            )
        )
        
    [<Extension>]
    static member MirrorY(this : NativeVolume<'a>) =
        NativeVolume<'a>(
            this.Pointer,
            VolumeInfo(
                this.Info.Index(0L, this.SY - 1L, 0L),
                this.Size,
                V3l(this.DX, -this.DY, this.DZ)
            )
        )
        
    [<Extension>]
    static member MirrorY(this : NativeTensor4<'a>) =
        NativeTensor4<'a>(
            this.Pointer,
            Tensor4Info(
                this.Info.Index(0L, this.SY - 1L, 0L, 0L),
                this.Size,
                V4l(this.DX, -this.DY, this.DZ, this.DW)
            )
        )        

    [<Extension>]
    static member MirrorZ(this : NativeVolume<'a>) =
        NativeVolume<'a>(
            this.Pointer,
            VolumeInfo(
                this.Info.Index(0L, 0L, this.SZ - 1L),
                this.Size,
                V3l(this.DX, this.DY, -this.DZ)
            )
        )
        
    [<Extension>]
    static member MirrorZ(this : NativeTensor4<'a>) =
        NativeTensor4<'a>(
            this.Pointer,
            Tensor4Info(
                this.Info.Index(0L, 0L, this.SZ - 1L, 0L),
                this.Size,
                V4l(this.DX, this.DY, -this.DZ, this.DW)
            )
        )

    [<Extension>]
    static member MirrorW(this : NativeTensor4<'a>) =
        NativeTensor4<'a>(
            this.Pointer,
            Tensor4Info(
                this.Info.Index(0L, 0L, 0L, this.SW - 1L),
                this.Size,
                V4l(this.DX, this.DY, this.DZ, -this.DW)
            )
        )



    [<Extension>]
    static member ToXYWTensor4(this : NativeVolume<'a>) =
        let info = this.Info
        let li = 1L + Vec.dot info.Delta (info.S - V3l.III)
        
        NativeTensor4<'a>(
            this.Pointer,
            Tensor4Info(
                info.Origin,
                V4l(info.SX, info.SY, 1L, info.SZ),
                V4l(info.DX, info.DY, li, info.DZ)
            )
        )