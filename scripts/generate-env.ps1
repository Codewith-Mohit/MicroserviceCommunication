function Generate-SecurePassword {
param(
[int]$Length = 16
)
$charSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*"
$password = ""
$random = New-Object System.Random

for ($i = 0; $i -lt $Length; $i++) {
$password += $charSet[$random.Next($charSet.Length)]
}
return $password
}

# Check if .env file already exists
if ((Test-Path ".env") -and -not $Force) {
Write-Host "✓ .env file already exists" -ForegroundColor Yellow
exit 0
}

Write-Host "Generating secure passwords for Docker Compose..." -ForegroundColor Green

# Generate passwords
$sqlPassword = Generate-SecurePassword -Length 20
$rabbitmqAdminPass = Generate-SecurePassword -Length 16
$rabbitmqAppPass = Generate-SecurePassword -Length 16

# Create .env file
$envContent = @"
# SQL Server Configuration
SA_PASSWORD=$sqlPassword
SQLSERVER_DATABASE_1=MS1Db
SQLSERVER_DATABASE_2=MS2Db
SQLSERVER_DATABASE_3=MS3Db

# RabbitMQ Configuration
RABBITMQ_USER=appuser
RABBITMQ_PASSWORD=$rabbitmqAppPass
RABBITMQ_DEFAULT_USER=admin
RABBITMQ_DEFAULT_PASS=$rabbitmqAdminPass

# Service Configuration
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_ENVIRONMENT=Development
"@

# Write to .env file
Set-Content -Path "../.env" -Value $envContent

Write-Host "✓ .env file created successfully!" -ForegroundColor Green
Write-Host "Credentials generated and stored in .env" -ForegroundColor Yellow
