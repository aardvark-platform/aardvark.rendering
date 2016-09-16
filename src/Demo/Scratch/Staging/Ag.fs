namespace Aardvark.Ag

open System
open System.Collections.Generic
open System.Reflection
open System.Reflection.Emit
open Microsoft.FSharp.Quotations
open Aardvark.Base
open QuotationCompiler

type SemanticAttribute() = inherit Attribute()

[<AutoOpen>]
module Operators =
    let (?) (o : 'a) (name : string) : 'b =
        failwith ""



module Test =
    
    type IList = interface end

    type Nil() = interface IList
    type Cons(h : int, t : IList) =
        interface IList
        member x.Head = h
        member x.Tail = t


    [<Semantic; ReflectedDefinition>]
    type Sems() =
        member x.Sum(n : Nil) =
            0

        member x.Sum(c : Cons) =
            c.Head + c.Tail?Sum()


    [<Demo("Ag")>]
    let generate() =
        let types = 
            Introspection.GetAllTypesWithAttribute<SemanticAttribute>()
                |> Seq.map (fun t -> t.E0)
                |> HashSet

        let methods =
            types 
                |> Seq.collect (fun t -> t.GetMethods(BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic))
                |> Seq.filter (fun mi -> mi.GetParameters().Length = 1)
                |> Seq.groupBy (fun mi -> mi.Name)
                |> Seq.map (fun (n,mis) -> n,mis |> Seq.choose Expr.TryGetReflectedDefinition |> Seq.toList)
                |> Dictionary.ofSeq


        for (name, meths) in Dictionary.toSeq methods do
            Log.start "%s" name
            
            for m in meths do
                let test = QuotationCompiler.ToDynamicAssembly(m, "Ag")

                

                Log.line "%A" m
            
            Log.stop() 

        ()




