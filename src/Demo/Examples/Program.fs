module Program

open System
open System.IO
open Rendering.Examples

open System.Runtime.InteropServices
open System.Diagnostics
open System.Threading
open Microsoft.FSharp.NativeInterop
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.SceneGraph

module TreeDiff =
    open System.Collections.Generic

    type IUpdater<'op> =
        inherit IAdaptiveObject
        inherit IDisposable
        abstract member Update : AdaptiveToken -> 'op

    type Neighbourhood<'a> =
        {
            prev : Option<'a>
            self : Option<'a>
            next : Option<'a>
        }

    let (|InsertAfter|InsertBefore|InsertFirst|Update|) (n : Neighbourhood<'a>) =
        match n.self with
            | Some s -> Update s
            | None -> 
                match n.prev, n.next with
                    | Some p, _ -> InsertAfter(p)
                    | _, Some n -> InsertBefore(n)
                    | None, None -> InsertFirst

    
    [<Struct; CustomEquality; CustomComparison>]
    type Id private(value : int) =
        
        static let mutable currentValue = 0

        member private x.Value = value


        override x.ToString() = 
            match value with
                | -1 -> "invalid"
                | 0 -> "root"
                | _ -> "n" + string value

        override x.GetHashCode() = value
        override x.Equals o =
            match o with
                | :? Id as o -> o.Value = value
                | _ -> false
                
        member x.CompareTo (o : Id) =
            compare value o.Value

        interface IComparable with
            member x.CompareTo o =
                match o with
                    | :? Id as o -> compare value o.Value
                    | _ -> failwithf "[Id] cannot compare to %A" o
                    
        interface IComparable<Id> with
            member x.CompareTo o = x.CompareTo o
    
        static member New = Id(Interlocked.Increment(&currentValue))
        static member Root = Id(0)
        static member Invalid = Id(-1)


    type Path =
        | Node of Id
        | Field of path : Path * name : string
        | Item of path : Path * index : int

    type Operation<'a> =
        | Update of path : Path * value : 'a
        | InsertAt of node : Id * index : int * value : 'a
        | RemoveAt of node : Id * index : int
        | InsertAfter of anchor : Id * id : Id * value : 'a
        | InsertBefore of anchor : Id * id : Id * value : 'a
        | AppendChild of parent : Id * id : Id * value : 'a


    type IOperationReader<'a> = IOpReader<list<Operation<'a>>>

    module List =
        let monoid<'a> =
            {
                mempty = List.empty<'a>
                mappend = List.append
                misEmpty = List.isEmpty
            }

    

    type AListReader<'a>(input : alist<'a>, id : Id) =
        inherit AbstractReader<list<Operation<'a>>>(Ag.emptyScope, List.monoid)

        let reader = input.GetReader()
        
        override x.Release() =
            reader.Dispose()

        override x.Compute(token : AdaptiveToken) =
            let mutable old = reader.State
            let ops = reader.GetOperations token

            let self = Node id
            ops |> PDeltaList.toList |> List.collect (fun (i,op) ->
                let index = old.AsMap |> MapExt.reference i
                        
                match op with
                    | Set n ->
                        match index with
                            | MapExtImplementation.Existing(i, o) -> 
                                if Unchecked.equals o n then
                                    []
                                else
                                    [ Update(Item(self, i), n) ]

                            | MapExtImplementation.NonExisting i -> 
                                [ InsertAt(id, i, n) ]

                    | Remove -> 
                        match index with
                            | MapExtImplementation.Existing(i, o) ->
                                [ RemoveAt(id, i) ]
                            | _ ->
                                []
            )



    [<AbstractClass>]
    type AListUpdater<'a, 'op>(l : alist<'a>, m : Monoid<'op>) =
        inherit AdaptiveObject()

        let reader = l.GetReader()
        let updaters = Dict<Index, IUpdater<'op>>()

        abstract member Invoke : Neighbourhood<'a> * 'a -> 'op * Option<IUpdater<'op>>
        abstract member Revoke : Neighbourhood<'a> -> 'op

        member x.Dispose() =
            lock x (fun () ->
                updaters.Values |> Seq.iter (fun u -> u.Outputs.Remove x |> ignore)
                reader.Dispose()
                updaters.Clear()
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
            )

        member x.Update(token) =
            x.EvaluateIfNeeded token m.mempty (fun token ->
                let mutable old = reader.State
                let ops = reader.GetOperations token

                let ops =
                    ops |> PDeltaList.toList |> List.map (fun (i,op) ->
                        let (l,s,r) = MapExt.neighbours i old.AsMap
                        let l = l |> Option.map snd
                        let s = s |> Option.map snd
                        let r = r |> Option.map snd
                        match op with
                            | Set v -> 
                                old <- PList.set i v old
                                let op, updater = x.Invoke({ prev = l; self = s; next = r }, v)

                                match updaters.TryRemove i with
                                    | (true, o) -> o.Dispose() 
                                    | _ -> ()
                                
                                match updater with
                                    | Some u ->
                                        updaters.[i] <- u
                                    | None ->
                                        ()

                                op
                            | Remove -> 
                                old <- PList.remove i old
                                x.Revoke { prev = l; self = s; next = r }
                    )

                let updates = updaters.Values |> Seq.map (fun u -> u.Update token) |> Seq.fold m.mappend m.mempty

                let ops = ops |> List.fold m.mappend m.mempty
                m.mappend ops updates
            )
            
        interface IUpdater<'op> with
            member x.Dispose() = x.Dispose()
            member x.Update t = x.Update t
    
    [<AbstractClass>]
    type ValueUpdater<'a, 'op>(input : IMod<'a>, m : Monoid<'op>) =
        inherit AdaptiveObject()
        let mutable last = None

        abstract member Invoke : Option<'a> * 'a -> 'op
        
        member x.Dispose() =
            lock x (fun () ->
                last <- None
                input.Outputs.Remove x |> ignore
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
            )

        member x.Update token =
            x.EvaluateIfNeeded token m.mempty (fun token ->
                let n = input.GetValue token
                let res = x.Invoke(last, n)
                last <- Some n
                res
            )

        interface IUpdater<'op> with
            member x.Dispose() = x.Dispose()
            member x.Update t = x.Update t


    type NodeDescription =
        {
            key         : string
            title       : IMod<string>
            isFolder    : IMod<bool>
        }

    type Node(desc : NodeDescription, children : alist<Node>) =
        member x.Description = desc
        member x.Children = children

    type Tree = { roots : alist<Node> }

    type TreeOperation =
        | AddNode of parentKey : string * leftKey : string * key : string
        | RemNode of key : string
        | UpdateNode of oldKey : string * newKey : string
        | SetTitle of key : string * title : string
        | SetIsFolder of key : string * isFolder : bool

    type NodeUpdater(parent : string, n : Node) =
        inherit AdaptiveObject()

        let childUpdater = ChildrenUpdater(n.Description.key, n.Children)
        let mutable lastTitle = None
        let mutable isFolder = None

        
        member x.Description = n.Description

        member x.Kill() =
            lock x (fun () ->
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
                childUpdater.Kill()
            )

        member x.Remove(parent : string) =
            lock x (fun () ->
                let mutable foo = 0
                x.Outputs.Consume(&foo) |> ignore
                childUpdater.Kill()
                [RemNode n.Description.key]
            )

        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token [] (fun token ->
                [
                    match lastTitle, n.Description.title.GetValue token with
                        | Some o, n when o = n -> ()
                        | _, t ->
                            lastTitle <- Some t
                            yield SetTitle(n.Description.key, t)

                    match isFolder, n.Description.isFolder.GetValue token with
                        | Some o, n when o = n -> ()
                        | _, t ->
                            isFolder <- Some t
                            yield SetIsFolder(n.Description.key, t)

                    yield! childUpdater.Update(token)
                ]
            )

    and ChildrenUpdater(parent : string, children : alist<Node>) =
        inherit AdaptiveObject()
        let updaters = children |> AList.map (fun n -> NodeUpdater(parent, n))
        let reader = updaters.GetReader()
        
        member x.Kill() =
            reader.State |> Seq.iter (fun u -> u.Kill())
            reader.Dispose()


        member x.Update(token : AdaptiveToken) =
            x.EvaluateIfNeeded token [] (fun token ->
                let old = reader.State.AsMap
                let ops = reader.GetOperations token

                let deltas =
                    ops |> PDeltaList.toList |> List.collect (fun (i, op) ->
                        let l, s, r = MapExt.neighbours i old

                        match op with
                            | Set v ->
                                match s with
                                    | Some(_,s) -> 
                                        if s = v then
                                            v.Update(token)
                                        else
                                            let desc = s.Description
                                            [UpdateNode(v.Description.key, desc.key)]
                                    | None ->
                                        let desc = v.Description
                                        let lKey = 
                                            match l with
                                                | Some(_,l) -> l.Description.key
                                                | _ -> ""
                                        [AddNode(parent, lKey, desc.key)]
                            | Remove ->
                                match s with
                                    | Some(_,s) -> s.Remove(parent)
                                    | None -> []
                    )

                deltas @ (reader.State |> PList.toList |> List.collect (fun n -> n.Update(token)))

            )

    type Tree with
        member x.GetUpdater() =
            ChildrenUpdater("", x.roots)


        
open System.Threading.Tasks


let colorLockTest() =
    let l = ColoredLock<int>()
    
    let mutable counts : int[] = Array.zeroCreate 2
    let mutable maxCounts : int[] = Array.zeroCreate 2
    let mutable exCount = 0
    let mutable bad = false

    let size = 1 <<< 13
    let start = new ManualResetEventSlim(false)
    let countDown = new CountdownEvent(size)

    let rand = RandomSystem()
    for _ in 1 .. size do
        let a =
            async {
                do! Async.SwitchToNewThread()
                let nested = rand.UniformDouble() > 0.9
                let isColored = rand.UniformDouble() > 0.2
                let color = rand.UniformInt(counts.Length)
                start.Wait()
                if isColored then
                    if l.Status <> NotEntered then
                        bad <- true

                    l.Enter(color)
                    let cnt = Interlocked.Increment(&counts.[color])

                    if l.Status <> Colored color then
                        bad <- true

                    if cnt > maxCounts.[color] then
                        maxCounts.[color] <- cnt

                    if nested then
                        l.Enter()
                        if l.Status <> Exclusive then
                            bad <- true
                        l.Exit()
                        if l.Status <> Colored color then
                            bad <- true

                    Thread.Sleep(0)
                    Interlocked.Decrement(&counts.[color]) |> ignore
                    l.Exit()

                    if l.Status <> NotEntered then
                        bad <- true

                    countDown.Signal() |> ignore
                else
                    if l.Status <> NotEntered then
                        bad <- true
                    l.Enter()
                    if l.Status <> Exclusive then
                        bad <- true
                    let c = Interlocked.Increment(&exCount)
                    if c > 1 then bad <- true
                    Thread.Sleep(0)
                    Interlocked.Decrement(&exCount) |> ignore
                    l.Exit()
                    if l.Status <> NotEntered then
                        bad <- true
                    countDown.Signal() |> ignore
            }
        Async.Start a


    start.Set()
    countDown.Wait()
    printfn "count: %A" maxCounts
    printfn "bad:   %A" bad


    let check should =
        if l.Status <> should then printfn "error: %A vs %A" l.Status should
        else printfn "good: %A" should

    check NotEntered
    l.Enter(1)
    check (Colored 1)
    l.Enter(1)
    check (Colored 1)
    l.Enter()
    check (Exclusive)
    l.Enter()
    check (Exclusive)
    l.Exit()
    check (Exclusive)
    l.Exit()
    check (Colored 1)
    l.Exit()
    check (Colored 1)
    l.Exit()
    check NotEntered
    Environment.Exit 0

open Aardvark.Rendering.Interactive

[<EntryPoint>]
[<STAThread>]
let main args =
    

    //Examples.Tutorial.run()
    //Examples.Instancing.run()
    //Examples.Render2TexturePrimitive.run()
    //Examples.Render2TextureComposable.run()
    //Examples.Render2TexturePrimiviteChangeableSize.run()
    //Examples.Render2TexturePrimitiveFloat.run()
    //Examples.ComputeTest.run()
    //colorLockTest()



    Ag.initialize()
    Aardvark.Init()
    
    Interactive.Renderer <- Vulkan
    
    Aardvark.Rendering.GL.RuntimeConfig.SupressSparseBuffers <- false
    //Examples.PostProcessing.run()
    //Examples.CommandTest.run()
    Examples.LoD.run()
    //Examples.Shadows.run()
    //Examples.AssimpInterop.run() 
    //Examples.ShaderSignatureTest.run()
    //Examples.Polygons.run()           attention: this one is currently broken due to package refactoring
    //Examples.TicTacToe.run()          attention: this one is currently broken due to package refactoring
    0
