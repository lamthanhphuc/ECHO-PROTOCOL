using UnityEngine;
namespace EchoProtocol.Salvage
{
    public sealed class SalvageAidVisual : MonoBehaviour
    {
        private int _dose=-1;
        public static void Apply(GameObject visual,int dose)
        {
            if(!SalvageSite.IsActive||visual==null)return;
            var marker=visual.GetComponent<SalvageAidVisual>();
            if(marker==null)marker=visual.AddComponent<SalvageAidVisual>();
            if(marker._dose==dose)return;
            marker._dose=dose;
            var material=Resources.Load<Material>(dose==2?"Salvage/Tools/AidGreen":"Salvage/Tools/AidRed");
            if(material==null)return;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)if(materials[i]!=null && (materials[i].name.Contains("FirstAid")||materials[i].name.StartsWith("Aid")))materials[i]=material;
                renderer.sharedMaterials=materials;
            }
        }
    }
}
