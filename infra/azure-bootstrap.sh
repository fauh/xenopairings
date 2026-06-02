#!/usr/bin/env bash
# azure-bootstrap.sh — one-time Azure provisioning for Xenopairings
#
# Run this script once to create all the Azure resources the app needs.
# Prerequisites:
#   - az CLI installed and authenticated (`az login`)
#   - The Azure subscription you want to use is selected (`az account set -s <id>`)
#
# After running this script, you need to:
#   1. Configure App Service to pull the GHCR image (see Step 5 comment below)
#   2. Set application settings in the portal (DATA_DIR, email secrets, etc.)
#   3. Add the AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID secrets to
#      GitHub repository secrets for the OIDC deploy workflow.
#
# Usage:
#   chmod +x infra/azure-bootstrap.sh
#   ./infra/azure-bootstrap.sh
#
# To customise names, export environment variables before running:
#   export APP_NAME=my-xenopairings
#   ./infra/azure-bootstrap.sh

set -euo pipefail

# ── Configuration ─────────────────────────────────────────────────────────────
APP_NAME="${APP_NAME:-xenopairings}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-${APP_NAME}}"
LOCATION="${LOCATION:-swedencentral}"
APP_SERVICE_PLAN="${APP_SERVICE_PLAN:-asp-${APP_NAME}}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-${APP_NAME}storage}"   # must be globally unique, 3-24 lowercase alphanumeric
BACKUP_CONTAINER="${BACKUP_CONTAINER:-backups}"
GHCR_IMAGE="${GHCR_IMAGE:-ghcr.io/fauh/xenopairings:latest}"

echo "=== Xenopairings — Azure bootstrap ==="
echo "Resource group : ${RESOURCE_GROUP}"
echo "Location       : ${LOCATION}"
echo "App name       : ${APP_NAME}"
echo "Storage account: ${STORAGE_ACCOUNT}"
echo ""

# ── Step 1: Resource group ────────────────────────────────────────────────────
echo "--- Creating resource group ---"
az group create \
  --name "${RESOURCE_GROUP}" \
  --location "${LOCATION}"

# ── Step 2: App Service plan (B1 Linux — always on, ~€10/month) ───────────────
echo "--- Creating App Service plan (B1 Linux) ---"
az appservice plan create \
  --name "${APP_SERVICE_PLAN}" \
  --resource-group "${RESOURCE_GROUP}" \
  --sku B1 \
  --is-linux

# ── Step 3: Web App (Linux container) ────────────────────────────────────────
echo "--- Creating Web App ---"
az webapp create \
  --name "${APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --plan "${APP_SERVICE_PLAN}" \
  --deployment-container-image-name "${GHCR_IMAGE}"

# ── Step 4: System-assigned managed identity ─────────────────────────────────
echo "--- Enabling managed identity ---"
az webapp identity assign \
  --name "${APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}"

# Capture the principal ID for role assignment below
PRINCIPAL_ID=$(az webapp identity show \
  --name "${APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query principalId \
  --output tsv)
echo "Managed identity principal ID: ${PRINCIPAL_ID}"

# ── Step 5: Storage account + backup container ────────────────────────────────
echo "--- Creating storage account ---"
az storage account create \
  --name "${STORAGE_ACCOUNT}" \
  --resource-group "${RESOURCE_GROUP}" \
  --location "${LOCATION}" \
  --sku Standard_LRS \
  --kind StorageV2 \
  --access-tier Hot \
  --allow-blob-public-access false

echo "--- Creating backup blob container ---"
az storage container create \
  --name "${BACKUP_CONTAINER}" \
  --account-name "${STORAGE_ACCOUNT}" \
  --auth-mode login

# ── Step 6: Role assignment — managed identity → Storage Blob Data Contributor ─
echo "--- Assigning Storage Blob Data Contributor to managed identity ---"
STORAGE_ID=$(az storage account show \
  --name "${STORAGE_ACCOUNT}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query id \
  --output tsv)

az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee-object-id "${PRINCIPAL_ID}" \
  --assignee-principal-type ServicePrincipal \
  --scope "${STORAGE_ID}"

# ── Step 7: App Service application settings ─────────────────────────────────
echo "--- Setting application settings ---"
STORAGE_URL="https://${STORAGE_ACCOUNT}.blob.core.windows.net"

az webapp config appsettings set \
  --name "${APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --settings \
    DATA_DIR="/home/data" \
    ASPNETCORE_ENVIRONMENT="Production" \
    Backup__StorageAccountUri="${STORAGE_URL}" \
    Backup__ContainerName="${BACKUP_CONTAINER}"

echo ""
echo ">>> IMPORTANT: Set the following secrets manually (not in this script):"
echo "    az webapp config appsettings set --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \\"
echo "      --settings EmailSettings__ApiKey=<your-resend-api-key>"
echo "    az webapp config appsettings set --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \\"
echo "      --settings EmailSettings__BaseUrl=https://<your-app-url>"
echo "    az webapp config appsettings set --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \\"
echo "      --settings EmailSettings__UseRealProvider=true"

# ── Step 8: GHCR credentials on App Service ──────────────────────────────────
echo ""
echo ">>> Configure GHCR pull credentials:"
echo "    az webapp config container set \\"
echo "      --name ${APP_NAME} --resource-group ${RESOURCE_GROUP} \\"
echo "      --docker-registry-server-url https://ghcr.io \\"
echo "      --docker-registry-server-user <github-username> \\"
echo "      --docker-registry-server-password <ghcr-pat-with-read-packages>"

# ── Step 9: OIDC federated credentials for GitHub Actions ────────────────────
echo "--- Creating Azure AD app for GitHub Actions OIDC ---"
APP_ID=$(az ad app create \
  --display-name "xenopairings-github-actions" \
  --query appId \
  --output tsv)
echo "Azure AD App ID: ${APP_ID}"

SERVICE_PRINCIPAL_ID=$(az ad sp create \
  --id "${APP_ID}" \
  --query id \
  --output tsv)
echo "Service Principal Object ID: ${SERVICE_PRINCIPAL_ID}"

# Assign Contributor on the resource group so the workflow can restart the app
SUBSCRIPTION_ID=$(az account show --query id --output tsv)
az role assignment create \
  --role "Contributor" \
  --assignee-object-id "${SERVICE_PRINCIPAL_ID}" \
  --assignee-principal-type ServicePrincipal \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}"

# Federated credential for push to main
az ad app federated-credential create \
  --id "${APP_ID}" \
  --parameters "{
    \"name\": \"github-main\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:fauh/xenopairings:ref:refs/heads/main\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

TENANT_ID=$(az account show --query tenantId --output tsv)

echo ""
echo "=== Bootstrap complete ==="
echo ""
echo "Add these as GitHub repository secrets:"
echo "  AZURE_CLIENT_ID       = ${APP_ID}"
echo "  AZURE_TENANT_ID       = ${TENANT_ID}"
echo "  AZURE_SUBSCRIPTION_ID = ${SUBSCRIPTION_ID}"
echo "  AZURE_RESOURCE_GROUP  = ${RESOURCE_GROUP}"
echo "  AZURE_APP_NAME        = ${APP_NAME}"
echo ""
echo "Then push to main — the GitHub Actions workflow will build + deploy."
