# Base image
FROM node:26-alpine AS base
WORKDIR /app

# Install dependencies
FROM base AS deps
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci --ignore-scripts

# Build the app
FROM base AS build
COPY --from=deps /app/node_modules ./node_modules
COPY src/web/ ./
# Dummy value: prisma.config.ts requires DATABASE_URL to be set, but `prisma
# generate` never connects to it. The real value is injected at runtime by
# Container Apps and never reaches this build-only stage.
ENV DATABASE_URL="postgresql://user:password@localhost:5432/db"
# Real values required here: Next.js inlines NEXT_PUBLIC_* vars into the
# production bundle at build time, not runtime, so these must be supplied
# as build args (see deploy.yml) rather than as Container App env vars.
ARG NEXT_PUBLIC_SUPABASE_URL
ARG NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY
ENV NEXT_PUBLIC_SUPABASE_URL=${NEXT_PUBLIC_SUPABASE_URL}
ENV NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY=${NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY}
RUN npx prisma generate
RUN npm run build

# Runtime image
FROM base AS runtime
ENV NODE_ENV=production
ENV PORT=8080

COPY --from=build /app/public ./public
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static

EXPOSE 8080
CMD ["node", "server.js"]
