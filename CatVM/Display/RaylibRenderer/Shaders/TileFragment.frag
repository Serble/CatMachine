#version 330 core

in vec2 fragCoord;

out vec4 finalColor;

uniform sampler2D data;
uniform vec4 colDiffuse;

uniform int paletteLocation;
uniform int imageLocation;
uniform int tileScrollLocation;
uniform int tileIndexLocation;
uniform int tilePaletteLocation;
uniform int spriteLocation;
uniform int imageWidth = 177;

int getSampler(int index) {
    return int(texelFetch(data, ivec2(index % imageWidth, index / imageWidth), 0).r * 255.0);
}

float getSamplerUnfiltererd(int index) {
    return texelFetch(data, ivec2(index % imageWidth, index / imageWidth), 0).r;
}

void main() {
    ivec2 coord = ivec2(fragCoord) + 
            min(ivec2(getSampler(tileScrollLocation), getSampler(tileScrollLocation + 1)), ivec2(32));
    int tileIndex = coord.x / 16 + coord.y / 16 * 34;
    
    int imageStart = imageLocation + getSampler(tileIndexLocation + tileIndex) * 128;
    
    int tileCoord = coord.x % 16 + coord.y % 16 * 16;
    
    int colorIndex = getSampler(imageStart + tileCoord / 2);
    if (tileCoord % 2 == 0) {
        colorIndex = (colorIndex >> 4) & 0xf;
    } else {
        colorIndex = colorIndex & 0xf;
    }
    
    int paletteStart = getSampler(tilePaletteLocation + tileIndex / 2);
    if (tileIndex % 2 == 0) {
        paletteStart = (paletteStart >> 4) & 7;
    } else {
        paletteStart = paletteStart & 7;
    }
    paletteStart = paletteLocation + paletteStart * 16*4; // 16 colors per palette, 4 bytes per color
    
    paletteStart += colorIndex * 4;

    finalColor = vec4(
        getSamplerUnfiltererd(paletteStart + 2),
        getSamplerUnfiltererd(paletteStart + 1),
        getSamplerUnfiltererd(paletteStart),
        getSamplerUnfiltererd(paletteStart + 3)
    );
}
