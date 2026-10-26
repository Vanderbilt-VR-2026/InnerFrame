using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.EditorTools
{
    /// <summary>Closed chamfered slab. Four eight-vertex rings, 60 triangles, no textures.</summary>
    public static class ArtworkInfoGeometry
    {
        public static Mesh Slab(string name, float width, float height, float depth, float bevel)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float[] zs = { -depth / 2, -depth / 2 + bevel, depth / 2 - bevel, depth / 2 };
            for (int ring = 0; ring < 4; ring++)
            {
                float inset = ring == 0 || ring == 3 ? bevel : 0;
                float x = width / 2 - inset, y = height / 2 - inset, c = bevel;
                Vector2[] corners = { new Vector2(-x+c,-y), new Vector2(x-c,-y), new Vector2(x,-y+c),
                    new Vector2(x,y-c), new Vector2(x-c,y), new Vector2(-x+c,y), new Vector2(-x,y-c), new Vector2(-x,-y+c) };
                foreach (var point in corners) vertices.Add(new Vector3(point.x, point.y, zs[ring]));
            }
            for (int ring = 0; ring < 3; ring++) for (int i = 0; i < 8; i++)
            {
                int a = ring * 8 + i, b = ring * 8 + (i+1)%8, c = a+8, d = b+8;
                triangles.AddRange(new[] { a,b,c,b,d,c });
            }
            for (int i = 1; i < 7; i++)
            {
                triangles.AddRange(new[] { 0,i+1,i,24,24+i,24+i+1 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
