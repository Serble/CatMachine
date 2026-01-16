#!/usr/bin/env python3
"""
8x16 pixel drawer -> Cat ASM data export
Pixel format stored/exported: 0x00RRGGBB (32-bit)

- One glyph per ASCII character (0–127)
- Always stored in strict ASCII order
- Glyph size = 8x16x4 = 512 bytes
- Address = font_base + ascii_code * 512
"""

import tkinter as tk
from tkinter import colorchooser, messagebox
import re

GRID_W = 128
GRID_H = 128
CELL = 5
FONT_ASM_PATH = "font.asm"
ASCII_MAX = 128
BYTES_PER_PIXEL = 4
GLYPH_SIZE = GRID_W * GRID_H * BYTES_PER_PIXEL


def rgb_to_00RRGGBB(rgb_hex: str) -> int:
    r = int(rgb_hex[1:3], 16)
    g = int(rgb_hex[3:5], 16)
    b = int(rgb_hex[5:7], 16)
    return (r << 16) | (g << 8) | b


def to_hex32(v: int) -> str:
    return f"0x{v | 0xFF000000 & 0xFFFFFFFF:08X}"


class PixelApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("ASCII Font Editor")
        self.resizable(False, False)

        self.current_color_hex = "#FFFFFF"
        self.pixels = [[0 for _ in range(GRID_W)] for _ in range(GRID_H)]

        self.current_ascii = 0

        top = tk.Frame(self, padx=8, pady=8)
        top.pack(fill="x")

        tk.Button(top, text="Pick color", command=self.pick_color).pack(side="left")
        self.color_preview = tk.Label(top, text="      ", bg=self.current_color_hex, relief="sunken")
        self.color_preview.pack(side="left", padx=(8, 16))

        tk.Button(top, text="Clear", command=self.clear).pack(side="left")

        tk.Button(top, text="Export", command=self.export_asm).pack(side="right")

        self.ascii_label = tk.Label(top, text="")
        self.ascii_label.pack(side="right", padx=8)

        self.canvas = tk.Canvas(self, width=GRID_W * CELL, height=GRID_H * CELL, bg="white")
        self.canvas.pack(padx=8, pady=(0, 8))

        self.rects = [[None for _ in range(GRID_W)] for _ in range(GRID_H)]
        self._build_grid()

        self.canvas.bind("<Button-1>", self.on_paint)
        self.canvas.bind("<B1-Motion>", self.on_paint)
        self.canvas.bind("<Button-3>", self.on_erase)
        self.canvas.bind("<B3-Motion>", self.on_erase)

        # Preview (1:1 scale)
        self.preview = tk.Canvas(self, width=GRID_W, height=GRID_H, bg="black")
        self.preview.pack(side="right", padx=8)

        self._update_ascii_label()

    def _refresh_preview(self):
        self.preview.delete("all")
        for y in range(GRID_H):
            for x in range(GRID_W):
                v = self.pixels[y][x] & 0xFFFFFF
                if v:
                    self.preview.create_rectangle(x+1, y+1, x+1, y+1, outline="", fill=f"#{v:06X}")

    def _update_ascii_label(self):
        ch = chr(self.current_ascii) if 32 <= self.current_ascii < 127 else "·"
        self.ascii_label.config(text=f"ASCII {self.current_ascii} '{ch}'")
        self._load_glyph(self.current_ascii)

    def _build_grid(self):
        for y in range(GRID_H):
            for x in range(GRID_W):
                x0, y0 = x * CELL, y * CELL
                x1, y1 = x0 + CELL, y0 + CELL
                r = self.canvas.create_rectangle(x0, y0, x1, y1, outline="#404040", fill="#000000")
                self.rects[y][x] = r

    def _refresh_canvas(self):
        for y in range(GRID_H):
            for x in range(GRID_W):
                v = self.pixels[y][x] & 0xFFFFFF
                fill = f"#{v:06X}" if v else "#000000"
                self.canvas.itemconfigure(self.rects[y][x], fill=fill)

    def pick_color(self):
        c = colorchooser.askcolor(color=self.current_color_hex)
        if c and c[1]:
            self.current_color_hex = c[1]
            self.color_preview.configure(bg=self.current_color_hex)

    def clear(self):
        for y in range(GRID_H):
            for x in range(GRID_W):
                self.pixels[y][x] = 0
                self.canvas.itemconfigure(self.rects[y][x], fill="#000000")

        self._refresh_preview()

    def _event_to_cell(self, event):
        x = event.x // CELL
        y = event.y // CELL
        if 0 <= x < GRID_W and 0 <= y < GRID_H:
            return x, y

    def on_paint(self, event):
        cell = self._event_to_cell(event)
        if cell:
            x, y = cell
            self.pixels[y][x] = rgb_to_00RRGGBB(self.current_color_hex)
            self.canvas.itemconfigure(self.rects[y][x], fill=self.current_color_hex)

        self._refresh_preview()

    def on_erase(self, event):
        cell = self._event_to_cell(event)
        if cell:
            x, y = cell
            self.pixels[y][x] = 0
            self.canvas.itemconfigure(self.rects[y][x], fill="#000000")

        self._refresh_preview()

    def _load_glyph(self, ascii_code):
        glyph_lines = GRID_H + 1
        try:
            with open(FONT_ASM_PATH, "r", encoding="utf-8") as f:
                all_lines = f.readlines()
        except FileNotFoundError:
            self.clear()
            return

        start = ascii_code * glyph_lines
        end = start + glyph_lines
        if end > len(all_lines):
            self.clear()
            return

        rows = all_lines[start+1:end]

        found_data = False
        for y, line in enumerate(rows):
            vals = re.findall(r"0x[0-9A-Fa-f]{8}", line)
            if len(vals) != GRID_W:
                self.clear()
                return
            for x, v in enumerate(vals):
                self.pixels[y][x] = int(v, 16)
                found_data = True

        if not found_data:
            self.clear()

        self._refresh_canvas()
        self._refresh_preview()

    def export_asm(self):
        directive = "d32"
        ascii_code = self.current_ascii
        ch = chr(ascii_code) if 32 <= ascii_code < 127 else "."

        # Build glyph text
        lines = []
        lines.append(f"; ASCII {ascii_code} '{ch}'\n")
        for y in range(GRID_H):
            row = [self.pixels[y][x] for x in range(GRID_W)]
            line = ", ".join(to_hex32(v) for v in row)
            lines.append(f"    {directive} {line}\n")

        glyph_text = "".join(lines)

        # Read existing file or create new
        try:
            with open(FONT_ASM_PATH, "r", encoding="utf-8") as f:
                all_lines = f.readlines()
        except FileNotFoundError:
            all_lines = []

        glyph_lines = len(lines)
        needed_lines = ASCII_MAX * glyph_lines

        # Expand file if needed
        while len(all_lines) < needed_lines:
            all_lines.append("\n")

        # Insert at correct ASCII slot
        start = ascii_code * glyph_lines
        all_lines[start:start + glyph_lines] = lines

        # Write back
        with open(FONT_ASM_PATH, "w", encoding="utf-8") as f:
            f.writelines(all_lines)

        self.current_ascii += 1
        if self.current_ascii >= ASCII_MAX:
            messagebox.showinfo("Done", "All ASCII glyphs completed.")
            self.current_ascii = 0

        self.clear()
        self._update_ascii_label()

if __name__ == "__main__":
    PixelApp().mainloop()

