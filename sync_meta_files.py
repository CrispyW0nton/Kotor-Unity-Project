#!/usr/bin/env python3
"""
Sync .meta files from user project to webapp.
For any .cs in webapp that lacks a .meta:
  1. Try to copy from user_unity_project/Kotor-Unity
  2. If not found there, generate a fresh one with uuidgen
"""
import os
import subprocess
import shutil

WEBAPP   = "/home/user/webapp"
USERPROJ = "/home/user/user_unity_project/Kotor-Unity"

META_TEMPLATE = """\
fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def gen_guid():
    result = subprocess.run(
        "uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]'",
        shell=True, capture_output=True, text=True
    )
    return result.stdout.strip()

copied = 0
generated = 0
already_ok = 0

for root, dirs, files in os.walk(os.path.join(WEBAPP, "Assets/Scripts")):
    dirs.sort()
    for fname in sorted(files):
        if not fname.endswith(".cs"):
            continue
        cs_path = os.path.join(root, fname)
        meta_path = cs_path + ".meta"
        
        if os.path.exists(meta_path):
            already_ok += 1
            continue
        
        # Compute relative path to look up in user project
        rel = os.path.relpath(cs_path, WEBAPP)
        user_meta = os.path.join(USERPROJ, rel + ".meta")
        
        if os.path.exists(user_meta):
            shutil.copy2(user_meta, meta_path)
            guid_line = [l for l in open(meta_path) if "guid:" in l]
            guid = guid_line[0].split()[-1] if guid_line else "?"
            print(f"  COPIED  {rel}  guid={guid}")
            copied += 1
        else:
            # Generate fresh meta with new GUID
            guid = gen_guid()
            with open(meta_path, "w") as f:
                f.write(META_TEMPLATE.format(guid=guid))
            print(f"  GENED   {rel}  guid={guid}")
            generated += 1

print(f"\nDone: {copied} copied, {generated} generated, {already_ok} already present")
