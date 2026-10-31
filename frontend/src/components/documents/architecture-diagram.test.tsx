import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ArchitectureDiagramView } from "./architecture-diagram";
import type { ArchitectureDiagram } from "@/lib/jobs";

describe("ArchitectureDiagramView", () => {
  it("shows an unavailable state when there is no evidence", () => {
    render(
      <ArchitectureDiagramView
        diagram={{ available: false, nodes: [], edges: [], unavailableReason: "Nothing found" }}
      />
    );
    expect(screen.getByText("Diagram unavailable")).toBeInTheDocument();
    expect(screen.getByText("Nothing found")).toBeInTheDocument();
  });

  it("renders nodes and edges from validated data", () => {
    const diagram: ArchitectureDiagram = {
      available: true,
      nodes: [
        { id: "repo:frontend", kind: "repository", label: "frontend" },
        { id: "page:p1", kind: "page", label: "/invoices/new" },
        { id: "endpoint:POST:/api/invoices", kind: "endpoint", label: "POST /api/invoices" },
      ],
      edges: [
        { fromId: "page:p1", toId: "endpoint:POST:/api/invoices", kind: "correlates", inferred: false },
      ],
    };
    const { container } = render(<ArchitectureDiagramView diagram={diagram} />);

    expect(screen.getByRole("img", { name: /architecture diagram/i })).toBeInTheDocument();
    expect(container.querySelector("svg")).not.toBeNull();
    expect(container.querySelectorAll("rect").length).toBe(3);
    expect(container.querySelectorAll("line").length).toBe(1);
  });

  it("escapes node labels rather than injecting markup", () => {
    const diagram: ArchitectureDiagram = {
      available: true,
      nodes: [{ id: "endpoint:x", kind: "endpoint", label: "<img src=x onerror=alert(1)>" }],
      edges: [],
    };
    const { container } = render(<ArchitectureDiagramView diagram={diagram} />);
    // The label is rendered as text, never as an element.
    expect(container.querySelector("img")).toBeNull();
  });
});
