namespace Aardvark.Rendering.GL

open Aardvark.Base
open Aardvark.Base.Incremental

module CompiledRenderObjects =

    type CompiledRenderObject() =
        interface ICompiledRenderObj
    
    let prepare (runtime : Runtime) (ro : RenderObject) =
        if ro.OptimizedRepr <> null then failwith "tried to optimize and optimized render object."
        
        //let indices = ro.Indices |> ArrayBuffer |> runtime.CreateBuffer
        
        failwith "JD" 