FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UI/MicroserviceCommunication.UI/MicroserviceCommunication.UI.csproj", "UI/MicroserviceCommunication.UI/"]
RUN dotnet restore "UI/MicroserviceCommunication.UI/MicroserviceCommunication.UI.csproj"

COPY . .
WORKDIR "/src/UI/MicroserviceCommunication.UI"
RUN dotnet build "MicroserviceCommunication.UI.csproj" -c Release -o /app/build
RUN dotnet publish "MicroserviceCommunication.UI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MicroserviceCommunication.UI.dll"]