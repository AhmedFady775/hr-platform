FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HrPlatform.slnx ./
COPY src/HrPlatform.Web/HrPlatform.Web.csproj src/HrPlatform.Web/
COPY src/HrPlatform.Contracts/HrPlatform.Contracts.csproj src/HrPlatform.Contracts/
RUN dotnet restore src/HrPlatform.Web/HrPlatform.Web.csproj

COPY src/HrPlatform.Web/ src/HrPlatform.Web/
COPY src/HrPlatform.Contracts/ src/HrPlatform.Contracts/
RUN dotnet publish src/HrPlatform.Web/HrPlatform.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HrPlatform.Web.dll"]
