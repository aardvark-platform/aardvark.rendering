
all: deploy 

deploy: libvkvm.so
	cp libvkvm.so ../../lib/Native/Aardvark.Rendering.Vulkan/linux/AMD64/libvkvm.so
	cp libvkvm.so ../../bin/Debug/libvkvm.so
	cp libvkvm.so ../../bin/Release/libvkvm.so

vkvm.o: vkvm.cpp vkvm.h
	g++ -std=c++11 -fPIC -c vkvm.cpp -o vkvm.o

commands.o: commands.cpp commands.h
	g++ -std=c++11 -fPIC -c commands.cpp -o commands.o

libvkvm.so: vkvm.o commands.o
	g++ vkvm.o commands.o -shared -o libvkvm.so -lvulkan

.PHONY clean:
	rm -fr *.o libvkvm.so
