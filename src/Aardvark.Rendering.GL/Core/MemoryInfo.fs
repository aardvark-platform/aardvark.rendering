namespace Aardvark.Rendering.GL

open System
open Aardvark.Base
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL4
open Aardvark.Rendering.GL
open System.Runtime.CompilerServices

type GPUMemoryInfo = 
    struct
        /// <summary>Dedicated video memory in bytes</summary>
        val mutable public Dedicated : int64
        /// <summary>Total memory for allocations in bytes</summary>
        val mutable public Total : int64
        /// <summary>Available memory for allocations in bytes</summary>
        val mutable public Available : int64
    end 

[<AutoOpen>]
module MemoryInfoUtils =
    
    type Context with

        member x.GetMemoryInfo() =
        
            using x.ResourceLock (fun _ ->

                // NVIDIA: 
                //GPU_MEMORY_INFO_DEDICATED_VIDMEM_NVX          0x9047
                //GPU_MEMORY_INFO_TOTAL_AVAILABLE_MEMORY_NVX    0x9048
                //GPU_MEMORY_INFO_CURRENT_AVAILABLE_VIDMEM_NVX  0x9049
                //GPU_MEMORY_INFO_EVICTION_COUNT_NVX            0x904A
                //GPU_MEMORY_INFO_EVICTED_MEMORY_NVX            0x904B

                let dedicated = GL.GetInteger64(0x9047 |> unbox<_>)
                if dedicated <> 0L then
                    let total = GL.GetInteger64(0x9048 |> unbox<_>)
                    let available = GL.GetInteger64(0x9049 |> unbox<_>)

                    GPUMemoryInfo(
                        Dedicated = dedicated,
                        Total = total,
                        Available = available )
                else
                    //AtiMeminfo.
                    //VboFreeMemoryAti
                    //TextureFreeMemoryAti
                    //RenderbufferFreeMemoryAti

                    let texMem1 = Array.create 4 0
                    GL.GetInteger(OpenTK.Graphics.OpenGL.AtiMeminfo.TextureFreeMemoryAti |> unbox<_>, texMem1);

                    // memory in Kbyte
                    //param[0] - total memory free in the pool
                    //param[1] - largest available free block in the pool
                    //param[2] - total auxiliary memory free
                    //param[3] - largest auxiliary free block

                    if texMem1.[0] <> 0 then
                        let total = (int64 texMem1.[0]) <<< 10
                        let available = (int64 texMem1.[2]) <<< 10
                        let dedicated = total // TODO: use wglGetGPUInfoAMD

                        GPUMemoryInfo( 
                            Dedicated = dedicated,
                            Total = total,
                            Available = available 
                        )

                        //GL_VBO_FREE_MEMORY_ATI 0x87FB
                        //GL_TEXTURE_FREE_MEMORY_ATI 0x87FC
                        //GL_RENDERBUFFER_FREE_MEMORY_ATI 0x87FD
                        //GL.
                        //+int i[4];
                        //+
                        //+printf("Memory info (GL_ATI_meminfo):\n");
                        //+
                        //+glGetIntegerv(GL_VBO_FREE_MEMORY_ATI, i);
                        //+printf("    VBO free memory - total: %u MB, largest block: %u MB\n",
                        //+i[0] / 1024, i[1] / 1024);
                        //+printf("    VBO free aux. memory - total: %u MB, largest block: %u MB\n",
                        //+i[2] / 1024, i[3] / 1024);
                        //+
                        //+glGetIntegerv(GL_TEXTURE_FREE_MEMORY_ATI, i);
                        //+printf("    Texture free memory - total: %u MB, largest block: %u MB\n",
                        //+i[0] / 1024, i[1] / 1024);
                        //+printf("    Texture free aux. memory - total: %u MB, largest block: %u MB\n",
                        //+i[2] / 1024, i[3] / 1024);
                        //+
                        //+glGetIntegerv(GL_RENDERBUFFER_FREE_MEMORY_ATI, i);
                        //+printf("    Renderbuffer free memory - total: %u MB, largest block: %u MB\n",
                        //+i[0] / 1024, i[1] / 1024);
                        //+printf("    Renderbuffer free aux. memory - total: %u MB, largest block: %u MB\n",
                        //+i[2] / 1024, i[3] / 1024);
                    else
                        Log.warn ("Could not get graphics memory information.")
                        GPUMemoryInfo()
                    )

module private Help =
    let inline getMemoryInfo (ctx : Context) = 
        ctx.GetMemoryInfo()

[<Extension; AbstractClass; Sealed>]
type ContextMemoryExtensions =
    [<Extension>]
    static member GetMemoryInfo(ctx : Context) : GPUMemoryInfo =
        Help.getMemoryInfo(ctx)
   
 