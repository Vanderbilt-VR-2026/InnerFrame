using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace InnerFrame.EditorTools
{
    /// <summary>One transparent shell, one tiny procedural tint map. No scene sampling or blur.</summary>
    public static class ArtworkGlassBuilder
    {
        public const float Width = 1.24f, Height = 0.94f, Depth = 0.008f, Radius = 0.045f;
        public static GameObject Create(string folder)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new System.InvalidOperationException("URP Unlit shader required.");
            var texture = TintTexture();
            AssetDatabase.CreateAsset(texture, folder + "/Milky Glass Tint.asset");
            var material = new Material(shader) { name="Light White Frosted Glass" };
            material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Surface",1); // Transparent, straight alpha (not premultiplied).
            material.SetFloat("_Blend",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);
            material.SetFloat("_Cull",(float)CullMode.Back);
            material.SetFloat("_AlphaClip",0);
            material.SetFloat("_QueueOffset",0);
            material.SetOverrideTag("RenderType","Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.renderQueue=(int)RenderQueue.Transparent; // Share queue with background UI for depth sorting.
            foreach (var pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals", "DepthNormalsOnly", "MotionVectors" })
                material.SetShaderPassEnabled(pass,false);
            AssetDatabase.CreateAsset(material,folder+"/Light White Frosted Glass.mat");
            var mesh=ArtworkInfoGeometry.RoundedGlass("Rounded thin glass shell",Width,Height,Depth,Radius);
            AssetDatabase.CreateAsset(mesh,folder+"/Rounded Glass Shell.asset");
            var root=new GameObject("ArtworkDetailPanel");
            var body=new GameObject("Milky Glass Body",typeof(MeshFilter),typeof(MeshRenderer));
            body.transform.SetParent(root.transform,false);
            body.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            return root;
        }
        static Texture2D TintTexture()
        {
            const int width=256, height=192;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false)
            { name="Milky tint and soft edge highlights", wrapMode=TextureWrapMode.Clamp, filterMode=FilterMode.Bilinear };
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                float u=(x+0.5f)/width, v=(y+0.5f)/height;
                Vector2 point=new Vector2((u-0.5f)*Width,(v-0.5f)*Height);
                Vector2 q=new Vector2(Mathf.Abs(point.x)-Width/2+Radius,Mathf.Abs(point.y)-Height/2+Radius);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+
                    Mathf.Min(Mathf.Max(q.x,q.y),0)-Radius;
                float rim=1-Mathf.SmoothStep(0,0.008f,-distance);
                // Localized extra milkiness in right reading column, with no second transparent layer.
                float reading=Mathf.SmoothStep(-0.07f,0.08f,point.x)*0.08f;
                float alpha=Mathf.Lerp(0.60f+0.04f*v+reading,0.84f,rim);
                pixels[y*width+x]=new Color(0.95f+0.025f*v,0.965f+0.02f*v,0.985f,alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false,false);
            return texture;
        }
    }
}
