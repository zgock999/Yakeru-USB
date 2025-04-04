# Yakeru-USB セットアップガイド

## 基本プロジェクト構造

```
frontend/
  └── Assets/
      ├── Scenes/          - シーンファイル
      ├── Scripts/         - C#スクリプト
      ├── Prefabs/         - プレハブ
      ├── UI/              - UI関連のアセット
      └── Resources/       - その他のリソース
```

## セットアップ手順

### 1. UIキャンバスの作成

1. Hierarchy ウィンドウで右クリック → UI → Canvas を選択し、メインとなる UI キャンバスを作成します
   - Canvas の Render Mode は「Screen Space - Overlay」を推奨します
   - Canvas Scaler の UI Scale Mode は「Scale With Screen Size」に設定し、Reference Resolution を 1920×1080 に設定します

2. Canvas の子として「EventSystem」が自動的に作成されていることを確認します（キーボードやマウスの入力処理に必要）

### 2. 必要なコンポーネントの作成

1. 作成したキャンバスの下に、右クリック → Create Empty を選択し、「UIRoot」という名前の空の GameObject を作成します

2. UIRoot オブジェクトを選択した状態で、Hierarchy ウィンドウで右クリック → Create Empty を選択し、「UIWizard」という名前の子オブジェクトを作成します

3. UIWizard オブジェクトを選択した状態で、Hierarchy ウィンドウで右クリック → Create Empty を選択し、「ScreenController」という名前の子オブジェクトを作成します

4. 必要な画面オブジェクトを ScreenController の子として作成します:
   - TitleScreen (タイトル/スタート画面)
   - ISOSelectionScreen
   - DeviceSelectionScreen
   - WritingScreen
   - ResultScreen

### 3. スクリプトのアタッチ

1. UIWizard オブジェクトに `UIWizard.cs` スクリプトをアタッチします
2. ScreenController オブジェクトに `UIWizardScreenController.cs` スクリプトをアタッチします

### 4. 基本的なUI要素の作成

各画面には以下のような基本的なUI要素が必要です:

#### TitleScreen
- Panel (背景)
- Text/TextMeshPro (アプリケーションタイトル)
- Image (ロゴ画像)
- Button (スタートボタン)
- Button (終了ボタン)

#### ISOSelectionScreen
- Panel (背景)
- Text (タイトル)
- ScrollRect (ISOファイルのリスト用)
- Button (更新ボタン)
- Button (次へボタン)

#### DeviceSelectionScreen
- Panel (背景)
- Text (タイトル)
- ScrollRect (USBデバイスのリスト用)
- Button (更新ボタン)
- Button (次へボタン)
- Button (戻るボタン)

#### WritingScreen
- Panel (背景)
- Text (タイトル)
- Slider (進捗バー)
- Text (進捗パーセンテージ)
- Text (ステータスメッセージ)

#### ResultScreen
- Panel (背景)
- Text (タイトル)
- Image (成功/失敗アイコン)
- Text (結果メッセージ)
- Button (完了ボタン)

### 5. インスペクタでの設定

#### UIWizardScreenController の設定:

1. UIWizardScreenController コンポーネントのインスペクタを開きます
2. 以下のフィールドを設定します:
   - **UI Wizard**: 親オブジェクトの UIWizard をドラッグ＆ドロップ
   - **ISO Selection Screen**: ISOSelectionScreen オブジェクトをドラッグ＆ドロップ
   - **Device Selection Screen**: DeviceSelectionScreen オブジェクトをドラッグ＆ドロップ
   - **Writing Screen**: WritingScreen オブジェクトをドラッグ＆ドロップ
   - **Result Screen**: ResultScreen オブジェクトをドラッグ＆ドロップ
   - 各画面内の必要な UI 要素（進捗バーなど）も設定します

#### UIWizard の設定:

1. UIWizard コンポーネントのインスペクタを開きます
2. 以下のフィールドを設定します:
   - **Screen Controller**: 子オブジェクトの ScreenController をドラッグ＆ドロップ
   - **Title Panel**: タイトル画面パネルをドラッグ＆ドロップ
   - その他各パネル（isoSelectionPanel, usbSelectionPanel, confirmationPanel, writingPanel, completionPanel など）に対応するオブジェクトを設定
   - **Start Button**: タイトル画面のスタートボタンを設定
   - リストコンテンツ（isoListContent, usbListContent）の Transform を設定
   - 各種テキスト要素や進捗バーなど、必要なすべての参照を設定

### 6. テスト実行

1. Unity エディタで再生ボタンを押して動作確認をします
2. 各画面が正しく遷移するか、進捗表示やエラーハンドリングが適切に機能するかを確認します
3. WebSocketClient や ISOManager などのシングルトンが正しく初期化されていることを確認します

### 7. ビルド設定

1. File → Build Settings でプラットフォームを設定 (Windows/Mac/Linux)
2. Player Settings でアプリケーション名やアイコンを設定
3. Resolution and Presentation で推奨解像度（1280×720 など）を設定
