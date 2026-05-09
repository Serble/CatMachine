#version 460 core

layout (location = 0) in vec4 aPos;
layout (location = 1) in vec2 aUV;
layout (location = 2) in vec4 aColor;

uniform mat4 uTransform;

out vec2 vUV;
out vec4 vColor;

void main() {
    gl_Position = uTransform * aPos;

    /*
     * this assumes aUV is already in whatever space the texture sampling expects.
     * may want to divide by texture size either here or in the fragment shader
     */
    vUV = aUV;

    vColor = aColor;
}