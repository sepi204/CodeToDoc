import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MarkdownViewer, extractHeadings } from "./markdown-viewer";

const headingMarkdown = `# Overview

Hello **world**.`;

const tableMarkdown = `| A | B |
|---|---|
| 1 | 2 |`;

describe("extractHeadings", () => {
  it("extracts heading levels and text", () => {
    const markdown = `# Title

## Section

### Subsection

text`;
    expect(extractHeadings(markdown)).toEqual([
      { level: 1, text: "Title" },
      { level: 2, text: "Section" },
      { level: 3, text: "Subsection" },
    ]);
  });

  it("returns an empty list for markdown without headings", () => {
    expect(extractHeadings("just a paragraph")).toEqual([]);
  });

  it("preserves Persian heading text", () => {
    expect(extractHeadings("# راهنمای کاربر")).toEqual([
      { level: 1, text: "راهنمای کاربر" },
    ]);
  });
});

describe("MarkdownViewer", () => {
  it("renders headings and paragraphs", () => {
    render(<MarkdownViewer markdown={headingMarkdown} />);
    expect(screen.getByRole("heading", { level: 1, name: "Overview" })).toBeInTheDocument();
    expect(screen.getByText(/Hello/)).toBeInTheDocument();
  });

  it("renders tables safely", () => {
    render(<MarkdownViewer markdown={tableMarkdown} />);
    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByText("A")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("does not execute raw script tags", () => {
    const markdown = `# Title

<script>window.__pwned = true;</script>`;
    const { container } = render(<MarkdownViewer markdown={markdown} />);
    // No <script> element should be created, and no global is set.
    expect(container.querySelector("script")).toBeNull();
    expect((window as unknown as { __pwned?: boolean }).__pwned).toBeUndefined();
  });

  it("strips javascript: links", () => {
    const { container } = render(
      <MarkdownViewer markdown="[click](javascript:alert(1))" />
    );
    // The dangerous protocol is removed entirely — no anchor is produced.
    expect(container.querySelector("a")).toBeNull();
  });

  it("keeps safe links", () => {
    const { container } = render(
      <MarkdownViewer markdown="[click](https://example.com)" />
    );
    const anchor = container.querySelector("a");
    expect(anchor).not.toBeNull();
    expect(anchor?.getAttribute("href")).toBe("https://example.com");
  });

  it("renders Persian content with RTL direction", () => {
    const { container } = render(
      <MarkdownViewer dir="rtl" markdown="# راهنمای کاربر" />
    );
    expect(container.querySelector("[dir='rtl']")).toBeInTheDocument();
  });
});
