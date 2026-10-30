export function PagePlaceholder({
  title,
  description,
}: {
  title: string;
  description?: string;
}) {
  return (
    <section className="mx-auto flex max-w-6xl flex-col items-center justify-center px-4 py-32 text-center sm:px-6">
      <h1 className="text-4xl font-bold tracking-tight">{title}</h1>
      <p className="mt-4 max-w-md text-lg text-muted-foreground">
        {description ?? "This page is under construction."}
      </p>
    </section>
  );
}
