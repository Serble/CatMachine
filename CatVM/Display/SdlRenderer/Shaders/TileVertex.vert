#version 450

layout(push_constant) uniform PushConstants {
    vec4 bounds; // x, y, width, height (pixel coords)
} pc;

// We'll pass the viewport size as a separate constant or use a standard projection
// For simplicity, we assume we are drawing to the swapchain size
layout(location = 0) out vec2 fragCoord;

void main() {
    // Generate a quad from gl_VertexIndex (0 to 5)
    vec2 pos[6] = vec2[](
        vec2(0.0, 0.0), vec2(1.0, 0.0), vec2(0.0, 1.0),
        vec2(0.0, 1.0), vec2(1.0, 0.0), vec2(1.0, 1.0)
    );

    vec2 vPos = pos[gl_VertexIndex];

    // Map vertex to NDC (-1 to 1) based on bounds
    // Note: This logic assumes you set a Scissor/Viewport to the window size
    // In SDL3 GPU, 0,0 is top-left in NDC for vertex positions? 
    // Actually, NDC is -1 to 1. We map [0,1] to the bounds.

    // Convert 0..1 to -1..1
    gl_Position = vec4(vPos.x * 2.0 - 1.0, vPos.y * 2.0 - 1.0, 0.0, 1.0);

    // Pass the VM-space coordinate (512x384) to fragment shader
    fragCoord = vPos * vec2(512, 384);
}
