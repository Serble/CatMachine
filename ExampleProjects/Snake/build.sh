#!/bin/bash

MAIN_FILE="snake.cat"
OUTPUT_FILE="snake.bin"

cd $(dirname $0)

echo "Building images"
python ../tools/image_to_tiles.py -i snake_body.png -p palette0.palette
python ../tools/image_to_tiles.py -i apple.png -p palette0.palette
python ../tools/image_to_buffer.py title_screen.png

if [[ -z "${CAT_ASSEMBLER_COMMAND}" ]]; then
  CAT_ASSEMBLER_COMMAND=catasm
fi

mkdir -p bin

echo "Assembling..."
$CAT_ASSEMBLER_COMMAND "$MAIN_FILE" -o "bin/$OUTPUT_FILE"
status=$?
if [ $status -ne 0 ]; then
  echo "Assemble failed: exit $status"
  exit $status
fi
