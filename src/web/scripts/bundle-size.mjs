// Prints the gzip size of the initial JS bundle (entry chunk + its static imports).
// Usage: node scripts/bundle-size.mjs [--max-kb 500]
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { gzipSync } from "node:zlib";

const dist = join(import.meta.dirname, "..", "dist");
const html = readFileSync(join(dist, "index.html"), "utf8");
const initial = new Set(
  [...html.matchAll(/<(?:script|link)[^>]+(?:src|href)="\/?([^"]+\.js)"/g)].map((m) => m[1]),
);

let total = 0;
for (const file of initial) {
  const size = gzipSync(readFileSync(join(dist, file))).length;
  total += size;
  console.log(`  ${file}  ${(size / 1024).toFixed(1)} kB gzip`);
}
const totalKb = total / 1024;
console.log(
  `Initial JS bundle: ${totalKb.toFixed(1)} kB gzip (budget 500 kB, docs/testing.md §11.1)`,
);

const maxIndex = process.argv.indexOf("--max-kb");
if (maxIndex > 0) {
  const max = Number(process.argv[maxIndex + 1]);
  if (totalKb > max) {
    console.error(`Bundle exceeds budget: ${totalKb.toFixed(1)} kB > ${max} kB`);
    process.exit(1);
  }
}
