# Native WebSocket for Unity

このライブラリはUnityでWebSocketを使用するために必要です。

## インストール方法

1. UnityプロジェクトでPackage Managerを開きます
2. 「+」メニューから「Add package from git URL...」を選択します
3. 以下のURLを入力します：
   ```
   https://github.com/endel/NativeWebSocket.git
   ```
4. インストールボタンをクリックします

## 代替インストール方法

OpenUPMを使用してインストールすることもできます。

```bash
openupm add com.endel.nativewebsocket
```

あるいは、`Packages/manifest.json`に直接次の行を追加します：

```json
{
  "dependencies": {
    "com.endel.nativewebsocket": "1.1.4"
  },
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.endel.nativewebsocket"
      ]
    }
  ]
}
```

## Yakeru-USBでの利用

このプロジェクトではバックエンドサーバーとリアルタイム通信するためにこのライブラリを使用しています。
バックエンドから書き込み進捗を受信するためのWebSocketクライアントが実装されています。
