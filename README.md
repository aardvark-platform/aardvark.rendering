![Build](https://github.com/aardvark-platform/aardvark.rendering/workflows/Build/badge.svg)
![Publish](https://github.com/aardvark-platform/aardvark.rendering/workflows/Publish/badge.svg)

[![Discord](https://badgen.net/discord/online-members/UyecnhM)](https://discord.gg/UyecnhM)
[![license](https://img.shields.io/github/license/aardvark-platform/aardvark.base.svg)](https://github.com/aardvark-platform/aardvark.rendering/blob/master/LICENSE)

[The Aardvark Platform](https://aardvarkians.com/) |
[Gallery](https://github.com/aardvark-platform/aardvark.docs/wiki/Gallery) | 
[Packages&Repositories](https://github.com/aardvark-platform/aardvark.docs/wiki/Packages-and-Repositories)

The Aardvark.Rendering engine is a high-performance engine that tries to bridge the gap between efficiency and high-level easy-to-use abstractions. The engine is used in applied research and industry as well as basic research, and heavily embraces incremental computation. It tracks all changes in the scene description and automatically updates affected parts in the incrementally maintained optimization data structures. The engine currently has two backends: OpenGL and Vulkan, runs on netstandard, and is basically platform independent. It was born in 2006 and was mostly written in C#, but later moved towards functional programming. Now, most of the code is written in F#. Supported platforms are windows, linux, macOS. Render backends exist for OpenGL and Vulkan.

You can find demos and code in the Gallery and Packages&Repositories links above. Additionally, examples are available in the  `src/Examples*` folder in this repository. For more information, please refer to the [aardvark.docs wiki](https://github.com/aardvark-platform/aardvark.docs/wiki).

Aardvark.Rendering is part of the open-source [Aardvark Platform](https://github.com/aardvark-platform) for visual computing, real-time graphics, and visualization. Aardvark.Rendering is a stand-alone rendering engine that builds on basic data structures and tools from [Aardvark.Base](https://github.com/aardvark-platform/aardvark.base). It is also integrated into [Aardvark.Media](https://github.com/aardvark-platform/aardvark.media), which provides web-based user interfaces and ELM-style application development. [Aardvark.Algodat](https://github.com/aardvark-platform/aardvark.algodat) provides advanced geometry tooling and algorithms, including out-of-core point cloud rendering and PolyMesh processing algorithms.
