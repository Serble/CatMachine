#!/bin/bash

cd $(dirname $0)

echo "Building images"
python ../tools/image_to_tiles.py -i snake_body.png -p palette0.palette
python ../tools/image_to_tiles.py -i apple.png -p palette0.palette
python ../tools/image_to_buffer.py title_screen.png

../tools/build.sh snake.cat
exit $?
