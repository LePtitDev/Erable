import type { ElementId } from "../ids/elementId";
import { ApiError, type ProblemDetails } from "./client";
import type { AttachmentItem } from "./pages";

/**
 * Uploads one file as an attachment of a page (F06.R1). One request per file, so that each file has its own
 * progress. `XMLHttpRequest` rather than `fetch`: only it reports upload progress. Rejects with an {@link ApiError}
 * carrying the backend's ProblemDetails (413 `attachment.tooLarge` with `maxSize`), or a plain `Error` when the
 * request never reached the backend.
 */
export function uploadAttachment(
  projectId: ElementId,
  pageId: ElementId,
  file: File,
  onProgress: (fraction: number) => void,
  signal?: AbortSignal,
): Promise<AttachmentItem> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/api/v1/projects/${projectId}/pages/${pageId}/attachments`);
    xhr.responseType = "text";
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable && event.total > 0) onProgress(event.loaded / event.total);
    };
    xhr.onload = () => {
      const body = parse(xhr.responseText);
      if (xhr.status >= 200 && xhr.status < 300 && body !== undefined) {
        resolve(body as AttachmentItem);
      } else {
        reject(new ApiError(xhr.status, body as ProblemDetails | undefined));
      }
    };
    xhr.onerror = () => {
      reject(new Error("The upload did not reach the backend."));
    };
    xhr.onabort = () => {
      reject(new DOMException("The upload was cancelled.", "AbortError"));
    };
    signal?.addEventListener("abort", () => {
      xhr.abort();
    });
    const form = new FormData();
    form.append("file", file, file.name);
    xhr.send(form);
  });
}

function parse(text: string): unknown {
  try {
    const value: unknown = JSON.parse(text);
    return typeof value === "object" && value !== null ? value : undefined;
  } catch {
    return undefined;
  }
}

/** The limit reported by a refused upload (413 `attachment.tooLarge`), in bytes. */
export function maxSizeOf(error: unknown): number | undefined {
  const value = error instanceof ApiError ? error.problem?.["maxSize"] : undefined;
  return typeof value === "number" ? value : undefined;
}
