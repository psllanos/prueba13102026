-- Datos de ejemplo para desarrollo: unos cuantos proveedores para que los endpoints devuelvan algo.
--
-- Esto NO es una migración. Está deliberadamente fuera de
-- src/ProviderOptimizer.Infrastructure/Migrations porque los datos de demo nunca deben formar parte
-- del historial de esquema que aplica producción.
--
-- Ejecutar después de `docker compose up`:
--   docker compose cp scripts/seed.sql postgres:/tmp/seed.sql
--   docker compose exec -T postgres psql -U postgres -d provider_optimizer -f /tmp/seed.sql
--
-- Los ids son FIJOS y legibles a propósito, no `gen_random_uuid()`. Con un uuid aleatorio, correr
-- este archivo dos veces inserta duplicados: `ON CONFLICT (id)` nunca se activa porque cada
-- ejecución genera ids distintos. Con ids fijos el script es idempotente y se puede re-ejecutar sin
-- duplicar proveedores.

INSERT INTO providers (id, name, service_type, latitude, longitude, eta_minutes, rating, is_available) VALUES
  ('11111111-1111-4111-8111-111111111111', 'Grúas El Rápido',   'tow-truck', 4.7150, -74.0700, 12, 4.6, true),
  ('22222222-2222-4222-8222-222222222222', 'Grúas Zona Norte',  'tow-truck', 4.7300, -74.0600,  8, 4.1, true),
  ('33333333-3333-4333-8333-333333333333', 'Grúas Ocupadas SA', 'tow-truck', 4.7100, -74.0750,  5, 5.0, false),
  ('44444444-4444-4444-8444-444444444444', 'Cerrajería 24/7',   'locksmith', 4.7120, -74.0730, 15, 4.8, true),
  ('55555555-5555-4555-8555-555555555555', 'Baterías Express',  'battery',   4.7250, -74.0650, 20, 4.3, true),
  ('66666666-6666-4666-8666-666666666666', 'Batería Móvil Sur', 'battery',   4.6900, -74.0900, 25, 3.9, true)
ON CONFLICT (id) DO UPDATE SET
  name         = EXCLUDED.name,
  service_type = EXCLUDED.service_type,
  latitude     = EXCLUDED.latitude,
  longitude    = EXCLUDED.longitude,
  eta_minutes  = EXCLUDED.eta_minutes,
  rating       = EXCLUDED.rating,
  is_available = EXCLUDED.is_available;
