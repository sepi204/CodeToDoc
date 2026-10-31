"use client";

import { useEffect, useRef, useState } from "react";
import { CircleAlert, CheckCircle2, Loader2, Upload, XCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { DocumentViewer } from "./document-viewer";
import { jobsApi, type JobStatusResponse } from "@/lib/jobs";

const TERMINAL = new Set([
  "completed",
  "completedWithWarnings",
  "failed",
  "cancelled",
]);

export function DocumentDelivery() {
  const [jobId, setJobId] = useState<string | null>(() => {
    if (typeof window === "undefined") return null;
    return new URLSearchParams(window.location.search).get("job");
  });
  const [status, setStatus] = useState<JobStatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const timerRef = useRef<number | null>(null);

  // Poll the status endpoint until the job reaches a terminal state.
  useEffect(() => {
    if (!jobId) return;
    let cancelled = false;

    const poll = async () => {
      try {
        const next = await jobsApi.status(jobId);
        if (cancelled) return;
        setStatus(next);
        if (!TERMINAL.has(next.status)) {
          timerRef.current = window.setTimeout(poll, 1000);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load job status.");
        }
      }
    };

    poll();

    return () => {
      cancelled = true;
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
      }
    };
  }, [jobId]);

  const handleFiles = async (files: File[]) => {
    if (files.length === 0) return;
    setError(null);
    setUploading(true);
    try {
      const started = await jobsApi.start(Array.from(files));
      setJobId(started.jobId);
      setStatus(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed.");
    } finally {
      setUploading(false);
    }
  };

  const startOver = () => {
    setJobId(null);
    setStatus(null);
    setError(null);
    window.history.replaceState(null, "", "/documents");
  };

  if (status && TERMINAL.has(status.status)) {
    if (status.status === "completed" || status.status === "completedWithWarnings") {
      return (
        <div>
          {status.status === "completedWithWarnings" ? (
            <div className="mx-auto max-w-6xl px-4 pt-6 sm:px-6">
              <div className="flex items-start gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm">
                <CircleAlert className="mt-0.5 size-5 shrink-0 text-amber-600" />
                <span>Some artifacts could not be generated — available documents are shown below.</span>
              </div>
            </div>
          ) : null}
          <DocumentViewer jobId={status.jobId} artifacts={status.artifacts} />
        </div>
      );
    }

    return (
      <div className="mx-auto max-w-xl px-4 py-20 text-center sm:px-6">
        {status.status === "cancelled" ? <XCircle className="mx-auto size-10 text-muted-foreground" /> : <CircleAlert className="mx-auto size-10 text-destructive" />}
        <h1 className="mt-4 text-2xl font-bold tracking-tight">
          {status.status === "cancelled" ? "Processing cancelled" : "Processing failed"}
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          {status.diagnostics.length > 0
            ? status.diagnostics[0]
            : "The uploaded archives could not be processed."}
        </p>
        <Button onClick={startOver} className="mt-6">
          Start over
        </Button>
      </div>
    );
  }

  if (status) {
    return (
      <div className="mx-auto max-w-xl px-4 py-20 text-center sm:px-6">
        <Loader2 className="mx-auto size-10 animate-spin text-primary" />
        <h1 className="mt-4 text-2xl font-bold tracking-tight">Processing</h1>
        <p className="mt-2 text-sm text-muted-foreground">{status.progressMessage || "Working…"}</p>
        <div className="mt-6 flex justify-center gap-1.5">
          {status.phases.map((phase) => (
            <Badge key={phase.phase} variant="secondary">
              {phase.phase} · {phase.status}
            </Badge>
          ))}
        </div>
        <Button variant="outline" onClick={() => jobsApi.cancel(status.jobId)} className="mt-6">
          Cancel
        </Button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl px-4 py-20 sm:px-6">
      <div className="text-center">
        <h1 className="text-3xl font-bold tracking-tight">Generate project documentation</h1>
        <p className="mt-3 text-muted-foreground">
          Upload one or more ZIP archives. CodeToDoc analyzes them and produces a
          user guide and a technical specification as Markdown, PDF, and DOCX.
        </p>
      </div>

      {error ? (
        <div className="mt-6 flex items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4">
          <CircleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />
          <p className="text-sm">{error}</p>
        </div>
      ) : null}

      <div
        className="mt-8 rounded-2xl border-2 border-dashed border-border bg-card/50 p-10 text-center"
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => {
          event.preventDefault();
          handleFiles(Array.from(event.dataTransfer.files).filter((f) => f.name.endsWith(".zip")));
        }}
      >
        <Upload className="mx-auto size-8 text-muted-foreground" />
        <p className="mt-3 text-sm font-medium">Drop ZIP archives here</p>
        <p className="mt-1 text-xs text-muted-foreground">or select files from your computer</p>

        <input
          ref={fileInputRef}
          type="file"
          accept=".zip,application/zip"
          multiple
          className="hidden"
          onChange={(event) => {
            if (event.target.files) handleFiles(Array.from(event.target.files));
            event.target.value = "";
          }}
        />

        <Button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={uploading}
          className="mt-5 gap-2"
        >
          {uploading ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
          {uploading ? "Uploading…" : "Choose ZIP files"}
        </Button>
      </div>

      <div className="mt-6 flex items-center justify-center gap-2 text-xs text-muted-foreground">
        <CheckCircle2 className="size-3.5" />
        Processing runs securely in the background — no source code is shared with a
        third-party LLM.
      </div>
    </div>
  );
}
