using UnityEngine;
using UnityEngine.VFX;

// Impact response adapted from Daniel Ilett's Energy Shield Hologram (MIT).
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer), typeof(MeshCollider))]
public sealed class EnergyShieldImpact : MonoBehaviour
{
    private static readonly int RippleOrigin = Shader.PropertyToID("_Ripple_Origin");
    private static readonly int RippleTime = Shader.PropertyToID("_Ripple_Time");
    private static readonly int RippleThickness = Shader.PropertyToID("_Ripple_Thickness");
    private static readonly int RippleSpeed = Shader.PropertyToID("_Ripple_Speed");
    private static readonly int AspectRatio = Shader.PropertyToID("_Aspect_Ratio");

    [SerializeField] private InputReader input;
    [SerializeField] private VisualEffect sparks;
    [SerializeField, Min(0)] private float rippleCooldown = 0.4f;
    [SerializeField, Min(0)] private float shakeStrength = 0.03f;
    [SerializeField, Min(0)] private float shakeDuration = 0.25f;

    private MeshRenderer shieldRenderer;
    private MeshCollider hitSurface;
    private MaterialPropertyBlock originalProperties;
    private MaterialPropertyBlock impactProperties;
    private float nextHitTime;
    private float hitTime;
    private float rippleStart;
    private float rippleEnd;
    private bool rippleActive;
    private bool shaking;
    private Vector3 restLocalPosition;

    private void Awake()
    {
        shieldRenderer = GetComponent<MeshRenderer>();
        hitSurface = GetComponent<MeshCollider>();
        originalProperties = new MaterialPropertyBlock();
        impactProperties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (input != null)
            input.Click += OnShieldClick;
    }

    private void OnDisable()
    {
        if (input != null)
            input.Click -= OnShieldClick;

        FinishRipple();
        FinishShake();
        nextHitTime = 0;
    }

    private void OnShieldClick(RaycastHit hit)
    {
        float now = Time.time;
        if (hit.collider != hitSurface || now < nextHitTime)
            return;

        if (!rippleActive)
            shieldRenderer.GetPropertyBlock(originalProperties);
        shieldRenderer.GetPropertyBlock(impactProperties);

        Material material = shieldRenderer.sharedMaterial;
        rippleStart = -2 * material.GetFloat(RippleThickness);
        float aspect = material.GetFloat(AspectRatio);
        // Once the radius exceeds the full UV diagonal, the ring is off the shield.
        rippleEnd = Mathf.Sqrt(1 + aspect * aspect) / Mathf.Max(0.01f, material.GetFloat(RippleSpeed));
        hitTime = now;
        nextHitTime = now + rippleCooldown;
        rippleActive = true;

        Vector2 uv = hit.textureCoord;
        impactProperties.SetVector(RippleOrigin, new Vector4(uv.x, uv.y, 0, 0));
        SetRippleTime(rippleStart);

        if (sparks != null)
        {
            sparks.transform.SetPositionAndRotation(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
            // Restart the local-space burst so old particles do not move to the new hit.
            sparks.Reinit();
            sparks.Play();
        }

        if (!shaking)
            restLocalPosition = transform.localPosition;
        shaking = true;
    }

    private void LateUpdate()
    {
        float elapsed = Time.time - hitTime;
        if (rippleActive)
        {
            float time = rippleStart + elapsed;
            if (time > rippleEnd)
                FinishRipple();
            else
                SetRippleTime(time);
        }

        if (shaking)
        {
            if (elapsed >= shakeDuration)
            {
                FinishShake();
            }
            else
            {
                // Match the tutorial's world-space jitter despite the model's 5x parent scale.
                Vector3 offset = Random.insideUnitSphere * shakeStrength;
                transform.localPosition = restLocalPosition + (transform.parent ? transform.parent.InverseTransformVector(offset) : offset);
            }
        }
    }

    private void SetRippleTime(float time)
    {
        impactProperties.SetFloat(RippleTime, time);
        shieldRenderer.SetPropertyBlock(impactProperties);
    }

    private void FinishRipple()
    {
        if (!rippleActive) return;
        shieldRenderer.SetPropertyBlock(originalProperties);
        rippleActive = false;
    }

    private void FinishShake()
    {
        if (!shaking) return;
        transform.localPosition = restLocalPosition;
        shaking = false;
    }
}
