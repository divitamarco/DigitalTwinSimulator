import torch
import cv2
import numpy as np
from segment_anything import sam_model_registry, SamAutomaticMaskGenerator

def load_sam(model_path):
    # Load SAM ViT-H model from checkpoint
    sam = sam_model_registry["vit_h"](checkpoint=model_path)

    # Move model to GPU if available, otherwise CPU
    sam.to(device="cuda" if torch.cuda.is_available() else "cpu")

    return sam

def segment_image_auto(
    sam_model,
    image_path,
    output_mask_path,
    transparent_output_path=None,
    preview_output_path=None
):
    # Automatic mask generator using SAM
    mask_generator = SamAutomaticMaskGenerator(sam_model)

    # Load image from disk (supports RGB/RGBA)
    image = cv2.imread(image_path, cv2.IMREAD_UNCHANGED)
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

    # Generate all segmentation masks
    masks = mask_generator.generate(image_rgb)
    if not masks:
        # No segmentation found
        return None

    # Select the largest mask by area (assumed main object)
    largest_mask = max(masks, key=lambda x: x['area'])['segmentation']
    mask_img = (largest_mask.astype(np.uint8)) * 255

    # Morphological operations to remove small artifacts
    kernel = np.ones((3, 3), np.uint8)
    mask_img = cv2.morphologyEx(mask_img, cv2.MORPH_CLOSE, kernel)
    mask_img = cv2.morphologyEx(mask_img, cv2.MORPH_OPEN, kernel)

    # Save binary mask
    cv2.imwrite(output_mask_path, mask_img)

    if transparent_output_path:
        # Invert mask to obtain transparent background
        inverted_mask = cv2.bitwise_not(mask_img)

        # Ensure image has alpha channel
        if image.shape[2] == 3:
            image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)

        # Apply transparency using inverted mask
        image[:, :, 3] = inverted_mask

        # Compute bounding box around segmented object
        coords = cv2.findNonZero(inverted_mask)
        if coords is not None:
            x, y, w, h = cv2.boundingRect(coords)
            cropped = image[y:y+h, x:x+w]

            # Save transparent PNG of segmented object
            cv2.imwrite(transparent_output_path, cropped)

            if preview_output_path:
                # Create preview with white background for visualization
                preview = cv2.cvtColor(cropped, cv2.COLOR_BGRA2BGR)
                white_bg = np.ones_like(preview, dtype=np.uint8) * 255
                mask_bool = cropped[:, :, 3] > 0
                white_bg[mask_bool] = preview[mask_bool]

                # Save JPG preview
                cv2.imwrite(preview_output_path, white_bg)

    return output_mask_path
