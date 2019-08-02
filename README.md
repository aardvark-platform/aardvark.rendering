[![Build status](https://ci.appveyor.com/api/projects/status/oqg1tw2ax1jl8qjx/branch/master?svg=true)](https://ci.appveyor.com/project/haraldsteinlechner/aardvark-rendering/branch/master)
[![Build Status](https://api.travis-ci.org/aardvark-platform/aardvark.rendering.svg?branch=master)](https://travis-ci.org/aardvark-platform/aardvark.rendering)
[![Join the chat at https://gitter.im/aardvark-platform/Lobby](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/aardvark-platform/Lobby)
[![license](https://img.shields.io/github/license/aardvark-platform/aardvark.rendering.svg)](https://github.com/aardvark-platform/aardvark.rendering/blob/master/LICENSE)

[The Aardvark Platform](https://aardvarkians.com/) |
[Platform Wiki](https://github.com/aardvarkplatform/aardvark.docs/wiki) | 
[aardvark.rendering Examples](%2Fsrc%2FExamples%20(netcore)) |
[Technical Walkthrough and Examples](https://github.com/aardvark-platform/walkthrough) |
[Platform Examples](https://github.com/aardvark-platform/aardvark.docs/wiki/Examples) |
[Gallery](https://github.com/aardvarkplatform/aardvark.docs/wiki/Gallery) | 
[Quickstart](https://github.com/aardvarkplatform/aardvark.docs/wiki/Quickstart-Windows) | 
[Status](https://github.com/aardvarkplatform/aardvark.docs/wiki/Status)

Aardvark.Rendering is part of the open-source [Aardvark platform](https://github.com/aardvark-platform/aardvark.docs/wiki) for visual computing, real-time graphics and visualization.

[Wiki](https://github.com/aardvarkplatform/aardvark.docs/wiki) is the landing page for documentation and further information.


The aardvark rendering engine was the vital spark which finally led to the complete ecosystem of libraries, we now call aardvark-platform. The engine as such was born in 2006. At that time, the engine was written mostly in C# (which was rather unusual in a world of highly optimized C++ engines). Later we more and more moved  towards functional programming. In various rewrites we modernized the engine over and over again. Now most code is written in F#. The unique features of the engine are:
 - The engine tries to bridge the gap between efficiency and high-level easy to use abstractions. We used a lot of energy to get out good performance for a lot cases. We did a lot but we look forward to getting better and better in this regard.
 - It is used in applied research and industry but it is also used as vehicle for basic research.
 - The engine currently has two backends: OpenGL and Vulkan, runs on netstandard and is basically platform independent
 - Not like classic rendering engines, the aardvark rendering engine does not provide any tooling such as level editors etc. but lives from the aardvark platform as whole which provides tools to create customized tooling for various needs. 
 - Aardvark does not understand light, shadows or particular material workflows as in most game engines. Instead, the codebase provides a rich set of tools to customize those features to fit the needs.
 - For application and UI programming we recommend to climb the abstraction ladder up towards [aardvark.media](https://github.com/aardvark-platform/aardvark.media) which provides easy-to-use ELM style API to both UI and high-performance computer graphics.
 
We are constantly looking for cool contributions ideas etc! Meet us on [gitter](https://gitter.im/aardvark-platform/Lobby)

To list some, the most important [packages found on nuget](https://www.nuget.org/packages?q=aardvark.Rendering.*) are:
- Aardvark.Base.Rendering	
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

