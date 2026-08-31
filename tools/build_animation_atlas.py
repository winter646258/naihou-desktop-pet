#!/usr/bin/env python3
"""Build the MonkeyPet 8x6 animation atlas from transparent 4x2 source sheets."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage


ACTIONS = ("idle", "crawl", "climb", "hang", "jump", "sleep")
FPS = {"idle": 4, "crawl": 8, "climb": 8, "hang": 5, "jump": 10, "sleep": 2}
SOURCE_COLUMNS = 4
SOURCE_ROWS = 2
FRAME_SIZE = 512
CONTENT_SIZE = 440


def split_frames(sheet: Image.Image) -> list[Image.Image]:
    alpha = np.asarray(sheet.getchannel("A"))
    labels, count = ndimage.label(alpha > 12)
    objects = ndimage.find_objects(labels)
    components = []
    for label, slices in enumerate(objects, start=1):
        if slices is None:
            continue
        pixels = int(np.count_nonzero(labels[slices] == label))
        if pixels < 500:
            continue
        y_slice, x_slice = slices
        components.append((pixels, label, x_slice.start, y_slice.start, x_slice.stop, y_slice.stop))

    components = sorted(components, reverse=True)[: SOURCE_COLUMNS * SOURCE_ROWS]
    if len(components) != SOURCE_COLUMNS * SOURCE_ROWS:
        raise ValueError(f"expected 8 character components, found {len(components)}")

    components.sort(key=lambda item: ((item[3] + item[5]) // 2, (item[2] + item[4]) // 2))
    top = sorted(components[:SOURCE_COLUMNS], key=lambda item: item[2])
    bottom = sorted(components[SOURCE_COLUMNS:], key=lambda item: item[2])
    ordered = top + bottom
    max_width = max(item[4] - item[2] for item in ordered)
    max_height = max(item[5] - item[3] for item in ordered)

    frames = []
    for _, label, left, top, right, bottom in ordered:
        component = sheet.crop((left, top, right, bottom))
        component_alpha = np.asarray(component.getchannel("A"))
        source_labels = labels[top:bottom, left:right]
        kept_alpha = np.where(source_labels == label, component_alpha, 0).astype(np.uint8)
        component.putalpha(Image.fromarray(kept_alpha, mode="L"))
        frame = Image.new("RGBA", (max_width, max_height), (0, 0, 0, 0))
        frame.alpha_composite(component, ((max_width - component.width) // 2, (max_height - component.height) // 2))
        frames.append(frame)
    return frames


def union_bbox(frames: list[Image.Image]) -> tuple[int, int, int, int]:
    boxes = [frame.getchannel("A").getbbox() for frame in frames]
    visible = [box for box in boxes if box is not None]
    if not visible:
        raise ValueError("source sheet has no visible pixels after chroma-key removal")
    return (
        min(box[0] for box in visible),
        min(box[1] for box in visible),
        max(box[2] for box in visible),
        max(box[3] for box in visible),
    )


def normalize_frames(frames: list[Image.Image]) -> list[Image.Image]:
    left, top, right, bottom = union_bbox(frames)
    crop_width = right - left
    crop_height = bottom - top
    scale = min(CONTENT_SIZE / crop_width, CONTENT_SIZE / crop_height)
    target = (max(1, round(crop_width * scale)), max(1, round(crop_height * scale)))
    offset = ((FRAME_SIZE - target[0]) // 2, (FRAME_SIZE - target[1]) // 2)

    normalized = []
    for frame in frames:
        crop = frame.crop((left, top, right, bottom))
        crop = crop.resize(target, Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
        canvas.alpha_composite(crop, offset)
        normalized.append(canvas)
    return normalized


def build(source_dir: Path, output: Path, contact_sheet: Path, preview_dir: Path | None) -> None:
    atlas = Image.new("RGBA", (FRAME_SIZE * 8, FRAME_SIZE * len(ACTIONS)), (0, 0, 0, 0))
    preview = Image.new("RGBA", (256 * 8, 256 * len(ACTIONS)), (28, 30, 34, 255))

    for row, action in enumerate(ACTIONS):
        source = source_dir / f"{action}.png"
        if not source.exists():
            raise FileNotFoundError(source)
        with Image.open(source) as opened:
            frames = normalize_frames(split_frames(opened.convert("RGBA")))
        gif_frames = []
        for column, frame in enumerate(frames):
            atlas.alpha_composite(frame, (column * FRAME_SIZE, row * FRAME_SIZE))
            small = frame.resize((256, 256), Image.Resampling.LANCZOS)
            preview.alpha_composite(small, (column * 256, row * 256))
            if preview_dir is not None:
                gif_frame = Image.new("RGBA", (256, 256), (28, 30, 34, 255))
                gif_frame.alpha_composite(small)
                gif_frames.append(gif_frame.convert("RGB"))
        if preview_dir is not None:
            preview_dir.mkdir(parents=True, exist_ok=True)
            gif_frames[0].save(
                preview_dir / f"{action}.gif",
                save_all=True,
                append_images=gif_frames[1:],
                duration=round(1000 / FPS[action]),
                loop=0,
                optimize=True,
            )

    output.parent.mkdir(parents=True, exist_ok=True)
    contact_sheet.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output, optimize=True)
    preview.convert("RGB").save(contact_sheet, quality=92)
    print(f"Wrote atlas: {output} ({atlas.width}x{atlas.height})")
    print(f"Wrote contact sheet: {contact_sheet} ({preview.width}x{preview.height})")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--contact-sheet", required=True, type=Path)
    parser.add_argument("--preview-dir", type=Path)
    args = parser.parse_args()
    build(args.source_dir, args.output, args.contact_sheet, args.preview_dir)


if __name__ == "__main__":
    main()
