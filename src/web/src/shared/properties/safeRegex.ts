/**
 * The regular expressions accepted by the `match` rule: a subset with the same meaning in .NET and JavaScript
 * (twin of Erable.Core/Properties/SafeRegex.cs, checked by spec/fixtures/rules/regex.json). The pattern written by the
 * user is translated into a canonical one: literals as `\uXXXX`, `\d \w \s` and `.` as explicit ASCII classes,
 * capturing groups as non-capturing ones. It runs without flags (UTF-16 semantics, like .NET).
 */

export const maxPatternLength = 500;
export const maxRepeat = 1000;

export type SafeRegexResult =
  | { readonly ok: true; readonly javaScript: string }
  | { readonly ok: false; readonly position: number; readonly message: string };

const metacharacters = "\\^$.|?*+()[]{}/-";

/** `\uXXXX`, built rather than written so that no tool or bundler can turn it into the character. */
function u(code: number): string {
  return "\\u" + code.toString(16).toUpperCase().padStart(4, "0");
}

function range(low: number, high: number): string {
  return u(low) + "-" + u(high);
}

const digitSet = range(0x30, 0x39);
const wordSet = range(0x41, 0x5a) + range(0x61, 0x7a) + range(0x30, 0x39) + u(0x5f);
const spaceSet = [0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20].map(u).join("");
const dotClass = "[^" + [0x0a, 0x0d, 0x2028, 0x2029].map(u).join("") + "]";

class Failure extends Error {}

class Parser {
  private position = 0;
  private readonly tokens: string[] = [];
  /** Quantifiers and alternations seen so far: a group that contains one cannot be quantified. */
  private complexity = 0;

  constructor(private readonly pattern: string) {}

  run(): SafeRegexResult {
    try {
      this.alternation();
      if (this.position < this.pattern.length) {
        throw new Failure(
          this.pattern[this.position] === ")"
            ? "unbalanced ')'"
            : `unexpected '${this.pattern[this.position] ?? ""}'`,
        );
      }
      return { ok: true, javaScript: this.tokens.join("") };
    } catch (error) {
      if (error instanceof Failure) {
        return { ok: false, position: this.position, message: error.message };
      }
      throw error;
    }
  }

  private at(offset = 0): string | undefined {
    return this.pattern[this.position + offset];
  }

  private peek(c: string): boolean {
    return this.at() === c;
  }

  private alternation(): void {
    this.sequence();
    while (this.peek("|")) {
      this.position++;
      this.complexity++;
      this.tokens.push("|");
      this.sequence();
    }
  }

  private sequence(): void {
    while (this.position < this.pattern.length && this.at() !== "|" && this.at() !== ")") {
      const term = this.term();
      const next = this.at();
      if (next === "*" || next === "+" || next === "?" || next === "{") {
        if (term === "anchor") {
          throw new Failure("nothing to repeat");
        }
        // (a+)+, (a|aa)*: exponential in a backtracking engine like the browser's.
        if (term === "complexGroup") {
          throw new Failure("a repeated group cannot contain a quantifier or an alternation");
        }
        this.quantifier();
        this.complexity++;
      }
    }
  }

  private term(): "atom" | "anchor" | "complexGroup" {
    const c = this.at() ?? "";
    switch (c) {
      case "^":
        this.position++;
        this.tokens.push("^");
        return "anchor";
      case "$":
        this.position++;
        this.tokens.push("$");
        return "anchor";
      case ".":
        this.position++;
        this.tokens.push(dotClass);
        return "atom";
      case "(": {
        this.position++;
        if (this.peek("?")) {
          if (this.at(1) === ":") {
            this.position += 2;
          } else {
            throw new Failure("only (?: ) groups are supported");
          }
        }
        this.tokens.push("(?:");
        const before = this.complexity;
        this.alternation();
        if (!this.peek(")")) {
          throw new Failure("missing ')'");
        }
        this.position++;
        this.tokens.push(")");
        return this.complexity === before ? "atom" : "complexGroup";
      }
      case "[":
        this.characterClass();
        return "atom";
      case "\\":
        this.tokens.push(this.escape());
        return "atom";
      case "*":
      case "+":
      case "?":
      case "{":
        throw new Failure("nothing to repeat");
      case "]":
      case "}":
        throw new Failure(`'${c}' must be escaped`);
      default:
        this.position++;
        this.tokens.push(u(c.charCodeAt(0)));
        return "atom";
    }
  }

  private quantifier(): void {
    const c = this.pattern[this.position++] ?? "";
    let token: string;
    if (c === "{") {
      const min = this.integer();
      if (min === null) {
        throw new Failure("expected a number in { }");
      }
      let max: number | null = min;
      if (this.peek(",")) {
        this.position++;
        max = this.integer();
      }
      if (!this.peek("}")) {
        throw new Failure("missing '}'");
      }
      this.position++;
      if (min > maxRepeat || (max !== null && max > maxRepeat)) {
        throw new Failure(`repetitions are limited to ${maxRepeat}`);
      }
      if (max !== null && max < min) {
        throw new Failure("{n,m} with m < n");
      }
      token = max === min ? `{${min}}` : max === null ? `{${min},}` : `{${min},${max}}`;
    } else {
      token = c;
    }
    if (this.peek("?")) {
      this.position++;
      token += "?";
    }
    this.tokens.push(token);
    const next = this.at();
    if (next === "*" || next === "+" || next === "{" || next === "?") {
      throw new Failure("nested quantifier");
    }
  }

  private integer(): number | null {
    const start = this.position;
    while (this.position - start < 5 && /^[0-9]$/.test(this.at() ?? "")) {
      this.position++;
    }
    return this.position === start ? null : Number(this.pattern.slice(start, this.position));
  }

  private characterClass(): void {
    this.position++;
    let result = "[";
    if (this.peek("^")) {
      this.position++;
      result += "^";
    }
    let count = 0;
    while (!this.peek("]")) {
      if (this.position >= this.pattern.length) {
        throw new Failure("missing ']'");
      }
      const low = this.classAtom();
      if (low.set !== null) {
        result += low.set;
      } else if (this.peek("-") && this.at(1) !== undefined && this.at(1) !== "]") {
        this.position++;
        const high = this.classAtom();
        if (high.set !== null || high.code < low.code) {
          throw new Failure("invalid range");
        }
        result += u(low.code) + "-" + u(high.code);
      } else {
        result += u(low.code);
      }
      count++;
    }
    if (count === 0) {
      throw new Failure("empty class");
    }
    this.position++;
    this.tokens.push(result + "]");
  }

  private classAtom(): { readonly code: number; readonly set: string | null } {
    const c = this.at() ?? "";
    if (c === "[") {
      throw new Failure("'[' must be escaped in a class");
    }
    if (c !== "\\") {
      this.position++;
      return { code: c.charCodeAt(0), set: null };
    }
    const next = this.at(1);
    if (next === undefined) {
      throw new Failure("incomplete escape");
    }
    switch (next) {
      case "d":
        this.position += 2;
        return { code: 0, set: digitSet };
      case "w":
        this.position += 2;
        return { code: 0, set: wordSet };
      case "s":
        this.position += 2;
        return { code: 0, set: spaceSet };
      case "D":
      case "W":
      case "S":
        throw new Failure(`\\${next} is not supported in a class`);
      default:
        return { code: this.escapedChar(), set: null };
    }
  }

  private escape(): string {
    const next = this.at(1);
    if (next === undefined) {
      throw new Failure("incomplete escape");
    }
    const sets: Record<string, string> = {
      d: "[" + digitSet + "]",
      D: "[^" + digitSet + "]",
      w: "[" + wordSet + "]",
      W: "[^" + wordSet + "]",
      s: "[" + spaceSet + "]",
      S: "[^" + spaceSet + "]",
    };
    const set = sets[next];
    if (set !== undefined) {
      this.position += 2;
      return set;
    }
    return u(this.escapedChar());
  }

  private escapedChar(): number {
    const next = this.at(1) ?? "";
    let code: number;
    if (next === "t") {
      code = 0x09;
    } else if (next === "n") {
      code = 0x0a;
    } else if (next === "r") {
      code = 0x0d;
    } else if (next !== "" && metacharacters.includes(next)) {
      code = next.charCodeAt(0);
    } else {
      throw new Failure(`unsupported escape \\${next}`);
    }
    this.position += 2;
    return code;
  }
}

/** Checks a pattern of the safe subset and returns its canonical JavaScript form. */
export function parseSafeRegex(pattern: string): SafeRegexResult {
  if (pattern.length > maxPatternLength) {
    return {
      ok: false,
      position: 0,
      message: `a pattern has at most ${maxPatternLength} characters`,
    };
  }
  return new Parser(pattern).run();
}

const cache = new Map<string, RegExp | null>();

/** True when the pattern is found in `value`; an invalid pattern never matches. */
export function safeRegexTest(pattern: string, value: string): boolean {
  let regex = cache.get(pattern);
  if (regex === undefined) {
    const parsed = parseSafeRegex(pattern);
    // The canonical pattern is built from a validated subset: no user input reaches RegExp unchecked.
    regex = parsed.ok ? new RegExp(parsed.javaScript) : null;
    cache.set(pattern, regex);
  }
  return regex?.test(value) ?? false;
}
