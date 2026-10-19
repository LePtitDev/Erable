// Version of a local build, from Git (pack-desktop.mjs, publish-cli.mjs, scripts/publish, Directory.Build.targets).
// Run directly, prints it (nothing when Git cannot tell): node build/version.mjs
import { execFileSync } from "node:child_process";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

/** x.y.z for a commit tagged vx.y.z (or x.y.z); x.y.(z+1)-dev.N N commits after it; 0.0.1-dev.N without any tag. */
export function versionFromGit(root) {
  let described;
  try {
    described = execFileSync("git", ["describe", "--tags", "--long", "--match", "v[0-9]*", "--match", "[0-9]*"], {
      cwd: root,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
  } catch {
    const count = execFileSync("git", ["rev-list", "--count", "HEAD"], { cwd: root, encoding: "utf8" }).trim();
    return `0.0.1-dev.${count}`;
  }
  // v1.2.3-4-gabcdef0 (--long: the count is always there, 0 on the tag itself)
  const match = /^v?(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+?)?-(\d+)-g[0-9a-f]+$/.exec(described);
  if (!match) return undefined;
  const [, major, minor, patch, prerelease, distance] = match;
  if (distance === "0") return `${major}.${minor}.${patch}${prerelease ?? ""}`;
  return `${major}.${minor}.${Number(patch) + 1}-dev.${distance}`;
}

if (process.argv[1] !== undefined && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    process.stdout.write(versionFromGit(resolve(import.meta.dirname, "..")) ?? "");
  } catch {
    // No Git, or not a repository (a source archive): the caller keeps its default.
  }
}
