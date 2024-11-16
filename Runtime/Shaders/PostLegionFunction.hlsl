//UNITY_SHADER_NO_UPGRADE
#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED

// Define vec4, as used in Dedrich's code
struct vec4 {
	float x, y, z, w;
};

vec4 new_vec4 (float x, float y, float z, float w) {
	vec4 new_vec;
	new_vec.x = x;
	new_vec.y = y;
	new_vec.z = z;
	new_vec.w = w;

	return new_vec;
};

// Define color, as used in Dedrich's code
struct color {
	float r, g, b, a;
};

color new_color (float4 args) {
	color new_color;
	new_color.r = args[0];
	new_color.g = args[1];
	new_color.b = args[2];
	new_color.a = args[3];

	return new_color;
};

//sum
float sum(color a){
	return a.r + a.b + a.g;
};

//vec4 clamp
vec4 clamp(vec4 a, float min, float max){
	float x = clamp(a.x,min,max);
	float y = clamp(a.y,min,max);
	float z = clamp(a.z,min,max);
	float w = clamp(a.w,min,max);
	
	return new_vec4(x,y,z,w);
};

//vec4 dot
float dot(vec4 A, vec4 B){
	return A.x*B.x + A.y*B.y + A.z*B.z + A.w*B.w;
};

//color and vec4 dot
float dot_color(vec4 A, color B){
	return (A.x * B.r) + (A.y * B.g) + (A.z * B.b) + (A.z * B.a);
};

//Multiply two vec4s
vec4 vec4_multiply (vec4 a, vec4 b) {
	return new_vec4 (a.x*b.x, a.y*b.y, a.z*b.z, a.w*b.w);
};

//Divide two vec4s
vec4 vec4_divide (vec4 a, vec4 b) {
	return new_vec4 (a.x/b.x, a.y/b.y, a.z/b.z, a.w/b.w);
};

//Subtract two vec4s
vec4 vec4_subtract (vec4 a, vec4 b) {
	return new_vec4 (a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
};

void PostLegion_float(float4 Alpha_Map,
											float4 Layer_0,
											float4 Layer_1,
											float4 Layer_2,
											float4 Layer_3,
											float4 Height_0,
											float4 Height_1,
											float4 Height_2,
											float4 Height_3,
											float4 Height_Scale,
											float4 Height_Offset,
											out float4 diffuseOut,
											out float metallicOut,
											out float specularOut)
{
	color H0 = new_color(Height_0);
	color H1 = new_color(Height_1);
	color H2 = new_color(Height_2);
	color H3 = new_color(Height_3);

	//vec4 blendTex = new_vec4(Alpha_Map[0], Alpha_Map[1], Alpha_Map[2], Alpha_Map[3]);
	color blendTex = new_color(Alpha_Map);

	vec4 layerWeights = new_vec4(
					(1.0 - clamp(Alpha_Map.r + Alpha_Map.g + Alpha_Map.b, 0, 1)),
					blendTex.r,
					blendTex.g,
					blendTex.b
	);

	vec4 layerPct = new_vec4(
					layerWeights.x * (H0.a * Height_Scale[0] + Height_Offset[0]),
					layerWeights.y * (H1.a * Height_Scale[1] + Height_Offset[1]),
					layerWeights.z * (H2.a * Height_Scale[2] + Height_Offset[2]),
					layerWeights.w * (H3.a * Height_Scale[3] + Height_Offset[3])
	);

	float FlayerPctMax = max(max(layerPct.x, layerPct.y), max(layerPct.z, layerPct.w));
	vec4 layerPctMax = {FlayerPctMax,FlayerPctMax,FlayerPctMax,FlayerPctMax};

	vec4 layerPct1 = vec4_multiply(layerPct, vec4_subtract(new_vec4(1.0,1.0,1.0,1.0), clamp(vec4_subtract(layerPctMax, layerPct), 0, 1)));

	float fDot = dot(new_vec4(1,1,1,1), layerPct1);

	//layer_pct = layer_pct / vec4(sum(layer_pct));                          
	vec4 layerPct2 = vec4_divide(layerPct1, new_vec4(fDot,fDot,fDot,fDot));
	
	float4 weightedLayer_0 = Layer_0 * layerPct2.x;
	float4 weightedLayer_1 = Layer_1 * layerPct2.y;
	float4 weightedLayer_2 = Layer_2 * layerPct2.z;
	float4 weightedLayer_3 = Layer_3 * layerPct2.w;
	
	float metalBlend = (weightedLayer_0[3] + weightedLayer_1[3]) * 20;
	float specBlend = (weightedLayer_2[3] + weightedLayer_3[3]) * 2;
	
	float4 matDiffuse = (weightedLayer_0 + weightedLayer_1 + weightedLayer_2 + weightedLayer_3);
	//Will handle this in the shader graph:
	// * vColor * 2;

	//Outputs
	diffuseOut = matDiffuse;
	metallicOut = metalBlend;
	specularOut = specBlend;
};
#endif //MYHLSLINCLUDE_INCLUDED
