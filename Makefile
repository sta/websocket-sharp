ROOT:=$(shell pwd)
PROJECT=websocket-sharp
BUILDDIR=build

CONFIGURATION=Debug
VERSION=$(shell cat VERSION)
REVISION=$(shell git rev-parse --short HEAD)

build: clean app

clean:
	echo $(ROOT)
	rm -rf $(ROOT)/build
	rm -rf $(ROOT)/*/bin/
	rm -rf $(ROOT)/*/obj/

app: 
	msbuild $(ROOT)/$(PROJECT).sln /t:$(PROJECT) /p:Configuration="$(CONFIGURATION)" /p:Platform="Any CPU" /p:BuildProjectReferences=false
