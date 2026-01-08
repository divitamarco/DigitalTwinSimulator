from flask import Flask, request, jsonify
import os
import subprocess
from sam_utils import load_sam, segment_image_auto
import sys
import uuid

# Flask application instance
app = Flask(__name__)

# Folder used to store uploaded images, masks and generated models
UPLOAD_FOLDER = "static"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

# Base paths
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SAM_MODEL_PATH = os.path.join(BASE_DIR, "models", "sam_vit_h_4b8939.pth")
STABLE3D_DIR = os.path.join(BASE_DIR, "..", "stable-fast-3d")

# Load SAM model once at startup to avoid repeated initialization cost
print("[INFO] Loading SAM model...")
sam_model = load_sam(SAM_MODEL_PATH)
print("[INFO] SAM loaded.")

# Image segmentation endpoint (SAM)
@app.route("/segment", methods=["POST"])
def segment():
    if "image" not in request.files:
        return jsonify({"error": "No image uploaded"}), 400

    image_file = request.files["image"]
    image_path = os.path.join(UPLOAD_FOLDER, image_file.filename)
    image_file.save(image_path)

    # Output files produced by SAM
    output_mask_path = os.path.join(UPLOAD_FOLDER, "mask_" + image_file.filename)
    output_png_path = os.path.join(
        UPLOAD_FOLDER,
        "cutout_" + os.path.splitext(image_file.filename)[0] + ".png"
    )
    output_preview_path = os.path.join(
        UPLOAD_FOLDER,
        "preview_" + os.path.splitext(image_file.filename)[0] + ".jpg"
    )

    # Automatic segmentation using SAM
    segment_image_auto(
        sam_model,
        image_path,
        output_mask_path,
        transparent_output_path=output_png_path,
        preview_output_path=output_preview_path
    )

    # Paths are returned as static URLs for Unity consumption
    return jsonify({
        "mask_path": f"/static/mask_{image_file.filename}",
        "cutout_path": f"/static/cutout_{os.path.splitext(image_file.filename)[0]}.png",
        "preview_path": f"/static/preview_{os.path.splitext(image_file.filename)[0]}.jpg"
    })

# 3D generation endpoint (Stable Fast 3D)
@app.route("/generate3d", methods=["POST"])
def generate3d():
    data = request.get_json(silent=True) or {}
    img_arg = (data.get("image_path") or "").strip()

    if not img_arg:
        return jsonify({"status": "error", "error": "Missing image_path"}), 400

    base_dir = BASE_DIR
    upload_folder = "static"

    # Helper to resolve relative paths safely
    def abs_if_exists(relpath):
        p = os.path.abspath(os.path.join(base_dir, relpath))
        return p if os.path.exists(p) else None

    # Flexible image resolution:
    # - absolute static path
    # - filename inside static/
    # - fallback to test_images/
    image_abs = None
    if img_arg.startswith("/static/"):
        image_abs = abs_if_exists(img_arg.lstrip("/"))
    if image_abs is None and os.path.basename(img_arg) == img_arg:
        image_abs = abs_if_exists(os.path.join(upload_folder, img_arg))
    if image_abs is None:
        image_abs = abs_if_exists(os.path.join("test_images", os.path.basename(img_arg)))

    if image_abs is None:
        return jsonify({
            "status": "error",
            "error": f"Image not found: {img_arg}"
        }), 404

    # Unique job directory to avoid collisions
    stem = os.path.splitext(os.path.basename(image_abs))[0]
    job_dir = os.path.abspath(os.path.join(
        base_dir, upload_folder, f"{stem}_3d_{uuid.uuid4().hex[:6]}"
    ))
    os.makedirs(job_dir, exist_ok=True)

    # Stable Fast 3D invocation
    run_py = os.path.join(STABLE3D_DIR, "run.py")
    cmd = [
        sys.executable,
        run_py,
        image_abs,
        "--output-dir",
        job_dir,
        "--device",
        "cpu"
    ]

    try:
        proc = subprocess.run(
            cmd,
            cwd=STABLE3D_DIR,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=1800
        )
    except subprocess.TimeoutExpired as te:
        return jsonify({"status": "error", "error": f"Timeout: {te}"}), 500
    except Exception as e:
        return jsonify({"status": "error", "error": f"Subprocess error: {e}"}), 500

    if proc.returncode != 0:
        return jsonify({
            "status": "error",
            "error": "3D generation failed",
            "stdout": proc.stdout[-2000:],
            "stderr": proc.stderr[-2000:]
        }), 500

    # Search for the generated GLB file
    glb_path = None
    for root, _, files in os.walk(job_dir):
        for f in files:
            if f.lower().endswith(".glb"):
                glb_path = os.path.join(root, f)
                break
        if glb_path:
            break

    if not glb_path:
        return jsonify({
            "status": "error",
            "error": "3D model not generated",
            "stdout": proc.stdout[-2000:],
            "stderr": proc.stderr[-2000:]
        }), 500

    # Move final GLB into static/ for direct access by Unity
    final_glb = os.path.abspath(os.path.join(base_dir, upload_folder, f"{stem}.glb"))
    try:
        if os.path.exists(final_glb):
            os.remove(final_glb)
        os.replace(glb_path, final_glb)
    except Exception:
        final_glb = glb_path

    # Absolute URL returned to Unity
    rel_path = "/" + os.path.relpath(final_glb, base_dir).replace("\\", "/")
    model_url = request.host_url.rstrip("/") + rel_path

    return jsonify({
        "status": "ok",
        "model_url": model_url,
        "model_path": rel_path
    })

# Application entry point
if __name__ == "__main__":
    app.run(debug=True)
