/** Characters that Windows, macOS or Linux refuse in a file name. */
// eslint-disable-next-line no-control-regex -- control characters are exactly what must go
const forbidden = /[<>:"/\\|?*\u0000-\u001f]/g;

/** Folder name for a new project: the name without the characters file systems refuse. */
export function folderNameOf(name: string): string {
  return name
    .trim()
    .replace(forbidden, "")
    .replace(/[. ]+$/, "");
}

/** Joins a parent folder and a child name with the separator the parent already uses. */
export function joinPath(parent: string, child: string): string {
  const separator = parent.includes("\\") && !parent.includes("/") ? "\\" : "/";
  const trimmed = parent.replace(/[\\/]+$/, "");
  return trimmed === "" ? child : `${trimmed}${separator}${child}`;
}

/** Last segment of a path: the default name proposed when a folder becomes a project. */
export function baseName(path: string): string {
  return (
    path
      .replace(/[\\/]+$/, "")
      .split(/[\\/]/)
      .pop() ?? ""
  );
}
