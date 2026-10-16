import usersFile from "../../../../spec/projects/users.json" with { type: "json" };

/** A test user of the reference project (docs/features.md §2.3). */
export interface TestUser {
  readonly id: string;
  readonly name: string;
  readonly role: "admin" | "editor" | "reader" | null;
  readonly password: string;
}

export const users = Object.fromEntries(
  (usersFile.users as TestUser[]).map((user) => [user.id, user]),
) as Record<"admin" | "camille" | "malo" | "lea" | "externe", TestUser>;
