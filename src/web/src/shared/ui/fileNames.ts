/** Extension shown in the file badge: « ZIP », « SVG » (mockup panel « Pièces jointes »). */
export function extensionOf(fileName: string): string {
  const dot = fileName.lastIndexOf(".");
  return dot > 0 && dot < fileName.length - 1
    ? fileName.slice(dot + 1, dot + 5).toUpperCase()
    : "—";
}
