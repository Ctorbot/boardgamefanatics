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
