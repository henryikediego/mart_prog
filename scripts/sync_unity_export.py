#!/usr/bin/env python3
"""Sync a Unity Android export into the Loci React Native host app."""

from __future__ import annotations

import argparse
import os
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


ANDROID_NS = "http://schemas.android.com/apk/res/android"
ET.register_namespace("android", ANDROID_NS)
ET.register_namespace("tools", "http://schemas.android.com/tools")


class SyncError(Exception):
    pass


def require_file(path: Path, description: str) -> str:
    if not path.is_file():
        raise SyncError(f"Missing {description}: {path}")
    return path.read_text(encoding="utf-8-sig")


def parse_properties(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in require_file(path, "Gradle properties file").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith(("#", "!")) or "=" not in stripped:
            continue
        key, value = stripped.split("=", 1)
        values[key.strip()] = value.strip()
    return values


def launcher_filter_count(manifest: Path) -> tuple[ET.ElementTree, ET.Element, list[ET.Element]]:
    try:
        tree = ET.parse(manifest)
    except (ET.ParseError, OSError) as exc:
        raise SyncError(f"Cannot read Unity library manifest {manifest}: {exc}") from exc
    root = tree.getroot()
    app = root.find("application")
    if app is None:
        raise SyncError(f"Unity library manifest has no <application>: {manifest}")

    matches = []
    for activity in app.findall("activity") + app.findall("activity-alias"):
        for intent_filter in activity.findall("intent-filter"):
            actions = {node.get(f"{{{ANDROID_NS}}}name") for node in intent_filter.findall("action")}
            categories = {node.get(f"{{{ANDROID_NS}}}name") for node in intent_filter.findall("category")}
            if "android.intent.action.MAIN" in actions and "android.intent.category.LAUNCHER" in categories:
                matches.append(intent_filter)
    return tree, app, matches


def validate(export_root: Path, app_root: Path) -> tuple[dict[str, str], Path]:
    unity_library = export_root / "unityLibrary"
    shared = export_root / "shared"
    if not unity_library.is_dir():
        raise SyncError(f"Unity export must contain a unityLibrary folder: {unity_library}")
    if not shared.is_dir():
        raise SyncError(f"Unity export must contain a shared folder: {shared}")

    export_properties_path = export_root / "gradle.properties"
    export_properties = parse_properties(export_properties_path)
    manifest = unity_library / "src/main/AndroidManifest.xml"
    _, _, filters = launcher_filter_count(manifest)
    if len(filters) != 1:
        raise SyncError(
            "Expected exactly one Unity MAIN/LAUNCHER intent-filter in "
            f"{manifest}, found {len(filters)}. No files were changed."
        )

    android = app_root / "android"
    settings = require_file(android / "settings.gradle", "React Native Android settings.gradle")
    if "include ':unityLibrary'" not in settings or "include ':unityLibrary:xrmanifest.androidlib'" not in settings:
        raise SyncError("android/settings.gradle is missing the Unity module includes from the handoff")
    if "../unity/builds/android/unityLibrary" not in settings:
        raise SyncError("android/settings.gradle does not point unityLibrary at unity/builds/android")
    if "../unity/builds/android/unityLibrary/xrmanifest.androidlib" not in settings:
        raise SyncError("android/settings.gradle does not point xrmanifest.androidlib at the Unity export")

    root_gradle = require_file(android / "build.gradle", "React Native root build.gradle")
    if "flatDir" not in root_gradle or "${project(':unityLibrary').projectDir}/libs" not in root_gradle:
        raise SyncError("android/build.gradle is missing the Unity library flatDir repository")

    app_gradle = require_file(android / "app/build.gradle", "React Native app build.gradle")
    if "minSdkVersion 25" not in app_gradle:
        raise SyncError("android/app/build.gradle must set minSdkVersion 25")

    app_manifest = require_file(android / "app/src/main/AndroidManifest.xml", "React Native app manifest")
    if "android.permission.HIGH_SAMPLING_RATE_SENSORS" not in app_manifest:
        raise SyncError("React Native app manifest is missing HIGH_SAMPLING_RATE_SENSORS")
    strings = require_file(android / "app/src/main/res/values/strings.xml", "React Native strings.xml")
    if 'name="game_view_content_description"' not in strings:
        raise SyncError("strings.xml is missing game_view_content_description")

    package = require_file(app_root / "package.json", "React Native package.json")
    if '"postinstall": "patch-package"' not in package:
        raise SyncError("package.json is missing the patch-package postinstall script")
    patch = app_root / "patches/@azesmway+react-native-unity+1.1.1.patch"
    patch_text = require_file(patch, "react-native-unity 1.1.1 patch")
    if "mavenCentral()" not in patch_text or "jcenter()" not in patch_text:
        raise SyncError("The react-native-unity patch must replace jcenter() with mavenCentral()")

    return export_properties, manifest


def write_host_properties(path: Path, export_properties: dict[str, str]) -> None:
    original = path.read_text(encoding="utf-8-sig")
    kept = []
    for line in original.splitlines():
        stripped = line.strip()
        key = stripped.split("=", 1)[0].strip() if "=" in stripped and not stripped.startswith(("#", "!")) else ""
        if key.startswith("unity"):
            continue
        if key == "unityStreamingAssets":
            continue
        kept.append(line)

    additions = ["unityStreamingAssets=.unity3d", "react.newArchEnabled=true"]
    additions.extend(f"{key}={value}" for key, value in export_properties.items() if key.startswith("unity") and key != "unityStreamingAssets")
    while kept and not kept[-1].strip():
        kept.pop()
    path.write_text("\n".join([*kept, "", *additions, ""]), encoding="utf-8")


def sync_directories(export_root: Path, destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    names = ("unityLibrary", "shared")
    staging = Path(tempfile.mkdtemp(prefix=".loci-unity-sync-", dir=destination))
    backups: dict[str, Path] = {}
    installed: list[str] = []
    try:
        for name in names:
            shutil.copytree(export_root / name, staging / name, symlinks=True)

        target_manifest = staging / "unityLibrary/src/main/AndroidManifest.xml"
        tree, app, filters = launcher_filter_count(target_manifest)
        if len(filters) != 1:
            raise SyncError(f"Staged manifest changed unexpectedly; found {len(filters)} Unity launcher filters")
        for intent_filter in filters:
            for parent in app.iter():
                if intent_filter in list(parent):
                    parent.remove(intent_filter)
                    break
        tree.write(target_manifest, encoding="utf-8", xml_declaration=True)

        for name in names:
            target = destination / name
            if target.exists():
                backup = staging / f"{name}.backup"
                os.replace(target, backup)
                backups[name] = backup
            os.replace(staging / name, target)
            installed.append(name)
    except Exception:
        for name in reversed(installed):
            target = destination / name
            if target.exists():
                shutil.rmtree(target)
        for name, backup in backups.items():
            if backup.exists():
                os.replace(backup, destination / name)
        raise
    finally:
        shutil.rmtree(staging, ignore_errors=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-root", type=Path, required=True, help="Unity Android export folder")
    parser.add_argument("--app-root", type=Path, required=True, help="Loci React Native app root")
    parser.add_argument("--dry-run", action="store_true", help="Validate inputs and show planned changes without writing")
    args = parser.parse_args()
    export_root = args.export_root.expanduser().resolve()
    app_root = args.app_root.expanduser().resolve()
    try:
        export_properties, _ = validate(export_root, app_root)
        destination = app_root / "unity/builds/android"
        properties_path = app_root / "android/gradle.properties"
        if not properties_path.is_file():
            raise SyncError(f"Missing React Native Android gradle.properties: {properties_path}")
        unity_keys = [key for key in export_properties if key.startswith("unity") and key != "unityStreamingAssets"]
        if args.dry_run:
            print("Validation succeeded. No files were changed.")
            print(f"Would replace: {destination}/unityLibrary and {destination}/shared")
            print(f"Would update: {properties_path} ({len(unity_keys)} Unity properties from export)")
            print("Would remove the single Unity MAIN/LAUNCHER filter from the copied library manifest.")
            return 0

        sync_directories(export_root, destination)
        write_host_properties(properties_path, export_properties)
        print("Unity export sync complete.")
        print(f"Copied unityLibrary and shared to {destination}")
        print(f"Updated Unity Gradle properties in {properties_path}")
        print("Removed the Unity launcher filter from the copied manifest; the React Native app remains the launcher.")
        return 0
    except (SyncError, OSError) as exc:
        print(f"Unity export sync failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
