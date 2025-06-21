#!/bin/bash

set -e

GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
    exit 1
}

PROJECT_NAME="ecommerce-signalr-service"
BACKUP_DIR="/var/backups/$PROJECT_NAME"
LOG_FILE="/var/log/$PROJECT_NAME-deploy.log"

log "Starting deployment for $PROJECT_NAME"

mkdir -p "$BACKUP_DIR" nginx/logs

if docker compose ps -q > /dev/null 2>&1; then
    log "Stopping running containers and backing up project"
    docker compose down
    tar --exclude='.git' --exclude='bin' --exclude='obj' -czf "$BACKUP_DIR/backup-$(date +%Y%m%d-%H%M%S).tar.gz" .
fi

# if [[ -d .git ]]; then
#     log "Pulling latest code"
#     git pull origin main || git pull origin master
# fi

log "Building services"
docker compose build --no-cache

log "Starting containers"
docker compose up -d

log "Waiting for app to initialize..."
sleep 10

if docker compose ps | grep -q "Up"; then
    log "Services running successfully"
else
    error "Some services failed to start"
fi

if curl -fs http://localhost:5005/health > /dev/null; then
    log "Health check passed"
else
    echo -e "${YELLOW}[WARNING] Health check failed${NC}"
fi

docker compose ps
