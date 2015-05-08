
msbuild ../src/Aardvark.Base.Rendering/Aardvark.Base.Rendering.fsproj /t:Build /p:Configuration="Release"
msbuild ../src/Aardvark.Rendering.GL/Aardvark.Rendering.GL.fsproj /t:Build /p:Configuration="Release"
msbuild ../src/Aardvark.SceneGraph/Aardvark.SceneGraph.fsproj /t:Build /p:Configuration="Release"
nuget pack Aardvark.Rendering.nuspec
