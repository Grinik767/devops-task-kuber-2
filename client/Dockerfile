FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Client.csproj ./
RUN dotnet restore Client.csproj
COPY Program.cs ./
RUN dotnet publish Client.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Client.dll"]
