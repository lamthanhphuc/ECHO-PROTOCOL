using System.Globalization;
using TMPro;
using UnityEngine;

namespace EchoProtocol.Salvage
{
    // Temporary world-space price; never a permanent HUD marker.
    [DefaultExecutionOrder(310)]
    public sealed class SalvageValuePopup : MonoBehaviour
    {
        private Transform _follow;
        private SalvageScrap _scrap;
        private Vector3 _offset;
        private TextMeshPro _text,_lossText;
        private float _expires,_refreshed=-100;
        private int _loss;
        private SalvageRunState _run;
        public int CargoSerial { get; private set; }
        public static SalvageValuePopup Show(Vector3 point,int value,int loss=0,Transform follow=null,int serial=0)
        {
            // A reliable loss RPC may arrive after the cargo despawn. Reuse its surviving receipt.
            if(serial>0)foreach(var existing in FindObjectsByType<SalvageValuePopup>())
                if(existing.CargoSerial==serial&&existing._run==SalvageRunState.Instance)
                {existing.Refresh(value,loss);return existing;}
            var go=new GameObject("Salvage temporary cargo value");var popup=go.AddComponent<SalvageValuePopup>();
            popup.CargoSerial=serial;popup._run=SalvageRunState.Instance;
            popup._text=popup.Text("Current price",go.transform,.95f);
            popup._lossText=popup.Text("Confirmed loss",go.transform,.65f);
            popup._lossText.transform.localPosition=new Vector3(0,-.078f,-.008f);
            popup._text.transform.localPosition=new Vector3(0,.015f,-.012f);
            go.transform.position=point;popup._follow=follow;popup._scrap=follow!=null?follow.GetComponent<SalvageScrap>():null;
            popup._offset=follow!=null?point-follow.position:Vector3.zero;popup.Refresh(value,loss);return popup;
        }
        private TextMeshPro Text(string label,Transform parent,float size)
        {
            var go=new GameObject(label);go.transform.SetParent(parent,false);var text=go.AddComponent<TextMeshPro>();
            text.font=Resources.Load<TMP_FontAsset>("UI/MainMenuTypography/OswaldTMP")??TMP_Settings.defaultFontAsset;
            text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.fontStyle=FontStyles.Bold;
            text.rectTransform.sizeDelta=new Vector2(.23f,.11f);text.textWrappingMode=TextWrappingModes.NoWrap;
            text.enableAutoSizing=true;text.fontSizeMin=.42f;text.fontSizeMax=size;
            text.raycastTarget=false;text.outlineWidth=.1f;text.outlineColor=new Color32(7,15,12,255);
            return text;
        }
        public void Refresh(int value,int loss)
        {
            value=Mathf.Max(0,value);loss=Mathf.Max(0,loss);
            _loss=Time.unscaledTime-_refreshed<.4f?_loss+loss:loss;
            _text.text=value.ToString("N0",CultureInfo.InvariantCulture)+"<size=55%> RV</size>";
            _text.color=new Color(.3f,1,.36f);
            _lossText.text=_loss>0?"−"+_loss.ToString("N0",CultureInfo.InvariantCulture)+" RV":"";
            _lossText.color=new Color(1,.68f,.35f);_refreshed=Time.unscaledTime;_expires=_refreshed+3;
            if(value==0){_scrap=null;_follow=null;}
        }
        private void LateUpdate()
        {
            float remaining=_expires-Time.unscaledTime;if(remaining<=0){Destroy(gameObject);return;}
            var camera=SalvagePresentation.ViewCamera;if(camera==null)return;
            if(_scrap!=null)
            {
                if(_scrap.TryPresentationCargo(out var cargo)&&cargo.State==SalvageCargoState.Held){Destroy(gameObject);return;}
                transform.position=_scrap.GripPoint+camera.transform.up*.27f;
            }
            else if(_follow!=null)transform.position=_follow.position+_offset;
            transform.rotation=camera.transform.rotation;
            float distance=Vector3.Distance(transform.position,camera.transform.position);
            float scale=Mathf.Clamp(distance*.6f,.7f,5),age=Time.unscaledTime-_refreshed;
            float pop=PlayerCameraSettings.ReduceMotion?1:1+Mathf.Sin(Mathf.Clamp01(age/.22f)*Mathf.PI)*.12f;
            transform.localScale=Vector3.one*scale*pop;
            float fade=Mathf.Clamp01(remaining/.45f)*Mathf.Clamp01(age/.08f);
            bool visible=distance<12&&camera.WorldToViewportPoint(transform.position).z>.1f&&!PlayerInteractionControlLock.HasModal;
            _text.enabled=visible;_lossText.enabled=visible;
            _text.alpha=fade;_lossText.alpha=fade*Mathf.Clamp01((2-age)/.4f);
        }
    }
}
