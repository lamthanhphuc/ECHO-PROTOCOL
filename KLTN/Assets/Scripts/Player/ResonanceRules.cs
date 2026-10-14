using UnityEngine;

namespace EchoProtocol.Networking
{
    public struct ResonanceEnergyState
    {
        public float Energy, RechargeDelay;
        public bool Active, Locked, NeedsRelease;
    }
    public struct ResonanceExposureState { public float Seconds, Grace; }
    public static class ResonanceRules
    {
        public static float ContributionSpeed(int players) => players<=0 ? 0f : players==1 ? 1f : players==2 ? 1.5f : players==3 ? 1.75f : 1.9f;
        public static ResonanceExposureState TickExposure(ResonanceExposureState state,int contributors,float delta)
        {
            if(contributors>0){state.Seconds=Mathf.Min(2f,state.Seconds+delta*ContributionSpeed(contributors));state.Grace=.5f;}
            else {state.Seconds=Mathf.Max(0f,state.Seconds-Mathf.Max(0f,delta-state.Grace));state.Grace=Mathf.Max(0f,state.Grace-delta);}
            return state;
        }
        public static ResonanceEnergyState Tick(ResonanceEnergyState state, bool held, bool allowed, float delta)
        {
            if(!held)state.NeedsRelease=false;
            if(state.Energy>=20f-.001f && !state.NeedsRelease)state.Locked=false;
            bool active=held && allowed && !state.Locked && !state.NeedsRelease && state.Energy>0f;
            if(state.Active && !active)state.RechargeDelay=1.5f;
            state.Active=active;
            if(active) {
                state.Energy=Mathf.Max(0f,state.Energy-20f*delta);
                if(state.Energy<=0f){state.Active=false;state.Locked=true;state.NeedsRelease=true;state.RechargeDelay=1.5f;}
            } else {
                float rechargeTime=Mathf.Max(0f,delta-state.RechargeDelay);
                state.RechargeDelay=Mathf.Max(0f,state.RechargeDelay-delta);
                state.Energy=Mathf.Min(100f,state.Energy+25f*rechargeTime);
            }
            return state;
        }
    }
}
