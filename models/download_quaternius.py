#!/usr/bin/env python3
"""Download Quaternius CC0 asset packs as self-contained GLB files from Poly Pizza.

Poly Pizza hosts Quaternius packs as "bundles". Each bundle page links to model
pages (/m/<id>), and each model page embeds a direct GLB link
(https://static.poly.pizza/<uuid>.glb). GLB files embed their textures, so a
single download per model is enough for Godot to import.

Usage:  python3 download_quaternius.py
Output: models/quaternius/<pack>/<slug>.glb
"""
from __future__ import annotations

import os
import re
import sys
import time
import urllib.request
import urllib.error

BASE = "https://poly.pizza"
UA = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
UA += "(KHTML, like Gecko) Chrome/124.0 Safari/537.36"

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_ROOT = os.path.join(SCRIPT_DIR, "quaternius")

# pack folder name -> poly.pizza bundle slug
PACKS = {
    "toon_shooter": "Toon-Shooter-Game-Kit-qraiSXoAru",
    "tanks": "Animated-Tank-Pack-0tfvbeAJkU",
    "scifi_guns": "Sci-Fi-Modular-Gun-Pack-TbRddR9Fsu",
    "modular_men": "Ultimate-Modular-Men-Pack-ZiH8muWqwQ",
    "modular_women": "Ultimate-Modular-Women-Pack-aCBDXDdTNN",
}

MODEL_LINK_RE = re.compile(r"/m/([A-Za-z0-9]+)")
GLB_RE = re.compile(r"https://static\.poly\.pizza/[A-Za-z0-9-]+\.glb")
# Poly Pizza embeds the model name in the <h1> and og:title meta tag, e.g.
#   <h1 ...>Tank</h1>   /   content="Tank - Free Model By Quaternius"
TITLE_RE = re.compile(
    r'<h1[^>]*>([^<]+)</h1>|property="og:title"\s+content="([^"-]+?)\s*-\s*Free Model'
)


def http_get(url: str, binary: bool = False, timeout: int = 60):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        data = r.read()
    return data if binary else data.decode("utf-8", "ignore")


def slugify(name: str) -> str:
    name = re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_").lower()
    return name or "model"


def get_model_ids(bundle_slug: str) -> list[str]:
    html = http_get(f"{BASE}/bundle/{bundle_slug}")
    ids: list[str] = []
    for mid in MODEL_LINK_RE.findall(html):
        if mid not in ids:
            ids.append(mid)
    return ids


def get_glb_and_name(model_id: str) -> tuple[str | None, str]:
    html = http_get(f"{BASE}/m/{model_id}")
    glb = GLB_RE.search(html)
    m = TITLE_RE.search(html)
    name = ""
    if m:
        name = (m.group(1) or m.group(2) or "").strip()
    return (glb.group(0) if glb else None), (name or model_id)


def download(url: str, dest: str) -> int:
    data = http_get(url, binary=True)
    with open(dest, "wb") as f:
        f.write(data)
    return len(data)


def main() -> int:
    os.makedirs(OUT_ROOT, exist_ok=True)
    total_ok = total_fail = 0
    manifest_lines: list[str] = []

    for pack, slug in PACKS.items():
        out_dir = os.path.join(OUT_ROOT, pack)
        os.makedirs(out_dir, exist_ok=True)
        print(f"\n=== {pack} ({slug}) ===")
        try:
            ids = get_model_ids(slug)
        except Exception as e:  # noqa: BLE001
            print(f"  [FAIL] bundle fetch: {e}")
            total_fail += 1
            continue
        print(f"  {len(ids)} models")

        seen_names: dict[str, int] = {}
        for mid in ids:
            try:
                glb, name = get_glb_and_name(mid)
                if not glb:
                    print(f"  [skip] {mid}: no GLB link")
                    total_fail += 1
                    continue
                base = slugify(name)
                seen_names[base] = seen_names.get(base, 0) + 1
                if seen_names[base] > 1:
                    base = f"{base}_{seen_names[base]}"
                dest = os.path.join(out_dir, f"{base}.glb")
                size = download(glb, dest)
                print(f"  [ok] {base}.glb ({size // 1024}KB)")
                manifest_lines.append(f"{pack}/{base}.glb\t{glb}")
                total_ok += 1
            except Exception as e:  # noqa: BLE001
                print(f"  [fail] {mid}: {e}")
                total_fail += 1
            time.sleep(0.3)  # be polite to the CDN

    with open(os.path.join(OUT_ROOT, "MANIFEST.txt"), "w") as f:
        f.write("\n".join(manifest_lines) + "\n")

    print(f"\nDONE  ok={total_ok} fail={total_fail}  -> {OUT_ROOT}")
    return 0 if total_ok else 1


if __name__ == "__main__":
    sys.exit(main())
