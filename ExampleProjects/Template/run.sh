#!/bin/sh

MAIN_FILE=main.bin
ARGUMENTS=

cd "$(dirname "$0")"

./build.sh || exit $?
../tools/run.sh $MAIN_FILE $ARGUMENTS $*
