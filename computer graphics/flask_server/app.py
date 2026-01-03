from flask import Flask, request, jsonify, send_from_directory
import os
import subprocess
import sys
import uuid
import shutil

from sam_utils import load_sam, segment_image_auto

# =========================================================
# CONFIG
# =========================================================

app = Flask(__name__)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

SAM_BASE_DIR = os.path.join(BASE_DIR, "sam")
THREED_BASE_DIR = os.path.join(BASE_DIR, "3d")

os.makedirs(SAM_BASE_DIR, exist_ok=True)
os.makedirs(THREED_BASE_DIR, exist_ok=True)

SAM_MODEL_PATH = os.path.join(BASE_DIR, "models", "sam_vit_h_4b8939.pth")
STABLE3D_DIR = os.path.join(BASE_DIR, "..", "stable-fast-3d")

# =========================================================
# LOAD SAM
# =========================================================

print("[INFO] Loading SAM...")
sam_model = load_sam(SAM_MODEL_PATH)
print("[INFO] SAM loaded.")

# =========================================================
# STATIC SERVING
# =========================================================

@app.route("/sam/<path:filename>")
def serve_sam(filename):
    return send_from_directory(SAM_BASE_DIR, filename)


@app.route("/3d/<path:filename>")
def serve_3d(filename):
    return send_from_directory(THREED_BASE_DIR, filename)

# =========================================================
# SEGMENTATION ENDPOINT
# =========================================================

@app.route("/segment", methods=["POST"])
def segment():
    if "image" not in request.files:
        return jsonify({"error": "No image uploaded"}), 400

    image_file = request.files["image"]
    stem = os.path.splitext(image_file.filename)[0]
    job_id = f"{stem}_{uuid.uuid4().hex[:6]}"

    sam_job_dir = os.path.join(SAM_BASE_DIR, job_id)
    os.makedirs(sam_job_dir, exist_ok=True)

    # Save original image
    image_path = os.path.join(sam_job_dir, image_file.filename)
    image_file.save(image_path)

    # Output paths
    output_mask_path = os.path.join(sam_job_dir, f"mask_{image_file.filename}")
    output_png_path = os.path.join(sam_job_dir, f"cutout_{stem}.png")
    output_preview_path = os.path.join(sam_job_dir, f"preview_{stem}.jpg")

    # Run SAM
    segment_image_auto(
        sam_model,
        image_path,
        output_mask_path,
        transparent_output_path=output_png_path,
        preview_output_path=output_preview_path
    )

    base_url = request.host_url.rstrip("/")

    return jsonify({
        "status": "ok",
        "job_id": job_id,
        "sam_dir": f"/sam/{job_id}",
        "cutout_path": f"sam/{job_id}/cutout_{stem}.png",
        "cutout_url": f"{base_url}/sam/{job_id}/cutout_{stem}.png"
    })

# =========================================================
# 3D GENERATION ENDPOINT
# =========================================================

@app.route("/generate3d", methods=["POST"])
def generate3d():
    data = request.get_json(silent=True) or {}
    img_path = (data.get("image_path") or "").strip()

    if not img_path:
        return jsonify({"status": "error", "error": "Missing image_path"}), 400

    image_abs = os.path.abspath(os.path.join(BASE_DIR, img_path))
    if not os.path.exists(image_abs):
        return jsonify({"status": "error", "error": f"Image not found: {img_path}"}), 404

    stem = os.path.splitext(os.path.basename(image_abs))[0]
    job_id = f"{stem}_{uuid.uuid4().hex[:6]}"

    job_3d_dir = os.path.join(THREED_BASE_DIR, job_id)
    os.makedirs(job_3d_dir, exist_ok=True)

    # Copy input image into 3d job folder
    input_copy_path = os.path.join(job_3d_dir, os.path.basename(image_abs))
    shutil.copy(image_abs, input_copy_path)

    run_py = os.path.join(STABLE3D_DIR, "run.py")

    cmd = [
        sys.executable,
        run_py,
        input_copy_path,
        "--output-dir",
        job_3d_dir,
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
    except subprocess.TimeoutExpired:
        return jsonify({"status": "error", "error": "Stable Fast 3D timeout"}), 500

    if proc.returncode != 0:
        return jsonify({
            "status": "error",
            "error": "3D generation failed",
            "stdout": proc.stdout[-2000:],
            "stderr": proc.stderr[-2000:]
        }), 500

    # Find GLB
    glb_path = None
    for root, _, files in os.walk(job_3d_dir):
        for f in files:
            if f.lower().endswith(".glb"):
                glb_path = os.path.join(root, f)
                break
        if glb_path:
            break

    if not glb_path:
        return jsonify({"status": "error", "error": "GLB not generated"}), 500

    final_glb = os.path.join(job_3d_dir, "model.glb")
    if glb_path != final_glb:
        os.replace(glb_path, final_glb)

    base_url = request.host_url.rstrip("/")
    rel_path = "/" + os.path.relpath(final_glb, BASE_DIR).replace("\\", "/")

    return jsonify({
        "status": "ok",
        "job_id": job_id,
        "model_url": f"{base_url}{rel_path}",
        "model_path": rel_path
    })

# =========================================================
# MAIN
# =========================================================

if __name__ == "__main__":
    app.run(debug=True)
