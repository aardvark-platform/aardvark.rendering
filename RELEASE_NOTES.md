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
 
