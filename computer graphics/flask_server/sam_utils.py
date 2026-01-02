import torch
import cv2
import numpy as np
from segment_anything import sam_model_registry, SamAutomaticMaskGenerator

def load_sam(model_path):
    sam = sam_model_registry["vit_h"](checkpoint=model_path)
    sam.to(device="cuda" if torch.cuda.is_available() else "cpu")
    return sam

def segment_image_auto(sam_model, image_path, output_mask_path, transparent_output_path=None, preview_output_path=None):
    mask_generator = SamAutomaticMaskGenerator(sam_model)

    # Carica immagine
    image = cv2.imread(image_path, cv2.IMREAD_UNCHANGED)
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

    # Genera tutte le maschere
    masks = mask_generator.generate(image_rgb)
    if not masks:
        print("Nessuna maschera trovata!")
        return None

    # Prende la maschera più grande
    largest_mask = max(masks, key=lambda x: x['area'])['segmentation']
    mask_img = (largest_mask.astype(np.uint8)) * 255

    # Pulizia bordi
    kernel = np.ones((3, 3), np.uint8)
    mask_img = cv2.morphologyEx(mask_img, cv2.MORPH_CLOSE, kernel)
    mask_img = cv2.morphologyEx(mask_img, cv2.MORPH_OPEN, kernel)

    # Salva maschera binaria
    cv2.imwrite(output_mask_path, mask_img)

    if transparent_output_path:
        inverted_mask = cv2.bitwise_not(mask_img)

        if image.shape[2] == 3:
            image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)

        image[:, :, 3] = inverted_mask

        # Trova bounding box
        coords = cv2.findNonZero(inverted_mask)
        if coords is not None:
            x, y, w, h = cv2.boundingRect(coords)
            cropped = image[y:y+h, x:x+w]

            # Salva PNG trasparente
            cv2.imwrite(transparent_output_path, cropped)

            # Salva anteprima JPG con sfondo bianco
            if preview_output_path:
                preview = cv2.cvtColor(cropped, cv2.COLOR_BGRA2BGR)
                white_bg = np.ones_like(preview, dtype=np.uint8) * 255
                mask_bool = cropped[:, :, 3] > 0
                white_bg[mask_bool] = preview[mask_bool]
                cv2.imwrite(preview_output_path, white_bg)

    return output_mask_path
