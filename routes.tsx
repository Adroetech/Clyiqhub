import { createBrowserRouter, Navigate } from "react-router-dom";
import LoginPage from "../pages/LoginPage";
import DashboardPage from "../pages/DashboardPage";
import { tokenStore } from "../services/tokenStore";
import * as React from "react";
import { JSX } from "react";

function RequireAuth({ children }: { children: JSX.Element }) {
    const token = tokenStore.getAccess();
    return token ? children : <Navigate to="/login" replace />;
}

export const router = createBrowserRouter([
    { path: "/", element: <Navigate to="/login" replace /> },
    { path: "/login", element: <LoginPage /> },
    { path: "/dashboard", element: <RequireAuth><DashboardPage /></RequireAuth> }
]);