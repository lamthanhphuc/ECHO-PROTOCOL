using UnityEngine;
using UnityEngine.UI;

namespace EchoProtocol.UI.HUD
{
    public enum PurgeIcon { Purge, UV, Item, Meat, Fuel, Alien, Medkit, Health, Stamina, Team, Interact, Downed, Spectate, Missions, Danger }

    // Small line meshes stay sharp at every Canvas scale and require no texture atlas.
    public sealed class HUDPurgeIcon : MaskableGraphic
    {
        [SerializeField] private PurgeIcon kind;
        public PurgeIcon Kind { get => kind; set { if (kind != value) { kind = value; SetVerticesDirty(); } } }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (kind)
            {
                case PurgeIcon.Danger: Path(mesh, 12,22, 23,3, 1,3, 12,22); Path(mesh, 12,15, 12,9); Path(mesh, 12,7, 12,6); break;
                case PurgeIcon.Purge: Path(mesh, 12,2, 7,6, 5,11, 8,16, 10,12, 12,22, 18,15, 19,10, 16,5, 12,2); break;
                case PurgeIcon.UV: Path(mesh, 7,22, 17,22, 17,18, 15,15, 15,3, 9,3, 9,15, 7,18, 7,22); Path(mesh, 8,18, 16,18); break;
                case PurgeIcon.Fuel: Path(mesh, 7,21, 12,21, 14,18, 19,18, 19,3, 5,3, 5,18, 7,21); Path(mesh, 8,6, 16,15); Path(mesh, 8,15, 16,6); break;
                case PurgeIcon.Meat: Path(mesh, 3,10, 4,16, 9,20, 15,19, 19,14, 17,9, 11,8, 7,3, 4,4, 5,7, 3,10); Circle(mesh, 11,14,3); break;
                case PurgeIcon.Alien: Path(mesh, 5,15, 7,21, 17,21, 20,15, 17,7, 12,2, 7,7, 5,15); Path(mesh, 7,14, 10,12, 8,11); Path(mesh, 17,14, 14,12, 16,11); break;
                case PurgeIcon.Medkit: Path(mesh, 3,17, 21,17, 21,3, 3,3, 3,17); Path(mesh, 8,17, 8,21, 16,21, 16,17); Path(mesh, 12,6, 12,14); Path(mesh, 8,10, 16,10); break;
                case PurgeIcon.Health: Path(mesh, 12,3, 3,12, 3,17, 6,20, 10,20, 12,17, 14,20, 18,20, 21,17, 21,12, 12,3); Path(mesh, 4,12, 8,12, 10,15, 13,8, 15,12, 20,12); break;
                case PurgeIcon.Stamina: Path(mesh, 7,21, 4,17, 4,12, 8,11, 10,15, 9,20, 7,21); Path(mesh, 7,8, 5,5, 7,3, 9,6); Path(mesh, 16,18, 14,14, 14,9, 18,8, 20,12, 18,17, 16,18); Path(mesh, 17,6, 15,3, 17,1, 19,4); break;
                case PurgeIcon.Team: Circle(mesh, 8,17,3); Circle(mesh, 17,16,2.5f); Path(mesh, 2,5, 3,10, 7,12, 11,11, 14,5); Path(mesh, 15,10, 19,10, 22,5); break;
                case PurgeIcon.Interact: Path(mesh, 7,4, 3,11, 5,13, 8,10, 8,20, 10,21, 11,14, 12,22, 14,21, 14,14, 16,20, 18,19, 17,12, 20,15, 22,13, 19,5, 15,2, 10,2, 7,4); break;
                case PurgeIcon.Downed: Circle(mesh, 12,15,7); Path(mesh, 7,10, 8,4, 16,4, 17,10); Circle(mesh, 9,15,1); Circle(mesh, 15,15,1); Path(mesh, 10,7, 10,4); Path(mesh, 14,7, 14,4); break;
                case PurgeIcon.Spectate: Path(mesh, 2,12, 6,17, 12,20, 18,17, 22,12, 18,7, 12,4, 6,7, 2,12); Circle(mesh, 12,12,4); break;
                case PurgeIcon.Missions: Path(mesh, 2,19, 4,17, 7,21); Path(mesh, 11,19, 22,19); Path(mesh, 2,11, 4,9, 7,13); Path(mesh, 11,11, 22,11); Path(mesh, 2,3, 4,1, 7,5); Path(mesh, 11,3, 22,3); break;
                default: Path(mesh, 3,17, 12,22, 21,17, 21,7, 12,2, 3,7, 3,17, 12,12, 21,17); Path(mesh, 12,12, 12,2); break;
            }
        }

        private void Circle(VertexHelper mesh, float x, float y, float radius)
        {
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI / 10f, b = (i + 1) * Mathf.PI / 10f;
                Line(mesh, new Vector2(x + Mathf.Cos(a)*radius, y + Mathf.Sin(a)*radius),
                    new Vector2(x + Mathf.Cos(b)*radius, y + Mathf.Sin(b)*radius));
            }
        }

        private void Path(VertexHelper mesh, params float[] points)
        {
            for (int i = 2; i < points.Length; i += 2)
                Line(mesh, new Vector2(points[i-2], points[i-1]), new Vector2(points[i], points[i+1]));
        }

        private void Line(VertexHelper mesh, Vector2 from, Vector2 to)
        {
            var rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height) / 24f;
            Vector2 offset = rect.center - Vector2.one * (12f * scale);
            from = from * scale + offset; to = to * scale + offset;
            Vector2 normal = new Vector2(-(to-from).y, (to-from).x).normalized * scale * .7f;
            int start = mesh.currentVertCount;
            mesh.AddVert(from-normal, color, Vector2.zero); mesh.AddVert(from+normal, color, Vector2.zero);
            mesh.AddVert(to+normal, color, Vector2.zero); mesh.AddVert(to-normal, color, Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
