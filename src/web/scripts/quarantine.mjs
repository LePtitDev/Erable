// Quarantine of unstable tests (docs/testing.md §1 and §13, M5-04).
//
// A flaky test is never "re-run until green": it is tagged `@quarantine` in its title (Playwright) so that it leaves the
// blocking pipelines (it still runs apart, `pnpm e2e:quarantine`), and registered in spec/quarantine.json:
//   { "entries": [ { "id": "E2E-05-03", "since": "2026-10-17", "reason": "why it is unstable" } ] }
// The rule: fixed or deleted within 24 hours.
//
//   node scripts/quarantine.mjs     checks the registry against the tests; fails when
//     - a test tagged @quarantine has no entry, or an entry has no test (the registry is the single list);
//     - an entry is older than 24 hours (or has no valid date).
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const webDir = resolve(import.meta.dirname, "..");
const registryPath = resolve(webDir, "..", "..", "spec", "quarantine.json");
const maxAgeMs = 24 * 60 * 60 * 1000;

function* files(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* files(path);
    else if (/\.(spec|test)\.ts$/.test(name)) yield path;
  }
}

const registry = JSON.parse(readFileSync(registryPath, "utf8"));
const entries = registry.entries ?? [];
const problems = [];

const tagged = new Map();
for (const path of files(join(webDir, "e2e"))) {
  for (const match of readFileSync(path, "utf8").matchAll(
    /test\(\s*[`"']((E2E-\d{2}-\d{2})[^`"']*@quarantine[^`"']*)[`"']/g,
  )) {
    tagged.set(match[2], relative(webDir, path));
  }
}

const registered = new Set(entries.map((entry) => entry.id));
for (const [id, path] of tagged) {
  if (!registered.has(id))
    problems.push(
      `${id} (${path}) is tagged @quarantine but not registered in spec/quarantine.json`,
    );
}
for (const entry of entries) {
  if (!tagged.has(entry.id))
    problems.push(`${entry.id} is registered but no test carries @quarantine: remove the entry`);
  const since = Date.parse(entry.since ?? "");
  if (Number.isNaN(since)) problems.push(`${entry.id}: "since" is missing or not a date`);
  else if (Date.now() - since > maxAgeMs)
    problems.push(
      `${entry.id} has been in quarantine since ${entry.since} (> 24 h): fix it or delete it — ${entry.reason ?? "no reason given"}`,
    );
}

if (problems.length > 0) {
  console.error(problems.join("\n"));
  process.exit(1);
}
console.log(`Quarantine: ${String(entries.length)} test(s), none expired.`);
