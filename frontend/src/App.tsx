import { AppProvider } from './app/AppContext';
import { AppShell } from './app/AppShell';
import type { Bootstrap } from './WorkspaceSession';
export default function App({ bootstrap }: { bootstrap: Bootstrap }) {
  return (
    <AppProvider bootstrap={bootstrap}>
      <AppShell />
    </AppProvider>
  );
}
