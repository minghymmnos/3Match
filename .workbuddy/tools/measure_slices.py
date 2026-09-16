# -*- coding: utf-8 -*-
"""Measure alpha bounding boxes of sprites in the art sheets.
Outputs rects in Unity texture coordinates (y measured from bottom).
"""
from PIL import Image
import numpy as np
import os

ROOT = r"E:\unityhub\project\3Match\Assets\Art\Resources"

def bbox_of(arr_mask):
    ys, xs = np.where(arr_mask)
    if len(xs) == 0:
        return None
    x0, x1 = int(xs.min()), int(xs.max()) + 1
    y0, y1 = int(ys.min()), int(ys.max()) + 1  # top-based, exclusive end
    return (x0, y0, x1, y1)

def unity_rect(x0, y0_top, x1, y1_top, H, pad=0):
    # convert top-based inclusive bbox to bottom-based rect with optional pad
    x = max(0, x0 - pad)
    w = min(W_limit, x1 + pad) - x
    y = max(0, H - y1_top - pad)
    h = (min(H, y1_top + pad)) - (H - y1_top - pad) if False else (y1_top + pad) - (H - y1_top - pad)
    # simpler: recompute
    y_bottom_start = max(0, H - (y1_top + pad))
    y_bottom_end = min(H, H - (y0_top - pad))
    return (int(x), int(y_bottom_start), int(w), int(y_bottom_end - y_bottom_start))

def report(path, cells=None, split_rows=False, split_cols=False):
    p = os.path.join(ROOT, path)
    im = Image.open(p).convert("RGBA")
    W, H = im.size
    a = np.array(im)[:, :, 3] > 12
    print(f"\n=== {path}  {W}x{H} ===")
    if cells:  # list of (cx0, cx1, cy0, cy1) top-based region to bbox
        for i, (cx0, cy0, cx1, cy1) in enumerate(cells):
            sub = a[cy0:cy1, cx0:cx1]
            bb = bbox_of(sub)
            if bb:
                x0, y0, x1, y1 = bb[0] + cx0, bb[1] + cy0, bb[2] + cx0, bb[3] + cy0
                print(f" cell{i}: pixel(top)=({x0},{y0})-({x1},{y1})  unity=Rect({x0},{H-y1},{x1-x0},{y1-y0})")
            else:
                print(f" cell{i}: EMPTY")
    if split_rows:  # find horizontal segments of content
        rows = a.any(axis=1)
        segs = []
        start = None
        for y, v in enumerate(rows):
            if v and start is None: start = y
            if not v and start is not None: segs.append((start, y)); start = None
        if start is not None: segs.append((start, H))
        for i, (y0, y1) in enumerate(segs):
            sub = a[y0:y1, :]
            bb = bbox_of(sub)
            x0, x1 = bb[0], bb[2]
            print(f" row{i}: y(top)=({y0},{y1}) x=({x0},{x1})  unity=Rect({x0},{H-y1},{x1-x0},{y1-y0})")

# 1. currency icons: 6 equal columns
im = Image.open(os.path.join(ROOT, "UI/ui_icons_currency_sheet.png")).convert("RGBA")
W, H = im.size
cells = [(int(i*W/6), 0, int((i+1)*W/6), H) for i in range(6)]
report("UI/ui_icons_currency_sheet.png", cells=cells)

# 2. level nodes: 5 equal columns
im = Image.open(os.path.join(ROOT, "UI/ui_level_nodes_5types.png")).convert("RGBA")
W, H = im.size
cells = [(int(i*W/5), 0, int((i+1)*W/5), H) for i in range(5)]
report("UI/ui_level_nodes_5types.png", cells=cells)

# 3. button sheet: split rows
report("UI/ui_btn_gold_sheet_3states.png", split_rows=True)

# 4. progressbar kit: split rows
report("UI/ui_progressbar_kit.png", split_rows=True)

# 5. burst sheet: 3x2 grid
im = Image.open(os.path.join(ROOT, "Effects/fx_burst_6colors_sheet.png")).convert("RGBA")
W, H = im.size
cells = []
for r in range(3):
    for c in range(2):
        cells.append((int(c*W/2), int(r*H/3), int((c+1)*W/2), int((r+1)*H/3)))
report("Effects/fx_burst_6colors_sheet.png", cells=cells)

# 6. pieces bbox (crop check)
for name in ["blk_rose_idle_01", "sp_rocket_idle_01", "sp_bomb_idle_01", "sp_rainbowball_idle_01", "sp_propeller_idle_01"]:
    report(f"Pieces/{name}.png", split_rows=False)
    im = Image.open(os.path.join(ROOT, f"Pieces/{name}.png")).convert("RGBA")
    a = np.array(im)[:, :, 3] > 12
    bb = bbox_of(a)
    if bb:
        x0, y0, x1, y1 = bb
        print(f"   content: x {x0}-{x1} ({100*(x1-x0)/im.size[0]:.0f}%w), y(top) {y0}-{y1} ({100*(y1-y0)/im.size[1]:.0f}%h)")

# 7. board base: inner grid geometry — detect via non-transparent bbox and assume 6x6 inner
report("UI/ui_board_base.png", split_rows=False)
im = Image.open(os.path.join(ROOT, "UI/ui_board_base.png")).convert("RGBA")
a = np.array(im)[:, :, 3] > 12
bb = bbox_of(a)
print("   full bbox(top):", bb)

# 8. panel base
report("UI/ui_panel_base_01.png", split_rows=False)
