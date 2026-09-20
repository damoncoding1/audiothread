FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY AudioThread.sln ./
COPY src/AudioThread.Api/AudioThread.Api.csproj src/AudioThread.Api/
COPY src/AudioThread.Application/AudioThread.Application.csproj src/AudioThread.Application/
COPY src/AudioThread.Domain/AudioThread.Domain.csproj src/AudioThread.Domain/
COPY src/AudioThread.Infrastructure/AudioThread.Infrastructure.csproj src/AudioThread.Infrastructure/
COPY tests/AudioThread.Application.Tests/AudioThread.Application.Tests.csproj tests/AudioThread.Application.Tests/
COPY tests/AudioThread.Infrastructure.Tests/AudioThread.Infrastructure.Tests.csproj tests/AudioThread.Infrastructure.Tests/
RUN dotnet restore src/AudioThread.Api/AudioThread.Api.csproj

COPY src/ src/
RUN dotnet publish src/AudioThread.Api/AudioThread.Api.csproj -c Release -o /app/publish --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=ghcr.io/astral-sh/uv:0.5.11 /uv /usr/local/bin/uv

WORKDIR /app

ENV UV_PYTHON_INSTALL_DIR=/opt/uv-python \
    VIRTUAL_ENV=/app/python/.venv \
    HF_HUB_DISABLE_PROGRESS_BARS=1 \
    HF_HUB_OFFLINE=1

COPY python/requirements.txt python/
RUN uv venv --python 3.10 "$VIRTUAL_ENV" \
    && uv pip install --python "$VIRTUAL_ENV/bin/python" --index-strategy unsafe-best-match -r python/requirements.txt

COPY python/download_models.py python/summarize_audio.py python/enhance_audio.py python/
RUN HF_HUB_OFFLINE=0 "$VIRTUAL_ENV/bin/python" python/download_models.py \
        --cache-directory /app/python/model-cache

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=0

EXPOSE 8080

ENTRYPOINT ["dotnet", "AudioThread.Api.dll"]
