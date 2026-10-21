import { createContext, useContext, type ReactNode } from 'react';
import type { Bootstrap } from '../WorkspaceSession';
import { useAppController } from './useAppController';
type AppController = ReturnType<typeof useAppController>;
const AppContext = createContext<AppController | null>(null);
export function AppProvider({
  bootstrap,
  children,
}: {
  bootstrap: Bootstrap;
  children: ReactNode;
}) {
  const controller = useAppController(bootstrap);
  return <AppContext.Provider value={controller}>{children}</AppContext.Provider>;
}
export function useAppContext() {
  const controller = useContext(AppContext);
  if (!controller) throw new Error('Orbit views must be rendered inside AppProvider.');
  return controller;
}
