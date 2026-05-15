#!/bin/bash

# Constants
cd $(dirname $0)
mkdir -p bin

echo "Assembling..."
dotnet run --project ../../CatAssembler/CatAssembler.csproj device_test.cat -o bin/device_test.bin
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi
