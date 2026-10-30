"use client";

import {
  createContext,
  useContext,
  useEffect,
  useReducer,
  type ReactNode,
} from "react";
import {
  createId,
  type CreateProjectInput,
  type Project,
} from "@/lib/projects";

const STORAGE_KEY = "codetodoc.projects";

interface ProjectsState {
  projects: Project[];
  hydrated: boolean;
}

type ProjectsAction =
  | { type: "hydrate"; projects: Project[] }
  | { type: "add"; project: Project }
  | { type: "update"; id: string; patch: Partial<Omit<Project, "id">> }
  | { type: "remove"; id: string };

const initialState: ProjectsState = { projects: [], hydrated: false };

function reducer(state: ProjectsState, action: ProjectsAction): ProjectsState {
  switch (action.type) {
    case "hydrate":
      return { projects: action.projects, hydrated: true };
    case "add":
      return {
        ...state,
        projects: [action.project, ...state.projects],
      };
    case "update":
      return {
        ...state,
        projects: state.projects.map((project) =>
          project.id === action.id
            ? { ...project, ...action.patch }
            : project
        ),
      };
    case "remove":
      return {
        ...state,
        projects: state.projects.filter((project) => project.id !== action.id),
      };
    default:
      return state;
  }
}

interface ProjectsContextValue {
  projects: Project[];
  isHydrated: boolean;
  createProject: (input: CreateProjectInput) => Project;
  updateProject: (
    id: string,
    patch: Partial<Omit<Project, "id">>
  ) => void;
  deleteProject: (id: string) => void;
}

const ProjectsContext = createContext<ProjectsContextValue | null>(null);

function readStoredProjects(): Project[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Project[]) : [];
  } catch {
    return [];
  }
}

export function ProjectsProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState);

  useEffect(() => {
    dispatch({ type: "hydrate", projects: readStoredProjects() });
  }, []);

  useEffect(() => {
    if (!state.hydrated) return;
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state.projects));
  }, [state.projects, state.hydrated]);

  const createProject = (input: CreateProjectInput): Project => {
    const now = Date.now();
    const project: Project = {
      id: createId(),
      title: input.title,
      description: input.description,
      sources: [],
      createdAt: now,
      updatedAt: now,
    };
    dispatch({ type: "add", project });
    return project;
  };

  const updateProject = (
    id: string,
    patch: Partial<Omit<Project, "id">>
  ) => {
    dispatch({ type: "update", id, patch: { ...patch, updatedAt: Date.now() } });
  };

  const deleteProject = (id: string) => {
    dispatch({ type: "remove", id });
  };

  return (
    <ProjectsContext.Provider
      value={{
        projects: state.projects,
        isHydrated: state.hydrated,
        createProject,
        updateProject,
        deleteProject,
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
