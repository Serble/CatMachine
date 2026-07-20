#version 460 core

in vec2 vUV;
in vec4 vColor;

out vec4 FragColor;

uniform sampler2D uTexture;
uniform vec2 uTextureSize;

/*
 * Ping-pong framebuffer texture.
 *
 * This must be the PREVIOUS framebuffer image, not the FBO currently being
 * rendered into.
 *
 * C# binding:
 *
 *     Texture unit 1 = _fboTextures[_frontFboIndex]
 *
 */
uniform sampler2D uMemory;

uniform bool uUseTexture;

uniform vec4 uFogColor;
uniform vec4 uBlendColor;

uniform vec2 uResolution; // usually 512, 384

// 0 = Fill
// 1 = Copy
// 2 = OneCycle
// 3 = TwoCycle
uniform int uCycleMode;

// Blend mode selectors, cycle 1
uniform int uASrc;
uniform int uBSrc;
uniform int uPSrc;
uniform int uQSrc;

// Blend mode selectors, cycle 2
uniform int uASrc2;
uniform int uBSrc2;
uniform int uPSrc2;
uniform int uQSrc2;

/*
 * BlendSource enum from C#:
 *
 * PixelColor  = 0
 * MemoryColor = 1
 * BlendColor  = 2
 * FogColor    = 3
 */
vec4 getColor(int src, vec4 pixel, vec4 memory) {
    if (src == 0) {
        return pixel;
    }

    if (src == 1) {
        return memory;
    }

    if (src == 2) {
        return uBlendColor;
    }

    if (src == 3) {
        return uFogColor;
    }

    return pixel;
}

/*
 * BlendFactor enum from C#:
 *
 * PixelAlpha = 0
 * FogAlpha   = 1
 * One        = 2
 * Zero       = 3
 */
float getFactor(int src, vec4 pixel) {
    if (src == 0) {
        return pixel.a;
    }

    if (src == 1) {
        return uFogColor.a;
    }

    if (src == 2) {
        return 1.0;
    }

    if (src == 3) {
        return 0.0;
    }

    return 1.0;
}

/*
 * Sample previous framebuffer color.
 *
 * Use texelFetch instead of texture() so that framebuffer memory reads are
 * pixel-exact and are not affected by filtering or half-pixel UV issues.
 */
vec4 sampleMemoryColor() {
    ivec2 memorySize = textureSize(uMemory, 0);
    ivec2 coord = ivec2(gl_FragCoord.xy);
    coord = clamp(coord, ivec2(0, 0), memorySize - ivec2(1, 1));

    return texelFetch(uMemory, coord, 0);
}

vec4 getPixelColor() {
    if (uUseTexture) {
        vec2 uv = vUV / uTextureSize;
        return texture(uTexture, uv) * vColor;
    }

    return vColor;
}

/*
 * approximate RDP-style blend equation:
 *
 *     result = (A * p + B * q) / (A + B)
 *
 * This is not bit-accurate N64 RDP blending, but the structure C# side currently exposes.
 */
vec4 runBlendCycle(
    vec4 pixel,
    vec4 memory,
    int aSrc,
    int bSrc,
    int pSrc,
    int qSrc
) {
    float A = getFactor(aSrc, pixel);
    float B = getFactor(bSrc, pixel);

    vec4 p = getColor(pSrc, pixel, memory);
    vec4 q = getColor(qSrc, pixel, memory);

    float denom = A + B;

    if (denom <= 0.0) {
        return pixel;
    }

    vec4 result = (A * p + B * q) / denom;

    /*
     * Keep alpha sane.
     *
     * The real N64 blender has more nuanced alpha/coverage behavior.
     * For this approximation, clamping avoids invalid values.
     */
    return clamp(result, 0.0, 1.0);
}

void main() {
    vec4 pixel = getPixelColor();
    vec4 memory = sampleMemoryColor();

    if (uCycleMode == 0) {
        /*
         * Fill mode.
         *
         * This is a simplified approximation. Real RDP fill mode is not just
         * normal triangle shading.
         */
        FragColor = uBlendColor;
        return;
    }

    if (uCycleMode == 1) {
        /*
         * Copy mode.
         *
         * Simplified: output pixel directly.
         */
        FragColor = pixel;
        return;
    }

    vec4 cycle1 = runBlendCycle(pixel, memory, uASrc, uBSrc, uPSrc, uQSrc);

    if (uCycleMode == 2) {
        FragColor = cycle1;
        return;
    }

    vec4 cycle2 = runBlendCycle(cycle1, memory, uASrc2, uBSrc2, uPSrc2, uQSrc2);
    FragColor = cycle2;
}