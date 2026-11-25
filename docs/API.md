# API reference

Interactive documentation is available at `/scalar` when the API runs in Development. This page is the short version.

## Conventions

- JSON, camelCase. Enums are sent and received as their **names** (`"Interviewing"`); numbers are rejected.
- Calendar days are `yyyy-MM-dd`. Timestamps are UTC ISO 8601.
- Everything except `/api/auth/*` and `/api/health` needs `Authorization: Bearer <access token>`.
- Failures are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details: `{ "title": "...", "status": 404, "detail": "..." }`. Validation failures add `errors`: `{ "field": ["message"] }`.
- Another user's resource returns `404`, identical to a missing one.
- Where an endpoint takes `today`, send the user's local calendar date. The server accepts it within two days of its own date and answers `400` otherwise.

## Auth

| Method and path | Body | Result |
|---|---|---|
| `POST /api/auth/register` | `{ email, password }` (password 10 to 100 characters) | `201` `{ accessToken, expiresAt, email }` and the refresh cookie |
| `POST /api/auth/login` | `{ email, password }` | `200` same as above |
| `POST /api/auth/refresh` | none (uses the `applytrail_refresh` cookie) | `200` new access token and rotated cookie, or `401` |
| `POST /api/auth/logout` | none | `204`, cookie cleared |

Auth endpoints are rate limited (`429` when exceeded).

## Applications

| Method and path | Purpose |
|---|---|
| `GET /api/applications` | List. Query: `q`, `status` (repeat for several), `source`, `priority`, `sponsorship`, `archived`, `sort` (`company`, `appliedOn`, `priority`, `created`, `lastActivity`), `dir` (`asc`, `desc`), `page`, `pageSize` (max 200). Returns `{ items, total, page, pageSize }`. |
| `GET /api/applications/{id}` | One application with its `events` and `reminders` |
| `POST /api/applications` | Create. Include `status` and `today`. |
| `PUT /api/applications/{id}` | Edit fields |
| `POST /api/applications/{id}/status` | `{ status, note?, today }`. Applies the status rules. |
| `POST /api/applications/{id}/archive` and `/unarchive` | Hide from or restore to the live list |
| `DELETE /api/applications/{id}` | Delete with its timeline and reminders |
| `POST /api/applications/{id}/events` | Log a note, email, call, interview, assessment or offer |
| `POST /api/applications/check-duplicates` | `{ company, roleTitle?, jobUrl?, excludeId? }` returns up to five likely matches |

## Reminders

| Method and path | Purpose |
|---|---|
| `GET /api/reminders?bucket=All\|Overdue\|Today\|Upcoming&today=` | Open reminders on live applications |
| `POST /api/applications/{id}/reminders` | `{ type, title, dueOn }` |
| `POST /api/applications/{id}/reminders/{reminderId}/complete` | `{ logAsEvent, note?, remindAgainInDays?, today }` |
| `POST /api/applications/{id}/reminders/{reminderId}/snooze` | `{ days, today }` |

## Dashboard and insights

| Method and path | Purpose |
|---|---|
| `GET /api/dashboard?today=` | Weekly goal, overdue/today/upcoming reminders, stale applications, counts per status |
| `GET /api/insights?weeks=8&today=` | Funnel, reply and interview rates, average days to reply, weekly series, results by source |

## Account and export

| Method and path | Purpose |
|---|---|
| `GET /api/me` | Email and settings |
| `PUT /api/me/settings` | `{ weeklyGoal, followUpAfterDays, staleAfterDays }` |
| `DELETE /api/me` | `{ password }`. Deletes the account and all its data. |
| `GET /api/export/csv` | UTF-8 CSV (with byte-order mark, for Excel) |
| `GET /api/export/json` | Complete backup, including timelines and reminders |
| `GET /api/health` | `{ "status": "ok" }`, no authentication |
