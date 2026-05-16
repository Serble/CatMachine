#!/bin/bash

# Constants
MAIN_FILE=$1
NAME=$(basename -- "$MAIN_FILE" .cat)

mkdir -p bin

echo "Assembling..."
dotnet run --project ../../CatAssembler/CatAssembler.csproj -- $MAIN_FILE -o "./bin/$NAME.bin"
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi
