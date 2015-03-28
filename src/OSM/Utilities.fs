namespace OSM 

open BruTile
open Aardvark.Base
open System.Threading.Tasks
open Aardvark.Base.Incremental

[<AutoOpen>]
module Extensions =

    let e = 8.1819190842622E-02
    let a = 6378137.0

    type ITileSchema with
        member x.GetBounds(level : string) =
            let minx = x.GetMatrixFirstCol level
            let miny = x.GetMatrixFirstRow level
            let sizeX = x.GetMatrixWidth level
            let sizeY = x.GetMatrixHeight level
            Box2i.FromMinAndSize(V2i(minx, miny), V2i(sizeX-1, sizeY-1))

        member x.GetExtent (region : Box2d) =
            let world = Box2d(x.Extent.MinX, x.Extent.MinY, x.Extent.MaxX, x.Extent.MaxY)
            //http://earth-info.nga.mil/GandG/wgs84/web_mercator/%28U%29%20NGA_SIG_0011_1.0.0_WEBMERC.pdf
            let thetaMin = region.Min.Y * Constant.RadiansPerDegree
            let thetaMax = region.Max.Y * Constant.RadiansPerDegree
            let minY = a * (Fun.Atanh (sin thetaMin) (*- e *  Fun.Atanh (e * sin thetaMin)*))
            let maxY = a * (Fun.Atanh (sin thetaMax) (*- e *  Fun.Atanh (e * sin thetaMax)*))

            Extent(
                x.Extent.Width  * (region.Min.X / 360.0),
                minY,
                x.Extent.Width  * (region.Max.X / 360.0),
                maxY
            )

    type TileInfo with
        member x.Bounds =
            Box2d(Constant.DegreesPerRadian * x.Extent.MinX / a, Constant.DegreesPerRadian * Fun.Asin(Fun.Tanh(x.Extent.MinY / a)), Constant.DegreesPerRadian * x.Extent.MaxX / a, Constant.DegreesPerRadian * Fun.Asin(Fun.Tanh(x.Extent.MaxY / a)))


    type Extent with
        member x.Box =
            Box2d(x.MinX, x.MinY, x.MaxX, x.MaxY)


    module Mod =
        

        let async (defaultValue : 'a) (t : Task<'a>) =

            let aw = ref None
            let r = Mod.initMod defaultValue

            let a = t.GetAwaiter()
            a.OnCompleted(fun () ->
                transact (fun () -> r.Value <- a.GetResult())
            )
            
            r :> IMod<_>
//
//
//                Mod.custom (fun () -> 
//                    if t.IsCompleted then t.Result
//                    else 
//                        let a = t.GetAwaiter()
//                        a.OnCompleted(fun () -> transact (fun () -> ()))
//                        aw := Some a
//                        defaultValue
//                )
//
//                
//            r

