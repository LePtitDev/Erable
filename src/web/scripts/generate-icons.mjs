// Renders the application icons from one drawing (the Lucide « leaf » of the in-app logo, white on the accent green)
// into the PNG files of the web manifest (F18.R8) and of the Android app (M6-07). Run it after changing the drawing;
// the outputs are committed.
// Usage: node scripts/generate-icons.mjs
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { chromium } from "@playwright/test";

const webDir = resolve(import.meta.dirname, "..");
const repoDir = resolve(webDir, "..", "..");
const accent = "#2c6a55"; // --color-accent (light theme)
const leaf = [
  "M11 20a10 10 0 0010-10 25.9 25.9 0 00-1.04-7.281 1 1 0 00-1.755-.325C15.833 5.5 13 5.5 9.8 6.1A7 7 0 0011 20",
  "M2 21a5 5 0 012.911-4.544C7.613 15.212 8.351 15.24 11 13",
];

/** The icon on a 512 grid: `rounded` for a plain icon, full bleed with a smaller leaf for a maskable one. */
function svg({ rounded, leafSize, background = true }) {
  const scale = leafSize / 24;
  const offset = (512 - leafSize) / 2;
  const paths = leaf.map((d) => `<path d="${d}"/>`).join("");
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">${
    background ? `<rect width="512" height="512" rx="${rounded ? 112 : 0}" fill="${accent}"/>` : ""
  }<g transform="translate(${offset} ${offset}) scale(${scale})" fill="none" stroke="#fff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">${paths}</g></svg>`;
}

const outputs = [
  // Web manifest and browsers.
  { file: join(webDir, "public", "icons", "icon.svg"), svg: svg({ rounded: true, leafSize: 320 }) },
  {
    file: join(webDir, "public", "icons", "icon-192.png"),
    size: 192,
    svg: svg({ rounded: true, leafSize: 320 }),
  },
  {
    file: join(webDir, "public", "icons", "icon-512.png"),
    size: 512,
    svg: svg({ rounded: true, leafSize: 320 }),
  },
  {
    file: join(webDir, "public", "icons", "maskable-512.png"),
    size: 512,
    svg: svg({ rounded: false, leafSize: 260 }),
  },
  {
    file: join(webDir, "public", "icons", "apple-touch-icon.png"),
    size: 180,
    svg: svg({ rounded: false, leafSize: 280 }),
  },
  // Android launcher (legacy, square) for each density; the adaptive icon is a vector drawable of the same paths.
  ...[
    ["mdpi", 48],
    ["hdpi", 72],
    ["xhdpi", 96],
    ["xxhdpi", 144],
    ["xxxhdpi", 192],
  ].map(([density, size]) => ({
    file: join(
      repoDir,
      "src",
      "Erable.Android",
      "Resources",
      `mipmap-${density}`,
      "ic_launcher.png",
    ),
    size,
    svg: svg({ rounded: true, leafSize: 320 }),
  })),
];

const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  for (const output of outputs) {
    mkdirSync(dirname(output.file), { recursive: true });
    if (output.size === undefined) {
      writeFileSync(output.file, `${output.svg}\n`);
    } else {
      await page.setViewportSize({ width: output.size, height: output.size });
      await page.setContent(
        `<style>html,body{margin:0;background:transparent}svg{display:block;width:100vw;height:100vh}</style>${output.svg}`,
      );
      writeFileSync(output.file, await page.screenshot({ omitBackground: true }));
    }
    console.log(`  ${output.file}`);
  }
} finally {
  await browser.close();
}
