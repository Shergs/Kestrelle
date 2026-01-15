import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/shell/AppShell";
import { OverviewPage } from "./pages/OverviewPage";
import { DashboardPage } from "./pages/DashboardPage";
import { DocumentationPage } from "./pages/DocumentationPage";
import { LoginPage } from "./pages/LoginPage";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/overview" replace />} />
      <Route path="/login" element={<LoginPage />} />

      {/* App shell routes */}
      <Route element={<AppShell />}>
        <Route path="/overview" element={<OverviewPage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/documentation" element={<DocumentationPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/overview" replace />} />
    </Routes>
  );
}
