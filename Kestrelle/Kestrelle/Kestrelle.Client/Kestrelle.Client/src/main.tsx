import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./App";
import { AuthProvider } from "./state/auth/AuthContext";
import { SelectedGuildProvider } from "./state/guild/SelectedGuildContext";
import { ToastProvider } from "./components/toast/ToastContext";
import { MusicRealtimeProvider } from "./state/music/MusicRealtimeContext";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <SelectedGuildProvider>
          <ToastProvider>
            <MusicRealtimeProvider>
              <App />
            </MusicRealtimeProvider>
          </ToastProvider>
        </SelectedGuildProvider>
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>
);
