#!/bin/sh

# Keep in mind you still need to pass CLI args you want
# passed to the VM, for example to enable a display backend
# you might do:
# --renderer raylib

# Constants
MAIN_FILE=main.asm  # Change this to be the name of your main asm file.

echo "Assembling..."
dotnet run --project ../../CatAssembler/CatAssembler.csproj $MAIN_FILE

echo "Running..."
dotnet run --project ../../CatVM/CatVM.csproj a.out $*
status=$?
echo "Application exited with status code $status"
