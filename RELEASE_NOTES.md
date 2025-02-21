### 5.5.16
- now using `glEnablei/glDisablei` for BlendModes.

### 5.5.15
- updated package FSharp.Data.Adaptive 1.2.19
- Improved error reporting for buffer creation and updating
- [Vulkan] Respect export flag for empty buffers
- [Vulkan] Print detailed memory information when allocation fails (uses VK_EXT_memory_budget if available)
- [Vulkan] Avoid passing VkExportMemoryAllocateInfo when not exporting memory
- [Vulkan] Added Device.PrintMemoryUsage()

### 5.5.14
- updated dependency FSharp.Data.Adaptive 1.2.19
- [Vulkan] Changed config location of device chooser to Aardvark cache directory
- Added DownloadDepth() and DownloadStencil() overloads for IBackendTexture with an explicit target parameter
- Fixed simpleLighting and stableLight shaders to use ambient term
- Fixed race conditions with compact buffers and Vulkan image sampler arrays
- Improved error reporting for null values as textures

### 5.5.13
- [OpenGL/WPF/ThreadedRenderControl] re-activate classic render control

### 5.5.12
- [OpenGL/WPF/ThreadedRenderControl] frame throttle

### 5.5.11
- [LodTreeNode] uniforms concurrent access fix

### 5.5.10
- [OpenGL/WPF/ThreadedRenderControl] fixed resize

### 5.5.9
- [OpenGL/WPF] Threaded rendering control

### 5.5.8
- MultiTreeNode: fixed picking

### 5.5.7
- LodTreeNode: added MultiTreeNode support

### 5.5.6
- [Assimp] Animations: fixed quaternion interpretation

### 5.5.5
- OpenGL/WPF control uses tasks for rendering (avoiding stack-inlining due to STAThread)

### 5.5.4
- OpenGL/WPF control uses `OnPainRender` again

### 5.5.3
- OpenGL/WPF control no longer uses `OnPainRender` by default

### 5.5.2
- added PoolGeometry as alternative to ManagedPool with SymbolDict (more efficient attribute lookups)
- added reference equality check to BufferView Equals
- using SortedSetExt value option methods
- avoid exception in upload/download/write when count=0
- marked MemoryManagementUtilities.FreeList as obsolete (duplicate of Aardvark.Base FreeList)
- updated Aardvark.Base to 5.3.5
- updated Aardvark.Build to 2.0.2
- updated aardpack to 2.0.3

### 5.5.1
- Improved adaptive converter caching
- Optimized GCHandle.Alloc usage
- [Assimp] Fixed remap.xml for Linux
- [GL] Optimized construction of attribute bindings
- [Text] Use FramebufferLayout as surface cache key
- [Text] Added type aliases

### 5.5.0
- https://github.com/aardvark-platform/aardvark.rendering/wiki/Aardvark-Rendering-5.5-changelog

### 5.5.0-prerelease0002
- Renamed `PixImageCube` to `PixCube`
- Renamed `Aardvark.SceneGraph.IO` to `Aardvark.SceneGraph.Assimp`

### 5.5.0-prerelease0001
- Initial prerelease

### 5.4.12
- [GL] Fixed potential memory leak after ContextHandle is disposed
- Optimized generic dispatch
- Fixed potential leaks with ConcurrentDictionary.GetOrAdd

### 5.4.11
- [Application.WPF.GL] SharingRenderControl implementation now uses Silk.NET.Direct3D9 instead of SharpDX
- Removed SharpDX dependency
- Re-added dynamic shader caches
- Fixed multi-threading issue in PrimitiveValueConverter
- [Sg] Use single value attributes for IndexedGeometry
- [IndexedGeometry] Fixed Union() and added ToIndexed() overload
- [IndexedGeometry] Added overload Clone() for deep copy

### 5.4.10
- [OpenVR] changed GL texture submit to 2 textures (previously side by side, issue with Quest 3)
- [GL] Improved querying of supported sample counts
- [GL] Fixed double disposal of Context
- [GLFW] Fixed OpenTK context interop
- [Vulkan] Fixed conservative raster validation error

### 5.4.10-prerelease0006
- rebuild glvm for ARM64

### 5.4.10-prerelease0005
- [Text] added option to disable sample shading

### 5.4.10-prerelease0004
- [GL] added flag to disable multidraw (experimental)

### 5.4.10-prerelease0003
- [FontResolve] fixed null family name failure

### 5.4.10-prerelease0002
- [PathSegment] minor fixes

### 5.4.10-prerelease0001
- [Text] improved Font resolver for Windows and MacOS
- [PathSegment] fixed several PathSegment tools and added a few new ones

### 5.4.9
- [LodRenderer] Handle exceptions in background threads
- [GL] Implemented GLSL shader caches for platforms that do not support program binaries (e.g. MacOS)

### 5.4.9-prerelease0001
- [GL] experimental support for quad-buffer stereo(is back again?)

### 5.4.8
- [GL] Fixed locking order of GlobalResourceLock and context locks to avoid potential deadlocks
- [GL] Added workaround for layered rendering and GLSL < 430
- [GL] Made context creation and sharing more robust (see RuntimeConfig.RobustContextSharing)
- [GL] Improved disposal of ContextHandle
- [GLVM / VKVM] Updated ARM64 binaries
- [GLFW] Fixed context resource leaks
- [GLFW] Reset GetCurrentContext on disposal
- [WinForms / WPF] Removed double dispose of context

### 5.4.7
- Fixed Frustum.withAspect and Frustum.withHorizontalFieldOfViewInDegrees
- [GL] Fixed InvalidEnum error due to GL_POINT_SPRITE
- [GL] Removed validation via proxy textures (resulted in errors on AMD with multisampled textures)
- [GL] Removed swizzle for multisampled textures (not supported)
- [GL] Added simple parameter device limit checks for textures and renderbuffers
- [GL] Improved texture memory usage tracking
- [GL] Made retrieval of program binaries more robust
- [GL] Improved driver information and error formatting
- [GL] Disabled Dispose() for Program
- [GL] Fixed resource leaks in ContextHandleOpenTK.create
- [GL] Fixed ComputeCommand.SetBufferCmd
- [GL] Fixed issue with texture targets and multisampling
- [Vulkan] Fixed swapchain creation if maxImages is zero
- [Vulkan] Fixed issue with image format queries and external memory
- [Vulkan] Improved error formatting
- [GLFW] Use no error context only when indicated by debug config
- Added IRenderTask.GetRuntime() and IRenderTask.GetFramebufferSignature()

### 5.4.6
- [ContextHandles] GL.Enable(EnableCap.PointSprite)
- [ManagedPool] Avoid evaluating draw call set if not active
- Fix BlendMode.Blend source alpha factor

### 5.4.5
- [GeometryPool] Fixed wrongly disposed shader caches

### 5.4.4
- Exceptions are caught and logged when updating shaders with the debugger
- [GL] Fixed resource management issue with compute shaders and shader debugger, resulting in invalid operation errors
- [GL] Fixed issue with preparing exported buffers
- [GL] Print before debugger break in DebugCommandStream
- [Vulkan] Fixed validation error related to memory export

### 5.4.3
- Updated to FShade 5.5
- Added support for debugging raytracing effects and compute shaders with the FShade ShaderDebugger
- Fixed issues with dirty sets in OrderedCommand (GL / Vulkan)
- [GL] Increased verbosity level of outdated resource warning
- [GL] Improved warning about missing internal format query support 

### 5.4.2
- [Vulkan] Fixed issue in SBT update
- [Sg] Added C# Surface overload
- Improved shade compile error reporting and code printing

### 5.4.1
- Fixed net6.0 target for WinForms and WPF

### 5.4.0
- https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-Rendering-5.4-changelog

### 5.4.0-prerelease0004
- Renamed NewInputBinding to CreateInputBinding
- Reverted renaming of provider ofDict methods
- Restored IAttributeProvider.All
- Added Signature property to ManagedPool and ManagedTracePool
- Added obsolete extensions for renamed buffer copy methods
- [GL] Removed duplicate context tracking
- [Vulkan] Fixed aspect for depth / stencil samplers
- [Vulkan] Fixed shader stage computation for dynamic effects

### 5.4.0-prerelease0003
- Restored IComputeRuntime.ContextLock

### 5.4.0-prerelease0002
- Added validation for sampler state translation
- Added texture filter reduction
- Added Blit, reworked Copy and ResolveMultisamples
- [GL] Added RuntimeConfig.AllowConcurrentResourceAccess
- [Vulkan] Fixed issue with concurrent eager destroy

### 5.4.0-prerelease0001
- Initial prerelease for 5.4

### 5.3.8
- [GL] Fixed update issues with OrderedCommand

### 5.3.7
- Various optimizations
- Added RenderTask.renderTo variants with adaptive clear values
- Fixed RenderPass.before to respect given order
- [AbstractRenderTask] Make Dispose mutually exclusive with Update and Run
- [GL] Fixed ObjectDisposedException related to invalid epilogue prev pointer
- [GLFW] Fixed vsync initialization
- [GLFW] Disabled unknown joystick axis warning
- [Vulkan] Fixed issue with pipeline statistics being wrongfully selected

### 5.3.6
- Opc: more robust patchhierarchy caching: https://github.com/pro3d-space/PRo3D/issues/283

### 5.3.5
- OpcPaths now more robust (images vs Images, patches vs Patches) - https://github.com/pro3d-space/PRo3D/issues/280
- Added support for loading mipmaps from file and stream textures
- Added RenderTask.renderToWithAdaptiveClear
- [Vulkan] Fixed synchronization issue with image and buffer uploads
- [Vulkan] Implemented direct texture and framebuffer clear
- [GL] Fixed issue with directly clearing depth-only textures

### 5.3.4
- Added default component swizzle that duplicates the red channel to the green and blue channels for grayscale formats
- [Application.Slim.GL] Fixed context initialization 
- [Vulkan] Fixed deadlock in concurrent descriptor set management

### 5.3.3
- Added union operation for IndexedGeometry
- Added IndexedGeometry primitives for arrows and coordinate crosses
- [Sg] Fixed and improved active flag cache
- [Vulkan] Fixed issue with logging shader cache reads

### 5.3.2
- [GL] Fixed access violation and other issues related to internal format queries
- [GL] Implemented shader caches for compute shaders
- [Vulkan] Implemented shader caches for raytracing shaders
- Improved error logging when shader cache access fails
- Reworked shader caches to use FShade-based interface serialization instead of FsPickler
- Made shader cache directory creation lazy

### 5.3.1
- Fixed export (sharing) bug on MacOS (pNext chain needs to be empty)
- Added hardware support validation for mipmap generation
- [GL] Fixed mipmap generation for compressed file and stream textures
- [GL] Fixed issue with compressed texture download
- [Vulkan] Implemented mipmap compressed PixTexture2d upload
- [Vulkan] Fixed prepare of Stream- / FileTexture with compression

### 5.3.0
- https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-Rendering-5.3-changelog

### 5.3.0-prerelease0005
- Implemented debug configurations for enabling backend-specific debug features
- Added validation for color attachment formats when creating framebuffer signatures
- Added proper support for unsigned integer color attachments

### 5.3.0-prerelease0004
- [Vulkan] Implemented RaytracingTask.Update
- [Vulkan] Implemented CommandTask.PerformUpdate
- [Vulkan] Reworked resource manager to prevent disposal of resources in use
- [Vulkan] Use separate device tokens for graphics and compute families
- [Vulkan] Lock pending set during update loop of ResourceLocationSet
- [Vulkan] Added basic support for validation features
- [Raytracing] Fixed issue with acceleration structure building and device tokens
- [GL] Fixed memory usage tracking for imported resources
- Added IRenderTask.Update overloads
- Updated to FShade 5.3 prerelease

### 5.3.0-prerelease0003
- [Vulkan] Fixed issue with duplicate descriptor writes
- [Vulkan] Trim excess elements from image sampler array
- [Vulkan] Fixed extensions being wrongly reported as unavailable
- [Vulkan] Lock pending set during update loop of DescriptorSetResource

### 5.3.0-prerelease0002
- [Vulkan] Fixed issue in DescriptorSetResource related to nested dependencies
- [Vulkan] Reworked RaytracingTask to prevent unnecessary recompilation
- [Vulkan] Implemented update-after-bind descriptors to prevent recompilation of render tasks
- [Vulkan] Added RuntimeConfig.SuppressUpdateAfterBind
- [Vulkan] Fixed issues with dynamic image sampler arrays
- Implemented shrinking of AdaptiveCompactBuffer
- Fixed issue with addition and removal order in AdaptiveCompactBuffer
- Added IRaytracingTask.Update() overloads
- Changed parameter order of Sg.pool

### 5.3.0-prerelease0001
- Initial prerelease for 5.3

### 5.2.17
- Improved error handling of DDS parser
- [Sg] Added delay
- [Sg] Added reference counting for onActivation
- [Application.Slim] Fixed error code printing

### 5.2.16
- [Application.Slim.GL] Fix issue with sample count for non-multisampled windows

### 5.2.15
- Font constructor with System.IO.Stream

### 5.2.14
- Framebuffer Copy/ReadPixels 

### 5.2.13
- switched to aardvark.assembler

### 5.2.12
- reverted Vulkan queue creation
- enabled sharing extensions by default (windows/linux)

### 5.2.11
- disabled useNoError (linux intel, steamdeck compat)

### 5.2.10
- text rendering workaround linux(nvidia)

### 5.2.9
- Arch/Fedora working
- moved to Aardvark.Assembler

### 5.2.9-prerelease0002
- added missing FragmentProgram.Update

### 5.2.9-prerelease0001
- test release
- moved to Aardvark.Assembler

### 5.2.8
- fixed GLFW init problem

### 5.2.7
- [Vulkan] requesting all queues for device

### 5.2.6
- [Vulkan] updated vk.xml to latest version (1.3)
- [GL] improved error handling when retrieving uniforms
- [Sg] Fixed runtime-dependent texture caching
- [GL] Remove render task commands from dirty set

### 5.2.5
- [Text] Winding order of triangles is consistent, degenerated triangles get removed
- [Vulkan] Added image limits checks for layers, levels and size
- [GL] Added texture size limit checks
- [GLFW] Fixed issues with MacOS and other platforms with poor GL support

### 5.2.4
- [Vulkan] Implemented host-side texture compression
- Added RenderTo overloads with adaptive clear values
- Added checks for maximum multisamples when creating framebuffer signatures and textures
- Implemented PickObjects for RenderCommand
- [GLFW] Fixed issue with non-positive window size
- [GL] Fixed streaming texture issues
- [GL] Use RGB internal format for BGR texture formats
- [GLFW] Add hideCocoaMenuBar parameter

### 5.2.3
- [ManagedPool] Fixed memory leak
- Improved block compression decoding and copying
- [GL] Implemented host-side texture compression

### 5.2.2
- [GL] relaxed framebuffer signature compability requirements
- [Vulkan] using any compatible QueueFamily for CopyEngine

### 5.2.1
- implemented ARM64 assembler
- fixed issue with Retina displays
- [Vulkan] fixed issue with platforms not supporting queries
- [Vulkan] fixed issue with platforms not supporting geometry or tessellation shaders

### 5.2.0
https://github.com/aardvark-platform/aardvark.docs/wiki/Aardvark-5.2-changelog

### 5.2.0-prerelease0002
- improved C# interop for ClearValues
- added setter for Call of ManagedDrawCall
- made texture clear API consistent with framebuffer clearing

### 5.2.0-prerelease0001
- Initial prerelease for 5.2

### 5.1.22
- [ManagedPool] no removal of empty PoolNodes from set of RenderObjects: dependency on IndirectBuffer input was causing performance issues when marking/re-evaluating complete set of RenderObjects

### 5.1.21
- fixed ComputeShader problem in Vulkan

### 5.1.20
- deterministic Id for instanced-effects

### 5.1.19
- updated Base & FShade

### 5.1.18

- disabled multisampling for text outline - fix for https://github.com/aardvark-platform/aardvark.rendering/issues/86

### 5.1.17
- fixed package dependeny to FSharp.Data.Adaptive
- [Vulkan] fixed package dependency to GLSLangSharp
- [GL] implemented UploadBufferCmd and CopyImageCmd

### 5.1.16
- [GL] fixed IRuntime.Clear/ClearColor
- [Sg] added C# ColorOutput overload

### 5.1.15
- fixed GL compute shader image bindings

### 5.1.14
- switched to official AssimpNet

### 5.1.13
- updated mac glfw lib & option to control cocoa menu bar

### 5.1.12
- fixed texture download in absense of bufferstorage

### 5.1.11
- fixed osx64 glvm build

### 5.1.10
- fix for segfaults on GL without direct state access

### 5.1.9
- updated FSharp.Core >= 4.7.0
- updated to newest Base/FShade/Adaptive packages
- removed System.Reactive

### 5.1.8
- added argument validation for texture copying
- added argument validation for texture download and upload
- [GL] changed Shader caches to depend on context / runtime
- [GL] fixed copy of cubemaps
- [GL] fixed RenderingLock

### 5.1.7
- added types to specify clear values more easily
- added IRenderTask.RenderTo() overloads with clear values
- added RenderTask.render* variants with clear values
- added map and bind functions for IAdaptiveResource
- DepthTest.None and DepthTest.Always are no longer aliases (revert to < 5.1.0 behavior)
- fixed various bugs related to cube texture arrays
- texture creation functions now validate parameters
- [GL] fixed nop RenderingLock 
- [GL] render control size is ensured to be valid now
- [GL] fixed bug with draw buffers and prepared surfaces with signatures different from the render task
### 5.1.6
- reworked low-level texture API
- added functions for creating (adaptive) 1D and 3D textures
- removed IBackendTextureOutputView and BackendTextureOutputView
- fixed management and disposal of renderTo tasks
- proper out-of-date marking for adaptive resources
- reworked TextureFilter and SamplerState regarding anisotropic filtering
- [GL] fixed bug in Context.Blit()
- [GL] fixed copy and download of texture array slices
- [SgFSharp] removed unnecessary SRTP usage
- [SgFSharp] added Sg.lines'
- [Vulkan] implemented dynamic sampler states

### 5.1.5
- fixed Silk.NET.Core depenedency
- fixed renderToColorCube 

### 5.1.4
- updated packages

### 5.1.3
- [Vulkan] CommandTask no longer disposes ResourceManager
- [Sg] Added instancing utilities for IndexedGeometry 

### 5.1.2
- fixed thread abort exn on linux

### 5.1.1
- https://hackmd.io/58CqcVmnRoGq-X5gIrNThg

### 5.0.17
 - [GL] OpenVR support for GL
 
### 5.0.15
 - [GL] replaced EXT_direct_state_access with ARB_direct_state_access
 - [GL] fixed crashes when using core profile
 - [GL] RenderTask.Dispose no longer needs a transaction (https://github.com/aardvark-platform/aardvark.rendering/issues/60)

### 5.0.14
 - [GL] More robust parsing of GL and GLSL versions

### 5.0.3
 - [Text] fixed compatibility with render passes

### 5.0.0
 - [Base] updated aardvark to v5
 - [Base] reworked Buffer API: added BufferUsage flags
 - [Base] added indirect draw stride
 - [Base] refactored RenderObject drawcalls
 - [Base] removed IResizeBuffer, IMappedBuffer, IMappedIndirectBuffer
 - [Sg] reworked ManagedPool

### 4.12.4
 - [GL] fixed mip level calculation in texture upload

### 4.12.3
 - [Base] updated base packages

### 4.12.2
 - [Base] fixed GLVM loading for all plattforms

### 4.12.1
 - [Base] updated GLVM for linux

### 4.12.0
 - [GL] removed warnings from LodRenderer
 - [GL] added support for NormalUV texture (2-channel float images)

### 4.11.15
 - [GL] fixed BufferRuntime.Clear

### 4.11.12
 - [GL] added quad-buffered stereo support to GameWindow

### 4.11.8
 - [Base] fixed RenderTask.custom
 - [GL] fixed size 0 UniformBuffer alloc

### 4.11.7
 - [Base] updated packages / fixed memory leak

### 4.11.5
 - [Base] reverted memory leak fix

### 4.11.4
 - [Base] rmeoved hooking mechanism of dynamic uniforms (no need anymore, allowed overwrite of view/proj trafo)
 - [Base] moved Caches (UnaryCache, BinaryCache) to Base.FSharp
 - [Sg] fixed memory leak when using derived attirbutes (e.g. ModelViewTrafo, ModelViewProjTrafo)
 - [GL] fixed texture array uniforms 

### 4.11.3
 - LoD Render: removed debug ouput

### 4.11.2
 - [GL] fixed buffer resource stats
 - [GL] fixed unmanaged memory leak of VAO
 
