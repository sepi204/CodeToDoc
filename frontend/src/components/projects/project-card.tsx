"use client";

import Link from "next/link";
import { ChevronRight, FileUp, GitBranch, Pencil, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import type { ProjectDto } from "@/lib/api";
import { formatDate } from "@/lib/projects";

interface ProjectCardProps {
  project: ProjectDto;
  onEdit: () => void;
  onDelete: () => void;
}

export function ProjectCard({ project, onEdit, onDelete }: ProjectCardProps) {
  const fileCount = project.sources.filter((source) => source.kind === "file").length;
  const repoCount = project.sources.filter((source) => source.kind === "repo").length;

  return (
    <Card className="h-full">
      <CardHeader>
        <CardAction>
          <div className="flex items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={onEdit}
              aria-label="Edit project"
            >
              <Pencil />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              onClick={onDelete}
              aria-label="Delete project"
            >
              <Trash2 />
            </Button>
          </div>
        </CardAction>
        <CardTitle>
          <Link
            href={`/projects/${project.id}`}
            className="transition-colors hover:text-primary"
          >
            {project.title}
          </Link>
        </CardTitle>
        <CardDescription className="line-clamp-2">
          {project.description || "No description yet."}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex items-center gap-4 text-xs text-muted-foreground">
          <span className="inline-flex items-center gap-1.5">
            <FileUp className="size-3.5" />
            {fileCount} file{fileCount === 1 ? "" : "s"}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <GitBranch className="size-3.5" />
            {repoCount} repo{repoCount === 1 ? "" : "s"}
          </span>
        </div>
      </CardContent>
      <CardFooter className="justify-between">
        <span className="text-xs text-muted-foreground">
          Created {formatDate(project.createdAt)}
        </span>
        <Link
          href={`/projects/${project.id}`}
          className="inline-flex items-center gap-1 text-sm font-medium text-primary transition-colors hover:underline"
        >
          Open
          <ChevronRight className="size-4" />
        </Link>
      </CardFooter>
    </Card>
  );
}
