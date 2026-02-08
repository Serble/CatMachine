#!/bin/sh

cd "$(dirname "$0")"

# Constants
MAIN_FILE=main.nip

echo "Compiling..."
dotnet run --project ../../Catnip.Compiler/Catnip.Compiler.csproj $MAIN_FILE -o bin/a.out -d bin/a.out.debug -s bin/a.cat
status=$?
if [ $status -ne 0 ]; then
  echo "Compile failed: exit $status"
  exit $status
fi

echo "Running..."

# Requires raylib rendering
dotnet run -c Release --project ../../CatVM/CatVM.csproj bin/a.out --renderer raylib $*

status=$?
echo "Application exited with status code $status"
