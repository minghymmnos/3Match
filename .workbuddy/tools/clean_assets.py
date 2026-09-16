# -*- coding: utf-8 -*-
"""v2: stronger checker removal (TOL 34 + despeckle), count-based segmentation."""
from PIL import Image, ImageFilter
import numpy as np
from collections import deque
import os

ROOT = r"E:\unityhub\project\3Match\Assets\Art\Resources"
CLEAN = os.path.join(ROOT, "Clean")
TOL = 34

def checker_colors(im):
    px = np.array(im.convert("RGB"))
    samples = px[0:40, 0:40].reshape(-1, 3)
    bright = samples.mean(axis=1)
    med = np.median(bright)
    return samples[bright >= med].mean(axis=0), samples[bright < med].mean(axis=0)

def despeckle(rgba):
    """Remove small opaque specks via 3x3 opening on alpha."""
    a = Image.fromarray(rgba[:, :, 3], "L")
    opened = a.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    keep = np.array(opened) > 0
    rgba[:, :, 3] = np.where(keep, rgba[:, :, 3], 0)
    return rgba

def remove_checker(im):
    rgba = np.array(im.convert("RGBA"))
    H, W = rgba.shape[:2]
    light, dark = checker_colors(im)
    rgb = rgba[:, :, :3].astype(int)
    mask = (np.abs(rgb - light.reshape(1, 1, 3)) <= TOL).all(axis=2) | \
           (np.abs(rgb - dark.reshape(1, 1, 3)) <= TOL).all(axis=2)
    visited = np.zeros((H, W), dtype=bool)
    q = deque()
    for x in range(W):
        for y in (0, H - 1):
            if mask[y, x] and not visited[y, x]:
                visited[y, x] = True; q.append((y, x))
    for y in range(H):
        for x in (0, W - 1):
            if mask[y, x] and not visited[y, x]:
                visited[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < H and 0 <= nx < W and mask[ny, nx] and not visited[ny, nx]:
                visited[ny, nx] = True
                q.append((ny, nx))
    rgba[visited, 3] = 0
    rgba = despeckle(rgba)
    return Image.fromarray(rgba, "RGBA")

def content_mask(img, thr=12):
    return np.array(img)[:, :, 3] > thr

def content_bbox(img, thr=12, min_count=12):
    a = content_mask(img, thr)
    rows = a.sum(axis=1) >= min_count
    cols = a.sum(axis=0) >= min_count
    ys = np.where(rows)[0]; xs = np.where(cols)[0]
    if len(xs) == 0:
        return None
    return int(xs[0]), int(ys[0]), int(xs[-1]) + 1, int(ys[-1]) + 1

def clear_watermark(img):
    W, H = img.size
    cx0, cy0 = int(W * 0.72), int(H * 0.90)
    a = content_mask(img)
    a2 = a.copy(); a2[cy0:, cx0:] = False
    ys, xs = np.where(a2)
    if len(xs) == 0:
        return img
    if int(xs.max()) < cx0 and int(ys.max()) < cy0:
        arr = np.array(img); arr[cy0:, cx0:, 3] = 0
        return Image.fromarray(arr, "RGBA")
    return img

def row_segments(img, thr=12, min_count=15):
    a = content_mask(img, thr)
    rows = a.sum(axis=1) >= min_count
    segs, start = [], None
    for y, v in enumerate(rows):
        if v and start is None: start = y
        if not v and start is not None: segs.append((start, y)); start = None
    if start is not None: segs.append((start, len(rows)))
    return segs

def save_frame(img, box, out_path, pad=4):
    x0, y0, x1, y1 = box
    x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
    x1 = min(img.size[0], x1 + pad); y1 = min(img.size[1], y1 + pad)
    img.crop((x0, y0, x1, y1)).save(out_path)
    print(f"  saved {os.path.relpath(out_path, CLEAN)}  {x1-x0}x{y1-y0}")

def ensure(d): os.makedirs(d, exist_ok=True)

def clean_and_split_sheet(rel, out_dir, n_cols, names):
    im = Image.open(os.path.join(ROOT, rel))
    cleaned = clear_watermark(remove_checker(im))
    W, H = cleaned.size
    ensure(out_dir)
    cw = W / n_cols
    for i, name in enumerate(names):
        sub = cleaned.crop((int(i * cw), 0, int((i + 1) * cw), H))
        bb = content_bbox(sub)
        if bb is None:
            print(f"  !! EMPTY frame {name}"); continue
        save_frame(sub, bb, os.path.join(out_dir, name + ".png"))

def clean_single(rel, out_path):
    im = Image.open(os.path.join(ROOT, rel))
    cleaned = clear_watermark(remove_checker(im))
    ensure(os.path.dirname(out_path))
    bb = content_bbox(cleaned)
    if bb: save_frame(cleaned, bb, out_path)
    return cleaned

print("== icons ==")
clean_and_split_sheet("UI/ui_icons_currency_sheet.png", os.path.join(CLEAN, "Icons"), 6,
                      ["icon_coin", "icon_star", "icon_heart", "icon_ticket", "icon_gear", "icon_coin2"])

print("== level nodes ==")
clean_and_split_sheet("UI/ui_level_nodes_5types.png", os.path.join(CLEAN, "Nodes"), 5,
                      ["node_done", "node_current", "node_locked", "node_hard", "node_super"])

print("== buttons ==")
im = Image.open(os.path.join(ROOT, "UI/ui_btn_gold_sheet_3states.png"))
cleaned = clear_watermark(remove_checker(im))
ensure(os.path.join(CLEAN, "Buttons"))
segs = row_segments(cleaned)
print("  button rows:", segs)
for i, (y0, y1) in enumerate(segs):
    sub = cleaned.crop((0, y0, cleaned.size[0], y1))
    bb = content_bbox(sub)
    name = ["btn_normal", "btn_pressed", "btn_disabled"][i] if i < 3 else f"btn_extra_{i}"
    save_frame(sub, bb, os.path.join(CLEAN, "Buttons", name + ".png"))

print("== progress bars ==")
im = Image.open(os.path.join(ROOT, "UI/ui_progressbar_kit.png"))
cleaned = clear_watermark(remove_checker(im))
ensure(os.path.join(CLEAN, "Bars"))
segs = row_segments(cleaned)
print("  bar rows:", segs)
if segs:
    y0, y1 = segs[0]
    sub = cleaned.crop((0, y0, cleaned.size[0], y1))
    save_frame(sub, content_bbox(sub), os.path.join(CLEAN, "Bars", "bar_empty.png"))
rgb = np.array(cleaned)[:, :, :3].astype(int)
alpha = np.array(cleaned)[:, :, 3] > 12
def fill_box(y0, y1, kind):
    r, g, b = rgb[y0:y1, :, 0], rgb[y0:y1, :, 1], rgb[y0:y1, :, 2]
    if kind == "teal":
        m = (g > r + 25) & (g > 100) & alpha[y0:y1, :]
    else:
        m = (r > b + 40) & (r > 150) & (g < r) & alpha[y0:y1, :]
    ys, xs = np.where(m)
    if len(xs) == 0: return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
if len(segs) >= 3:
    (t0, t1), (g0, g1) = segs[1], segs[2]
    for (yy0, yy1, kind, nm) in [(t0, t1, "teal", "bar_fill_teal"), (g0, g1, "gold", "bar_fill_gold")]:
        bb = fill_box(yy0, yy1, kind)
        if bb: save_frame(cleaned.crop((0, yy0, cleaned.size[0], yy1)), bb, os.path.join(CLEAN, "Bars", nm + ".png"), pad=2)

print("== burst frames ==")
im = Image.open(os.path.join(ROOT, "Effects/fx_burst_6colors_sheet.png"))
cleaned = clear_watermark(remove_checker(im))
ensure(os.path.join(CLEAN, "Effects"))
names = ["fx_burst_rose", "fx_burst_drop", "fx_burst_leaf", "fx_burst_star", "fx_burst_gem", "fx_burst_orange"]
W, H = cleaned.size
for idx, nm in enumerate(names):
    r, c = idx // 2, idx % 2
    sub = cleaned.crop((int(c * W / 2), int(r * H / 3), int((c + 1) * W / 2), int((r + 1) * H / 3)))
    bb = content_bbox(sub)
    if bb: save_frame(sub, bb, os.path.join(CLEAN, "Effects", nm + ".png"))

print("== fx singles ==")
for f in ["fx_explosion_flash_01", "fx_rainbow_wave", "fx_sparkle_trail"]:
    clean_single(f"Effects/{f}.png", os.path.join(CLEAN, "Effects", f + ".png"))

print("== pieces ==")
for f in ["blk_rose_idle_01", "blk_drop_idle_01", "blk_leaf_idle_01", "blk_star_idle_01",
          "blk_gem_idle_01", "blk_orange_idle_01", "sp_rocket_idle_01", "sp_bomb_idle_01",
          "sp_rainbowball_idle_01", "sp_propeller_idle_01"]:
    clean_single(f"Pieces/{f}.png", os.path.join(CLEAN, "Pieces", f + ".png"))

print("== panel base ==")
clean_single("UI/ui_panel_base_01.png", os.path.join(CLEAN, "UI", "ui_panel_base_01.png"))

print("== board base (keep size) ==")
im = Image.open(os.path.join(ROOT, "UI/ui_board_base.png"))
board = remove_checker(im)  # keep size; do NOT clear watermark if art reaches corner
ensure(os.path.join(CLEAN, "UI"))
board.save(os.path.join(CLEAN, "UI", "ui_board_base.png"))
print(f"  saved UI/ui_board_base.png (kept {board.size[0]}x{board.size[1]})")
arr = np.array(board)
warm = (arr[:, :, 0].astype(int) > arr[:, :, 2].astype(int) + 15) & (arr[:, :, 3] > 12)
ys, xs = np.where(warm)
gx0, gy0, gx1, gy1 = int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
print(f"  inner grid(top): ({gx0},{gy0})-({gx1},{gy1})")
cw, ch = (gx1 - gx0) / 6.0, (gy1 - gy0) / 6.0
for (cx, cy, nm) in [(0, 0, "cell_light"), (1, 0, "cell_dark")]:
    x0 = int(gx0 + cx * cw) + 3; y0 = int(gy0 + cy * ch) + 3
    x1 = int(gx0 + (cx + 1) * cw) - 3; y1 = int(gy0 + (cy + 1) * ch) - 3
    board.crop((x0, y0, x1, y1)).save(os.path.join(CLEAN, "UI", nm + ".png"))
    print(f"  saved UI/{nm}.png  {x1-x0}x{y1-y0}")

# ---- contact sheet for visual verification ----
print("== contact sheet ==")
tiles = []
for d, names in [
    ("Icons", ["icon_coin", "icon_star", "icon_heart", "icon_ticket", "icon_gear"]),
    ("Nodes", ["node_done", "node_current", "node_locked", "node_hard", "node_super"]),
    ("Buttons", ["btn_normal", "btn_pressed", "btn_disabled"]),
    ("Bars", ["bar_empty", "bar_fill_teal", "bar_fill_gold"]),
    ("Effects", ["fx_burst_rose", "fx_burst_star", "fx_burst_orange"]),
    ("Pieces", ["blk_rose_idle_01", "blk_star_idle_01", "sp_rainbowball_idle_01"]),
    ("UI", ["cell_light", "cell_dark"]),
]:
    for n in names:
        p = os.path.join(CLEAN, d, n + ".png")
        if os.path.exists(p):
            tiles.append((f"{d}/{n}", Image.open(p)))
cols = 6
cell = 260
rows = (len(tiles) + cols - 1) // cols
sheet = Image.new("RGBA", (cols * cell, rows * (cell + 22)), (90, 90, 110, 255))
from PIL import ImageDraw
dr = ImageDraw.Draw(sheet)
for i, (nm, im2) in enumerate(tiles):
    x = (i % cols) * cell; y = (i // cols) * (cell + 22)
    im2 = im2.copy(); im2.thumbnail((cell - 12, cell - 12))
    sheet.paste(im2, (x + 6, y + 6), im2)
    dr.text((x + 8, y + cell - 2), nm, fill=(255, 255, 255, 255))
sheet.save(r"E:\unityhub\project\3Match\.workbuddy\tools\contact_sheet.png")
print("  contact sheet saved")
print("ALL DONE")
