
all: deploy 

deploy: glvm.so
	cp glvm.so ../../lib/Native/Aardvark.Rendering.GL/linux/AMD64/glvm.so
	cp glvm.so ../../bin/Debug/glvm.so
	cp glvm.so ../../bin/Release/glvm.so

glvm.o: glvm.cpp glvm.h
	g++ -std=c++11 -fPIC -c glvm.cpp -o glvm.o

State.o: State.cpp State.h
	g++ -std=c++11 -fPIC -c State.cpp -o State.o

glvm.so: glvm.o State.o
	g++ State.o glvm.o -shared -o glvm.so -lGL

glvm.dylib: glvm.o State.o
	g++ State.o glvm.o -shared -o glvm.dylib -framework OpenGL

.PHONY clean:
	rm -fr *.o glvm.so
