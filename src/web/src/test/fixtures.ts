/**
 * Runner for the specification shared with the C# implementation (docs/testing.md §5).
 * Reads every `spec/fixtures/<folder>/**\/*.json` file: `{ "description": "…", "cases": [{ "name": "…", … }] }`.
 * The same files are read by tests/Erable.Testing/SharedFixtures.cs.
 *
 * ```ts
 * describe.each(loadFixtures<IdCase>("ids"))("$file › $name", (c) => { … });
 * ```
 */
interface FixtureFile {
  readonly description?: string;
  readonly cases: readonly ({ readonly name: string } & Record<string, unknown>)[];
}

// Every folder of spec/fixtures is picked up (limits, markdown, rules, ids…); a new folder shows after Vite re-reads this file.
const files = import.meta.glob<FixtureFile>("../../../../spec/fixtures/**/*.json", {
  eager: true,
  import: "default",
});

const prefix = "../../../../spec/fixtures/";

export type FixtureCase<T> = T & { readonly file: string; readonly name: string };

export function loadFixtures<T>(folder: string): FixtureCase<T>[] {
  const cases = Object.entries(files)
    .filter(([path]) => path.startsWith(`${prefix}${folder}/`))
    .sort(([a], [b]) => a.localeCompare(b))
    .flatMap(([path, content]) =>
      content.cases.map(
        (c) => ({ ...c, file: path.slice(prefix.length + folder.length + 1) }) as FixtureCase<T>,
      ),
    );
  if (cases.length === 0) {
    throw new Error(`No fixture case found under spec/fixtures/${folder}`);
  }
  return cases;
}
