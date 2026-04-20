# 1. 빌드 스테이지
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 모든 파일을 복사 (Discode 폴더 및 .sln 포함)
COPY . .

# 종속성 복구 및 빌드
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# 2. 실행 스테이지
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Render용 포트 설정
ENV ASPNETCORE_URLS=http://*:${PORT:-8080}

# 실행 파일(.dll)을 자동으로 찾아 실행
ENTRYPOINT ["sh", "-c", "dotnet $(ls *.dll | grep -v 'Discord.Net' | head -n 1)"]