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
            clipWorld();
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
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D reflectionMap;
        uniform sampler2D refractionMap;
        uniform vec2 waterViewport;
        uniform vec3 shallowColor;
        uniform vec3 deepColor;
        uniform vec4 waterOptics; // clarity, reflection, refraction, foam
        uniform vec3 waterSurface; // animation time, wave height, captures available
        out vec4 finalColor;
        vec2 waterGradient(vec2 cell)
        {
            uint seed=uint(int(cell.x))*374761393u+uint(int(cell.y))*668265263u+2246822519u;
            seed=(seed^(seed>>13u))*1274126177u; seed^=seed>>16u;
            vec2 gradient=vec2(float(seed&65535u),float(seed>>16u))*(1.0/32767.5)-1.0;
            return gradient*inversesqrt(max(dot(gradient,gradient),0.0001));
        }
        // Random corner gradients avoid the repeated flat seams of lattice value-noise derivatives.
        vec3 waterNoise(vec2 p)
        {
            vec2 cell=floor(p),f=fract(p);
            vec2 u=f*f*f*(f*(f*6.0-15.0)+10.0);
            vec2 du=30.0*f*f*(f*(f-2.0)+1.0);
            vec2 ga=waterGradient(cell),gb=waterGradient(cell+vec2(1,0));
            vec2 gc=waterGradient(cell+vec2(0,1)),gd=waterGradient(cell+vec2(1,1));
            float a=dot(ga,f),b=dot(gb,f-vec2(1,0));
            float c=dot(gc,f-vec2(0,1)),d=dot(gd,f-vec2(1,1));
            float bottom=mix(a,b,u.x),top=mix(c,d,u.x);
            vec2 slope=mix(mix(ga,gb,u.x),mix(gc,gd,u.x),u.y);
            slope+=du*vec2(mix(b-a,d-c,u.y),top-bottom);
            return vec3(mix(bottom,top,u.y),slope);
        }
        vec2 filteredWaterSlope(vec2 p)
        {
            float visible=1.0-smoothstep(0.7,2.0,max(length(dFdx(p)),length(dFdy(p))));
            return waterNoise(p).yz*visible;
        }
        vec2 waterSlope(vec2 p,float t)
        {
            mat2 turn0=mat2(0.8,-0.6,0.6,0.8),turn1=mat2(0.36,0.932,-0.932,0.36);
            mat2 turn2=mat2(-0.73,0.683,-0.683,-0.73),turn3=mat2(0.91,0.415,-0.415,0.91);
            vec2 warp=vec2(waterNoise(p*0.21+vec2(t*0.08,-t*0.05)).x,
                waterNoise(turn1*p*0.17+vec2(19.7-t*0.06,31.4+t*0.09)).x);
            vec2 q0=turn0*(p+warp*1.2)*1.47+vec2(t*0.17,-t*0.12);
            vec2 q1=turn1*(p+vec2(warp.y,-warp.x)*0.8)*5.23+vec2(13.7-t*0.31,8.4+t*0.27);
            vec2 q2=turn2*(p-warp*0.5)*13.79+vec2(37.1+t*0.63,-21.6-t*0.41);
            vec2 q3=turn3*(p+vec2(-warp.y,warp.x)*0.3)*31.13+vec2(-11.3-t*0.87,43.9+t*0.72);
            vec2 slope=transpose(turn0)*filteredWaterSlope(q0)*0.09;
            slope+=transpose(turn1)*filteredWaterSlope(q1)*0.10;
            slope+=transpose(turn2)*filteredWaterSlope(q2)*0.07;
            slope+=transpose(turn3)*filteredWaterSlope(q3)*0.045;
            return slope;
        }
        vec2 flowingSlope(vec2 p,vec2 velocity,float t)
        {
            float amount=smoothstep(0.0,0.15,length(velocity));
            if(amount<0.001)return waterSlope(p,t);
            // Two fading phases bound stretching where painted currents turn or meet.
            // Subtracting the velocity makes wave features travel in the painted direction.
            float phase=fract(t*0.25),other=fract(phase+0.5);
            float weight=1.0-abs(phase*2.0-1.0);
            // Slow evolution keeps phase renewal from replaying exactly the same four-second loop.
            vec2 moving=mix(waterSlope(p-velocity*(other*4.0),t*0.05),
                waterSlope(p-velocity*(phase*4.0),t*0.05),weight);
            if(amount>0.999)return moving;
            return mix(waterSlope(p,t),moving,amount);
        }
        void main()
        {
            float depth=max(0.0,fragTexCoord.x),t=waterSurface.x;
            vec2 p=fragPosition.xz;
            vec2 worldDx=dFdx(p),worldDy=dFdy(p);
            float depthDx=dFdx(depth),depthDy=dFdy(depth);
            float determinant=worldDx.x*worldDy.y-worldDx.y*worldDy.x;
            vec2 bedGradient=abs(determinant)>0.00000001
                ? vec2(depthDx*worldDy.y-depthDy*worldDx.y,depthDy*worldDx.x-depthDx*worldDy.x)/determinant : vec2(0.0);
            // A band measured along the ground keeps its width on gentle and steep banks.
            float shoreDistance=depth/max(0.06,length(bedGradient));
            float wave=clamp(waterSurface.y/0.08,0.0,2.5)*smoothstep(0.0,0.25,depth);
            mat2 turn0=mat2(0.8,-0.6,0.6,0.8),turn1=mat2(0.36,0.932,-0.932,0.36);
            vec2 warp=vec2(waterNoise(p*0.21+vec2(t*0.08,-t*0.05)).x,
                waterNoise(turn1*p*0.17+vec2(19.7-t*0.06,31.4+t*0.09)).x);
            float shoreNoise=clamp(0.5+waterNoise(p*2.7+vec2(17.2,31.9)).x,0.0,1.0);
            float shoreWidth=mix(0.15,0.25,clamp(0.5+waterNoise(turn0*p*1.3+vec2(8.1,53.7)).x,0.0,1.0));
            float retreat=mix(0.035,0.075,shoreNoise);
            retreat+=waterNoise(p*0.9+vec2(t*0.13,-t*0.17)).x*min(waterSurface.y,0.12)*0.2;
            float edgeAA=max(0.005,fwidth(shoreDistance)*0.7);
            float coverage=smoothstep(retreat,retreat+shoreWidth+edgeAA,shoreDistance);
            float shoreOptics=smoothstep(0.06,0.5,shoreDistance);
            vec2 velocity=fragNormal.xz/max(fragNormal.y,0.0001);
            vec2 slope=flowingSlope(p,velocity,t)*wave;
            vec3 n=normalize(vec3(-slope.x,1.0,-slope.y));
            vec3 v=normalize(viewPosition-fragPosition);
            float opticalDepth=depth/(waterOptics.x*max(0.35,abs(v.y)));
            float transmission=exp(-opticalDepth);
            vec3 tint=mix(shallowColor,deepColor,1.0-exp(-depth/waterOptics.x));
            vec3 illumination=sceneLight();
            vec3 color=tint*illumination;
            vec2 uv=gl_FragCoord.xy/waterViewport;
            vec2 distortion=n.xz*waterOptics.z*0.007*min(depth,1.0)*shoreOptics;
            if(waterSurface.z>0.5)
            {
                vec4 bed=texture(refractionMap,clamp(uv+distortion,vec2(0.002),vec2(0.998)));
                vec3 transmitted=exp(-opticalDepth*vec3(1.65,0.95,0.8));
                color=bed.rgb*transmitted*bed.a+color*(1.0-transmitted*bed.a);
            }
            float fresnel=0.12+0.82*pow(1.0-clamp(dot(n,v),0.0,1.0),4.0);
            vec3 sky=mix(vec3(0.24,0.39,0.46),vec3(0.56,0.71,0.77),clamp(n.y,0.0,1.0))*ambientLight;
            if(waterSurface.z>0.5)
            {
                vec4 reflected=texture(reflectionMap,clamp(vec2(uv.x,1.0-uv.y)+distortion*0.65,vec2(0.002),vec2(0.998)));
                sky=mix(sky,reflected.rgb,reflected.a);
            }
            color=mix(color,sky,clamp(fresnel*waterOptics.y*shoreOptics,0.0,0.85));
            vec3 sun=normalize(vec3(-0.35,1.0,-0.45));
            vec3 halfSun=normalize(sun+v);
            float normalVariance=dot(dFdx(n),dFdx(n))+dot(dFdy(n),dFdy(n));
            float sharpness=clamp(2.0/(0.0015+normalVariance*0.22),250.0,1000.0);
            float facing=max(0.0,dot(n,halfSun));
            float glint=pow(facing,sharpness)*1.7;
            float sheen=pow(facing,32.0)*0.08;
            color+=vec3(1.0,0.98,0.93)*(glint+sheen)*min(1.0,ambientLight.g)*waterOptics.y*shoreOptics;
            for(int i=0;i<lightCount;i++)
            {
                vec3 l=lightPositionRadius[i].xyz-fragPosition;
                float falloff=max(0.0,1.0-length(l)/lightPositionRadius[i].w);
                vec3 halfLight=normalize(normalize(l)+v);
                color+=lightColorIntensity[i].rgb*lightColorIntensity[i].w*falloff*falloff*waterOptics.y*shoreOptics*
                    pow(max(0.0,dot(n,halfLight)),min(240.0,sharpness))*0.85;
            }
            float foamNoise=clamp(0.6+waterNoise(turn1*p*3.7+warp+vec2(t*0.21,-t*0.32)).x,0.0,1.0);
            float foamBand=smoothstep(0.05,0.14,shoreDistance)*(1.0-smoothstep(0.22,0.5,shoreDistance));
            float foam=foamBand*smoothstep(0.36,0.76,foamNoise)*waterOptics.w*0.6;
            color=mix(color,vec3(0.78,0.86,0.83)*ambientLight,foam);
            color+=vec3(0.14,0.20,0.24)*rainRipple()*min(1.0,ambientLight.g);
            float opacity=waterSurface.z>0.5 ? coverage : clamp(1.0-transmission,0.0,0.96)*coverage;
            if(opacity<=0.001)discard;
            finalColor=vec4(atmosphere(color),opacity);
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
        uniform float time;
        out vec4 finalColor;
        float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7))) * 43758.5453); }
        void main()
        {
            vec2 uv = fragTexCoord;
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
