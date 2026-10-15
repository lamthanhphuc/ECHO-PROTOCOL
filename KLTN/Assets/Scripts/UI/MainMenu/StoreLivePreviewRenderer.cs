using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EchoProtocol.UI.MainMenu
{
    /// <summary>
    /// Shared Store V2 live 3D preview.
    ///
    /// Only one model is rendered at a time.
    /// Character and Team Tool panels share the same RenderTexture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreLivePreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 31;

        private static readonly Vector3 PreviewWorldPosition =
            new Vector3(0f, -10000f, 0f);

        private const int TextureSize = 1024;

        [SerializeField]
        private float rotationSpeed = 18f;
        private bool _dragging;
        private Vector3 _framePosition;
        private Quaternion _frameRotation;
        private bool _frameTransition;
        private float _resumeRotationAt;
        public void DragPreview(Vector2 delta) { _dragging=true; _resumeRotationAt=Time.unscaledTime+2f; if(_spinRoot!=null)_spinRoot.Rotate(0f,-delta.x*0.35f,0f,Space.Self); }
        public void EndPreviewDrag() { _dragging=false; _resumeRotationAt=Time.unscaledTime+2f; }

        private GameObject _rig;
        private Transform _spinRoot;

        private Camera _camera;

        private Light _keyLight;
        private Light _fillLight;
        private Light _rimLight;

        private RenderTexture _renderTexture;

        private GameObject _currentModel;

        private RawImage _target;

        private string _currentResource;

        public void EnsureInitialized()
        {
            if (_rig != null)
                return;

            _rig =
                new GameObject(
                    "[Store Live Preview Rig]");

            _rig.hideFlags =
                HideFlags.DontSave;

            if (gameObject.scene.IsValid()
                && gameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(
                    _rig,
                    gameObject.scene);
            }

            // Keep the complete preview rig physically isolated from
            // the Lobby world. Camera/model/lights all live together
            // here so preview rendering is unchanged.
            _rig.transform.position =
                new Vector3(
                    10000f,
                    10000f,
                    10000f);

            var spinOwner =
                CreateRigObject(
                    "Model Spin");

            _spinRoot =
                spinOwner.transform;

            _spinRoot.SetParent(
                _rig.transform,
                false);

            _spinRoot.localPosition =
                Vector3.zero;

            CreateCamera();
            CreateLights();
            CreateRenderTexture();

            _camera.targetTexture =
                _renderTexture;

            _camera.enabled =
                false;
        }

        public bool Show(
            string resourcePath,
            RawImage target)
        {
            if(StoreReferencePresentation.IsAwaitingSelection(transform)) { if(target!=null){target.texture=null;target.color=Color.clear;} return false; }
            EnsureInitialized();

            if (target == null
                || string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            if(_target!=null && _target!=target){_target.texture=null;_target.color=Color.clear;}
            _target =
                target;
            bool resizedPreview = ResizePreviewTexture();

            _target.texture =
                _renderTexture;

            _target.uvRect =
                new Rect(
                    0f,
                    0f,
                    1f,
                    1f);

            _target.raycastTarget =
                true;
            var drag=_target.GetComponent<StorePreviewDrag>();
            if(drag==null)drag=_target.gameObject.AddComponent<StorePreviewDrag>();
            drag.Renderer=this;

            _target.color =
                Color.white;

            if (_currentModel != null
                && string.Equals(
                    _currentResource,
                    resourcePath,
                    System.StringComparison.Ordinal))
            {
                if(resizedPreview)CenterAndFrameCurrentModel();
                _camera.enabled =
                    true;

                SetPreviewLightsEnabled(
                    true);

                enabled =
                    true;

                return true;
            }

            DestroyCurrentModel();
            _spinRoot.localRotation=Quaternion.identity;
            _dragging=false;
            _resumeRotationAt=Time.unscaledTime+1f;

            var prefab =
                Resources.Load<GameObject>(
                    resourcePath);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] Missing preview prefab: " +
                    resourcePath,
                    this);

                _target.texture =
                    null;

                _target.color =
                    Color.clear;

                _camera.enabled =
                    false;

                return false;
            }

            _currentModel =
                Instantiate(
                    prefab,
                    _spinRoot);

            _currentModel.name =
                "Preview_" +
                prefab.name;

            _currentModel.transform.localPosition =
                Vector3.zero;

            _currentModel.transform.localRotation = resourcePath.StartsWith("Pets/") || resourcePath.Contains("Character_")
                ? Quaternion.Euler(0f,180f,0f) : Quaternion.identity;

            _currentResource =
                resourcePath;

            // STORE_LIVE_PREVIEW_FREEZE_GAMEPLAY
            // Preview prefabs can be real gameplay prefabs. Prevent physics
            // and gameplay scripts from moving individual child objects
            // while the common spin root is rotating.
            PrepareCurrentModelForPreview();

            // STORE_TEAM_TOOL_PREVIEW_STABILIZE
            StabilizeTeamToolPreviewRendering();

            SetLayerRecursively(
                _currentModel,
                PreviewLayer);

            foreach (var animator in
                     _currentModel.GetComponentsInChildren<
                         Animator>(true))
            {
                if (resourcePath.Contains("Character_Jammo") && animator.runtimeAnimatorController == null)
                    animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("Characters/AOC_JammoPlayer");
                if (animator.runtimeAnimatorController != null)
                {
                    animator.enabled=true;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    animator.applyRootMotion=false;
                    animator.Rebind();
                    if(resourcePath.Contains("Character_"))animator.Play("Locomotion",0,0.15f);
                    else if(resourcePath.StartsWith("Pets/"))animator.Play("Idle",0,0.15f);
                    animator.Update(1f/60f);
                }
            }

            if (!CenterAndFrameCurrentModel())
            {
                DestroyCurrentModel();

                _target.texture =
                    null;

                _target.color =
                    Color.clear;

                return false;
            }

            _camera.enabled =
                true;

            SetPreviewLightsEnabled(
                true);

            enabled =
                true;

            return true;
        }

        public bool ApplyMaterial(
            Material material)
        {
            if (_currentModel == null
                || material == null)
            {
                return false;
            }

            var renderers =
                _currentModel
                    .GetComponentsInChildren<SkinnedMeshRenderer>(
                        true);

            if (renderers == null
                || renderers.Length == 0)
            {
                return false;
            }

            bool applied = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer =
                    renderers[i];

                if (renderer == null)
                    continue;

                var materials =
                    renderer.sharedMaterials;

                if (materials == null
                    || materials.Length == 0)
                {
                    continue;
                }

                for (int slot = 0;
                     slot < materials.Length;
                     slot++)
                {
                    materials[slot] =
                        material;
                }

                renderer.sharedMaterials =
                    materials;

                applied = true;
            }

            return applied;
        }

        // STORE_V2_CHARACTER_OUTFIT_MATERIALS
        // Applies Store-only visual variants to the currently rendered
        // Character model without rebuilding the preview rig or touching
        // gameplay material assets.
        public bool ApplyCharacterOutfit(
            int characterId,
            int outfitId)
        {
            if (_currentModel == null)
                return false;

            if (characterId
                == CharacterShopAssets.JammoCharacterId)
            {
                return ApplyJammoOutfit(
                    outfitId);
            }

            if (characterId
                == CharacterShopAssets.AstronautCharacterId)
            {
                return ApplyAstronautOutfit(
                    outfitId);
            }

            return false;
        }

        private bool ApplyJammoOutfit(
            int outfitId)
        {
            int safeId =
                Mathf.Clamp(
                    outfitId,
                    0,
                    3);

            Material replacement =
                Resources.Load<Material>(
                    "Characters/JammoSkins/M_JammoSkin_"
                    + safeId);

            if (replacement == null)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] Missing Jammo outfit material: "
                    + safeId,
                    this);

                return false;
            }

            bool applied =
                false;

            Renderer[] renderers =
                _currentModel.GetComponentsInChildren<Renderer>(
                    true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Material[] materials =
                    renderer.sharedMaterials;

                bool changed =
                    false;

                for (int i = 0;
                     i < materials.Length;
                     i++)
                {
                    Material current =
                        materials[i];

                    if (current == null)
                        continue;

                    string materialName =
                        current.name;

                    // The Jammo preview may start on the original
                    // body material or on a previously selected
                    // Store skin. Both represent the same body slot.
                    bool isJammoBody =
                        materialName.Contains(
                            "M_Jammo_m_jammo_metal")
                        || materialName.Contains(
                            "M_JammoSkin_");

                    if (!isJammoBody)
                        continue;

                    materials[i] =
                        replacement;

                    changed =
                        true;
                }

                if (!changed)
                    continue;

                renderer.sharedMaterials =
                    materials;

                applied =
                    true;
            }

            if (!applied)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] No Jammo body material slot found "
                    + "for outfit "
                    + safeId
                    + ".",
                    this);
            }

            return applied;
        }

        private bool ApplyAstronautOutfit(
            int outfitId)
        {
            string[] variants =
            {
                "M_PF_PlayerCharacter_P1_Default",
                "M_PF_PlayerCharacter_P2_Orange",
                "M_PF_PlayerCharacter_P3_Green",
                "M_PF_PlayerCharacter_P4_Purple"
            };

            int safeId =
                Mathf.Clamp(
                    outfitId,
                    0,
                    variants.Length - 1);

            string root =
                "Shop/LivePreview/CharacterOutfits/Astronaut/"
                + variants[safeId];

            Material suit =
                Resources.Load<Material>(
                    root + "_Suit");

            Material equipment =
                Resources.Load<Material>(
                    root + "_HelmetBackpackBase");

            Material visor =
                Resources.Load<Material>(
                    root + "_Visor");

            if (suit == null
                || equipment == null
                || visor == null)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] Missing Astronaut outfit materials: "
                    + safeId,
                    this);

                return false;
            }

            bool applied =
                false;

            Renderer[] renderers =
                _currentModel.GetComponentsInChildren<Renderer>(
                    true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Material[] materials =
                    renderer.sharedMaterials;

                bool changed =
                    false;

                for (int i = 0;
                     i < materials.Length;
                     i++)
                {
                    Material current =
                        materials[i];

                    if (current == null)
                        continue;

                    string materialName =
                        current.name;

                    if (materialName.IndexOf(
                            "_HelmetBackpackBase",
                            System.StringComparison.OrdinalIgnoreCase)
                        >= 0)
                    {
                        materials[i] =
                            equipment;

                        changed =
                            true;
                    }
                    else if (materialName.IndexOf(
                                 "_Suit",
                                 System.StringComparison.OrdinalIgnoreCase)
                             >= 0)
                    {
                        materials[i] =
                            suit;

                        changed =
                            true;
                    }
                    else if (materialName.IndexOf(
                                 "_Visor",
                                 System.StringComparison.OrdinalIgnoreCase)
                             >= 0)
                    {
                        materials[i] =
                            visor;

                        changed =
                            true;
                    }
                }

                if (!changed)
                    continue;

                renderer.sharedMaterials =
                    materials;

                applied =
                    true;
            }

            return applied;
        }

        public void Clear(
            RawImage target = null)
        {
            if (target != null)
            {
                target.texture =
                    null;

                target.color =
                    Color.clear;
            }

            if (_target == target
                || target == null)
            {
                _target =
                    null;
            }

            DestroyCurrentModel();

            if (_camera != null)
            {
                _camera.enabled =
                    false;
            }

            SetPreviewLightsEnabled(
                false);
        }

        private void LateUpdate()
        {
            if (_currentModel == null
                || _spinRoot == null)
            {
                return;
            }

            if (ResizePreviewTexture()) CenterAndFrameCurrentModel();
            if(_frameTransition) {
                float blend=1f-Mathf.Exp(-18f*Time.unscaledDeltaTime);
                _camera.transform.position=Vector3.Lerp(_camera.transform.position,_framePosition,blend);
                _camera.transform.rotation=Quaternion.Slerp(_camera.transform.rotation,_frameRotation,blend);
                if((_camera.transform.position-_framePosition).sqrMagnitude<0.000001f && Quaternion.Angle(_camera.transform.rotation,_frameRotation)<0.05f) {
                    _camera.transform.SetPositionAndRotation(_framePosition,_frameRotation);
                    _frameTransition=false;
                }
            }
            if(_dragging || Time.unscaledTime<_resumeRotationAt)return;
            _spinRoot.Rotate(
                0f,
                rotationSpeed *
                Time.unscaledDeltaTime,
                0f,
                Space.Self);
        }

        private void PrepareCurrentModelForPreview()
        {
            if (_currentModel == null)
                return;

            // Gameplay logic must never drive the Store preview.
            foreach (MonoBehaviour behaviour in
                     _currentModel.GetComponentsInChildren<
                         MonoBehaviour>(
                             true))
            {
                if (behaviour == null)
                    continue;

                behaviour.enabled =
                    false;
            }

            // Physics on a real pickup/tool prefab can make separate
            // pieces drift while the Store rotates the common parent.
            foreach (Rigidbody body in
                     _currentModel.GetComponentsInChildren<
                         Rigidbody>(
                             true))
            {
                if (body == null)
                    continue;

                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;

                body.useGravity =
                    false;

                body.detectCollisions =
                    false;

                body.isKinematic =
                    true;
            }

            foreach (Collider collider in
                     _currentModel.GetComponentsInChildren<
                         Collider>(
                             true))
            {
                if (collider != null)
                {
                    collider.enabled =
                        false;
                }
            }
        }
        private void StabilizeTeamToolPreviewRendering()
        {
            if (_currentModel == null)
                return;

            if (string.IsNullOrEmpty(
                    _currentResource))
            {
                return;
            }

            // Character/Pet previews are already correct.
            // Restrict this rendering workaround to Team Tools.
            if (!_currentResource.StartsWith(
                    "Shop/LivePreview/Tool_"))
            {
                return;
            }

            Renderer[] renderers =
                _currentModel
                    .GetComponentsInChildren<
                        Renderer>(
                            true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                // Store preview does not need real-world shadows.
                renderer.shadowCastingMode =
                    UnityEngine.Rendering
                        .ShadowCastingMode
                        .Off;

                renderer.receiveShadows =
                    false;

                // Use runtime-only material instances.
                // The real gameplay material asset is NOT modified.
                Material[] materials =
                    renderer.materials;

                foreach (Material material in materials)
                {
                    if (material == null)
                        continue;

                    // URP/Lit imported meshes can contain thin or
                    // reversed faces. When rotating, back-face
                    // culling can make parts visually disappear.
                    if (material.HasProperty("_Cull"))
                    {
                        material.SetFloat(
                            "_Cull",
                            0f);
                    }

                    if (material.HasProperty("_AlphaClip"))
                    {
                        material.SetFloat(
                            "_AlphaClip",
                            0f);
                    }

                    if (material.HasProperty("_Surface"))
                    {
                        material.SetFloat(
                            "_Surface",
                            0f);
                    }

                    material.renderQueue =
                        2000;

                    material.enableInstancing =
                        false;
                }
            }
        }
        private static Bounds PreviewBounds(Renderer renderer)
        {
            if(!(renderer is SkinnedMeshRenderer skin))return renderer.bounds;
            var mesh=new Mesh();
            skin.BakeMesh(mesh,false);mesh.RecalculateBounds();
            var local=mesh.bounds;
            var imported=renderer.bounds;
            Bounds scaled=TransformBounds(local,skin.transform.localToWorldMatrix);
            Bounds unscaled=TransformBounds(local,Matrix4x4.TRS(skin.transform.position,skin.transform.rotation,Vector3.one));
            DestroyPreviewObject(mesh);
            // Imported parent scale can already be included in baked vertices. Choose the
            // coordinate interpretation consistent with the renderer instead of multiplying it twice.
            float Score(Bounds value) => Mathf.Abs(Mathf.Log(Mathf.Max(0.0001f,value.size.magnitude)
                / Mathf.Max(0.0001f,imported.size.magnitude)))
                + Vector3.Distance(value.center,imported.center)/Mathf.Max(0.1f,imported.extents.magnitude);
            return Score(scaled)<=Score(unscaled) ? scaled : unscaled;
        }
        private static Bounds TransformBounds(Bounds local,Matrix4x4 matrix)
        {
            var world=new Bounds(matrix.MultiplyPoint3x4(local.center),Vector3.zero);
            for(int i=0;i<8;i++)world.Encapsulate(matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,
                new Vector3((i&1)==0 ? -1 : 1,(i&2)==0 ? -1 : 1,(i&4)==0 ? -1 : 1))));
            return world;
        }

        private bool CenterAndFrameCurrentModel()
        {
            var renderers =
                _currentModel.GetComponentsInChildren<
                    Renderer>(true);

            bool found =
                false;

            Bounds bounds =
                default;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!found)
                {
                    bounds =
                        PreviewBounds(renderer);

                    found =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        PreviewBounds(renderer));
                }
            }

            if (!found)
            {
                Debug.LogWarning(
                    "[StoreLivePreview] Preview model contains no visible renderer.",
                    this);

                return false;
            }

            // Move model geometry so its visual center sits on
            // the spin pivot. This produces natural rotation
            // even when the source prefab pivot is at the feet.
            Vector3 centerDelta =
                _spinRoot.position -
                bounds.center;

            _currentModel.transform.position +=
                centerDelta;

            // Recalculate after centering.
            renderers =
                _currentModel.GetComponentsInChildren<
                    Renderer>(true);

            found =
                false;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!found)
                {
                    bounds =
                        PreviewBounds(renderer);

                    found =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        PreviewBounds(renderer));
                }
            }

            if (!found)
                return false;

            float radius = Mathf.Max(0.15f, bounds.extents.magnitude);
            Vector3 viewDirection = (_currentResource != null && _currentResource.Contains("Character_")
                ? new Vector3(0.04f,0.02f,-1f) : new Vector3(0.32f,0.10f,-1f)).normalized;
            Vector3 targetPoint = _spinRoot.position + Vector3.up * bounds.size.y * 0.015f;
            Quaternion view = Quaternion.LookRotation(-viewDirection,Vector3.up);
            Vector3 right=view*Vector3.right, up=view*Vector3.up, forward=view*Vector3.forward;
            float fill = _currentResource != null && _currentResource.Contains("Tool_") ? 0.72f : 0.92f;
            float tangent=Mathf.Tan(_camera.fieldOfView*0.5f*Mathf.Deg2Rad);
            float distance=0.1f;
            // Fit each visible mesh in camera space. Deep tails and wide rest bounds no longer
            // force a small body, and long tools retain their own proportions.
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
                var meshBounds=PreviewBounds(renderer);
                for(int i=0;i<8;i++)
                {
                    Vector3 point=meshBounds.center+Vector3.Scale(meshBounds.extents,
                        new Vector3((i&1)==0 ? -1 : 1,(i&2)==0 ? -1 : 1,(i&4)==0 ? -1 : 1))-targetPoint;
                    float depth=Vector3.Dot(point,forward);
                    distance=Mathf.Max(distance,Mathf.Abs(Vector3.Dot(point,up))/(tangent*fill)-depth);
                    distance=Mathf.Max(distance,Mathf.Abs(Vector3.Dot(point,right))/(tangent*_camera.aspect*fill)-depth);
                }
            }

            _framePosition=targetPoint+viewDirection*distance;
            _frameRotation=Quaternion.LookRotation(targetPoint-_framePosition,Vector3.up);
            _frameTransition=Application.isPlaying && _camera.enabled;
            if(!_frameTransition)_camera.transform.SetPositionAndRotation(_framePosition,_frameRotation);
            // Include the previous camera distance while blending so the new model
            // cannot disappear against a near/far plane during the transition.
            _camera.nearClipPlane=0.01f;
            _camera.farClipPlane=Mathf.Max(distance,Vector3.Distance(_camera.transform.position,targetPoint))+radius*4f;

            return true;
        }

        private void CreateCamera()
        {
            var owner =
                CreateRigObject(
                    "Preview Camera",
                    typeof(Camera));

            owner.transform.SetParent(
                _rig.transform,
                false);

            _camera =
                owner.GetComponent<Camera>();

            _camera.enabled =
                false;

            _camera.clearFlags =
                CameraClearFlags.SolidColor;

            _camera.backgroundColor = Color.clear;

            _camera.fieldOfView =
                28f;

            _camera.cullingMask =
                1 << PreviewLayer;

            _camera.allowHDR =
                true;

            _camera.allowMSAA =
                true;
        }

        private void CreateLights()
        {
            _keyLight =
                CreatePreviewPointLight(
                    "Key Light",
                    new Vector3(
                        2.4f,
                        2.8f,
                        -2.8f),
                    18.8f,
                    10f,
                    true);

            _fillLight =
                CreatePreviewPointLight(
                    "Fill Light",
                    new Vector3(
                        -2.6f,
                        1.5f,
                        -1.8f),
                    12.8f,
                    10f,
                    false);

            _rimLight =
                CreatePreviewPointLight(
                    "Rim Light",
                    new Vector3(
                        0.5f,
                        2.8f,
                        2.8f),
                    8.4f,
                    10f,
                    false);

            SetPreviewLightsEnabled(false);
        }

        private Light CreateDirectionalLight(
            string name,
            Vector3 rotation,
            float intensity)
        {
            // Kept only for source compatibility.
            // Store preview must never use an infinite Directional Light.
            return CreatePreviewPointLight(
                name,
                Vector3.zero,
                intensity,
                10f,
                false);
        }

        private Light CreatePreviewPointLight(
            string name,
            Vector3 localPosition,
            float intensity,
            float range,
            bool shadows)
        {
            var owner =
                CreateRigObject(
                    name,
                    typeof(Light));

            owner.transform.SetParent(
                _rig.transform,
                false);

            owner.transform.localPosition =
                localPosition;

            var light =
                owner.GetComponent<Light>();

            light.type =
                LightType.Point;

            light.intensity =
                intensity;

            light.range =
                range;

            light.shadows =
                shadows
                    ? LightShadows.Soft
                    : LightShadows.None;

            // Standard GameObject-layer isolation.
            light.cullingMask =
                1 << PreviewLayer;

            // SRP/URP rendering-layer isolation.
            // Bit 7 is reserved locally for Store preview rendering.
            light.renderingLayerMask =
                1 << 7;
            var additional = light.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (additional == null)
                additional = light.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            additional.renderingLayers = 1u << 7;

            light.enabled =
                false;

            return light;
        }

        private void SetPreviewLightsEnabled(
            bool value)
        {
            // Includes the front fill created by StoreV2PreviewLighting, so closing
            // the preview disables every shop light immediately.
            if (_rig != null)
                foreach (var previewLight in _rig.GetComponentsInChildren<Light>(true))
                    previewLight.enabled = value;

            if (_keyLight != null)
                _keyLight.enabled = value;

            if (_fillLight != null)
                _fillLight.enabled = value;

            if (_rimLight != null)
                _rimLight.enabled = value;
        }

        private bool ResizePreviewTexture()
        {
            if (_target == null || _camera == null) return false;
            var rect = _target.rectTransform.rect;
            if (rect.width <= 1f || rect.height <= 1f) return false;
            float ratio = rect.width / rect.height;
            int width = ratio >= 1f ? TextureSize : Mathf.Max(128, Mathf.RoundToInt(TextureSize * ratio));
            int height = ratio >= 1f ? Mathf.Max(128, Mathf.RoundToInt(TextureSize / ratio)) : TextureSize;
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height) return false;
            _camera.targetTexture = null;
            if (_renderTexture != null) { _renderTexture.Release(); DestroyPreviewObject(_renderTexture); }
            _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { name = "StoreLivePreviewRT", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _renderTexture.Create();
            var previousTarget=RenderTexture.active;
            RenderTexture.active=_renderTexture;
            GL.Clear(true,true,Color.clear);
            RenderTexture.active=previousTarget;
            _camera.targetTexture = _renderTexture;
            _camera.aspect = ratio;
            _target.texture = _renderTexture;
            return true;
        }

        private void CreateRenderTexture()
        {
            _renderTexture =
                new RenderTexture(
                    TextureSize,
                    TextureSize,
                    24,
                    RenderTextureFormat.ARGB32);

            _renderTexture.name =
                "StoreLivePreviewRT";

            _renderTexture.useMipMap =
                false;

            _renderTexture.autoGenerateMips =
                false;

            _renderTexture.antiAliasing =
                1;

            _renderTexture.filterMode =
                FilterMode.Bilinear;

            _renderTexture.wrapMode =
                TextureWrapMode.Clamp;

            _renderTexture.Create();
            var previousTarget=RenderTexture.active;
            RenderTexture.active=_renderTexture;
            GL.Clear(true,true,Color.clear);
            RenderTexture.active=previousTarget;
        }

        private GameObject CreateRigObject(
            string name,
            params System.Type[] components)
        {
            var owner =
                new GameObject(
                    name,
                    components);

            owner.hideFlags =
                HideFlags.DontSave;

            if (gameObject.scene.IsValid()
                && gameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(
                    owner,
                    gameObject.scene);
            }

            owner.layer =
                PreviewLayer;

            return owner;
        }

        private void DestroyCurrentModel()
        {
            _currentResource =
                null;

            if (_currentModel == null)
                return;

            _currentModel.SetActive(
                false);

            DestroyPreviewObject(
                _currentModel);

            _currentModel =
                null;

            if (_spinRoot != null)
            {
                _spinRoot.localRotation =
                    Quaternion.identity;
            }
        }

        private static void SetLayerRecursively(
            GameObject root,
            int layer)
        {
            root.layer =
                layer;

            var renderer =
                root.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.renderingLayerMask =
                    1u << 7;
            }

            foreach (Transform child
                     in root.transform)
            {
                SetLayerRecursively(
                    child.gameObject,
                    layer);
            }
        }

        private void OnEnable()
        {
            bool activePreview =
                _camera != null
                && _currentModel != null
                && _target != null;

            if (_camera != null)
            {
                _camera.enabled =
                    activePreview;
            }

            SetPreviewLightsEnabled(
                activePreview);
        }

        private void OnDisable()
        {
            _frameTransition=false;
            if (_camera != null)
            {
                _camera.enabled =
                    false;
            }

            SetPreviewLightsEnabled(
                false);
        }

        private void OnDestroy()
        {
            DestroyCurrentModel();

            if (_renderTexture != null)
            {
                // Detach the RT before releasing it. This prevents
                // stale/invalid camera output when leaving or rebuilding
                // the Store preview.
                if (_camera != null &&
                    _camera.targetTexture == _renderTexture)
                {
                    _camera.targetTexture =
                        null;
                }

                if (_target != null &&
                    _target.texture == _renderTexture)
                {
                    _target.texture =
                        null;
                }

                _renderTexture.Release();

                DestroyPreviewObject(
                    _renderTexture);

                _renderTexture =
                    null;
            }

            if (_rig != null)
            {
                DestroyPreviewObject(
                    _rig);

                _rig =
                    null;
            }
        }

        private static void DestroyPreviewObject(
            Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}

