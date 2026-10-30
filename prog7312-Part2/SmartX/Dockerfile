FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SmartX.Api/SmartX.Api.csproj SmartX.Api/
RUN dotnet restore SmartX.Api/SmartX.Api.csproj
COPY . .
WORKDIR /src/SmartX.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/Uploads
ENV ASPNETCORE_URLS=http://+:5245
EXPOSE 5245
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]