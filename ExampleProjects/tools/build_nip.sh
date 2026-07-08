#!/bin/bash

# Constants
MAIN_FILE=$1
NAME=$(basename -- "$MAIN_FILE" .nip)

mkdir -p bin

echo "Assembling..."
dotnet run --project ../../Catnip.Compiler/Catnip.Compiler.csproj -- $MAIN_FILE -o "./bin/$NAME.bin" -s "./bin/$NAME.asm" -d "./bin/$NAME.asm.debug"
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi
