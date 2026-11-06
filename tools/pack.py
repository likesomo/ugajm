"""Bundle the web build into dist/lucky-alchemist.zip for itch.io (HTML game, index.html at zip root)."""
import pathlib
import zipfile

root = pathlib.Path(__file__).resolve().parent.parent
out = root / "dist" / "lucky-alchemist.zip"
out.parent.mkdir(exist_ok=True)
files = [root / "index.html", root / "style.css", *sorted((root / "src").glob("*.js"))]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for f in files:
        z.write(f, f.relative_to(root).as_posix())
print(f"{out} ({out.stat().st_size // 1024} KB, {len(files)} files)")
