#!/usr/bin/env bash
# DayPlannio - Setup da VM Ubuntu: instala .NET 10, clona o repositorio,
# publica a API e o Web, e sobe os dois como servicos (systemd) atras do nginx.
#
# Antes de rodar:
#   cp vm.env.example vm.env    # e preencha os segredos
#
# Rode como root:
#   sudo bash vm-setup.sh
#
# Resultado:
#   http://<IP>/       -> portal Web
#   http://<IP>/api/   -> API
set -euo pipefail

APP_DIR="/opt/dayplannio"
SRC_DIR="$APP_DIR/src"
API_PUBLISH="$APP_DIR/api"
WEB_PUBLISH="$APP_DIR/web"
API_PORT="5000"
WEB_PORT="5001"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/vm.env"

if [ "$(id -u)" -ne 0 ]; then
  echo "Rode como root: sudo bash vm-setup.sh"
  exit 1
fi

if [ ! -f "$ENV_FILE" ]; then
  echo "Arquivo vm.env nao encontrado em $ENV_FILE"
  echo "Rode: cp vm.env.example vm.env  (e preencha os segredos)"
  exit 1
fi

# shellcheck disable=SC1090
set -a
source "$ENV_FILE"
set +a

need() { [ -n "${!1:-}" ] || { echo "ERRO: variavel $1 nao definida em vm.env"; exit 1; }; }
need MONGO_CONNECTION_STRING
need WEB_API_KEY
need AZURE_BLOB_CONNECTION
need GMAIL_USER
need GMAIL_PASSWORD
need ADMIN_EMAIL
need ADMIN_SENHA
: "${MONGO_DATABASE:=dbDayPlannio}"
: "${BLOB_CONTAINER:=fotos}"
: "${ADMIN_NOME:=Administrador}"
: "${REPO_URL:=https://github.com/DayPlannio/DayPlannio.git}"
: "${GIT_SSH_KEY:=}"
: "${SKIP_GIT:=0}"
: "${VM_TIMEZONE:=America/Sao_Paulo}"
: "${GEOFENCING_CONFIRMACAO_MINUTOS:=5}"
: "${GEOFENCING_TOLERANCIA_SAIDA_METROS:=15}"
: "${GEOFENCING_TOLERANCIA_PAUSA_MINUTOS:=30}"
: "${GEOFENCING_JANELA_HORAS:=24}"
: "${GEOFENCING_PRECISAO_MAXIMA_METROS:=0}"

# Repositorio privado: usa a deploy key via SSH (defina GIT_SSH_KEY no vm.env)
if [ -n "$GIT_SSH_KEY" ] && [ -f "$GIT_SSH_KEY" ]; then
  export GIT_SSH_COMMAND="ssh -i $GIT_SSH_KEY -o StrictHostKeyChecking=accept-new"
fi

echo "==> Instalando dependencias do sistema..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y git curl nginx libfontconfig1 libfreetype6 tzdata

echo "==> Ajustando o fuso horario ($VM_TIMEZONE)..."
timedatectl set-timezone "$VM_TIMEZONE" || echo "    Nao foi possivel ajustar o fuso; seguindo com o padrao do sistema."

echo "==> Configurando swap (ajuda no build em VMs pequenas)..."
if [ ! -f /swapfile ]; then
  fallocate -l 2G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=2048
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "==> Instalando o SDK do .NET 10..."
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
if ! command -v dotnet >/dev/null 2>&1; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi
dotnet --version

echo "==> Obtendo o codigo-fonte..."
mkdir -p "$APP_DIR"
if [ "$SKIP_GIT" = "1" ]; then
  [ -f "$SRC_DIR/DayPlannio.Web/DayPlannio.Web.csproj" ] || { echo "ERRO: SKIP_GIT=1 mas o codigo nao esta em $SRC_DIR"; exit 1; }
  echo "    Usando o codigo ja presente em $SRC_DIR."
elif [ -d "$SRC_DIR/.git" ]; then
  echo "    Atualizando repositorio ($REPO_URL)..."
  git -C "$SRC_DIR" pull --ff-only
else
  echo "    Clonando repositorio ($REPO_URL)..."
  git clone --depth 1 "$REPO_URL" "$SRC_DIR"
fi

echo "==> Publicando a API..."
rm -rf "$API_PUBLISH"
dotnet publish "$SRC_DIR/DayPlannio.Api/DayPlannio.Api/DayPlannio.Api.csproj" -c Release -o "$API_PUBLISH"

echo "==> Publicando o Web..."
rm -rf "$WEB_PUBLISH"
dotnet publish "$SRC_DIR/DayPlannio.Web/DayPlannio.Web.csproj" -c Release -o "$WEB_PUBLISH"

echo "==> Gerando arquivos de ambiente dos servicos..."
cat > "$APP_DIR/api.env" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:$API_PORT
MongoConnection__ConnectionString=$MONGO_CONNECTION_STRING
MongoConnection__Database=$MONGO_DATABASE
MongoConnection__IsSSL=true
WebApiKey=$WEB_API_KEY
EmailSettings__SmtpServer=smtp.gmail.com
EmailSettings__SmtpPort=587
EmailSettings__SenderName=DayPlannio
EmailSettings__SenderEmail=$GMAIL_USER
EmailSettings__Username=$GMAIL_USER
EmailSettings__Password=$GMAIL_PASSWORD
AzureBlobStorage__ConnectionString=$AZURE_BLOB_CONNECTION
AzureBlobStorage__ContainerName=$BLOB_CONTAINER
AdminSettings__Email=$ADMIN_EMAIL
AdminSettings__Senha=$ADMIN_SENHA
AdminSettings__Nome=$ADMIN_NOME
Geofencing__ConfirmacaoMinutos=$GEOFENCING_CONFIRMACAO_MINUTOS
Geofencing__ToleranciaSaidaMetros=$GEOFENCING_TOLERANCIA_SAIDA_METROS
Geofencing__ToleranciaPausaMinutos=$GEOFENCING_TOLERANCIA_PAUSA_MINUTOS
Geofencing__JanelaHoras=$GEOFENCING_JANELA_HORAS
Geofencing__PrecisaoMaximaMetros=$GEOFENCING_PRECISAO_MAXIMA_METROS
EOF
chmod 600 "$APP_DIR/api.env"

cat > "$APP_DIR/web.env" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:$WEB_PORT
Api__BaseUrl=http://127.0.0.1:$API_PORT/
Api__Key=$WEB_API_KEY
EOF
chmod 600 "$APP_DIR/web.env"

echo "==> Criando os servicos (systemd)..."
cat > /etc/systemd/system/dayplannio-api.service <<EOF
[Unit]
Description=DayPlannio API
After=network.target

[Service]
WorkingDirectory=$API_PUBLISH
ExecStart=/usr/bin/dotnet $API_PUBLISH/DayPlannio.Api.dll
EnvironmentFile=$APP_DIR/api.env
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

cat > /etc/systemd/system/dayplannio-web.service <<EOF
[Unit]
Description=DayPlannio Web
After=network.target

[Service]
WorkingDirectory=$WEB_PUBLISH
ExecStart=/usr/bin/dotnet $WEB_PUBLISH/DayPlannio.Web.dll
EnvironmentFile=$APP_DIR/web.env
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

echo "==> Configurando o nginx como proxy reverso..."
cat > /etc/nginx/sites-available/dayplannio <<'EOF'
server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name _;

    client_max_body_size 15m;

    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        proxy_pass http://127.0.0.1:5001;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF

ln -sf /etc/nginx/sites-available/dayplannio /etc/nginx/sites-enabled/dayplannio
rm -f /etc/nginx/sites-enabled/default
nginx -t

echo "==> Subindo os servicos..."
systemctl daemon-reload
systemctl enable dayplannio-api dayplannio-web nginx >/dev/null 2>&1
systemctl restart dayplannio-api
systemctl restart dayplannio-web
systemctl restart nginx

sleep 3
echo
echo "==> Status:"
echo "  API:   $(systemctl is-active dayplannio-api || true)"
echo "  Web:   $(systemctl is-active dayplannio-web || true)"
echo "  nginx: $(systemctl is-active nginx || true)"

IP="$(curl -s --max-time 5 http://169.254.169.254/metadata/instance/network/interface/0/ipv4/ipAddress/0/publicIpAddress?api-version=2021-02-01\&format=text -H Metadata:true 2>/dev/null || true)"
echo
echo "==================================================="
echo " Tudo pronto!"
echo " Portal Web: http://$IP/"
echo " API:        http://$IP/api/"
echo "==================================================="
echo " Para ver os logs:  journalctl -u dayplannio-api -f"
echo "                    journalctl -u dayplannio-web -f"
