import type { Metadata } from "next";
import { Suspense } from "react";
import { Geist, Geist_Mono } from "next/font/google";
import { SiteHeader } from "@/components/site-header";
import { SiteFooter } from "@/components/site-footer";
import { ProjectsProvider } from "@/components/projects/projects-provider";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: {
    default: "CodeToDoc — Turn your codebase into documentation",
    template: "%s · CodeToDoc",
  },
  description:
    "CodeToDoc scans your repositories and automatically generates clean, searchable documentation — no manual writing required.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col">
        <ProjectsProvider>
          <Suspense fallback={<div className="sticky top-0 z-50 h-16 border-b border-border bg-background" />}>
            <SiteHeader />
          </Suspense>
          <main className="flex-1">{children}</main>
          <SiteFooter />
        </ProjectsProvider>
      </body>
    </html>
  );
}
