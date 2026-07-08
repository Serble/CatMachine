#!/bin/bash

MAIN_FILE=main.nip

cd $(dirname $0)

../tools/build_nip.sh $MAIN_FILE
exit $?
