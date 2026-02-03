#!/bin/bash

# Constants
MAIN_FILE=snake.cat

cd $(dirname $0)
mkdir -p bin

echo "Building images"
python ../tools/image_to_tiles.py -i snake_body.png -p palette0.palette
python ../tools/image_to_tiles.py -i apple.png -p palette0.palette

echo "Assembling..."
dotnet run --project ../../CatAssembler/CatAssembler.csproj $MAIN_FILE -o bin/snake.bin
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi
