#!/usr/bin/env python3
"""
Create .meta files for all directories under Assets/Scripts that are missing them.
For each, try to copy from user project first, then generate fresh.
"""
import os
import uuid
import shutil

WEBAPP   = "/home/user/webapp"
USERPROJ = "/home/user/user_unity_project/Kotor-Unity"

FOLDER_META_TEMPLATE = """\
fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

copied = generated = already_ok = 0

for root, dirs, files in os.walk(os.path.join(WEBAPP, "Assets")):
    dirs.sort()
    # Also check root itself
    meta_path = root + ".meta"
    if os.path.exists(meta_path):
        already_ok += 1
        continue
    
    rel = os.path.relpath(root, WEBAPP)
    user_meta = os.path.join(USERPROJ, rel + ".meta")
    
    if os.path.exists(user_meta):
        shutil.copy2(user_meta, meta_path)
        g = [l.split()[-1] for l in open(meta_path) if "guid:" in l]
        print(f"  COPIED  {rel}  guid={g[0] if g else '?'}")
        copied += 1
    else:
        g = uuid.uuid4().hex
        with open(meta_path, "w") as f:
            f.write(FOLDER_META_TEMPLATE.format(guid=g))
        print(f"  GENED   {rel}  guid={g}")
        generated += 1

print(f"\nDone: {copied} copied, {generated} generated, {already_ok} already present")
