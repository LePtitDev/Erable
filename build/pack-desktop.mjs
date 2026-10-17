// Packs the published desktop application with Velopack: installer, update packages and release manifest (M5-03).
// Usage: node build/pack-desktop.mjs <rid> --version <x.y.z> [--skip-publish] [--channel <name>]
//   rid: win-x64 | linux-x64 | osx-arm64 | osx-x64   (run on the matching OS: vpk packs a macOS app on macOS only)
// Output: artifacts/release/<rid>/ (Setup / AppImage / pkg, *.nupkg, releases.<channel>.json, RELEASES-*).
//
// Signing is optional and driven by environment variables (docs/release.md); without them the packages are unsigned.
//   Windows (Azure Trusted Signing)  ERABLE_SIGN_AZURE_METADATA = path of the metadata JSON (endpoint, account, profile)
//   macOS                            ERABLE_SIGN_APPLE_APP_IDENTITY, ERABLE_SIGN_APPLE_INSTALL_IDENTITY, ERABLE_NOTARY_PROFILE
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync } from "node:fs";
import { join, resolve } from "node:path";

const rids = ["win-x64", "linux-x64", "osx-arm64", "osx-x64"];
const argument = (name) => {
  const index = process.argv.indexOf(name);
  return index > 0 ? process.argv[index + 1] : undefined;
};
const rid = process.argv[2];
const version = argument("--version");
if (!rids.includes(rid) || !/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(version ?? "")) {
  console.error(`Usage: node build/pack-desktop.mjs <${rids.join("|")}> --version <x.y.z> [--skip-publish] [--channel <name>]`);
  process.exit(2);
}

const root = resolve(import.meta.dirname, "..");
const run = (file, args) => execFileSync(file, args, { cwd: root, stdio: "inherit" });
const os = rid.startsWith("win") ? "win" : rid.startsWith("osx") ? "osx" : "linux";
const publishDir = join(root, "artifacts", "desktop", rid, "Erable");
const outputDir = join(root, "artifacts", "release", rid);

if (!process.argv.includes("--skip-publish")) {
  run(process.execPath, [join(root, "build", "publish-desktop.mjs"), rid]);
}
if (!existsSync(publishDir)) throw new Error(`${publishDir} is missing: publish first.`);

rmSync(outputDir, { recursive: true, force: true });
mkdirSync(outputDir, { recursive: true });

const args = [
  "vpk",
  `[${os}]`,
  "pack",
  "--packId",
  "Erable",
  "--packTitle",
  "Erable",
  "--packAuthors",
  "Erable",
  "--packVersion",
  version,
  "--packDir",
  publishDir,
  "--mainExe",
  os === "win" ? "Erable.Desktop.exe" : "Erable.Desktop",
  "--runtime",
  rid,
  "--icon",
  join(root, "src", "Erable.Desktop", "Assets", os === "win" ? "erable.ico" : "erable.png"),
  "--outputDir",
  outputDir,
];
const channel = argument("--channel");
if (channel) args.push("--channel", channel);

const env = process.env;
if (os === "win" && env["ERABLE_SIGN_AZURE_METADATA"]) {
  args.push("--azureTrustedSignFile", env["ERABLE_SIGN_AZURE_METADATA"]);
} else if (os === "osx" && env["ERABLE_SIGN_APPLE_APP_IDENTITY"]) {
  args.push("--signAppIdentity", env["ERABLE_SIGN_APPLE_APP_IDENTITY"]);
  if (env["ERABLE_SIGN_APPLE_INSTALL_IDENTITY"]) args.push("--signInstallIdentity", env["ERABLE_SIGN_APPLE_INSTALL_IDENTITY"]);
  if (env["ERABLE_NOTARY_PROFILE"]) args.push("--notaryProfile", env["ERABLE_NOTARY_PROFILE"]);
} else if (os !== "linux") {
  console.warn(`No signing configured for ${rid}: the packages are unsigned (docs/release.md).`);
}


run("dotnet", ["tool", "restore"]);
run("dotnet", args);
console.log(`Packaged ${rid} ${version} in ${outputDir}`);
