@echo off
dotnet publish src-cs/ReadAloud/ReadAloud.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o dist-cs
