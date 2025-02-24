cmake_minimum_required(VERSION 3.15)

project(glvm LANGUAGES CXX)
if(WIN32)
    set(OS windows)
    SET(ARCH AMD64)
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
elseif(APPLE)
    set(OS mac)
    execute_process(COMMAND uname -m OUTPUT_VARIABLE ARCH)
    string(REGEX REPLACE "\n$" "" ARCH "${ARCH}")
    string(REGEX REPLACE "x86_64" "AMD64" ARCH "${ARCH}")
elseif(UNIX AND NOT APPLE)
    set(OS linux)
    execute_process(COMMAND uname -m OUTPUT_VARIABLE ARCH)
    string(REGEX REPLACE "\n$" "" ARCH "${ARCH}")
    string(REGEX REPLACE "x86_64" "AMD64" ARCH "${ARCH}")
else()
    set(OS unknown)
    SET(ARCH unknown)
endif(WIN32)


SET(CMAKE_INSTALL_PREFIX "${CMAKE_CURRENT_SOURCE_DIR}/../../lib/Native/Aardvark.Rendering.GL/") 
SET(CMAKE_BUILD_TYPE "MinSizeRel" CACHE INTERNAL "release mode")

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(glvm SHARED State.h glvm.h glvm.cpp State.cpp)

find_package(OpenGL REQUIRED)
include_directories( ${OPENGL_INCLUDE_DIRS} )
target_link_libraries(glvm OpenGL::GL )

install(TARGETS glvm DESTINATION ${OS}/${ARCH})

