$currentDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Start-Process powershell -Verb RunAs -ArgumentList "-NoExit", "-Command", "cd '$currentDir'; .\install-service.ps1"