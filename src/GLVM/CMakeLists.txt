cmake_minimum_required(VERSION 3.5)

project(glvm LANGUAGES CXX)
if(WIN32)
    set(OS windows)
    SET(ARCH AMD64)
    find_package(OpenGL REQUIRED)
    set(LIBS opengl32)
elseif(APPLE)
    set(OS mac)
    execute_process(COMMAND uname -m OUTPUT_VARIABLE ARCH)
    string(REGEX REPLACE "\n$" "" ARCH "${ARCH}")
    string(REGEX REPLACE "x86_64" "AMD64" ARCH "${ARCH}")
    FIND_LIBRARY(OpenGL_LIBRARY OpenGL)
    set(LIBS ${LIBS} ${OpenGL_LIBRARY})
elseif(UNIX AND NOT APPLE)
    set(OS linux)
    SET(ARCH AMD64)
else()
    set(OS unknown)
    SET(ARCH AMD64)
endif(WIN32)


SET(CMAKE_INSTALL_PREFIX "${CMAKE_CURRENT_SOURCE_DIR}/../../lib/Native/Aardvark.Rendering.GL/") 
SET(CMAKE_BUILD_TYPE "MinSizeRel" CACHE INTERNAL "release mode")

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(glvm SHARED State.h glvm.h glvm.cpp State.cpp)
target_link_libraries(glvm ${LIBS})

install(TARGETS glvm DESTINATION ${OS}/${ARCH})

