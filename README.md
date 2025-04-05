# Yakeru-USB

注：フロントエンドのAssets込みのリポジトリはlfsの制限が緩い[Azure Devops](https://dev.azure.com/gaplant555/Yakeru-USB/_git/Yakeru-USB)上にあります

Yakeru-USBは、ISOイメージファイルをUSBメモリに簡単に書き込むためのアプリケーションです。Pythonバックエンド（REST API）とUnityフロントエンドで構成されており、直感的なUIで簡単にUSBメモリへの書き込みが行えます。

![アプリケーションイメージ](.github/app_screenshot.png)

## 機能

- ISOファイルの一覧表示と選択
- 接続されたUSBデバイスの自動検出
- リアルタイムの書き込み進捗表示
- クロスプラットフォーム対応（Windows/Linux/macOS）

## システム要件

### バックエンド
- Python 3.8以上
- Flask、Flask-SocketIO
- Linux/macOSでの実行時は管理者権限（sudo）が必要

### フロントエンド
- Unity 2022.3以上
- .NET Framework 4.6以上

## セットアップ

### 1. リポジトリのクローン

```bash
git clone https://github.com/yourusername/Yakeru-USB.git
cd Yakeru-USB
```

### 2. バックエンドのセットアップ

```bash
cd backend
# 仮想環境の作成（任意）
python -m venv venv
# Windowsの場合
venv\Scripts\activate
# Linux/macOSの場合
source venv/bin/activate

# 依存パッケージのインストール
pip install -r requirements.txt

# ISOファイルを保存するディレクトリの作成
mkdir -p isos
```

### 3. フロントエンドのセットアップ

1. Unityでフロントエンドプロジェクトを開きます
   ```
   Unity Hub > プロジェクトを開く > Yakeru-USB/frontend
   ```

2. 必要なパッケージが自動的にインストールされます
   - TextMeshPro
   - Newtonsoft.Json

3. プロジェクトを初めて開く場合は、シーンをセットアップする必要があります
   - 詳細な手順は `frontend/HOWTO.md` を参照してください
   - または、`File > New Scene` からシーンを新規作成し、必要なコンポーネントを配置してください

## 使用方法

### 1. バックエンドサーバーの起動

**推奨方法**: 提供の起動スクリプトを使用する（管理者権限で自動的に実行されます）
```
start_backend.bat
```

または手動で起動:
```bash
# backendディレクトリで
python app.py
```

**重要**: Windowsでは管理者権限でコマンドプロンプトを開き、そこからアプリケーションを実行してください。
1. スタートメニューでコマンドプロンプトまたはPowerShellを右クリック
2. 「管理者として実行」を選択
3. 開いたコンソールで以下を実行:
   ```
   cd パス\to\Yakeru-USB\backend
   python app.py
   ```

Linux/macOSでは `sudo python app.py` として実行します。

サーバーはデフォルトで `http://localhost:5000` で起動します。

### 2. ISOファイルの配置

書き込みたいISOファイルを `backend/isos` ディレクトリに配置します。

### 3. フロントエンドの起動

1. Unityエディタでプロジェクトを開き、再生ボタンをクリックします
2. または、ビルド済みアプリケーションを起動します

### 4. ISOファイルの書き込み手順

1. アプリケーション画面で利用可能なISOファイル一覧から書き込みたいファイルを選択します
2. 接続されたUSBデバイス一覧から書き込み先のデバイスを選択します
3. 「書き込み開始」ボタンをクリックします
4. 進捗バーで書き込みの状況を確認します
5. 書き込みが完了するとメッセージが表示されます

## プロジェクト構造

```
Yakeru-USB/
├── backend/             # Python REST APIサーバー
│   ├── app.py           # メインアプリケーションサーバー
│   ├── iso_writer.py    # ISO書き込みロジック
│   ├── usb_detector.py  # USBデバイス検出ロジック
│   ├── requirements.txt # 依存パッケージリスト
│   ├── isos/            # ISOファイル格納ディレクトリ
│   └── README.md        # バックエンド固有の説明
├── frontend/            # Unity フロントエンド
│   ├── Assets/
│   │   ├── Scripts/     # C#スクリプト
│   │   ├── Scenes/      # Unityシーン
│   │   └── Prefabs/     # UIコンポーネントのプレハブ
│   ├── HOWTO.md         # シーン作成などの開発ガイド
│   └── README.md        # フロントエンド固有の説明
└── README.md            # このファイル
```

## 注意事項

- **管理者権限が必須**: USBデバイスへの書き込みには管理者権限が**必ず**必要です。管理者権限がないと「Invalid Argument」などのエラーが発生します。
- **データ損失の注意**: USBデバイスへの書き込みによりデバイス上の既存データはすべて削除されます。重要なデータは事前にバックアップしてください。
- **サポートされるデバイス**: 一般的なUSBメモリ/フラッシュドライブに対応しています。特殊なデバイスでは動作しない場合があります。
- **書き込み中の取り外し**: 書き込み処理中はUSBデバイスを取り外さないでください。データ破損の原因となります。

## トラブルシューティング

### バックエンドサーバーに接続できない
- サーバーが起動しているか確認してください
- ファイアウォール設定でポート5000が許可されているか確認してください
- APIのベースURL設定が正しいか確認してください

### USBデバイスが検出されない
- デバイスが正しく接続されているか確認してください
- 管理者権限でアプリケーションを実行してください
- デバイスドライバが正しくインストールされているか確認してください

### 書き込み中にエラーが発生する
- 十分なディスク容量があるか確認してください
- 書き込み先デバイスが書き込み保護されていないか確認してください
- バックエンドのログでエラーメッセージを確認してください
- **「Error code: 5」が発生する場合**: `force_clean_disk.bat` ユーティリティを実行し、USBディスクを強制的にクリーンアップしてください

詳細なトラブルシューティング情報は [トラブルシューティングガイド](troubleshooting.md) を参照してください。

## ライセンス

[MIT License](LICENSE)

## 貢献

バグ報告や機能リクエストは、GitHubのIssueを通じてお願いします。プルリクエストも歓迎します。