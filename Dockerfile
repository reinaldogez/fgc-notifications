# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# nao instala git hooks no build: o manifesto de tools e o repositorio git
# nao fazem parte do contexto do container
ENV HUSKY=0

COPY ["fcg-notifications.slnx", "./"]
COPY ["nuget.config", "./"]
COPY ["src/Fcg.Notifications.Application/Fcg.Notifications.Application.csproj",       "src/Fcg.Notifications.Application/"]
COPY ["src/Fcg.Notifications.Infrastructure/Fcg.Notifications.Infrastructure.csproj", "src/Fcg.Notifications.Infrastructure/"]
COPY ["src/Fcg.Notifications.Api/Fcg.Notifications.Api.csproj",                       "src/Fcg.Notifications.Api/"]

RUN --mount=type=secret,id=gh_token \
    dotnet nuget update source github-fcg \
      --username x --password "$(cat /run/secrets/gh_token)" --store-password-in-clear-text \
      --configfile nuget.config \
 && dotnet restore "src/Fcg.Notifications.Api/Fcg.Notifications.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/Fcg.Notifications.Api/Fcg.Notifications.Api.csproj" \
    -c Release -o /app/publish --no-restore

# ---- final ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Fcg.Notifications.Api.dll"]
