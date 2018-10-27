﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Voronoi2;

namespace Barotrauma
{
    partial class Map
    {        
        private MapGenerationParams generationParams;
        
        private List<Level> levels;

        private List<Location> locations;

        private List<LocationConnection> connections;

        private string seed;
        private int size;

        private Location currentLocation;
        private Location selectedLocation;

        private LocationConnection selectedConnection;

        public Action<Location, LocationConnection> OnLocationSelected;
        //from -> to
        public Action<Location, Location> OnLocationChanged;
        
        public Location CurrentLocation
        {
            get { return currentLocation; }
        }

        public int CurrentLocationIndex
        {
            get { return locations.IndexOf(currentLocation); }
        }

        public Location SelectedLocation
        {
            get { return selectedLocation; }
        }

        public int SelectedLocationIndex
        {
            get { return locations.IndexOf(selectedLocation); }
        }

        public LocationConnection SelectedConnection
        {
            get { return selectedConnection; }
        }

        public string Seed
        {
            get { return seed; }
        }

        public List<Location> Locations
        {
            get { return locations; }
        }

        public Map(string seed)
        {
            generationParams = MapGenerationParams.Instance;
            this.seed = seed;
            this.size = generationParams.Size;

            levels = new List<Level>();

            locations = new List<Location>();

            connections = new List<LocationConnection>();

            Rand.SetSyncedSeed(ToolBox.StringToInt(this.seed));

            Generate();

            //start from the colony furthest away from the center
            float largestDist = 0.0f;
            Vector2 center = new Vector2(size, size) / 2;
            foreach (Location location in locations)
            {
                if (location.Type.Name != "City") continue;
                float dist = Vector2.DistanceSquared(center, location.MapPosition);
                if (dist > largestDist)
                {
                    largestDist = dist;
                    currentLocation = location;
                }
            }
            
            currentLocation.Discovered = true;

            foreach (LocationConnection connection in connections)
            {
                connection.Level = Level.CreateRandom(connection);
            }

            InitProjectSpecific();
        }

        partial void InitProjectSpecific();
        
        public float[,] Noise;

        private void GenerateNoiseMap(int octaves, float persistence)
        {
            float z = Rand.Range(0.0f, 1.0f, Rand.RandSync.Server);
            Noise = new float[generationParams.NoiseResolution, generationParams.NoiseResolution];
            
            float min = float.MaxValue, max = 0.0f;
            for (int x = 0; x < generationParams.NoiseResolution; x++)
            {
                for (int y = 0; y < generationParams.NoiseResolution; y++)
                {
                    Noise[x, y] = (float)PerlinNoise.OctavePerlin(
                        (double)x / generationParams.NoiseResolution, 
                        (double)y / generationParams.NoiseResolution, 
                        z, generationParams.NoiseFrequency, octaves, persistence);
                    min = Math.Min(Noise[x, y], min);
                    max = Math.Max(Noise[x, y], max);
                }
            }

            float radius = generationParams.NoiseResolution / 2;
            Vector2 center = Vector2.One * radius;
            float range = max - min;
            
            float centerDarkenRadius = radius * generationParams.CenterDarkenRadius;            
            float edgeDarkenRadius = radius * generationParams.EdgeDarkenRadius;
            for (int x = 0; x < generationParams.NoiseResolution; x++)
            {
                for (int y = 0; y < generationParams.NoiseResolution; y++)
                {
                    //normalize the noise to 0-1 range
                    Noise[x, y] = (Noise[x, y] - min) / range;

                    float dist = Vector2.Distance(center, new Vector2(x, y));
                    if (dist < centerDarkenRadius)
                    {
                        float angle = (float)Math.Atan2(y - center.Y, x - center.X);
                        float phase = angle * generationParams.CenterDarkenWaveFrequency + Noise[x, y] * generationParams.CenterDarkenWavePhaseNoise;
                        float currDarkenRadius = centerDarkenRadius * (0.6f + (float)Math.Sin(phase) * 0.4f);
                        if (dist < currDarkenRadius)
                        {
                            float darkenAmount = 1.0f - (dist / currDarkenRadius);
                            Noise[x, y] = MathHelper.Lerp(Noise[x, y], Noise[x, y] * (1.0f - generationParams.CenterDarkenStrength), darkenAmount);
                        }
                    }
                    if (dist > edgeDarkenRadius)
                    {
                        float darkenAmount = Math.Min((dist - edgeDarkenRadius) / (radius - edgeDarkenRadius), 1.0f);
                        Noise[x, y] = MathHelper.Lerp(Noise[x, y], 1.0f - generationParams.EdgeDarkenStrength, darkenAmount);
                    }
                }
            }
        }

        partial void GenerateNoiseMapProjSpecific();

        private void Generate()
        {
            connections.Clear();
            locations.Clear();

            GenerateNoiseMap(generationParams.NoiseOctaves, generationParams.NoisePersistence);

            List<Vector2> sites = new List<Vector2>();
            float mapRadius = size / 2;
            Vector2 mapCenter = new Vector2(mapRadius, mapRadius);

            float locationRadius = mapRadius * generationParams.LocationRadius;

            for (float x = mapCenter.X - locationRadius; x < mapCenter.X + locationRadius; x += generationParams.VoronoiSiteInterval)
            {
                for (float y = mapCenter.Y - locationRadius; y < mapCenter.Y + locationRadius; y += generationParams.VoronoiSiteInterval)
                {
                    float noiseVal = Noise[(int)(x / size * generationParams.NoiseResolution), (int)(y / size * generationParams.NoiseResolution)];
                    if (Rand.Range(generationParams.VoronoiSitePlacementMinVal, 1.0f, Rand.RandSync.Server) < 
                        noiseVal * generationParams.VoronoiSitePlacementProbability)
                    {
                        sites.Add(new Vector2(x, y));
                    }
                }
            }

            Voronoi voronoi = new Voronoi(0.5f);
            List<GraphEdge> edges = voronoi.MakeVoronoiGraph(sites, size, size);
            float zoneRadius = size / 2 / generationParams.DifficultyZones;

            sites.Clear();
            foreach (GraphEdge edge in edges)
            {
                if (edge.Point1 == edge.Point2) continue;
                
                if (Vector2.DistanceSquared(edge.Point1, mapCenter) >= locationRadius * locationRadius ||
                    Vector2.DistanceSquared(edge.Point2, mapCenter) >= locationRadius * locationRadius) continue;

                Location[] newLocations = new Location[2];
                newLocations[0] = locations.Find(l => l.MapPosition == edge.Point1 || l.MapPosition == edge.Point2);
                newLocations[1] = locations.Find(l => l != newLocations[0] && (l.MapPosition == edge.Point1 || l.MapPosition == edge.Point2));

                for (int i = 0; i < 2; i++)
                {
                    if (newLocations[i] != null) continue;

                    Vector2[] points = new Vector2[] { edge.Point1, edge.Point2 };

                    int positionIndex = Rand.Int(1, Rand.RandSync.Server);

                    Vector2 position = points[positionIndex];
                    if (newLocations[1 - i] != null && newLocations[1 - i].MapPosition == position) position = points[1 - positionIndex];
                    int zone = MathHelper.Clamp(generationParams.DifficultyZones - (int)Math.Floor(Vector2.Distance(position, mapCenter) / zoneRadius), 1, generationParams.DifficultyZones);
                    newLocations[i] = Location.CreateRandom(position, zone);
                    locations.Add(newLocations[i]);
                }

                var newConnection = new LocationConnection(newLocations[0], newLocations[1]);
                float centerDist = Vector2.Distance(newConnection.CenterPos, mapCenter);
                newConnection.Difficulty = MathHelper.Clamp(((1.0f - centerDist / mapRadius) * 100) + Rand.Range(-10.0f, 10.0f, Rand.RandSync.Server), 0, 100);

                connections.Add(newConnection);
            }

            //remove connections that are too short
            float minDistance = generationParams.MinConnectionDistance;
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                LocationConnection connection = connections[i];

                if (Vector2.Distance(connection.Locations[0].MapPosition, connection.Locations[1].MapPosition) > minDistance)
                {
                    continue;
                }

                //locations.Remove(connection.Locations[0]);
                connections.Remove(connection);

                foreach (LocationConnection connection2 in connections)
                {
                    if (connection2.Locations[0] == connection.Locations[0]) connection2.Locations[0] = connection.Locations[1];
                    if (connection2.Locations[1] == connection.Locations[0]) connection2.Locations[1] = connection.Locations[1];
                }
            }

            HashSet<Location> connectedLocations = new HashSet<Location>();
            foreach (LocationConnection connection in connections)
            {
                connection.Locations[0].Connections.Add(connection);
                connection.Locations[1].Connections.Add(connection);

                connectedLocations.Add(connection.Locations[0]);
                connectedLocations.Add(connection.Locations[1]);
            }

            //remove orphans
            locations.RemoveAll(c => !connectedLocations.Contains(c));
            
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                i = Math.Min(i, connections.Count - 1);

                LocationConnection connection = connections[i];

                for (int n = Math.Min(i - 1, connections.Count - 1); n >= 0; n--)
                {
                    if (connection.Locations.Contains(connections[n].Locations[0])
                        && connection.Locations.Contains(connections[n].Locations[1]))
                    {
                        connections.RemoveAt(n);
                    }
                }
            }

            foreach (LocationConnection connection in connections)
            {
                float centerDist = Vector2.Distance(connection.CenterPos, mapCenter);
                connection.Difficulty = MathHelper.Clamp(((1.0f - centerDist / mapRadius) * 100) + Rand.Range(-10.0f, 10.0f, Rand.RandSync.Server), 0, 100);
            }

            AssignBiomes();

            GenerateNoiseMapProjSpecific();
        }

        private void AssignBiomes()
        {
            float locationRadius = size * 0.5f * generationParams.LocationRadius;

            var biomes = LevelGenerationParams.GetBiomes();
            Vector2 centerPos = new Vector2(size, size) / 2;
            for (int i = 0; i < generationParams.DifficultyZones; i++)
            {
                List<Biome> allowedBiomes = biomes.FindAll(b => b.AllowedZones.Contains(generationParams.DifficultyZones - i));
                float zoneRadius = locationRadius * ((i + 1.0f) / generationParams.DifficultyZones);
                foreach (LocationConnection connection in connections)
                {
                    if (connection.Biome != null) continue;

                    if (i == generationParams.DifficultyZones - 1 ||
                        Vector2.Distance(connection.Locations[0].MapPosition, centerPos) < zoneRadius ||
                        Vector2.Distance(connection.Locations[1].MapPosition, centerPos) < zoneRadius)
                    {
                        connection.Biome = allowedBiomes[Rand.Range(0, allowedBiomes.Count, Rand.RandSync.Server)];
                    }
                }
            }
        }

        private void ExpandBiomes(List<LocationConnection> seeds)
        {
            List<LocationConnection> nextSeeds = new List<LocationConnection>(); 
            foreach (LocationConnection connection in seeds)
            {
                foreach (Location location in connection.Locations)
                {
                    foreach (LocationConnection otherConnection in location.Connections)
                    {
                        if (otherConnection == connection) continue;                        
                        if (otherConnection.Biome != null) continue; //already assigned

                        otherConnection.Biome = connection.Biome;
                        nextSeeds.Add(otherConnection);                        
                    }
                }
            }

            if (nextSeeds.Count > 0)
            {
                ExpandBiomes(nextSeeds);
            }
        }

        private List<LocationConnection> GetMapEdges()
        {
            List<Vector2> verts = locations.Select(l => l.MapPosition).ToList();

            List<Vector2> giftWrappedVerts = MathUtils.GiftWrap(verts);

            List<LocationConnection> edges = new List<LocationConnection>();
            foreach (LocationConnection connection in connections)
            {
                if (giftWrappedVerts.Contains(connection.Locations[0].MapPosition) || 
                    giftWrappedVerts.Contains(connection.Locations[1].MapPosition))
                {
                    edges.Add(connection);
                }
            }
            
            return edges;
        }
        
        public void MoveToNextLocation()
        {
            Location prevLocation = currentLocation;
            selectedConnection.Passed = true;

            currentLocation = selectedLocation;
            currentLocation.Discovered = true;
            selectedLocation = null;

            OnLocationChanged?.Invoke(prevLocation, currentLocation);
        }

        public void SetLocation(int index)
        {
            if (index == -1)
            {
                currentLocation = null;
                return;
            }

            if (index < 0 || index >= locations.Count)
            {
                DebugConsole.ThrowError("Location index out of bounds");
                return;
            }

            Location prevLocation = currentLocation;
            currentLocation = locations[index];
            currentLocation.Discovered = true;

            OnLocationChanged?.Invoke(prevLocation, currentLocation);
        }

        public void SelectLocation(int index)
        {
            if (index == -1)
            {
                selectedLocation = null;
                selectedConnection = null;

                OnLocationSelected?.Invoke(null, null);
                return;
            }

            if (index < 0 || index >= locations.Count)
            {
                DebugConsole.ThrowError("Location index out of bounds");
                return;
            }

            selectedLocation = locations[index];
            selectedConnection = connections.Find(c => c.Locations.Contains(currentLocation) && c.Locations.Contains(selectedLocation));
            OnLocationSelected?.Invoke(selectedLocation, selectedConnection);
        }

        public void SelectLocation(Location location)
        {
            if (!locations.Contains(location))
            {
                DebugConsole.ThrowError("Failed to select a location. "+location.Name+" not found in the map.");
                return;
            }

            selectedLocation = location;
            selectedConnection = connections.Find(c => c.Locations.Contains(currentLocation) && c.Locations.Contains(selectedLocation));
            OnLocationSelected?.Invoke(selectedLocation, selectedConnection);
        }

        public void SelectRandomLocation(bool preferUndiscovered)
        {
            List<Location> nextLocations = currentLocation.Connections.Select(c => c.OtherLocation(currentLocation)).ToList();            
            List<Location> undiscoveredLocations = nextLocations.FindAll(l => !l.Discovered);
            
            if (undiscoveredLocations.Count > 0 && preferUndiscovered)
            {
                SelectLocation(undiscoveredLocations[Rand.Int(undiscoveredLocations.Count, Rand.RandSync.Unsynced)]);
            }
            else
            {
                SelectLocation(nextLocations[Rand.Int(nextLocations.Count, Rand.RandSync.Unsynced)]);
            }
        }

        public void ProgressWorld()
        {
            foreach (Location location in locations)
            {
                if (!location.Discovered) continue;

                //find which types of locations this one can change to
                List<LocationTypeChange> allowedTypeChanges = new List<LocationTypeChange>();
                List<LocationTypeChange> readyTypeChanges = new List<LocationTypeChange>();
                foreach (LocationTypeChange typeChange in location.Type.CanChangeTo)
                {
                    //check if there are any adjacent locations that would prevent the change
                    bool disallowedFound = false;
                    foreach (string disallowedLocationName in typeChange.DisallowedAdjacentLocations)
                    {
                        if (location.Connections.Any(c => c.OtherLocation(location).Type.Name.ToLowerInvariant() == disallowedLocationName.ToLowerInvariant()))
                        {
                            disallowedFound = true;
                            break;
                        }
                    }
                    if (disallowedFound) continue;

                    //check that there's a required adjacent location present
                    bool requiredFound = false;
                    foreach (string requiredLocationName in typeChange.RequiredAdjacentLocations)
                    {
                        if (location.Connections.Any(c => c.OtherLocation(location).Type.Name.ToLowerInvariant() == requiredLocationName.ToLowerInvariant()))
                        {
                            requiredFound = true;
                            break;
                        }
                    }
                    if (!requiredFound && typeChange.RequiredAdjacentLocations.Count > 0) continue;

                    allowedTypeChanges.Add(typeChange);

                    if (location.TypeChangeTimer >= typeChange.RequiredDuration)
                    {
                        readyTypeChanges.Add(typeChange);
                    }
                }

                //select a random type change
                if (Rand.Range(0.0f, 1.0f) < readyTypeChanges.Sum(t => t.Probability))
                {
                    var selectedTypeChange = 
                        ToolBox.SelectWeightedRandom(readyTypeChanges, readyTypeChanges.Select(t => t.Probability).ToList(), Rand.RandSync.Unsynced);
                    if (selectedTypeChange != null)
                    {
                        string prevName = location.Name;
                        location.ChangeType(LocationType.List.Find(lt => lt.Name.ToLowerInvariant() == selectedTypeChange.ChangeTo.ToLowerInvariant()));
                        ChangeLocationType(location, prevName, selectedTypeChange);
                        location.TypeChangeTimer = -1;
                        break;
                    }
                }
                
                if (allowedTypeChanges.Count > 0)
                {
                    location.TypeChangeTimer++;
                }
                else
                {
                    location.TypeChangeTimer = 0;
                }
            }
        }

        partial void ChangeLocationType(Location location, string prevName, LocationTypeChange change);

        public static Map LoadNew(XElement element)
        {
            string mapSeed = element.GetAttributeString("seed", "a");            
            Map map = new Map(mapSeed);
            map.Load(element, false);

            return map;
        }

        public void Load(XElement element, bool showNotifications)
        {
            SetLocation(element.GetAttributeInt("currentlocation", 0));

            if (!Version.TryParse(element.GetAttributeString("version", ""), out _))
            {
                DebugConsole.ThrowError("Incompatible map save file, loading the game failed.");
                return;
            }

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "location":
                        string locationType = subElement.GetAttributeString("type", "");
                        Location location = locations[subElement.GetAttributeInt("i", 0)];
                        int typeChangeTimer = subElement.GetAttributeInt("changetimer", 0);

                        string prevLocationName = location.Name;
                        LocationType prevLocationType = location.Type;
                        location.Discovered = true;
                        location.ChangeType(LocationType.List.Find(lt => lt.Name.ToLowerInvariant() == locationType.ToLowerInvariant()));
                        location.TypeChangeTimer = typeChangeTimer;
                        if (showNotifications && prevLocationType != location.Type)
                        {
                            ChangeLocationType(
                                location,
                                prevLocationName,
                                prevLocationType.CanChangeTo.Find(c => c.ChangeTo.ToLowerInvariant() == location.Type.Name.ToLowerInvariant()));
                        }
                        break;
                    case "connection":
                        int connectionIndex = subElement.GetAttributeInt("i", 0);
                        int missionsCompleted = subElement.GetAttributeInt("missionscompleted", 0);

                        connections[connectionIndex].Passed = true;
                        connections[connectionIndex].MissionsCompleted = missionsCompleted;
                        break;
                }
            }
        }

        public void Save(XElement element)
        {
            XElement mapElement = new XElement("map");

            mapElement.Add(new XAttribute("version", GameMain.Version.ToString()));
            mapElement.Add(new XAttribute("currentlocation", CurrentLocationIndex));
            mapElement.Add(new XAttribute("seed", Seed));

            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (!location.Discovered) continue;

                var locationElement = new XElement("location", new XAttribute("i", i));
                locationElement.Add(new XAttribute("type", location.Type.Name));

                if (location.TypeChangeTimer > 0)
                {
                    locationElement.Add(new XAttribute("changetimer", location.TypeChangeTimer));
                }

                mapElement.Add(locationElement);
            }

            for (int i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (!connection.Passed) continue;

                connection.CheckMissionCompleted();

                var connectionElement = new XElement("connection", new XAttribute("i", i));
                if (connection.MissionsCompleted > 0)
                {
                    connectionElement.Add(new XAttribute("missionscompleted", connection.MissionsCompleted));
                }

                mapElement.Add(connectionElement);
            }
            
            element.Add(mapElement);
        }
    }
}
