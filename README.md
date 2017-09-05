[![Build status](https://ci.appveyor.com/api/projects/status/oqg1tw2ax1jl8qjx/branch/master?svg=true)](https://ci.appveyor.com/project/haraldsteinlechner/aardvark-rendering/branch/master)
[![Build Status](https://api.travis-ci.org/aardvark-platform/aardvark.rendering.svg?branch=master)](https://travis-ci.org/aardvark-platform/aardvark.rendering)

Aardvark.Base is part of the open-source [Aardvark platform](https://github.com/aardvark-platform/aardvark.docs) for visual computing, real-time graphics and visualization.

# Aardvark.Rendering

How to build:
------

Windows:
- Visual Studio 2015,
- Visual FSharp Tools 4.0 installed: visit http://fsharp.org/use/windows/ or download directly [[1]]
- run build.cmd which will install all dependencies
- msbuild src\Aardvark.Rendering.sln or use VisualStudio to build the solution

Linux:
- install mono >= 4.2.3.0 (might work in older versions as well)
- install fsharp 4.0 (http://fsharp.org/use/linux/)
- run build.sh which will install all dependencies
- run xbuild src/Aardvark.Rendering.sln

In order to use F# interactive with Aardvark.Rendering make sure you are using the 64bit version of fsi.exe (fsharpi.exe on linux). 
For windows, in order to use the visual studio integrated interactive shell, go to Tools/Options/F#  and 64bit interactive to true.
On Linux, make sure to use a 64bit mono and all is fine.

Tutorials can be found here:
https://github.com/vrvis/aardvark.rendering/tree/master/src/Demo/Examples
- [Getting Started, Aardvark.Base libraries](https://github.com/vrvis/aardvark/wiki)
- [Tutorial: Terrain Generator](https://aszabo314.github.io/stuff/terraingenerator.html)
- "Hello World": https://github.com/vrvis/aardvark.rendering/blob/master/src/Demo/Examples/HelloWorld.fs
- a version of Hello World based on Aardvark.Rendering.Interactive (lightweight abstraction for application setup in examples): https://github.com/vrvis/aardvark.rendering/blob/master/src/Demo/Examples/Tutorial.fs ... Note that this file can be executed in the F# interactive shell



[1]: https://www.microsoft.com/en-us/download/details.aspx?id=48179


