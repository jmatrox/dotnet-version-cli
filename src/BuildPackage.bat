rmdir /s /q bin\Release
pause
rem dotnet version -f .\dotnet-version.csproj patch
rem dotnet pack -c Release
rem pause
dotnet nuget push --interactive --source "nugetfeed" --api-key az bin\Release\dotnet-version-netframework-cli.*.nupkg
pause