"use client";

import { useState } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { ProjectCard } from "@/components/projects/project-card";
import { ProjectDialog } from "@/components/projects/project-dialog";
import { useProjects } from "@/components/projects/projects-provider";
import {
  BookOpen,
  CircleAlert,
  FileCode2,
  FolderOpen,
  Loader2,
  Plus,
  Sparkles,
} from "lucide-react";
import type { ProjectDto } from "@/lib/api";

const outputTypes = [
  {
    icon: FileCode2,
    title: "Technical documentation",
    description:
      "Detailed references for developers: API endpoints, architecture, functions, and types.",
  },
  {
    icon: BookOpen,
    title: "User guide",
    description:
      "Friendly, step-by-step instructions that help end users get the most from your product.",
  },
];

export default function ProjectsPage() {
  const { projects, isLoading, error, createProject, updateProject, deleteProject } =
    useProjects();

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingProject, setEditingProject] = useState<ProjectDto | undefined>(
    undefined
  );
  const [deletingProject, setDeletingProject] = useState<ProjectDto | undefined>(
    undefined
  );
  const [actionError, setActionError] = useState<string | null>(null);

  const openCreate = () => {
    setEditingProject(undefined);
    setDialogOpen(true);
  };

  const openEdit = (project: ProjectDto) => {
    setEditingProject(project);
    setDialogOpen(true);
  };

  const handleSubmit = async (values: { title: string; description: string }) => {
    setActionError(null);
    try {
      if (editingProject) {
        await updateProject(editingProject.id, values);
      } else {
        await createProject(values);
      }
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Something went wrong.");
    }
  };

  const handleDelete = async () => {
    if (!deletingProject) return;
    setActionError(null);
    try {
      await deleteProject(deletingProject.id);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "Something went wrong.");
    }
    setDeletingProject(undefined);
  };

  const errorMessage = error ?? actionError;

  return (
    <div className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
      {/* Intro */}
      <section className="mx-auto max-w-3xl text-center">
        <div className="inline-flex items-center gap-1.5 rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-medium text-foreground">
          <Sparkles className="size-3.5 text-primary" />
          Code in, documentation out
        </div>
        <h1 className="mt-5 text-4xl font-bold tracking-tight sm:text-5xl">
          Turn your projects into{" "}
          <span className="text-primary">documentation</span>
        </h1>
        <p className="mt-4 text-lg leading-8 text-muted-foreground">
          Upload one or more projects, add repository links, and convert
          everything into a polished technical reference or an end-user guide —
          automatically.
        </p>
      </section>

      {/* Output types */}
      <section className="mx-auto mt-10 grid max-w-4xl gap-6 sm:grid-cols-2">
        {outputTypes.map((type) => (
          <div
            key={type.title}
            className="rounded-2xl border border-border bg-card p-6"
          >
            <span className="inline-flex size-11 items-center justify-center rounded-xl bg-primary/15 text-primary">
              <type.icon className="size-5" />
            </span>
            <h2 className="mt-4 text-lg font-semibold">{type.title}</h2>
            <p className="mt-2 text-sm leading-6 text-muted-foreground">
              {type.description}
            </p>
          </div>
        ))}
      </section>

      {/* Projects list */}
      <section className="mt-16">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-2xl font-bold tracking-tight">Your projects</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {isLoading
                ? "Loading…"
                : `${projects.length} project${projects.length === 1 ? "" : "s"}`}
            </p>
          </div>
          <Button onClick={openCreate} className="gap-1.5">
            <Plus />
            New project
          </Button>
        </div>

        {errorMessage ? (
          <div className="mt-6 flex items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4">
            <CircleAlert className="mt-0.5 size-5 shrink-0 text-destructive" />
            <p className="text-sm text-foreground">{errorMessage}</p>
          </div>
        ) : null}

        {isLoading ? (
          <div className="mt-8 flex items-center justify-center py-16">
            <Loader2 className="size-6 animate-spin text-primary" />
          </div>
        ) : projects.length === 0 ? (
          <div className="mt-8 flex flex-col items-center justify-center rounded-2xl border border-dashed border-border bg-card/50 px-6 py-16 text-center">
            <span className="inline-flex size-12 items-center justify-center rounded-xl bg-muted text-muted-foreground">
              <FolderOpen className="size-6" />
            </span>
            <h3 className="mt-4 text-lg font-semibold">No projects yet</h3>
            <p className="mt-2 max-w-sm text-sm text-muted-foreground">
              Create your first project to start generating technical docs and
              user guides.
            </p>
            <Button onClick={openCreate} className="mt-6 gap-1.5">
              <Plus />
              Create your first project
            </Button>
          </div>
        ) : (
          <div className="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {projects.map((project) => (
              <ProjectCard
                key={project.id}
                project={project}
                onEdit={() => openEdit(project)}
                onDelete={() => setDeletingProject(project)}
              />
            ))}
          </div>
        )}
      </section>

      <ProjectDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        project={editingProject}
        onSubmit={handleSubmit}
      />

      <AlertDialog
        open={Boolean(deletingProject)}
        onOpenChange={(open) => {
          if (!open) setDeletingProject(undefined);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete project?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete{" "}
              <span className="font-medium text-foreground">
                {deletingProject?.title}
              </span>{" "}
              and all of its uploaded files and repository links.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={handleDelete}>
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
