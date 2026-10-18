"""Package the verified Windows release, without project sources or reference files."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Builds/ReleaseWindows"
DEST = ROOT / "Builds/Submission/PrimitiveLogistics_Windows_x64.zip"
PREFIX = "PrimitiveLogistics"

runtime_files = {"PrimitiveLogistics.exe", "UnityPlayer.dll", "UnityCrashHandler64.exe", "GameAssembly.dll", "baselib.dll", "WinPixEventRuntime.dll"}
runtime_dirs = {"PrimitiveLogistics_Data", "MonoBleedingEdge", "D3D12"}
excluded_suffixes = {".pdb", ".mdb", ".map", ".meta", ".cs", ".psd", ".blend"}
required = ["PrimitiveLogistics.exe", "UnityPlayer.dll", "PrimitiveLogistics_Data/globalgamemanagers"]
for relative in required:
    if not (SOURCE / relative).is_file():
        raise RuntimeError(f"Required release file missing: {relative}")

files = []
for path in sorted(SOURCE.rglob("*")):
    if not path.is_file():
        continue
    relative = path.relative_to(SOURCE)
    if relative.parts[0] not in runtime_dirs and str(relative) not in runtime_files:
        continue
    if path.suffix.lower() in excluded_suffixes:
        continue
    if any("DontShip" in part or "BurstDebugInformation" in part for part in relative.parts):
        continue
    files.append((relative.as_posix(), path.read_bytes()))

submission = (ROOT / "Documentation/Submission.md").read_text(encoding="utf-8")
description = submission.split("## Game description\n", 1)[1].split("## Controls", 1)[0].strip()
description = "PRIMITIVE LOGISTICS\n\n" + description.replace("**", "") + "\n"
files.extend([
    ("README.txt", (ROOT / "Documentation/ReleaseReadme.txt").read_bytes()),
    ("CONTROLS.txt", (ROOT / "Documentation/ReleaseControls.txt").read_bytes()),
    ("GAME_DESCRIPTION.txt", description.encode("utf-8")),
    ("CREDITS.txt", (ROOT / "Documentation/Credits.txt").read_bytes()),
])

DEST.parent.mkdir(parents=True, exist_ok=True)
manifest = []
with zipfile.ZipFile(DEST, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for relative, data in files:
        name = f"{PREFIX}/{relative}"
        archive.writestr(name, data)
        manifest.append({"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
with zipfile.ZipFile(DEST) as archive:
    failed = archive.testzip()
    if failed:
        raise RuntimeError(f"ZIP integrity check failed: {failed}")
    for name in archive.namelist():
        lowered = name.lower()
        if "artreferences" in lowered or "backups" in lowered or "dontship" in lowered:
            raise RuntimeError(f"Unneeded source/reference material included: {name}")

report = {"archive": str(DEST), "bytes": DEST.stat().st_size, "sha256": hashlib.sha256(DEST.read_bytes()).hexdigest(), "files": manifest}
log = ROOT / "Logs/FinalSubmission/PackageManifest.json"
log.parent.mkdir(parents=True, exist_ok=True)
log.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"PASS: {DEST}\n{len(files)} files, {DEST.stat().st_size / 1024 / 1024:.2f} MiB; ZIP integrity verified.")
