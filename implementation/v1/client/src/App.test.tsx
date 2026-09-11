import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("application accessibility shell", () => {
  it("offers keyboard users a focusable main-content destination", () => {
    const html = renderToStaticMarkup(<App />);

    expect(html).toContain("href=\"#main-content\"");
    expect(html).toContain("Skip to world controls");
    expect(html).toContain("<main id=\"main-content\"");
    expect(html).toContain("tabindex=\"-1\"");
  });
});
