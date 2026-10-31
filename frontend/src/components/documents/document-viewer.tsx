"use client";

import { useEffect, useState } from "react";
import { BookOpen, Download, FileCode2, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { MarkdownViewer, extractHeadings } from "./markdown-viewer";
import { ApiReferenceTable } from "./api-reference-table";
import { ArchitectureDiagramView } from "./architecture-diagram";
import {
  jobsApi,
  type ArchitectureDiagram,
  type ArtifactFileEntry,
  type DocumentPreviewResponse,
  type TechnicalSkeleton,
} from "@/lib/jobs";

type Tab = "userGuide" | "technical";

export function DocumentViewer({
  jobId,
  artifacts,
}: {
  jobId: string;
  artifacts: ArtifactFileEntry[];
}) {
  const [tab, setTab] = useState<Tab>("userGuide");

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
      <div
        role="tablist"
        aria-label="Document type"
        className="flex gap-2 border-b border-border"
      >
        <TabButton
          active={tab === "userGuide"}
          onClick={() => setTab("userGuide")}
          id="tab-userGuide"
          controls="panel-userGuide"
        >
          <BookOpen className="size-4" />
          User Manual / راهنمای کاربر
        </TabButton>
        <TabButton
          active={tab === "technical"}
          onClick={() => setTab("technical")}
          id="tab-technical"
          controls="panel-technical"
        >
          <FileCode2 className="size-4" />
          Technical Specification
        </TabButton>
      </div>

      <div role="tabpanel" id="panel-userGuide" hidden={tab !== "userGuide"}>
        {tab === "userGuide" ? (
          <UserGuideTab jobId={jobId} artifacts={artifacts} />
        ) : null}
      </div>
      <div role="tabpanel" id="panel-technical" hidden={tab !== "technical"}>
        {tab === "technical" ? (
          <TechnicalTab jobId={jobId} artifacts={artifacts} />
        ) : null}
      </div>
    </div>
  );
}

function TabButton({
  active,
  onClick,
  children,
  id,
  controls,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
  id: string;
  controls: string;
}) {
  return (
    <button
      role="tab"
      id={id}
      aria-controls={controls}
      aria-selected={active}
      onClick={onClick}
      className={`inline-flex items-center gap-2 rounded-t-lg border-b-2 px-4 py-2.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${
        active
          ? "border-primary text-foreground"
          : "border-transparent text-muted-foreground hover:text-foreground"
      }`}
    >
      {children}
    </button>
  );
}

function DownloadLinks({
  jobId,
  artifacts,
  documentType,
}: {
  jobId: string;
  artifacts: ArtifactFileEntry[];
  documentType: "technical" | "userGuide";
}) {
  const relevant = artifacts.filter((a) => a.documentType === documentType);
  const byFormat = (format: string) =>
    relevant.find((a) => a.format === format && a.status === "available");

  const markdown = byFormat("markdown");
  const pdf = byFormat("pdf");
  const docx = byFormat("docx");

  return (
    <div className="flex flex-wrap gap-2">
      {markdown ? (
        <Button variant="outline" size="sm" asChild>
          <a href={jobsApi.downloadUrl(jobId, markdown.artifactId)}>
            <Download className="size-3.5" />
            Markdown
          </a>
        </Button>
      ) : null}
      {pdf ? (
        <Button variant="outline" size="sm" asChild>
          <a href={jobsApi.downloadUrl(jobId, pdf.artifactId)}>
            <Download className="size-3.5" />
            PDF
          </a>
        </Button>
      ) : null}
      {docx ? (
        <Button variant="outline" size="sm" asChild>
          <a href={jobsApi.downloadUrl(jobId, docx.artifactId)}>
            <Download className="size-3.5" />
            DOCX
          </a>
        </Button>
      ) : null}
    </div>
  );
}

function UserGuideTab({
  jobId,
  artifacts,
}: {
  jobId: string;
  artifacts: ArtifactFileEntry[];
}) {
  return (
    <DocumentPane
      jobId={jobId}
      documentType="userGuide"
      artifacts={artifacts}
      extra={<div />}
    />
  );
}

function TechnicalTab({
  jobId,
  artifacts,
}: {
  jobId: string;
  artifacts: ArtifactFileEntry[];
}) {
  const [technical, setTechnical] = useState<TechnicalSkeleton | null>(null);
  const [diagram, setDiagram] = useState<ArchitectureDiagram | null>(null);

  useEffect(() => {
    let cancelled = false;
    jobsApi
      .technical(jobId)
      .then((data) => {
        if (!cancelled) setTechnical(data);
      })
      .catch(() => {});
    jobsApi
      .diagram(jobId)
      .then((data) => {
        if (!cancelled) setDiagram(data);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [jobId]);

  const extra = (
    <div className="space-y-8">
      <section>
        <h2 className="mb-3 text-lg font-semibold">API Reference</h2>
        {technical ? (
          <ApiReferenceTable endpoints={technical.endpoints} />
        ) : (
          <p className="text-sm text-muted-foreground">Loading API reference…</p>
        )}
      </section>

      <section>
        <h2 className="mb-3 text-lg font-semibold">Architecture Diagram</h2>
        {diagram ? (
          <ArchitectureDiagramView diagram={diagram} />
        ) : (
          <p className="text-sm text-muted-foreground">Loading diagram…</p>
        )}
      </section>
    </div>
  );

  return (
    <DocumentPane
      jobId={jobId}
      documentType="technical"
      artifacts={artifacts}
      extra={extra}
    />
  );
}

function DocumentPane({
  jobId,
  documentType,
  artifacts,
  extra,
}: {
  jobId: string;
  documentType: "technical" | "userGuide";
  artifacts: ArtifactFileEntry[];
  extra: React.ReactNode;
}) {
  const [preview, setPreview] = useState<DocumentPreviewResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    jobsApi
      .document(jobId, documentType)
      .then((data) => {
        if (!cancelled) setPreview(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load.");
      });
    return () => {
      cancelled = true;
    };
  }, [jobId, documentType]);

  if (error) {
    return <p className="mt-6 text-sm text-destructive">{error}</p>;
  }

  if (!preview) {
    return (
      <div className="mt-12 flex items-center justify-center gap-2 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin" />
        Loading document…
      </div>
    );
  }

  if (preview.status === "notApplicable") {
    return (
      <p className="mt-6 text-sm text-muted-foreground">
        No {documentType === "technical" ? "technical specification" : "user guide"}{" "}
        is available for this project — insufficient source evidence.
      </p>
    );
  }

  const dir: "ltr" | "rtl" = preview.language === "fa" ? "rtl" : "ltr";
  const headings = extractHeadings(preview.content);

  return (
    <div className="mt-6">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <DownloadLinks jobId={jobId} artifacts={artifacts} documentType={documentType} />
        {preview.status === "available" && preview.content ? null : (
          <Badge variant="secondary">partial</Badge>
        )}
      </div>

      <div className="grid gap-8 lg:grid-cols-[200px_minmax(0,1fr)]">
        <aside className="hidden lg:block">
          <p className="mb-2 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
            On this page
          </p>
          <nav aria-label="Table of contents">
            <ul className="space-y-1 border-l border-border">
              {headings.map((heading, i) => (
                <li key={i} style={{ paddingLeft: `${(heading.level - 1) * 10}px` }}>
                  <span
                    className={`block truncate py-0.5 text-xs ${
                      heading.level === 1
                        ? "font-medium text-foreground"
                        : "text-muted-foreground"
                    }`}
                  >
                    {heading.text}
                  </span>
                </li>
              ))}
              {headings.length === 0 ? (
                <li className="text-xs text-muted-foreground">No sections</li>
              ) : null}
            </ul>
          </nav>
        </aside>

        <div className="min-w-0">
          <article>
            <MarkdownViewer markdown={preview.content} dir={dir} />
          </article>
          {extra}
        </div>
      </div>
    </div>
  );
}
