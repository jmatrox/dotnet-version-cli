rmdir /s /q bin\Release
pause
rem dotnet version -f .\dotnet-version.csproj patch
dotnet pack -c Release
dotnet nuget push --interactive --source "nugetfeed" --api-key az bin\Release\dotnet-version-netframework-cli.*.nupkg
pause