#version 330 core

in vec2 fragTexCoord;
in vec4 fragColor;

out vec4 finalColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform uint image[32];
uniform uint palette[16];

void main() {
    uvec2 coord = uvec2(int(gl_FragCoord.x) % 16, 15 - int(gl_FragCoord.y) % 16);
    uint index = coord.x + coord.y * 16u; // position in color
    
    uint uIndex = index / 8u; // 2 colors per byte, 8 colors per uint
    uint colorIndex = image[uIndex];
    colorIndex = (colorIndex >> (((index / 2u) % 4u) * 8u)); // isolate byte
    
    if (index % 2u == 0u) { // get half byte
        colorIndex = (colorIndex >> 4) & 0xFu;
    } else {
        colorIndex = colorIndex & 0xFu;
    }
    
    finalColor = vec4(colorIndex, index % 2u, 0.0, 1.0);
    
    uint color = palette[colorIndex];
    finalColor = vec4(float((color >> 16u) & 0xffu) / 255.0, float((color >> 8u) & 0xffu) / 255.0,
        float(color & 0xffu) / 255.0, float((color >> 24u) & 0xffu) / 255.0);
}
