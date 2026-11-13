using System;
using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.Gallery
{
    /// <summary>
    /// The Gallery's one source of truth (Part B, 2026-11-12): the room's size, the paintings in the order a visitor meets
    /// them walking from the door left to right (each with its real canvas size, title, image, the scene Enter opens or none
    /// for "Coming soon", its wall label's number and time of day, and where it hangs), and where the decor stands.
    /// Tools > InnerFrame > Gallery > Build Layout (GalleryLayoutSetup) works out the hanging sizes, writes them back here
    /// and builds Gallery.unity; Art/Source/Gallery/build_gallery.py reads the room, the bench and The Bedroom's frame
    /// from here for the shell (Gallery.fbx). Unity axes: north z-, east x-, west x+, the arch in the south wall (z+).
    /// </summary>
    [CreateAssetMenu(menuName = "InnerFrame/Gallery Layout", fileName = "GalleryLayout")]
    public class GalleryLayout : ScriptableObject
    {
        public enum Wall { North, East, West, South }

        [Serializable]
        public class Painting
        {
            [Tooltip("Names its objects: Slot_<key>, Frame_<key>, Entrance_<key>, Label_<key>; the canvas is Painting_<key> " +
                     "(The Bedroom and The Starry Night keep Painting_TheBedroom and Painting_TheStarryNight).")]
            public string key;
            public string title;
            [Tooltip("Under the title, e.g. \"Vincent van Gogh · 1889\".")] public string caption;
            [Tooltip("The wall label's number; empty for none (Vincent's self-portrait).")] public string number;
            [Tooltip("The wall label's time of day, e.g. Afternoon.")] public string time;
            public Texture2D image;
            [Tooltip("URP Lit, white (GalleryLightingSetup.MakePainting).")] public Material material;
            [Tooltip("The scene Enter opens (in Build Settings); empty shows \"Coming soon\" with Enter off.")] public string scene;
            [Tooltip("Off for a painting with no menu at all (Vincent's self-portrait: you talk to him instead).")] public bool menu = true;
            public Wall wall;
            [Tooltip("Metres from the wall's centre: x on the north and south walls, z on the east and west walls.")] public float along;
            [Tooltip("The real canvas, centimetres (width, height), as the museum gives it.")] public Vector2 canvasCm;
            [Header("Worked out by Build Layout (build_gallery.py reads them)")]
            [Tooltip("The canvas as hung, metres.")] public float width, height;
            [Tooltip("Floor to the canvas's bottom edge, metres.")] public float bottom;
        }

        [Serializable]
        public class Prop
        {
            [Tooltip("A child of the GalleryDecor instance.")] public string name;
            public Vector3 position;
            public float yaw;
            [Tooltip("Lanterns: whether its lamp gives light (the lantern keeps its glowing glass either way).")] public bool lit = true;
        }

        [Header("Room, inside, metres (build_gallery.py reads these)")]
        public float roomWidth = 9f;
        public float roomLength = 12f;
        public float roomHeight = 4f;
        [Tooltip("The bench's centre, Unity x and z (build_gallery.py reads it).")] public Vector2 bench;

        [Header("Hanging (every painting at one scale, so their real sizes compare)")]
        [Tooltip("The Bedroom's width as hung, metres, before the height rule below.")] public float bedroomWidth = 2.2f;
        [Tooltip("Top of the wainscot's cap rail, metres (build_gallery.py: WAINSCOT_H + RAIL_H / 2).")] public float railTop = 1.03f;
        [Tooltip("Every frame's bottom edge stands this far above the rail, metres.")] public float frameAboveRail = 0.10f;
        [Tooltip("No canvas's centre higher than this, metres; if one would be, all shrink by the same ratio.")] public float maxCentre = 2.2f;
        [Tooltip("The Bedroom frame's border, metres (build_gallery.py FRAME_W); the others are its copies, scaled.")] public float frameBorder = 0.18f;
        [Tooltip("Worked out: metres of wall per metre of canvas.")] public float scale;

        [Header("Paintings, in the order you meet them from the door, left to right")]
        public List<Painting> paintings = new List<Painting>();

        [Header("Decor, along the walls")]
        public List<Prop> decor = new List<Prop>();

        [Header("The arch (GP_Arch: Cici's Tripo model, made by Art/Source/Gallery/make_arch.py) and the corridor behind it")]
        [Tooltip("The wall the arch stands in, facing into the room. build_gallery.py cuts its doorway in the south wall.")]
        public Wall archWall = Wall.South;
        [Tooltip("Metres from the wall's centre, as a painting's along (the doorway is cut at 0).")] public float archAlong;
        [Tooltip("The corridor behind the arch, metres from the wall's outer face to its end wall and door (build_gallery.py reads it).")]
        public float corridorLength = 4f;

        public Painting Find(string key) => paintings.Find(p => p.key == key);

        /// <summary>Into the room from that wall.</summary>
        public static Vector3 Normal(Wall w) =>
            w == Wall.North ? Vector3.forward : w == Wall.South ? Vector3.back : w == Wall.East ? Vector3.right : Vector3.left;

        /// <summary>Facing into the room from `w` (a north-wall object, facing +z, needs no turn).</summary>
        public static Quaternion Facing(Wall w) => Quaternion.LookRotation(Normal(w), Vector3.up);

        /// <summary>The canvas's centre, on the wall's face.</summary>
        public Vector3 OnWall(Painting p)
        {
            float y = p.bottom + p.height / 2f;
            switch (p.wall)
            {
                case Wall.North: return new Vector3(p.along, y, -roomLength / 2f);
                case Wall.South: return new Vector3(p.along, y, roomLength / 2f);
                case Wall.East: return new Vector3(-roomWidth / 2f, y, p.along);
                default: return new Vector3(roomWidth / 2f, y, p.along);
            }
        }

        /// <summary>The hanging sizes from the canvases: one scale for all (The Bedroom bedroomWidth wide, unless that puts a
        /// canvas's centre above maxCentre), each frame's bottom frameAboveRail over the rail. Returns whether anything changed.</summary>
        public bool WorkOutSizes()
        {
            var bedroom = Find("Bedroom");
            if (bedroom == null || bedroom.canvasCm.x <= 0f) throw new InvalidOperationException("GalleryLayout: The Bedroom's canvasCm is needed");
            float hB = bedroom.canvasCm.y / 100f;
            float s = bedroomWidth / (bedroom.canvasCm.x / 100f);
            foreach (var p in paintings)
            {
                float h = p.canvasCm.y / 100f;
                float low = railTop + frameAboveRail + frameBorder * h / hB;          // the canvas's bottom
                s = Mathf.Min(s, (maxCentre - low) / (h / 2f));
            }
            s = (float)Math.Round(s, 4);
            bool changed = !Mathf.Approximately(scale, s);
            scale = s;
            foreach (var p in paintings)
            {
                float h = p.canvasCm.y / 100f;
                float w = R(p.canvasCm.x / 100f * s), ht = R(h * s), b = R(railTop + frameAboveRail + frameBorder * h / hB);
                changed |= !Mathf.Approximately(p.width, w) || !Mathf.Approximately(p.height, ht) || !Mathf.Approximately(p.bottom, b);
                p.width = w; p.height = ht; p.bottom = b;
            }
            return changed;
        }

        static float R(float v) => (float)Math.Round(v, 4);
    }
}
