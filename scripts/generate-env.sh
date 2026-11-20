#!/bin/bash

# Generate Secure Passwords for Docker Compose - Linux/Mac
# This script generates secure passwords and creates a .env file

generate_password() {
	local length=${1:-16}
	openssl rand -base64 ${length} | tr -d "=+/" | cut -c1-${length}
}

echo ""
echo "Generating secure passwords for Docker Compose..."
echo ""

# Generate passwords
SQL_PASS=$(generate_password 20)
RABBITMQ_ADMIN_PASS=$(generate_password 16)
RABBITMQ_APP_PASS=$(generate_password 16)

# Create .env file
cat > .env << EOF
# SQL Server Configuration
SA_PASSWORD=${SQL_PASS}
SQLSERVER_DATABASE_1=MS1Db
SQLSERVER_DATABASE_2=MS2Db
SQLSERVER_DATABASE_3=MS3Db

# RabbitMQ Configuration
RABBITMQ_USER=appuser
RABBITMQ_PASSWORD=${RABBITMQ_APP_PASS}
RABBITMQ_DEFAULT_USER=admin
RABBITMQ_DEFAULT_PASS=${RABBITMQ_ADMIN_PASS}

# Service Configuration
ASPNETCORE_URLS=http://+:8080
ASPNETCORE_ENVIRONMENT=Development
EOF

echo ""
echo "✓ .env file created successfully!"
echo ""
echo "Generated Credentials:"
echo "SQL Server SA Password: ${SQL_PASS}"
echo "RabbitMQ Admin User: admin"
echo "RabbitMQ Admin Password: ${RABBITMQ_ADMIN_PASS}"
echo "RabbitMQ App User: appuser"
echo "RabbitMQ App Password: ${RABBITMQ_APP_PASS}"
echo ""
echo "⚠️  KEEP THIS FILE SECURE - .env is already in .gitignore!"
echo "File location: $(pwd)/.env"
echo ""
