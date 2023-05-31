![Windows](https://github.com/aardvark-platform/aardvark.rendering/workflows/Windows/badge.svg)
![MacOS](https://github.com/aardvark-platform/aardvark.rendering/workflows/MacOS/badge.svg)
![Linux](https://github.com/aardvark-platform/aardvark.rendering/workflows/Linux/badge.svg)

[![Discord](https://badgen.net/discord/online-members/UyecnhM)](https://discord.gg/UyecnhM)
[![license](https://img.shields.io/github/license/aardvark-platform/aardvark.rendering.svg)](https://github.com/aardvark-platform/aardvark.rendering/blob/master/LICENSE)

[The Aardvark Platform](https://aardvarkians.com/) |
[Platform Wiki](https://github.com/aardvarkplatform/aardvark.docs/wiki) | 
[aardvark.rendering Examples](%2Fsrc%2FExamples%20(netcore)) |
[Technical Walkthrough and Examples](https://github.com/aardvark-platform/walkthrough) |
[Platform Examples](https://github.com/aardvark-platform/aardvark.docs/wiki/Examples) |
[Gallery](https://github.com/aardvarkplatform/aardvark.docs/wiki/Gallery) | 
[Quickstart](https://github.com/aardvarkplatform/aardvark.docs/wiki/Quickstart-Windows) | 
[Status](https://github.com/aardvarkplatform/aardvark.docs/wiki/Status)

Aardvark.Rendering is part of the open-source [Aardvark platform](https://github.com/aardvark-platform/aardvark.docs/wiki) for visual computing, real-time graphics and visualization. This repository heavily builds on datastructures and tools from [aardvark.base](https://github.com/aardvark-platform/aardvark.base). The engine can be used standalone or using [aardvark.media](https://github.com/aardvark-platform/aardvark.media) which provides purely functional ELM style application abstraction.

![Alt text](./data/context.svg)


The aardvark rendering engine was the vital spark which finally led to the complete ecosystem of libraries, we now call aardvark-platform. The engine as such was born in 2006. At that time, the engine was written mostly in C# (which was rather unusual in a world of highly optimized C++ engines). Later we more and more moved  towards functional programming. In various rewrites we modernized the engine over and over again. Now most code is written in F#. The unique features of the engine are:
 - The engine tries to bridge the gap between *efficiency* and *high-level easy to use abstractions*. We used a lot of energy to get out good performance for a lot cases. We did a lot but we look forward to getting better and better in this regard. [This video](https://www.youtube.com/watch?v=QjVRJworUOw) demonstrates the rapid prototyping features of aardvark.
 - It is used in applied research and industry but it is also used as vehicle for basic research.
 - The engine heavily embraces *incremental computation*. Rendering engines typically use some form of scene description which is then interpreted by the rendering kernel. The interpretation of large scenes quickly becomes a [bottleneck](https://www.cg.tuwien.ac.at/courses/RendEng/2015/RendEng-2015-11-16-paper2.pdf). Aardvark by contrast tracks all changes in the scene description and automatically updates affected parts in the *incrementally maintained optimization datstructures*. The approach was published in a paper [An Incremental Rendering VM](https://www.vrvis.at/publications/pdfs/PB-VRVis-2015-015.pdf). The scene graph concept and implementation is published in the paper [Attribute Grammars for Incremental Scene Graph Rendering](https://www.vrvis.at/publications/pdfs/PB-VRVis-2019-004.pdf).
 - The engine currently has two backends: OpenGL and Vulkan, runs on netstandard and is basically platform independent
 - Not like classic rendering engines, the aardvark rendering engine does not provide any tooling such as level editors etc. but lives from the aardvark platform as whole which provides tools to *create customized tooling for various needs*. 
 - Aardvark does not understand light, shadows or particular material workflows as in most game engines. Instead, the codebase provides a *rich set of tools* to customize those features to fit the needs.
 - For application and UI programming we recommend to climb the abstraction ladder up towards [aardvark.media](https://github.com/aardvark-platform/aardvark.media) which provides easy-to-use ELM style API to both UI and high-performance computer graphics.
 
We are constantly looking for cool contributions ideas etc! Meet us on [Discord](https://discord.gg/UyecnhM)

To list some, the most important [packages found on nuget](https://www.nuget.org/packages?q=aardvark.Rendering.*) are:
- Aardvark.Rendering	
- Aardvark.Rendering.GL	
- Aardvark.Rendering.Vulkan
- Aardvark.SceneGraph	
- Aardvark.Application.WPF.GL
- Aardvark.Application.WPF
- Aardvark.Application.WinForms.GL
- Aardvark.Application.WinForms.Vulkan
- Aardvark.Application.WinForms
- Aardvark.Application
- Aardvark.GPGPU
- Aardvark.Application.OpenVR
- Aardvark.Application.Slim
- Aardvark.Application.Slim.GL
- Aardvark.Application.Slim.Vulkan
- Aardvark.Rendering.Text		
- Aardvark.SceneGraph.IO	

