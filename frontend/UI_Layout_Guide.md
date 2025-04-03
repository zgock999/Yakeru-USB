# UIレイアウトガイド

## リストのVertical Layout Group設定

### 1. リストのContentオブジェクトを選択

ISOListScrollViewまたはUSBListScrollViewの階層:
```
- ISOListScrollView (Scroll View)
  - Viewport
    - Content  <- このオブジェクトを選択
```

### 2. コンポーネントの追加と設定

Inspectorパネルで以下のコンポーネントを追加し、設定します:

#### Vertical Layout Group

![Vertical Layout Group設定例](images/vertical_layout_group_settings.png)

主な設定値:
- Padding: Top: 5, Bottom: 5, Left: 5, Right: 5
- Spacing: 10
- Child Alignment: Upper Center
- Child Controls Size: Width ✓, Height ✓
- Child Force Expand: Width ✓, Height ☐

#### Content Size Fitter

![Content Size Fitter設定例](images/content_size_fitter_settings.png)

設定値:
- Horizontal Fit: Unconstrained
- Vertical Fit: Preferred Size

### 3. リスト項目のプレハブ設定

ISOListItemやUSBListItemプレハブに、LayoutElement追加が推奨:

![Layout Element設定例](images/layout_element_settings.png)

設定値:
- Flexible Width: 1
- Preferred Height: 60

## レイアウトのテスト方法

1. プレイモードでアプリケーションを実行
2. 複数のアイテムが表示されることを確認
3. スクロール操作でリスト内をスムーズに移動できることを確認
4. ウィンドウサイズを変更してもレイアウトが適切に対応することを確認
