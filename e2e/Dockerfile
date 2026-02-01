FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /workspace

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY backend/ /workspace/backend/
COPY e2e/run.sh /workspace/e2e/run.sh

ENTRYPOINT ["sh", "/workspace/e2e/run.sh"]
