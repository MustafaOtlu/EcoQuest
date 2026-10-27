using System.Collections.Generic;
using UnityEngine;

/// <summary>Retains the existing scene component reference while mounting tools to the animated right hand.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class FirstPersonWeaponHolder : MonoBehaviour
{
    [SerializeField, HideInInspector] private Transform viewTransform;
    [SerializeField] private Transform equippedWeapon;
    [Header("Character grip")]
    [Range(0f, 1f)] public float gripWeight = 1f;
    [Min(0.1f)] public float poseResponse = 12f;
    public bool IsHandMounted => rightHand != null && transform.parent == rightHand;
    public Transform EquippedWeapon => equippedWeapon;
    public Vector3 MuzzlePosition => IsHandMounted && equippedWeapon != null ? transform.TransformPoint(muzzleLocal) : transform.position;
    public Vector3 ToolForward => transform.forward;

    private PlayerController player;
    private PlayerWeaponSystem weapons;
    private Animator animator;
    private Transform rightArm, rightForearm, rightHand, leftArm, leftForearm, leftHand;
    private Transform rightMiddle, rightIndex, rightPinky, leftMiddle, leftIndex, leftPinky;
    private Quaternion rightPalmFrame, leftPalmFrame;
    private Vector3 muzzleLocal = new Vector3(0, .06f, .32f);
    private float weight;
    private int toolIndex;
    private readonly Dictionary<Transform, Grip> grips = new Dictionary<Transform, Grip>();
    private readonly List<Finger> fingers = new List<Finger>();

    private sealed class Grip { public Vector3 position, scale, muzzle; public Quaternion rotation; }
    private sealed class Finger { public Transform bone; public Quaternion rest; public float curl; }

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        weapons = GetComponent<PlayerWeaponSystem>();
    }

    public bool BindToCharacter(PlayerController owner, PlayerWeaponSystem source)
    {
        if (owner == null) return false;
        player = owner; weapons = source;
        animator = owner.characterAnimator != null ? owner.characterAnimator : owner.GetComponentInChildren<Animator>(true);
        if (animator == null) return false;
        rightArm = Bone(HumanBodyBones.RightUpperArm, "RightArm");
        rightForearm = Bone(HumanBodyBones.RightLowerArm, "RightForeArm");
        rightHand = Bone(HumanBodyBones.RightHand, "RightHand");
        leftArm = Bone(HumanBodyBones.LeftUpperArm, "LeftArm");
        leftForearm = Bone(HumanBodyBones.LeftLowerArm, "LeftForeArm");
        leftHand = Bone(HumanBodyBones.LeftHand, "LeftHand");
        if (rightArm == null || rightForearm == null || rightHand == null) return false;
        rightMiddle = Bone(HumanBodyBones.RightMiddleProximal, "RightHandMiddle1");
        rightIndex = Bone(HumanBodyBones.RightIndexProximal, "RightHandIndex1");
        rightPinky = Bone(HumanBodyBones.RightLittleProximal, "RightHandPinky1");
        leftMiddle = Bone(HumanBodyBones.LeftMiddleProximal, "LeftHandMiddle1");
        leftIndex = Bone(HumanBodyBones.LeftIndexProximal, "LeftHandIndex1");
        leftPinky = Bone(HumanBodyBones.LeftLittleProximal, "LeftHandPinky1");
        rightPalmFrame = PalmFrame(rightHand, rightMiddle, rightIndex, rightPinky, true);
        if (leftHand != null) leftPalmFrame = PalmFrame(leftHand, leftMiddle, leftIndex, leftPinky, false);
        CacheFingers();
        transform.SetParent(rightHand, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        viewTransform = null;
        weight = 0;
        if (equippedWeapon != null) Equip(equippedWeapon, toolIndex);
        return true;
    }

    private Transform Bone(HumanBodyBones id, string suffix)
    {
        if (animator.isHuman && animator.avatar != null && animator.avatar.isValid)
        {
            var value = animator.GetBoneTransform(id);
            if (value != null) return value;
        }
        foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            if (bone.name == suffix || bone.name.EndsWith(":" + suffix)) return bone;
        return null;
    }

    private static Quaternion PalmFrame(Transform hand, Transform middle, Transform index, Transform little, bool right)
    {
        if (hand == null || middle == null || index == null || little == null) return Quaternion.identity;
        Vector3 fingers = hand.InverseTransformDirection(middle.position - hand.position).normalized;
        Vector3 across = hand.InverseTransformDirection(index.position - little.position).normalized;
        Vector3 normal = Vector3.Cross(fingers, across).normalized * (right ? 1f : -1f);
        if (normal.sqrMagnitude < .1f) return Quaternion.identity;
        return Quaternion.LookRotation(fingers, normal);
    }

    private void CacheFingers()
    {
        if (fingers.Count > 0) return;
        foreach (var t in animator.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.Contains("Hand")) continue;
            bool thumb = t.name.Contains("Thumb");
            bool index = t.name.Contains("Index");
            int joint = t.name.EndsWith("1") ? 1 : t.name.EndsWith("2") ? 2 : t.name.EndsWith("3") ? 3 : 0;
            if (joint == 0 || thumb && joint == 1) continue;
            float curl = thumb ? 12f : index ? (joint == 1 ? 18f : 32f) : (joint == 1 ? 34f : 48f);
            fingers.Add(new Finger { bone = t, rest = t.localRotation, curl = curl });
        }
    }

    public void Unequip()
    {
        if (equippedWeapon != null) equippedWeapon.gameObject.SetActive(false);
        equippedWeapon = null;
    }

    public void Equip(Transform weapon) => Equip(weapon, weapons != null ? (int)weapons.Selected : 0);

    public void Equip(Transform weapon, int index)
    {
        if (weapon == null || weapon == transform || transform.IsChildOf(weapon)) return;
        if (equippedWeapon != null && equippedWeapon != weapon) equippedWeapon.gameObject.SetActive(false);
        equippedWeapon = weapon; toolIndex = Mathf.Clamp(index, 0, PlayerWeaponSystem.ToolCount - 1);
        weapon.SetParent(transform, false);
        if (!grips.TryGetValue(weapon, out var grip))
        {
            weapon.localPosition = Vector3.zero;
            weapon.localRotation = toolIndex == (int)PlayerWeaponSystem.Tool.Flashlight ? Quaternion.identity : Quaternion.Euler(-90f, -90f, 0f);
            Bounds bounds;
            if (!TryLocalBounds(weapon, out bounds))
            { weapon.gameObject.SetActive(false); equippedWeapon = null; return; }
            float length = toolIndex == (int)PlayerWeaponSystem.Tool.Flashlight ? .26f : toolIndex == 3 ? .21f : toolIndex == 2 ? .30f : toolIndex == 1 ? .40f : .46f;
            weapon.localScale *= length / Mathf.Max(.001f, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));
            TryLocalBounds(weapon, out bounds);
            // Place the palm on the rear lower handle, rather than on the model's geometric centre.
            // The vacuum (EcoVacuumTool) is held by its rear carry handle, level with the nozzle.
            float gripHeight = toolIndex == (int)PlayerWeaponSystem.Tool.Flashlight ? .5f : toolIndex == 0 ? .44f : toolIndex == 1 ? .10f : toolIndex == 2 ? .80f : .18f;
            float gripDepth = toolIndex == (int)PlayerWeaponSystem.Tool.Flashlight ? .28f : toolIndex == 0 ? .05f : toolIndex == 1 ? .58f : toolIndex == 2 ? .45f : toolIndex == 3 ? .35f : .24f;
            var palm = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, gripHeight),
                Mathf.Lerp(bounds.min.z, bounds.max.z, gripDepth));
            grip = new Grip { position = -palm, rotation = weapon.localRotation, scale = weapon.localScale,
                muzzle = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, toolIndex == 1 ? .46f : toolIndex == 0 ? .44f : .68f), bounds.max.z + .015f) - palm };
            grips.Add(weapon, grip);
        }
        weapon.localPosition = grip.position;
        weapon.localRotation = grip.rotation;
        weapon.localScale = grip.scale;
        muzzleLocal = grip.muzzle;
        weapon.gameObject.SetActive(true);
    }

    private bool TryLocalBounds(Transform model, out Bounds bounds)
    {
        bounds = new Bounds(); bool found = false;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
            Matrix4x4 matrix = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var local = renderer.localBounds;
            for (int k = 0; k < 8; k++)
            {
                var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
        }
        return found;
    }

    private void LateUpdate() => UpdateGrip(Time.deltaTime);

    // Also used by the editor pose preview; does not start the game or advance world simulation.
    public void UpdateGrip(float seconds)
    {
        if (!IsHandMounted || player == null) return;
        bool holding = equippedWeapon != null && equippedWeapon.gameObject.activeInHierarchy
            && (weapons == null || weapons.CanOperate);
        weight = Mathf.MoveTowards(weight, holding ? gripWeight : 0, Mathf.Max(0, seconds) * poseResponse);
        if (weight <= .001f) return;
        float scale = Mathf.Max(.01f, player.transform.lossyScale.y);
        Vector3 forward = player.transform.forward;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool usingTool = Application.isPlaying && mouse != null && (EcoKeys.FireHeld(mouse) || (weapons != null && weapons.FlashlightOn));
        if (usingTool && !EcoUIFlow.WorldBlocked && EcoIsoCamera.TryCursorPoint(player.transform, out var point))
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up);
            if (flat.sqrMagnitude > .04f)
            {
                float yaw = Mathf.Clamp(Vector3.SignedAngle(player.transform.forward, flat, Vector3.up), -35, 35);
                forward = Quaternion.AngleAxis(yaw, Vector3.up) * player.transform.forward;
            }
        }
        Quaternion aim = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 middle = leftArm != null ? (rightArm.position + leftArm.position) * .5f : rightArm.position - player.transform.right * .1f * scale;
        Vector3 target = middle + aim * new Vector3(.10f, -.095f, .14f) * scale;
        SolveArm(rightArm, rightForearm, rightHand, target,
            middle + aim * new Vector3(.28f, -.20f, -.04f) * scale, weight);
        Quaternion rightRotation = Quaternion.LookRotation(-Vector3.up, -(aim * Vector3.right)) * Quaternion.Inverse(rightPalmFrame);
        rightHand.rotation = Quaternion.Slerp(rightHand.rotation, rightRotation, weight);
        transform.SetPositionAndRotation(rightHand.position + aim * new Vector3(0, -.025f, .015f) * scale, aim);
        // Compact utility tools leave the other arm in its normal walking animation.
        bool oneHanded = toolIndex == 3 || toolIndex == (int)PlayerWeaponSystem.Tool.Flashlight;
        if (!oneHanded && leftArm != null && leftForearm != null && leftHand != null)
        {
            var support = transform.TransformPoint(toolIndex == 2 ? new Vector3(-.07f, -.06f, .06f) : new Vector3(-.07f, .015f, .085f));
            SolveArm(leftArm, leftForearm, leftHand, support,
                middle + aim * new Vector3(-.28f, -.20f, -.04f) * scale, weight);
            var leftRotation = Quaternion.LookRotation(aim * Vector3.right, Vector3.up) * Quaternion.Inverse(leftPalmFrame);
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, leftRotation, weight);
        }
        foreach (var finger in fingers)
        {
            if (finger.bone == null || oneHanded && finger.bone.name.Contains("Left")) continue;
            finger.bone.localRotation = Quaternion.Slerp(finger.bone.localRotation,
                finger.rest * Quaternion.AngleAxis(finger.curl, Vector3.right), weight);
        }
    }

    private static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hint, float amount)
    {
        float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
        Vector3 delta = target - upper.position;
        if (a < .001f || b < .001f || delta.sqrMagnitude < .000001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, (a + b) * .985f);
        Vector3 direction = delta.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(hint - upper.position, direction).normalized;
        if (bend.sqrMagnitude < .001f) bend = Vector3.Cross(direction, Vector3.up).normalized;
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        Vector3 elbow = upper.position + direction * along + bend * height;
        Quaternion upperGoal = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
        upper.rotation = Quaternion.Slerp(upper.rotation, upperGoal, amount);
        Vector3 end = upper.position + direction * distance;
        Quaternion lowerGoal = Quaternion.FromToRotation(hand.position - lower.position, end - lower.position) * lower.rotation;
        lower.rotation = Quaternion.Slerp(lower.rotation, lowerGoal, amount);
    }
}
