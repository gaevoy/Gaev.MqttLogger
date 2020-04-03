#!/bin/bash
ssh root@app.gaevoy.com 'bash -s' <<'ENDSSH'
printf "Stopping service...\n"
systemctl stop GaevMqttLogger
printf "Service is "
systemctl is-active GaevMqttLogger
mkdir -p /apps/GaevMqttLogger
ENDSSH

printf "Uploading new version of service...\n"
rsync -v -a ./bin/Release/netcoreapp3.0/ubuntu.18.04-x64/publish/ root@app.gaevoy.com:/apps/GaevMqttLogger/

ssh root@app.gaevoy.com 'bash -s' <<'ENDSSH'
chmod 777 /apps/GaevMqttLogger/Gaev.MqttLogger
if [[ ! -e /etc/systemd/system/GaevMqttLogger.service ]]; then
    printf "Installing service...\n"
    cat > /etc/systemd/system/GaevMqttLogger.service <<'EOF'
    [Unit]
    Description=GaevMqttLogger
    After=network.target
    
    [Service]
    WorkingDirectory=/apps/GaevMqttLogger
    ExecStart=/apps/GaevMqttLogger/Gaev.MqttLogger
    Restart=always
    KillSignal=SIGINT
    
    [Install]
    WantedBy=multi-user.target
EOF
    systemctl daemon-reload
    systemctl enable GaevMqttLogger
fi
printf "Starting service...\n"
systemctl start GaevMqttLogger
printf "Service is "
systemctl is-active GaevMqttLogger
ENDSSH