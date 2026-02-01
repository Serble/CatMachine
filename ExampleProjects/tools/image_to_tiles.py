import sys
from PIL import Image

def main():
    image_path = sys.argv[1]
    with open(image_path[:image_path.rfind('.')] + ".palette", 'r') as fp:
        lines = fp.read().split('\n')

    color_to_palette = {}
    for line in lines:
        spl = line.split(':')
        if len(spl) != 2:
            continue
        
        color_to_palette[spl[0].strip()] = int(spl[1].strip())
    
    image = Image.open(image_path)
    output = []
    
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, _ = image.getpixel((x, y))
            color = f"{r:02x}{g:02x}{b:02x}"
            if color not in color_to_palette:
                print(f"Color: {color} not in palette")
                return
            
            output.append(color_to_palette[color])

    true_output = []
    for i in range(len(output) // 2):
        true_output.append((output[i*2] << 4) | (output[i*2+1]))

    with open(image_path[:image_path.rfind('.')] + ".bin", 'wb') as fp:
        fp.write(bytes(true_output))

if __name__ == "__main__":
    main()
