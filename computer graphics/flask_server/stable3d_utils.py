import os
import sys
import subprocess
import uuid
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
STABLE3D_DIR = (BASE_DIR / ".." / "stable-fast-3d").resolve()

def generate_3d_model(image_path, output_dir="static"):
    # percorsi assoluti
    image_abs = (BASE_DIR / image_path).resolve()
    out_abs = (BASE_DIR / output_dir).resolve()
    out_abs.mkdir(parents=True, exist_ok=True)

    # nome unico cartella output (per non sovrascrivere)
    stem = Path(image_path).stem
    run_out_dir = out_abs / f"{stem}_3d_{uuid.uuid4().hex[:6]}"
    run_out_dir.mkdir(parents=True, exist_ok=True)

    # script run.py
    run_py = (STABLE3D_DIR / "run.py").resolve()

    # Comando: uso sys.executable + cwd=STABLE3D_DIR
    cmd = [
        sys.executable,
        str(run_py),
        str(image_abs),
        "--output-dir", str(run_out_dir),
        "--device", "cpu"   # così non tenta CUDA/flash-attn
    ]

    print("[Stable3D] CWD:", STABLE3D_DIR)
    print("[Stable3D] CMD:", " ".join(cmd))

    proc = subprocess.run(
        cmd,
        cwd=str(STABLE3D_DIR),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True
    )

    if proc.returncode != 0:
        # Log utili per capire l’errore reale
        raise RuntimeError(f"StableFast3D failed.\nSTDOUT:\n{proc.stdout}\n\nSTDERR:\n{proc.stderr}")

    # alcuni run salvano 'mesh.glb' o <nome>.glb — cerchiamo qualsiasi .glb
    glb_path = None
    for f in run_out_dir.glob("*.glb"):
        glb_path = f
        break

    if not glb_path:
        # dump log in caso non troviamo il file
        raise FileNotFoundError(
            f"Nessun .glb trovato in {run_out_dir}\nSTDOUT:\n{proc.stdout}\n\nSTDERR:\n{proc.stderr}"
        )

    # rinomino con lo stesso nome dell'input (più pulito da usare da Unity)
    final_glb = out_abs / f"{stem}.glb"
    try:
        if final_glb.exists():
            final_glb.unlink()
        glb_path.replace(final_glb)
    except Exception:
        # se rename fallisce per lock, manteniamo quello originale
        final_glb = glb_path

    # ritorno un path relativo servibile da Flask
    # (static/... con slash forward per URL)
    return "/" + str(final_glb.relative_to(BASE_DIR)).replace("\\", "/")
