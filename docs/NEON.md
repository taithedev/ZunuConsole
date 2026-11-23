# ZunuConsole + Neon setup (optional)

## Architecture

The mod does **not** connect to Postgres directly. The Unity DLL makes HTTPS requests
through `UnityWebRequest` to your serverless ZunuConsole API. That API stores a small
allowlisted set of appearance preferences and favorite command names in Neon.
No game logs, identities, room codes or player locations are uploaded.

The `zunuconsole.profiles` table has already been created on your existing Neon
**Zuzify App Data** project (`zuzify` database). SQL is in `cloud/schema.sql`.
This is a separate schema and does not modify Zuzify account tables.
For production, create a **dedicated least-privilege PostgreSQL role** that is granted
`USAGE` on `zunuconsole` and `SELECT`, `INSERT` and `UPDATE` on
`zunuconsole.profiles` only. Do not use a superuser or the Zuzify account-owner role.

## Deploy the server securely

1. Create a **Vercel** project pointing at this GitHub repo, with **Root Directory** `cloud`.
2. Configure private environment variable `DATABASE_URL` using the **Zuzify App Data**
   Neon project's **pooled** connection URL for its `zuzify` database. Do not put it
   in Git, Unity files, screenshots, or a downloadable EXE/DLL.
3. Create one secure random token per person using `node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"`.
4. Compute the SHA-256 hex digest of each token locally using
   `node -e "console.log(require('crypto').createHash('sha256').update(process.argv[1]).digest('hex'))" YOUR_TOKEN`.
5. In Vercel add `ZUNU_TOKEN_HASHES` as JSON mapping token hashes to opaque account IDs:
   `{ "<64-character SHA-256 digest>": "your-zuzify-account-id-or-a-random-uuid" }`.
   Do **not** use real account passwords or Discord IDs as access tokens. Token hash mappings
   are currently managed server-side; automatic login with Zuzify Identity is not implemented.
6. Deploy. `https://YOUR-DEPLOYMENT.vercel.app/api/state` will answer GET and PUT;
   unauthenticated requests return HTTP 401. Use the exact deployment URL, not a GitHub URL.
7. Start Gorilla Tag, open **Cloud** in ZunuConsole, enter the **HTTPS base URL**
   and your **private user token** (not the database connection string), then click
   **SAVE & CONNECT**. The token input is masked; it is saved only in your local
   `BepInEx/config/com.zuzify.zunuconsole.cfg` for subsequent sessions.
8. Use **PULL** / **PUSH** for manual sync. First sync automatically creates a
   profile if one does not already exist. **DISCONNECT** disables sync and clears
   the locally saved access token. You can also manage these values in that .cfg file.

Keep your `com.zuzify.zunuconsole.cfg` private: the access token is stored in that local file.
Do not post it in a GitHub issue. Revoke a token by removing its hash from the server.
The API has no public signup or password endpoints. Cloud is disabled by default.

## Conflict behavior

Initial sync pulls remote data if it exists. If no remote profile exists, the local
settings/favorites are uploaded. Subsequent automatic pushes occur on the configured
interval, minimum 60 seconds. Explicit PULL overwrites the synced appearance settings
and favorites on this device; explicit PUSH overwrites those fields in Neon. Window
position, dimensions, command history, local notes, logs and game data never sync.

## Limitations

A working Neon table **does not** mean the cloud API is deployed. You must host the
server and set its environment variables for real network sync to work. Direct Neon
credentials inside a distributed game mod would expose the entire database.
