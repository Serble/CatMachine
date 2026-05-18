#!/bin/sh

cd "$(dirname "$0")"

./build.sh || exit $?
../tools/run.sh snake.bin -d RaylibPpu
