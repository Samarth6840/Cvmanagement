FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CvManagement/CvManagement.csproj CvManagement/
RUN dotnet restore CvManagement/CvManagement.csproj
COPY . .
RUN dotnet publish CvManagement/CvManagement.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
CMD dotnet CvManagement.dll --urls "http://0.0.0.0:${PORT:-8080}"