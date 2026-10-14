import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HelloPage } from "./HelloPage";

describe("HelloPage", () => {
  it("renders the application name as main heading", () => {
    render(<HelloPage />);
    expect(screen.getByRole("heading", { level: 1, name: "Erable" })).toBeInTheDocument();
  });
});
