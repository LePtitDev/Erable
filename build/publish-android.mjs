// Publishes the Android application (M6-08, decision Q11) as one signed APK.
// Usage: node build/publish-android.mjs [--version <x.y.z>]
//   --version: default from Git, as build/publish-cli.mjs
// Signing (docs/release.md): with ERABLE_ANDROID_KEYSTORE (path of a .jks / .keystore), ERABLE_ANDROID_KEY_ALIAS,
// ERABLE_ANDROID_KEYSTORE_PASSWORD and ERABLE_ANDROID_KEY_PASSWORD (the store password when not set). Without them the
// APK is signed with the debug key of this machine: installable for a test, but not updatable by a release build.
// Needs the android workload: dotnet workload install android.
// Output: artifacts/android/Erable-<version>.apk
import { execFileSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, readdirSync, rmSync } from "node:fs";
import { join, resolve } from "node:path";
import { versionFromGit } from "./version.mjs";

const root = resolve(import.meta.dirname, "..");
const versionIndex = process.argv.indexOf("--version");
const version = versionIndex > 0 ? process.argv[versionIndex + 1] : versionFromGit(root);
if (!/^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$/.test(version ?? "")) {
  console.error("Usage: node build/publish-android.mjs [--version <x.y.z>]");
  process.exit(2);
}

/**
 * The versionCode Android compares to accept an update: always growing with the version, a release above the
 * development builds before it (x.y.z-dev.N comes before x.y.z). Up to 20.999.999 (Android: at most 2 100 000 000).
 */
function versionCodeOf(text) {
  const m = /^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$/.exec(text);
  if (m === null) throw new Error(`Not a version: ${text}`);
  const [, major, minor, patch, prerelease] = m;
  if (Number(major) > 20 || Number(minor) > 999 || Number(patch) > 999) {
    throw new Error(`${text} does not fit in an Android versionCode.`);
  }
  const build = prerelease === undefined ? 99 : Math.min(Number(/(\d+)$/.exec(prerelease)?.[1] ?? 0), 98);
  return ((Number(major) * 1000 + Number(minor)) * 1000 + Number(patch)) * 100 + build;
}

const project = join(root, "src", "Erable.Android");
const publishDir = join(project, "bin", "Release", "net10.0-android", "publish");
const outDir = join(root, "artifacts", "android");
const keystore = process.env["ERABLE_ANDROID_KEYSTORE"];

const signing = [];
if (keystore) {
  if (!existsSync(keystore)) throw new Error(`ERABLE_ANDROID_KEYSTORE: ${keystore} does not exist.`);
  for (const name of ["ERABLE_ANDROID_KEY_ALIAS", "ERABLE_ANDROID_KEYSTORE_PASSWORD"]) {
    if (!process.env[name]) throw new Error(`${name} is needed with ERABLE_ANDROID_KEYSTORE.`);
  }
  // « env: »: MSBuild reads the passwords from the environment, they never appear on a command line or in a log.
  const keyPassword = process.env["ERABLE_ANDROID_KEY_PASSWORD"]
    ? "env:ERABLE_ANDROID_KEY_PASSWORD"
    : "env:ERABLE_ANDROID_KEYSTORE_PASSWORD";
  signing.push(
    "-p:AndroidKeyStore=true",
    `-p:AndroidSigningKeyStore=${resolve(keystore)}`,
    `-p:AndroidSigningKeyAlias=${process.env["ERABLE_ANDROID_KEY_ALIAS"]}`,
    "-p:AndroidSigningStorePass=env:ERABLE_ANDROID_KEYSTORE_PASSWORD",
    `-p:AndroidSigningKeyPass=${keyPassword}`,
  );
} else {
  console.warn(
    "warning: ERABLE_ANDROID_KEYSTORE is not set: the APK is signed with the debug key (docs/release.md).",
  );
}

rmSync(publishDir, { recursive: true, force: true });
execFileSync(
  "dotnet",
  [
    "publish",
    project,
    "--configuration",
    "Release",
    "--framework",
    "net10.0-android",
    `-p:Version=${version}`,
    `-p:ApplicationDisplayVersion=${version}`,
    `-p:ApplicationVersion=${String(versionCodeOf(version))}`,
    ...signing,
  ],
  { cwd: root, stdio: "inherit" },
);

const apk = readdirSync(publishDir).find((name) => name.endsWith("-Signed.apk"));
if (apk === undefined) throw new Error(`No signed APK in ${publishDir}.`);
mkdirSync(outDir, { recursive: true });
const target = join(outDir, `Erable-${version}.apk`);
copyFileSync(join(publishDir, apk), target);
console.log(`Published ${target} (versionCode ${String(versionCodeOf(version))})`);
