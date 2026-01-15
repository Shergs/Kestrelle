import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./App";
import { AuthProvider } from "./state/auth/AuthContext";
import { SelectedGuildProvider } from "./state/guild/SelectedGuildContext";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <SelectedGuildProvider>
          <App />
        </SelectedGuildProvider>
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>
);
