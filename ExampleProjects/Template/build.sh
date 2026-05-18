#!/bin/bash

MAIN_FILE=main.cat

cd $(dirname $0)

../tools/build.sh $MAIN_FILE
exit $?
