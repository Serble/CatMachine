#version 330 core

in vec2 fragCoord;

out vec4 fragColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform sampler2D images;
uniform sampler2D palettes;
uniform sampler2D tileImages;
uniform sampler2D tilePalettes;

int getSampler(sampler2D samp, int index) {
    return int(texelFetch(samp, ivec2(index, 0), 0).r * 255.0 + 0.5);
}

void main() {
    fragColor = vec4(texelFetch(images, ivec2(0,0),0).r*255.0,0,0,1);
//    finalColor = vec4(getSampler(images, 0), 0.0, 0.0, 1.0);
    return;
    
    ivec2 coord = ivec2(fragCoord);
    int tileIndex = coord.x / 16 + coord.y / 16 * 32;
    int hTileIndex = tileIndex / 2;
    
    int imageStart = getSampler(tileImages, tileIndex) * 128;
    
    int tileCoord = coord.x % 16 + coord.y % 16 * 16;
    
    int colorIndex = getSampler(images, imageStart + tileCoord / 2);
    if (tileCoord % 2 == 0) {
        colorIndex = (colorIndex >> 4) & 0xf;
    } else {
        colorIndex = colorIndex & 0xf;
    }
    
    int paletteStart = getSampler(tilePalettes, hTileIndex);
    if (tileIndex % 2 == 0) {
        paletteStart = (paletteStart >> 4) & 7;
    } else {
        paletteStart = paletteStart & 7;
    }
    paletteStart *= 16; // 16 colors per palette

    fragColor = texelFetch(palettes, ivec2(paletteStart + colorIndex, 0), 0).bgra;
}
