import { Suspense } from "react";
import { DocumentDelivery } from "@/components/documents/document-delivery";

export default function DocumentsPage() {
  return (
    <Suspense fallback={<div className="mx-auto max-w-2xl px-4 py-32 sm:px-6" />}>
      <DocumentDelivery />
    </Suspense>
  );
}
