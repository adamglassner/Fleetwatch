# Build stage
FROM node:22-bookworm-slim AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY tsconfig.json ./
COPY src ./src
RUN npm run build

# Runtime stage
FROM node:22-bookworm-slim AS runtime
ENV NODE_ENV=production
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY --from=build /app/dist ./dist
# schema.sql is read at runtime from alongside the compiled db.js
COPY --from=build /app/src/schema.sql ./dist/schema.sql

# Data (SQLite file) lives on a mounted volume.
RUN mkdir -p /app/data
VOLUME ["/app/data"]
ENV DATABASE_PATH=/app/data/fleetwatch.db

EXPOSE 8080
CMD ["node", "dist/index.js"]
