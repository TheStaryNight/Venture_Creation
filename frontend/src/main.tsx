import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import { SessionGate } from './WorkspaceSession';
import './style.css';
createRoot(document.getElementById('root')!).render(<React.StrictMode><SessionGate>{bootstrap => <App bootstrap={bootstrap}/>}</SessionGate></React.StrictMode>);
