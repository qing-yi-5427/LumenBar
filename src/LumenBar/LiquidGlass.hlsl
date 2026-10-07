sampler2D source : register(s0);
float4 geometry : register(c0); // width, height, corner radius, dark mode
float4 tint : register(c1);     // tint RGB, density
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 size=geometry.xy;
    float2 p=(uv-.5)*size;
    float2 q=abs(p)-(size*.5-geometry.z);
    float2 outside=max(q,0);
    float distance=length(outside)+min(max(q.x,q.y),0)-geometry.z;
    float2 normal=normalize(outside+.001)*sign(p);
    float edge=saturate(1+distance/12);
    float2 bent=uv-normal*(edge*edge*7)/size;
    // Preserve the background detail at the centre; bend and soften at the rim.
    float2 blur=(1.1+edge*1.4)/size;
    float3 colour=tex2D(source,bent).rgb*.5;
    colour+=(tex2D(source,bent+blur).rgb+tex2D(source,bent-blur).rgb)*.25;
    colour=lerp(colour,tint.rgb,tint.a);
    float rim=saturate(1-abs(distance+1.0)/1.1);
    float light=saturate(dot(normal,float2(-.55,-.83)));
    float sheen=light*edge*edge*.12;
    colour+=sheen+rim*(.1+light*.45);
    colour-=saturate(dot(normal,float2(.55,.83)))*edge*(1-edge)*.16;
    float alpha=saturate(.5-distance);
    return float4(saturate(colour)*alpha,alpha);
}
