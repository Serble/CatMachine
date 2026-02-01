#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

uniform sampler2D data;
uniform int imageWidth = 177;

out vec4 finalColor;

int getSampler(int index) {
    return int(texelFetch(data, ivec2(index % imageWidth, index / imageWidth), 0).r * 255.0);
}

float getSamplerUnfiltererd(int index) {
    return texelFetch(data, ivec2(index % imageWidth, index / imageWidth), 0).r;
}

void main() {
    ivec2 coord = ivec2(fragTexCoord.xy * 16.0);
    if (fragColor.g > 0.5) { // HFlip
        coord.x = 15 - coord.x;
    }

    if (fragColor.b > 0.5) { // VFlip
        coord.y = 15 - coord.y;
    }
    
    int coordScalar = coord.x + coord.y * 16;
    int imageIndex = int(fragColor.r * 255.0);
    int paletteIndex = int(fragColor.a * 255.0);

    int imageStart = 4+512 + imageIndex * 128;

    int colorIndex = getSampler(imageStart + coordScalar / 2);
    if (coordScalar % 2 == 0) {
        colorIndex = (colorIndex >> 4) & 0xf;
    } else {
        colorIndex = colorIndex & 0xf;
    }

    // 4 bytes for clear color, 16 colors per palette, 4 bytes per color
    int paletteStart = 4 + paletteIndex * 16*4 + colorIndex * 4;
    
    finalColor = vec4(
        getSamplerUnfiltererd(paletteStart + 2),
        getSamplerUnfiltererd(paletteStart + 1),
        getSamplerUnfiltererd(paletteStart),
        getSamplerUnfiltererd(paletteStart + 3)
    );
}
