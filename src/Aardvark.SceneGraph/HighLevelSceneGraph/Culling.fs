module Culling

//    [<Semantic>]
//    type CullNodeSem() =
//        member x.RenderJobs(c : ViewFrustumCullNode) :  aset<RenderJob>=
//            let intersectsFrustum (b : Box3d) (f : Trafo3d) =
//                b.IntersectsFrustum(f.Forward)
//            
//            aset {
//
//                let! child = c.Child
//                let jobs = child?RenderJobs() : aset<RenderJob>
//
//                let viewProjTrafo = c?ViewProjTrafo() : IMod<Trafo3d>
//
//                yield! jobs |> ASet.filterM (fun rj -> Mod.map2 intersectsFrustum (rj.GetBoundingBox()) viewProjTrafo)
////
////                for rj : RenderObject in jobs do
////                    let! viewProjTrafo = c?ViewProjTrafo() : Mod<Trafo3d>
////                    let! bb = rj.GetBoundingBox().Mod
////                    if intersectsFrustum bb viewProjTrafo 
////                    then yield rj
//            }