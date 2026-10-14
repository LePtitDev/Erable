import { createHash } from "node:crypto";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

/** SHA-256 of every file of a folder, by relative path: « the files are unchanged » (E2E-01-04). */
export function fingerprint(folder: string): Record<string, string> {
  const result: Record<string, string> = {};
  const walk = (dir: string) => {
    for (const name of readdirSync(dir)) {
      const path = join(dir, name);
      if (statSync(path).isDirectory()) walk(path);
      else
        result[relative(folder, path).replaceAll("\\", "/")] = createHash("sha256")
          .update(readFileSync(path))
          .digest("hex");
    }
  };
  walk(folder);
  return result;
}
