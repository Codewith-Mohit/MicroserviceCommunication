#!/usr/bin/env pwsh

# Quick Kubernetes Cluster Setup Helper

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Kubernetes Cluster Setup Helper                            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check if kubectl can connect
Write-Host "Checking Kubernetes connection..." -ForegroundColor Yellow

$clusterCheck = kubectl cluster-info 2>&1
if ($LASTEXITCODE -eq 0) {
	Write-Host "✅ Kubernetes cluster is RUNNING and accessible!" -ForegroundColor Green
	Write-Host ""
	Write-Host $clusterCheck -ForegroundColor Gray
	Write-Host ""
	Write-Host "You can now deploy your microservices:" -ForegroundColor Green
	Write-Host ""
	Write-Host "  cd kubernetes" -ForegroundColor Cyan
	Write-Host "  .\deploy-all.ps1 -RegistryUrl ""docker.io/mt1mngnw"" -ImageTag ""latest""" -ForegroundColor Cyan
	Write-Host ""
	exit 0
}

Write-Host "❌ No Kubernetes cluster found!" -ForegroundColor Red
Write-Host ""
Write-Host "Choose one of these options to set up Kubernetes:" -ForegroundColor Yellow
Write-Host ""

Write-Host "┌─ OPTION 1: Docker Desktop + Kubernetes (Recommended) ─────────┐" -ForegroundColor Green
Write-Host "│                                                                │" -ForegroundColor Green
Write-Host "│ 1. Open Docker Desktop                                         │" -ForegroundColor Green
Write-Host "│ 2. Settings > Kubernetes > Enable Kubernetes                   │" -ForegroundColor Green
Write-Host "│ 3. Wait 3-5 minutes                                            │" -ForegroundColor Green
Write-Host "│ 4. Run this script again to verify                             │" -ForegroundColor Green
Write-Host "│                                                                │" -ForegroundColor Green
Write-Host "│ Pros: Simple, fast, perfect for learning                       │" -ForegroundColor Green
Write-Host "│ Cons: Single node, local only                                  │" -ForegroundColor Green
Write-Host "│                                                                │" -ForegroundColor Green
Write-Host "└────────────────────────────────────────────────────────────────┘" -ForegroundColor Green
Write-Host ""

Write-Host "┌─ OPTION 2: MiniKube ─────────────────────────────────────────┐" -ForegroundColor Cyan
Write-Host "│                                                                │" -ForegroundColor Cyan
Write-Host "│ Run these commands:                                            │" -ForegroundColor Cyan
Write-Host "│   choco install minikube                                       │" -ForegroundColor Yellow
Write-Host "│   minikube start                                               │" -ForegroundColor Yellow
Write-Host "│   kubectl config use-context minikube                          │" -ForegroundColor Yellow
Write-Host "│                                                                │" -ForegroundColor Cyan
Write-Host "│ Pros: More realistic, multi-node capable                       │" -ForegroundColor Cyan
Write-Host "│ Cons: More setup required                                      │" -ForegroundColor Cyan
Write-Host "│                                                                │" -ForegroundColor Cyan
Write-Host "└────────────────────────────────────────────────────────────────┘" -ForegroundColor Cyan
Write-Host ""

Write-Host "┌─ OPTION 3: Azure AKS ────────────────────────────────────────┐" -ForegroundColor Blue
Write-Host "│                                                                │" -ForegroundColor Blue
Write-Host "│ Run these commands:                                            │" -ForegroundColor Blue
Write-Host "│   az login                                                     │" -ForegroundColor Yellow
Write-Host "│   az aks create -g myRG -n myCluster --node-count 1            │" -ForegroundColor Yellow
Write-Host "│   az aks get-credentials -g myRG -n myCluster                  │" -ForegroundColor Yellow
Write-Host "│                                                                │" -ForegroundColor Blue
Write-Host "│ Pros: Production-ready, cloud-native                           │" -ForegroundColor Blue
Write-Host "│ Cons: Requires Azure account, costs money                      │" -ForegroundColor Blue
Write-Host "│                                                                │" -ForegroundColor Blue
Write-Host "└────────────────────────────────────────────────────────────────┘" -ForegroundColor Blue
Write-Host ""

Write-Host "📖 For detailed setup instructions, see:" -ForegroundColor Yellow
Write-Host "   KUBERNETES_SETUP_REQUIRED.md" -ForegroundColor Cyan
Write-Host ""
