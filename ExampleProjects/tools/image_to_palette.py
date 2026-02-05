import sys
from PIL import Image
import numpy as np

def rgb_to_hex(rgb):
    return "{:02x}{:02x}{:02x}".format(*rgb)

def hex_to_rgb(hexstr):
    return tuple(int(hexstr[i:i+2], 16) for i in (0, 2, 4))

def main():
    import argparse
    parser = argparse.ArgumentParser(
        description="Convert image to 4bpp indexed binary + palette."
    )
    parser.add_argument('-i', dest='input', required=True, help='Input PNG path')
    parser.add_argument('-o', dest='output', help="Output .bin path")
    parser.add_argument('-p', dest='palette', help="Output .palette path")
    parser.add_argument('--palettebin', dest='palettebin', help="Output .bin BGRA palette path")
    args = parser.parse_args()

    input_path = args.input
    output_path = args.output or input_path.rsplit('.', 1)[0] + '.bin'
    palette_path = args.palette or input_path.rsplit('.', 1)[0] + ".palette"
    palettebin_path = args.palettebin or input_path.rsplit('.', 1)[0] + ".palette.bin"

    img = Image.open(input_path).convert("RGBA")
    pixels = np.array(img).reshape((-1, 4))
    # stride RGBA, for palette bin we write as BGRA

    # For palette selection, use only RGB, but later include Alpha.
    colors, counts = np.unique(pixels[:, :3], axis=0, return_counts=True)
    color_list = [tuple(c) for c in colors]
    color_counts = dict(zip([rgb_to_hex(c) for c in color_list], counts))

    # Build RGBA for palette (get last alpha seen for each RGB value)
    rgb_to_a = {}
    for px in pixels:
        rgb_to_a[tuple(px[:3])] = px[3]
    color_list_full = [tuple(list(c) + [rgb_to_a[c]]) for c in color_list]

    # Find 16 palette colors in RGBA
    if len(color_list) <= 16:
        palette_colors = color_list_full
    else:
        from sklearn.cluster import KMeans
        kmeans = KMeans(n_clusters=16, n_init=10, random_state=0)
        labels = kmeans.fit_predict(colors)
        centers = kmeans.cluster_centers_
        # For each center, find closest real color for alpha
        palette_colors = []
        for center in centers:
            center_rgb = tuple(np.round(center).astype(int))
            dists = np.sum((colors - center) ** 2, axis=1)
            minidx = np.argmin(dists)
            closest_rgb = tuple(colors[minidx])
            alpha = rgb_to_a[closest_rgb]
            palette_colors.append(tuple(list(center_rgb) + [alpha]))
        palette_colors = [tuple(np.clip(np.round(c), 0, 255).astype(int)) for c in palette_colors]

    # Remap every pixel to its palette index (nearest in RGBA space)
    palette_arr = np.array(palette_colors)
    color_to_palette_idx = {}
    for i, pc in enumerate(palette_colors):
        color_to_palette_idx[rgb_to_hex(pc[:3])] = i

    indexed_pixels = []
    for px in pixels:
        hexcol = rgb_to_hex(px[:3])
        if hexcol in color_to_palette_idx:
            idx = color_to_palette_idx[hexcol]
        else:
            # Find closest palette color (use RGBA distance)
            dists = np.linalg.norm(palette_arr - px, axis=1)
            idx = np.argmin(dists)
            color_to_palette_idx[hexcol] = idx
        indexed_pixels.append(idx)

    # Pack as 4bpp
    packed = bytearray()
    for i in range(0, len(indexed_pixels), 2):
        v = indexed_pixels[i] << 4
        if i + 1 < len(indexed_pixels):
            v |= indexed_pixels[i + 1]
        packed.append(v)

    # Write packed output
    with open(output_path, "wb") as f:
        f.write(packed)

    # Write palette.txt file
    with open(palette_path, 'w') as fp:
        for i, color in enumerate(palette_colors):
            fp.write(f"{rgb_to_hex(color[:3])}: {i}\n")

    # Write palette.bin file as BGRA
    with open(palettebin_path, 'wb') as fp:
        for color in palette_colors:
            b, g, r, a = color[2], color[1], color[0], color[3]  # Correction: want BGRA (so [2], [1], [0], [3])?
            fp.write(bytes([color[2], color[1], color[0], color[3]]))  # BGRA

        # If fewer than 16, pad
        for _ in range(16 - len(palette_colors)):
            fp.write(b'\x00\x00\x00\x00')

    print(f"Wrote {output_path}, {palette_path}, and {palettebin_path}")

if __name__ == "__main__":
    main()