#!/bin/sh

# Constants
echo "Assembling..."
dotnet run --project ../../CatAssembler/CatAssembler.csproj disktest.cat -o a.out
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi

echo "Running..."

# Requires raylib rendering
dotnet run -c Release --project ../../CatVM/CatVM.csproj a.out --test-ints --disk "disk.catdisk" 1000 $*

status=$?
echo "Application exited with status code $status"
