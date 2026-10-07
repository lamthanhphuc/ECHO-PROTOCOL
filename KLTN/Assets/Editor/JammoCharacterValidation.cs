using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class JammoCharacterValidation
{
    public static void Validate()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>("Assets/Resources/Characters/AOC_JammoPlayer.overrideController");
        if (controller == null) throw new Exception("Missing Jammo override.");
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        int replaced = 0;
        foreach (var pair in overrides)
        {
            string path = AssetDatabase.GetAssetPath(pair.Key);
            bool expected = path == "Assets/Animations/Player/Idle.fbx" ||
                path == "Assets/Animations/Player/Walking.fbx" || path == "Assets/Animations/Player/Fast Run.fbx" ||
                path == "Assets/Resources/Characters/a_Idle_Jammo_Humanoid.anim" ||
                path == "Assets/Resources/Characters/a_Walking_Jammo_Humanoid.anim" ||
                path == "Assets/Resources/Characters/a_Running_Jammo_Humanoid.anim";
            if (expected)
            {
                var locomotion = pair.Value != null ? pair.Value : pair.Key;
                if (!locomotion.humanMotion || locomotion.length <= 0f)
                    throw new Exception("Invalid Humanoid locomotion override: " + path);
                bool hasMotion = false;
                foreach (var binding in AnimationUtility.GetCurveBindings(locomotion))
                {
                    var curve = AnimationUtility.GetEditorCurve(locomotion, binding);
                    if (Mathf.Abs(curve.Evaluate(0f) - curve.Evaluate(locomotion.length * 0.25f)) > 0.001f) hasMotion = true;
                }
                int fingerBindings = AnimationUtility.GetCurveBindings(locomotion).Count(binding => binding.propertyName.StartsWith("LeftHand.") || binding.propertyName.StartsWith("RightHand."));
                if (fingerBindings != 40) throw new Exception("Invalid finger bindings on " + path);
                if (!hasMotion) throw new Exception("Humanoid conversion lost source motion: " + path);
                replaced++;
            }
            else if (pair.Value != null) throw new Exception("Unexpected override of " + path);
        }
        if (replaced != 3) throw new Exception("Expected three overrides.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
        var instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var presenter = instance.GetComponent<PlayerCharacterPresenter>();
            presenter.InitializeVisualRig();
            var animator = instance.GetComponentInChildren<Animator>();
            var handAnchor = instance.GetComponent<PlayerHeldItemAnchor>().RightHandAnchor;
            var coreAnchor = instance.GetComponent<PlayerHeldItemAnchor>().CoreCarryAnchor;
            var originalAvatar = animator.avatar;
            var originalController = animator.runtimeAnimatorController;
            var state = new SerializedObject(presenter);
            state.FindProperty("offlineCharacterId").intValue = 1;
            state.ApplyModifiedPropertiesWithoutUndo();
            presenter.Refresh();
            if (animator.avatar == originalAvatar || !animator.isHuman || !animator.avatar.isValid)
                throw new Exception("Jammo rig did not bind.");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || !hips.name.Contains("mixamorig:")) throw new Exception("Jammo bones are not bound.");
            animator.Update(0.05f);
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (foot.position.y < -0.05f || hips.position.y < 0.5f) throw new Exception("Jammo animation sinks below the floor.");
            if (handAnchor.parent != animator.GetBoneTransform(HumanBodyBones.RightHand) || Mathf.Abs(handAnchor.lossyScale.x - 1f) > 0.01f || Mathf.Abs(coreAnchor.lossyScale.x - 1f) > 0.01f) throw new Exception("Carry anchors were not rebound at world scale one.");
            Debug.Log("[Jammo validation] Hips=" + hips.position + "; left foot=" + foot.position);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.Contains("FirstPerson")) Debug.Log("[Jammo validation] " + renderer.name + " parent=" + renderer.transform.parent.name + " active=" + renderer.gameObject.activeInHierarchy);
            foreach (float speed in new[] { 0f, 0.5f, 1f })
            {
                animator.SetFloat("Speed", speed);
                animator.SetFloat("MoveX", 0f);
                animator.SetFloat("MoveY", speed == 0f ? 0f : 1f);
                animator.SetBool("IsMoving", speed > 0f);
                animator.SetBool("IsSprinting", speed == 1f);
                for (int frame = 0; frame <= 30; frame++)
                {
                    animator.Play(speed == 1f ? "Base Layer.Run Forward" : "Base Layer.Locomotion", 0, frame / 30f);
                    animator.Update(0.001f);
                    if (Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y) < -0.05f)
                        throw new Exception("Locomotion puts feet below the floor at speed " + speed);
                }
            }
            if (Quaternion.Angle(handAnchor.localRotation, presenter.RightHandGripCorrection) > 0.01f) throw new Exception("Missing Jammo tool grip correction.");
            foreach (bool left in new[] { false, true })
            {
                var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                foreach (var aim in new[] { Vector3.forward, new Vector3(0.3f, -0.6f, 1f).normalized })
                {
                    presenter.ApplyJammoGrip(hand, left, aim, Vector3.up);
                    if (Vector3.Dot((middle.position - hand.position).normalized, aim) < 0.99f) throw new Exception("Jammo grip fingers do not follow aim.");
                }
            }
            state.Update();
            state.FindProperty("offlineCharacterId").intValue = 0;
            state.ApplyModifiedPropertiesWithoutUndo();
            presenter.Refresh();
            if (animator.avatar != originalAvatar || animator.runtimeAnimatorController != originalController)
                throw new Exception("Original rig was not restored.");
            Debug.Log("[Jammo validation] Three overrides only; Humanoid rig binding and return to original character passed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
}

