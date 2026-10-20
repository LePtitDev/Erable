// Packs the published desktop application with Velopack: installer, update packages and release manifest.
// Usage: node build/pack-desktop.mjs <rid> [--version <x.y.z>] [--skip-publish] [--channel <name>]
//   rid: win-x64 | linux-x64 | osx-arm64 | osx-x64   (run on the matching OS: vpk packs a macOS app on macOS only)
//   --version: default from Git: the version tag of HEAD, or after the last one x.y.(z+1)-dev.<commits since>, which
//              sorts between that release and the next (an installed local build is not « updated » to an older one).
//   --channel: Velopack channel; default win, linux, osx-arm64 or osx-x64. The two macOS architectures need their own
//              channel: they share one release, and the default « osx » would give their files the same names.
// Output: artifacts/release/<rid>/ (Setup / AppImage / pkg, *.nupkg, releases.<channel>.json, RELEASES-*).
//
// Signing is optional and driven by environment variables (docs/release.md); without them the packages are unsigned.
//   Windows (Azure Trusted Signing)  ERABLE_SIGN_AZURE_METADATA = path of the metadata JSON (endpoint, account, profile)
//   macOS                            ERABLE_SIGN_APPLE_APP_IDENTITY, ERABLE_SIGN_APPLE_INSTALL_IDENTITY, ERABLE_NOTARY_PROFILE
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { versionFromGit } from "./version.mjs";

const rids = ["win-x64", "linux-x64", "osx-arm64", "osx-x64"];
const argument = (name) => {
  const index = process.argv.indexOf(name);
  return index > 0 ? process.argv[index + 1] : undefined;
};
const root = resolve(import.meta.dirname, "..");
const rid = process.argv[2];
const version = argument("--version") ?? versionFromGit(root);
if (!rids.includes(rid) || !/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(version ?? "")) {
  console.error(`Usage: node build/pack-desktop.mjs <${rids.join("|")}> [--version <x.y.z>] [--skip-publish] [--channel <name>]`);
  process.exit(2);
}

const run = (file, args) => execFileSync(file, args, { cwd: root, stdio: "inherit" });
const os = rid.startsWith("win") ? "win" : rid.startsWith("osx") ? "osx" : "linux";
const publishDir = join(root, "artifacts", "desktop", rid, "Erable");
const outputDir = join(root, "artifacts", "release", rid);

if (!process.argv.includes("--skip-publish")) {
  run(process.execPath, [join(root, "build", "publish-desktop.mjs"), rid, "--version", version]);
}
if (!existsSync(publishDir)) throw new Error(`${publishDir} is missing: publish first.`);

rmSync(outputDir, { recursive: true, force: true });
mkdirSync(outputDir, { recursive: true });

// vpk wants an .ico on Windows, a .png on Linux and an .icns on macOS. The .icns is built from the 256 px PNG: an icns
// file is a header followed by typed chunks, and the « ic08 » chunk (256×256) holds a PNG as it is.
const icon = (() => {
  const assets = join(root, "src", "Erable.Desktop", "Assets");
  if (os === "win") return join(assets, "erable.ico");
  if (os === "linux") return join(assets, "erable.png");
  const png = readFileSync(join(assets, "erable.png"));
  if (png.readUInt32BE(16) !== 256 || png.readUInt32BE(20) !== 256) throw new Error("erable.png must be 256×256 for ic08.");
  const chunk = Buffer.alloc(8);
  chunk.write("ic08", 0, "ascii");
  chunk.writeUInt32BE(8 + png.length, 4);
  const header = Buffer.alloc(8);
  header.write("icns", 0, "ascii");
  header.writeUInt32BE(16 + png.length, 4);
  const path = join(root, "artifacts", "desktop", rid, "erable.icns");
  writeFileSync(path, Buffer.concat([header, chunk, png]));
  return path;
})();

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
  icon,
  "--outputDir",
  outputDir,
];
const channel = argument("--channel") ?? (os === "osx" ? rid : undefined);
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
