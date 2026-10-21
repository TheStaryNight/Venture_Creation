# Engineering guide

Orbit uses React + TypeScript for its UI and ASP.NET Core for its API and background workers. Start here when changing a feature.

## Frontend map

Paths below are relative to `frontend/src`.

| Area | Entry point | Responsibility |
| --- | --- | --- |
| Startup | `main.tsx`, `App.tsx` | Session and preferences providers, app composition |
| Layout | `app/AppShell.tsx` | Sidebar, navigation, notifications and mounted dialogs |
| App state | `app/useAppController.ts`, `app/AppContext.tsx` | Current page, project selection, workflow planning and approval actions |
| Server summaries | `hooks/useWorkspaceStatus.ts` | Poll connections, billing and workflow history |
| Pages | `pages/` | Home, projects, activity, connections and settings |
| Dialogs | `components/*Dialog.tsx` | Connection details, project creation and workflow review |
| Shared views | `components/WorkspaceViews.tsx` | Composer, activity list and project cards |
| Contracts | `types.ts` | API and workspace data types |
| HTTP | `lib/api.ts` | Request headers, JSON handling and typed API errors |
| Browser storage | `hooks/useStored.ts` | Local fallback storage |
| Service labels | `config/services.ts` | Integration descriptions and workflow status labels |
| Preferences | `Preferences.tsx` | Account preferences, theme and language application |
| Styling | `theme.css`, `style.css` | Theme colors and component/layout styles |

Pages consume `useAppContext()`; they do not each create their own controller. Keep shared state in the provider, form-only state in the relevant component, and API transport concerns in `lib/api.ts`. `WorkspaceSession.tsx` owns workspace persistence; preferences have a separate save lifecycle.

## Backend map

Paths below are relative to `backend/AgentHub.Api`.

| Area | Entry point | Responsibility |
| --- | --- | --- |
| Startup | `Program.cs` | Dependency registration, initialization, middleware order and endpoint mapping |
| Workflow API | `Endpoints/WorkflowEndpoints.cs` | Validate plans, resolve recipients and authorize execution |
| Billing, connections, reminders | `Endpoints/` | Routes grouped by feature |
| Planning | `Planning/Planner.cs`, `GeminiPlanner.cs`, `PlanContract.cs` | Provider requests, model output schema and validation |
| Execution | `Workflows/WorkflowWorker.cs` | Sequential execution, result persistence and failure handling |
| Workflow state | `Workflows/Workflow.cs`, `Step.cs`, `WorkflowStore.cs` | Models, snapshots, queue and recovery |
| Google integrations | `GoogleActions.cs`, `GoogleWorkspaceActions.cs` | Drive, Calendar, Gmail and Docs requests |
| Google login | `GoogleConnection.cs` | OAuth, tokens and account isolation |
| Account features | `AccountFeatures.cs`, `AccountDatabase.cs` | Contacts, groups, preferences and scheduled reminders |
| Persistence | `OrbitDatabase.cs` | Database initialization, workspace data and worker ownership |
| Discord URL validation | `Integrations/Discord/Webhook.cs` | Allowed webhook URL checks |

## Follow a workflow

1. The composer calls the controller's `plan` action.
2. `POST /api/workflows/plan` validates input and asks a planner for steps.
3. `WorkflowDialog` displays the plan and lets the user resolve recipients and folder selection.
4. Approval calls `POST /api/workflows/{id}/execute`. The backend checks ownership and approval requirements before queuing work.
5. `WorkflowWorker` executes steps in order. Google and Discord calls are separated into named methods.
6. The worker persists each transition. An uncertain external result fails the workflow and skips remaining steps; it must not be retried automatically.
7. The UI polls the active workflow. Calendar reminders are persisted only after an event exists and run separately from the workflow queue.

## Before submitting changes

Run from the repository root:

```powershell
npm ci --prefix frontend
npm run format:check --prefix frontend
npm run typecheck --prefix frontend
npm run build --prefix frontend
dotnet build tests/PlannerChecks -p:UseSharedCompilation=false -p:UseAppHost=false
dotnet tests/PlannerChecks/bin/Debug/net8.0/PlannerChecks.dll
```

Use `npm run format --prefix frontend` to format UI code. `.editorconfig` defines shared whitespace rules for the repository. Keep functions focused, name intermediate conditions, and avoid multiple unrelated statements on one line. Comments should explain a constraint or reason that the code cannot express clearly.

PlannerChecks uses mock HTTP for external integrations; it does not send real email. PostgreSQL checks require explicitly configured disposable test databases and are skipped otherwise. Never point those checks at production.

## Refactor boundaries

This reorganization preserves route names, JSON fields, database schema, OAuth scopes and approval rules. It adds no migration. Do not remove owner checks, change reminder retry semantics or weaken workflow approval to simplify code. Keep secrets in ignored local configuration or deployment variables.

Some larger integration and database files remain grouped by feature. Split them when changing that feature rather than introducing an extra abstraction for every method. Theme palette identifiers are retained so this refactor does not change the design.
