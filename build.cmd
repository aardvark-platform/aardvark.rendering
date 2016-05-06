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


SET FSI_PATH=packages\build\FAKE\tools\Fake.exe

IF exist boot.fsx ( 
    "%FSI_PATH%" "boot.fsx" 
    del "boot.fsx"
	.paket\paket.exe install
) ELSE (
	"%FSI_PATH%" "build.fsx" Dummy --fsiargs build.fsx %* 
)



