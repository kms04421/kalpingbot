# 1. 빌드 스테이지
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# .csproj 파일을 먼저 복사하여 종속성을 캐싱합니다.
# 프로젝트 파일 이름이 다를 경우 *.csproj로 통칭하여 복사합니다.
COPY *.csproj ./
RUN dotnet restore

# 소스 코드 전체를 복사하고 빌드합니다.
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# 2. 실행 스테이지
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 빌드 스테이지에서 생성된 결과물만 가져옵니다.
COPY --from=build /app/publish .

# Render에서 주입하는 PORT 환경 변수를 사용하도록 설정 (없을 시 8080)
ENV ASPNETCORE_URLS=http://*:${PORT:-8080}

# 실행할 DLL 파일 이름을 입력합니다. 
# 프로젝트 이름이 EternalReturnBot이 맞는지 확인하세요.
ENTRYPOINT ["dotnet", "EternalReturnBot.dll"]