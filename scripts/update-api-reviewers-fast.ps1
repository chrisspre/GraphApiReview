# Update API Reviewers Helper Script
# This script runs the C# reviewer collection functionality

Write-Host "Updating API Reviewers List" -ForegroundColor Cyan
Write-Host ("=" * 40)

Write-Host "[INFO] Running reviewer collection..." -ForegroundColor Yellow

# Run the C# application to collect reviewers
try {
    dotnet run --project src/gapir -- --collect-reviewers
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "[SUCCESS] API reviewers list has been updated!" -ForegroundColor Green
        Write-Host "[INFO] The generated code has been displayed above and saved to generated-reviewers.cs" -ForegroundColor Yellow
        Write-Host "[INFO] You can also manually copy the code to update ApiReviewersFallback.cs if needed" -ForegroundColor Yellow
    } else {
        Write-Host "[ERROR] Failed to collect reviewers" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "[ERROR] Failed to run reviewer collection: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
