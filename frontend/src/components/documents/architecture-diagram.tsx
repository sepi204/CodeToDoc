"use client";

import type { ArchitectureDiagram, DiagramNode } from "@/lib/jobs";

/**
 * Renders a deterministic SVG architecture diagram from validated skeleton
 * data. Node labels are emitted as React text (automatically escaped); no
 * user-controlled markup or scripts are rendered.
 */
export function ArchitectureDiagramView({ diagram }: { diagram: ArchitectureDiagram }) {
  if (!diagram.available) {
    return (
      <div className="rounded-lg border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
        <p className="font-medium">Diagram unavailable</p>
        <p className="mt-1">{diagram.unavailableReason ?? "Insufficient evidence."}</p>
      </div>
    );
  }

  const columnX: Record<string, number> = {
    repository: 40,
    page: 320,
    endpoint: 600,
  };
  const width: Record<string, number> = {
    repository: 180,
    page: 180,
    endpoint: 200,
  };

  const byKind = new Map<string, DiagramNode[]>();
  for (const node of diagram.nodes) {
    const list = byKind.get(node.kind) ?? [];
    list.push(node);
    byKind.set(node.kind, list);
  }

  const rowHeight = 56;
  const positions = new Map<string, { x: number; y: number; kind: string }>();
  let maxRows = 1;

  for (const [kind, nodes] of byKind.entries()) {
    maxRows = Math.max(maxRows, nodes.length);
    nodes.forEach((node, i) => {
      positions.set(node.id, {
        x: columnX[kind] ?? 40,
        y: 40 + i * rowHeight,
        kind,
      });
    });
  }

  const svgWidth = 840;
  const svgHeight = 40 + maxRows * rowHeight + 20;

  return (
    <div className="overflow-x-auto">
      <svg
        viewBox={`0 0 ${svgWidth} ${svgHeight}`}
        role="img"
        aria-label="Architecture diagram derived from validated project data"
        className="w-full min-w-[640px] rounded-lg border border-border bg-background"
      >
        {/* Edges */}
        {diagram.edges.map((edge, i) => {
          const from = positions.get(edge.fromId);
          const to = positions.get(edge.toId);
          if (!from || !to) return null;
          const x1 = from.x + (width[from.kind] ?? 180);
          const y1 = from.y + 20;
          const x2 = to.x;
          const y2 = to.y + 20;
          return (
            <g key={i}>
              <line
                x1={x1}
                y1={y1}
                x2={x2}
                y2={y2}
                stroke={edge.inferred ? "#f59e0b" : "#64748b"}
                strokeWidth={edge.inferred ? 1.5 : 1}
                strokeDasharray={edge.inferred ? "4 3" : undefined}
              />
              <circle cx={x2} cy={y2} r={3} fill={edge.inferred ? "#f59e0b" : "#64748b"} />
            </g>
          );
        })}

        {/* Nodes */}
        {diagram.nodes.map((node) => {
          const pos = positions.get(node.id);
          if (!pos) return null;
          const w = width[pos.kind] ?? 180;
          return (
            <g key={node.id}>
              <rect
                x={pos.x}
                y={pos.y}
                width={w}
                height={40}
                rx={6}
                fill={fillFor(pos.kind)}
                stroke="#cbd5e1"
                strokeWidth={1}
              />
              <text
                x={pos.x + w / 2}
                y={pos.y + 24}
                textAnchor="middle"
                fontSize={12}
                fontFamily="ui-monospace, SFMono-Regular, Menlo, monospace"
                fill="#0f172a"
              >
                {node.label}
              </text>
            </g>
          );
        })}
      </svg>

      <div className="mt-2 flex flex-wrap gap-4 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded bg-sky-100" /> Repository
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded bg-emerald-100" /> Page
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded bg-violet-100" /> Endpoint
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-0.5 w-4 bg-slate-400" /> Correlated
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-0.5 w-4 border-t border-dashed border-amber-500" /> Inferred
        </span>
      </div>
    </div>
  );
}

function fillFor(kind: string): string {
  switch (kind) {
    case "repository":
      return "#e0f2fe";
    case "page":
      return "#dcfce7";
    case "endpoint":
      return "#ede9fe";
    default:
      return "#f1f5f9";
  }
}
