// Traceability between docs/features.md scenarios and Playwright tests (docs/features.md §4.3).
//   node scripts/trace-e2e.mjs                 report missing scenarios per milestone (never fails for them)
//   node scripts/trace-e2e.mjs --enforce M1    also fail if a scenario of M0..M1 has no test
// Always fails when a test carries an ID that the document does not define (or has struck out).
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const webDir = resolve(import.meta.dirname, "..");
const featuresPath = resolve(webDir, "..", "..", "docs", "features.md");
const e2eDir = join(webDir, "e2e");

const enforceIndex = process.argv.indexOf("--enforce");
const enforced = enforceIndex > 0 ? Number(process.argv[enforceIndex + 1]?.replace(/^M/, "")) : -1;

// Scenario header: "E2E-05-03 · Title    P0 · @pr @desktop · M1" (struck out: "~~E2E-…~~").
const scenarios = new Map();
const struck = new Set();
for (const line of readFileSync(featuresPath, "utf8").split(/\r?\n/)) {
  const m = /^(~~)?(E2E-\d{2}-\d{2}) · (.+?)\s{2,}(P\d) · (.*?) · M(\d)/.exec(line);
  if (!m) continue;
  const [, strike, id, title, priority, tags, milestone] = m;
  if (strike) struck.add(id);
  else
    scenarios.set(id, {
      id,
      title: title.trim(),
      priority,
      tags: tags.trim(),
      milestone: Number(milestone),
    });
}

function* files(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* files(path);
    else if (/\.(spec|test)\.ts$/.test(name)) yield path;
  }
}

const tests = new Map();
for (const file of files(e2eDir)) {
  for (const m of readFileSync(file, "utf8").matchAll(
    /\btest(?:\.\w+)?\(\s*["'`](E2E-\d{2}-\d{2})/g,
  )) {
    tests.set(m[1], relative(webDir, file).replaceAll("\\", "/"));
  }
}

const unknown = [...tests.keys()].filter((id) => !scenarios.has(id));
const byMilestone = new Map();
for (const s of scenarios.values()) {
  const entry = byMilestone.get(s.milestone) ?? { total: 0, missing: [] };
  entry.total++;
  if (!tests.has(s.id)) entry.missing.push(s);
  byMilestone.set(s.milestone, entry);
}

console.log(`E2E traceability: ${tests.size} of ${scenarios.size} scenarios have a test.`);
for (const [milestone, { total, missing }] of [...byMilestone].sort(([a], [b]) => a - b)) {
  const status = milestone <= enforced ? (missing.length ? "MISSING" : "ok") : "info";
  console.log(`\nM${milestone}: ${total - missing.length}/${total} implemented [${status}]`);
  if (milestone <= Math.max(enforced, 0) || missing.length <= 12) {
    for (const s of missing) console.log(`  - ${s.id} · ${s.title} (${s.priority} · ${s.tags})`);
  } else {
    console.log(`  - ${missing.length} scenarios still to implement`);
  }
}

let failed = false;
if (unknown.length) {
  failed = true;
  console.error(
    `\nTests with an ID not defined in docs/features.md: ${unknown.map((id) => `${id} (${tests.get(id)})${struck.has(id) ? " [struck out]" : ""}`).join(", ")}`,
  );
}
const enforcedMissing = [...byMilestone].filter(([m, e]) => m <= enforced && e.missing.length > 0);
if (enforcedMissing.length) {
  failed = true;
  console.error(`\nScenarios required up to M${enforced} have no test.`);
}
process.exit(failed ? 1 : 0);
