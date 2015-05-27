namespace Aardvark.SceneGraph.Semantics

open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Ag
open Aardvark.Base.AgHelpers
open Aardvark.SceneGraph

open Aardvark.SceneGraph.Internal

[<AutoOpen>]
module TrafoExtensions =

    let inline private trafo v : IMod<Trafo3d> = v 
    type ISg with
        member x.ModelTrafo             = x?ModelTrafo              |> trafo
        member x.ViewTrafo              = x?ViewTrafo               |> trafo
        member x.ProjTrafo              = x?ProjTrafo               |> trafo
        member x.ModelTrafoInv          = x?ModelTrafoInv()         |> trafo
        member x.ViewTrafoInv           = x?ViewTrafoInv()          |> trafo
        member x.ProjTrafoInv           = x?ProjTrafoInv()          |> trafo
        member x.ModelViewTrafo         = x?ModelViewTrafo()        |> trafo
        member x.ViewProjTrafo          = x?ViewProjTrafo()         |> trafo
        member x.ModelViewProjTrafo     = x?ModelViewProjTrafo()    |> trafo
        member x.ModelViewTrafoInv      = x?ModelViewTrafoInv()     |> trafo
        member x.ViewProjTrafoInv       = x?ViewProjTrafoInv()      |> trafo
        member x.ModelViewProjTrafoInv  = x?ModelViewProjTrafoInv() |> trafo
                        
[<AutoOpen>]
module TrafoSemantics =

    /// the root trafo for the entire Sg (used when no trafos are applied)
    let rootTrafo = Mod.initConstant Trafo3d.Identity
  
    [<Semantic>]
    type Trafos() =
        let mulCache = Caching.BinaryOpCache (Mod.map2 (*))
        let invCache = Caching.UnaryOpCache(Mod.map (fun (t : Trafo3d) -> t.Inverse))

        let (<*>) a b = 
            if a = rootTrafo then b
            elif b = rootTrafo then a
            else mulCache.Invoke a b

        let inverse t = invCache.Invoke t

        member x.ModelTrafo(e : Root) = 
            e.Child?ModelTrafo <- rootTrafo

        member x.ModelTrafo(t : Sg.TrafoApplicator) =
            t.Child?ModelTrafo <- t.Trafo <*> t.ModelTrafo



        member x.ViewTrafo(v : Sg.ViewTrafoApplicator) =
            v.Child?ViewTrafo <- v.ViewTrafo

        member x.ProjTrafo(p : Sg.ProjectionTrafoApplicator) =
            p.Child?ProjTrafo <- p.ProjectionTrafo

        member x.ViewTrafo(e : Sg.Environment) =
            e.Child?ViewTrafo <- e.ViewTrafo

        member x.ProjTrafo(e : Sg.Environment) =
            e.Child?ProjTrafo <- e.ProjTrafo


        member x.ModelTrafoInv(s : ISg) =
            s.ModelTrafo |> inverse

        member x.ViewTrafoInv(s : ISg) =
            s.ViewTrafo |> inverse

        member x.ProjTrafoInv(s : ISg) =
            s.ProjTrafo |> inverse


        member x.ModelViewTrafo(s : ISg) =
            s.ModelTrafo <*> s.ViewTrafo

        member x.ViewProjTrafo(s : ISg) =
            s.ViewTrafo <*> s.ProjTrafo

        member x.ModelViewProjTrafo(s : ISg) =
            s.ModelTrafo <*> s.ViewProjTrafo


        member x.ModelViewTrafoInv(s : ISg) =
            s.ModelViewTrafo |> inverse
        
        member x.ViewProjTrafoInv(s : ISg) =
            s.ViewProjTrafo |> inverse

        member x.ModelViewProjTrafoInv(s : ISg) =
            s.ModelViewProjTrafo |> inverse