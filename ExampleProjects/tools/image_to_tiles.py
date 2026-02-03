import sys
from PIL import Image

def main():
    in_path = None
    palette_path = None
    out_path = None
    
    i = 0 # we are starting at index 1
    while i < len(sys.argv) - 1:
        i += 1
        arg = sys.argv[i]

        if arg == "-o":
            i += 1
            out_path = sys.argv[i]
            continue
        elif arg == "-p":
            i += 1
            palette_path = sys.argv[i]
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
        if palette_path is None:
            palette_path = in_path[:in_path.rfind('.')] + ".palette"

    if in_path is None or palette_path is None or out_path is None:
        print("You need to specify an input path at least")
        return 1
    
    with open(palette_path, 'r') as fp:
        lines = fp.read().split('\n')

    color_to_palette = {}
    for line in lines:
        spl = line.split(':')
        if len(spl) != 2:
            continue
        
        color_to_palette[spl[0].strip()] = int(spl[1].strip())
    
    image = Image.open(in_path)
    output = []
    
    for y in range(image.height):
        for x in range(image.width):
            color = image.getpixel((x, y))
            color = f"{color[0]:02x}{color[1]:02x}{color[2]:02x}"
            if color not in color_to_palette:
                print(f"Color: {color} not in palette")
                return 1
            
            output.append(color_to_palette[color])

    true_output = []
    for i in range(len(output) // 2):
        true_output.append((output[i*2] << 4) | (output[i*2+1]))

    with open(out_path, 'wb') as fp:
        fp.write(bytes(true_output))
    
    return 0

if __name__ == "__main__":
    exit(main())
