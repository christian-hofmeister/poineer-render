-- V2__insert_sample_pois.sql
-- Insert a few sample POIs so we can verify the DB content

INSERT INTO poi (osm_id, name, amenity, latitude, longitude)
VALUES
    ('node/1', 'Test Café Berlin', 'cafe', 52.520008, 13.404954),
    ('node/2', 'Test Restaurant Berlin', 'restaurant', 52.519000, 13.401000),
    ('node/3', 'Test Supermarkt Berlin', 'supermarket', 52.518000, 13.409000);
