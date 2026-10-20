# Meeting invitations, contacts, and preferences

## Included

- Koneksi → Kontak & Grup: private manual contacts, searchable names/emails, and groups. Deleting a contact also removes its group memberships.
- Pengaturan: Indonesian/English, light/dark/system, and an account time zone initialized from the device. Preferences are saved in PostgreSQL and cached by account in the browser.
- Meeting plans accept explicit emails or exact contact/group names. Ambiguous matches require a selection; missing contacts and empty groups block execution. The approved plan contains a fixed email list.
- Calendar invitations use `attendees` with `sendUpdates=all`, only during approved live execution. Test mode has no external effects.
- Meeting reminders default to 30 minutes when requested, are editable in review, and appear under Aktivitas. Immediate Discord messages remain separate steps.
- A database worker polls every 30 seconds. Late reminders are sent only before the meeting starts. Restarting during a send marks that delivery `needs_review`, without resending.

## Database and deployment

The application migration runner now reads ordered `backend/AgentHub.Api/Database/*.sql` files. Schema version 2 adds `workspaces.account_data` and `orbit.reminders`; version 1 data stays intact. The matching Supabase migration is checked in for reference. Use the application's `--migrate` runner for a fresh database, since the earlier application schema predates Supabase CLI migration history.

Schema version 2 was applied to the existing Orbit Supabase project and verified. The private schema has no `anon` or `authenticated` access; RLS is enabled. The advisory finding “RLS enabled without policies” is intentional for this backend-only schema. [Supabase explanation](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy).

Keep a single backend replica, consistent with the existing database worker lease. Keep `Security__EncryptionKey` stable: reminder destinations are encrypted with it. The worker does not use an in-memory timer as the source of truth, and reminder delivery does not consume another workflow quota.

The backend and frontend must both be deployed for these features to appear online. No real invitation or Discord message was sent during verification.

## Domain change

Target: `https://orbitworkspaces.vercel.app`.

1. Assign that domain to the existing Vercel project, subject to availability. Keep the existing domain while verifying the new one.
2. Register `https://orbitworkspaces.vercel.app/api/google/callback` in the existing Google OAuth web client.
3. Set Railway `Deployment__FrontendUrl=https://orbitworkspaces.vercel.app`; the allowed-host list now includes both frontend domains.
4. Deploy the backend and frontend. Check login and an authenticated API request through the new Vercel `/api` rewrite.
5. Redirect the old frontend domain after the new flow works.

The repository configuration and export helper are prepared. The Vercel domain assignment and Google OAuth console change have not been performed.

## Verification

- Frontend TypeScript/Vite production build.
- Backend compilation and existing planner/Drive/Meet regression checks.
- Invitation request body and notification query, email deduplication, ambiguous/missing participants, empty groups, fixed recipient snapshots, and structured clarification.
- Reminder delivery outcomes, late cutoff, no automatic retry after an uncertain send.
- PostgreSQL test fixtures: owner isolation, concurrent account updates, restart persistence, reminder uniqueness and cancellation, and test-mode isolation. Fixtures are removed afterward.
- Local HTTP contact/group CRUD, deleted-member cleanup, preferences, and frontend API proxy.
- Visual browser verification is outstanding because the available preview browser blocked the local test URL.

## Frontend handoff

`Preferences.tsx` owns account preference loading/saving; `ContactsPanel.tsx` and `MeetingPanel.tsx` isolate the new screens from the existing layout. `i18n.ts` and `translations.json` hold shared UI copy; generated plan text follows the selected language. `theme.css` centralizes theme colors, while `style.css` holds layout rules. Change the design without changing the API contracts.

New endpoints: GET/POST `/api/preferences`; GET/POST `/api/contacts` and `/api/groups`; POST their `/{id}/delete` routes; POST `/api/workflows/{id}/steps/{stepId}/meeting`; GET `/api/reminders`; POST `/api/reminders/{id}/cancel`. All account ownership comes from the authenticated session.
