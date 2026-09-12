FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Shared/Shared.Contracts/Shared.Contracts.csproj", "Shared/Shared.Contracts/"]
COPY ["Services/MS2/MS2.Api/MS2.Api.csproj", "Services/MS2/MS2.Api/"]
RUN dotnet restore "Services/MS2/MS2.Api/MS2.Api.csproj"

COPY . .
WORKDIR "/src/Services/MS2/MS2.Api"
RUN dotnet build "MS2.Api.csproj" -c Release -o /app/build
RUN dotnet publish "MS2.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MS2.Api.dll"]