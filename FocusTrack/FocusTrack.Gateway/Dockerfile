# syntax=docker/dockerfile:1
# ---- build stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY FocusTrack.sln ./
COPY Contracts/Contracts.csproj                                   Contracts/
COPY Infrastructure/Infrastructure.csproj                         Infrastructure/
COPY Session.Domain/Session.Domain.csproj                         Session.Domain/
COPY Session.Application/Session.Application.csproj               Session.Application/
COPY Session.Infrastructure/Session.Infrastructure.csproj         Session.Infrastructure/
COPY FocusTrack.Gateway/FocusTrack.Gateway.csproj             FocusTrack.Gateway/
COPY Notification.Domain/Notification.Domain.csproj               Notification.Domain/
COPY Notification.Application/Notification.Application.csproj     Notification.Application/
COPY Notification.Infrastructure/Notification.Infrastructure.csproj Notification.Infrastructure/
COPY Notification.Presentation/Notification.Presentation.csproj   Notification.Presentation/
COPY Admin.Domain/Admin.Domain.csproj                             Admin.Domain/
COPY Admin.Application/Admin.Application.csproj                   Admin.Application/
COPY Admin.Infrastructure/Admin.Infrastructure.csproj             Admin.Infrastructure/
COPY Admin.Presentation/Admin.Presentation.csproj                 Admin.Presentation/
COPY RewardWorker/RewardWorker.csproj                             RewardWorker/
COPY FocusTrack.Gateway/FocusTrack.Gateway.csproj                 FocusTrack.Gateway/

RUN dotnet restore FocusTrack.Gateway/FocusTrack.Gateway.csproj

COPY . .
WORKDIR /src/FocusTrack.Gateway
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

USER app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "FocusTrack.Gateway.dll"]
