
all: deploy 

deploy: libglvm.so
	cp libglvm.so ../../lib/Native/Aardvark.Rendering.GL/linux/AMD64/libglvm.so
	cp libglvm.so ../../bin/Debug/libglvm.so
	cp libglvm.so ../../bin/Release/libglvm.so

glvm.o: glvm.cpp glvm.h
	g++ -std=c++11 -fPIC -c glvm.cpp -o glvm.o

State.o: State.cpp State.h
	g++ -std=c++11 -fPIC -c State.cpp -o State.o

libglvm.so: glvm.o State.o
	g++ State.o glvm.o -shared -o libglvm.so -lGL

glvm.dylib: glvm.o State.o
	g++ State.o glvm.o -shared -o glvm.dylib -framework OpenGL

.PHONY clean:
	rm -fr *.o glvm.so
