#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 3 ]]; then
  echo 'Usage: bash scripts/configure-azure.sh <resource-group> <account-name> <deployment-name>' >&2
  exit 2
fi
opspilot_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
opspilot_endpoint="$(az cognitiveservices account show -g "$1" -n "$2" --query 'properties.endpoints."OpenAI Language Model Instance API"' -o tsv --only-show-errors)"
if [[ -z "$opspilot_endpoint" ]]; then
  opspilot_endpoint="$(az cognitiveservices account show -g "$1" -n "$2" --query properties.endpoint -o tsv --only-show-errors)"
fi
opspilot_state="$(az cognitiveservices account deployment show -g "$1" -n "$2" --deployment-name "$3" --query properties.provisioningState -o tsv --only-show-errors)"
[[ "$opspilot_state" == 'Succeeded' ]] || { echo 'Deployment is not ready.' >&2; exit 1; }
opspilot_tenant="$(az account show --query tenantId -o tsv --only-show-errors)"
dotnet user-secrets set AZURE_OPENAI_ENDPOINT "$opspilot_endpoint" --project "$opspilot_root/src/OpsPilot.Web" >/dev/null
dotnet user-secrets set AZURE_OPENAI_DEPLOYMENT "$3" --project "$opspilot_root/src/OpsPilot.Web" >/dev/null
dotnet user-secrets set AZURE_TENANT_ID "$opspilot_tenant" --project "$opspilot_root/src/OpsPilot.Web" >/dev/null
echo 'OpsPilot user-secrets configured. No keys retrieved or Azure resources changed.'
