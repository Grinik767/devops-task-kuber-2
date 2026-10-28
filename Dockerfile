FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY app/HomeworkApp.csproj app/
RUN dotnet restore app/HomeworkApp.csproj

COPY app/ app/
ARG APP_VERSION=hw2-v1
RUN printf "%s\n" "$APP_VERSION" > app/version.txt
RUN dotnet publish app/HomeworkApp.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .

ENV APP_PORT=8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "HomeworkApp.dll"]
