// Publishes erable-cli (docs/cli.md) as one self-contained executable for one runtime, and archives it.
// Usage: node build/publish-cli.mjs <rid> [--version <x.y.z>]
//   rid: win-x64 | linux-x64 | osx-arm64 | osx-x64   (any OS can publish any of them)
//   --version: default from Git, as build/pack-desktop.mjs
// Output: artifacts/cli/<rid>/erable-cli[.exe] and artifacts/cli/erable-cli-<rid>.(zip|tar.gz)
import { execFileSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { join, resolve } from "node:path";
import { versionFromGit } from "./version.mjs";

const rids = ["win-x64", "linux-x64", "osx-arm64", "osx-x64"];
const rid = process.argv[2];
const root = resolve(import.meta.dirname, "..");
const versionIndex = process.argv.indexOf("--version");
const version = versionIndex > 0 ? process.argv[versionIndex + 1] : versionFromGit(root);
if (!rids.includes(rid) || !/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(version ?? "")) {
  console.error(`Usage: node build/publish-cli.mjs <${rids.join("|")}> [--version <x.y.z>]`);
  process.exit(2);
}

const outRoot = join(root, "artifacts", "cli");
const appDir = join(outRoot, rid);
const run = (file, args, cwd = root) => execFileSync(file, args, { cwd, stdio: "inherit" });

rmSync(appDir, { recursive: true, force: true });
run("dotnet", [
  "publish",
  join("src", "Erable.Cli"),
  "--configuration",
  "Release",
  "--runtime",
  rid,
  "--self-contained",
  "true",
  // One file to copy anywhere; SQLite's native library is inside and extracted on first run.
  "-p:PublishSingleFile=true",
  "-p:IncludeNativeLibrariesForSelfExtract=true",
  // Half the size on disk, for a few milliseconds more at start.
  "-p:EnableCompressionInSingleFile=true",
  "-p:DebugType=none",
  `-p:Version=${version}`,
  "--output",
  appDir,
]);
const executable = join(appDir, rid.startsWith("win") ? "erable-cli.exe" : "erable-cli");
if (!existsSync(executable)) throw new Error(`${executable} is missing after the publication.`);

const archiveName = `erable-cli-${rid}.${rid.startsWith("win") ? "zip" : "tar.gz"}`;
rmSync(join(outRoot, archiveName), { force: true });
// Relative paths, run from the output folder. On Windows, the system bsdtar (not Git's GNU tar) writes zip with -a.
const tar = process.platform === "win32" ? join(process.env["SystemRoot"] ?? "C:\\Windows", "System32", "tar.exe") : "tar";
const file = rid.startsWith("win") ? "erable-cli.exe" : "erable-cli";
run(tar, rid.startsWith("win") ? ["-a", "-cf", join("..", archiveName), file] : ["-czf", join("..", archiveName), file], appDir);
console.log(`Published ${executable}\nArchive   ${join(outRoot, archiveName)}`);
