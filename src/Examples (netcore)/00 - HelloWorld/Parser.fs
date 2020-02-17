module Parser

open System
open System.IO
open System.Collections.Generic

open Aardvark.Base

let parseStream (s : Stream) = 
    let t = new StreamReader(s)

    // 1:x[0] 2:x[1] 3:x[2] 4:v[0] 5:v[1] 6:v[2] 7:mass 8:density 9:energy 10:material type 11:number of flaws 12:DIM-root of damage 13:S/sigma[0][0] 14:S/sigma[0][1] 15:S/sigma[0][2] 16:S/sigma[1][0] 17:S/sigma[1][1] 18:S/sigma[1][2] 19:S/sigma[2][0] 20:S/sigma[2][1] 21:S/sigma[2][2] 22:alpha_jutzi 23:pressure 24->24+number of flaws:activation thresholds for this particle
    //-9.724503491703503e+00	+3.875623563592026e+01	+7.621347413150763e+00	-2.397972994566778e+01	+9.932345543280366e+01	+1.890887445786145e+01	5.000000e+02	2.661836e+03	2.180371e+06	5.265390e-01	0	0	0	0	0.000000e+00	-1.190558e-01	-5.147173e+07	1.163500e+08	3.702428e+07	1.163500e+08	1.119055e+08	3.273650e+07	3.702428e+07	3.273650e+07	-6.043372e+07	1.000000e+00	8.382190e+09	
    
    let inline parseDouble (s : string) = 
        double s

    let mutable cnt = 0
    let vertices = List<V3d>()
    let velocities = List<V3d>()
    let mutable bb = Box3d.Invalid
    while not t.EndOfStream do  
        let l = t.ReadLine()
        let elems = 
            l.Split([|'\t'; ' '|], StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun s -> s.Trim())
        let vertex = V3d(parseDouble elems.[0], parseDouble elems.[1], parseDouble elems.[2])
        let v = V3d(parseDouble elems.[3], parseDouble elems.[4], parseDouble elems.[5])
        vertices.Add(vertex)
        velocities.Add(v)
        cnt <- cnt + 1
        bb.ExtendBy vertex

    vertices.ToArray(), velocities.ToArray(), bb


let parseFile (f : string) =
    use f = File.OpenRead f
    parseStream f