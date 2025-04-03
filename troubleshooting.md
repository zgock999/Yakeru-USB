# Yakeru-USB トラブルシューティングガイド

## バックエンド関連の問題

### Werkzeug/Flaskのインポートエラー

**症状**: 
```
ImportError: cannot import name 'url_quote' from 'werkzeug.urls'
```

**解決方法**:

互換性のあるバージョンのFlaskとWerkzeugをインストールします：

```bash
pip uninstall flask werkzeug
pip install flask==2.0.1 werkzeug==2.0.3
```

このエラーは、FlaskがWerkzeugの古いバージョンに依存する機能を使用しているのに、システムに新しいバージョンのWerkzeugがインストールされている場合に発生します。特定のバージョンをインストールすることで、互換性が確保されます。

### Socket.IOの接続エラー

**症状**:
- WebSocketクライアントが接続できない
- 進捗更新が受信されない

**解決方法**:
1. バックエンドサーバーが実行中かを確認
2. ファイアウォールが5000番ポートを許可しているかを確認
3. クライアント側のSocket.IO URLが正しいかを確認（デフォルト: `ws://localhost:5000`）
4. 異なるSocket.IOバージョンを試す:
   ```bash
   pip uninstall python-socketio python-engineio flask-socketio
   pip install python-socketio==5.5.2 python-engineio==4.3.1 flask-socketio==5.1.1
   ```

### 管理者権限の問題

**症状**:
```
PermissionError: [Errno 13] Permission denied: '/dev/sdX'
```

**解決方法**:
- Windowsでは管理者としてコマンドプロンプトを開き、アプリケーションを実行
- Linux/macOSでは `sudo python app.py` を使用

## フロントエンド関連の問題

### TextMeshPro参照エラー

**症状**:
```
The type or namespace name 'TextMeshPro' could not be found
```

**解決方法**:
1. Unity EditorでWindow > Package Managerを開く
2. TextMeshProパッケージをインストール

### Newtonsoft.Jsonの参照エラー

**症状**:
```
The type or namespace name 'Newtonsoft' could not be found
```

**解決方法**:
1. Unity EditorでWindow > Package Managerを開く
2. Advanced > Show preview packagesを有効にする
3. Newtonsoft Json（com.unity.nuget.newtonsoft-json）パッケージをインストール

### HTTP通信エラー

**症状**:
- "Failed to get ISO files" または "Failed to get USB devices" エラー
- APIへの接続が失敗する

**解決方法**:
1. バックエンドサーバーが起動していることを確認
2. APIClient.csのapiBaseUrl設定が正しい値になっていることを確認
3. Unity設定でインターネットアクセスが許可されていることを確認

## OSごとの特有の問題

### Windows

- USBデバイスへの書き込み権限がない場合：アプリケーションを管理者権限で実行

### Windows: Invalid Argument エラー

**症状**:
```
error 22 invalid argument \\.\Physical Disk X
```

**解決方法**:

1. **管理者権限で実行**:
   - コマンドプロンプトまたはPowerShellを管理者権限で開く
   - そのコンソールからPythonアプリケーションを起動する
   ```
   cd D:\Python\Yakeru-USB\backend
   python app.py
   ```

2. **USBデバイスのパスを確認**:
   - USBデバイスがロックされていないことを確認
   - Windows Disk Managementで当該USBデバイスが表示されていることを確認

3. **ディスクの準備**:
   - デバイスマネージャーでUSBデバイスを一度取り外し、再接続
   - Disk Managementでボリュームがマウントされている場合は「オフライン」に設定

4. **ソースコードの修正**:
   - デバイスパス形式を修正（`\\.\PhysicalDriveX` → `\\\\?\\PhysicalDriveX`）
   - 管理者権限のチェック機能を追加

5. **ファイルシステムの問題**:
   - USBデバイスを再フォーマットしてみる
   - フォーマットされていないディスクの場合は、先にDisk Managementでフォーマット

6. **競合の解決**:
   - エクスプローラーやディスク管理ツールなど、USBデバイスにアクセスしている可能性のある他のプログラムを終了
   - システムの「デバイスとプリンター」からUSBデバイスを安全に取り外してから再接続

### Windows: Write Failed エラーコード5

**症状**:
```
Write failed. Error code: 5
```

**原因**:
このエラーは `ERROR_ACCESS_DENIED` (アクセス拒否) を示します。USBデバイスが他のプロセスでロックされているか、Windows Disk Managementがデバイスを使用中である可能性があります。

**解決方法**:

1. **提供のユーティリティスクリプトを使用**:
   - `start_backend.bat` を使って管理者権限でアプリケーションを起動する
   - 問題が解決しない場合は `force_clean_disk.bat` を使ってUSBディスクを強制的にクリーンアップする

2. **手動でディスクをクリーンアップ**:
   - 管理者権限でコマンドプロンプトを開く
   - 次のコマンドを実行:
   ```
   diskpart
   list disk                     # 利用可能なディスク一覧を表示
   select disk N                 # Nは対象USBディスクの番号に置き換え
   clean                         # ディスク全体をクリーンアップ
   exit
   ```

3. **USBデバイスを完全に初期化**:
   - ディスク管理ツールですべてのパーティションを削除
   - USBデバイスを一度物理的に取り外し、10秒以上待ってから再接続
   - アンチウイルスソフトが一時的にUSBアクセスをブロックしている可能性もあるため、必要に応じて一時的に無効化

4. **システム診断**:
   - 管理者権限のコマンドプロンプトで次のコマンドを実行し、進行中のディスク操作を確認:
   ```
   tasklist /fi "imagename eq diskpart.exe"
   tasklist /fi "imagename eq wmic.exe"
   ```
   - 上記プロセスが実行中の場合、終了してから再試行

5. **システム再起動**:
   - すべての対策が失敗する場合、コンピュータを再起動し、再起動後に最初に行う操作としてYakeru-USBを実行

### Linux

- デバイスが検出されない場合：`lsblk`コマンドで利用可能なブロックデバイスを一覧表示
- アクセス権限の問題：`sudo` を使用してアプリケーションを実行

### Linux環境での特有の問題

#### USBデバイスが検出されない

**症状**:
- Linux環境でUSBデバイスを接続しても認識されない
- デバイス一覧が空のまま

**原因**:
- 権限が不足している
- udev規則が適切に設定されていない
- デバイス検出ロジックに互換性がない

**解決方法**:

1. **管理者権限で実行**:
   ```bash
   sudo ./start_backend.sh
   ```
   このスクリプトは必要な権限設定を自動的に行います。

2. **デバイス権限の手動設定**:
   ```bash
   sudo chmod -R a+rw /dev/sd*
   ```

3. **udevルールの設定**:
   ```bash
   sudo nano /etc/udev/rules.d/99-usb-permissions.rules
   ```
   
   以下の内容を追加:
   ```
   KERNEL=="sd[a-z]*", SUBSYSTEMS=="usb", MODE="0666"
   ```
   
   そして、udevルールを再読み込み:
   ```bash
   sudo udevadm control --reload-rules
   sudo udevadm trigger
   ```

4. **アプリケーション内の「更新」ボタンを複数回クリック**:
   - 「Refresh」ボタンを何度かクリックして強制的に再スキャンする

5. **デバイスが適切にマウントされていることを確認**:
   ```bash
   lsblk
   ```
   
   デバイスが表示されるが、アプリで検出されない場合:
   ```bash
   sudo umount /dev/sdX  # デバイスをアンマウント
   ```

6. **他のUSBポートを試す**:
   - デバイスを別のUSBポートに接続
   - USB 2.0ポートを試す（USB 3.0ポートで問題が発生することがある）

7. **再起動**:
   - バックエンドサービスを再起動
   - 問題が解決しない場合はシステムを再起動

### macOS

- ディスクユーティリティでデバイスがロックされている場合：まずディスクユーティリティでマウント解除
- システム保護によりブロックされる：セキュリティ設定でフルディスクアクセスを許可
