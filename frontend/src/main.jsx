import React from "react";
import { createRoot } from "react-dom/client";
import App from "./App.jsx";
import DashboardShowcase from "./DashboardShowcase.jsx";
import "./styles.css";

const root = createRoot(document.getElementById("root"));
const path = window.location.pathname.replace(/\/+$/, "") || "/";
const match = path.match(/^\/([1-5])$/);
const layoutVariant = match ? Number(match[1]) : null;
root.render(
  <React.StrictMode>
    {layoutVariant ? <DashboardShowcase variant={layoutVariant} /> : <App />}
  </React.StrictMode>
);
