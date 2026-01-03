from flask import Flask, request, jsonify, send_from_directory
import os
import sys
import uuid
import shutil
import subprocess
from pathlib import Path
from urllib.parse import urlparse

from sam_utils import load_sam, segment_image_auto

# =========================================================
# CONFIG
# =========================================================

app = Flask(__name__)

BASE_DIR = Path(__file__).resolve().parent

SAM_DIR = BASE_DIR / "sam"
THREED_DIR = BASE_DIR / "3d"

SAM_DIR.mkdir(exist_ok=True)
THREED_DIR.mkdir(exist_ok=True)

SAM_MODEL_PATH = BASE_DIR / "models" / "sam_vit_h_4b8939.pth"
STABLE3D_DIR = (BASE_DIR / ".." / "stable-fast-3d").resolve()

# Python del venv di stable-fast-3d
PY_3D = STABLE3D_DIR / ".venv" / "Scripts" / "python.exe"   # Windows
if not PY_3D.exists():
    PY_3D = STABLE3D_DIR / ".venv" / "bin" / "python"       # Linux / macOS

if not PY_3D.exists():
    raise RuntimeError("❌ Python venv di stable-fast-3d NON trovato")

# =========================================================
# LOAD SAM
# =========================================================

print("[INFO] Loading SAM...")
sam_model = load_sam(str(SAM_MODEL_PATH))
print("[INFO] SAM loaded.")

# =========================================================
# STATIC SERVING
# =========================================================

@app.route("/sam/<path:filename>")
def serve_sam(filename):
    return send_from_directory(SAM_DIR, filename)


@app.route("/3d/<path:filename>")
def serve_3d(filename):
    return send_from_directory(THREED_DIR, filename)

# =========================================================
# UTILS
# =========================================================

def resolve_image_path(img_input: str) -> Path | None:
    """
    Accetta:
    - sam/xxx/file.png
    - /sam/xxx/file.png
    - cutout_x.png
    - URL http://localhost:5000/sam/...
    """
    if img_input.startswith("http"):
        img_input = urlparse(img_input).path

    img_input = img_input.lstrip("/")

    # tentativo diretto
    p = (BASE_DIR / img_input).resolve()
    if p.exists():
        return p

    # fallback: cerca dentro sam/*
    for f in SAM_DIR.rglob(Path(img_input).name):
        return f.resolve()

    return None

# =========================================================
# SEGMENTATION ENDPOINT
# =========================================================

@app.route("/segment", methods=["POST"])
def segment():
    if "image" not in request.files:
        return jsonify({"error": "No image uploaded"}), 400

    image_file = request.files["image"]
    stem = Path(image_file.filename).stem
    job_id = f"{stem}_{uuid.uuid4().hex[:6]}"

    job_dir = SAM_DIR / job_id
    job_dir.mkdir(parents=True, exist_ok=True)

    image_path = job_dir / image_file.filename
    image_file.save(image_path)

    mask_path = job_dir / f"mask_{image_file.filename}"
    cutout_path = job_dir / f"cutout_{stem}.png"
    preview_path = job_dir / f"preview_{stem}.jpg"

    segment_image_auto(
        sam_model,
        str(image_path),
        str(mask_path),
        transparent_output_path=str(cutout_path),
        preview_output_path=str(preview_path)
    )

    base_url = request.host_url.rstrip("/")

    return jsonify({
        "status": "ok",
        "job_id": job_id,
        "cutout_path": f"sam/{job_id}/cutout_{stem}.png",
        "cutout_url": f"{base_url}/sam/{job_id}/cutout_{stem}.png"
    })

# =========================================================
# 3D GENERATION ENDPOINT
# =========================================================

@app.route("/generate3d", methods=["POST"])
def generate3d():
    data = request.get_json(silent=True) or {}
    img_input = (data.get("image_path") or "").strip()

    if not img_input:
        return jsonify({"status": "error", "error": "Missing image_path"}), 400

    image_abs = resolve_image_path(img_input)

    if image_abs is None:
        return jsonify({"status": "error", "error": "Image not found"}), 404

    stem = image_abs.stem
    job_id = f"{stem}_{uuid.uuid4().hex[:6]}"

    job_dir = THREED_DIR / job_id
    job_dir.mkdir(parents=True, exist_ok=True)

    # Copia input nella cartella 3D
    input_copy = job_dir / image_abs.name
    shutil.copy(image_abs, input_copy)

    run_py = STABLE3D_DIR / "run.py"

    cmd = [
        str(PY_3D),
        str(run_py),
        str(input_copy),
        "--output-dir", str(job_dir),
        "--device", "cpu"
    ]

    print("[Stable3D] CMD:", " ".join(cmd))

    proc = subprocess.run(
        cmd,
        cwd=str(STABLE3D_DIR),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True
    )

    if proc.returncode != 0:
        return jsonify({
            "status": "error",
            "error": "Stable Fast 3D failed",
            "stdout": proc.stdout,
            "stderr": proc.stderr
        }), 500

    # Cerca GLB ricorsivamente
    glb_path = None
    for f in job_dir.rglob("*.glb"):
        glb_path = f
        break

    if not glb_path:
        return jsonify({
            "status": "error",
            "error": "GLB not generated",
            "stdout": proc.stdout,
            "stderr": proc.stderr
        }), 500

    final_glb = job_dir / "model.glb"
    if glb_path != final_glb:
        glb_path.replace(final_glb)

    rel_path = "/" + str(final_glb.relative_to(BASE_DIR)).replace("\\", "/")
    model_url = request.host_url.rstrip("/") + rel_path

    return jsonify({
        "status": "ok",
        "job_id": job_id,
        "model_url": model_url,
        "model_path": rel_path
    })

# =========================================================
# MAIN
# =========================================================

if __name__ == "__main__":
    app.run(debug=True)
