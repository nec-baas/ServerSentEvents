ビルド手順
==========

必要環境
--------

* Windows 7 以上
* Visual Studio 2013 Professional

ビルド手順
----------

コマンドプロンプトで、"build.cmd Build" を実行する。

備考
----
上記ビルドを実施後は、下記手順でVisual Studioにてビルド可能。

ServerSentEvents.sln ファイルを Visual Studio で開く。

ビルド ⇒ ソリューションのビルド でビルドを実施する。

ビルド中に、自動的に依存するパッケージが NuGet でダウンロードされる。

正しくダウンロードされない場合は、ツール ⇒ NuGetパッケージマネージャ
⇒ ソリューションのNuGetパッケージの管理 を開き、パッケージのインストール
状況を確認すること。

テスト手順については Testing.md を参照のこと。
