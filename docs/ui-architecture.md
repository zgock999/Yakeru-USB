# Yakeru-USB UI アーキテクチャ

## 分離型 UI アーキテクチャについて

Yakeru-USB では、UIWizard（メイン UI コントローラ）と UIWizardScreenController（画面表示管理）を分離したアーキテクチャを採用しています。この構造により、ロジックと表示の責務を明確に分け、コードの保守性を向上させています。

- **UIWizard**: アプリケーション全体のフローを制御し、イベントハンドリングとデータ管理を担当
- **UIWizardScreenController**: 画面遷移とUI要素の表示・更新を担当

## コンポーネント構成

```
UIRoot/
  ├── UIWizard (UIWizard.cs)
  └── ScreenController (UIWizardScreenController.cs)
      ├── ISOSelectionScreen
      ├── DeviceSelectionScreen
      ├── WritingScreen
      └── ResultScreen
```

## 責務の分離

### UIWizard の役割

- ウィザードの全体的なフロー制御
- ISOManager とのデータ連携
- ユーザー操作とイベント処理
- 選択データの管理

### UIWizardScreenController の役割

- 各画面の表示管理
- UI要素の更新と反映
- 進捗表示やステータスメッセージの表示
- エラー表示と結果表示

## 相互連携

UIWizard と UIWizardScreenController は相互に参照しながら、以下のように連携します：

1. UIWizard は画面遷移を決定し、ScreenController に通知
2. ScreenController は適切な画面を表示
3. ISOManager からのイベント（進捗など）は UIWizard が受け取り、必要に応じて ScreenController に転送
4. ユーザー入力は主に UIWizard が処理し、UI表示の更新は ScreenController が担当

この分離型アーキテクチャにより、将来的な機能拡張や UI の変更が容易になります。

## 安全性に関する設計上の決定

Yakeru-USB では、USBデバイスの安全性を確保するため、以下の設計上の決定を行いました：

- 書き込みプロセスが開始されたら、完了するまでキャンセルできない設計
- 書き込み中に重要なメッセージを表示し、ユーザーに中断しないよう促す
- 書き込みプロセスの各段階で適切なフィードバックを提供

これらの措置により、書き込みプロセスの信頼性を確保し、デバイスの破損リスクを最小化しています。
