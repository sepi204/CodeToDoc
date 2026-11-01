"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  CircleAlert,
  FileCode2,
  FileText,
  Link2,
  Loader2,
  Pencil,
  Plus,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { FileUploadBox } from "@/components/projects/file-upload-box";
import { ProjectDialog } from "@/components/projects/project-dialog";
import { useProjects } from "@/components/projects/projects-provider";
import {
  createLocalId,
  formatBytes,
  isFileSource,
  isRepoSource,
} from "@/lib/projects";
import { jobsApi, type JobStatusResponse } from "@/lib/jobs";

const TERMINAL = new Set([
  "completed",
  "completedWithWarnings",
  "failed",
  "cancelled",
]);

interface PendingUpload {
  id: string;
  uploading: boolean;
}

export function ProjectDetail({ id }: { id: string }) {
  const router = useRouter();
  const { projects, isLoading, updateProject, addSource, removeSource } =
    useProjects();
  const project = projects.find((p) => p.id === id);

  const [pendingUploads, setPendingUploads] = useState<PendingUpload[]>([
    { id: createLocalId(), uploading: false },
  ]);
  const [repoUrl, setRepoUrl] = useState("");
  const [sourceError, setSourceError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [zipFiles, setZipFiles] = useState<File[]>([]);
  const [processing, setProcessing] = useState(false);
  const [processError, setProcessError] = useState<string | null>(null);
  const [lastJobId, setLastJobId] = useState<string | null>(null);
  const [lastJobStatus, setLastJobStatus] = useState<JobStatusResponse | null>(null);

  // Track the last started job so the button doesn't stay stuck on
  // "Processing…" and the user can upload + process again.
  useEffect(() => {
    if (!lastJobId) return;
    let cancelled = false;
    let timer: number | undefined;

    const poll = async () => {
      try {
        const next = await jobsApi.status(lastJobId);
        if (cancelled) return;
        setLastJobStatus(next);
        if (!TERMINAL.has(next.status)) {
          timer = window.setTimeout(poll, 1000);
        }
      } catch {
        if (!cancelled) {
          timer = window.setTimeout(poll, 3000);
        }
      }
    };

    poll();
    return () => {
      cancelled = true;
      if (timer !== undefined) window.clearTimeout(timer);
    };
  }, [lastJobId]);

  const addUploadBox = () =>
    setPendingUploads((prev) => [
      ...prev,
      { id: createLocalId(), uploading: false },
    ]);

  const removeUploadBox = (boxId: string) =>
    setPendingUploads((prev) => prev.filter((box) => box.id !== boxId));

  const handleFileSelected = async (boxId: string, file: File) => {
    if (!project) return;
    setSourceError(null);
    setPendingUploads((prev) =>
      prev.map((box) => (box.id === boxId ? { ...box, uploading: true } : box))
    );

    try {
      await addSource(project.id, {
        kind: "file",
        name: file.name,
        size: file.size,
        contentType: file.type || "application/octet-stream",
      });
      if (file.name.toLowerCase().endsWith(".zip")) {
        setZipFiles((prev) => [...prev, file]);
      }
      setPendingUploads((prev) => prev.filter((box) => box.id !== boxId));
    } catch (err) {
      setSourceError(err instanceof Error ? err.message : "Failed to upload file.");
      setPendingUploads((prev) =>
        prev.map((box) => (box.id === boxId ? { ...box, uploading: false } : box))
      );
    }
  };

  const handleRemoveFile = async (sourceId: string) => {
    if (!project) return;
    const removed = project.sources.find((s) => s.id === sourceId);
    setSourceError(null);
    try {
      await removeSource(project.id, sourceId);
      // Keep the local ZIP buffer in sync so a removed file doesn't stay queued.
      if (removed && removed.kind === "file" && removed.name) {
        setZipFiles((prev) => prev.filter((f) => f.name !== removed.name));
      }
    } catch (err) {
      setSourceError(err instanceof Error ? err.message : "Failed to remove file.");
    }
  };

  const handleAddRepo = async () => {
    if (!project) return;
    const url = repoUrl.trim();
    if (!url) return;

    setSourceError(null);
    try {
      await addSource(project.id, { kind: "repo", url });
      setRepoUrl("");
    } catch (err) {
      setSourceError(
        err instanceof Error ? err.message : "Failed to add repository."
      );
    }
  };

  const handleRemoveRepo = async (sourceId: string) => {
    if (!project) return;
    setSourceError(null);
    try {
      await removeSource(project.id, sourceId);
    } catch (err) {
      setSourceError(
        err instanceof Error ? err.message : "Failed to remove repository."
      );
    }
  };

  const handleProcess = async () => {
    setProcessError(null);
    if (zipFiles.length === 0) {
      setProcessError(
        "Upload at least one ZIP archive to generate documentation."
      );
      return;
    }

    setProcessing(true);
    try {
      const started = await jobsApi.start(zipFiles);
      setLastJobId(started.jobId);
      // Reset the form so the user can immediately upload + process again.
      // The job itself keeps running on the backend and can be followed
      // via the link below or on /documents?job=...
      setZipFiles([]);
      setPendingUploads([{ id: createLocalId(), uploading: false }]);
      router.push(`/documents?job=${started.jobId}`);
    } catch (err) {
      setProcessError(
        err instanceof Error ? err.message : "Failed to start processing."
      );
    } finally {
      setProcessing(false);
    }
  };

  if (isLoading) {
    return (
      <div className="mx-auto flex max-w-5xl items-center justify-center px-4 py-32 sm:px-6">
        <Loader2 className="size-6 animate-spin text-primary" />
      </div>
    );
  }

  if (!project) {
    return (
      <div className="mx-auto max-w-5xl px-4 py-32 text-center sm:px-6">
        <h1 className="text-3xl font-bold tracking-tight">Project not found</h1>
        <p className="mt-3 text-muted-foreground">
          This project may have been deleted.
        </p>
        <Button asChild className="mt-6 gap-1.5">
          <Link href="/projects">
            <ArrowLeft />
            Back to projects
          </Link>
        </Button>
      </div>
    );
  }

  const fileSources = project.sources.filter(isFileSource);
  const repoSources = project.sources.filter(isRepoSource);

  return (
    <div className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
      <Link
        href="/projects"
        className="inline-flex items-center gap-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft className="size-4" />
        Back to projects
      </Link>

      {/* Header */}
      <div className="mt-6 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">{project.title}</h1>
          <p className="mt-2 max-w-2xl text-muted-foreground">
            {project.description || "No description yet."}
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => setEditOpen(true)}
          className="gap-1.5"
        >
          <Pencil />
          Edit
        </Button>
      </div>

      {/* Sources */}
      <section className="mt-10">
        <h2 className="text-xl font-semibold">Add sources</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Upload your project files or connect repository links. You can add as
          many as you need.
        </p>

        {sourceError ? (
          <div className="mt-4 flex items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4">
            <CircleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />
            <p className="text-sm text-foreground">{sourceError}</p>
          </div>
        ) : null}

        <div className="mt-6 grid gap-8 lg:grid-cols-2">
          <div>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Project files</h3>
              <Button
                variant="ghost"
                size="sm"
                onClick={addUploadBox}
                className="gap-1"
              >
                <Plus />
                Add upload box
              </Button>
            </div>
            <div className="mt-3 space-y-3">
              {fileSources.map((source) => (
                <div
                  key={source.id}
                  className="flex items-center gap-3 rounded-lg border border-border bg-muted/40 px-3 py-2.5"
                >
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary/15 text-primary">
                    <FileText className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{source.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {formatBytes(source.size ?? 0)}
                    </p>
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => handleRemoveFile(source.id)}
                    aria-label="Remove file"
                  >
                    <X />
                  </Button>
                </div>
              ))}

              {pendingUploads.map((box) => (
                <div key={box.id} className="flex items-start gap-2">
                  <div className="flex-1">
                    <FileUploadBox
                      uploading={box.uploading}
                      onFileSelected={(file) => handleFileSelected(box.id, file)}
                    />
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => removeUploadBox(box.id)}
                    aria-label="Remove upload box"
                  >
                    <X />
                  </Button>
                </div>
              ))}
            </div>
          </div>

          <div>
            <h3 className="text-sm font-semibold">Repository links</h3>
            <div className="mt-3 space-y-3">
              {repoSources.map((source) => (
                <div
                  key={source.id}
                  className="flex items-center gap-3 rounded-lg border border-border bg-muted/40 px-3 py-2.5"
                >
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary/15 text-primary">
                    <Link2 className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{source.url}</p>
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => handleRemoveRepo(source.id)}
                    aria-label="Remove repository"
                  >
                    <X />
                  </Button>
                </div>
              ))}

              <form
                onSubmit={(event) => {
                  event.preventDefault();
                  handleAddRepo();
                }}
                className="flex items-center gap-2"
              >
                <div className="relative flex-1">
                  <Link2 className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                  <Input
                    type="url"
                    value={repoUrl}
                    onChange={(event) => setRepoUrl(event.target.value)}
                    placeholder="https://github.com/owner/repo"
                    className="pl-8"
                  />
                </div>
                <Button type="submit" disabled={!repoUrl.trim()} className="gap-1">
                  <Plus />
                  Add
                </Button>
              </form>
            </div>
          </div>
        </div>
      </section>

      {/* Process */}
      {project.sources.length > 0 ? (
        <section className="mt-12 rounded-2xl border border-border bg-card p-6">
          <h2 className="text-xl font-semibold">Generate documentation</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Process this project&apos;s uploaded ZIP archives into a technical
            specification and a user guide (available as Markdown, PDF, and
            DOCX).
          </p>

          {processError ? (
            <div className="mt-4 flex items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4">
              <CircleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />
              <p className="text-sm text-foreground">{processError}</p>
            </div>
          ) : null}

          <div className="mt-5 flex flex-wrap items-center gap-3">
            <Button
              type="button"
              disabled={processing || zipFiles.length === 0}
              onClick={handleProcess}
              className="h-11 gap-2 px-5"
            >
              {processing ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <FileCode2 />
              )}
              {processing ? "Starting…" : "Generate documentation"}
            </Button>
            <span className="text-xs text-muted-foreground">
              {zipFiles.length > 0
                ? `${zipFiles.length} ZIP archive${zipFiles.length === 1 ? "" : "s"} ready.`
                : "Upload a ZIP archive above to enable processing."}
            </span>
          </div>

          {lastJobId ? (
            <div className="mt-5 rounded-xl border border-border bg-muted/40 p-4" aria-live="polite">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm">
                  Last job <span className="font-mono text-xs">{lastJobId}</span>
                </p>
                {lastJobStatus ? (
                  <Badge variant="secondary">
                    {lastJobStatus.status}
                    {lastJobStatus.progressMessage ? ` · ${lastJobStatus.progressMessage}` : ""}
                  </Badge>
                ) : (
                  <Badge variant="secondary">starting…</Badge>
                )}
              </div>
              {lastJobStatus && !TERMINAL.has(lastJobStatus.status) ? (
                <div
                  className="mt-3 h-2 w-full overflow-hidden rounded-full bg-muted"
                  role="progressbar"
                  aria-valuenow={undefined}
                  aria-label="Job progress"
                >
                  <div className="h-full w-1/2 animate-pulse rounded-full bg-primary" />
                </div>
              ) : null}
              <div className="mt-3">
                <Button asChild variant="outline" size="sm">
                  <Link href={`/documents?job=${lastJobId}`}>
                    View document / progress
                  </Link>
                </Button>
              </div>
            </div>
          ) : null}
        </section>
      ) : (
        <section className="mt-12 rounded-2xl border border-dashed border-border bg-card/50 p-8 text-center">
          <p className="text-sm text-muted-foreground">
            Add at least one file or repository link above to generate
            documentation.
          </p>
        </section>
      )}

      <ProjectDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        project={project}
        onSubmit={(values) => updateProject(project.id, values)}
      />
    </div>
  );
}
