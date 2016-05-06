@echo off
SETLOCAL
PUSHD %~dp0


.paket\paket.bootstrapper.exe
if errorlevel 1 (
  exit /b %errorlevel%
)

.paket\paket.exe restore group Build
if errorlevel 1 (
  exit /b %errorlevel%
)

cls

.paket\paket.exe restore

packages\FSharp.Formatting.CommandTool\tools\fsformatting.exe literate --processDirectory --inputDirectory src --outputDirectory output



