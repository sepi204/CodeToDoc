"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  BookOpen,
  CheckCircle2,
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

type ConvertType = "technical" | "guide";

interface PendingUpload {
  id: string;
  uploading: boolean;
}

export function ProjectDetail({ id }: { id: string }) {
  const { projects, isLoading, updateProject, addSource, removeSource } =
    useProjects();
  const project = projects.find((p) => p.id === id);

  const [pendingUploads, setPendingUploads] = useState<PendingUpload[]>([
    { id: createLocalId(), uploading: false },
  ]);
  const [repoUrl, setRepoUrl] = useState("");
  const [sourceError, setSourceError] = useState<string | null>(null);
  const [editOpen, setEditOpen] = useState(false);
  const [converting, setConverting] = useState<ConvertType | null>(null);
  const [result, setResult] = useState<ConvertType | null>(null);

  const timerRef = useRef<number | null>(null);

  useEffect(() => {
    return () => {
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
      }
    };
  }, []);

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
    setSourceError(null);
    try {
      await removeSource(project.id, sourceId);
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

  const handleConvert = (type: ConvertType) => {
    setResult(null);
    setConverting(type);
    timerRef.current = window.setTimeout(() => {
      setConverting(null);
      setResult(type);
    }, 1500);
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

      {/* Convert */}
      {project.sources.length > 0 ? (
        <section className="mt-12 rounded-2xl border border-border bg-card p-6">
          <h2 className="text-xl font-semibold">Generate documentation</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Choose the type of documentation you want to produce from this
            project.
          </p>

          <div className="mt-5 flex flex-wrap gap-3">
            <Button
              type="button"
              disabled={converting !== null}
              onClick={() => handleConvert("technical")}
              className="h-11 gap-2 px-5"
            >
              <FileCode2 />
              Convert to Technical Documentation
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={converting !== null}
              onClick={() => handleConvert("guide")}
              className="h-11 gap-2 px-5"
            >
              <BookOpen />
              Convert to User Guide
            </Button>
          </div>

          {converting ? (
            <div className="mt-6 flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin text-primary" />
              Generating{" "}
              {converting === "technical"
                ? "technical documentation"
                : "user guide"}
              …
            </div>
          ) : null}

          {result ? (
            <div className="mt-6 flex items-start gap-3 rounded-xl border border-primary/30 bg-primary/10 p-4">
              <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-primary" />
              <div>
                <p className="font-semibold">
                  {result === "technical"
                    ? "Technical documentation"
                    : "User guide"}{" "}
                  generated
                </p>
                <p className="mt-1 text-sm text-muted-foreground">
                  The documentation for “{project.title}” is ready. This is a
                  frontend preview — the actual generation will be handled by
                  the backend.
                </p>
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
