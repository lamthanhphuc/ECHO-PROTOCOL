# EchoProtocol.Web

Next.js BFF and web portal for ECHO PROTOCOL players and administrators.

## Local setup

```powershell
Set-Location E:\ECHO-PROTOCOL\EchoProtocol.Web
Copy-Item .env.example .env.local
npm install
npm run lint
npm run typecheck
npm run build
npm run dev
```

`BACKEND_API_URL` is server-only. Do not add JWT or payOS credentials to any `NEXT_PUBLIC_*` variable.

The current Backend does not expose an owner-safe payment catalog, payment-order read route, or player payment history route. Wallet top-up package selection and payment polling/history remain explicitly unavailable until those contracts are added.

## Deployment

- Vercel: set `BACKEND_API_URL` in project environment variables.
- Docker: run `npm install` first so `package-lock.json` exists, then build with `docker build -t echo-protocol-web .`.
- Health probe: `GET /api/health`.
