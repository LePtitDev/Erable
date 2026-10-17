// Runs the visual tests (and updates their references) in the fixed container of e2e/visual/Dockerfile.
//   node scripts/visual-docker.mjs            compare with the references
//   node scripts/visual-docker.mjs --update   rewrite the references (review the images before committing)
// The repository is mounted read-write but built from a copy made inside the container (without node_modules, bin and
// obj: binaries of another OS); only the screenshots are copied back.
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..", "..", "..");
const image = "erable-visual";
const update = process.argv.includes("--update");

function run(command, args) {
  const result = spawnSync(command, args, { stdio: "inherit" });
  if (result.status !== 0) process.exit(result.status ?? 1);
}

run("docker", ["build", "-t", image, resolve(root, "src", "web", "e2e", "visual")]);

const inside = [
  "mkdir /tmp/repo",
  "tar -C /work --exclude=node_modules --exclude=bin --exclude=obj --exclude=.git --exclude=artifacts -cf - . | tar -C /tmp/repo -xf -",
  "cd /tmp/repo",
  "corepack pnpm -C src/web install --frozen-lockfile",
  `corepack pnpm -C src/web exec playwright test --project visual${update ? " --update-snapshots" : ""}`,
].join(" && ");
// Whatever the outcome, the screenshots (new references, or the actual and diff images of a failure) come back.
const script = `${inside}; status=$?; mkdir -p /work/src/web/e2e/screenshots /work/src/web/test-results; cp -r /tmp/repo/src/web/e2e/screenshots/. /work/src/web/e2e/screenshots/; cp -r /tmp/repo/src/web/test-results/. /work/src/web/test-results/ 2>/dev/null; exit $status`;

run("docker", [
  "run",
  "--rm",
  "--ipc=host",
  "-e",
  "CI=1",
  "-v",
  `${root}:/work`,
  image,
  "bash",
  "-c",
  script,
]);
