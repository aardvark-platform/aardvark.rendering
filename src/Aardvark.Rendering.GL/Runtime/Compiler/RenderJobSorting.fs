namespace Aardvark.Rendering.GL.Compiler

open System
open System.Collections.Generic

open Aardvark.Base.Incremental
open Aardvark.Base
open Aardvark.Base.Rendering
open Aardvark.Rendering.GL
//
//module RenderJobSorting =
//
//    let private emptyMod = Mod.constant () :> IMod
//    let private projections = 
//        [| fun (r : RenderObject) -> r.Surface :> IMod
//
//           fun (r : RenderObject) -> 
//               match r.Uniforms.TryGetUniform (r.AttributeScope, DefaultSemantic.DiffuseColorTexture) with
//                   | Some t -> t
//                   | _ -> emptyMod
//
//           fun (r : RenderObject) -> if r.Indices <> null then r.Indices :> IMod else emptyMod 
//        |]
//
//    let project (rj : RenderObject) =
//        projections |> Array.map (fun f -> f rj) |> Array.toList
