"use client";

import { useState } from "react";
import { FileText, UploadCloud, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { createId, formatBytes, type FileSource } from "@/lib/projects";

interface FileUploadBoxProps {
  file: FileSource | null;
  onChange: (file: FileSource | null) => void;
}

export function FileUploadBox({ file, onChange }: FileUploadBoxProps) {
  const [dragging, setDragging] = useState(false);

  const handleFiles = (files: FileList | null) => {
    const selected = files?.[0];
    if (!selected) return;
    onChange({
      id: createId(),
      kind: "file",
      name: selected.name,
      size: selected.size,
      type: selected.type || "application/octet-stream",
    });
  };

  if (file) {
    return (
      <div className="flex items-center gap-3 rounded-lg border border-border bg-muted/40 px-3 py-2.5">
        <span className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary/15 text-primary">
          <FileText className="size-4" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{file.name}</p>
          <p className="text-xs text-muted-foreground">{formatBytes(file.size)}</p>
        </div>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          onClick={() => onChange(null)}
          aria-label="Remove file"
        >
          <X />
        </Button>
      </div>
    );
  }

  return (
    <label
      onDragOver={(event) => {
        event.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(event) => {
        event.preventDefault();
        setDragging(false);
        handleFiles(event.dataTransfer.files);
      }}
      className={cn(
        "flex cursor-pointer flex-col items-center justify-center gap-1.5 rounded-lg border-2 border-dashed px-3 py-6 text-center transition-colors",
        dragging
          ? "border-primary bg-primary/10"
          : "border-border hover:border-primary/60 hover:bg-muted/40"
      )}
    >
      <UploadCloud className="size-6 text-muted-foreground" />
      <span className="text-sm font-medium">Upload a project file</span>
      <span className="text-xs text-muted-foreground">
        Drag &amp; drop or click to browse
      </span>
      <input
        type="file"
        className="sr-only"
        onChange={(event) => handleFiles(event.target.files)}
      />
    </label>
  );
}
