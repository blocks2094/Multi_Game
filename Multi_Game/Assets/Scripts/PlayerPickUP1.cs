using UnityEngine;

public class PlayerPickUP1 : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private CharacterController playerController;

    [Header("감지 설정")]
    [SerializeField] private float pickupDistance = 3f;
    [SerializeField, Min(0.01f)] private float sphereRadius = 0.15f;
    [SerializeField] private LayerMask pickupLayerMask = ~0;

    [Header("던지기 설정")]
    [SerializeField] private float throwForce = 10f;

    [Header("기즈모 설정")]
    [SerializeField] private bool showSphereCastGizmo = true;

    private PickableObject targetObject;
    private PickableObject heldObject;

    private void Update()
    {
        UpdateTargetObject();
        HandlePickupInput();
        HandleThrowInput();
    }

    private void UpdateTargetObject()
    {
        if (heldObject != null)
        {
            SetTargetObject(null);
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!Physics.SphereCast(
                ray,
                sphereRadius,
                out RaycastHit hit,
                pickupDistance,
                pickupLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            SetTargetObject(null);
            return;
        }

        PickableObject detectedObject = hit.collider.GetComponentInParent<PickableObject>();

        if (detectedObject == null || detectedObject.IsHeld)
        {
            SetTargetObject(null);
            return;
        }

        SetTargetObject(detectedObject);
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

        heldObject.PickUp(holdPoint, playerController);
    }

    private void DropObject()
    {
        heldObject.Drop(playerController);
        heldObject = null;
    }

    private void ThrowObject()
    {
        heldObject.Throw(playerController, playerCamera.transform.forward, throwForce);
        heldObject = null;
    }

    private void OnDisable()
    {
        SetTargetObject(null);

        if (heldObject != null)
        {
            DropObject();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSphereCastGizmo || playerCamera == null)
        {
            return;
        }

        Transform cameraTransform = playerCamera.transform;

        Vector3 startPosition = cameraTransform.position;
        Vector3 direction = cameraTransform.forward.normalized;
        Vector3 endPosition = startPosition + direction * pickupDistance;

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(startPosition, sphereRadius);
        Gizmos.DrawWireSphere(endPosition, sphereRadius);

        DrawSphereCastLines(
            startPosition,
            endPosition,
            cameraTransform.right,
            cameraTransform.up);

        if (Physics.SphereCast(
                startPosition,
                sphereRadius,
                direction,
                out RaycastHit hit,
                pickupDistance,
                pickupLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            Vector3 hitSpherePosition = startPosition + direction * hit.distance;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitSpherePosition, sphereRadius);
            Gizmos.DrawSphere(hit.point, 0.04f);
        }
    }

    private void DrawSphereCastLines(
        Vector3 startPosition,
        Vector3 endPosition,
        Vector3 rightDirection,
        Vector3 upDirection)
    {
        const int lineCount = 8;

        for (int i = 0; i < lineCount; i++)
        {
            float angle = i * Mathf.PI * 2f / lineCount;

            Vector3 offset =
                (rightDirection * Mathf.Cos(angle) +
                 upDirection * Mathf.Sin(angle)) *
                sphereRadius;

            Gizmos.DrawLine(
                startPosition + offset,
                endPosition + offset);
        }
    }
}