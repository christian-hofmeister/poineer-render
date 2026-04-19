-- V1__create_poi_table.sql
-- Basic schema for POIs in Berlin

CREATE TABLE IF NOT EXISTS poi (
    id              INTEGER PRIMARY KEY,
    osm_id          INTEGER NOT NULL,
    name            TEXT NULL,
    amenity         TEXT NULL,
    latitude        REAL NOT NULL,
    longitude       REAL NOT NULL
);

-- Indexes
CREATE UNIQUE INDEX IF NOT EXISTS idx_poi_osm_id ON poi(osm_id);
CREATE INDEX IF NOT EXISTS idx_poi_amenity ON poi(amenity);
CREATE INDEX IF NOT EXISTS idx_poi_lat_lon ON poi(latitude, longitude);