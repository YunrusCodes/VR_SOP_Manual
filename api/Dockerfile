# ---------------------------------------------------------------------------
# Stage 1: build the admin SPA
# ---------------------------------------------------------------------------
FROM node:20-slim AS admin-build
WORKDIR /admin
COPY admin/package.json admin/package-lock.json* ./
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi
COPY admin/ ./
RUN npm run build

# ---------------------------------------------------------------------------
# Stage 2: API
# ---------------------------------------------------------------------------
FROM python:3.12-slim

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    STORAGE_DIR=/data/storage \
    ADMIN_DIST_DIR=/app/static/admin

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY api/requirements.txt ./
RUN pip install -r requirements.txt

COPY api/main.py api/seed.py api/schema.py api/csv_io.py ./
COPY --from=admin-build /admin/dist /app/static/admin

RUN mkdir -p /data/storage

EXPOSE 8000

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8000/healthz || exit 1

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
