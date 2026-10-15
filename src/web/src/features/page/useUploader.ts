import { useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { maxSizeOf, uploadAttachment } from "../../shared/api/attachments";
import { errorCode, errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import type { AttachmentItem } from "../../shared/api/pages";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";

export type UploadResult =
  | { readonly ok: true; readonly attachment: AttachmentItem }
  | { readonly ok: false; readonly message: string | null };

/**
 * Uploads files to a page and refreshes what shows its attachments (side panel, palette, tree). Failures come back
 * as a message for the user (null when cancelled): « too large » names the file and the limit (F06.R5).
 */
export function useUploader(projectId: ElementId, pageId: ElementId) {
  const { t } = useTranslation();
  const format = useFormatters();
  const client = useQueryClient();

  return useCallback(
    async (
      file: File,
      onProgress: (fraction: number) => void,
      signal?: AbortSignal,
    ): Promise<UploadResult> => {
      try {
        const attachment = await uploadAttachment(projectId, pageId, file, onProgress, signal);
        // Not awaited: the caller swaps its progress for the result at once, the lists catch up when refetched.
        void client.invalidateQueries({ queryKey: queryKeys.page(projectId, pageId) });
        void client.invalidateQueries({ queryKey: queryKeys.tree(projectId) });
        void client.invalidateQueries({
          queryKey: [...queryKeys.projectContent(projectId), "attachments"],
        });
        return { ok: true, attachment };
      } catch (e) {
        if (e instanceof DOMException && e.name === "AbortError")
          return { ok: false, message: null };
        const max = maxSizeOf(e);
        return {
          ok: false,
          message:
            errorCode(e) === "attachment.tooLarge" && max !== undefined
              ? t("attachments.tooLarge", { name: file.name, max: format.size(max) })
              : t("attachments.failed", { name: file.name, reason: errorMessage(t, e) }),
        };
      }
    },
    [projectId, pageId, client, t, format],
  );
}
