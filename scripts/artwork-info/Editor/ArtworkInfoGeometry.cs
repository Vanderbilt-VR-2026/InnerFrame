using System.Collections.Generic;
using UnityEngine;

namespace InnerFrame.EditorTools
{
    /// <summary>Closed chamfered slab. Four eight-vertex rings, 60 triangles, no textures.</summary>
    public static class ArtworkInfoGeometry
    {
        /// <summary>Thin closed rounded rectangle; 8 segments per corner, four rings (284 triangles).</summary>
        public static Mesh RoundedGlass(string name, float width, float height, float depth, float radius)
        {
            const int steps = 8;
            const int count = 4 * (steps + 1);
            const float bevel = 0.001f;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float[] zs = { -depth/2, -depth/2 + bevel, depth/2 - bevel, depth/2 };
            for (int ring = 0; ring < 4; ring++)
            {
                float inset = ring == 0 || ring == 3 ? bevel : 0;
                Vector2[] centers = { new Vector2(width/2-radius,-height/2+radius),
                    new Vector2(width/2-radius,height/2-radius), new Vector2(-width/2+radius,height/2-radius),
                    new Vector2(-width/2+radius,-height/2+radius) };
                for (int corner = 0; corner < 4; corner++) for (int i = 0; i <= steps; i++)
                {
                    float angle = (-90 + 90*corner + 90f*i/steps)*Mathf.Deg2Rad;
                    Vector2 point = centers[corner] + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(radius-inset);
                    vertices.Add(new Vector3(point.x,point.y,zs[ring]));
                    uv.Add(new Vector2(point.x/width+0.5f,point.y/height+0.5f));
                }
            }
            for (int ring=0; ring<3; ring++) for (int i=0; i<count; i++)
            {
                int a=ring*count+i, b=ring*count+(i+1)%count, c=a+count, d=b+count;
                triangles.AddRange(new[] { a,b,c,b,d,c });
            }
            for (int i=1; i<count-1; i++)
                triangles.AddRange(new[] { 0,i+1,i,3*count,3*count+i,3*count+i+1 });
            var mesh = new Mesh { name=name };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

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
