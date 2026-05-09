#!/usr/bin/env python3
"""
S64 -> ASM converter
Converts a Sausage64 (.S64) static mesh into assembly display list + vertex data.
"""

import sys, os, re, math, json

# ── optional PIL import ───────────────────────────────────────────────────────
try:
    from PIL import Image
    HAS_PIL = True
except ImportError:
    HAS_PIL = False

FIXED_SCALE = 32.0
CACHE_SIZE  = 32

# ── fixed-point helpers ───────────────────────────────────────────────────────

def to_s10_5(f):
    v = int(round(f * FIXED_SCALE))
    # FIX: A 16-bit integer ranges from -32768 to 32767, not -1024 to 1023!
    v = max(-32768, min(32767, v))
    return v & 0xFFFF

def to_s15_16(f):
    clamped = max(-32768.0, min(32767.9999847, f))
    raw = int(round(clamped * 65536.0)) & 0xFFFFFFFF
    return (raw >> 16) & 0xFFFF, raw & 0xFFFF

# ── matrix math ───────────────────────────────────────────────────────────────

def mat_mul(a, b):
    result = [0.0] * 16
    for row in range(4):
        for col in range(4):
            result[row*4+col] = sum(a[row*4+k] * b[k*4+col] for k in range(4))
    return result

def perspective(fovy_deg, aspect, near, far):
    f = 1.0 / math.tan(math.radians(fovy_deg) / 2.0)
    nf = 1.0 / (near - far)
    return [
        f/aspect, 0, 0,                  0,
        0,        f, 0,                  0,
        0,        0, (far+near)*nf,      2*far*near*nf,
        0,        0, -1,                 0,
    ]

def look_at(eye, center, up):
    ex, ey, ez = eye
    cx, cy, cz = center
    ux, uy, uz = up
    fx, fy, fz = cx-ex, cy-ey, cz-ez
    fl = math.sqrt(fx*fx+fy*fy+fz*fz)
    fx, fy, fz = fx/fl, fy/fl, fz/fl
    rx, ry, rz = fy*uz-fz*uy, fz*ux-fx*uz, fx*uy-fy*ux
    rl = math.sqrt(rx*rx+ry*ry+rz*rz)
    rx, ry, rz = rx/rl, ry/rl, rz/rl
    ux2, uy2, uz2 = ry*fz-rz*fy, rz*fx-rx*fz, rx*fy-ry*fx
    return [
        rx,  ry,  rz,  -(rx*ex+ry*ey+rz*ez),
        ux2, uy2, uz2, -(ux2*ex+uy2*ey+uz2*ez),
        -fx, -fy, -fz,  (fx*ex+fy*ey+fz*ez),
        0,   0,   0,   1,
    ]

def build_mvp(meshes):
    all_x = [v[0] for m in meshes for v in m["verts"]]
    all_y = [v[1] for m in meshes for v in m["verts"]]
    all_z = [v[2] for m in meshes for v in m["verts"]]
    cx = (min(all_x)+max(all_x))/2.0
    cy = (min(all_y)+max(all_y))/2.0
    cz = (min(all_z)+max(all_z))/2.0
    radius = max(math.sqrt((v[0]-cx)**2+(v[1]-cy)**2+(v[2]-cz)**2)
                 for m in meshes for v in m["verts"]) or 1.0
    fovy, aspect = 512.0/384.0, 512.0/384.0 # Aspect ratio from your RCP size
    dist = radius / math.tan(math.radians(fovy/2.0)) * 1.3
    view = look_at((cx, cy, cz+dist), (cx, cy, cz), (0, -1, 0))
    proj = perspective(fovy, aspect, dist*0.01, dist+radius*4.0)
    return mat_mul(proj, view)

# ── texture conversion ────────────────────────────────────────────────────────

def to_rgb5551(r, g, b, a):
    r5 = (r >> 3) & 0x1F
    g5 = (g >> 3) & 0x1F
    b5 = (b >> 3) & 0x1F
    a1 = 1 if a >= 128 else 0
    return (r5 << 11) | (g5 << 6) | (b5 << 1) | a1

def load_texture(path):
    if not HAS_PIL:
        raise RuntimeError("Pillow is required for texture conversion. Install with: pip install Pillow")

    img = Image.open(path).convert("RGBA")
    w, h = img.size
    pixels = []
    for y in range(h):
        for x in range(w):
            r, g, b, a = img.getpixel((x, y))
            pixels.append(to_rgb5551(r, g, b, a))
    return pixels, w, h

# ── S64 parser ────────────────────────────────────────────────────────────────

def parse_s64(path):
    meshes = []
    current = None
    section = None

    with open(path) as f:
        for raw in f:
            line = raw.strip()
            if not line:
                continue
            parts = line.split()

            if parts[0] == "BEGIN" and len(parts) > 1 and parts[1] == "MESH":
                current = {"name": parts[2] if len(parts) > 2 else "mesh", "verts": [], "faces": []}
                section = None
            elif parts[0] == "END" and len(parts) > 1 and parts[1] == "MESH":
                if current:
                    meshes.append(current)
                current = None
                section = None
            elif parts[0] == "BEGIN" and len(parts) > 1 and parts[1] == "VERTICES":
                section = "verts"
            elif parts[0] == "END" and len(parts) > 1 and parts[1] == "VERTICES":
                section = None
            elif parts[0] == "BEGIN" and len(parts) > 1 and parts[1] == "FACES":
                section = "faces"
            elif parts[0] == "END" and len(parts) > 1 and parts[1] == "FACES":
                section = None
            elif section == "verts" and current is not None:
                f_vals = [float(p) for p in parts]
                x, y, z = f_vals[0], f_vals[1], f_vals[2]
                r = int(round(f_vals[6] * 255)) if len(f_vals) > 6 else 255
                g = int(round(f_vals[7] * 255)) if len(f_vals) > 7 else 255
                b = int(round(f_vals[8] * 255)) if len(f_vals) > 8 else 255
                a = int(round(f_vals[11] * 255)) if len(f_vals) > 11 else 255
                u = f_vals[9]  if len(f_vals) > 9  else 0.0
                v = f_vals[10] if len(f_vals) > 10 else 0.0
                r, g, b, a = [max(0, min(255, c)) for c in (r, g, b, a)]
                current["verts"].append((x, y, z, u, v, r, g, b, a))
            elif section == "faces" and current is not None:
                if len(parts) >= 4 and parts[0] == "3":
                    i0, i1, i2 = int(parts[1]), int(parts[2]), int(parts[3])
                    mat = parts[4] if len(parts) > 4 else "None"
                    current["faces"].append((i0, i1, i2, mat))

    return meshes

# ── batching ──────────────────────────────────────────────────────────────────

def batch_faces(faces, cache_size):
    from collections import OrderedDict
    by_mat = OrderedDict()
    for i0, i1, i2, mat in faces:
        by_mat.setdefault(mat, []).append((i0, i1, i2))

    batches = []
    for mat, tris in by_mat.items():
        current_verts = []
        current_faces = []
        vert_to_local = {}

        def flush(m=mat):
            if current_faces:
                batches.append({
                    "material":       m,
                    "global_indices": list(current_verts),
                    "faces":          list(current_faces),
                })

        for tri in tris:
            needed = [v for v in tri if v not in vert_to_local]
            if len(current_verts) + len(needed) > cache_size:
                flush()
                current_verts = []
                current_faces = []
                vert_to_local = {}
                needed = list(tri)

            for v in needed:
                if v not in vert_to_local:
                    vert_to_local[v] = len(current_verts)
                    current_verts.append(v)

            current_faces.append((vert_to_local[tri[0]], vert_to_local[tri[1]], vert_to_local[tri[2]]))

        flush()

    return batches

# ── ASM emitter ───────────────────────────────────────────────────────────────

def emit_asm(meshes, model_name, mat_map, base_dir):
    lines = []
    w = lines.append

    tex_info = {}
    tex_slot = 0
    
    # FIX: Add a default 1x1 white texture to prevent texture bleeding onto unmapped meshes
    tex_info["__DEFAULT_WHITE__"] = {"label": "tex_default_white", "pixels": [0xFFFF], "w": 1, "h": 1, "slot": tex_slot % 8}
    tex_slot += 1

    needed_mats = set()
    for mesh in meshes:
        for face in mesh["faces"]:
            needed_mats.add(face[3])

    for mat in sorted(needed_mats):
        if mat in mat_map:
            img_path = os.path.join(base_dir, mat_map[mat])
            if not os.path.exists(img_path):
                print(f"  WARNING: texture not found for material '{mat}': {img_path}")
                continue
            try:
                pixels, tw, th = load_texture(img_path)
                label = f"tex_{re.sub(r'\W+', '_', mat)}"
                tex_info[mat] = {"label": label, "pixels": pixels, "w": tw, "h": th, "slot": tex_slot % 8}
                tex_slot += 1
                print(f"  Loaded texture for '{mat}': {img_path} ({tw}x{th})")
            except Exception as e:
                print(f"  WARNING: failed to load texture for '{mat}': {e}")

    mvp = build_mvp(meshes)

    w(f"; Model: {model_name}")
    w(f"; Vertex cache size: {CACHE_SIZE}")
    w("")
    w("d32 0x15            ; SetCycleMode")
    w("d32 2               ; OneCycle")
    w("")
    w("d32 0x03            ; SetTransform")
    w("d32 mvp_matrix")
    w("")

    last_mat = None
    for mesh in meshes:
        mname   = re.sub(r'\W+', '_', mesh["name"])
        batches = batch_faces(mesh["faces"], CACHE_SIZE)
        w(f"; -- Mesh: {mesh['name']} ({len(mesh['verts'])} verts, {len(mesh['faces'])} tris, {len(batches)} batch(es)) --")

        for b_idx, batch in enumerate(batches):
            mat = batch["material"]

            if mat != last_mat:
                # FIX: Fallback safely to white texture to avoid "bleeding" into untextured materials
                ti = tex_info.get(mat, tex_info["__DEFAULT_WHITE__"])
                w(f"")
                w(f"; SetTexture for material '{mat}'")
                w(f"d32 0x10            ; SetTexture")
                w(f"d32 {ti['label']}")
                w(f"d32 {ti['w']}")
                w(f"d32 {ti['h']}")
                w(f"d32 {ti['slot']}    ; texture slot")
                last_mat = mat

            global_ids = batch["global_indices"]
            w(f"; batch {b_idx+1}/{len(batches)} mat={mat} ({len(global_ids)} verts, {len(batch['faces'])} tris)")
            w(f"d32 0x01            ; LoadVertices")
            w(f"d32 verts_{mname}_b{b_idx}")
            w(f"d32 {len(global_ids)}")
            w(f"d32 0")
            w("")
            for l0, l1, l2 in batch["faces"]:
                w(f"d32 0x02            ; DrawTriangle")
                w(f"d32 {l0}")
                w(f"d32 {l1}")
                w(f"d32 {l2}")
            w("")

    w("d32 0x21            ; EndList")
    w("")

    # ── MVP matrix ────────────────────────────────────────────────────────────
    w("; ---- MVP matrix (s15.16 fixed-point) ----")
    w("mvp_matrix:")
    int_halves, frac_halves = [], []
    for i, f in enumerate(mvp):
        ih, fh = to_s15_16(f)
        int_halves.append(ih)
        frac_halves.append(fh)
    w("; integer halves")
    for i, ih in enumerate(int_halves):
        row, col = divmod(i, 4)
        w(f"d16 0x{ih:04X}  ; m[{row}][{col}] = {mvp[i]:.6f}")
    w("; fractional halves")
    for i, fh in enumerate(frac_halves):
        row, col = divmod(i, 4)
        w(f"d16 0x{fh:04X}  ; m[{row}][{col}] = {mvp[i]:.6f}")
    w("")

    # ── Vertex data ───────────────────────────────────────────────────────────
    for mesh in meshes:
        mname   = re.sub(r'\W+', '_', mesh["name"])
        batches = batch_faces(mesh["faces"], CACHE_SIZE)

        for b_idx, batch in enumerate(batches):
            w(f"verts_{mname}_b{b_idx}:")
            for slot, global_i in enumerate(batch["global_indices"]):
                x, y, z, u, v, r, g, b, a = mesh["verts"][global_i]
                w(f"; slot {slot} (global vert {global_i})")
                w(f"d16 0x{to_s10_5(x):04X}  ; x = {x}")
                w(f"d16 0x{to_s10_5(y):04X}  ; y = {y}")
                w(f"d16 0x{to_s10_5(z):04X}  ; z = {z}")
                w(f"d16 0x{to_s10_5(1.0):04X}  ; w = 1.0")
                w(f"d16 0x{to_s10_5(u):04X}  ; u = {u}")
                w(f"d16 0x{to_s10_5(v):04X}  ; v = {v}")
                w(f"d8  0x{r:02X}    ; r")
                w(f"d8  0x{g:02X}    ; g")
                w(f"d8  0x{b:02X}    ; b")
                w(f"d8  0x{a:02X}    ; a")
                w("")
            w("")

    # ── Texture pixel data ────────────────────────────────────────────────────
    if tex_info:
        w("; ---- Texture data (RGB5551, u16 per pixel) ----")
        for mat, ti in tex_info.items():
            w(f"; Texture for material '{mat}' ({ti['w']}x{ti['h']})")
            w(f"{ti['label']}:")
            pixels = ti["pixels"]
            for i in range(0, len(pixels), 8):
                chunk = pixels[i:i+8]
                w("d16 " + ", ".join(f" 0x{p:04X}" for p in chunk))
            w("")

    return "\n".join(lines)

def main():
    if len(sys.argv) < 2:
        print("Usage: python s64_to_asm.py <model.S64> [output.cat] [materials.json]")
        sys.exit(1)

    in_path    = sys.argv[1]
    out_path   = sys.argv[2] if len(sys.argv) > 2 else os.path.splitext(in_path)[0] + ".cat"
    mat_file   = sys.argv[3] if len(sys.argv) > 3 else None
    base_dir   = os.path.dirname(os.path.abspath(out_path))
    model_name = re.sub(r'\W+', '_', os.path.splitext(os.path.basename(in_path))[0])

    mat_map = {}
    if mat_file:
        with open(mat_file) as f:
            mat_map = json.load(f)
        print(f"Loaded {len(mat_map)} material mappings from {mat_file}")

    if mat_map and not HAS_PIL:
        print("ERROR: Pillow is required for texture conversion.")
        sys.exit(1)

    print(f"Parsing {in_path} ...")
    meshes = parse_s64(in_path)
    asm = emit_asm(meshes, model_name, mat_map, base_dir)
    with open(out_path, "w") as f:
        f.write(asm)
    print(f"Written to {out_path}")

if __name__ == "__main__":
    main()