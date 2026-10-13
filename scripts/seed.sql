-- Development seed: a handful of providers so the endpoints return something.
--
-- This is NOT a migration. It is deliberately outside src/ProviderOptimizer.Infrastructure/Migrations
-- because seeded demo data must never be part of the schema history that production applies.
--
-- Run it after `docker compose up`:
--   docker compose exec -T postgres psql -U postgres -d provider_optimizer < scripts/seed.sql
--
-- `gen_random_uuid()` requires no extension on Postgres 13+ (it is built in).

INSERT INTO providers (id, name, service_type, latitude, longitude, eta_minutes, rating, is_available) VALUES
  (gen_random_uuid(), 'Gruas El Rapido',  'tow-truck', 4.7150, -74.0700, 12, 4.6, true),
  (gen_random_uuid(), 'Gruas Zona Norte', 'tow-truck', 4.7300, -74.0600,  8, 4.1, true),
  (gen_random_uuid(), 'Gruas Ocupadas',   'tow-truck', 4.7100, -74.0750,  5, 5.0, false),
  (gen_random_uuid(), 'Cerrajeria 24/7',  'locksmith', 4.7120, -74.0730, 15, 4.8, true),
  (gen_random_uuid(), 'Baterias Express', 'battery',   4.7250, -74.0650, 20, 4.3, true)
ON CONFLICT (id) DO NOTHING;
