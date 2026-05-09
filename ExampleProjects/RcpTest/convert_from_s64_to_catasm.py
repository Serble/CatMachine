#!/usr/bin/env python3
"""
S64 -> ASM converter
Converts a Sausage64 (.S64) static mesh into assembly display list + vertex data.

S64 vertex columns:
  x y z  nx ny nz  r g b  u v
  (colours are 0.0-1.0, converted to 0-255)

Fixed-point:
  Vertices: s10.5  (1.0 = 0x0020 = 32)
  Matrix:   s15.16 (1.0 = 0x00010000)
    stored as two separate 16-bit arrays:
      first  16x d16 = integer halves  (rows 0-3, cols 0-3)
      second 16x d16 = fractional halves
"""

import sys, os, re, math

FIXED_SCALE = 32.0
CACHE_SIZE  = 32

# ── fixed-point helpers ───────────────────────────────────────────────────────

def to_s10_5(f):
    v = int(round(f * FIXED_SCALE))
    v = max(-1024, min(1023, v))
    return v & 0xFFFF

def to_s15_16(f):
    """Return (int_half u16, frac_half u16) for an s15.16 fixed-point value."""
    clamped = max(-32768.0, min(32767.9999847, f))
    raw = int(round(clamped * 65536.0)) & 0xFFFFFFFF  # unsigned 32-bit
    int_half  = (raw >> 16) & 0xFFFF
    frac_half = raw & 0xFFFF
    return int_half, frac_half

# ── matrix math (column-major, standard OpenGL convention) ───────────────────

def mat_mul(a, b):
    """Multiply two 4x4 matrices stored as flat row-major lists of 16 floats."""
    result = [0.0] * 16
    for row in range(4):
        for col in range(4):
            s = 0.0
            for k in range(4):
                s += a[row * 4 + k] * b[k * 4 + col]
            result[row * 4 + col] = s
    return result

def perspective(fovy_deg, aspect, near, far):
    """Standard OpenGL perspective matrix (row-major)."""
    f = 1.0 / math.tan(math.radians(fovy_deg) / 2.0)
    nf = 1.0 / (near - far)
    return [
        f / aspect, 0,  0,                    0,
        0,          f,  0,                    0,
        0,          0,  (far + near) * nf,   2 * far * near * nf,
        0,          0, -1,                    0,
    ]

def look_at(eye, center, up):
    """Standard lookAt matrix (row-major)."""
    ex, ey, ez = eye
    cx, cy, cz = center
    ux, uy, uz = up

    fx, fy, fz = cx - ex, cy - ey, cz - ez
    fl = math.sqrt(fx*fx + fy*fy + fz*fz)
    fx, fy, fz = fx/fl, fy/fl, fz/fl

    rx, ry, rz = fy*uz - fz*uy, fz*ux - fx*uz, fx*uy - fy*ux
    rl = math.sqrt(rx*rx + ry*ry + rz*rz)
    rx, ry, rz = rx/rl, ry/rl, rz/rl

    ux2, uy2, uz2 = ry*fz - rz*fy, rz*fx - rx*fz, rx*fy - ry*fx

    return [
        rx,   ry,   rz,   -(rx*ex + ry*ey + rz*ez),
        ux2,  uy2,  uz2,  -(ux2*ex + uy2*ey + uz2*ez),
        -fx,  -fy,  -fz,   (fx*ex + fy*ey + fz*ez),
        0,    0,    0,    1,
    ]

def build_mvp(meshes):
    """
    Compute a perspective MVP that frames all verts in view.
    Camera sits on the +Z side looking toward the model centre.
    """
    # Collect all vertex positions
    all_x = [v[0] for m in meshes for v in m["verts"]]
    all_y = [v[1] for m in meshes for v in m["verts"]]
    all_z = [v[2] for m in meshes for v in m["verts"]]

    cx = (min(all_x) + max(all_x)) / 2.0
    cy = (min(all_y) + max(all_y)) / 2.0
    cz = (min(all_z) + max(all_z)) / 2.0

    # Bounding sphere radius
    radius = max(
        math.sqrt((v[0]-cx)**2 + (v[1]-cy)**2 + (v[2]-cz)**2)
        for m in meshes for v in m["verts"]
    )
    if radius == 0:
        radius = 1.0

    fovy = 60.0
    aspect = 512.0 / 384.0

    # Distance so the sphere just fits vertically in the frustum
    dist = radius / math.tan(math.radians(fovy / 2.0))
    dist *= 1.3  # small padding

    eye    = (cx, cy, cz + dist)
    center = (cx, cy, cz)
    up     = (0, -1, 0)

    near = dist * 0.01
    far  = dist + radius * 4.0

    view = look_at(eye, center, up)
    proj = perspective(fovy, aspect, near, far)
    mvp  = mat_mul(proj, view)   # model = identity

    return mvp

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
                u = f_vals[9]  if len(f_vals) > 9  else 0.0
                v = f_vals[10] if len(f_vals) > 10 else 0.0
                r, g, b = [max(0, min(255, c)) for c in (r, g, b)]
                a = int(round(f_vals[11] * 255)) if len(f_vals) > 11 else 255
                a = max(0, min(255, a))
                current["verts"].append((x, y, z, u, v, r, g, b, a))
            elif section == "faces" and current is not None:
                if len(parts) >= 4 and parts[0] == "3":
                    i0, i1, i2 = int(parts[1]), int(parts[2]), int(parts[3])
                    current["faces"].append((i0, i1, i2))

    return meshes

# ── batching ──────────────────────────────────────────────────────────────────

def batch_faces(faces, cache_size):
    batches = []
    current_verts = []
    current_faces = []
    vert_to_local = {}

    def flush():
        if current_faces:
            batches.append({
                "global_indices": list(current_verts),
                "faces": list(current_faces),
            })

    for tri in faces:
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

        local_tri = (vert_to_local[tri[0]], vert_to_local[tri[1]], vert_to_local[tri[2]])
        current_faces.append(local_tri)

    flush()
    return batches

# ── ASM emitter ───────────────────────────────────────────────────────────────

def emit_asm(meshes, model_name):
    lines = []
    w = lines.append

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

    for mesh in meshes:
        mname   = re.sub(r'\W+', '_', mesh["name"])
        batches = batch_faces(mesh["faces"], CACHE_SIZE)
        w(f"; -- Mesh: {mesh['name']} ({len(mesh['verts'])} verts, {len(mesh['faces'])} tris, {len(batches)} batch(es)) --")

        for b_idx, batch in enumerate(batches):
            global_ids = batch["global_indices"]
            w(f"; batch {b_idx + 1}/{len(batches)} ({len(global_ids)} verts, {len(batch['faces'])} tris)")
            w(f"d32 0x01            ; LoadVertices")
            w(f"d32 verts_{mname}_b{b_idx}")
            w(f"d32 {len(global_ids)}")
            w(f"d32 0")
            w("")
            for local_i0, local_i1, local_i2 in batch["faces"]:
                w(f"d32 0x02            ; DrawTriangle")
                w(f"d32 {local_i0}")
                w(f"d32 {local_i1}")
                w(f"d32 {local_i2}")
            w("")

    w("d32 0x21            ; EndList")
    w("")

    # ── MVP matrix (s15.16, integer halves then fractional halves) ────────────
    w("; ---- MVP matrix (s15.16 fixed-point) ----")
    w("; Layout: 16x d16 integer halves, then 16x d16 fractional halves")
    w("mvp_matrix:")
    int_halves  = []
    frac_halves = []
    for i, f in enumerate(mvp):
        ih, fh = to_s15_16(f)
        int_halves.append(ih)
        frac_halves.append(fh)
        row, col = divmod(i, 4)

    w("; integer halves")
    for i, ih in enumerate(int_halves):
        row, col = divmod(i, 4)
        w(f"d16 0x{ih:04X}  ; m[{row}][{col}] int  = {mvp[i]:.6f}")
    w("; fractional halves")
    for i, fh in enumerate(frac_halves):
        row, col = divmod(i, 4)
        w(f"d16 0x{fh:04X}  ; m[{row}][{col}] frac = {mvp[i]:.6f}")
    w("")

    # ── Vertex data ───────────────────────────────────────────────────────────
    for mesh in meshes:
        mname   = re.sub(r'\W+', '_', mesh["name"])
        batches = batch_faces(mesh["faces"], CACHE_SIZE)

        for b_idx, batch in enumerate(batches):
            w(f"verts_{mname}_b{b_idx}:")
            for slot, global_i in enumerate(batch["global_indices"]):
                x, y, z, u, v, r, g, b, a = mesh["verts"][global_i]
                fx = to_s10_5(x)
                fy = to_s10_5(y)
                fz = to_s10_5(z)
                fw = to_s10_5(1.0)
                fu = to_s10_5(u)
                fv = to_s10_5(v)
                w(f"; slot {slot} (global vert {global_i})")
                w(f"d16 0x{fx:04X}  ; x = {x}")
                w(f"d16 0x{fy:04X}  ; y = {y}")
                w(f"d16 0x{fz:04X}  ; z = {z}")
                w(f"d16 0x{fw:04X}  ; w = 1.0")
                w(f"d16 0x{fu:04X}  ; u = {u}")
                w(f"d16 0x{fv:04X}  ; v = {v}")
                w(f"d8  0x{r:02X}    ; r")
                w(f"d8  0x{g:02X}    ; g")
                w(f"d8  0x{b:02X}    ; b")
                w(f"d8  0x{a:02X}    ; a")
                w("")
            w("")

    return "\n".join(lines)

# ── entry point ───────────────────────────────────────────────────────────────

def main():
    if len(sys.argv) < 2:
        print("Usage: python s64_to_asm.py <model.S64> [output.cat]")
        sys.exit(1)

    in_path    = sys.argv[1]
    out_path   = sys.argv[2] if len(sys.argv) > 2 else os.path.splitext(in_path)[0] + ".cat"
    model_name = re.sub(r'\W+', '_', os.path.splitext(os.path.basename(in_path))[0])

    print(f"Parsing {in_path} ...")
    meshes = parse_s64(in_path)
    for m in meshes:
        batches = batch_faces(m["faces"], CACHE_SIZE)
        print(f"  {m['name']}: {len(m['verts'])} verts, {len(m['faces'])} faces -> {len(batches)} batch(es)")

    mvp = build_mvp(meshes)
    print(f"MVP (row-major):")
    for row in range(4):
        print(f"  {[round(mvp[row*4+col], 4) for col in range(4)]}")

    asm = emit_asm(meshes, model_name)
    with open(out_path, "w") as f:
        f.write(asm)
    print(f"Written to {out_path}")

if __name__ == "__main__":
    main()