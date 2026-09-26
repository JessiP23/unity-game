#!/usr/bin/env python3
"""Download the free art the prototype dresses itself with.

Poly Haven models and textures are CC0. Microsoft Rocketbox avatars and
animations are MIT. Files land in Assets/Resources/Art and Unity writes the
.meta files on its next import. Re-running skips files that already exist.
"""
import http.client
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'Assets/Resources/Art'
AGENT = {'User-Agent': 'Mozilla/5.0 (NightSupermarket art fetch)'}
POLYHAVEN = 'https://api.polyhaven.com/files/'
ROCKETBOX = 'https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/Assets/'

PROPS = [
    # Sales floor
    'steel_frame_shelves_01', 'steel_frame_shelves_02', 'long_life_food', 'russian_food_cans_01',
    'multi_cleaner_bottle', 'bleach_bottle', 'all_purpose_cleaner', 'multi_cleaner_5_litre',
    'wine_bottles_01', 'croissant', 'hamburger_buns', 'food_apple_01', 'bananas', 'lemon',
    'yellow_onion', 'sweet_potato', 'food_avocado_01', 'food_lime_01', 'plastic_crate_01',
    'plastic_crate_02', 'CashRegister_01', 'WetFloorSign_01', 'metal_trash_can',
    'korean_fire_extinguisher_01', 'fire_alarm', 'wall_clock', 'security_camera_01',
    'security_camera_02', 'mounted_fluorescent_lights', 'wooden_display_shelves_01',
    # Warehouse and security room
    'worn_metal_rack', 'cardboard_box_01', 'hand_truck', 'plastic_container',
    'industrial_pastic_container', 'wooden_crate_02', 'Barrel_02', 'caged_hanging_light',
    'industrial_wall_lamp', 'rollershutter_door', 'trashbag', 'plastic_broom', 'ladder_sectioned_01',
    'metal_office_desk', 'television_02', 'metal_stool_03', 'power_box_01', 'utility_box_01',
    'modular_airduct_rectangular_01', 'vintage_flashlight', 'clipboard',
]
SURFACES = {
    'tiled_floor_001': '2k', 'smooth_concrete_floor': '1k', 'white_plaster_02': '1k',
    'concrete_block_wall_02': '1k', 'ceiling_interior': '1k', 'blue_metal_plate': '1k',
'brushed_concrete': '1k', 'wood_table_001': '1k',
    'rusty_metal_shutter': '1k', 'concrete_wall_004': '1k',
}
AVATARS = {
    'Guard': 'Avatars/Professions/Security_Male_01',
    'MannequinMale': 'Avatars/Adults/Male_Adult_08',
    'MannequinFemale': 'Avatars/Adults/Female_Adult_01',
}
STATIC = 'Animations/all_animations_max_motextr_static/'
MOVING = 'Animations/all_animations_max_motextr_xy/'
ANIMATIONS = [
    STATIC + 'm_idle_neutral_01.max.fbx', STATIC + 'm_idle_look_around_01.max.fbx',
    STATIC + 'f_idle_neutral_01.max.fbx',
    MOVING + 'm_walk_neutral_01.max.fbx', MOVING + 'm_walk_slow_01.max.fbx', MOVING + 'm_run_neutral_01.max.fbx',
    MOVING + 'f_walk_neutral_01.max.fbx', MOVING + 'f_run_neutral_01.max.fbx',
]

def download(url, timeout=120):
    for attempt in range(5):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=AGENT), timeout=timeout) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            if error.code < 500 or attempt == 4:
                raise
        except (urllib.error.URLError, http.client.HTTPException, TimeoutError):
            if attempt == 4:
                raise
        time.sleep(2 + attempt * 3)

def fetch(url, target):
    if target.exists() and target.stat().st_size > 0:
        return False
    target.parent.mkdir(parents=True, exist_ok=True)
    data = download(url)
    if data[:15].lower().startswith(b'<!doctype html') or data[:6].lower() == b'<html>':
        raise ValueError('HTML instead of an asset: ' + url)
    target.write_bytes(data)
    return True

def files(asset):
    return json.loads(download(POLYHAVEN + asset, 60))

def jpg(info, key, size):
    try:
        return info[key][size]['jpg']['url']
    except KeyError:
        return None

def fetch_any(urls, target):
    """Some Poly Haven 1k files are broken on the CDN; the 2k file is the same asset."""
    for index, url in enumerate(urls):
        try:
            return fetch(url, target)
        except (OSError, http.client.HTTPException):
            if index == len(urls) - 1:
                raise

def prop(asset):
    info = files(asset)
    folder = ART / 'Props' / asset
    fetch_any([info['fbx'][size]['fbx']['url'] for size in ('1k', '2k')], folder / f'{asset}_1k.fbx')
    maps = {'Diffuse': 'diff', 'nor_gl': 'nor_gl'}
    for key in info:
        if key.endswith('_diff'):
            maps[key] = f'{key[:-5]}_diff'
        elif key.endswith('_nor_gl'):
            maps[key] = f'{key[:-7]}_nor_gl'
    for key, suffix in maps.items():
        urls = [url for url in (jpg(info, key, '1k'), jpg(info, key, '2k')) if url]
        if urls:
            fetch_any(urls, folder / 'textures' / f'{asset}_{suffix}_1k.jpg')

def surface(asset, size):
    info = files(asset)
    folder = ART / 'Surfaces' / asset
    for key, suffix in (('Diffuse', 'diff'), ('nor_gl', 'nor_gl')):
        url = jpg(info, key, size)
        if url:
            fetch(url, folder / f'{asset}_{suffix}.jpg')

def tga_to_jpg(source, target):
    subprocess.run(['sips', '-s', 'format', 'jpeg', '-s', 'formatOptions', '88', str(source), '--out', str(target)],
                   check=True, stdout=subprocess.DEVNULL)

def avatar(name, path):
    folder = ART / 'Characters' / name
    avatar_id = path.rsplit('/', 1)[1]
    fetch(ROCKETBOX + f'{path}/Export/{avatar_id}.fbx', folder / f'{name}.fbx')
    listing = f'https://api.github.com/repos/microsoft/Microsoft-Rocketbox/contents/Assets/{path}/Textures'
    textures = [entry['name'] for entry in json.loads(download(listing, 60))]
    with tempfile.TemporaryDirectory() as scratch:
        for texture in textures:
            stem = Path(texture).stem
            if not (stem.endswith('_color') or stem.endswith('_normal')):
                continue
            target = folder / 'textures' / (stem + '.jpg')
            if target.exists():
                continue
            raw = Path(scratch) / texture
            fetch(ROCKETBOX + f'{path}/Textures/{texture}', raw)
            target.parent.mkdir(parents=True, exist_ok=True)
            tga_to_jpg(raw, target)

def animation(path):
    name = path.rsplit('/', 1)[1].replace('.max.fbx', '.fbx')
    try:
        fetch(ROCKETBOX + path, ART / 'Characters' / 'Animations' / name)
    except urllib.error.HTTPError as error:
        print('  skipped', name, error.code)

def main():
    if shutil.which('sips') is None:
        sys.exit('sips (macOS) is required to convert Rocketbox TGA textures.')
    jobs = [('prop ' + a, lambda a=a: prop(a)) for a in PROPS]
    jobs += [('surface ' + a, lambda a=a, s=s: surface(a, s)) for a, s in SURFACES.items()]
    jobs += [('avatar ' + n, lambda n=n, p=p: avatar(n, p)) for n, p in AVATARS.items()]
    jobs += [('animation ' + p.rsplit('/', 1)[1], lambda p=p: animation(p)) for p in ANIMATIONS]
    failed = []
    for label, job in jobs:
        print(label, flush=True)
        try:
            job()
        except (OSError, KeyError, ValueError, http.client.HTTPException, subprocess.CalledProcessError) as error:
            print('  FAILED', error, flush=True)
            failed.append(label)
    total = sum(f.stat().st_size for f in ART.rglob('*') if f.is_file() and f.suffix != '.meta')
    print(f'Art in {ART} ({total / 1e6:.0f} MB). Open Unity or run a Unity command to import it.')
    if failed:
        print('Failed:', ', '.join(failed))
        return 1
    return 0

if __name__ == '__main__':
    sys.exit(main())
