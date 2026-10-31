"use client";

import { useMemo, useState } from "react";
import { Search } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import type { EndpointModel } from "@/lib/jobs";

export function ApiReferenceTable({ endpoints }: { endpoints: EndpointModel[] }) {
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return endpoints;
    return endpoints.filter((e) => {
      const haystack = [
        e.path,
        e.method,
        e.controllerName,
        e.actionName,
        e.requestDto?.typeName,
        e.responseDto?.typeName,
        e.roles.join(" "),
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();
      return haystack.includes(q);
    });
  }, [endpoints, query]);

  if (endpoints.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        No API endpoints were detected for this project.
      </p>
    );
  }

  return (
    <div>
      <div className="relative mb-3 max-w-sm">
        <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="search"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search by path, method, controller…"
          aria-label="Search API endpoints"
          className="pl-8"
        />
      </div>

      <div className="overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-left">
            <tr>
              <th className="px-3 py-2 font-semibold">Method</th>
              <th className="px-3 py-2 font-semibold">Path</th>
              <th className="px-3 py-2 font-semibold">Controller / Action</th>
              <th className="px-3 py-2 font-semibold">Auth</th>
              <th className="px-3 py-2 font-semibold">Request DTO</th>
              <th className="px-3 py-2 font-semibold">Response DTO</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((endpoint, index) => (
              <tr key={index} className="border-t border-border">
                <td className="px-3 py-2">
                  <MethodBadge method={endpoint.method} />
                </td>
                <td className="px-3 py-2 font-mono text-[0.85em]">{endpoint.path}</td>
                <td className="px-3 py-2">
                  {endpoint.controllerName}
                  {endpoint.actionName ? ` · ${endpoint.actionName}` : ""}
                </td>
                <td className="px-3 py-2">
                  {endpoint.authRequired ? (
                    <span className="text-xs">
                      {endpoint.roles.length > 0
                        ? endpoint.roles.join(", ")
                        : "Required"}
                    </span>
                  ) : (
                    <span className="text-xs text-muted-foreground">—</span>
                  )}
                </td>
                <td className="px-3 py-2 font-mono text-[0.85em]">
                  {endpoint.requestDto?.typeName ?? "—"}
                </td>
                <td className="px-3 py-2 font-mono text-[0.85em]">
                  {endpoint.responseDto?.typeName ?? "—"}
                </td>
              </tr>
            ))}
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-3 py-4 text-center text-muted-foreground">
                  No endpoints match “{query}”.
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function MethodBadge({ method }: { method: string }) {
  const color =
    method === "GET"
      ? "bg-emerald-500/15 text-emerald-600 dark:text-emerald-400"
      : method === "POST"
        ? "bg-blue-500/15 text-blue-600 dark:text-blue-400"
        : method === "PUT" || method === "PATCH"
          ? "bg-amber-500/15 text-amber-600 dark:text-amber-400"
          : method === "DELETE"
            ? "bg-red-500/15 text-red-600 dark:text-red-400"
            : "bg-muted text-muted-foreground";

  return (
    <Badge variant="secondary" className={`font-mono ${color}`}>
      {method || "—"}
    </Badge>
  );
}
