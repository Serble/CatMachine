---
title: Raylib PPU
slug: hardware/raylibppu
---

## Terminology
 - bgra: blue, green, red, alpha in one uint it looks like 0xAARRGGBB
 - bgrx: blue, green, red, reserved in one uint it looks like 0xRRGGBB  
reserved means it is ignored and does nothing

## Buffer display modes (0x0X)
these display modes are just raw bgra buffers
 - 0x00: 512x512, 1 048 576 bytes
 - 0x01: 512x384,   786 432 bytes
currently there is only a 512x512 version (0x00)

## Tiled display modes (0x1X)
These work with tiles and sprites:
tiles and sprites have alpha

4 layers:
 - clear color
 - sprites with draw_behind bit set
 - tiles
 - sprites without draw_behind bit set

Sprites:
 - sprite positions will be for its top left corner
 - sprites will be rotated around its center
 - sprites will scroll with tiles

```
sprite attributes: 0b0       0           0      0      0      000
                     visible draw_behind v-flip h-flip unused palette
```

### 0x11: 512x384 Tiled:
total size: 34_868 bytes
 - clear color: 4 bytes
 - 8 palettes: 16 colors each bgra, 64 bytes per palette, 512 bytes
 - 256 images: 16x16 4 bits per pixel, 128 bytes per image, 32768 bytes
 - tile scroll x: 1 byte, values are clamped to 0-32 inclusive
 - tile scroll y: 1 byte, values are clamped to 0-32 inclusive
 - tile indexes: 34x26 1 byte per tile index, 884 bytes
 - tile palettes: 34x26 4 bits per palette, 442 bytes
 - sprites: image_index: u8, attributes: u8, x_pos: u16, y_pos: u16, rotation: u16, 32 sprites, 256 bytes
