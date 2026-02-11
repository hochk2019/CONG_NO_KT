# RUN_FRONTEND

## Prerequisites
- Node.js 18+ (current setup uses Node 22)

## Configure
Create `src/frontend/.env` (or copy from `.env.example`):
```
VITE_API_BASE_URL=/api
```

## Install & Run
```
cd src/frontend
npm install
npm run dev
```

Open: http://localhost:5173

## Notes
- Login uses `/auth/login` from backend and keeps the access token in memory with a refresh cookie.
- The Vite proxy (`/api`) keeps refresh cookies working in dev.
- Adjust `VITE_API_BASE_URL` when deploying behind IIS reverse proxy.
