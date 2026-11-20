# Automatic Password Configuration Guide

This guide explains how to configure automatic password generation for SQL Server and RabbitMQ in your microservices environment.

## Overview

The setup uses Docker Compose environment variables (`.env` file) to manage passwords securely without manual intervention.

### Key Features
✅ Auto-generated secure passwords  
✅ Centralized configuration via `.env`  
✅ Services access credentials automatically  
✅ Safe password management (`.env` is gitignored)  
✅ Custom RabbitMQ user creation  

---

## Setup Instructions

### Step 1: Generate Secure Passwords

Run the PowerShell script to generate secure passwords:

```powershell
.\scripts\generate-env.ps1
```

This will:
- Generate 20-character SQL Server password
- Generate 16-character RabbitMQ admin password
- Generate 16-character RabbitMQ app user password
- Create a `.env` file with all credentials

### Step 2: Verify `.env` File

The script creates `.env` with content like:

```env
# SQL Server Configuration
SA_PASSWORD=YourSecureGeneratedPassword!@#
SQLSERVER_DATABASE_1=MS1Db
SQLSERVER_DATABASE_2=MS2Db
SQLSERVER_DATABASE_3=MS3Db

# RabbitMQ Configuration
RABBITMQ_USER=appuser
RABBITMQ_PASSWORD=AppUserSecurePassword!@#
RABBITMQ_DEFAULT_USER=admin
RABBITMQ_DEFAULT_PASS=AdminSecurePassword!@#

# Service Configuration
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_ENVIRONMENT=Development
```

### Step 3: Start Docker Containers

```bash
docker-compose up -d
```

**What happens automatically:**
- SQL Server starts with the generated `SA_PASSWORD`
- RabbitMQ starts with admin credentials
- RabbitMQ initialization script creates custom `appuser`

### Step 4: Configure Your Microservices

Uncomment the microservice definitions in `docker-compose.yml` and use environment variables:

#### For SQL Server Connection

```yaml
ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=MS1Db;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
```

#### For RabbitMQ Connection

```yaml
RabbitMQ__Host: "rabbitmq"
RabbitMQ__Port: "5672"
RabbitMQ__UserName: "${RABBITMQ_USER}"
RabbitMQ__Password: "${RABBITMQ_PASSWORD}"
```

---

## File Structure

```
project-root/
├── docker-compose.yml          # Main Docker Compose file
├── docker-compose.reference.yml # Reference with all services
├── .env                         # Generated credentials (GITIGNORED)
├── .env.example                 # Template for new developers
├── .gitignore                   # Includes .env
├── docker/
│   └── rabbitmq-init.sh        # RabbitMQ initialization script
└── scripts/
	└── generate-env.ps1        # Password generation script
```

---

## RabbitMQ User Setup Details

The `docker/rabbitmq-init.sh` script automatically:

1. **Waits** for RabbitMQ service to be ready
2. **Creates** a custom application user (`appuser`)
3. **Sets permissions** for the user on the virtual host
4. **Grants** administrator privileges (optional)

```bash
#!/bin/bash
# Runs inside RabbitMQ container after startup
rabbitmqctl add_user "$RABBITMQ_USER" "$RABBITMQ_PASSWORD"
rabbitmqctl set_permissions -p / "$RABBITMQ_USER" ".*" ".*" ".*"
rabbitmqctl set_user_tags "$RABBITMQ_USER" administrator
```

### Access RabbitMQ Management UI

- URL: `http://localhost:15672`
- Username: `admin`
- Password: Check your `.env` file for `RABBITMQ_DEFAULT_PASS`

---

## SQL Server Connection Details

- **Host**: `sqlserver` (within Docker network) or `localhost` (from host machine)
- **Port**: `1434` (mapped from container's 1433)
- **Username**: `sa`
- **Password**: Value of `SA_PASSWORD` in `.env`
- **Connection String Example**:
  ```
  Server=sqlserver,1433;Database=MS1Db;User Id=sa;Password=YourGeneratedPassword;TrustServerCertificate=True;Encrypt=False;
  ```

---

## Security Best Practices

⚠️ **Important:**

1. **Never commit `.env` file** - It's automatically gitignored
2. **Share `.env.example` instead** - Shows structure without secrets
3. **Generate new passwords** for production:
   ```powershell
   .\scripts\generate-env.ps1
   ```
4. **Use Docker secrets** for production deployments in Kubernetes

---

## Troubleshooting

### Services can't connect to RabbitMQ/SQL

**Check environment variables:**
```bash
docker ps
docker inspect rabbitmq  # Check if env vars are set
```

### RabbitMQ custom user not created

**Check RabbitMQ logs:**
```bash
docker logs rabbitmq
```

**Ensure script permissions (Linux/Mac):**
```bash
chmod +x docker/rabbitmq-init.sh
```

### SQL Server connection refused

**Verify `.env` password matches** in connection string  
**Check SQL Server logs:**
```bash
docker logs sqlserver
```

---

## Manual Password Change

If you need to change passwords after containers are running:

1. **Stop containers:**
   ```bash
   docker-compose down
   ```

2. **Edit `.env` file** with new passwords

3. **Restart containers:**
   ```bash
   docker-compose up -d
   ```

---

## Integration with .NET Configuration

In your `Program.cs` or `appsettings.json`:

```csharp
// Program.cs example
builder.Services.AddDbContext<ApplicationDbContext>(options =>
	options.UseSqlServer(
		builder.Configuration.GetConnectionString("DefaultConnection")
	)
);

// Connection string with environment variable interpolation
// appsettings.json
{
  "ConnectionStrings": {
	"DefaultConnection": "Server=sqlserver,1433;Database=MS1Db;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
  },
  "RabbitMQ": {
	"Host": "rabbitmq",
	"Port": 5672,
	"UserName": "${RABBITMQ_USER}",
	"Password": "${RABBITMQ_PASSWORD}"
  }
}
```

---

## For New Developers

1. Clone the repository
2. Run: `.\scripts\generate-env.ps1`
3. Run: `docker-compose up -d`
4. All services start with secure, auto-generated passwords
5. No manual password entry needed! ✓

---

## Environment Variables Reference

| Variable | Purpose | Example |
|----------|---------|---------|
| `SA_PASSWORD` | SQL Server admin password | 20-char generated |
| `RABBITMQ_USER` | App user for RabbitMQ | `appuser` |
| `RABBITMQ_PASSWORD` | App user password | 16-char generated |
| `RABBITMQ_DEFAULT_USER` | RabbitMQ admin user | `admin` |
| `RABBITMQ_DEFAULT_PASS` | RabbitMQ admin password | 16-char generated |
| `ASPNETCORE_ENVIRONMENT` | .NET environment | `Development` |

