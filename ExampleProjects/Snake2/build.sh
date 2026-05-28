#!/bin/bash

MAIN_FILE=main.cat

cd $(dirname $0)

rm -rf data
mkdir -p data

echo "Building images"
for f in ./images/snake_*.png ./images/apple.png; do
    # remove extension and folder path
    x="${f##*/}"
    x="${x%.*}"
    python ../tools/image_to_tiles.py -i $f -p palette0.palette -o ./data/$x
done

python ../tools/image_to_buffer.py -i ./images/title_screen.png -o ./data/title_screen

../tools/build.sh $MAIN_FILE
exit $?
