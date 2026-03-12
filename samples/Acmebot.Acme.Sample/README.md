# Acmebot.Acme.Sample

`Acmebot.Acme` を使って ACME アカウント作成、オーダー作成、チャレンジ応答、証明書取得までを試せるコンソールアプリケーションです。

## 実行例

```powershell
dotnet run --project Acmebot.Acme.Sample -- \
  --email admin@example.com \
  --domain example.com \
  --domain www.example.com \
  --challenge dns-01 \
  --output artifacts
```

## オプション

- `--email`: ACME アカウントの連絡先メールアドレス
- `--domain`: 証明書を発行したいドメイン。複数指定可
- `--challenge`: `dns-01` または `http-01`。既定値は `dns-01`
- `--directory`: ACME ディレクトリ URL。既定値は Let's Encrypt Staging
- `--output`: 鍵と証明書の出力先ディレクトリ。既定値は `output`
- `--poll-interval`: ポーリング間隔(秒)。既定値は `5`

## 注意

- チャレンジの公開は自動化していません。表示された内容を DNS または HTTP に反映してから Enter を押してください。
- 既定値は Let's Encrypt Staging です。本番利用時は `--directory` を明示してください。
