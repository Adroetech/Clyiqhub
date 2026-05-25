import React from "react";
import { createBrowserRouter, Navigate } from "react-router-dom";
import LoginPage from "../pages/LoginPage";
import DashboardPage from "../pages/DashboardPage";
import { tokenStore } from "../services/tokenStore";
import Shell from "./Shell";

// Placeholder list pages (we’ll create them in Step 5)
import TenantsListPage from "../pages/TenantsListPage";
import SubscriptionsListPage from "../pages/SubscriptionsListPage";
import SupportTicketsListPage from "../pages/SupportTicketsListPage";
import InvoicesListPage from "../pages/InvoicesListPage";

function RequireAuth({ children }: { children: React.ReactElement }) {
  const token = tokenStore.getAccess();
  return token ? children : <Navigate to="/login" replace />;
}

function WithShell({ children }: { children: React.ReactElement }) {
  return <Shell>{children}</Shell>;
}

export const router = createBrowserRouter([
  { path: "/", element: <Navigate to="/login" replace /> },
  { path: "/login", element: <LoginPage /> },

  {
    path: "/dashboard",
    element: (
      <RequireAuth>
        <WithShell>
          <DashboardPage />
        </WithShell>
      </RequireAuth>
    )
  },

  // Placeholder destination pages so tile navigation works
  {
    path: "/tenants",
    element: (
      <RequireAuth>
        <WithShell>
          <TenantsListPage />
        </WithShell>
      </RequireAuth>
    )
  },
  {
    path: "/tenant-subscriptions",
    element: (
      <RequireAuth>
        <WithShell>
          <SubscriptionsListPage />
        </WithShell>
      </RequireAuth>
    )
  },
  {
    path: "/support-tickets",
    element: (
      <RequireAuth>
        <WithShell>
          <SupportTicketsListPage />
        </WithShell>
      </RequireAuth>
    )
  },
  {
    path: "/invoices",
    element: (
      <RequireAuth>
        <WithShell>
          <InvoicesListPage />
        </WithShell>
      </RequireAuth>
    )
  }
]);
