-- Namespaced tables, safe to apply to the existing Zuzify App Data Neon project.
-- Already applied on the `zuzify` database. No other tables are changed.
CREATE SCHEMA IF NOT EXISTS zunuconsole;
CREATE TABLE IF NOT EXISTS zunuconsole.profiles (
  account_id text PRIMARY KEY,
  settings jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(settings) = 'array'),
  favorites jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(favorites) = 'array'),
  updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS zunuconsole_profiles_updated_at_idx ON zunuconsole.profiles (updated_at DESC);
