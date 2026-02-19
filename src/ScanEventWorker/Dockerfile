FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ScanEventWorker/ScanEventWorker.csproj src/ScanEventWorker/
RUN dotnet restore src/ScanEventWorker/ScanEventWorker.csproj

COPY src/ScanEventWorker/ src/ScanEventWorker/
RUN dotnet publish src/ScanEventWorker/ScanEventWorker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ScanEventWorker.dll"]
