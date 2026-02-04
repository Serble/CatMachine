#version 330

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;
uniform vec4 bounds;

out vec2 fragCoord;

void main() {
    fragCoord = (vertexPosition.xy - bounds.xy) / bounds.zw * vec2(512, 384);
    
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}
