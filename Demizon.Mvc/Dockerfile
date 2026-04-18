FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Demizon.Mvc/Demizon.Mvc.csproj", "Demizon.Mvc/"]
COPY ["Demizon.Core/Demizon.Core.csproj", "Demizon.Core/"]
COPY ["Demizon.Dal/Demizon.Dal.csproj", "Demizon.Dal/"]
COPY ["Demizon.Common/Demizon.Common.csproj", "Demizon.Common/"]
RUN dotnet restore "Demizon.Mvc/Demizon.Mvc.csproj"
COPY . .
WORKDIR "/src/Demizon.Mvc"
RUN dotnet build "Demizon.Mvc.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Demizon.Mvc.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
RUN mkdir -p /data && chmod 777 /data
WORKDIR /app
COPY --from=publish /app/publish .
RUN printf '#!/bin/sh\necho "Waiting for /data..."\ni=0\nwhile [ $i -lt 60 ]; do\n  if touch /data/.probe 2>/dev/null; then rm -f /data/.probe; echo "/data ready after ${i}s"; break; fi\n  sleep 1\n  i=$((i+1))\ndone\nexec dotnet Demizon.Mvc.dll\n' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh
USER $APP_UID
ENTRYPOINT ["/bin/sh", "/app/entrypoint.sh"]
