import Link from "next/link";
import {
  ArrowRight,
  BookOpen,
  Braces,
  Check,
  Download,
  GitBranch,
  GitMerge,
  Languages,
  Layers,
  Search,
  Sparkles,
  Wand2,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

const features = [
  {
    icon: Wand2,
    title: "Auto-generated docs",
    description:
      "Docstrings, comments, types, and structure are turned into coherent documentation automatically.",
  },
  {
    icon: Languages,
    title: "Every major language",
    description:
      "TypeScript, Python, Go, Rust, Java, C# and 20+ more — one consistent output format.",
  },
  {
    icon: Download,
    title: "Markdown & HTML export",
    description:
      "Export clean Markdown for your repo, or publish a hosted site with full theming.",
  },
  {
    icon: Search,
    title: "Search & navigation",
    description:
      "Full-text search and a linked sidebar make finding any symbol effortless.",
  },
  {
    icon: GitMerge,
    title: "CI/CD integration",
    description:
      "Regenerate docs on every merge request so they never drift out of date.",
  },
  {
    icon: Layers,
    title: "Versioned documentation",
    description:
      "Keep docs in sync with every release and tag your history automatically.",
  },
];

const steps = [
  {
    icon: GitBranch,
    step: "01",
    title: "Connect your repository",
    description:
      "Link GitHub, GitLab, or upload a repo. We read the code, not the comments alone.",
  },
  {
    icon: Braces,
    step: "02",
    title: "We analyze the code",
    description:
      "Our engine maps structure, signatures, and types to build a complete model.",
  },
  {
    icon: Sparkles,
    step: "03",
    title: "Publish your docs",
    description:
      "Generate clean, searchable documentation and share it with your team in minutes.",
  },
];

const stats = [
  { value: "20+", label: "Languages supported" },
  { value: "10k+", label: "Repos documented" },
  { value: "98%", label: "Doc coverage" },
  { value: "2 min", label: "Average setup" },
];

export default function Home() {
  return (
    <>
      {/* Hero */}
      <section className="relative overflow-hidden">
        <div
          aria-hidden
          className="pointer-events-none absolute inset-x-0 top-0 -z-10 h-[480px] bg-[radial-gradient(60%_60%_at_50%_0%,rgba(253,184,39,0.18),transparent)]"
        />
        <div className="mx-auto grid max-w-6xl items-center gap-12 px-4 pb-16 pt-16 sm:px-6 lg:grid-cols-2 lg:pb-24 lg:pt-24">
          <div className="flex flex-col items-start">
            <Badge variant="outline" className="gap-1.5 text-primary">
              <Sparkles className="size-3" />
              Docs as code, generated automatically
            </Badge>
            <h1 className="mt-5 text-4xl font-bold leading-tight tracking-tight sm:text-5xl lg:text-6xl">
              Turn your codebase into{" "}
              <span className="text-primary">beautiful documentation</span>
            </h1>
            <p className="mt-5 max-w-xl text-lg leading-8 text-muted-foreground">
              CodeToDoc scans your repositories and generates clean, searchable
              documentation your team will actually use — no manual writing
              required.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-3">
              <Button asChild className="h-11 px-6 text-base">
                <Link href="/projects">
                  Get started free
                  <ArrowRight data-icon="inline-end" />
                </Link>
              </Button>
              <Button asChild variant="outline" className="h-11 px-6 text-base">
                <Link href="/docs">View documentation</Link>
              </Button>
            </div>
            <p className="mt-4 text-sm text-muted-foreground">
              No credit card required · Free for open source
            </p>
          </div>

          {/* Hero visual */}
          <div className="relative">
            <div className="overflow-hidden rounded-2xl border border-border bg-card shadow-2xl shadow-black/10">
              <div className="flex items-center gap-1.5 border-b border-border bg-muted/60 px-4 py-3">
                <span className="size-2.5 rounded-full bg-[#ff5f57]" />
                <span className="size-2.5 rounded-full bg-[#febc2e]" />
                <span className="size-2.5 rounded-full bg-[#28c840]" />
                <span className="ml-3 text-xs font-medium text-muted-foreground">
                  CodeToDoc · workspace
                </span>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2">
                {/* Code pane */}
                <div className="border-b border-border bg-[#23120b] p-4 font-mono text-[13px] leading-6 sm:border-b-0 sm:border-r">
                  <p className="text-[#8d7b6e]">{"// src/users.ts"}</p>
                  <p>
                    <span className="text-[#fdb827]">export async</span>{" "}
                    <span className="text-[#e5c07b]">function</span>{" "}
                    <span className="text-[#61afef]">createUser</span>
                    <span className="text-[#d8d4cf]">(</span>
                    <span className="text-[#61afef]">input</span>
                    <span className="text-[#d8d4cf]">) {"{"}</span>
                  </p>
                  <p className="pl-4">
                    <span className="text-[#fdb827]">const</span>{" "}
                    <span className="text-[#61afef]">user</span>{" "}
                    <span className="text-[#d8d4cf]">=</span>{" "}
                    <span className="text-[#fdb827]">await</span>{" "}
                    <span className="text-[#e5c07b]">db.user</span>
                    <span className="text-[#d8d4cf]">.</span>
                    <span className="text-[#61afef]">create</span>
                    <span className="text-[#d8d4cf]">({"{"}</span>
                  </p>
                  <p className="pl-8">
                    <span className="text-[#61afef]">data</span>
                    <span className="text-[#d8d4cf]">:</span>{" "}
                    <span className="text-[#61afef]">input</span>
                    <span className="text-[#d8d4cf]">,</span>
                  </p>
                  <p className="pl-4">
                    <span className="text-[#d8d4cf]">{"}"});</span>
                  </p>
                  <p className="pl-4">
                    <span className="text-[#fdb827]">return</span>{" "}
                    <span className="text-[#61afef]">user</span>
                    <span className="text-[#d8d4cf]">;</span>
                  </p>
                  <p>
                    <span className="text-[#d8d4cf]">{"}"}</span>
                  </p>
                </div>
                {/* Docs pane */}
                <div className="bg-card p-4 text-[13px] leading-6">
                  <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    README.md
                  </p>
                  <p className="mt-2 text-lg font-bold text-foreground">
                    createUser
                  </p>
                  <p className="mt-1 text-muted-foreground">
                    Creates a new user record.
                  </p>
                  <p className="mt-3 font-semibold text-foreground">
                    Parameters
                  </p>
                  <ul className="mt-1 space-y-1">
                    <li className="flex items-center gap-2 text-muted-foreground">
                      <span className="text-primary">
                        <Check className="size-3.5" />
                      </span>
                      <span>
                        <code className="font-mono">input</code> — CreateUserInput
                      </span>
                    </li>
                  </ul>
                  <p className="mt-3 font-semibold text-foreground">Returns</p>
                  <ul className="mt-1 space-y-1">
                    <li className="flex items-center gap-2 text-muted-foreground">
                      <span className="text-primary">
                        <Check className="size-3.5" />
                      </span>
                      <span>
                        <code className="font-mono">Promise&lt;User&gt;</code>
                      </span>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Stats */}
        <div className="border-y border-border bg-card/50">
          <div className="mx-auto grid max-w-6xl grid-cols-2 gap-6 px-4 py-10 sm:px-6 md:grid-cols-4">
            {stats.map((stat) => (
              <div key={stat.label} className="text-center">
                <p className="text-3xl font-bold tracking-tight text-foreground">
                  {stat.value}
                </p>
                <p className="mt-1 text-sm text-muted-foreground">
                  {stat.label}
                </p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Features */}
      <section className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
        <div className="mx-auto max-w-2xl text-center">
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">
            Everything you need to ship docs
          </h2>
          <p className="mt-4 text-lg text-muted-foreground">
            Stop writing documentation by hand. Let CodeToDoc do the heavy
            lifting while you keep shipping.
          </p>
        </div>
        <div className="mt-14 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((feature) => (
            <div
              key={feature.title}
              className="group rounded-2xl border border-border bg-card p-6 transition-colors hover:border-primary/50"
            >
              <span className="inline-flex size-11 items-center justify-center rounded-xl bg-primary/15 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground">
                <feature.icon className="size-5" />
              </span>
              <h3 className="mt-4 text-lg font-semibold">{feature.title}</h3>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {feature.description}
              </p>
            </div>
          ))}
        </div>
      </section>

      {/* How it works */}
      <section className="border-y border-border bg-card/50">
        <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
          <div className="mx-auto max-w-2xl text-center">
            <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">
              How it works
            </h2>
            <p className="mt-4 text-lg text-muted-foreground">
              Three simple steps from source code to published documentation.
            </p>
          </div>
          <div className="mt-14 grid gap-6 md:grid-cols-3">
            {steps.map((step) => (
              <div key={step.step} className="relative rounded-2xl border border-border bg-card p-6">
                <span className="text-sm font-bold text-primary">{step.step}</span>
                <span className="mt-4 inline-flex size-11 items-center justify-center rounded-xl bg-primary text-primary-foreground">
                  <step.icon className="size-5" />
                </span>
                <h3 className="mt-4 text-lg font-semibold">{step.title}</h3>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {step.description}
                </p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
        <div className="relative overflow-hidden rounded-3xl bg-primary px-6 py-16 text-center sm:px-16">
          <div
            aria-hidden
            className="pointer-events-none absolute -left-20 -top-20 size-64 rounded-full bg-white/20 blur-3xl"
          />
          <div
            aria-hidden
            className="pointer-events-none absolute -bottom-24 -right-16 size-64 rounded-full bg-[#23120b]/10 blur-3xl"
          />
          <div className="relative">
            <BookOpen className="mx-auto size-12 text-primary-foreground" />
            <h2 className="mx-auto mt-6 max-w-2xl text-3xl font-bold tracking-tight text-primary-foreground sm:text-4xl">
              Ready to document your codebase?
            </h2>
            <p className="mx-auto mt-4 max-w-xl text-lg text-primary-foreground/80">
              Join thousands of developers who stopped writing docs by hand and
              let CodeToDoc generate them instead.
            </p>
            <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
              <Button
                asChild
                variant="secondary"
                className="h-11 px-6 text-base"
              >
                <Link href="/projects">
                  Start for free
                  <ArrowRight data-icon="inline-end" />
                </Link>
              </Button>
              <Button
                asChild
                variant="ghost"
                className="h-11 px-6 text-base text-primary-foreground hover:bg-primary-foreground/10 hover:text-primary-foreground"
              >
                <Link href="/docs">Read the docs</Link>
              </Button>
            </div>
          </div>
        </div>
      </section>
    </>
  );
}
