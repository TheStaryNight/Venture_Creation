import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import { PreferencesProvider } from './Preferences';
import './theme.css';
import { SessionGate } from './WorkspaceSession';
import './style.css';
createRoot(document.getElementById('root')!).render(<React.StrictMode><SessionGate>{bootstrap => <PreferencesProvider key={bootstrap.owner} owner={bootstrap.owner}><App bootstrap={bootstrap}/></PreferencesProvider>}</SessionGate></React.StrictMode>);
