import { readFileSync } from "node:fs";
import { join } from "node:path";

/** A file as given to a file chooser, a paste or a drop. */
export interface SampleFile {
  readonly name: string;
  readonly mimeType: string;
  readonly buffer: Buffer;
}

const assets = join(import.meta.dirname, "..", "..", "..", "..", "spec", "projects", "assets");

/**
 * A real PNG (the portrait of the reference project), under another name. With a `salt`, bytes are appended after
 * the image so that its content is not already stored in the project.
 */
export function png(name = "croquis.png", salt?: string): SampleFile {
  const image = readFileSync(join(assets, "portrait_ilsa.png"));
  return {
    name,
    mimeType: "image/png",
    buffer: salt === undefined ? image : Buffer.concat([image, Buffer.from(salt)]),
  };
}

/** A minimal one-page PDF. */
export function pdf(name = "registre.pdf"): SampleFile {
  const body = [
    "%PDF-1.4",
    "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj",
    "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj",
    "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>endobj",
    "trailer<</Root 1 0 R>>",
    "%%EOF",
  ].join("\n");
  return { name, mimeType: "application/pdf", buffer: Buffer.from(body, "latin1") };
}

export function zip(name = "archives.zip"): SampleFile {
  return { name, mimeType: "application/zip", buffer: readFileSync(join(assets, "plans.zip")) };
}

/** `size` bytes of zeros. */
export function blob(name: string, size: number): SampleFile {
  return { name, mimeType: "application/octet-stream", buffer: Buffer.alloc(size) };
}
