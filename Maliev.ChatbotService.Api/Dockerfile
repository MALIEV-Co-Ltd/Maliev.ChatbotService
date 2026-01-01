FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /

# Copy nuget.config for GitHub Packages authentication
COPY nuget.config ./

# Copy project files for restore
COPY ["Maliev.ChatbotService.Api/Maliev.ChatbotService.Api.csproj", "Maliev.ChatbotService.Api/"]
COPY ["Maliev.ChatbotService.Application/Maliev.ChatbotService.Application.csproj", "Maliev.ChatbotService.Application/"]
COPY ["Maliev.ChatbotService.Domain/Maliev.ChatbotService.Domain.csproj", "Maliev.ChatbotService.Domain/"]
COPY ["Maliev.ChatbotService.Infrastructure/Maliev.ChatbotService.Infrastructure.csproj", "Maliev.ChatbotService.Infrastructure/"]

# Restore with BuildKit secrets for NuGet authentication
RUN --mount=type=secret,id=nuget_username \
    --mount=type=secret,id=nuget_password \
    GITHUB_ACTOR=$(cat /run/secrets/nuget_username) \
    NUGET_AUTH_TOKEN=$(cat /run/secrets/nuget_password) \
    dotnet restore "Maliev.ChatbotService.Api/Maliev.ChatbotService.Api.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/Maliev.ChatbotService.Api"
RUN dotnet publish -c Release -o /app/publish --no-restore

# Final stage - ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Set ownership before switching to app user
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Copy published output
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Health check
HEALTHCHECK CMD curl -f http://localhost:8080/chatbot/liveness || exit 1

ENTRYPOINT ["dotnet", "Maliev.ChatbotService.Api.dll"]
