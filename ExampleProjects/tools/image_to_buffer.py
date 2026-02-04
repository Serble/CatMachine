import sys
from PIL import Image

def main():
    in_path = None
    out_path = None
    
    i = 0 # we are starting at index 1
    while i < len(sys.argv) - 1:
        i += 1
        arg = sys.argv[i]

        if arg == "-o":
            i += 1
            out_path = sys.argv[i]
            continue
        elif arg == "-i":
            i += 1
            arg = sys.argv[i]
        
        if in_path is not None:
            print("You specified the input path twice")
            return 1
        
        in_path = arg
        if out_path is None:
            out_path = in_path[:in_path.rfind('.')] + ".bin"

    if in_path is None or out_path is None:
        print("You need to specify an input path at least")
        return 1
    
    image = Image.open(in_path)
    output = []
    
    for y in range(image.height):
        for x in range(image.width):
            color = image.getpixel((x, y))
            output.append(color[2]) # blue
            output.append(color[1]) # green
            output.append(color[0]) # red
            output.append(color[3] if len(color) > 3 else 255) # alpha

    with open(out_path, 'wb') as fp:
        fp.write(bytes(output))
    
    return 0

if __name__ == "__main__":
    exit(main())
