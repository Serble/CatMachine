#version 450

layout(location = 0) in vec2 fragCoord;
layout(location = 0) out vec4 finalColor;

layout(set = 0, binding = 0) uniform sampler2D dataTex;

layout(push_constant) uniform Uniforms {
    layout(offset = 16) // Offset 16 because bounds is at 0 in the vertex stage
    int paletteLocation;
    int imageLocation;
    int tileScrollLocation;
    int tileIndexLocation;
    int tilePaletteLocation;
    int spriteLocation;
    int imageWidth;
} u;

int getSampler(int index) {
    return int(texelFetch(dataTex, ivec2(index % u.imageWidth, index / u.imageWidth), 0).r * 255.0);
}

float getSamplerUnfiltered(int index) {
    return texelFetch(dataTex, ivec2(index % u.imageWidth, index / u.imageWidth), 0).r;
}

void main() {
    // Apply scrolling
    ivec2 scroll = ivec2(getSampler(u.tileScrollLocation), getSampler(u.tileScrollLocation + 1));
    ivec2 coord = ivec2(fragCoord) + min(scroll, ivec2(32));

    int tileIndex = (coord.x / 16) + (coord.y / 16) * 34;
    int imageIndex = getSampler(u.tileIndexLocation + tileIndex);
    int imageStart = u.imageLocation + (imageIndex * 128);

    int tileLocalCoord = (coord.x % 16) + (coord.y % 16 * 16);

    // 4-bit nibble logic
    int colorIndex = getSampler(imageStart + tileLocalCoord / 2);
    if (tileLocalCoord % 2 == 0) {
        colorIndex = (colorIndex >> 4) & 0xf;
    } else {
        colorIndex = colorIndex & 0xf;
    }

    // Palette lookup
    int palData = getSampler(u.tilePaletteLocation + tileIndex / 2);
    int palIndex = (tileIndex % 2 == 0) ? ((palData >> 4) & 0x7) : (palData & 0x7);

    int paletteStart = u.paletteLocation + (palIndex * 64) + (colorIndex * 4);

    finalColor = vec4(
        getSamplerUnfiltered(paletteStart + 2), // B -> R
        getSamplerUnfiltered(paletteStart + 1), // G -> G
        getSamplerUnfiltered(paletteStart),     // R -> B
        getSamplerUnfiltered(paletteStart + 3)  // A
    );
}
