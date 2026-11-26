using System;

namespace EchoProtocol.Salvage
{
    public static class SalvageCollisionDamage
    {
        // Tuned for Salvage. Public R.E.P.O. material does not specify its private damage coefficients.
        public static int IntegrityLoss(float normalSpeed,float mass,bool fragile,SalvageCargoMaterial material)
        {
            if(!float.IsFinite(normalSpeed)||!float.IsFinite(mass)||normalSpeed<=1.8f||mass<=0)return 0;
            float severity=(normalSpeed-1.8f)*(normalSpeed-1.8f)*(float)Math.Sqrt(Math.Clamp(mass,.5f,20))*.8f;
            severity*=fragile?2.5f:material==SalvageCargoMaterial.Plastic?1.5f:1f;
            return (int)Math.Clamp(Math.Ceiling(severity),1,100);
        }
    }
}
