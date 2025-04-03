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

### 進捗表示の問題

**症状**:
- 進捗バーが最初から高い値（例：90%）から始まる
- 進捗が不正確または進まない
- 99%で長時間停滞する

**原因**:
- オペレーティングシステムのディスクキャッシュにより、最初に高速に書き込まれたように見える
- 複数パーティションを持つUSBデバイスの場合、書き込みサイズの計算が不正確になる
- フラッシング（ディスクへの同期）フェーズは実際に時間がかかる

**仕組みの説明**:
最新版のYakeru-USBでは、より現実的な進捗表示を実現するため、書き込み処理を複数のフェーズに分割しています：

1. **準備フェーズ (0-5%)**: ディスクの初期化と準備
2. **初期書き込みフェーズ (5-30%)**: データの初期部分の書き込み
3. **メイン書き込みフェーズ (30-85%)**: 実際のデータ量に基づく書き込み
4. **フラッシュフェーズ (85-95%)**: キャッシュからディスクへの同期（時間がかかる場合あり）
5. **最終化フェーズ (95-99%)**: 書き込み完了処理
6. **完了 (100%)**: 処理完了

**解決方法**:

1. **最新バージョンを使用する**:
   - 最新版では進捗表示のアルゴリズムが改善されています

2. **操作の仕組みを理解する**:
   - 99%付近での停滞は正常な動作です（フラッシュフェーズ）
   - 特に大きなISOファイルの場合、このフェーズで数分かかることがあります

3. **ディスク性能の理解**:
   - 低速なUSBデバイスでは書き込みに時間がかかります
   - USB 3.0デバイスはUSB 2.0ポートに接続すると遅くなります

4. **忍耐を持って待つ**:
   - 進捗が99%で停滞している場合でも、処理は進行中です
   - 「データをディスクに同期中...」というメッセージが表示されている間は処理を中断しないでください

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
- "`chmod: '/dev/sd*' にアクセスできません: そのようなファイルやディレクトリはありません`"というエラー

**原因**:
- 権限が不足している
- udev規則が適切に設定されていない
- デバイス検出ロジックに互換性がない
- システムがSD形式以外のデバイスパス（NVMeやVirtual Disk）を使用している

**解決方法**:

1. **管理者権限で実行**:
   ```bash
   sudo ./start_backend.sh
   ```
   このスクリプトは必要な権限設定を自動的に行います。

2. **デバイスタイプを確認**:
   ```bash
   ls -l /dev/sd* /dev/nvme* /dev/vd* 2>/dev/null
   ```
   
   このコマンドを実行して、どのタイプのデバイスパスが存在するか確認してください。
   sd* はSATA/USB接続のディスク
   nvme* はNVMe SSDディスク
   vd* は仮想マシンのディスク

3. **デバイスの権限を手動で設定**:
   ```bash
   # システムに存在するデバイスタイプに応じて実行
   sudo chmod -R a+rw /dev/sd* 2>/dev/null
   sudo chmod -R a+rw /dev/nvme* 2>/dev/null
   sudo chmod -R a+rw /dev/vd* 2>/dev/null
   ```

4. **デバイスの種類に応じた設定を確認**:
   ```bash
   # NVMeデバイスの場合
   lsblk -d -o NAME,SIZE,MODEL,REMOVABLE | grep nvme
   # 仮想ディスクの場合
   lsblk -d -o NAME,SIZE,MODEL,REMOVABLE | grep vd
   ```

### macOS

- ディスクユーティリティでデバイスがロックされている場合：まずディスクユーティリティでマウント解除
- システム保護によりブロックされる：セキュリティ設定でフルディスクアクセスを許可
