#!/bin/bash

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
PROJECT_NAME="ecommerce-signalr-service"
BACKUP_DIR="/var/backups/$PROJECT_NAME"
LOG_FILE="/var/log/$PROJECT_NAME-deploy.log"

# Functions
log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] $1${NC}" | tee -a "$LOG_FILE"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}" | tee -a "$LOG_FILE"
    exit 1
}

warning() {
    echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}" | tee -a "$LOG_FILE"
}

# Check if running as root
if [[ $EUID -eq 0 ]]; then
    error "This script should not be run as root for security reasons"
fi

# Check if .env file exists
if [[ ! -f .env ]]; then
    error ".env file not found. Please copy .env.example to .env and configure it."
fi

# Source environment variables
source .env

log "Starting deployment for $PROJECT_NAME"

# Create necessary directories
mkdir -p "$BACKUP_DIR" nginx/logs ssl-certs

# Backup current deployment
if docker-compose ps -q > /dev/null 2>&1; then
    log "Creating backup of current deployment"
    docker-compose down
    tar --exclude='.git' --exclude='bin' --exclude='obj' -czf "$BACKUP_DIR/backup-$(date +%Y%m%d-%H%M%S).tar.gz" .
fi

# Pull latest changes (if Git repository)
if [[ -d .git ]]; then
    log "Pulling latest changes from git"
    git pull origin main || git pull origin master
fi

# Build and start services
log "Building Docker images"
docker-compose build --no-cache

log "Starting services"
docker-compose up -d

# Wait briefly for containers to stabilize
log "Waiting for containers to initialize"
sleep 15

# Check if services are running
if docker-compose ps | grep -q "Up"; then
    log "Services are running successfully"
else
    error "Some services failed to start"
fi

# Run health check
if curl -fs http://localhost/health > /dev/null; then
    log "Health check passed"
else
    warning "Health check failed, continuing deployment"
fi

log "Deployment completed successfully"
docker-compose ps
