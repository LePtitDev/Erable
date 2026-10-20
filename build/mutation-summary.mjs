// Mutation score and surviving mutants, as Markdown, from a Stryker report (Stryker.NET and StrykerJS share the format
// of mutation-testing-elements).
// Usage: node build/mutation-summary.mjs <folder holding the JSON report> [--top 25]   → Markdown on stdout
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join, resolve } from "node:path";

const folder = process.argv[2];
if (!folder) {
  console.error("Usage: node build/mutation-summary.mjs <folder> [--top 25]");
  process.exit(2);
}
const topIndex = process.argv.indexOf("--top");
const top = topIndex > 0 ? Number(process.argv[topIndex + 1]) : 25;

function* jsonFiles(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) yield* jsonFiles(path);
    else if (name.endsWith(".json")) yield path;
  }
}

const root = resolve(folder);
if (!existsSync(root)) {
  console.log(`No mutation report in \`${folder}\`.`);
  process.exit(0);
}
let report;
for (const path of jsonFiles(root)) {
  const candidate = JSON.parse(readFileSync(path, "utf8"));
  if (candidate.files && candidate.schemaVersion) {
    report = candidate;
    break;
  }
}
if (!report) {
  console.log(`No mutation report in \`${folder}\`.`);
  process.exit(0);
}

const counts = {};
const survivors = [];
for (const [file, data] of Object.entries(report.files)) {
  for (const mutant of data.mutants) {
    counts[mutant.status] = (counts[mutant.status] ?? 0) + 1;
    if (mutant.status === "Survived" || mutant.status === "NoCoverage") {
      survivors.push({
        file,
        line: mutant.location.start.line,
        mutator: mutant.mutatorName,
        status: mutant.status,
        replacement: mutant.replacement,
      });
    }
  }
}
const detected = (counts["Killed"] ?? 0) + (counts["Timeout"] ?? 0);
const undetected = (counts["Survived"] ?? 0) + (counts["NoCoverage"] ?? 0);
const score =
  detected + undetected === 0
    ? 100
    : (100 * detected) / (detected + undetected);

const lines = [
  `### Mutation score: ${score.toFixed(1)} % ${score >= 70 ? "(objective >= 70 % reached)" : "(**below the objective of 70 %**)"}`,
  "",
  `Killed ${counts["Killed"] ?? 0} · timeout ${counts["Timeout"] ?? 0} · survived ${counts["Survived"] ?? 0} · no coverage ${counts["NoCoverage"] ?? 0}`,
  "",
];
if (survivors.length > 0) {
  lines.push(
    `Surviving mutants (first ${Math.min(top, survivors.length)} of ${survivors.length}):`,
    "",
    "| File | Line | Mutator | Status |",
    "|---|---|---|---|",
  );
  for (const s of survivors.slice(0, top))
    lines.push(`| \`${s.file}\` | ${s.line} | ${s.mutator} | ${s.status} |`);
  lines.push("");
}
process.stdout.write(lines.join("\n"));
