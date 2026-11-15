static class GraphicsShaders
{
    public const string SpriteVertex = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        in vec3 vertexNormal; // vegetation: height weight, anchor X, anchor Z
        uniform mat4 mvp;
        uniform vec4 windParameters;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragPosition;
        out vec3 fragNormal;
        void main()
        {
            vec3 p = vertexPosition;
            float phase = dot(vertexNormal.yz, vec2(0.83, 0.61)) * 1.37 + windParameters.z * windParameters.y;
            float sway = sin(phase) * 0.75 + sin(phase * 1.71 + 1.2) * 0.25;
            p.xz += vec2(0.83, 0.61) * sway * windParameters.x * vertexNormal.x * vertexNormal.x;
            fragTexCoord = vertexTexCoord; fragColor = vertexColor; fragPosition = p; fragNormal = vec3(0,1,0);
            gl_Position = mvp * vec4(p, 1.0);
        }
        """;
    public const string SpriteFragment = """
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec2 outlineParameters;
        out vec4 finalColor;
        float alphaAt(vec2 uv)
        {
            if (any(lessThan(uv, vec2(0))) || any(greaterThan(uv, vec2(1)))) return 0.0;
            return texture(texture0, uv).a;
        }
        void main()
        {
            vec4 color = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            if (color.a <= 0.01) discard;
            if (outlineParameters.y > 0.0)
            {
                vec2 d = fwidth(fragTexCoord) * outlineParameters.x;
                float edge = min(min(alphaAt(fragTexCoord + vec2(d.x,0)), alphaAt(fragTexCoord - vec2(d.x,0))),
                    min(alphaAt(fragTexCoord + vec2(0,d.y)), alphaAt(fragTexCoord - vec2(0,d.y))));
                color.rgb = mix(color.rgb, vec3(0.055,0.045,0.035), (1.0 - edge) * outlineParameters.y);
            }
            finalColor = vec4(atmosphere(color.rgb * sceneLight()), color.a);
        }
        """;
    public const string WaterFragment = """
        in vec4 fragColor;
        out vec4 finalColor;
        void main()
        {
            vec3 ripple = vec3(0.22, 0.34, 0.40) * rainRipple() * dot(ambientLight, vec3(0.3333));
            finalColor = vec4(atmosphere(fragColor.rgb + ripple), fragColor.a);
        }
        """;
    public const string PostVertex = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        void main() { fragTexCoord = vertexTexCoord; fragColor = vertexColor; gl_Position = mvp * vec4(vertexPosition, 1); }
        """;
    public const string BrightPass = """
        #version 330
        in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform float threshold;
        uniform int sourceCount;
        uniform vec4 sources[16];
        uniform vec4 sourceColors[16];
        uniform vec2 resolution;
        out vec4 finalColor;
        void main()
        {
            vec3 color = texture(texture0, fragTexCoord).rgb;
            float brightness = dot(color, vec3(0.2126,0.7152,0.0722));
            vec3 glow = color * smoothstep(threshold, min(1.0, threshold + 0.2), brightness);
            for (int i = 0; i < sourceCount; i++)
            {
                vec2 d = (fragTexCoord - sources[i].xy) * resolution / sources[i].z;
                glow += sourceColors[i].rgb * sourceColors[i].a * exp(-dot(d,d) * 3.5) * 0.6;
            }
            finalColor = vec4(glow, 1);
        }
        """;
    public const string Blur = """
        #version 330
        in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform vec2 direction;
        out vec4 finalColor;
        void main()
        {
            vec3 c = texture(texture0, fragTexCoord).rgb * 0.227027;
            c += texture(texture0, fragTexCoord + direction * 1.384615).rgb * 0.316216;
            c += texture(texture0, fragTexCoord - direction * 1.384615).rgb * 0.316216;
            c += texture(texture0, fragTexCoord + direction * 3.230769).rgb * 0.070270;
            c += texture(texture0, fragTexCoord - direction * 3.230769).rgb * 0.070270;
            finalColor = vec4(c, 1);
        }
        """;
    public const string Rays = """
        #version 330
        in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform vec2 sunPosition;
        out vec4 finalColor;
        void main()
        {
            vec2 p = fragTexCoord;
            vec2 stepUV = (p - sunPosition) * (0.95 / 32.0);
            float illumination = 1.0, result = 0.0;
            for (int i = 0; i < 32; i++)
            {
                p -= stepUV;
                if (all(greaterThanEqual(p,vec2(0))) && all(lessThanEqual(p,vec2(1)))) result += texture(texture0,p).r * illumination;
                illumination *= 0.96;
            }
            finalColor = vec4(vec3(result / 16.0 * exp(-length(fragTexCoord-sunPosition) * 0.8)), 1);
        }
        """;
    public const string DepthOfField = """
        #version 330
        in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform sampler2D depthMap;
        uniform vec2 resolution;
        uniform vec4 cameraPlanes; // near, far, orthographic, unused
        uniform vec4 focusParameters; // focus distance, sharp half-range, falloff, maximum radius in pixels
        out vec4 finalColor;
        float linearDepth(vec2 uv)
        {
            float d = texture(depthMap, uv).r;
            float n = cameraPlanes.x, f = cameraPlanes.y;
            if (cameraPlanes.z > 0.5) return mix(n, f, d);
            return (2.0 * n * f) / (f + n - (d * 2.0 - 1.0) * (f - n));
        }
        float blurRadius(float depth)
        {
            return smoothstep(focusParameters.y, focusParameters.y + focusParameters.z,
                abs(depth - focusParameters.x)) * focusParameters.w;
        }
        void main()
        {
            vec2 uv = fragTexCoord;
            float depth = linearDepth(uv), radius = blurRadius(depth);
            vec3 color = texture(texture0, uv).rgb;
            if (radius < 0.5) { finalColor = vec4(color, 1); return; }
            float total = 1.0;
            // A disk rather than a horizontal screen band: elevated terrain and both projections keep their depth.
            for (int i = 0; i < 24; i++)
            {
                float angle = float(i) * 2.39996323;
                float distance = sqrt((float(i) + 0.5) / 24.0);
                vec2 sampleUV = clamp(uv + vec2(cos(angle), sin(angle)) * distance * radius / resolution,
                    vec2(0.001), vec2(0.999));
                float sampleDepth = linearDepth(sampleUV);
                // Keep an in-focus foreground silhouette from bleeding into a blurred background.
                float weight = sampleDepth < depth - 0.3
                    ? smoothstep(radius * 0.25, radius * 0.75, blurRadius(sampleDepth)) : 1.0;
                color += texture(texture0, sampleUV).rgb * weight;
                total += weight;
            }
            finalColor = vec4(color / total, 1);
        }
        """;
    public const string Composite = """
        #version 330
        in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform sampler2D bloomMap;
        uniform sampler2D raysMap;
        uniform vec2 resolution;
        uniform vec4 strengths; // bloom, rays, rain, damage
        uniform vec4 colorGrade; // exposure, contrast, saturation, temperature
        uniform float vignetteStrength;
        uniform float heatStrength;
        uniform float time;
        uniform int heatCount;
        uniform vec4 heatSources[16];
        out vec4 finalColor;
        float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7))) * 43758.5453); }
        void main()
        {
            vec2 uv = fragTexCoord;
            for (int i = 0; i < heatCount; i++)
            {
                vec2 p = (uv - heatSources[i].xy) / heatSources[i].zw;
                float envelope = max(0.0, 1.0 - dot(p,p));
                uv.x += sin(p.y * 18.0 - time * 6.0 + float(i)) * heatStrength * envelope * envelope;
                uv.y += cos(p.x * 13.0 + time * 4.0) * heatStrength * 0.2 * envelope * envelope;
            }
            vec3 color = texture(texture0,clamp(uv,vec2(0.001),vec2(0.999))).rgb;
            color += texture(bloomMap,fragTexCoord).rgb * strengths.x;
            color += texture(raysMap,fragTexCoord).rgb * vec3(1.0,0.88,0.65) * strengths.y;
            if (strengths.z > 0.0)
            {
                vec2 px = vec2(fragTexCoord.x,1.0-fragTexCoord.y) * resolution;
                px.x += px.y * 0.19 + time * 36.0; px.y -= time * 540.0;
                vec2 cell = floor(px / vec2(25,110)), f = fract(px / vec2(25,110));
                float enabled = step(hash(cell), strengths.z * 0.6);
                float drop = (1.0-smoothstep(0.015,0.045,abs(f.x-0.5))) * smoothstep(0.1,0.17,f.y) * (1.0-smoothstep(0.42,0.63,f.y));
                color = mix(color,vec3(0.68,0.78,0.88),drop * enabled * 0.22);
            }
            color *= colorGrade.x;
            color = (color - 0.5) * colorGrade.y + 0.5;
            float luminance = dot(color, vec3(0.2126,0.7152,0.0722));
            color = mix(vec3(luminance), color, colorGrade.z);
            float highlights = smoothstep(0.2, 0.8, luminance);
            color += mix(vec3(-0.045,0.005,0.065), vec3(0.13,0.035,-0.075), highlights) * colorGrade.w;
            float edge = smoothstep(0.2,0.68,length((fragTexCoord-0.5)*vec2(1.0,0.85)));
            color *= 1.0 - edge * vignetteStrength;
            color = mix(color,vec3(0.55,0.025,0.015),edge * strengths.w * 0.7);
            finalColor = vec4(color,1);
        }
        """;
}
