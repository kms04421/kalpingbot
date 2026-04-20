# 1. 빌드 스테이지
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 모든 .csproj 파일을 복사하여 종속성을 복구합니다.
COPY *.csproj ./
RUN dotnet restore

# 소스 코드를 복사하고 빌드합니다.
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# 2. 실행 스테이지
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 빌드 결과물을 복사합니다.
COPY --from=build /app/publish .

# Render 환경 변수 PORT를 사용하도록 설정
ENV ASPNETCORE_URLS=http://*:${PORT:-8080}

# ⚠️ 프로젝트 이름이 달라도 자동으로 실행 파일을 찾아 실행하는 설정입니다.
ENTRYPOINT ["sh", "-c", "dotnet $(ls *.dll | head -n 1)"]