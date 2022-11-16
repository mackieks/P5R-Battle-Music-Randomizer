# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/RestoreLastSupriseMod/*" -Force -Recurse
dotnet publish "./RestoreLastSupriseMod.csproj" -c Release -o "$env:RELOADEDIIMODS/RestoreLastSupriseMod" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location