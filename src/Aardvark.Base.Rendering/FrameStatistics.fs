namespace Aardvark.Base

open System
open Aardvark.Base

type ResourceKind =
    | Unknown = 0
    | Buffer = 1
    | VertexArrayObject = 2
    | Texture = 3
    | UniformBuffer = 4
    | UniformLocation = 5
    | SamplerState = 6
    | ShaderProgram = 7
    | Renderbuffer = 8
    | Framebuffer = 9
    | IndirectBuffer = 10
    | DrawCall = 11
    | IndexBuffer = 12


type RenderToken() =
    static let empty = RenderToken()

    static member Empty = empty
    member x.IsEmpty = x = empty

    member x.AddMemory(m : Mem) = ()
    member x.RemoveMemory(m : Mem) = x.AddMemory(-m)

    member x.InPlaceResourceUpdate(kind : ResourceKind) = ()
    member x.ReplacedResource(kind : ResourceKind) = ()
    member x.CreatedResource(kind : ResourceKind) = ()

    member x.AddInstructions(total : int, active : int) = ()
    member x.AddDrawCalls(count : int, effective : int) = ()
    member x.AddSubTask(sorting : MicroTime, update : MicroTime, execution : MicroTime, submission : MicroTime) =
        ()
    member x.AddResourceUpdate(submission : MicroTime, execution : MicroTime) =
        ()
    member x.AddPrimitiveCount(cnt : int64) =
        ()