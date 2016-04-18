namespace Aardvark.Rendering.GL

open System
open System.Threading
open System.Collections.Concurrent
open System.Runtime.InteropServices
open Aardvark.Base
open OpenTK
open OpenTK.Platform
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL

[<AutoOpen>]
module private VaoMemoryUsage =
    let addPhysicalVao (ctx:Context) =
        Interlocked.Increment(&ctx.MemoryUsage.PhysicalVertexArrayObjectCount) |> ignore
    let removePhysicalVao (ctx:Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.PhysicalVertexArrayObjectCount) |> ignore
    let addVirtualVao (ctx:Context) =
        Interlocked.Increment(&ctx.MemoryUsage.VirtualVertexArrayObjectCount) |> ignore
    let removeVirtualVao (ctx:Context) =
        Interlocked.Decrement(&ctx.MemoryUsage.VirtualVertexArrayObjectCount) |> ignore


type VertexArrayObject(context : Context, bindings : list<int * AttributeDescription>, index : Option<Buffer>, create : int -> unit) =
    inherit UnsharedObject(context, (fun _ -> let h = GL.GenVertexArray()
                                              addPhysicalVao context; create h; h), 
                                    (fun h -> removePhysicalVao context; 
                                              GL.DeleteVertexArray h))
        
    member x.Bindings = bindings
    member x.Index = index


[<AutoOpen>]
module VertexArrayObjectExtensions =
    open AttributeDescriptionExtensions

    let private init (index : Option<Buffer>) (attributes : list<int * AttributeDescription>) (handle : int) =
        GL.BindVertexArray handle
        GL.Check "could not bind VertexArrayObjects"

        for (id, att) in attributes do


            GL.BindBuffer(BufferTarget.ArrayBuffer, att.Buffer.Handle)
            GL.Check "could not bind buffer"


            if att.Type = typeof<M44f> then
                for i in 0..3 do
                    let id = id + i

                    match att.Frequency with
                        | PerVertex -> GL.VertexAttribDivisor(id, 0)
                        | PerInstances s -> GL.VertexAttribDivisor(id, s)
                    GL.Check "could not set vertex attribute frequency"

                    GL.EnableVertexAttribArray(id)
                    GL.Check "could not enable vertex attribute array"

                    GL.VertexAttribPointer(id, 4, VertexAttribPointerType.Float, false, 16 * sizeof<float32>, 4 * i * sizeof<float32>)
           
            else
                if (att.Buffer.Handle = 0) then
                    //GL.VertexAttrib4(id, Vector4(0.0f, 0.0f, 1.0f, 1.0f)) // this way a default can be defined
                    GL.DisableVertexAttribArray(id)

                    GL.Check "could not disable vertex attribute array"
                else
                    GL.EnableVertexAttribArray(id)

                    GL.Check "could not enable vertex attribute array"

                    if ExecutionContext.instancingSupported then
                        match att.Frequency with
                            | PerVertex -> GL.VertexAttribDivisor(id, 0)
                            | PerInstances s -> GL.VertexAttribDivisor(id, s)
                    GL.Check "could not set vertex attribute frequency"

                    let attType = 
                        match glTypes.TryGetValue att.BaseType with
                            | (true,v) -> v
                            | _ -> failwithf "[Aardvark.Rendering.GL] no GL type for given attribute type registered. Attribute has type: %A" att.BaseType.FullName
                    
                    if att.Type = typeof<C4b> then
                        GL.VertexAttribPointer(id, 0x80E1, attType, true, att.Stride, att.Offset)
                    else
                        GL.VertexAttribPointer(id, att.Dimension, attType, att.Normalized, att.Stride, att.Offset)

                    GL.Check "could not set vertex attribute pointer"


        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.Check "could not unbind buffer"


        match index with
            | Some index ->
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, index.Handle)
                GL.Check "could not bind element buffer"
            | None ->
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0)
                GL.Check "could not unbind element buffer"

        GL.BindVertexArray 0
        GL.Check "could not unbind VertexArrayObjects"

    type Context with
        member x.CreateVertexArrayObject(index : Buffer, attributes : list<int * AttributeDescription>) =
            addVirtualVao x
            VertexArrayObject(x, attributes, Some index, init (Some index) attributes)

        member x.CreateVertexArrayObject(attributes : list<int * AttributeDescription>) =
            addVirtualVao x
            VertexArrayObject(x, attributes, None, init None attributes)

        member x.Delete(vao : VertexArrayObject) =
            removeVirtualVao x
            vao.DestroyHandles()

        member x.Update(vao : VertexArrayObject, index : Buffer, attributes : list<int * AttributeDescription>) =
            let newInitFun h =
                let h = GL.GenVertexArray()
                addPhysicalVao vao.Context
                init (Some index) attributes h
                h

            vao.Update(newInitFun)

        member x.Update(vao : VertexArrayObject, attributes : list<int * AttributeDescription>) =
            let newInitFun h =
                let h = GL.GenVertexArray()
                addPhysicalVao vao.Context
                init None attributes h
                h

            vao.Update(newInitFun)


    module ExecutionContext =
        let bindVertexArray (vao : VertexArrayObject) =
            seq {
                if ExecutionContext.vertexArrayObjectsSupported then
                    // simply use the VAO handle if supported
                    yield Instruction.BindVertexArray vao.Handle

                else
                    // bind all attributes (buffers and view-properties)
                    for (index, att) in vao.Bindings do
                        yield! ExecutionContext.bindVertexAttribute index att

                    // bind the index-buffer (if available)
                    match vao.Index with
                        | Some index -> yield! ExecutionContext.bindBuffer BufferTarget.ElementArrayBuffer index
                        | None -> yield! ExecutionContext.unbindBuffer BufferTarget.ElementArrayBuffer
            }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module VertexArrayObject =

    let create (context : Context) (index : Option<Buffer>) (bindings : list<int * AttributeDescription>) =
        match index with
            | Some index -> context.CreateVertexArrayObject(index, bindings)
            | None -> context.CreateVertexArrayObject(bindings)
