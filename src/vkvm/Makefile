
all: deploy 

deploy: vkvm.so
	cp vkvm.so ../../lib/Native/Aardvark.Rendering.Vulkan/linux/AMD64/vkvm.so
	cp vkvm.so ../../bin/Debug/vkvm.so
	cp vkvm.so ../../bin/Release/vkvm.so

vkvm.o: vkvm.cpp vkvm.h
	g++ -std=c++11 -fPIC -c vkvm.cpp -o vkvm.o

commands.o: commands.cpp commands.h
	g++ -std=c++11 -fPIC -c commands.cpp -o commands.o

vkvm.so: vkvm.o commands.o
	g++ vkvm.o commands.o -shared -o vkvm.so -lvulkan

.PHONY clean:
	rm -fr *.o vkvm.so
