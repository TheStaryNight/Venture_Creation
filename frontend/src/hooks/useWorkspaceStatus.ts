import { useCallback, useEffect, useState } from 'react';
import { api } from '../lib/api';
import type { Billing, Connection, Workflow } from '../types';

/** Keeps the dashboard connection, billing and workflow summaries up to date. */
export function useWorkspaceStatus() {
  const [billing, setBilling] = useState<Billing | null>(null);
  const [connections, setConnections] = useState<Connection[]>([]);
  const [history, setHistory] = useState<Workflow[]>([]);
  const [ai, setAi] = useState(false);
  const [googleConfigured, setGoogleConfigured] = useState(false);
  const [online, setOnline] = useState<boolean | null>(null);
  const refresh = useCallback(async () => {
    try {
      const [state, workflows, subscription] = await Promise.all([
        api<{ ai: boolean; googleConfigured: boolean; services: Connection[] }>('/connections'),
        api<Workflow[]>('/workflows'),
        api<Billing>('/billing'),
      ]);
      setBilling(subscription);
      setConnections(state.services);
      setAi(state.ai);
      setGoogleConfigured(state.googleConfigured === true);
      setHistory(workflows);
      setOnline(true);
    } catch {
      setOnline(false);
    }
  }, []);
  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), 5000);
    return () => clearInterval(timer);
  }, [refresh]);
  return { billing, setBilling, connections, history, ai, googleConfigured, online, refresh };
}
