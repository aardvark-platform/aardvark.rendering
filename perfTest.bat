@echo off
pushd bin\Release
Aardvark.Rendering.GL.Tests.exe > tmp
set /p time= < tmp
echo %*;%time% >> C:\Aardwork\perf.csv
popd