# Yakeru-USB Backend API

ISOファイルをUSBメモリに書き込むためのREST APIバックエンド。

## セットアップ

```bash
# 依存関係のインストール
pip install -r requirements.txt

# サーバーの起動
python app.py
```

サーバーはデフォルトで `http://localhost:5000` で起動します。

### 注意: 依存関係の互換性

このプロジェクトでは、特定のバージョンのパッケージが必要です：
- Flask 2.0.1
- Werkzeug 2.0.3

これらのバージョン指定は、互換性の問題を回避するために重要です。エラー「ImportError: cannot import name 'url_quote'」が発生した場合、以下のコマンドで解決できます：

```bash
pip uninstall flask werkzeug
pip install flask==2.0.1 werkzeug==2.0.3
```

## APIエンドポイント

### ISOファイル一覧の取得

```
GET /api/isos
```

レスポンス例:
```json
{
  "isos": [
    {
      "name": "ubuntu-22.04-desktop-amd64.iso",
      "path": "D:\\Python\\Yakeru-USB\\backend\\isos\\ubuntu-22.04-desktop-amd64.iso",
      "size": 4348968960,
      "size_formatted": "4.05 GB"
    }
  ]
}
```

### USBデバイス一覧の取得

```
GET /api/usb-devices
```

レスポンス例:
```json
{
  "devices": [
    {
      "id": "/dev/sdc",
      "name": "SanDisk Ultra",
      "size": "32 GB",
      "vendor": "SanDisk"
    }
  ]
}
```

### ISOファイルの書き込み開始

```
POST /api/write
Content-Type: application/json

{
  "iso_file": "ubuntu-22.04-desktop-amd64.iso",
  "device": "/dev/sdc"
}
```

レスポンス例:
```json
{
  "status": "Writing started"
}
```

## WebSocketによる進捗通知

WebSocketに接続して `write_progress` イベントをリッスンすることで、書き込みの進捗状況をリアルタイムで取得できます。

```javascript
const socket = io('http://localhost:5000');
socket.on('write_progress', (data) => {
  console.log(`Progress: ${data.progress}%, Status: ${data.status}`);
});
```

## 注意事項

- USBデバイスへの書き込みには適切な権限が必要です
- Linuxでは `sudo` が必要な場合があります
- バックアップを取ってから使用してください
