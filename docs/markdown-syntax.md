# Erable — Extended Markdown syntax

> Complements `docs/architecture.md` §6. Executable specification: `spec/fixtures/markdown/*.json`, run by xUnit (`Erable.Core.Tests`) **and** Vitest (`src/web`). A rule of this document without a fixture case is not a rule.

The backend (Markdig) **extracts** references, headings and plain text; the frontend (micromark / remark) **renders** the page. Both implementations follow this grammar and are checked against the same cases.

---

## 1. Basis

- **CommonMark 0.31 + GFM**: tables, task lists, strikethrough (`~~`), extended autolinks (`www.…`, `https://…`), footnotes.
- **Raw HTML disabled** (§A-9, F04.R5): `<img src=x onerror=…>` is not a tag but text, shown escaped. `<https://…>` autolinks remain links.
- The extensions below are **inline** constructs: they are recognized wherever CommonMark parses inline text (paragraphs, headings, lists, block quotes, table cells, link text) and **nowhere else**: not in inline code, not in a code block (indented or fenced), not in a link destination.

## 2. Grammar

```abnf
reference   = "[[" id [ "#" anchor ] [ separator label ] "]]"
embed       = "![[" id [ separator label ] "]]" [ attributes ]
separator   = "|" / "\|"                    ; "\|" required in a table (R12)

id          = 16HEXDIG                      ; case-insensitive, other than 0000000000000000
anchor      = 1*( ALPHA / DIGIT / "-" / "_" )
label       = 0*200( any character except line ending ) ; stops at the first "]]"
attributes  = "{" attribute *( 1*SP attribute ) "}" ; at most 64 characters between the braces
attribute   = "width=" width / "align=" align
width       = 1*4DIGIT                      ; integer from 1 to 4096, without a leading zero
align       = "left" / "center" / "right"
```

### 2.1 Rules

| # | Rule | Example |
|---|---|---|
| R1 | The ID is **exactly** 16 hexadecimal characters, in any case. 15 or 17 characters, a non-hexadecimal character or the null ID: **not** a reference, the text stays as it is. | `[[3F2A9C14E07B5D2]]` → text |
| R2 | The **label** starts after the first `\|` and stops at the **first** `]]`. It may contain `\|`, `#`, `[` or a lone `]`. Its leading and trailing spaces are trimmed; an empty label is the same as no label. No line break. | `[[ID\|a \| b]]` → label `a \| b` |
| R3 | The **anchor** follows `#` and precedes `\|` or `]]`; it only allows ASCII letters, digits, `-` and `_`. Otherwise, no reference. | `[[ID#early-years]]` |
| R4 | An **embed** (`![[…]]`) has no anchor: `![[ID#x]]` is neither an embed nor a reference, it stays text (CommonMark sees an image without a destination). | |
| R5 | **Attributes** only apply to an embed, right after `]]`. Known keys only (`width`, `align`), each at most once, separated by spaces. An invalid attribute block is not consumed: it stays text. | `![[ID]]{width=320 align=right}` |
| R6 | **Escaping**: `\[[ID]]` is not a reference (the first `[` is escaped). There is no other escape inside a reference. | |
| R7 | **Code**: nothing is recognized in inline code or in a code block. | `` `[[ID]]` `` → code |
| R8 | **Nested links**: a reference in the text of a Markdown link is extracted (it counts in the incoming links); when rendered, it is not separately clickable (no link inside a link). `[[ID]](https://x)` is a reference followed by the text `(https://x)`. | `[see [[ID]]](https://x)` |
| R9 | **Brackets**: `[[[ID]]]` gives `[`, the reference, then `]`. | |
| R10 | **Position**: `start` is the index (in UTF-16 units, the same in C# and JavaScript) of the first character (`[` or `!`); `end` is the index after the last consumed character, attributes included. | |
| R11 | **Bounded lengths**: a label longer than 200 characters (before trimming) or an attribute block longer than 64 characters is not recognized. This bound guarantees linear parsing time even on a hostile line (`[[ID\|[[ID\|…` without end). | |
| R12 | **Tables**: in a GFM table row, an unescaped vertical bar separates cells **before** any inline parsing: a labelled reference written with a bare bar is cut into two cells there and is not a reference. The separator is written `\|`, preceded by a backslash. Everywhere, this escaped form is accepted as a separator, and an escaped bar inside a label stands for a plain bar. | see `refs.json` › *table* |

### 2.2 Plain text of a reference

For plain text (full-text search) and the computation of heading anchors, a reference stands for **its label**, or nothing without a label: the name of the target may change, and it must not alter the indexed text of the page that cites it nor the anchors of its headings.

## 3. Headings and anchors

- Every heading gets an anchor. By default it is the **slug** of its plain text.
- **Explicit anchor**: a heading that ends with ` {#anchor}` (a space, then `{#`, an anchor as in R3, then `}`) takes that anchor, and the suffix is removed from the displayed text. `## History {#history}`.
- **Duplicates** in a page: the second occurrence of an anchor gets `-1`, the third `-2`, etc. (like GitHub). Explicit anchors take part in the count.

### 3.1 Slug

1. Replace ligatures and letters without decomposition: `œ→oe`, `æ→ae`, `ß→ss`, `ø→o`, `đ→d`, `ł→l` (upper case included);
2. decompose to NFD and remove diacritical marks (Unicode category `Mn`);
3. convert to lower case (invariant);
4. replace each run of characters outside `[a-z0-9]` with a single `-`;
5. remove leading and trailing `-`; an empty result becomes `section`.

"Les années de cartographe" → `les-annees-de-cartographe`.

## 4. Plain text

Used for the full-text index and compared between the two implementations after **normalization**: every run of whitespace (line breaks included) becomes one space, then the whole is trimmed.

- Text of every block, code blocks and inline code included;
- references: §2.2; embeds: their label;
- links and images: their text (not the URL); autolinks: the URL;
- table cells separated by spaces; the checkbox of a task is ignored;
- the `{#anchor}` suffix of a heading is excluded.

## 5. Out of scope

- No reference in attributes, link destinations or reference definitions (`[x]: url`).
- No section embed (`![[ID#anchor]]`).
- No attribute other than `width` and `align`; any addition goes through this document, then through the fixtures.

## 6. Safety limits

The parsers (Markdig on the server, remark/micromark in the browser) take **quadratic** time, or even overflow the stack, on a few dozen KB of degenerate input: `]` repeated 40,000 times freezes the browser for about 40 s, `> ` repeated 8,000 times throws a `RangeError`. A single editor could thus freeze their colleagues or saturate the server. A **linear** pre-scan rejects these shapes before any parser, on the server (`MarkdownLimits`, error 422, nothing is written) and in the client (message before sending). A real page comes nowhere near these values.

| Rule | Limit | Error code |
|---|---|---|
| Page size | 1,048,576 UTF-16 units (≈ 1 MB) | `page.content.tooLarge` |
| Line length | 10,000 characters | `page.content.lineTooLong` |
| Repetition of `[` `]` `(` `)` `>` | 256 in a row | `page.content.repeatedCharacter` |
| Block quote and list markers at the start of a line (`> > 1. - `) | 32; indentation: 256 spaces or tabs | `page.content.tooNested` |
| Consecutive lines without an empty line | 2,000 | `page.content.paragraphTooLong` |

- The first rule broken, in the order of this document, is reported with its line (1-based; 0 for the size). `\r\n` and `\n` are equivalent.
- These limits **block** saving, unlike property rules, which warn (§A-5.4): they are safeguards, not business rules.
- A page already saved that exceeds them (imported, or written before they existed) stays readable: it is shown as **plain text**, without formatting or table of contents, and the server indexes it as text.
- Shared cases: `spec/fixtures/limits/limits.json` (`md` + `unit` repeated `count` times), run by xUnit and Vitest.
