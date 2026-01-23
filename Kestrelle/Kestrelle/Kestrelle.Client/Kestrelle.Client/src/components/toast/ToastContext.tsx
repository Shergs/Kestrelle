import React, { createContext, useCallback, useContext, useMemo, useState } from "react";

export type ToastKind = "success" | "info" | "warning" | "error";

export type ToastItem = {
  id: string;
  kind: ToastKind;
  title?: string;
  message: string;
};

type ToastContextValue = {
  push: (t: Omit<ToastItem, "id">) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);

  const push = useCallback((t: Omit<ToastItem, "id">) => {
    const id = crypto.randomUUID();
    setItems((prev) => [{ id, ...t }, ...prev].slice(0, 5));
    window.setTimeout(() => setItems((prev) => prev.filter((x) => x.id !== id)), 4000);
  }, []);

  const value = useMemo(() => ({ push }), [push]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="fixed right-4 top-4 z-[100] flex w-[360px] flex-col gap-2">
        {items.map((t) => (
          <div
            key={t.id}
            className="rounded-2xl border border-slate-800 bg-slate-950/90 p-4 shadow-2xl backdrop-blur"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="text-sm font-semibold text-slate-100">
                  {t.title ?? (t.kind === "success" ? "Success" : t.kind === "warning" ? "Warning" : t.kind === "error" ? "Error" : "Info")}
                </div>
                <div className="mt-1 text-sm text-slate-300">{t.message}</div>
              </div>
              <div className="mt-1 h-2 w-2 rounded-full bg-indigo-400" />
            </div>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within ToastProvider.");
  return ctx;
}
