#!/bin/sh

# Constants
MAIN_FILE=main.asm

echo "Assembling..."
dotnet run --project ../../CatAssembler/CatAssembler.csproj $MAIN_FILE
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi

echo "Running..."

# Requires raylib rendering
dotnet run --project ../../CatVM/CatVM.csproj a.out --renderer raylib $*

status=$?
echo "Application exited with status code $status"
