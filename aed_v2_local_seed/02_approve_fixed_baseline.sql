-- ONLY after: (1) Unity content has been inspected, (2) local validator test passed.
-- Requires psql -v expected_db=<local db> -v validator_passed=yes -f 02_approve_fixed_baseline.sql
\set ON_ERROR_STOP on
SELECT set_config('app.aed_expected_db', :'expected_db', false);
SELECT set_config('app.aed_validator_passed', :'validator_passed', false);
BEGIN;
DO $approve$
DECLARE content_changed integer;
DECLARE config_changed integer;
BEGIN
    IF current_database() <> current_setting('app.aed_expected_db') THEN
        RAISE EXCEPTION 'Wrong database: expected %, connected %',
            current_setting('app.aed_expected_db'), current_database();
    END IF;
    IF current_setting('app.aed_validator_passed') <> 'yes' THEN
        RAISE EXCEPTION 'Validator and Unity content verification are required before approval';
    END IF;
    IF EXISTS (
        SELECT 1 FROM "ScenarioConfigs"
        WHERE "UnityCompatibilityVersion" = '0.1.0'
          AND "IsActive" AND "IsProductionApproved" AND "IsFixedFallback"
    ) THEN
        RAISE EXCEPTION 'An approved active fixed fallback for 0.1.0 already exists';
    END IF;
    IF (
        SELECT count(*) FROM "ScenarioContentDefinitions"
        WHERE "ContentWhitelistVersion" = 'M2-WHITELIST-1'
          AND "UnityCompatibilityVersion" = '0.1.0'
    ) <> 4 OR (
        SELECT count(*) FROM "ScenarioContentDefinitions"
        WHERE "ContentWhitelistVersion" = 'M2-WHITELIST-1'
          AND "UnityCompatibilityVersion" = '0.1.0'
          AND "Provenance" = 'LOCAL_AED_V2_SOURCE_FIXEDDIRECTOR_20261008'
          AND NOT "IsActive" AND NOT "IsProductionApproved"
          AND ("ContentType", "ContentId") IN (
            ('Map', 'M2-MAP-1'), ('Monster', 'STALKER'),
            ('ObjectiveSpawnSet', 'DEFAULT_OBJECTIVES'),
            ('RouteModifier', 'DEFAULT_ROUTE')
          )
    ) <> 4 THEN
        RAISE EXCEPTION 'Staged content definitions must match the four reviewed entries exactly';
    END IF;
    IF (
        SELECT count(*) FROM "ScenarioConfigs"
        WHERE "ScenarioConfigId" = 'FIXED_BASELINE_V1'
          AND "ScenarioConfigVersion" = 'FIXED_BASELINE_V1'
          AND "SchemaVersion" = '1.1'
          AND "PolicyVersion" = 'AED_SCENARIO_POLICY_V1_1'
          AND "ConfigSource" = 'Fixed'
          AND "MapId" = 'M2-MAP-1' AND "MonsterType" = 'STALKER'
          AND "ObjectiveSpawnSetId" = 'DEFAULT_OBJECTIVES'
          AND "SupportItemBudget" = 0
          AND "DetectionFillRate" = 0.5 AND "DetectionDecayRate" = 1.0
          AND "ChaseSpeed" = 9.0 AND "SearchDuration" = 5.0
          AND "RouteModifier" = 'DEFAULT_ROUTE' AND "EscapeDoorTimerSeconds" = 45.0
          AND "FallbackConfigId" = 'FIXED_BASELINE_V1'
          AND "FallbackConfigVersion" = 'FIXED_BASELINE_V1'
          AND "ContentWhitelistVersion" = 'M2-WHITELIST-1'
          AND "UnityCompatibilityVersion" = '0.1.0'
          AND "IsFixedFallback" AND NOT "IsActive" AND NOT "IsProductionApproved"
          AND "Provenance" = 'LOCAL_AED_V2_SOURCE_FIXEDDIRECTOR_20261008'
    ) <> 1 THEN
        RAISE EXCEPTION 'Staged fixed scenario config does not match reviewed baseline';
    END IF;

    UPDATE "ScenarioContentDefinitions"
    SET "IsActive" = true, "IsProductionApproved" = true, "UpdatedAtUtc" = now()
    WHERE "ContentWhitelistVersion" = 'M2-WHITELIST-1'
      AND "UnityCompatibilityVersion" = '0.1.0'
      AND "Provenance" = 'LOCAL_AED_V2_SOURCE_FIXEDDIRECTOR_20261008';
    GET DIAGNOSTICS content_changed = ROW_COUNT;
    IF content_changed <> 4 THEN
        RAISE EXCEPTION 'Expected 4 content rows, updated %', content_changed;
    END IF;

    UPDATE "ScenarioConfigs"
    SET "IsActive" = true, "IsProductionApproved" = true, "UpdatedAtUtc" = now()
    WHERE "ScenarioConfigId" = 'FIXED_BASELINE_V1'
      AND "ScenarioConfigVersion" = 'FIXED_BASELINE_V1'
      AND "Provenance" = 'LOCAL_AED_V2_SOURCE_FIXEDDIRECTOR_20261008';
    GET DIAGNOSTICS config_changed = ROW_COUNT;
    IF config_changed <> 1 THEN
        RAISE EXCEPTION 'Expected 1 config row, updated %', config_changed;
    END IF;
END
$approve$;
COMMIT;

SELECT "ScenarioConfigId", "ScenarioConfigVersion", "IsActive", "IsProductionApproved", "IsFixedFallback"
FROM "ScenarioConfigs"
WHERE "UnityCompatibilityVersion" = '0.1.0'
  AND "IsActive" AND "IsProductionApproved" AND "IsFixedFallback";
