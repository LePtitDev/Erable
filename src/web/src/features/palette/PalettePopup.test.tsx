import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import { renderWithProviders } from "../../test/render";
import { PalettePopup } from "./PalettePopup";
import type { PaletteItem } from "./paletteSearch";
import type { PalettePopupProps } from "./paletteView";

const orsen: PaletteItem = {
  id: requireElementId("7A1C4E90B3F2D615"),
  kind: "page",
  name: "Capitaine Orsen",
  path: "Pages / Personnages / PNJ",
  label: "Capitaine Orsen",
  haystack: "capitaine orsen",
  words: ["capitaine", "orsen"],
};

function renderPopup(overrides: Partial<PalettePopupProps> = {}) {
  const onPick = vi.fn<PalettePopupProps["onPick"]>(() => true);
  renderWithProviders(
    <PalettePopup
      snapshot={{
        open: true,
        trigger: "typed",
        from: 0,
        embed: false,
        activeOption: null,
        query: "ors",
        at: null,
      }}
      tab="all"
      results={[orsen]}
      active={orsen}
      ownQuery=""
      onOwnQuery={() => undefined}
      onTab={() => undefined}
      onSelect={() => undefined}
      onPick={onPick}
      onClose={() => undefined}
      onMove={() => undefined}
      {...overrides}
    />,
  );
  return { onPick };
}

describe("PalettePopup", () => {
  it("shows the short ID in the highlighted option, which holds no other control", () => {
    renderPopup();

    const option = screen.getByRole("option", { selected: true });
    expect(option).toHaveTextContent("7A1C…D615");
    expect(option.querySelector("button")).toBeNull();
  });

  it("embeds the highlighted option from the footer", async () => {
    const { onPick } = renderPopup();

    await userEvent.click(screen.getByRole("button", { name: /intégrer/i }));

    expect(onPick).toHaveBeenCalledWith(true);
  });

  it("disables the footer actions when no option is highlighted", () => {
    renderPopup({ results: [], active: null });

    expect(screen.getByRole("button", { name: /intégrer/i })).toBeDisabled();
  });
});
