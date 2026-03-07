# Stage 1 – build & publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/PicoBusX.Web/PicoBusX.Web.csproj

RUN dotnet publish src/PicoBusX.Web/PicoBusX.Web.csproj \
    --no-restore \
    -c Release \
    -o /app/publish

# Stage 2 – runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PicoBusX.Web.dll"]