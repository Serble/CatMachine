#!/bin/sh

cd "$(dirname "$0")"

# Constants
MAIN_FILE=test.nip

echo "Compiling..."
dotnet run --project ../../Catnip.Compiler/Catnip.Compiler.csproj $MAIN_FILE -o a.out
status=$?
if [ $status -ne 0 ]; then
  echo "Compile failed: exit $status"
  exit $status
fi

echo "Running..."

# Requires raylib rendering
dotnet run -c Release --project ../../CatVM/CatVM.csproj a.out --renderer raylib $*

status=$?
echo "Application exited with status code $status"
