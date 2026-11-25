# ApplyTrail web client

Angular 22 single-page app: standalone components, signals, Tailwind CSS 4, Angular CDK (drag-and-drop only).

```bash
npm install
npm start                       # http://localhost:4200, proxies /api to http://localhost:5080
npm run build                   # production build into dist/web/browser
npm test -- --watch=false       # Vitest
```

`nginx.conf` and `security-headers.conf` are used by the production `Dockerfile`. See the [root README](../README.md) for the whole project.

## Layout

```
src/app/
  core/       API client, auth (in-memory token + cookie refresh), interceptor, guards, models, date helpers
  shared/     strip, status chip and picker, reminder row, dialogs, icons, toasts
  features/   auth, shell, today, board, applications (list, detail, form), insights, settings
```
