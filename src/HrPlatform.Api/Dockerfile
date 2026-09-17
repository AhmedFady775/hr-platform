FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HrPlatform.slnx ./
COPY src/HrPlatform.Api/HrPlatform.Api.csproj src/HrPlatform.Api/
COPY src/HrPlatform.Contracts/HrPlatform.Contracts.csproj src/HrPlatform.Contracts/
RUN dotnet restore src/HrPlatform.Api/HrPlatform.Api.csproj

COPY src/HrPlatform.Api/ src/HrPlatform.Api/
COPY src/HrPlatform.Contracts/ src/HrPlatform.Contracts/
RUN dotnet publish src/HrPlatform.Api/HrPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HrPlatform.Api.dll"]
