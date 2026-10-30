"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  BookOpen,
  CheckCircle2,
  FileCode2,
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
  createId,
  isFileSource,
  isRepoSource,
  type FileSource,
  type ProjectSource,
  type RepoSource,
} from "@/lib/projects";

type FileSlot = { id: string; file: FileSource | null };
type RepoSlot = { id: string; url: string };
type ConvertType = "technical" | "guide";

export function ProjectDetail({ id }: { id: string }) {
  const { projects, isHydrated, updateProject } = useProjects();
  const project = projects.find((p) => p.id === id);

  const [fileSlots, setFileSlots] = useState<FileSlot[]>([]);
  const [repoSlots, setRepoSlots] = useState<RepoSlot[]>([]);
  const [editOpen, setEditOpen] = useState(false);
  const [converting, setConverting] = useState<ConvertType | null>(null);
  const [result, setResult] = useState<ConvertType | null>(null);

  const initializedRef = useRef(false);
  const skipSyncRef = useRef(false);
  const timerRef = useRef<number | null>(null);

  // Load slots from the project once it becomes available.
  useEffect(() => {
    if (!project || initializedRef.current) return;

    const fileSources = project.sources
      .filter(isFileSource)
      .map((source) => ({ id: source.id, file: source }));
    const repoSources = project.sources
      .filter(isRepoSource)
      .map((source) => ({ id: source.id, url: source.url }));

    setFileSlots(
      fileSources.length > 0
        ? fileSources
        : [{ id: createId(), file: null }]
    );
    setRepoSlots(
      repoSources.length > 0
        ? repoSources
        : [{ id: createId(), url: "" }]
    );

    initializedRef.current = true;
    skipSyncRef.current = true; // skip the sync triggered by our own init
  }, [project]);

  // Persist slots back to the project whenever they change.
  useEffect(() => {
    if (!project || !initializedRef.current) return;

    if (skipSyncRef.current) {
      skipSyncRef.current = false;
      return;
    }

    const sources: ProjectSource[] = [
      ...fileSlots.flatMap((slot) => (slot.file ? [slot.file] : [])),
      ...repoSlots.flatMap((slot) =>
        slot.url.trim()
          ? [{ id: slot.id, kind: "repo", url: slot.url.trim() } as RepoSource]
          : []
      ),
    ];

    updateProject(project.id, { sources });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fileSlots, repoSlots]);

  useEffect(() => {
    return () => {
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
      }
    };
  }, []);

  const hasSources =
    fileSlots.some((slot) => slot.file) ||
    repoSlots.some((slot) => slot.url.trim());

  const addUploadBox = () =>
    setFileSlots((prev) => [...prev, { id: createId(), file: null }]);

  const setUploadFile = (slotId: string, file: FileSource | null) =>
    setFileSlots((prev) =>
      prev.map((slot) => (slot.id === slotId ? { ...slot, file } : slot))
    );

  const addRepoSlot = () =>
    setRepoSlots((prev) => [...prev, { id: createId(), url: "" }]);

  const setRepoUrl = (slotId: string, url: string) =>
    setRepoSlots((prev) =>
      prev.map((slot) => (slot.id === slotId ? { ...slot, url } : slot))
    );

  const removeRepoSlot = (slotId: string) =>
    setRepoSlots((prev) => prev.filter((slot) => slot.id !== slotId));

  const handleConvert = (type: ConvertType) => {
    setResult(null);
    setConverting(type);
    timerRef.current = window.setTimeout(() => {
      setConverting(null);
      setResult(type);
    }, 1500);
  };

  if (!isHydrated) {
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
              {fileSlots.map((slot) => (
                <FileUploadBox
                  key={slot.id}
                  file={slot.file}
                  onChange={(file) => setUploadFile(slot.id, file)}
                />
              ))}
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Repository links</h3>
              <Button
                variant="ghost"
                size="sm"
                onClick={addRepoSlot}
                className="gap-1"
              >
                <Plus />
                Add repository
              </Button>
            </div>
            <div className="mt-3 space-y-3">
              {repoSlots.map((slot) => (
                <div key={slot.id} className="flex items-center gap-2">
                  <div className="relative flex-1">
                    <Link2 className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                    <Input
                      type="url"
                      value={slot.url}
                      onChange={(event) =>
                        setRepoUrl(slot.id, event.target.value)
                      }
                      placeholder="https://github.com/owner/repo"
                      className="pl-8"
                    />
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => removeRepoSlot(slot.id)}
                    aria-label="Remove repository"
                  >
                    <X />
                  </Button>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* Convert */}
      {hasSources ? (
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
