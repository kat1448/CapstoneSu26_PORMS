FROM python:3.11-slim

WORKDIR /app

# Cài system deps cần cho psycopg2
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    libpq-dev \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Cài Python dependencies trước (cache layer)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy source code
COPY . .

# Prefect worker sẽ được start bởi docker-compose command
CMD ["prefect", "worker", "start", "--pool", "porms-pool", "--type", "process"]
