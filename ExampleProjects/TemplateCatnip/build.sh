#!/bin/bash

MAIN_FILE="main.nip"
OUTPUT_NAME="main"

cd $(dirname $0)

if [[ -z "${CAT_NIP_COMPILER_COMMAND}" ]]; then
  CAT_NIP_COMPILER_COMMAND=nipcompile
fi

mkdir -p bin

echo "Compiling..."
$CAT_NIP_COMPILER_COMMAND $MAIN_FILE -o "./bin/$OUTPUT_NAME.bin" -s "./bin/$OUTPUT_NAME.asm" -d "./bin/$OUTPUT_NAME.asm.debug"
status=$?
if [ $status -ne 0 ]; then
  echo "Compile failed: exit $status"
  exit $status
fi
