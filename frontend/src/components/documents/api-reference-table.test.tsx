import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ApiReferenceTable } from "./api-reference-table";
import type { EndpointModel } from "@/lib/jobs";

const endpoints: EndpointModel[] = [
  {
    path: "/api/invoices",
    method: "POST",
    controllerName: "InvoicesController",
    actionName: "CreateInvoice",
    authRequired: true,
    roles: ["Admin"],
  },
  {
    path: "/api/customers",
    method: "GET",
    controllerName: "CustomersController",
    actionName: "GetAll",
    authRequired: false,
    roles: [],
  },
];

describe("ApiReferenceTable", () => {
  it("renders all endpoints", () => {
    render(<ApiReferenceTable endpoints={endpoints} />);
    expect(screen.getByText("/api/invoices")).toBeInTheDocument();
    expect(screen.getByText("/api/customers")).toBeInTheDocument();
  });

  it("filters by path", () => {
    render(<ApiReferenceTable endpoints={endpoints} />);
    fireEvent.change(screen.getByLabelText("Search API endpoints"), {
      target: { value: "invoices" },
    });
    expect(screen.getByText("/api/invoices")).toBeInTheDocument();
    expect(screen.queryByText("/api/customers")).toBeNull();
  });

  it("filters by method", () => {
    render(<ApiReferenceTable endpoints={endpoints} />);
    fireEvent.change(screen.getByLabelText("Search API endpoints"), {
      target: { value: "GET" },
    });
    expect(screen.getByText("/api/customers")).toBeInTheDocument();
    expect(screen.queryByText("/api/invoices")).toBeNull();
  });

  it("filters by controller name", () => {
    render(<ApiReferenceTable endpoints={endpoints} />);
    fireEvent.change(screen.getByLabelText("Search API endpoints"), {
      target: { value: "Customers" },
    });
    expect(screen.getByText("/api/customers")).toBeInTheDocument();
    expect(screen.queryByText("/api/invoices")).toBeNull();
  });

  it("shows an empty message when no endpoints match", () => {
    render(<ApiReferenceTable endpoints={endpoints} />);
    fireEvent.change(screen.getByLabelText("Search API endpoints"), {
      target: { value: "zzz-nope" },
    });
    expect(screen.getByText(/No endpoints match/)).toBeInTheDocument();
  });

  it("shows a notice when there are no endpoints at all", () => {
    render(<ApiReferenceTable endpoints={[]} />);
    expect(screen.getByText(/No API endpoints were detected/)).toBeInTheDocument();
  });
});
