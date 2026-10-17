# Fixed environment of the visual tests (docs/testing.md §9.3): the Playwright image of the version in package.json
# (browsers and system fonts included) plus the .NET SDK needed by the backend of the end-to-end tests.
FROM mcr.microsoft.com/playwright:v1.64.0-noble

ARG DOTNET_CHANNEL=10.0
ENV DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    PATH="/usr/share/dotnet:${PATH}" \
    TZ=Europe/Paris

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_ROOT}" \
    && rm /tmp/dotnet-install.sh \
    && corepack enable

WORKDIR /work
