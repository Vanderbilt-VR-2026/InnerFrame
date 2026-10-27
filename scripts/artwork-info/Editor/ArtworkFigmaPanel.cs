using System;
using System.Linq;
using InnerFrame.ArtworkInfo;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace InnerFrame.EditorTools
{
    // Figma dRg2LX8SNV5wouBX1Zr76N / 2:37. Coordinates are top-left design pixels.
    // Uniform 3x canvas units at .001 m/unit preserves 476:416 and every relative slot.
    public static class ArtworkFigmaPanel
    {
        public const float Scale=3;
        public static readonly Vector2 CanvasSize=new Vector2(476,416)*Scale;
        const string Design="Assets/ArtworkInfo/Design/";
        public const string Copy="Painted in 1889, The Bedroom depicts Vincent van Gogh's room in the Yellow House in Arles, southern France. Its unusual perspective, simplified forms, and expressive colors transform an ordinary interior into a deeply personal space.\n"+
            "Van Gogh wanted the painting to communicate a sense of rest and tranquility. Yet the tilted furniture, strong outlines, and contrasting colors create a distinctive visual tension.\n"+
            "Rather than reproducing the room exactly as it appeared, he used color and composition to express how the space felt to him.\n"+
            "Look closer: Notice how the angled floorboards, bright walls, and carefully arranged furniture shape your experience of the room.";
        static readonly Color Ink=new Color32(0x5A,0x53,0x53,255);

        public static void Preflight()
        {
            foreach(var name in new[]{"Portrait.png","CloseBackground.png","Divider.png","EnterBackground.png"})
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(Design+name)==null)
                    throw new InvalidOperationException("Missing Figma design asset: "+Design+name+". Install the supplied design bundle; see docs/artwork-info/README.md. No generic substitute will be generated.");
            foreach(var name in new[]{"CormorantGaramond-Medium.ttf","Inter-Regular.ttf"})
                if(AssetDatabase.LoadAssetAtPath<Font>(Design+name)==null)
                    throw new InvalidOperationException("Missing design font: "+Design+name+". Install the supplied design bundle first.");
        }

        public static void Build(RectTransform canvas,string folder,ArtworkDetailPanel panel)
        {
            var serif=FontAsset("CormorantGaramond-Medium",folder);
            var sans=FontAsset("Inter-Regular",folder);
            Picture(canvas,"Portrait — Figma 2:2","Portrait.png",30,48,112,136);
            Label(canvas,serif,"Title — 1:7","THE BEDROOM",28,189,112,19,16,19,Color.black);
            Picture(canvas,"Divider — 2:9","Divider.png",28,212,112.0045f,3);
            Label(canvas,sans,"Artist and Year — 2:3","Vincent van Gogh · 1889",28,219,116,12,10,12,Ink);
            Label(canvas,sans,"Medium and Collection — 2:5","Oil on canvas\nArt Institute of Chicago",28,234,110,24,10,12,Ink);
            Label(canvas,sans,"Introduction — 2:7",Copy,205,48,243,285,12,15,Ink);
            var enter=Control(canvas,"ENTER (navigation unconnected)","EnterBackground.png",26,345,112,36);
            Label(canvas,serif,"Enter Label — 2:32","ENTER",57,354,51,19,16,19,Color.black);
            // Preserve visual state; no implicit scene search, hardcoded scene name or invented route.
            enter.gameObject.AddComponent<ArtworkEnterAction>().Configure(enter);
            var close=Control(canvas,"Close","CloseBackground.png",444,20,21,21);
            Label(canvas,serif,"Close Label — 2:36","X",449,20,11,19,16,19,new Color32(170,170,170,255));
            panel.Configure(close);
            Check(canvas);
            CheckTitleRegression(canvas);
        }

        static TMP_FontAsset FontAsset(string name,string folder)
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>(Design+name+".ttf");
            var asset=TMP_FontAsset.CreateFontAsset(source,90,9,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
            if(asset==null) throw new InvalidOperationException("TMP could not create "+name);
            asset.name=name+" SDF";
            string characters=new string(Enumerable.Range(32,95).Select(x=>(char)x).ToArray())+"·";
            if(!asset.TryAddCharacters(characters,out string missing))
                throw new InvalidOperationException("Missing SDF glyphs for "+name+": "+missing);
            // Bake the required glyphs now: no runtime atlas population or OS-font dependency.
            asset.atlasPopulationMode=AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset,folder+"/"+asset.name+".asset");
            asset.material.name=asset.name+" Material";
            AssetDatabase.AddObjectToAsset(asset.material,asset);
            foreach(var atlas in asset.atlasTextures)
            {
                atlas.name=asset.name+" Atlas"; atlas.filterMode=FilterMode.Bilinear;
                AssetDatabase.AddObjectToAsset(atlas,asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static RectTransform Slot(RectTransform canvas,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));
            go.layer=LayerMask.NameToLayer("UI"); go.transform.SetParent(canvas,false);
            var rect=(RectTransform)go.transform;
            rect.anchoredPosition=new Vector2(x+w/2-238,208-y-h/2)*Scale;
            rect.sizeDelta=new Vector2(w,h)*Scale;
            return rect;
        }
        static Texture2D Texture(string filename)
        {
            string path=Design+filename;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            // Configure only these owned design inputs. No project-wide rendering changes.
            if(importer.textureCompression!=TextureImporterCompression.Uncompressed || importer.mipmapEnabled || importer.npotScale!=TextureImporterNPOTScale.None || !importer.alphaIsTransparency)
            {
                importer.textureType=TextureImporterType.Default;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=false; importer.npotScale=TextureImporterNPOTScale.None;
                importer.alphaIsTransparency=true; importer.filterMode=FilterMode.Bilinear;
                importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static RawImage Picture(RectTransform canvas,string name,string file,float x,float y,float w,float h)
        {
            var image=Slot(canvas,name,x,y,w,h).gameObject.AddComponent<RawImage>();
            image.texture=Texture(file); image.raycastTarget=false;
            return image;
        }
        static Button Control(RectTransform canvas,string name,string file,float x,float y,float w,float h)
        {
            var image=Picture(canvas,name,file,x,y,w,h); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.normalColor=Color.white;
            colors.highlightedColor=new Color(.94f,.92f,.88f,1);
            colors.pressedColor=new Color(.82f,.82f,.82f,1);
            colors.selectedColor=colors.highlightedColor; colors.fadeDuration=.1f;
            button.colors=colors; button.navigation=new Navigation{mode=Navigation.Mode.None};
            return button;
        }
        static void Label(RectTransform canvas,TMP_FontAsset font,string name,string value,float x,float y,float w,float h,float size,float lineHeight,Color color)
        {
            var rect=Slot(canvas,name,x,y,w,h);
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font; text.fontSize=size*Scale; text.color=color;
            // Figma's line box includes half-leading above and below the font em metrics.
            // TMP's <line-height> sets baseline advance, NOT first-line ascender/descender bounds.
            // Preserve the design rect and font size; express that half-leading as margins.
            var face=font.faceInfo;
            float emHeight=(face.ascentLine-face.descentLine)*text.fontSize/face.pointSize*face.scale;
            float halfLeading=(lineHeight*Scale-emHeight)/2;
            text.margin=new Vector4(0,halfLeading,0,halfLeading-0.01f);
            text.alignment=TextAlignmentOptions.TopLeft;
            text.raycastTarget=false; text.enableAutoSizing=false;
            // Figma AUTO resolves to 15/12/19 px for these runs. Fixed line-height preserves composition.
            text.richText=true;
            text.text="<line-height="+(lineHeight*Scale).ToString(System.Globalization.CultureInfo.InvariantCulture)+">"+value;
            text.overflowMode=TextOverflowModes.Overflow;
        }
        public static void Check(RectTransform canvas)
        {
            if(Vector2.Distance(canvas.sizeDelta,CanvasSize)>.01f)
                throw new InvalidOperationException("Figma board must preserve 476:416 aspect ratio.");
            // Rebuild the completed hierarchy, including inactive reloaded prefab contents.
            canvas.ForceUpdateRectTransforms();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvas);
            Canvas.ForceUpdateCanvases();
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                CheckText(text);
            foreach(var image in canvas.GetComponentsInChildren<RawImage>(true))
                if(image.texture==null) throw new InvalidOperationException("Missing Figma layer texture: "+image.name);
        }

        static void CheckTitleRegression(RectTransform canvas)
        {
            var title=canvas.Find("Title — 1:7").GetComponent<TMP_Text>();
            if(title.fontSize!=16*Scale || title.rectTransform.sizeDelta!=new Vector2(112,19)*Scale || title.enableAutoSizing)
                throw new InvalidOperationException("Title regression: Figma size/box changed.");
            var margin=title.margin;
            try
            {
                title.margin=Vector4.zero; // Reproduce the old line-box mapping, not a missing font.
                bool rejected=false;
                try { CheckText(title); }
                catch(InvalidOperationException error)
                {
                    if(!error.Message.StartsWith("Figma TEXT OVERFLOW",StringComparison.Ordinal)) throw;
                    rejected=true;
                }
                if(!rejected) throw new InvalidOperationException("Title regression: expected the old zero-margin box to overflow with the bundled Cormorant metrics.");
            }
            finally { title.margin=margin; title.ComputeMarginSize(); title.ForceMeshUpdate(true,true); }
            CheckText(title);
        }

        static void CheckText(TMP_Text text)
        {
            if(text.font==null)
                throw new InvalidOperationException("Figma FONT MISSING: "+text.name+"; rect="+text.rectTransform.rect);
            var font=text.font;
            var face=font.faceInfo;
            string identity=$"{text.name}; font={font.name}; path={AssetDatabase.GetAssetPath(font)}; family={face.familyName}; style={face.styleName}; pointSize={face.pointSize}; scale={face.scale}; ascent={face.ascentLine}; descent={face.descentLine}; faceLineHeight={face.lineHeight}";
            if(font.atlasPopulationMode!=AtlasPopulationMode.Static || font.material==null ||
                font.atlasTextures==null || font.atlasTextures.Length==0 || font.atlasTextures.Any(x=>x==null) ||
                face.pointSize<=0 || face.scale<=0)
                throw new InvalidOperationException("Figma SDF INVALID: "+identity+"; population="+font.atlasPopulationMode);
            text.rectTransform.ForceUpdateRectTransforms();
            text.ComputeMarginSize();
            text.ForceMeshUpdate(true,true);
            var plain=text.GetParsedText();
            var missing=plain.Where(c=>!char.IsControl(c) && !font.HasCharacter(c)).Distinct()
                .Select(c=>$"U+{(int)c:X4}").ToArray();
            var preferred=text.GetPreferredValues(text.text,text.rectTransform.rect.width,float.PositiveInfinity);
            // GetPreferredValues has its own layout pass. Regenerate before inspecting rendered state.
            text.ForceMeshUpdate(true,true);
            var rect=text.rectTransform.rect;
            string atlasInfo=string.Join(",",font.atlasTextures.Select(x=>$"{x.name}:{x.width}x{x.height}"));
            var glyphs=new System.Text.StringBuilder();
            bool outside=false, fallback=false;
            const float tolerance=0.5f*Scale; // Half a Figma pixel for raster/font sampling, not layout growth.
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                var c=text.textInfo.characterInfo[i];
                if(char.IsControl(c.character)) continue;
                fallback |= c.fontAsset!=font;
                var glyph=c.textElement?.glyph;
                glyphs.Append($" U+{(int)c.character:X4}:glyph={glyph?.index},font={c.fontAsset?.name},scale={c.scale:F4}");
                if(!c.isVisible || glyph==null) continue;
                // Check ink geometry, excluding the transparent SDF padding around its quad.
                var m=glyph.metrics;
                float left=c.origin+m.horizontalBearingX*c.scale;
                float top=c.baseLine+m.horizontalBearingY*c.scale;
                float right=left+m.width*c.scale, bottom=top-m.height*c.scale;
                glyphs.Append($",ink=({left:F3},{bottom:F3})..({right:F3},{top:F3})");
                outside |= left<rect.xMin-tolerance || right>rect.xMax+tolerance ||
                    bottom<rect.yMin-tolerance || top>rect.yMax+tolerance;
            }
            string report=identity+$"; atlas=[{atlasInfo}]; glyphCount={font.glyphTable.Count}; shader={font.material.shader?.name}; fontSize={text.fontSize}; autoSize={text.enableAutoSizing}; rect={rect}; margins={text.margin}; preferred={preferred}; textBounds={text.textBounds}; overflow={text.isTextOverflowing}; firstOverflow={text.firstOverflowCharacterIndex}; lines={text.textInfo.lineCount}; missing=[{string.Join(",",missing)}]; fallback={fallback}; inkOutside={outside}; markup={text.text}\nGlyphs:"+glyphs;
            if(missing.Length>0 || fallback)
                throw new InvalidOperationException("Figma GLYPH MISSING OR SUBSTITUTED: "+report);
            if(text.isTextOverflowing)
                throw new InvalidOperationException("Figma TEXT OVERFLOW after finalized layout: "+report);
            if(outside)
                throw new InvalidOperationException("Figma VISIBLE GLYPH OUTSIDE DESIGN BOX: "+report);
            Debug.Log("[ArtworkInfo] Text layout PASS: "+report,text);
        }
    }
}
