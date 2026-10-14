#!/usr/bin/env bash
# DayPlannio - Cria a VM Ubuntu no Azure (Azure for Students) e abre as portas 80/443.
# Feito para rodar no Azure Cloud Shell (modo Bash).
#
# Uso:
#   bash deploy/azure-create-vm.sh
# Variaveis opcionais (pode exportar antes de rodar):
#   RG, LOCATION, VM, ADMIN_USER, SIZE, IMAGE
set -euo pipefail

RG="${RG:-DayPlannio}"
LOCATION="${LOCATION:-canadacentral}"
VM="${VM:-dayplannio-vm}"
ADMIN_USER="${ADMIN_USER:-azureuser}"
SIZE="${SIZE:-Standard_B2als_v2}"
IMAGE="${IMAGE:-Ubuntu2404}"

az account show >/dev/null 2>&1 || { echo "Rode primeiro: az login"; exit 1; }

echo "==> Criando resource group $RG ($LOCATION)..."
az group create --name "$RG" --location "$LOCATION" -o none

echo "==> Criando a VM $VM ($SIZE / $IMAGE)..."
az vm create \
  --resource-group "$RG" \
  --name "$VM" \
  --image "$IMAGE" \
  --size "$SIZE" \
  --admin-username "$ADMIN_USER" \
  --generate-ssh-keys \
  --public-ip-sku Standard -o none

echo "==> Liberando portas 80 e 443 (SSH ja vem liberada)..."
az vm open-port --resource-group "$RG" --name "$VM" --port 80  --priority 1010 -o none
az vm open-port --resource-group "$RG" --name "$VM" --port 443 --priority 1020 -o none

IP=$(az vm show -d --resource-group "$RG" --name "$VM" --query publicIps -o tsv)

echo
echo "==================================================="
echo " VM criada com sucesso!"
echo " IP publico: $IP"
echo " Conecte com: ssh $ADMIN_USER@$IP"
echo "==================================================="
