// Publishes the self-contained desktop application for one runtime and archives it (the Velopack
// installer is built from it by build/pack-desktop.mjs).
// Usage: node build/publish-desktop.mjs <rid> [--skip-web] [--version <x.y.z>]
//   rid: win-x64 | linux-x64 | osx-arm64 | osx-x64
//   --version: version stamped in the binaries (shown by the application); default: Directory.Build.props
// Output: artifacts/desktop/<rid>/Erable/ and artifacts/desktop/Erable-<rid>.(zip|tar.gz)
import { execFileSync } from "node:child_process";
import { existsSync, rmSync } from "node:fs";
import { join, resolve } from "node:path";

const rids = ["win-x64", "linux-x64", "osx-arm64", "osx-x64"];
const rid = process.argv[2];
const versionIndex = process.argv.indexOf("--version");
const version = versionIndex > 0 ? process.argv[versionIndex + 1] : undefined;
if (!rids.includes(rid) || (versionIndex > 0 && !/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(version ?? ""))) {
  console.error(`Usage: node build/publish-desktop.mjs <${rids.join("|")}> [--skip-web] [--version <x.y.z>]`);
  process.exit(2);
}

const root = resolve(import.meta.dirname, "..");
const web = join(root, "src", "web");
const outRoot = join(root, "artifacts", "desktop");
const appDir = join(outRoot, rid, "Erable");
const run = (file, args, cwd = root) => execFileSync(file, args, { cwd, stdio: "inherit" });

if (!process.argv.includes("--skip-web")) {
  // The SPA is copied into wwwroot/ by Erable.Server's build (linked content).
  run(process.execPath, [join(web, "node_modules", "vite", "bin", "vite.js"), "build"], web);
}
if (!existsSync(join(web, "dist", "index.html"))) {
  throw new Error("src/web/dist is missing: build the SPA first.");
}

rmSync(join(outRoot, rid), { recursive: true, force: true });
run("dotnet", [
  "publish",
  join("src", "Erable.Desktop"),
  "--configuration",
  "Release",
  "--runtime",
  rid,
  "--self-contained",
  "true",
  // Precompiled code: noticeably faster start (E2E-16-01 budget: 3 s).
  "-p:PublishReadyToRun=true",
  ...(version ? [`-p:Version=${version}`] : []),
  "--output",
  appDir,
]);
if (!existsSync(join(appDir, "wwwroot", "index.html"))) {
  throw new Error("The published application has no wwwroot/index.html.");
}

const archiveName = `Erable-${rid}.${rid.startsWith("win") ? "zip" : "tar.gz"}`;
rmSync(join(outRoot, archiveName), { force: true });
// Relative paths, run from the output folder. On Windows, the system bsdtar (not Git's GNU tar) writes zip with -a.
const tar = process.platform === "win32" ? join(process.env["SystemRoot"] ?? "C:\\Windows", "System32", "tar.exe") : "tar";
run(tar, rid.startsWith("win") ? ["-a", "-cf", archiveName, "-C", rid, "Erable"] : ["-czf", archiveName, "-C", rid, "Erable"], outRoot);
const archive = join(outRoot, archiveName);
console.log(`Published ${appDir}\nArchive   ${archive}`);
