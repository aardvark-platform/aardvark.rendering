cmake_minimum_required(VERSION 3.5)

project(vkvm LANGUAGES CXX)
if(WIN32)
    set(OS windows)
    SET(ARCH AMD64)
elseif(APPLE)
    set(OS mac)
    execute_process(COMMAND uname -m OUTPUT_VARIABLE ARCH)
    string(REGEX REPLACE "\n$" "" ARCH "${ARCH}")
    string(REGEX REPLACE "x86_64" "AMD64" ARCH "${ARCH}")
elseif(UNIX AND NOT APPLE)
    set(OS linux)
    SET(ARCH AMD64)
else()
    set(OS unknown)
    SET(ARCH AMD64)
endif(WIN32)


SET(CMAKE_INSTALL_PREFIX "${CMAKE_CURRENT_SOURCE_DIR}/../../lib/Native/Aardvark.Rendering.Vulkan/") 
SET(CMAKE_BUILD_TYPE "MinSizeRel" CACHE INTERNAL "release mode")

set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(vkvm SHARED commands.h vkvm.h vkvm.cpp commands.cpp)
find_package(Vulkan REQUIRED)
target_include_directories(${PROJECT_NAME} PUBLIC ${Vulkan_INCLUDE_DIRS})
target_link_libraries(${PROJECT_NAME} Vulkan::Vulkan)

install(TARGETS vkvm DESTINATION ${OS}/${ARCH})

