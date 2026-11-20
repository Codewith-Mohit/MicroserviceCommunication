FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Shared/Shared.Contracts/Shared.Contracts.csproj", "Shared/Shared.Contracts/"]
COPY ["Services/MS3/MS3.Api/MS3.Api.csproj", "Services/MS3/MS3.Api/"]
RUN dotnet restore "Services/MS3/MS3.Api/MS3.Api.csproj"

COPY . .
WORKDIR "/src/Services/MS3/MS3.Api"
RUN dotnet build "MS3.Api.csproj" -c Release -o /app/build
RUN dotnet publish "MS3.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MS3.Api.dll"]