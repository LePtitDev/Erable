// Release notes from the Conventional Commits since the previous tag (M5-03).
// Usage: node build/release-notes.mjs <tag> [--previous <tag>]   → Markdown on stdout
import { execFileSync } from "node:child_process";

const tag = process.argv[2];
if (!tag) {
  console.error("Usage: node build/release-notes.mjs <tag> [--previous <tag>]");
  process.exit(2);
}
const git = (...args) => execFileSync("git", args, { encoding: "utf8" }).trim();
const previousIndex = process.argv.indexOf("--previous");
let previous = previousIndex > 0 ? process.argv[previousIndex + 1] : undefined;
if (!previous) {
  try {
    previous = git("describe", "--tags", "--abbrev=0", "--match", "v*", `${tag}^`);
  } catch {
    previous = undefined; // first release: the whole history
  }
}

const range = previous ? `${previous}..${tag}` : tag;
const subjects = git("log", "--no-merges", "--format=%s", range).split("\n").filter(Boolean);

const sections = [
  ["feat", "Nouveautés"],
  ["fix", "Corrections"],
  ["perf", "Performance"],
  ["docs", "Documentation"],
  ["test", "Tests"],
  ["build", "Construction et livraison"],
  ["ci", "Construction et livraison"],
  ["refactor", "Autres changements"],
  ["chore", "Autres changements"],
];
const titles = [...new Set(sections.map(([, title]) => title))];
const grouped = new Map(titles.map((title) => [title, []]));
for (const subject of subjects) {
  const match = /^(\w+)(?:\(([^)]+)\))?(!)?: (.+)$/.exec(subject);
  const title = match ? (sections.find(([type]) => type === match[1])?.[1] ?? "Autres changements") : "Autres changements";
  const text = match ? `${match[2] ? `**${match[2]}** : ` : ""}${match[3] ? "⚠️ rupture : " : ""}${match[4]}` : subject;
  grouped.get(title).push(text);
}

const lines = [`# Erable ${tag.replace(/^v/, "")}`, ""];
for (const [title, items] of grouped) {
  if (items.length === 0) continue;
  lines.push(`## ${title}`, "", ...items.map((item) => `- ${item}`), "");
}
lines.push(
  "## Installation",
  "",
  "- **Windows** : `Erable-win-Setup.exe`. **macOS** : paquet `.pkg`. **Linux** : `.AppImage`.",
  "- **Serveur** : `docker pull ghcr.io/leptitdev/erable:" + tag.replace(/^v/, "") + "` (voir `docs/deployment.md`).",
  "- Les installations existantes se mettent à jour seules au démarrage.",
  "",
);
process.stdout.write(lines.join("\n"));
