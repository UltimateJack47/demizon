FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_ENVIRONMENT=Production

# Stejný base jako mru.sk na serveru, takže se vrstvy runtime na disku sdílejí
# (jen při shodném digestu, po patchi .NET je potřeba stáhnout oba).
# Alpine běží v invariant globalization módu: cs-CZ (datumy, řazení) potřebuje ICU,
# bez něj appka spadne už při startu na CultureNotFoundException. tzdata kvůli
# Europe/Prague v GoogleCalendarService — bez ní spadne až synchronizace kalendáře.
RUN apk add --no-cache icu-libs icu-data-full tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Demizon.Mvc/Demizon.Mvc.csproj", "Demizon.Mvc/"]
COPY ["Demizon.Core/Demizon.Core.csproj", "Demizon.Core/"]
COPY ["Demizon.Dal/Demizon.Dal.csproj", "Demizon.Dal/"]
COPY ["Demizon.Common/Demizon.Common.csproj", "Demizon.Common/"]
RUN dotnet restore "Demizon.Mvc/Demizon.Mvc.csproj" -r linux-musl-x64
COPY . .
WORKDIR "/src/Demizon.Mvc"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
# -r linux-musl-x64 vyhodí adresář runtimes/ s 22 kopiemi libe_sqlite3 pro platformy,
# na kterých tenhle image nikdy nepoběží: 62 MB -> 30 MB. musl, ne linux-x64 (glibc),
# jinak by e_sqlite3 na alpine nešla načíst.
# ReadyToRun záměrně nezapnuto — přidal by 26 MB zpět (viz hosting-optimization-plan.md).
RUN dotnet publish "Demizon.Mvc.csproj" -c $BUILD_CONFIGURATION \
    -r linux-musl-x64 --self-contained false \
    -o /app/publish -p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Demizon.Mvc.dll"]
