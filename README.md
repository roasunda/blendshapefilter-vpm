# BlendShape Filter

Unity Editor拡張。`SkinnedMeshRenderer` のBlendShapeを検索・フィルタ・部位別グループ表示・Weight編集できるツールです。

主な用途はVRChatアバター改変時のシェイプキー探し。「Mouth_Aどこだっけ」を検索一発で解決します。

## インストール（VCC / ALCOM）

1. VCC または ALCOM を開く
2. `Settings > Packages > Add Repository` からこのリポジトリのVPMリポジトリURLを追加
3. 対象プロジェクトの `Manage Project` から `BlendShape Filter` を追加

## 使い方

Unityメニューの `Tools > BlendShape Filter` から起動します。

- **Target**: 対象の `SkinnedMeshRenderer` を指定、または `Use Selected` でHierarchyの選択から自動取得
- **Search**: BlendShape名を部分一致・大文字小文字区別なしで検索
- **Face Part**: 名前から推定した顔の部位（Eye / Brow / Mouth など）でワンクリック絞り込み。Eye・Mouthはさらに細かい小分類も選べる
- **Non-Zero / ★ Favorites**: Weightが0以外のものだけ、お気に入り登録したものだけに絞り込み
- **Weight編集**: Slider・数値入力ともUndo対応
- **Reset / Reset Visible**: 個別、または表示中のBlendShapeだけをまとめて0に戻す

## 安全性

このツールは **Mesh Assetを一切変更しません**。変更するのは対象 `SkinnedMeshRenderer` のBlendShape Weightのみです。BlendShapeのindex・名前・頂点データは常に読み取り専用です。

## 対応環境

- Unity 2019.4以降
- Editor拡張のみ。Runtimeコード・ビルド成果物には含まれません
- VRChat SDK等の外部Packageには依存しません

## ライセンス

MIT License
