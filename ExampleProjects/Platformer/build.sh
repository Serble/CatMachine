#!/bin/bash

MAIN_FILE="main.cat"
OUTPUT_FILE="main.bin"

cd $(dirname $0)

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
