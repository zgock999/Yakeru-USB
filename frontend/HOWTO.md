# Yakeru-USB 開発ガイド

このガイドでは、Yakeru-USBアプリケーションの開発方法について説明します。

## WriterSampleシーンの作成方法

### 1. 基本的なシーンのセットアップ

1. Unityエディタを開き、プロジェクトブラウザで `Assets` フォルダを右クリックします
2. `Create` > `Folder` を選択し、`Scenes` というフォルダを作成します
3. 新しく作成した `Scenes` フォルダを右クリックし、`Create` > `Scene` を選択します
4. 新しいシーンに `WriterSample` という名前を付けます
5. 作成したシーンをダブルクリックして開きます

### 2. UIキャンバスの設定

1. シーンに階層ビューで右クリックし、`UI` > `Canvas` を選択します
2. キャンバスを選択した状態で、インスペクタパネルで以下のように設定します：
   - Canvas Scaler (Script)コンポーネントの `UI Scale Mode` を `Scale With Screen Size` に設定
   - `Reference Resolution` を `1920 x 1080` に設定

3. 階層ビューでCanvasを右クリックし、`UI` > `Event System` を追加します

### 3. 基本的なUI要素の追加

#### 3.1 パネルレイアウトの作成

1. Canvasを右クリックし、`UI` > `Panel` を選択し、メインパネルを作成します
2. パネルを選択し、Anchor Presets（インスペクタの左上にある四角いボタン）を押しながらAltキーを押し、「Stretch-Stretch」を選択して画面全体に広がるようにします
3. パネルの色を変更するには、Imageコンポーネントの `Color` プロパティを調整します（例：#1E1E1E）

4. このメインパネルを右クリックし、以下のパネルを作成します：
   - `LeftPanel` (ISOファイルリスト用)
   - `RightPanel` (USBデバイスリスト用)
   - `BottomPanel` (操作ボタンと進捗表示用)

5. 各パネルのRect Transformを適切に設定し、画面レイアウトを構成します

#### 3.2 リストビューの作成

1. `LeftPanel` を右クリックし、`UI` > `Scroll View` を選択します
2. スクロールビューの名前を `ISOListScrollView` に変更します
3. 同様に、`RightPanel` にも `USBListScrollView` というスクロールビューを作成します

4. 各スクロールビューの Content オブジェクトを選択し、以下の設定を行います：
   - Inspector パネルで「Add Component」をクリックし、「Vertical Layout Group」を追加
   - Vertical Layout Group の設定：
     - Child Alignment: Upper Center
     - Spacing: 5 (アイテム間の間隔)
     - Child Controls Size にチェック：Width と Height
     - Child Force Expand にチェック：Width のみ
   - Inspector パネルで「Add Component」をクリックし、「Content Size Fitter」も追加
     - Vertical Fit: Preferred Size

5. スクロールビューの設定：
   - ISOListScrollView の Viewport > Scrollbar Vertical を使ってスクロール動作を確認
   - USBListScrollView も同様に設定

#### 3.3 リストアイテムのプレハブ作成

1. プロジェクトビューで `Assets` フォルダを右クリックし、`Create` > `Folder` で `Prefabs` フォルダを作成します
2. 階層ビューで一時的に `UI` > `Button` を作成し、名前を `ISOListItem` に変更します
3. このボタンをカスタマイズします：
   - サイズを調整（例：幅 400, 高さ 60）
   - ボタンにテキスト要素を2つ持たせるため、子オブジェクトとして新しいTextオブジェクトを追加します
   - 元のTextを `NameText` に、新しいTextを `SizeText` に変更します
   - テキストの配置やフォントサイズを調整します

4. 完成したボタンをプロジェクトビューの `Prefabs` フォルダにドラッグして、プレハブとして保存します
5. 同様の手順で `USBListItem` プレハブも作成します

### 4. スクリプトとコンポーネントの接続

1. 空のゲームオブジェクトを3つ作成し、以下の名前を付けます：
   - `APIClient`
   - `WebSocketClient`
   - `ISOManager`

2. それぞれのゲームオブジェクトに、対応する名前のスクリプトをアタッチします

3. キャンバス下に空のゲームオブジェクトを作成し、`UIController` という名前を付け、同名のスクリプトをアタッチします

4. `UIController` コンポーネントのインスペクタで、以下のプロパティを設定します：
   - `ISO List Content`: ISOListScrollView の Content オブジェクトをドラッグ
   - `ISO List Item Prefab`: 作成した ISOListItem プレハブをドラッグ
   - `USB List Content`: USBListScrollView の Content オブジェクトをドラッグ
   - `USB List Item Prefab`: 作成した USBListItem プレハブをドラッグ
   - その他の参照（ボタン、パネル、テキスト要素など）も同様に設定

### 5. 進捗表示UIの作成

1. `BottomPanel` に子オブジェクトとして新しい `Panel` を作成し、`ProgressPanel` という名前を付けます
2. この中に `UI` > `Slider` を追加し、進捗バーとして使用します
3. スライダーの見た目をカスタマイズし、シーン上で目立つようにします
4. `UI` > `Text - TextMeshPro` を2つ追加し、それぞれ `ProgressText` と `StatusText` という名前を付けます
5. `UIController` スクリプトの対応するプロパティにこれらの要素を接続します

### 6. メッセージパネルの追加

1. キャンバスに子オブジェクトとして新しい `Panel` を作成し、`MessagePanel` という名前を付けます
2. このパネルには以下の要素を追加します：
   - 背景パネル（半透明の黒など）
   - メッセージ用テキスト（TextMeshPro）
   - 「閉じる」ボタン
3. `UIController` スクリプトの対応するプロパティにこれらの要素も接続します

### 7. シーンの保存と実行

1. すべての設定が完了したら、シーンを保存します
2. シーンをビルド設定に追加するには、`File` > `Build Settings` を開き、`Add Open Scenes` をクリックします
3. プレイモードで動作確認を行います

### レイアウトコンポーネントの設定

#### Vertical Layout Group の詳細設定

UIでリスト要素を縦に並べるVertical Layout Groupの設定について詳しく説明します：

1. **リストのContent要素に追加**:
   - ISOListScrollView > Viewport > Content を選択
   - Inspector で「Add Component」> 「Layout」> 「Vertical Layout Group」

2. **設定パラメータ**:
   - Padding: リスト全体の余白（Top:5, Bottom:5, Left:5, Right:5 など）
   - Spacing: リストアイテム間の間隔（10など）
   - Child Alignment: 子要素の配置（Upper Center が推奨）
   - Control Child Size: 子要素のサイズ制御
     - Width: オン（横幅をコントロール）
     - Height: オン（高さをコントロール）
   - Child Force Expand: 子要素の拡張
     - Width: オン（幅を親に合わせる）
     - Height: オフ（各アイテムの高さは固定）

3. **Content Size Fitter の追加**:
   - Content 要素に「Content Size Fitter」も追加すると、内容に応じて縦サイズが自動調整されます
   - Vertical Fit: Preferred Size

4. **リストアイテムの設定**:
   - Layout Element コンポーネントを各アイテムに追加して、最小/優先サイズを指定できます
   - Preferred Height: 60 などに設定

これにより、リストアイテムの数が増減しても自動的にレイアウトが調整されるようになります。

## UIデザインのヒント

### 全体的なレイアウト

```
+---------------------------------------+
|              Header                   |
+---------------+---------------------+ |
| ISO Files     | USB Devices         | |
| +-----------+ | +-----------------+ | |
| | ubuntu.iso| | | Kingston USB    | | |
| +-----------+ | +-----------------+ | |
| | debian.iso| | | SanDisk Ultra   | | |
| +-----------+ | +-----------------+ | |
|               |                     | |
+---------------+---------------------+ |
| Selected:     | Selected:           | |
| ubuntu.iso    | Kingston USB (32GB) | |
+---------------+---------------------+ |
|  [Refresh]    [Write to USB]  [Quit] |
|                                      |
|  [========Progress Bar========] 45%  |
|  Writing data...                     |
+--------------------------------------+
```

### 配色の提案

- 背景色: #1E1E1E (濃いグレー)
- パネル背景: #2D2D30 (少し明るいグレー)
- ボタン: #007ACC (青)
- テキスト: #FFFFFF (白)
- 進捗バー: #388E3C (緑)
- エラーテキスト: #E57373 (赤)

## エラー処理とユーザーフィードバック

- アクションが失敗した場合は、明確なエラーメッセージでユーザーに通知
- 書き込み中は進捗バーとステータステキストで現在の状態を示す
- 書き込み完了時は、成功メッセージと共に結果を表示

## ビルドとデプロイ

1. `File` > `Build Settings` を開く
2. 対象プラットフォームを選択（Windows/macOS/Linux）
3. `Build` または `Build And Run` をクリック
4. 出力先フォルダを選択して、アプリケーションをビルド
5. ビルドしたアプリケーションはバックエンドAPIサーバーと同じマシン上で実行するのがベスト

## トラブルシューティング

### シーン切り替え時の「Objects were not cleaned up」警告

シーン切り替え時に「Some objects were not cleaned up when closing the scene」といった警告が表示される場合は、シングルトンオブジェクトがうまく破棄されていない可能性があります。

**解決方法**:

1. シングルトンクラスに以下のメソッドが実装されていることを確認:
   - `OnApplicationQuit()`
   - `OnDestroy()`

2. より安全なシングルトン設計として `SingletonBase<T>` クラスを継承することを検討:
   ```csharp
   public class MyManager : SingletonBase<MyManager>
   {
       // シングルトンを継承したクラス固有の処理
   }
   ```

### DontDestroyOnLoad 関連の問題

`DontDestroyOnLoad` を使用したオブジェクトは、通常のシーン破棄プロセスの外で存続します。これにより以下の問題が起こることがあります：

1. シーンを再ロード/切り替えた際に重複インスタンスが作成される
2. イベント購読が適切に解除されない
3. シーン終了時にリソースリークが発生する

**解決方法**:

- シングルトンの初期化時に既存のインスタンスを確認
- OnDestroy でイベント購読を明示的に解除
- OnApplicationQuit で静的参照をクリア
