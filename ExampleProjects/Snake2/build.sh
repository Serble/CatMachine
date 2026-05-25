#!/bin/bash

MAIN_FILE=main.cat

cd $(dirname $0)

rm -rf data
mkdir -p data

echo "Building images"
for f in ./images/*.png; do
    # remove extension and folder path
    x="${f##*/}"
    x="${x%.*}"
    python ../tools/image_to_tiles.py -i $f -p palette0.palette -o ./data/$x
done

../tools/build.sh $MAIN_FILE
exit $?
