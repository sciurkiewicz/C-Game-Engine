using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// A shared material atlas and a small lookup window keep blending independent of map size.
sealed class TerrainBlending : IDisposable
{
    public Shader Shader { get; }
    public Texture2D Atlas { get; private set; }
    Texture2D lookup;
    Color[] cells = [];
    KeyValuePair<string, Texture2D>[] materials = [];
    readonly Dictionary<string, int> indices = [];
    readonly RoadMaterialMask roads = new();
    readonly int atlasLayoutLocation, lookupLocation, lookupOriginLocation, mapSizeLocation, textureAxesLocation;
    readonly int roadMapLocation, roadWindowLocation;

    public TerrainBlending()
    {
        Shader = LoadShaderFromMemory(WorldLighting.VertexShader, WorldLighting.FragmentPrelude + """
            in vec2 fragTexCoord;
            in vec4 fragColor;
            uniform sampler2D texture0;
            uniform sampler2D materialMap;
            uniform vec4 atlasLayout; // columns, inner tile size, atlas width, atlas height
            uniform vec2 lookupOrigin;
            uniform vec2 mapSize;
            uniform vec4 textureAxes; // reference view's horizontal right XZ and backward XZ
            uniform sampler2D roadMap;
            uniform vec4 roadWindow; // lookup origin XY, samples per world unit, enabled
            out vec4 finalColor;

            int materialAt(ivec2 cell)
            {
                cell = clamp(cell, ivec2(0), ivec2(mapSize) - 1);
                ivec2 pixel = clamp(cell - ivec2(lookupOrigin), ivec2(0), textureSize(materialMap, 0) - 1);
                ivec3 id = ivec3(round(texelFetch(materialMap, pixel, 0).rgb * 255.0));
                return id.r + id.g * 256 + id.b * 65536;
            }
            vec3 materialColor(int id)
            {
                int columns = int(atlasLayout.x);
                vec2 tile = vec2(id % columns, id / columns);
                // Rotate the art around the map centre. PNG tops face away from the reference view;
                // the unrotated coordinates below still choose materials and blend their edges.
                vec2 offset = fragTexCoord - mapSize * 0.5;
                vec2 uv = vec2(dot(offset, textureAxes.xy), dot(offset, textureAxes.zw)) + mapSize * 0.5;
                // Large inked ground textures span eight tiles; material painting still uses one-tile cells.
                vec2 pixel = tile * (atlasLayout.y + 2.0) + 1.0 + fract(uv / 8.0) * atlasLayout.y;
                // Keep PNG colors; blending and scene lighting are applied separately below.
                return texture(texture0, pixel / atlasLayout.zw).rgb;
            }
            void main()
            {
                // Materials are anchored at tile centres. Smooth weights meet continuously across every edge.
                vec2 position = fragTexCoord - 0.5;
                ivec2 cell = ivec2(floor(position));
                vec2 weight = smoothstep(vec2(0.0), vec2(1.0), fract(position));
                int a = materialAt(cell);
                int b = materialAt(cell + ivec2(1, 0));
                int c = materialAt(cell + ivec2(0, 1));
                int d = materialAt(cell + ivec2(1, 1));
                vec3 color = materialColor(a);
                if (a != b || a != c || a != d)
                    color = mix(mix(color, materialColor(b), weight.x),
                                mix(materialColor(c), materialColor(d), weight.x), weight.y);
                if (roadWindow.w > 0.5)
                {
                    vec2 pixel = (fragTexCoord - roadWindow.xy) * roadWindow.z;
                    ivec2 size = textureSize(roadMap, 0);
                    if (all(greaterThanEqual(pixel, vec2(0.0))) && all(lessThan(pixel, vec2(size))))
                    {
                        vec4 road = texelFetch(roadMap, ivec2(pixel), 0);
                        ivec3 id = ivec3(round(road.rgb * 255.0));
                        float coverage = texture(roadMap, pixel / vec2(size)).a;
                        if (coverage > 0.0) color = mix(color, materialColor(id.r + id.g * 256 + id.b * 65536), coverage);
                    }
                }
                finalColor = vec4(atmosphere(wetGround(color * fragColor.rgb * sceneLight())), 1.0);
            }
            """);
        if (!IsShaderValid(Shader) || Shader.Id == Rlgl.GetShaderIdDefault())
            throw new InvalidOperationException("Cannot load terrain blending shader.");
        atlasLayoutLocation = GetShaderLocation(Shader, "atlasLayout");
        lookupLocation = GetShaderLocation(Shader, "materialMap");
        lookupOriginLocation = GetShaderLocation(Shader, "lookupOrigin");
        mapSizeLocation = GetShaderLocation(Shader, "mapSize");
        textureAxesLocation = GetShaderLocation(Shader, "textureAxes");
        roadMapLocation = GetShaderLocation(Shader, "roadMap");
        roadWindowLocation = GetShaderLocation(Shader, "roadWindow");
    }

    void UpdateAtlas(Dictionary<string, Texture2D> textures)
    {
        if (Atlas.Id != 0 && materials.Length == textures.Count &&
            materials.All(m => textures.TryGetValue(m.Key, out var texture) && texture.Id == m.Value.Id)) return;
        var entries = textures.ToArray();
        int size = entries.Max(m => Math.Max(m.Value.Width, m.Value.Height));
        int columns = (int)Math.Ceiling(Math.Sqrt(entries.Length)), rows = (entries.Length + columns - 1) / columns;
        var image = GenImageColor(columns * (size + 2), rows * (size + 2), Color.White);
        try
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var source = LoadImageFromTexture(entries[i].Value);
                try
                {
                    if (source.Width != size || source.Height != size) ImageResize(ref source, size, size);
                    int x = i % columns * (size + 2), y = i / columns * (size + 2);
                    // Wrap opposite edges into one-pixel gutters so bilinear filtering never samples another material.
                    for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int w = dx == 0 ? size : 1, h = dz == 0 ? size : 1;
                        ImageDraw(ref image, source, new(dx < 0 ? size - 1 : 0, dz < 0 ? size - 1 : 0, w, h),
                            new(x + (dx < 0 ? 0 : dx > 0 ? size + 1 : 1), y + (dz < 0 ? 0 : dz > 0 ? size + 1 : 1), w, h), Color.White);
                    }
                }
                finally { UnloadImage(source); }
            }
            var atlas = LoadTextureFromImage(image);
            if (!IsTextureValid(atlas)) throw new InvalidOperationException("Cannot create terrain material atlas.");
            SetTextureFilter(atlas, TextureFilter.Bilinear);
            if (Atlas.Id != 0) UnloadTexture(Atlas);
            Atlas = atlas;
            materials = entries;
            indices.Clear();
            for (int i = 0; i < materials.Length; i++) indices.Add(materials[i].Key, i);
            SetShaderValue(Shader, atlasLayoutLocation, new Vector4(columns, size, image.Width, image.Height), ShaderUniformDataType.Vec4);
        }
        finally { UnloadImage(image); }
    }

    public void Update(Dictionary<string, Texture2D> textures, MapData map, TerrainBounds bounds, Camera3D camera)
    {
        UpdateAtlas(textures);
        int minX = Math.Max(0, bounds.MinX - 1), minZ = Math.Max(0, bounds.MinZ - 1);
        int width = Math.Min(map.Width, bounds.MaxX + 1) - minX, height = Math.Min(map.Depth, bounds.MaxZ + 1) - minZ;
        bool changed = false;
        if (lookup.Id == 0 || lookup.Width < width || lookup.Height < height)
        {
            int textureWidth = Math.Max(32, (int)BitOperations.RoundUpToPowerOf2((uint)width));
            int textureHeight = Math.Max(32, (int)BitOperations.RoundUpToPowerOf2((uint)height));
            var image = GenImageColor(textureWidth, textureHeight, Color.Black);
            try
            {
                if (lookup.Id != 0) UnloadTexture(lookup);
                lookup = LoadTextureFromImage(image);
                if (!IsTextureValid(lookup)) throw new InvalidOperationException("Cannot create terrain material lookup.");
                SetTextureFilter(lookup, TextureFilter.Point);
                cells = new Color[textureWidth * textureHeight]; changed = true;
            }
            finally { UnloadImage(image); }
        }
        int fallback = indices["grass"];
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            string material = map.Tiles[(minZ + z) * map.Width + minX + x];
            int id = indices.GetValueOrDefault(material == "water" ? "sand" : material, fallback);
            var color = new Color(id & 255, (id >> 8) & 255, (id >> 16) & 255, 255);
            int pixel = z * lookup.Width + x;
            if (cells[pixel].R == color.R && cells[pixel].G == color.G && cells[pixel].B == color.B && cells[pixel].A == color.A) continue;
            cells[pixel] = color; changed = true;
        }
        if (changed) UpdateTexture(lookup, cells);
        SetShaderValue(Shader, lookupOriginLocation, new Vector2(minX, minZ), ShaderUniformDataType.Vec2);
        SetShaderValue(Shader, mapSizeLocation, new Vector2(map.Width, map.Depth), ShaderUniformDataType.Vec2);
        var down = new Vector2(camera.Position.X - camera.Target.X, camera.Position.Z - camera.Target.Z);
        // A top-down view gets its heading from screen-up instead of a zero-length ground direction.
        if (down.LengthSquared() < .000001f) down = new(-camera.Up.X, -camera.Up.Z);
        down = down.LengthSquared() > .000001f ? Vector2.Normalize(down) : Vector2.UnitY;
        SetShaderValue(Shader, textureAxesLocation, new Vector4(down.Y, -down.X, down.X, down.Y), ShaderUniformDataType.Vec4);
        roads.Update(map, bounds, indices, Atlas.Id);
        SetShaderValue(Shader, roadWindowLocation, roads.Window, ShaderUniformDataType.Vec4);
    }
    public unsafe void ConfigureMaterial(ref Material material)
    {
        material.Shader = Shader;
        material.Maps[(int)MaterialMapIndex.Albedo].Texture = Atlas;
        material.Maps[(int)MaterialMapIndex.Metalness].Texture = lookup;
        Shader.Locs[(int)ShaderLocationIndex.MapMetalness] = lookupLocation;
        material.Maps[(int)MaterialMapIndex.Roughness].Texture = roads.Texture;
        Shader.Locs[(int)ShaderLocationIndex.MapRoughness] = roadMapLocation;
    }
    public void Dispose()
    {
        roads.Dispose();
        if (lookup.Id != 0) UnloadTexture(lookup);
        if (Atlas.Id != 0) UnloadTexture(Atlas);
        UnloadShader(Shader);
    }
}
