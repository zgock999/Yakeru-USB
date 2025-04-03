#!/bin/bash

# Yakeru-USB バックエンドサーバー起動スクリプト (Linux/macOS用)
echo "Yakeru-USB バックエンドサーバーを起動します..."
echo "管理者権限が必要です。"
echo ""

# 現在のユーザーが管理者権限を持っているか確認
if [ "$(id -u)" != "0" ]; then
   echo "このスクリプトは管理者権限で実行する必要があります。" 
   echo "sudo ./start_backend.sh を実行してください。"
   exec sudo "$0" "$@"
   exit
fi

echo "USBデバイスを検出して、ISOファイルを書き込みます。"
cd "$(dirname "$0")/backend"

# 仮想環境があれば有効化
if [ -f "venv/bin/activate" ]; then
    echo "仮想環境を有効化しています..."
    source venv/bin/activate
fi

# USB権限を確保
if [ -d "/dev" ]; then
    echo "USBデバイスの権限を設定しています..."
    chmod -R a+rw /dev/sd*  2>/dev/null || true
fi

# polkitルールを追加（オプション）
if [ ! -f "/etc/polkit-1/rules.d/99-yakeru-usb.rules" ]; then
    echo "polkitルールの作成を試みています..."
    cat > /tmp/99-yakeru-usb.rules << 'EOL'
polkit.addRule(function(action, subject) {
    if (action.id == "org.freedesktop.udisks2.filesystem-mount" ||
        action.id == "org.freedesktop.udisks2.filesystem-unmount-others" ||
        action.id == "org.freedesktop.udisks2.eject-media" ||
        action.id == "org.freedesktop.udisks2.power-off-drive") {
        return polkit.Result.YES;
    }
});
EOL
    cp /tmp/99-yakeru-usb.rules /etc/polkit-1/rules.d/ 2>/dev/null || true
    rm -f /tmp/99-yakeru-usb.rules
fi

# アプリケーションを起動
echo "バックエンドサーバーを起動しています..."
python app.py
