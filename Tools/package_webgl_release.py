"""Package the WebGL player with index.html at the ZIP root for itch.io."""
from pathlib import Path
import hashlib
import gzip
import json
import struct
import zipfile

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Builds/WebGL"
DEST = ROOT / "Builds/Submission/PrimitiveLogistics_HTML5.zip"
assert (SOURCE / "index.html").is_file(), "Build WebGL first."
files = [p for p in sorted(SOURCE.rglob("*")) if p.is_file()
         and p.suffix.lower() not in {".meta", ".pdb", ".map"}
         and not any("DontShip" in part or "BurstDebugInformation" in part for part in p.parts)]
assert any(".wasm" in p.name for p in files), "WebAssembly player missing."
data = gzip.decompress((SOURCE / "Build/WebGL.data.unityweb").read_bytes())
assert data.startswith(b"UnityWebData1.0\0")
header_end = struct.unpack_from("<I", data, 16)[0]
position = 20
virtual_files = []
while position < header_end:
    offset, size, length = struct.unpack_from("<III", data, position)
    position += 12
    virtual_files.append(data[position:position + length].decode("utf-8"))
    position += length
assert any(name.endswith("data.unity3d") or name == "level0" for name in virtual_files), "Scene data missing from Unity export; rebuild with CleanBuildCache."
assert len(files) < 1000
assert sum(p.stat().st_size for p in files) < 500_000_000
assert all(p.stat().st_size < 200_000_000 for p in files)
assert all(len(p.relative_to(SOURCE).as_posix()) <= 240 for p in files)
DEST.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(DEST, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for path in files:
        archive.write(path, path.relative_to(SOURCE).as_posix())
    for name, doc in [("README.txt", "ReleaseReadmeWebGL.txt"), ("CONTROLS.txt", "ReleaseControls.txt"), ("CREDITS.txt", "Credits.txt")]:
        archive.writestr(name, (ROOT / "Documentation" / doc).read_bytes())
with zipfile.ZipFile(DEST) as archive:
    assert "index.html" in archive.namelist()
    assert archive.testzip() is None
    manifest = [{"path": i.filename, "bytes": i.file_size} for i in archive.infolist()]
report = {"archive": str(DEST), "bytes": DEST.stat().st_size,
          "sha256": hashlib.sha256(DEST.read_bytes()).hexdigest(), "files": manifest}
(ROOT / "Logs/WebGL/Package.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"PASS: {DEST}\n{len(manifest)} files, {DEST.stat().st_size/1024/1024:.2f} MiB; index.html at root; ZIP integrity verified.")
