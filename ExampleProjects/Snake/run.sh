#!/bin/sh

# Navigate to the script's directory
cd "$(dirname "$0")"

./build.sh

# Keep in mind you still need to pass CLI args you want
# passed to the VM, for example to enable a display backend
# you might do:
# --renderer raylib

echo "Running..."
dotnet run --project ../../CatVM/CatVM.csproj ./bin/snake.bin --raylib-ppu $*
status=$?
echo "Application exited with status code $status"
