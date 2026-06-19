import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const css = ["app.css", "layout.css", "pages.css", "components.css"]
  .map((file) => readFileSync(resolve(process.cwd(), "src/styles", file), "utf8"))
  .join("\n");

describe("prototype style contract", () => {
  it("contains shell and Dashboard layout rules", () => {
    expect(css).toContain("--sidebar: 224px");
    expect(css).toContain("--topbar: 58px");
    expect(css).toContain(".sidebar.open");
    expect(css).toContain(".sidebar-backdrop");
    expect(css).toContain("grid-template-columns: 1.25fr 0.75fr");
    expect(css).toContain(".zone-grid");
    expect(css).toContain("@media (max-width: 780px)");
  });
});
