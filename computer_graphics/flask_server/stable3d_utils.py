import os
import sys
import subprocess
import uuid
from pathlib import Path

# Base directory of this module
BASE_DIR = Path(__file__).resolve().parent

# Stable Fast 3D root directory
STABLE3D_DIR = (BASE_DIR / ".." / "stable-fast-3d").resolve()

def generate_3d_model(image_path, output_dir="static"):
    # Resolve absolute paths for input image and output directory
    image_abs = (BASE_DIR / image_path).resolve()
    out_abs = (BASE_DIR / output_dir).resolve()
    out_abs.mkdir(parents=True, exist_ok=True)

    # Create a unique output folder to avoid overwriting previous runs
    stem = Path(image_path).stem
    run_out_dir = out_abs / f"{stem}_3d_{uuid.uuid4().hex[:6]}"
    run_out_dir.mkdir(parents=True, exist_ok=True)

    # Path to Stable Fast 3D entry script
    run_py = (STABLE3D_DIR / "run.py").resolve()

    # Build command using current Python interpreter
    # CPU device is enforced to avoid CUDA / flash-attention issues
    cmd = [
        sys.executable,
        str(run_py),
        str(image_abs),
        "--output-dir", str(run_out_dir),
        "--device", "cpu"
    ]

    # Execute Stable Fast 3D from its own working directory
    proc = subprocess.run(
        cmd,
        cwd=str(STABLE3D_DIR),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True
    )

    # Propagate execution errors with full logs
    if proc.returncode != 0:
        raise RuntimeError(
            f"StableFast3D failed.\nSTDOUT:\n{proc.stdout}\n\nSTDERR:\n{proc.stderr}"
        )

    # Search for generated GLB file in output directory
    glb_path = None
    for f in run_out_dir.glob("*.glb"):
        glb_path = f
        break

    if not glb_path:
        raise FileNotFoundError(
            f"No .glb found in {run_out_dir}\nSTDOUT:\n{proc.stdout}\n\nSTDERR:\n{proc.stderr}"
        )

    # Rename final GLB to match input image name for cleaner access from Unity
    final_glb = out_abs / f"{stem}.glb"
    try:
        if final_glb.exists():
            final_glb.unlink()
        glb_path.replace(final_glb)
    except Exception:
        # Fallback to original file if rename fails
        final_glb = glb_path

    # Return relative path suitable for Flask static serving
    return "/" + str(final_glb.relative_to(BASE_DIR)).replace("\\", "/")
