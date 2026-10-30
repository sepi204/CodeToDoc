import { Suspense } from "react";
import { ProjectDetail } from "./project-detail";

export default function ProjectDetailPage({
  params,
}: PageProps<"/projects/[id]">) {
  return (
    <Suspense fallback={<ProjectDetailFallback />}>
      <ProjectDetailLoader params={params} />
    </Suspense>
  );
}

async function ProjectDetailLoader({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <ProjectDetail id={id} />;
}

function ProjectDetailFallback() {
  return <div className="mx-auto max-w-5xl px-4 py-32 sm:px-6" />;
}
