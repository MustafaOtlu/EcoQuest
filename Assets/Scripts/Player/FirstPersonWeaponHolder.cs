using UnityEngine;

/// <summary>Keeps the equipped weapon attached to the view and outside its near clipping plane.</summary>
[DisallowMultipleComponent]
public sealed class FirstPersonWeaponHolder : MonoBehaviour
{
    [Tooltip("The first-person camera transform. The holder must be its child.")]
    [SerializeField] private Transform viewTransform;
    [SerializeField] private Transform equippedWeapon;
    [Tooltip("Minimum distance from the camera to the back of the weapon, in metres.")]
    [Min(0.02f)] [SerializeField] private float minimumDepth = 0.06f;

    [Header("Weapon Motion — Idle / Walk / Run")]
    [Tooltip("Vertical motion in metres for idle, walking and running.")]
    [SerializeField] private Vector3 motionAmplitudes = new Vector3(0.002f, 0.008f, 0.015f);
    [Tooltip("Sway cycles per second for idle, walking and running.")]
    [SerializeField] private Vector3 motionFrequencies = new Vector3(0.25f, 0.85f, 1.3f);
    [Tooltip("Maximum tilt in degrees for idle, walking and running.")]
    [SerializeField] private Vector3 motionTilts = new Vector3(0.15f, 0.6f, 1.2f);
    [Min(0.1f)] [SerializeField] private float motionResponse = 8f;

    private Renderer[] weaponRenderers;
    private PlayerController player;
    private CharacterController movement;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 weaponRestPosition;
    private float motionBlend;
    private float phase;
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");

    public Transform EquippedWeapon => equippedWeapon;

    public void Unequip()
    {
        if (equippedWeapon != null)
        {
            equippedWeapon.localPosition = weaponRestPosition;
            equippedWeapon.gameObject.SetActive(false);
        }
        equippedWeapon = null;
        weaponRenderers = null;
    }

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        player = GetComponentInParent<PlayerController>();
        movement = GetComponentInParent<CharacterController>();
        if (viewTransform == null) viewTransform = transform.parent;
        if (equippedWeapon != null) Equip(equippedWeapon);
    }

    // Pass a scene instance, not a prefab asset. Its authored position and rotation are preserved.
    public void Equip(Transform weapon)
    {
        if (weapon == null || weapon == transform || transform.IsChildOf(weapon)) return;
        // Undo the camera safety offset before caching or switching weapons.
        if (equippedWeapon != null && weaponRenderers != null)
            equippedWeapon.localPosition = weaponRestPosition;
        if (equippedWeapon != null && equippedWeapon != weapon)
            equippedWeapon.gameObject.SetActive(false);
        equippedWeapon = weapon;
        equippedWeapon.SetParent(transform, true);
        weaponRestPosition = equippedWeapon.localPosition;
        equippedWeapon.gameObject.SetActive(true);
        weaponRenderers = equippedWeapon.GetComponentsInChildren<Renderer>();
        KeepOutsideCamera();
    }

    private void LateUpdate()
    {
        if (equippedWeapon == null || !equippedWeapon.gameObject.activeInHierarchy) return;

        // Share the character's smoothed Idle/Walk/Run blend, including actual movement speed.
        float targetBlend = player != null && player.characterAnimator != null
            ? player.characterAnimator.GetFloat(SpeedParameter) : 0f;
        if (movement != null && !movement.isGrounded) targetBlend = 0f;
        float smoothing = 1f - Mathf.Exp(-motionResponse * Time.deltaTime);
        motionBlend = Mathf.Lerp(motionBlend, Mathf.Clamp01(targetBlend), smoothing);
        float amplitude = BlendMotion(motionAmplitudes, motionBlend);
        float frequency = BlendMotion(motionFrequencies, motionBlend);
        float tilt = BlendMotion(motionTilts, motionBlend);
        phase = Mathf.Repeat(phase + Time.deltaTime * frequency * Mathf.PI * 2f, Mathf.PI * 2f);

        float sideways = Mathf.Sin(phase);
        float vertical = Mathf.Sin(phase * 2f);
        transform.localPosition = restPosition + restRotation * new Vector3(
            sideways * amplitude * 0.5f, vertical * amplitude, 0f);
        transform.localRotation = restRotation * Quaternion.Euler(vertical * tilt * 0.5f, 0f, -sideways * tilt);

        // Recompute safety from the authored pose every frame, so bobbing cannot push the gun
        // farther forward cumulatively. The holder always oscillates around its original pose.
        equippedWeapon.localPosition = weaponRestPosition;
        KeepOutsideCamera();
    }

    private static float BlendMotion(Vector3 values, float blend)
    {
        return blend <= 0.5f
            ? Mathf.Lerp(values.x, values.y, blend * 2f)
            : Mathf.Lerp(values.y, values.z, (blend - 0.5f) * 2f);
    }

    private void OnDisable()
    {
        transform.localPosition = restPosition;
        transform.localRotation = restRotation;
        if (equippedWeapon != null && weaponRenderers != null)
            equippedWeapon.localPosition = weaponRestPosition;
        phase = 0f;
        motionBlend = 0f;
    }

    private void KeepOutsideCamera()
    {
        if (viewTransform == null || equippedWeapon == null || weaponRenderers == null) return;
        float nearestDepth = float.PositiveInfinity;
        foreach (var weaponRenderer in weaponRenderers)
        {
            if (weaponRenderer == null || !weaponRenderer.enabled) continue;
            Bounds bounds = weaponRenderer.localBounds;
            Matrix4x4 toView = viewTransform.worldToLocalMatrix * weaponRenderer.transform.localToWorldMatrix;
            // Local bounds avoid a world-space bounding box growing as the player turns.
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                nearestDepth = Mathf.Min(nearestDepth, toView.MultiplyPoint3x4(point).z);
            }
        }
        if (nearestDepth < minimumDepth)
            equippedWeapon.position += viewTransform.TransformVector(Vector3.forward * (minimumDepth - nearestDepth));
    }
}
