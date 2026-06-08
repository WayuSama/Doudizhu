# 斗地主服务器 Docker 镜像
# 构建: docker build -t doudizhu-server .
# 运行: docker run -d -p 5123:5123 --name doudizhu doudizhu-server

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DoudizhuServer.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5123
ENV ASPNETCORE_URLS=http://0.0.0.0:5123
ENTRYPOINT ["dotnet", "DoudizhuServer.dll"]
