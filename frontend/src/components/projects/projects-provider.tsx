"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import {
  projectsApi,
  type AddSourceInput,
  type CreateProjectInput,
  type ProjectDto,
  type ProjectSourceDto,
} from "@/lib/api";

interface ProjectsContextValue {
  projects: ProjectDto[];
  isLoading: boolean;
  error: string | null;
  createProject: (input: CreateProjectInput) => Promise<ProjectDto>;
  updateProject: (id: string, input: CreateProjectInput) => Promise<ProjectDto>;
  deleteProject: (id: string) => Promise<void>;
  addSource: (projectId: string, input: AddSourceInput) => Promise<ProjectSourceDto>;
  removeSource: (projectId: string, sourceId: string) => Promise<void>;
}

const ProjectsContext = createContext<ProjectsContextValue | null>(null);

export function ProjectsProvider({ children }: { children: ReactNode }) {
  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    (async () => {
      try {
        const data = await projectsApi.list();
        if (active) {
          setProjects(data);
          setError(null);
        }
      } catch (err) {
        if (active) {
          setError(err instanceof Error ? err.message : "Failed to load projects.");
        }
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    })();

    return () => {
      active = false;
    };
  }, []);

  const createProject = async (input: CreateProjectInput): Promise<ProjectDto> => {
    const created = await projectsApi.create(input);
    setProjects((prev) => [created, ...prev]);
    return created;
  };

  const updateProject = async (
    id: string,
    input: CreateProjectInput
  ): Promise<ProjectDto> => {
    const updated = await projectsApi.update(id, input);
    setProjects((prev) => prev.map((p) => (p.id === id ? updated : p)));
    return updated;
  };

  const deleteProject = async (id: string): Promise<void> => {
    await projectsApi.remove(id);
    setProjects((prev) => prev.filter((p) => p.id !== id));
  };

  const addSource = async (
    projectId: string,
    input: AddSourceInput
  ): Promise<ProjectSourceDto> => {
    const source = await projectsApi.addSource(projectId, input);
    setProjects((prev) =>
      prev.map((p) =>
        p.id === projectId ? { ...p, sources: [...p.sources, source] } : p
      )
    );
    return source;
  };

  const removeSource = async (projectId: string, sourceId: string): Promise<void> => {
    await projectsApi.removeSource(projectId, sourceId);
    setProjects((prev) =>
      prev.map((p) =>
        p.id === projectId
          ? { ...p, sources: p.sources.filter((s) => s.id !== sourceId) }
          : p
      )
    );
  };

  return (
    <ProjectsContext.Provider
      value={{
        projects,
        isLoading,
        error,
        createProject,
        updateProject,
        deleteProject,
        addSource,
        removeSource,
      }}
    >
      {children}
    </ProjectsContext.Provider>
  );
}

export function useProjects(): ProjectsContextValue {
  const context = useContext(ProjectsContext);
  if (!context) {
    throw new Error("useProjects must be used within a ProjectsProvider");
  }
  return context;
}
