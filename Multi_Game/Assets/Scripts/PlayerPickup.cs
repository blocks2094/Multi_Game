using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private CharacterController playerController;

    [Header("감지 설정")]
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField] private LayerMask pickupLayerMask = ~0;
    [SerializeField, Min(0f)] private float detectionInterval = 0.033f;

    [Header("던지기 설정")]
    [SerializeField] private float throwForce = 10f;

    private Transform cameraTransform;
    private PickableObject targetObject;
    private PickableObject heldObject;

    private Collider cachedHitCollider;
    private PickableObject cachedPickableObject;

    private float nextDetectionTime;
    private int lastDetectionFrame = -1;

    private void Awake()
    {
        if (playerCamera == null || holdPoint == null || playerController == null)
        {
            Debug.LogError($"{name}의 PlayerPickup 참조가 설정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        cameraTransform = playerCamera.transform;
    }

    private void Update()
    {
        UpdateDetection();
        HandlePickupInput();
        HandleThrowInput();
    }

    private void UpdateDetection()
    {
        if (heldObject != null || Time.time < nextDetectionTime)
        {
            return;
        }

        nextDetectionTime = Time.time + detectionInterval;
        UpdateTargetObject();
    }

    private void UpdateTargetObject()
    {
        lastDetectionFrame = Time.frameCount;

        if (!Physics.Raycast(
                cameraTransform.position,
                cameraTransform.forward,
                out RaycastHit hit,
                pickupDistance,
                pickupLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            ClearDetection();
            return;
        }

        PickableObject detectedObject = GetPickableObject(hit.collider);

        if (detectedObject == null || detectedObject.IsHeld)
        {
            SetTargetObject(null);
            return;
        }

        SetTargetObject(detectedObject);
    }

    private PickableObject GetPickableObject(Collider hitCollider)
    {
        if (cachedHitCollider == hitCollider)
        {
            return cachedPickableObject;
        }

        cachedHitCollider = hitCollider;
        cachedPickableObject = hitCollider.GetComponentInParent<PickableObject>();

        return cachedPickableObject;
    }

    private void SetTargetObject(PickableObject newTargetObject)
    {
        if (targetObject == newTargetObject)
        {
            return;
        }

        if (targetObject != null)
        {
            targetObject.SetHighlighted(false);
        }

        targetObject = newTargetObject;

        if (targetObject != null)
        {
            targetObject.SetHighlighted(true);
        }
    }

    private void HandlePickupInput()
    {
        if (!Input.GetKeyDown(KeyCode.F))
        {
            return;
        }

        if (heldObject != null)
        {
            DropObject();
            return;
        }

        if (lastDetectionFrame != Time.frameCount)
        {
            UpdateTargetObject();
            nextDetectionTime = Time.time + detectionInterval;
        }

        PickUpObject();
    }

    private void HandleThrowInput()
    {
        if (heldObject == null || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        ThrowObject();
    }

    private void PickUpObject()
    {
        if (targetObject == null)
        {
            return;
        }

        heldObject = targetObject;
        SetTargetObject(null);
        ClearDetectionCache();

        heldObject.PickUp(holdPoint, playerController);
    }

    private void DropObject()
    {
        heldObject.Drop(playerController);
        heldObject = null;
        nextDetectionTime = Time.time + detectionInterval;
    }

    private void ThrowObject()
    {
        heldObject.Throw(playerController, cameraTransform.forward, throwForce);
        heldObject = null;
        nextDetectionTime = Time.time + detectionInterval;
    }

    private void ClearDetection()
    {
        ClearDetectionCache();
        SetTargetObject(null);
    }

    private void ClearDetectionCache()
    {
        cachedHitCollider = null;
        cachedPickableObject = null;
    }

    private void OnDisable()
    {
        ClearDetection();

        if (heldObject != null)
        {
            DropObject();
        }
    }
}