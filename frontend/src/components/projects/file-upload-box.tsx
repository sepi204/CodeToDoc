"use client";

import { useRef, useState } from "react";
import { Loader2, UploadCloud } from "lucide-react";
import { cn } from "@/lib/utils";

interface FileUploadBoxProps {
  uploading?: boolean;
  onFileSelected: (file: File) => void;
}

export function FileUploadBox({
  uploading = false,
  onFileSelected,
}: FileUploadBoxProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  const handleFiles = (files: FileList | null) => {
    const file = files?.[0];
    if (!file) return;
    onFileSelected(file);
    if (inputRef.current) {
      inputRef.current.value = "";
    }
  };

  if (uploading) {
    return (
      <div className="flex items-center justify-center gap-2 rounded-lg border border-border bg-muted/40 px-3 py-6 text-sm text-muted-foreground">
        <Loader2 className="size-4 animate-spin text-primary" />
        Uploading…
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
        ref={inputRef}
        type="file"
        className="sr-only"
        onChange={(event) => handleFiles(event.target.files)}
      />
    </label>
  );
}
