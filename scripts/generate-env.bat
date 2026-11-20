@echo off
REM Generate Secure Passwords for Docker Compose - Windows Batch
REM This script generates secure passwords and creates a .env file

SetLocal EnableDelayedExpansion

echo.
echo Generating secure passwords for Docker Compose...
echo.

REM Generate random passwords using PowerShell
for /f "delims=" %%A in ('powershell -NoProfile -Command "function Gen-Pass {param([int]$len=16) ([char[]](33..126) | sort {Get-Random} | join('') | substring(0,$len)}; Gen-Pass -len 20"') do set SQL_PASS=%%A

for /f "delims=" %%A in ('powershell -NoProfile -Command "function Gen-Pass {param([int]$len=16) ([char[]](33..126) | sort {Get-Random} | join('') | substring(0,$len)}; Gen-Pass -len 16"') do set RABBITMQ_ADMIN_PASS=%%A

for /f "delims=" %%A in ('powershell -NoProfile -Command "function Gen-Pass {param([int]$len=16) ([char[]](33..126) | sort {Get-Random} | join('') | substring(0,$len)}; Gen-Pass -len 16"') do set RABBITMQ_APP_PASS=%%A

REM Create .env file
(
echo # SQL Server Configuration
echo SA_PASSWORD=%SQL_PASS%
echo SQLSERVER_DATABASE_1=MS1Db
echo SQLSERVER_DATABASE_2=MS2Db
echo SQLSERVER_DATABASE_3=MS3Db
echo.
echo # RabbitMQ Configuration
echo RABBITMQ_USER=appuser
echo RABBITMQ_PASSWORD=%RABBITMQ_APP_PASS%
echo RABBITMQ_DEFAULT_USER=admin
echo RABBITMQ_DEFAULT_PASS=%RABBITMQ_ADMIN_PASS%
echo.
echo # Service Configuration
echo ASPNETCORE_URLS=http://+:8080
echo ASPNETCORE_ENVIRONMENT=Development
) > .env

echo.
echo ✓ .env file created successfully!
echo.
echo Generated Credentials:
echo SQL Server SA Password: %SQL_PASS%
echo RabbitMQ Admin User: admin
echo RabbitMQ Admin Password: %RABBITMQ_ADMIN_PASS%
echo RabbitMQ App User: appuser
echo RabbitMQ App Password: %RABBITMQ_APP_PASS%
echo.
echo ⚠️  KEEP THIS FILE SECURE - .env is already in .gitignore!
echo File location: %cd%\.env
echo.
pause
